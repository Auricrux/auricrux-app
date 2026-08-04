<#
.SYNOPSIS
  Enforce product fallback / recipe profile: product must not silently run interim fallback.
.DESCRIPTION
  Verifies ForceCorpusFallback=false, PrimaryModel=auricrux-fca, RecipeProfile=product_gguf_serve_v1,
  HostProfile=product-gce, and Ollama init safety (dev-fallback explicit only).
  Token: PRODUCT_FALLBACK_PROFILE_OK / PRODUCT_FALLBACK_PROFILE_BLOCKED
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

Write-Host '=== Product fallback / recipe profile enforcement ===' -ForegroundColor Cyan

$appsettings = Join-Path $repoRoot 'Auricrux.Web\appsettings.json'
if (-not (Test-Path $appsettings)) {
    Add-Check 'FP-01-appsettings' 'FAIL' 'appsettings.json missing'
} else {
    try {
        $as = Get-Content $appsettings -Raw | ConvertFrom-Json
        $a = $as.Auricrux
        if ([string]$a.PrimaryModel -ne 'auricrux-fca') {
            Add-Check 'FP-01-primary' 'FAIL' ("PrimaryModel={0} (expected auricrux-fca)" -f $a.PrimaryModel)
        } else {
            Add-Check 'FP-01-primary' 'PASS' 'PrimaryModel=auricrux-fca'
        }
        if ([bool]$a.ForceCorpusFallback -eq $true) {
            Add-Check 'FP-02-force-corpus' 'FAIL' 'ForceCorpusFallback=true (unsafe for product)'
        } else {
            Add-Check 'FP-02-force-corpus' 'PASS' 'ForceCorpusFallback=false'
        }
        if ([string]$a.RecipeProfile -ne 'product_gguf_serve_v1') {
            Add-Check 'FP-03-recipe' 'FAIL' ("RecipeProfile={0} (expected product_gguf_serve_v1)" -f $a.RecipeProfile)
        } else {
            Add-Check 'FP-03-recipe' 'PASS' 'RecipeProfile=product_gguf_serve_v1'
        }
        if ([string]$a.HostProfile -ne 'product-gce') {
            Add-Check 'FP-04-host' 'FAIL' ("HostProfile={0} (expected product-gce)" -f $a.HostProfile)
        } else {
            Add-Check 'FP-04-host' 'PASS' 'HostProfile=product-gce'
        }
    } catch {
        Add-Check 'FP-01-appsettings' 'FAIL' ("appsettings parse failed: {0}" -f $_.Exception.Message)
    }
}

$ollamaAssert = Join-Path $PSScriptRoot 'Assert-OllamaInitSafety.ps1'
if (Test-Path $ollamaAssert) {
    & $ollamaAssert | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'FP-05-ollama-init' 'FAIL' 'OLLAMA_INIT_SAFETY_BLOCKED'
    } else {
        Add-Check 'FP-05-ollama-init' 'PASS' 'OLLAMA_INIT_SAFETY_OK (dev-fallback explicit only)'
    }
} else {
    Add-Check 'FP-05-ollama-init' 'FAIL' 'Assert-OllamaInitSafety.ps1 missing'
}

$doc = Join-Path $repoRoot 'docs\runtime-proof\OLLAMA_INIT_SAFE_UNSAFE_PATHS.md'
Add-Check 'FP-06-docs' $(if (Test-Path $doc) { 'PASS' } else { 'FAIL' }) 'OLLAMA_INIT_SAFE_UNSAFE_PATHS.md'

if ($SkipLiveProbe) {
    Add-Check 'FP-07-live' 'WARN' 'Skipped live probe'
} else {
    try {
        $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
        $pm = [string]$h.primaryModel
        $mode = [string]$h.runtimeMode
        $bad = ($pm -match 'dev-fallback') -or ($pm -match '^llama3\.2') -or ($mode -match 'corpus-fallback')
        if ($bad) {
            Add-Check 'FP-07-live' 'FAIL' ("Live fallback active model={0} mode={1}" -f $pm, $mode)
        } else {
            Add-Check 'FP-07-live' 'PASS' ("Live model={0} mode={1} (not interim fallback)" -f $pm, $mode)
        }
    } catch {
        Add-Check 'FP-07-live' 'FAIL' ("Live health probe failed: {0}" -f $_.Exception.Message)
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'PRODUCT_FALLBACK_PROFILE_OK' } else { 'PRODUCT_FALLBACK_PROFILE_BLOCKED' }

$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    checks = $checks
}
$receiptPath = Join-Path $repoRoot 'docs\runtime-proof\product-fallback-profile-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
if ($fail -gt 0) { Write-Host 'PRODUCT_FALLBACK_PROFILE_BLOCKED'; exit 1 }
Write-Host 'PRODUCT_FALLBACK_PROFILE_OK'
exit 0
