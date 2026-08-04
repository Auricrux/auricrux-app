<#
.SYNOPSIS
  Verify runtime truth endpoint exists in package and (optionally) probe live host.
  Operational truth only — no secrets expected in payload.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [switch]$SkipLiveProbe
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

Write-Host '=== Runtime truth endpoint audit ===' -ForegroundColor Cyan

$svc = Join-Path $repoRoot 'Auricrux.Web\Services\RuntimeTruthService.cs'
$ctl = Join-Path $repoRoot 'Auricrux.Web\Controllers\AuricruxApiController.cs'
$doc = Join-Path $repoRoot 'docs\runtime-proof\RUNTIME_TRUTH.md'
$appsettings = Join-Path $repoRoot 'Auricrux.Web\appsettings.json'

Add-Check 'RT-01-service' $(if (Test-Path $svc) { 'PASS' } else { 'FAIL' }) 'RuntimeTruthService.cs'
Add-Check 'RT-02-route' $(if ((Test-Path $ctl) -and ((Get-Content $ctl -Raw) -match 'runtime-truth')) { 'PASS' } else { 'FAIL' }) 'GET /api/runtime-truth mapped'
Add-Check 'RT-03-docs' $(if (Test-Path $doc) { 'PASS' } else { 'FAIL' }) 'RUNTIME_TRUTH.md'

if (Test-Path $svc) {
    $src = Get-Content $svc -Raw
    $need = @('ActiveModel', 'ActivePackageVersion', 'ActiveDllVersion', 'CorpusVersion', 'HostProfile', 'RecipeProfile', 'SuiteCompatibility', 'BuildTimestampUtc', 'DeploymentSource', 'FallbackModeActive')
    $missing = @($need | Where-Object { $src -notmatch $_ })
    if ($missing.Count -gt 0) {
        Add-Check 'RT-04-fields' 'FAIL' ("Missing truth fields: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'RT-04-fields' 'PASS' 'Required operational truth fields present'
    }
    $forbidden = @(
        @{ id = 'ConnectionString'; pat = '(?i)\bConnectionString\b' },
        @{ id = 'Password'; pat = '(?i)\bPassword\b' },
        @{ id = 'ApiKey'; pat = '(?i)\bApiKey\b' },
        @{ id = 'PrivateKey'; pat = '(?i)\bPrivateKey\b' }
    )
    $hit = @($forbidden | Where-Object { $src -match $_.pat } | ForEach-Object { $_.id })
    if ($hit.Count -gt 0) {
        Add-Check 'RT-05-no-secrets' 'FAIL' ("Secret-like identifiers in truth service: {0}" -f ($hit -join ', '))
    } else {
        Add-Check 'RT-05-no-secrets' 'PASS' 'No secret-like field names in RuntimeTruthService'
    }
}

if (Test-Path $appsettings) {
    $as = Get-Content $appsettings -Raw | ConvertFrom-Json
    if ($as.Auricrux.HostProfile -and $as.Auricrux.RecipeProfile -and $as.Auricrux.DeploymentSource) {
        Add-Check 'RT-06-appsettings' 'PASS' ("host={0} recipe={1} deploy={2}" -f $as.Auricrux.HostProfile, $as.Auricrux.RecipeProfile, $as.Auricrux.DeploymentSource)
    } else {
        Add-Check 'RT-06-appsettings' 'FAIL' 'appsettings missing HostProfile/RecipeProfile/DeploymentSource'
    }
}

if ($SkipLiveProbe) {
    Add-Check 'RT-07-live' 'WARN' 'Skipped (-SkipLiveProbe)'
} else {
    try {
        $r = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45
        $needLive = @('activeModel', 'activePackageVersion', 'activeDllVersion', 'corpusVersion', 'hostProfile', 'recipeProfile', 'suiteCompatibility', 'buildTimestampUtc', 'deploymentSource', 'fallbackModeActive')
        # System.Text.Json camelCase by default in ASP.NET
        $props = @($r.PSObject.Properties.Name)
        $miss = @()
        foreach ($n in $needLive) {
            $found = $props | Where-Object { $_.ToLowerInvariant() -eq $n.ToLowerInvariant() }
            if (-not $found) { $miss += $n }
        }
        if ($miss.Count -gt 0) {
            Add-Check 'RT-07-live' 'FAIL' ("Live truth missing fields (deploy package?): {0}" -f ($miss -join ', '))
        } else {
            $blob = ($r | ConvertTo-Json -Depth 6 -Compress)
            if ($blob -match '(?i)(password|connectionstring|api[_-]?key|BEGIN (RSA )?PRIVATE)') {
                Add-Check 'RT-07-live' 'FAIL' 'Live truth payload appears to contain secrets'
            } else {
                Add-Check 'RT-07-live' 'PASS' ("Live truth model={0} package={1} fallback={2}" -f $r.activeModel, $r.activePackageVersion, $r.fallbackModeActive)
            }
        }
    } catch {
        $msg = $_.Exception.Message
        if ($msg -match '404') {
            Add-Check 'RT-07-live' 'WARN' 'Live host 404 on /api/runtime-truth — package cutover required'
        } else {
            Add-Check 'RT-07-live' 'FAIL' ("Live probe failed: {0}" -f $msg)
        }
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'RUNTIME_TRUTH_OK' } else { 'RUNTIME_TRUTH_BLOCKED' }

$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    checks = $checks
}
$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receiptPath = Join-Path $receiptDir 'runtime-truth-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
if ($fail -gt 0) { exit 1 }
Write-Host 'RUNTIME_TRUTH_OK'
exit 0
