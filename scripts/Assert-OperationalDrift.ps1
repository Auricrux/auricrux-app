<#
.SYNOPSIS
  Operational drift checks: detect stale publish/corpus/manifest/ledger/runtime/deploy artifacts.
.DESCRIPTION
  FAIL only on high-confidence identity mismatches. WARN for expected pre-cutover and soft age.
  Does not start training, cutover, or delete data.
.PARAMETER ProbeLive
  Compare against product host packageIdentity / runtime-truth when reachable.
.PARAMETER StrictAge
  Elevate soft age WARNs to FAIL.
.EXAMPLE
  .\scripts\Assert-OperationalDrift.ps1
  .\scripts\Assert-OperationalDrift.ps1 -ProbeLive
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$PublishDir = '',
    [switch]$ProbeLive,
    [switch]$StrictAge
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$checks = New-Object System.Collections.Generic.List[hashtable]
function Add-Check([string]$Id, [string]$Status, [string]$Detail, [string]$Class = '') {
    [void]$checks.Add(@{ id = $Id; status = $Status; detail = $Detail; class = $Class })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } 'WARN' { 'Yellow' } default { 'Gray' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Get-Sha256Lower([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Read-JsonSafe([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) } catch { return $null }
}

function Get-AgeHours([string]$UtcStamp) {
    if ([string]::IsNullOrWhiteSpace($UtcStamp)) { return $null }
    try {
        $dt = [DateTime]::Parse($UtcStamp, $null, [System.Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        return [math]::Round(((Get-Date).ToUniversalTime() - $dt).TotalHours, 1)
    } catch { return $null }
}

Write-Host '=== Operational drift checks ===' -ForegroundColor Cyan
Write-Host 'FAIL = hard identity mismatch. WARN = soft/expected. Live probes off unless -ProbeLive.'

$policyPath = Join-Path $repoRoot 'auricrux\system\auricrux_operational_drift_v1.json'
$pol = Read-JsonSafe $policyPath
if (-not $pol) {
    Add-Check 'OD-00-policy' 'FAIL' 'auricrux_operational_drift_v1.json missing' 'policy'
} else {
    Add-Check 'OD-00-policy' 'PASS' $pol.policyId 'policy'
}

$maxPreparedAge = 168.0
$maxLedgerLag = 72.0
if ($pol -and $pol.softAge) {
    if ($pol.softAge.maxPreparedAgeHours) { $maxPreparedAge = [double]$pol.softAge.maxPreparedAgeHours }
    if ($pol.softAge.maxLedgerLagHours) { $maxLedgerLag = [double]$pol.softAge.maxLedgerLagHours }
}

# Paths
$repoStamp = Join-Path $repoRoot 'auricrux\system\package_stamp.json'
$pubStamp = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
if (-not (Test-Path $pubStamp)) { $pubStamp = Join-Path $PublishDir 'Data\package_stamp.json' }
$repoManifest = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
$pubManifest = Join-Path $PublishDir 'auricrux\system\model_manifest.json'
$srcCorpus = Join-Path $repoRoot 'Auricrux.Web\Data\construction-corpus.json'
$pubCorpus = Join-Path $PublishDir 'Data\construction-corpus.json'
$pubDll = Join-Path $PublishDir 'Auricrux.Web.dll'
$ledgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
$jsonlPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.jsonl'
$preparedPath = Join-Path $repoRoot 'docs\runtime-proof\package-prepared-latest.json'
$gonoPath = Join-Path $repoRoot 'docs\runtime-proof\cutover-go-no-go-latest.json'

$rs = Read-JsonSafe $repoStamp
$ps = Read-JsonSafe $pubStamp
$rm = Read-JsonSafe $repoManifest
$pm = Read-JsonSafe $pubManifest
$ledger = Read-JsonSafe $ledgerPath
$prepared = Read-JsonSafe $preparedPath
$gono = Read-JsonSafe $gonoPath

$srcCorpusSha = Get-Sha256Lower $srcCorpus
$pubCorpusSha = Get-Sha256Lower $pubCorpus
$pubDllSha = Get-Sha256Lower $pubDll

# ---------- OD-10 publish package ----------
if (-not (Test-Path $PublishDir) -or -not (Test-Path $pubDll)) {
    Add-Check 'OD-10-publish-present' 'FAIL' "_publish/web incomplete: $PublishDir" 'stale_publish_package'
} else {
    Add-Check 'OD-10-publish-present' 'PASS' 'Publish DLL present' 'stale_publish_package'
}

if ($rs -and $ps) {
    $rv = [string]$rs.packageVersion
    $pv = [string]$ps.packageVersion
    if ($rv -and $pv -and $rv -ne $pv) {
        Add-Check 'OD-11-stamp-version' 'FAIL' ("Repo stamp {0} != publish stamp {1}" -f $rv, $pv) 'stale_publish_package'
    } else {
        Add-Check 'OD-11-stamp-version' 'PASS' ("packageVersion={0}" -f $pv) 'stale_publish_package'
    }
    $rds = [string]$rs.deploymentSource
    $pds = [string]$ps.deploymentSource
    if ($rds -and $pds -and $rds -ne $pds) {
        Add-Check 'OD-12-stamp-source' 'WARN' ("deploymentSource repo={0} publish={1}" -f $rds, $pds) 'stale_publish_package'
    } else {
        Add-Check 'OD-12-stamp-source' 'PASS' ("deploymentSource={0}" -f $pds) 'stale_publish_package'
    }
} elseif (-not $ps) {
    Add-Check 'OD-11-stamp-version' 'FAIL' 'Publish package_stamp.json missing' 'stale_publish_package'
} else {
    Add-Check 'OD-11-stamp-version' 'WARN' 'Repo package_stamp missing; publish only' 'stale_publish_package'
}

if ($prepared -and $prepared.package -and $pubDllSha) {
    $expDll = [string]$prepared.package.dllSha256
    if ($expDll -and $expDll.ToLowerInvariant() -ne $pubDllSha) {
        Add-Check 'OD-13-prepared-dll' 'FAIL' ("prepared dllSha {0} != publish {1}" -f $expDll.Substring(0, [Math]::Min(12, $expDll.Length)), $pubDllSha.Substring(0, 12)) 'stale_publish_package'
    } else {
        Add-Check 'OD-13-prepared-dll' 'PASS' 'prepared dllSha matches publish' 'stale_publish_package'
    }
    $expCorpus = [string]$prepared.package.corpusSha256
    if ($expCorpus -and $pubCorpusSha -and $expCorpus.ToLowerInvariant() -ne $pubCorpusSha) {
        Add-Check 'OD-14-prepared-corpus' 'FAIL' 'prepared corpusSha != publish corpus' 'stale_corpora'
    } elseif ($expCorpus -and $pubCorpusSha) {
        Add-Check 'OD-14-prepared-corpus' 'PASS' 'prepared corpusSha matches publish' 'stale_corpora'
    }
    $prepTs = [string]$prepared.package.buildTimestampUtc
    $pubTs = if ($ps) { [string]$ps.buildTimestampUtc } else { '' }
    if ($prepTs -and $pubTs) {
        try {
            $pt = [DateTime]::Parse($prepTs).ToUniversalTime()
            $ut = [DateTime]::Parse($pubTs).ToUniversalTime()
            if ($ut -gt $pt.AddMinutes(1)) {
                Add-Check 'OD-15-prepared-vs-stamp' 'WARN' 'Publish stamp newer than package-prepared receipt (re-stamp after prepare)' 'stale_deployment_artifacts'
            } else {
                Add-Check 'OD-15-prepared-vs-stamp' 'PASS' 'prepared timestamp aligns with publish stamp' 'stale_deployment_artifacts'
            }
        } catch {
            Add-Check 'OD-15-prepared-vs-stamp' 'WARN' 'Could not parse prepared/stamp timestamps' 'stale_deployment_artifacts'
        }
    }
} else {
    Add-Check 'OD-13-prepared-dll' 'WARN' 'package-prepared-latest.json missing or incomplete' 'stale_deployment_artifacts'
}

# ---------- OD-20 corpora ----------
if ($srcCorpusSha -and $pubCorpusSha) {
    if ($srcCorpusSha -ne $pubCorpusSha) {
        Add-Check 'OD-20-corpus-hash' 'FAIL' ("Source corpus != publish corpus ({0} vs {1})" -f $srcCorpusSha.Substring(0, 12), $pubCorpusSha.Substring(0, 12)) 'stale_corpora'
    } else {
        Add-Check 'OD-20-corpus-hash' 'PASS' ("corpusSha={0}..." -f $pubCorpusSha.Substring(0, 12)) 'stale_corpora'
    }
} elseif ($pubCorpusSha -and -not $srcCorpusSha) {
    Add-Check 'OD-20-corpus-hash' 'WARN' 'Publish corpus present; Auricrux.Web source corpus missing' 'stale_corpora'
} elseif (-not $pubCorpusSha) {
    Add-Check 'OD-20-corpus-hash' 'FAIL' 'Publish corpus missing' 'stale_corpora'
}

# ---------- OD-30 manifests ----------
if ($rm -and $pm) {
    $re = [string]$rm.adapter.evalStatus
    $pe = [string]$pm.adapter.evalStatus
    $rr = $null; $pr = $null
    try { $rr = [double]$rm.adapter.ggufGenerativePassRatePercent } catch {}
    try { $pr = [double]$pm.adapter.ggufGenerativePassRatePercent } catch {}
    if ($re -ne $pe -or ($null -ne $rr -and $null -ne $pr -and [math]::Abs($rr - $pr) -gt 0.15)) {
        Add-Check 'OD-30-manifest-repo-pub' 'FAIL' ("Repo manifest eval/rate != publish ({0}/{1} vs {2}/{3})" -f $re, $rr, $pe, $pr) 'stale_manifests'
    } else {
        Add-Check 'OD-30-manifest-repo-pub' 'PASS' ("evalStatus={0} rate={1}" -f $re, $rr) 'stale_manifests'
    }
} elseif (-not $rm) {
    Add-Check 'OD-30-manifest-repo-pub' 'FAIL' 'Repo model_manifest.json missing' 'stale_manifests'
} else {
    Add-Check 'OD-30-manifest-repo-pub' 'WARN' 'Publish model_manifest missing' 'stale_manifests'
}

if ($rm -and $ledger -and $ledger.currentLiveAuthority) {
    $auth = $ledger.currentLiveAuthority
    $mPass = [bool]$rm.adapter.ggufGenerativeSuitePassed -or ([string]$rm.adapter.evalStatus -match '(?<!FAIL-)PASS')
    # Prefer explicit suitePassed / FAIL in evalStatus
    $eval = [string]$rm.adapter.evalStatus
    $claimsPass = [bool]$rm.adapter.ggufGenerativeSuitePassed -or ($eval -match 'PASS' -and $eval -notmatch 'FAIL')
    $authFail = [string]$auth.status -eq 'FAIL'
    $authPass = [string]$auth.status -eq 'PASS'
    $mRate = $null
    try { $mRate = [double]$rm.adapter.ggufGenerativePassRatePercent } catch {}
    $aRate = $null
    try { $aRate = [double]$auth.passRatePercent } catch {}

    if ($claimsPass -and $authFail) {
        Add-Check 'OD-31-manifest-vs-ledger' 'FAIL' 'Manifest claims PASS while ledger currentLiveAuthority is FAIL' 'stale_manifests'
    } elseif ($claimsPass -and $authPass -and $null -ne $mRate -and $null -ne $aRate -and [math]::Abs($mRate - $aRate) -gt 0.15) {
        Add-Check 'OD-31-manifest-vs-ledger' 'FAIL' ("Manifest PASS rate {0} != ledger PASS rate {1}" -f $mRate, $aRate) 'stale_manifests'
    } elseif (-not $claimsPass -and $authFail -and $null -ne $mRate -and $null -ne $aRate -and [math]::Abs($mRate - $aRate) -le 0.15) {
        Add-Check 'OD-31-manifest-vs-ledger' 'PASS' ("Honest FAIL align rate={0}" -f $mRate) 'stale_manifests'
    } elseif (-not $claimsPass -and $authFail) {
        Add-Check 'OD-31-manifest-vs-ledger' 'WARN' ("Both FAIL but rates differ manifest={0} ledger={1}" -f $mRate, $aRate) 'stale_manifests'
    } else {
        Add-Check 'OD-31-manifest-vs-ledger' 'PASS' ("manifest claimsPass={0} ledger={1}" -f $claimsPass, $auth.status) 'stale_manifests'
    }
} else {
    Add-Check 'OD-31-manifest-vs-ledger' 'WARN' 'Skipped (manifest or ledger authority missing)' 'stale_manifests'
}

# ---------- OD-40 ledgers ----------
if (-not $ledger) {
    Add-Check 'OD-40-ledger-present' 'FAIL' 'Evidence ledger JSON missing' 'stale_ledgers'
} else {
    Add-Check 'OD-40-ledger-present' 'PASS' ("entries={0}" -f $ledger.entryCount) 'stale_ledgers'
}

if ($ledger -and (Test-Path $jsonlPath)) {
    $jsonIds = @($ledger.entries | ForEach-Object { [string]$_.evidenceId })
    $jsonlIds = @()
    foreach ($line in @(Get-Content $jsonlPath | Where-Object { $_ })) {
        try { $jsonlIds += [string](($line | ConvertFrom-Json).evidenceId) } catch {}
    }
    $missing = @($jsonIds | Where-Object { $_ -and ($_ -notin $jsonlIds) })
    if ($missing.Count -gt 0) {
        Add-Check 'OD-41-ledger-jsonl' 'FAIL' ("JSON ids missing from JSONL: {0}" -f ($missing -join ',')) 'stale_ledgers'
    } else {
        Add-Check 'OD-41-ledger-jsonl' 'PASS' ("jsonl aligned count={0}" -f $jsonIds.Count) 'stale_ledgers'
    }
} elseif ($ledger) {
    Add-Check 'OD-41-ledger-jsonl' 'FAIL' 'Ledger JSONL missing' 'stale_ledgers'
}

if ($ledger -and $ledger.currentLiveAuthority -and $ledger.currentLiveAuthority.report) {
    $repRel = [string]$ledger.currentLiveAuthority.report
    $repAbs = Join-Path $repoRoot ($repRel -replace '/', '\')
    $leaf = Split-Path $repRel -Leaf
    $alts = @(
        $repAbs,
        (Join-Path (Join-Path $repoRoot 'docs\runtime-proof') $leaf),
        (Join-Path (Join-Path $repoRoot 'eval\reports') $leaf)
    )
    $found = $alts | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) {
        Add-Check 'OD-42-authority-report' 'PASS' ("authority report on disk: {0}" -f $leaf) 'stale_ledgers'
    } else {
        Add-Check 'OD-42-authority-report' 'FAIL' ("authority report missing: {0}" -f $repRel) 'stale_ledgers'
    }
}

# Soft: ledger lag vs newest suite report
$suiteReports = @(Get-ChildItem (Join-Path $repoRoot 'docs\runtime-proof') -Filter 'construction_god_suite_gguf_generative*.json' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch 'alias_rescore' } |
    Sort-Object LastWriteTimeUtc -Descending)
if ($ledger -and $suiteReports.Count -gt 0) {
    $newest = $suiteReports[0]
    $lagHrs = $null
    if ($ledger.updatedAtUtc) {
        try {
            $lu = [DateTime]::Parse([string]$ledger.updatedAtUtc).ToUniversalTime()
            $lagHrs = [math]::Round(($newest.LastWriteTimeUtc - $lu).TotalHours, 1)
        } catch {}
    }
    if ($null -ne $lagHrs -and $lagHrs -gt $maxLedgerLag) {
        $st = if ($StrictAge) { 'FAIL' } else { 'WARN' }
        Add-Check 'OD-43-ledger-age' $st ("Newest suite report newer than ledger by {0}h (threshold {1}h)" -f $lagHrs, $maxLedgerLag) 'stale_ledgers'
    } else {
        Add-Check 'OD-43-ledger-age' 'PASS' ("Ledger vs newest suite lag ok (lag={0}h)" -f $lagHrs) 'stale_ledgers'
    }
}

# ---------- OD-50 runtime versions ----------
if ($rs -and $ps) {
    $rst = [string]$rs.suiteTarget
    $pst = [string]$ps.suiteTarget
    if ($rst -and $pst -and $rst -ne $pst) {
        Add-Check 'OD-50-suite-target' 'FAIL' ("suiteTarget repo={0} publish={1}" -f $rst, $pst) 'stale_runtime_versions'
    } else {
        Add-Check 'OD-50-suite-target' 'PASS' ("suiteTarget={0}" -f $pst) 'stale_runtime_versions'
    }
    $rh = [string]$rs.hostProfile
    $ph = [string]$ps.hostProfile
    $rrcp = [string]$rs.recipeProfile
    $prcp = [string]$ps.recipeProfile
    if (($rh -and $ph -and $rh -ne $ph) -or ($rrcp -and $prcp -and $rrcp -ne $prcp)) {
        Add-Check 'OD-51-profiles' 'WARN' ("host/recipe profile drift repo={0}/{1} pub={2}/{3}" -f $rh, $rrcp, $ph, $prcp) 'stale_runtime_versions'
    } else {
        Add-Check 'OD-51-profiles' 'PASS' ("hostProfile={0} recipe={1}" -f $ph, $prcp) 'stale_runtime_versions'
    }
} else {
    Add-Check 'OD-50-suite-target' 'WARN' 'Stamp pair incomplete for runtime version compare' 'stale_runtime_versions'
}

# ---------- OD-60 deployment artifacts + optional live ----------
$cutoverDone = $false
if ($prepared -and $prepared.PSObject.Properties.Name -contains 'liveCutoverExecuted') {
    $cutoverDone = [bool]$prepared.liveCutoverExecuted
}
if ($gono -and $gono.PSObject.Properties.Name -contains 'cutoverExecuted') {
    if ([bool]$gono.cutoverExecuted) { $cutoverDone = $true }
}

if ($prepared) {
    Add-Check 'OD-60-prepared-receipt' 'PASS' ("prepared liveCutoverExecuted={0}" -f $cutoverDone) 'stale_deployment_artifacts'
    $age = Get-AgeHours ([string]$prepared.atUtc)
    if ($null -ne $age -and $age -gt $maxPreparedAge) {
        $st = if ($StrictAge) { 'FAIL' } else { 'WARN' }
        Add-Check 'OD-61-prepared-age' $st ("package-prepared age {0}h > {1}h" -f $age, $maxPreparedAge) 'stale_deployment_artifacts'
    } else {
        Add-Check 'OD-61-prepared-age' 'PASS' ("package-prepared age={0}h" -f $age) 'stale_deployment_artifacts'
    }
} else {
    Add-Check 'OD-60-prepared-receipt' 'WARN' 'No package-prepared-latest.json' 'stale_deployment_artifacts'
}

$liveIdentity = $null
$liveOk = $false
if ($ProbeLive) {
    try {
        $capUrl = ($BaseUrl.TrimEnd('/') + '/api/capabilities')
        $cap = Invoke-RestMethod -Uri $capUrl -TimeoutSec 20
        if ($cap.packageIdentity) {
            $liveIdentity = $cap.packageIdentity
            $liveOk = $true
            Add-Check 'OD-70-live-identity' 'PASS' ("host packageIdentity version={0}" -f $liveIdentity.packageVersion) 'stale_runtime_versions'
        } else {
            if ($cutoverDone) {
                Add-Check 'OD-70-live-identity' 'FAIL' 'Cutover claimed done but host lacks packageIdentity' 'stale_deployment_artifacts'
            } else {
                Add-Check 'OD-70-live-identity' 'WARN' 'Host lacks packageIdentity (expected pre-cutover)' 'stale_deployment_artifacts'
            }
        }
    } catch {
        if ($cutoverDone) {
            Add-Check 'OD-70-live-identity' 'FAIL' ("Cutover claimed done but host probe failed: {0}" -f $_.Exception.Message) 'stale_deployment_artifacts'
        } else {
            Add-Check 'OD-70-live-identity' 'WARN' ("Live probe failed (pre-cutover tolerant): {0}" -f $_.Exception.Message) 'stale_deployment_artifacts'
        }
    }

    if ($liveOk -and $liveIdentity -and $pubCorpusSha) {
        $liveCorpus = [string]$liveIdentity.corpusSha256
        if ($liveCorpus -and $liveCorpus.ToLowerInvariant() -ne $pubCorpusSha) {
            Add-Check 'OD-71-live-corpus' 'FAIL' 'Live corpusSha != publish corpus (stale host package)' 'stale_corpora'
        } elseif ($liveCorpus) {
            Add-Check 'OD-71-live-corpus' 'PASS' 'Live corpusSha matches publish' 'stale_corpora'
        } else {
            Add-Check 'OD-71-live-corpus' 'WARN' 'Live identity missing corpusSha256' 'stale_corpora'
        }
        $liveVer = [string]$liveIdentity.packageVersion
        $pubVer = if ($ps) { [string]$ps.packageVersion } else { '' }
        if ($liveVer -and $pubVer -and $liveVer -ne $pubVer) {
            Add-Check 'OD-72-live-version' 'WARN' ("Live packageVersion {0} behind/differ publish {1}" -f $liveVer, $pubVer) 'stale_runtime_versions'
        } elseif ($liveVer -and $pubVer) {
            Add-Check 'OD-72-live-version' 'PASS' ("Live packageVersion={0}" -f $liveVer) 'stale_runtime_versions'
        }
    }
} else {
    Add-Check 'OD-70-live-identity' 'PASS' 'Live probe skipped (default; use -ProbeLive)' 'stale_deployment_artifacts'
}

# Doc present
$doc = Join-Path $repoRoot 'docs\runtime-proof\OPERATIONAL_DRIFT.md'
Add-Check 'OD-80-doc' $(if (Test-Path $doc) { 'PASS' } else { 'FAIL' }) 'OPERATIONAL_DRIFT.md' 'policy'

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -gt 0) { 'OPERATIONAL_DRIFT_BLOCKED' } elseif ($warn -gt 0) { 'OPERATIONAL_DRIFT_WARN' } else { 'OPERATIONAL_DRIFT_OK' }

$byClass = @{}
foreach ($c in $checks) {
    $k = if ($c.class) { $c.class } else { 'other' }
    if (-not $byClass.ContainsKey($k)) { $byClass[$k] = @{ pass = 0; warn = 0; fail = 0 } }
    switch ($c.status) {
        'PASS' { $byClass[$k].pass++ }
        'WARN' { $byClass[$k].warn++ }
        'FAIL' { $byClass[$k].fail++ }
    }
}

$receipt = @{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    warnCount = $warn
    failCount = $fail
    probeLive = [bool]$ProbeLive
    strictAge = [bool]$StrictAge
    cutoverExecutedClaim = $cutoverDone
    fingerprints = @{
        publishDllSha12 = if ($pubDllSha) { $pubDllSha.Substring(0, 12) } else { $null }
        publishCorpusSha12 = if ($pubCorpusSha) { $pubCorpusSha.Substring(0, 12) } else { $null }
        sourceCorpusSha12 = if ($srcCorpusSha) { $srcCorpusSha.Substring(0, 12) } else { $null }
        packageVersion = if ($ps) { [string]$ps.packageVersion } else { $null }
        buildTimestampUtc = if ($ps) { [string]$ps.buildTimestampUtc } else { $null }
    }
    byClass = $byClass
    checks = @($checks)
    policyPath = 'auricrux/system/auricrux_operational_drift_v1.json'
    falsePositiveNote = 'Pre-cutover missing host identity is WARN. Age alone is WARN unless -StrictAge. Honest FAIL align is PASS.'
}

$out = Join-Path $repoRoot 'docs\runtime-proof\operational-drift-latest.json'
($receipt | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $out -Encoding utf8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} WARN={2} FAIL={3})" -f $token, $pass, $warn, $fail) -ForegroundColor $(
    if ($fail -gt 0) { 'Red' } elseif ($warn -gt 0) { 'Yellow' } else { 'Green' }
)
Write-Host "Receipt: $out"
Write-Host $token
if ($fail -gt 0) { exit 2 }
if ($warn -gt 0) { exit 1 }
exit 0
