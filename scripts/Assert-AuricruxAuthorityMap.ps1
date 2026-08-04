<#
.SYNOPSIS
  Assert Auricrux definitive authority map is present, unambiguous, and matches current truth.
.DESCRIPTION
  Does not promote PASS. Verifies policy + docs + ledger/manifest alignment with authority chain.
  Token: AUTHORITY_MAP_OK / AUTHORITY_MAP_BLOCKED
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

Write-Host '=== Auricrux authority map assert ===' -ForegroundColor Cyan
Write-Host 'No PASS promotion. Evidence-based transitions only.'

$policy = Join-Path $repoRoot 'auricrux\system\auricrux_authority_chain_v1.json'
$doc = Join-Path $repoRoot 'docs\runtime-proof\AURICRUX_AUTHORITY_MAP.md'
$ledger = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
$manifest = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
$ledgerWriter = Join-Path $repoRoot 'scripts\Write-GgufSuiteEvidenceLedger.ps1'

Add-Check 'AM-01-policy' $(if (Test-Path $policy) { 'PASS' } else { 'FAIL' }) 'auricrux_authority_chain_v1.json'
Add-Check 'AM-02-doc' $(if (Test-Path $doc) { 'PASS' } else { 'FAIL' }) 'AURICRUX_AUTHORITY_MAP.md'

if (Test-Path $policy) {
    $p = Get-Content $policy -Raw | ConvertFrom-Json
    $need = @('liveSuiteAuthority', 'manifestAuthority', 'promotionAuthority', 'deploymentAuthority')
    $keys = @($p.transitions.PSObject.Properties.Name)
    $miss = @($need | Where-Object { $_ -notin $keys })
    if ($miss.Count -gt 0) {
        Add-Check 'AM-03-transitions' 'FAIL' ("Missing transitions: {0}" -f ($miss -join ','))
    } else {
        Add-Check 'AM-03-transitions' 'PASS' 'All four authority transitions defined'
    }
    $passReq = @($p.transitions.liveSuiteAuthority.mayBecomePassWhen_ALL_REQUIRED)
    if ($passReq.Count -lt 6 -or (($passReq | Out-String) -notmatch 'packageIdentity') -or (($passReq | Out-String) -notmatch 'no live model')) {
        Add-Check 'AM-04-live-pass-qualifiers' 'FAIL' 'Live PASS qualifiers incomplete (need packageIdentity + no-fallback)'
    } else {
        Add-Check 'AM-04-live-pass-qualifiers' 'PASS' 'Live PASS requires packageIdentity + zero fallback contamination'
    }
}

if (Test-Path $doc) {
    $d = Get-Content $doc -Raw
    $bits = @('Live suite authority', 'Manifest authority', 'Promotion authority', 'Deployment authority', 'Disqualifies PASS', 'Dependency order')
    $missing = @($bits | Where-Object { $d -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        Add-Check 'AM-05-doc-sections' 'FAIL' ("Doc missing: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'AM-05-doc-sections' 'PASS' 'Authority map doc covers all four + disqualify + order'
    }
}

if (Test-Path $ledgerWriter) {
    $lw = Get-Content $ledgerWriter -Raw
    if ($lw -match 'live-dated-host-validation-disqualified' -and $lw -match 'fallbackContamination' -and $lw -match 'currentLiveAuthority') {
        Add-Check 'AM-06-ledger-qualify' 'PASS' 'Ledger writer qualifies PASS and guards currentLiveAuthority'
    } else {
        Add-Check 'AM-06-ledger-qualify' 'FAIL' 'Ledger writer missing PASS qualification / currentLiveAuthority guard'
    }
} else {
    Add-Check 'AM-06-ledger-qualify' 'FAIL' 'Write-GgufSuiteEvidenceLedger.ps1 missing'
}

# Current truth: no false PASS
if (Test-Path $ledger) {
    $led = Get-Content $ledger -Raw | ConvertFrom-Json
    $auth = $led.currentLiveAuthority
    if (-not $auth) {
        Add-Check 'AM-07-live-pointer' 'FAIL' 'Ledger missing currentLiveAuthority'
    } else {
        $st = [string]$auth.status
        $rate = [double]$auth.passRatePercent
        if ($st -eq 'PASS' -and $rate -ge 80) {
            # Verify cited report still qualifies
            $repRel = [string]$auth.report
            $repAbs = Join-Path $repoRoot ($repRel -replace '/', '\')
            if (-not (Test-Path $repAbs)) {
                Add-Check 'AM-07-live-pointer' 'FAIL' ("Authority PASS cites missing report: {0}" -f $repRel)
            } else {
                $rep = Get-Content $repAbs -Raw | ConvertFrom-Json
                $blob = ($rep | ConvertTo-Json -Depth 6 -Compress)
                if ($blob -match 'no live model reachable' -or $blob -match 'corpus-fallback') {
                    Add-Check 'AM-07-live-pointer' 'FAIL' 'currentLiveAuthority PASS cites fallback-contaminated report'
                } else {
                    Add-Check 'AM-07-live-pointer' 'PASS' ("currentLiveAuthority PASS rate={0} (appears clean)" -f $rate)
                }
            }
        } elseif ($st -eq 'FAIL') {
            Add-Check 'AM-07-live-pointer' 'PASS' ("currentLiveAuthority FAIL rate={0} (truthful)" -f $rate)
        } else {
            Add-Check 'AM-07-live-pointer' 'FAIL' ("Unexpected authority status={0}" -f $st)
        }
    }
} else {
    Add-Check 'AM-07-live-pointer' 'FAIL' 'Evidence ledger missing'
}

if (Test-Path $manifest) {
    $m = Get-Content $manifest -Raw | ConvertFrom-Json
    $claimsPass = [bool]$m.adapter.ggufGenerativeSuitePassed -or ([string]$m.adapter.evalStatus -match 'PASS' -and [string]$m.adapter.evalStatus -notmatch 'FAIL')
    $led2 = if (Test-Path $ledger) { Get-Content $ledger -Raw | ConvertFrom-Json } else { $null }
    $livePass = $led2 -and $led2.currentLiveAuthority -and [string]$led2.currentLiveAuthority.status -eq 'PASS'
    if ($claimsPass -and -not $livePass) {
        Add-Check 'AM-08-manifest-align' 'FAIL' 'Manifest claims PASS without currentLiveAuthority PASS'
    } elseif (-not $claimsPass) {
        Add-Check 'AM-08-manifest-align' 'PASS' ("Manifest does not claim PASS (eval={0})" -f $m.adapter.evalStatus)
    } else {
        Add-Check 'AM-08-manifest-align' 'PASS' 'Manifest PASS aligned with live authority PASS'
    }
}

# Ambiguity killers present in policy
if (Test-Path $policy) {
    $pt = Get-Content $policy -Raw
    if ($pt -match 'packageWebCutover' -and $pt -match 'modelWeightCutover' -and $pt -match 'doesNotGrant') {
        Add-Check 'AM-09-deploy-kinds' 'PASS' 'Deployment splits package-web vs model-weight; package cutover doesNotGrant PASS'
    } else {
        Add-Check 'AM-09-deploy-kinds' 'FAIL' 'Deployment kinds ambiguous in policy'
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'AUTHORITY_MAP_OK' } else { 'AUTHORITY_MAP_BLOCKED' }

$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    checks = $checks
    policy = 'auricrux/system/auricrux_authority_chain_v1.json'
    doc = 'docs/runtime-proof/AURICRUX_AUTHORITY_MAP.md'
}
$receiptPath = Join-Path $repoRoot 'docs\runtime-proof\authority-map-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
if ($fail -gt 0) { Write-Host 'AUTHORITY_MAP_BLOCKED'; exit 1 }
Write-Host 'AUTHORITY_MAP_OK'
exit 0
