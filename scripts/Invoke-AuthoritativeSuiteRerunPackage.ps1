<#
.SYNOPSIS
  Authoritative live GGUF suite rerun package - prerequisite verification only.

.DESCRIPTION
  Verifies every prerequisite for an authoritative product-host suite rerun.
  NEVER runs run-gguf-construction-suite.ps1.
  NEVER dispatches cutover, mutates model, or touches 3B train.

  Verdict:
    GO                 - May run authoritative suite now (no SkipSafetyGate)
    GO-WITH-BLOCKERS   - Offline/rules/rollback ready; live blockers remain (do not run for authority)
    NO-GO              - Hard integrity/policy failures; do not plan suite until fixed

  Token printed: AUTHORITATIVE_SUITE_RERUN_<VERDICT>
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedProductModel = 'auricrux-fca',
    [string]$PublishDir = '',
    [switch]$SkipLiveProbes
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}
$proofDir = Join-Path $repoRoot 'docs\runtime-proof'
$utc = (Get-Date).ToUniversalTime()
$pkgId = 'authoritative-suite-rerun-package-{0:yyyyMMddTHHmmss}Z' -f $utc

$checks = New-Object System.Collections.Generic.List[object]
$blockers = New-Object System.Collections.Generic.List[object]
$tokens = [ordered]@{}

function Add-Check([string]$Id, [string]$Area, [string]$Status, [string]$Detail, [string]$Severity = 'hard') {
    [void]$checks.Add([pscustomobject]@{
        id = $Id; area = $Area; status = $Status; detail = $Detail; severity = $Severity
    })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
    if ($Status -eq 'FAIL') {
        [void]$blockers.Add([pscustomobject]@{
            id = $Id; area = $Area; detail = $Detail; severity = $Severity
        })
    }
}

function Invoke-AssertReceipt {
    param(
        [string]$Id,
        [string]$Area,
        [string]$ScriptRel,
        [hashtable]$Splat = @{},
        [string]$ReceiptRel,
        [string[]]$AcceptOk,
        [string[]]$AcceptBlocker = @(),
        [string]$Severity = 'hard'
    )
    $sp = Join-Path $repoRoot $ScriptRel
    if (-not (Test-Path $sp)) {
        Add-Check $Id $Area 'FAIL' ("missing {0}" -f $ScriptRel) $Severity
        return $null
    }
    & $sp @Splat | Out-Null
    $tok = $null
    $rp = Join-Path $repoRoot $ReceiptRel
    if (Test-Path $rp) {
        try { $tok = [string](Get-Content $rp -Raw | ConvertFrom-Json).token } catch { }
    }
    if ($tok -and ($AcceptOk -contains $tok)) {
        Add-Check $Id $Area 'PASS' $tok $Severity
        $tokens[$tok] = $true
        return $tok
    }
    if ($tok -and ($AcceptBlocker -contains $tok)) {
        # Expected live blocker - record as FAIL with live severity so GO-WITH-BLOCKERS can apply
        Add-Check $Id $Area 'FAIL' $tok 'live'
        $tokens[$tok] = $true
        return $tok
    }
    Add-Check $Id $Area 'FAIL' ("expected one of [{0}]; got='{1}'" -f (($AcceptOk + $AcceptBlocker) -join ','), $tok) $Severity
    return $tok
}

Write-Host '=== Authoritative live GGUF suite rerun package (prereqs only) ===' -ForegroundColor Cyan
Write-Host 'NO suite execution. NO cutover dispatch. NO model/train mutate.'
Write-Host ("packageId={0}" -f $pkgId)
Write-Host ("BaseUrl={0} SkipLiveProbes={1}" -f $BaseUrl, [bool]$SkipLiveProbes)

# ---------------------------------------------------------------------------
# 1) Deployment package
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Deployment package ---' -ForegroundColor Cyan
$area = 'deployment-package'
$need = @(
    (Join-Path $PublishDir 'Auricrux.Web.dll'),
    (Join-Path $PublishDir 'Data\construction-corpus.json'),
    (Join-Path $PublishDir 'auricrux\system\package_stamp.json'),
    (Join-Path $PublishDir 'auricrux\system\model_manifest.json'),
    (Join-Path $PublishDir 'appsettings.json')
)
$miss = @($need | Where-Object { -not (Test-Path $_) })
if ($miss.Count -eq 0) {
    Add-Check 'SR-10-publish-complete' $area 'PASS' '_publish/web complete (DLL/corpus/stamp/manifest/appsettings)'
} else {
    Add-Check 'SR-10-publish-complete' $area 'FAIL' ("missing publish files count={0}" -f $miss.Count)
}

$dll = Join-Path $PublishDir 'Auricrux.Web.dll'
if (Test-Path $dll) {
    $bytes = [System.IO.File]::ReadAllBytes($dll)
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
    $hasExpand = $ascii.Contains('ExpandSearchTerms')
    $hasRT = $ascii.Contains('RuntimeTruth') -or $ascii.Contains('packageIdentity')
    if ($hasExpand) { Add-Check 'SR-11-expand-search' $area 'PASS' 'ExpandSearchTerms in publish DLL' }
    else { Add-Check 'SR-11-expand-search' $area 'FAIL' 'ExpandSearchTerms missing from publish DLL' }
    if ($hasRT) { Add-Check 'SR-12-identity-capability' $area 'PASS' 'RuntimeTruth/packageIdentity strings present in DLL' }
    else { Add-Check 'SR-12-identity-capability' $area 'FAIL' 'DLL lacks RuntimeTruth/packageIdentity markers' }
}

$stampPath = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
if (Test-Path $stampPath) {
    $st = Get-Content $stampPath -Raw | ConvertFrom-Json
    if ($st.suiteTarget -eq 'construction_god_suite_v1' -and $st.packageVersion -and $st.hostProfile -eq 'product-gce') {
        Add-Check 'SR-13-stamp' $area 'PASS' ("version={0} suite={1} host={2}" -f $st.packageVersion, $st.suiteTarget, $st.hostProfile)
    } else {
        Add-Check 'SR-13-stamp' $area 'FAIL' 'stamp fields incomplete or wrong suite/host'
    }
} else {
    Add-Check 'SR-13-stamp' $area 'FAIL' 'package_stamp missing'
}

$suiteDef = Join-Path $repoRoot 'eval\construction_god_suite_v1.json'
$runner = Join-Path $repoRoot 'scripts\run-gguf-construction-suite.ps1'
if (Test-Path $suiteDef) { Add-Check 'SR-14-suite-def' $area 'PASS' 'eval/construction_god_suite_v1.json present' }
else { Add-Check 'SR-14-suite-def' $area 'FAIL' 'canonical suite definition missing' }
if (Test-Path $runner) {
    $rt = Get-Content $runner -Raw
    if ($rt -match 'SkipSafetyGate' -and $rt -match 'Assert-GgufSuiteDeploymentSafetyGate') {
        Add-Check 'SR-15-suite-runner' $area 'PASS' 'Suite runner enforces safety gate by default'
    } else {
        Add-Check 'SR-15-suite-runner' $area 'FAIL' 'Suite runner missing gate enforcement'
    }
} else {
    Add-Check 'SR-15-suite-runner' $area 'FAIL' 'run-gguf-construction-suite.ps1 missing'
}

# ---------------------------------------------------------------------------
# 2) Target host
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Target host ---' -ForegroundColor Cyan
$area = 'target-host'
try {
    $uri = [Uri]$BaseUrl
    if ($uri.Scheme -eq 'https' -and $uri.Host -eq $ExpectedHost) {
        Add-Check 'SR-20-host-url' $area 'PASS' ("https://{0}" -f $uri.Host)
    } else {
        Add-Check 'SR-20-host-url' $area 'FAIL' ("wrong BaseUrl={0}" -f $BaseUrl)
    }
} catch {
    Add-Check 'SR-20-host-url' $area 'FAIL' $_.Exception.Message
}

if ($SkipLiveProbes) {
    Add-Check 'SR-21-health' $area 'WARN' 'Live health skipped (-SkipLiveProbes)' 'soft'
    Add-Check 'SR-22-package-host' $area 'WARN' 'Package-host assert skipped' 'soft'
    Add-Check 'SR-23-runtime-truth' $area 'WARN' 'Runtime-truth probe skipped' 'soft'
} else {
    try {
        $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
        if ([string]$h.primaryModel -eq $ExpectedProductModel -and [bool]$h.primaryModelReady) {
            Add-Check 'SR-21-health' $area 'PASS' ("status={0} model={1} mode={2}" -f $h.status, $h.primaryModel, $h.runtimeMode)
        } else {
            Add-Check 'SR-21-health' $area 'FAIL' ("model={0} ready={1}" -f $h.primaryModel, $h.primaryModelReady) 'hard'
        }
        $tokens['healthPrimaryModel'] = [string]$h.primaryModel
        $tokens['healthRuntimeMode'] = [string]$h.runtimeMode
    } catch {
        Add-Check 'SR-21-health' $area 'FAIL' $_.Exception.Message 'hard'
    }

    Invoke-AssertReceipt -Id 'SR-22-package-host' -Area $area `
        -ScriptRel 'scripts\Assert-PackageHostConsistency.ps1' `
        -ReceiptRel 'docs\runtime-proof\package-host-consistency-latest.json' `
        -AcceptOk @('PACKAGE_HOST_CONSISTENCY_OK') `
        -AcceptBlocker @('PACKAGE_HOST_CONSISTENCY_BLOCKED') `
        -Severity 'live'

    try {
        $null = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45
        Add-Check 'SR-23-runtime-truth' $area 'PASS' 'HTTP 200 /api/runtime-truth' 'live'
    } catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        if ($code -eq 404) {
            Add-Check 'SR-23-runtime-truth' $area 'FAIL' 'HTTP 404 - package cutover required (RB-C2)' 'live'
        } else {
            Add-Check 'SR-23-runtime-truth' $area 'FAIL' ("runtime-truth failed: {0}" -f $_.Exception.Message) 'live'
        }
    }
}

# ---------------------------------------------------------------------------
# 3) Authority rules
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Authority rules ---' -ForegroundColor Cyan
$area = 'authority-rules'
Invoke-AssertReceipt -Id 'SR-30-authority-map' -Area $area `
    -ScriptRel 'scripts\Assert-AuricruxAuthorityMap.ps1' `
    -ReceiptRel 'docs\runtime-proof\authority-map-latest.json' `
    -AcceptOk @('AUTHORITY_MAP_OK')

$authDoc = Join-Path $proofDir 'AURICRUX_AUTHORITY_MAP.md'
$authPol = Join-Path $repoRoot 'auricrux\system\auricrux_authority_chain_v1.json'
if ((Test-Path $authDoc) -and (Test-Path $authPol)) {
    Add-Check 'SR-31-authority-docs' $area 'PASS' 'Authority map doc + chain policy present'
} else {
    Add-Check 'SR-31-authority-docs' $area 'FAIL' 'Authority map doc or policy missing'
}

# ---------------------------------------------------------------------------
# 4) Manifest rules
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Manifest rules ---' -ForegroundColor Cyan
$area = 'manifest-rules'
$manPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
try {
    $man = Get-Content $manPath -Raw | ConvertFrom-Json
    $eval = [string]$man.adapter.evalStatus
    $passed = [bool]$man.adapter.ggufGenerativeSuitePassed
    $rate = [double]$man.adapter.ggufGenerativePassRatePercent
    $deployReq = [bool]$man.adapter.productHostDeployRequired
    $honest = ($eval -match 'FAIL') -and (-not $passed) -and ([math]::Abs($rate - 76.7) -lt 0.05)
    if ($honest) {
        Add-Check 'SR-40-manifest-honest' $area 'PASS' ("FAIL@{0}% suitePassed=false eval={1}" -f $rate, $eval)
    } else {
        Add-Check 'SR-40-manifest-honest' $area 'FAIL' ("Unexpected manifest authority eval={0} rate={1} passed={2}" -f $eval, $rate, $passed)
    }
    if ($deployReq) {
        Add-Check 'SR-41-manifest-deploy-required' $area 'PASS' 'productHostDeployRequired=true (honest pre-cutover)' 'soft'
    } else {
        Add-Check 'SR-41-manifest-deploy-required' $area 'WARN' 'productHostDeployRequired=false - confirm host package proven' 'soft'
    }
    $trainMark = (Get-Content $manPath -Raw) -match 'running-do-not-interrupt'
    if ($trainMark) {
        Add-Check 'SR-42-manifest-train' $area 'PASS' 'Train do-not-interrupt marker retained'
    } else {
        Add-Check 'SR-42-manifest-train' $area 'WARN' 'Train marker not found in manifest text' 'soft'
    }
} catch {
    Add-Check 'SR-40-manifest-honest' $area 'FAIL' $_.Exception.Message
}

# ---------------------------------------------------------------------------
# 5) Ledger rules
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Ledger rules ---' -ForegroundColor Cyan
$area = 'ledger-rules'
Invoke-AssertReceipt -Id 'SR-50-evidence-rules' -Area $area `
    -ScriptRel 'scripts\Assert-AuricruxEvidenceRules.ps1' `
    -ReceiptRel 'docs\runtime-proof\auricrux-evidence-rules-latest.json' `
    -AcceptOk @('EVIDENCE_RULES_OK')

Invoke-AssertReceipt -Id 'SR-51-ledger-integrity' -Area $area `
    -ScriptRel 'scripts\Assert-EvidenceLedgerIntegrity.ps1' `
    -ReceiptRel 'docs\runtime-proof\evidence-ledger-integrity-latest.json' `
    -AcceptOk @('EVIDENCE_LEDGER_INTEGRITY_OK')

$ledgerPath = Join-Path $proofDir 'auricrux_evidence_ledger_v1.json'
try {
    $led = Get-Content $ledgerPath -Raw | ConvertFrom-Json
    $auth = $led.currentLiveAuthority
    if ([string]$auth.status -eq 'FAIL' -and [math]::Abs([double]$auth.passRatePercent - 76.7) -lt 0.05) {
        Add-Check 'SR-52-ledger-authority' $area 'PASS' ("currentLiveAuthority FAIL@{0}%" -f $auth.passRatePercent)
    } else {
        Add-Check 'SR-52-ledger-authority' $area 'FAIL' ("Unexpected ledger authority {0}@{1}" -f $auth.status, $auth.passRatePercent)
    }
} catch {
    Add-Check 'SR-52-ledger-authority' $area 'FAIL' $_.Exception.Message
}

# ---------------------------------------------------------------------------
# 6) Safety gates
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Safety gates ---' -ForegroundColor Cyan
$area = 'safety-gates'

# Offline gate must be OK (package-side)
Invoke-AssertReceipt -Id 'SR-60-offline-gate' -Area $area `
    -ScriptRel 'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1' `
    -Splat @{ SkipLiveProbes = $true } `
    -ReceiptRel 'docs\runtime-proof\gguf-deployment-safety-gate-latest.json' `
    -AcceptOk @('DEPLOYMENT_SAFETY_GATE_OK')

Invoke-AssertReceipt -Id 'SR-61-train-protect' -Area $area `
    -ScriptRel 'scripts\Assert-Live3bTrainProtection.ps1' `
    -ReceiptRel 'docs\runtime-proof\live-3b-train-protection-latest.json' `
    -AcceptOk @('LIVE_3B_TRAIN_PROTECTION_OK')

Invoke-AssertReceipt -Id 'SR-62-clobber' -Area $area `
    -ScriptRel 'scripts\Assert-ProductModelClobberProtection.ps1' `
    -ReceiptRel 'docs\runtime-proof\product-model-clobber-protection-latest.json' `
    -AcceptOk @('PRODUCT_MODEL_CLOBBER_PROTECTION_OK')

Invoke-AssertReceipt -Id 'SR-63-fallback' -Area $area `
    -ScriptRel 'scripts\Assert-ProductFallbackProfile.ps1' `
    -ReceiptRel 'docs\runtime-proof\product-fallback-profile-latest.json' `
    -AcceptOk @('PRODUCT_FALLBACK_PROFILE_OK')

if (-not $SkipLiveProbes) {
    Invoke-AssertReceipt -Id 'SR-64-live-gate' -Area $area `
        -ScriptRel 'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1' `
        -ReceiptRel 'docs\runtime-proof\gguf-deployment-safety-gate-latest.json' `
        -AcceptOk @('DEPLOYMENT_SAFETY_GATE_OK') `
        -AcceptBlocker @('DEPLOYMENT_SAFETY_GATE_BLOCKED') `
        -Severity 'live'
} else {
    Add-Check 'SR-64-live-gate' $area 'WARN' 'Live safety gate skipped (-SkipLiveProbes)' 'soft'
}

# Promotion blocked is expected and does not hard-block suite entry (SG-23 WARN)
Invoke-AssertReceipt -Id 'SR-65-promotion' -Area $area `
    -ScriptRel 'scripts\Assert-PromotionEvidenceGate.ps1' `
    -Splat @{ SkipLiveProbes = $true } `
    -ReceiptRel 'docs\runtime-proof\promotion-evidence-gate-latest.json' `
    -AcceptOk @('PROMOTION_EVIDENCE_OK') `
    -AcceptBlocker @('PROMOTION_EVIDENCE_BLOCKED') `
    -Severity 'soft'
# Soft: if BLOCKED, downgrade FAIL to WARN for verdict (promotion != suite entry)
$promoFail = @($checks | Where-Object { $_.id -eq 'SR-65-promotion' -and $_.status -eq 'FAIL' })
if ($promoFail.Count -gt 0 -and $tokens.Contains('PROMOTION_EVIDENCE_BLOCKED')) {
    # Replace last SR-65 with WARN
    for ($i = $checks.Count - 1; $i -ge 0; $i--) {
        if ($checks[$i].id -eq 'SR-65-promotion') {
            $checks[$i] = [pscustomobject]@{
                id = 'SR-65-promotion'; area = $area; status = 'WARN'
                detail = 'PROMOTION_EVIDENCE_BLOCKED (expected; blocks promote/Release not suite entry)'
                severity = 'soft'
            }
            Write-Host '[WARN] SR-65-promotion: PROMOTION_EVIDENCE_BLOCKED (soft; not suite-entry NO-GO)' -ForegroundColor Yellow
            break
        }
    }
    # Remove from blockers list
    $newB = New-Object System.Collections.Generic.List[object]
    foreach ($b in $blockers) {
        if ($b.id -ne 'SR-65-promotion') { [void]$newB.Add($b) }
    }
    $blockers = $newB
}

# ---------------------------------------------------------------------------
# 7) Rollback package
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Rollback package ---' -ForegroundColor Cyan
$area = 'rollback-package'
$proc = Join-Path $proofDir 'gguf-suite-live-cutover-procedure-2026-08-03.md'
$base = Join-Path $proofDir 'gguf-grounding-precutover-baseline-2026-08-03.json'
$wf = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
$exec = Join-Path $proofDir 'RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
if ((Test-Path $proc) -and (Select-String -Path $proc -Pattern 'auricrux-web-prev', '/api/health' -Quiet)) {
    Add-Check 'SR-70-rollback-proc' $area 'PASS' 'Cutover rollback procedure present'
} else {
    Add-Check 'SR-70-rollback-proc' $area 'FAIL' 'Rollback procedure missing/incomplete'
}
if (Test-Path $base) { Add-Check 'SR-71-precutover-baseline' $area 'PASS' 'Precutover baseline present' }
else { Add-Check 'SR-71-precutover-baseline' $area 'FAIL' 'Precutover baseline missing' }
if (Test-Path $wf) {
    $w = Get-Content $wf -Raw
    if (($w -match 'prev-\$\(date' -or $w -match 'prev-') -and ($w -match 'PrimaryModel=auricrux-fca')) {
        Add-Check 'SR-72-workflow-rollback' $area 'PASS' 'Workflow prev-rename + PrimaryModel preserve'
    } else {
        Add-Check 'SR-72-workflow-rollback' $area 'FAIL' 'Workflow rollback/model preserve markers missing'
    }
} else {
    Add-Check 'SR-72-workflow-rollback' $area 'FAIL' 'Cutover workflow missing'
}
if (Test-Path $exec) { Add-Check 'SR-73-rb-c2-package' $area 'PASS' 'RB-C2 execution package present' }
else { Add-Check 'SR-73-rb-c2-package' $area 'WARN' 'RB-C2 execution package missing' 'soft' }

Invoke-AssertReceipt -Id 'SR-74-dryrun-drill' -Area $area `
    -ScriptRel 'scripts\Invoke-CutoverRollbackDryRun.ps1' `
    -ReceiptRel 'docs\runtime-proof\cutover-rollback-drill-latest.json' `
    -AcceptOk @('CUTOVER_ROLLBACK_DRILL_OK', 'CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED')

# ---------------------------------------------------------------------------
# Verdict
# ---------------------------------------------------------------------------
$hardFails = @($checks | Where-Object { $_.status -eq 'FAIL' -and $_.severity -eq 'hard' })
$liveFails = @($checks | Where-Object { $_.status -eq 'FAIL' -and $_.severity -eq 'live' })
$softFails = @($checks | Where-Object { $_.status -eq 'FAIL' -and $_.severity -eq 'soft' })
$passN = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$failN = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$warnN = @($checks | Where-Object { $_.status -eq 'WARN' }).Count

# Rebuild blockers from remaining FAILs only
$blockers = New-Object System.Collections.Generic.List[object]
foreach ($c in $checks) {
    if ($c.status -eq 'FAIL') {
        [void]$blockers.Add([pscustomobject]@{
            id = $c.id; area = $c.area; detail = $c.detail; severity = $c.severity
        })
    }
}

$verdict = 'NO-GO'
$reason = ''
if ($hardFails.Count -gt 0) {
    $verdict = 'NO-GO'
    $reason = 'Hard integrity/policy failures present; fix before suite planning'
} elseif ($liveFails.Count -gt 0) {
    $verdict = 'GO-WITH-BLOCKERS'
    $reason = 'Offline package + rules + rollback ready; live host/gate blockers remain (RB-C2/RB-C3). Do not run authoritative suite yet.'
} elseif ($SkipLiveProbes) {
    $verdict = 'GO-WITH-BLOCKERS'
    $reason = 'Live probes skipped; cannot authorize suite start without live gate OK'
} else {
    $verdict = 'GO'
    $reason = 'All hard + live prereqs PASS; may run authoritative suite without -SkipSafetyGate'
}

$token = 'AUTHORITATIVE_SUITE_RERUN_' + ($verdict -replace '-', '_')

$namedBlockers = @(
    [ordered]@{
        id = 'B3'; title = 'Operator package-web cutover not executed'
        mapsTo = @('SR-22-package-host', 'SR-23-runtime-truth', 'SR-64-live-gate')
        closure = 'Follow RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md; gh workflow run gcp-cutover-build-auricrux.yml -f action=full'
    },
    [ordered]@{
        id = 'B2'; title = 'Intended package not on product host'
        mapsTo = @('SR-22-package-host', 'SR-23-runtime-truth')
        closure = 'Same cutover as B3; then PACKAGE_HOST_CONSISTENCY_OK + runtime-truth 200'
    },
    [ordered]@{
        id = 'B1'; title = 'Host lacks packageIdentity'
        mapsTo = @('SR-22-package-host')
        closure = 'Deploy package with PackageIdentityService + stamp'
    },
    [ordered]@{
        id = 'B4'; title = 'Live safety gate blocks suite entry'
        mapsTo = @('SR-64-live-gate')
        closure = 'Clear B1-B3; Assert-GgufSuiteDeploymentSafetyGate.ps1 without SkipLiveProbes'
    }
)

$activeNamed = @()
foreach ($nb in $namedBlockers) {
    $hit = $false
    foreach ($m in $nb.mapsTo) {
        if (@($checks | Where-Object { $_.id -eq $m -and $_.status -eq 'FAIL' }).Count -gt 0) { $hit = $true; break }
    }
    if ($hit) { $activeNamed += $nb }
}

$suiteCommand = '.\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com'
$nextAction = switch ($verdict) {
    'GO' {
        "Operator may run: $suiteCommand  (do NOT use -SkipSafetyGate). Then ledger append; Manifest only if qualified PASS."
    }
    'GO-WITH-BLOCKERS' {
        'Close live blockers (RB-C2 cutover -> RB-C3 live gate OK), re-run this package to GO, then run suite. Forbidden: -SkipSafetyGate for authority.'
    }
    default {
        'Fix hard FAIL checks; re-run Invoke-AuthoritativeSuiteRerunPackage.ps1; do not run suite.'
    }
}

$doc = [ordered]@{}
$doc['schemaVersion'] = 1
$doc['packageId'] = $pkgId
$doc['purpose'] = 'authoritative-live-gguf-suite-rerun-package'
$doc['atUtc'] = $utc.ToString('o')
$doc['verdict'] = $verdict
$tokenOut = $token
$doc['token'] = $tokenOut
$doc['reason'] = $reason
$doc['suiteExecuted'] = $false
$doc['skipSafetyGateAuthorized'] = $false
$doc['pass'] = $passN
$doc['fail'] = $failN
$doc['warn'] = $warnN
$doc['hardFailCount'] = $hardFails.Count
$doc['liveFailCount'] = $liveFails.Count
$doc['baseUrl'] = $BaseUrl
$doc['publishDir'] = $PublishDir
$doc['areas'] = @(
    'deployment-package', 'target-host', 'authority-rules', 'manifest-rules',
    'ledger-rules', 'safety-gates', 'rollback-package'
)
$doc['checks'] = @(
    foreach ($c in $checks) {
        [ordered]@{ id = $c.id; area = $c.area; status = $c.status; detail = [string]$c.detail; severity = $c.severity }
    }
)
$doc['blockers'] = @(
    foreach ($b in $blockers) {
        [ordered]@{ id = $b.id; area = $b.area; detail = [string]$b.detail; severity = $b.severity }
    }
)
$doc['namedBlockersActive'] = $activeNamed
$doc['dependencyOrder'] = @('B3', 'B2', 'B1', 'B4', 'authoritative suite rerun')
$doc['tokens'] = $tokens
$doc['suiteCommandWhenGo'] = $suiteCommand
$doc['forbiddenForAuthority'] = @(
    '-SkipSafetyGate',
    'offline alias rescore as live PASS',
    'Manifest PASS without qualified live report',
    'suite while PACKAGE_HOST_CONSISTENCY_BLOCKED'
)
$doc['nextOperatorAction'] = $nextAction
$doc['related'] = [ordered]@{
    packageDoc = 'docs/runtime-proof/AUTHORITATIVE_LIVE_GGUF_SUITE_RERUN_PACKAGE.md'
    rbC2 = 'docs/runtime-proof/RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
    evidenceRules = 'docs/runtime-proof/AURICRUX_EVIDENCE_RULES.md'
    safetyGate = 'docs/runtime-proof/GGUF_DEPLOYMENT_SAFETY_GATE.md'
}

$json = ConvertTo-Json -InputObject $doc -Depth 14
if ([string]::IsNullOrWhiteSpace($json)) { throw 'empty receipt' }
$utf8 = New-Object System.Text.UTF8Encoding $false
$latest = Join-Path $proofDir 'authoritative-suite-rerun-package-latest.json'
$dated = Join-Path $proofDir ('authoritative-suite-rerun-package-{0:yyyy-MM-dd}.json' -f $utc)
# Also refresh legacy prereqs pointer used by other docs
$legacy = Join-Path $proofDir 'authoritative-suite-rerun-prereqs-latest.json'
[System.IO.File]::WriteAllText($latest, $json, $utf8)
[System.IO.File]::WriteAllText($dated, $json, $utf8)

$legacyDoc = [ordered]@{
    schemaVersion = 1
    evidenceId = $pkgId
    atUtc = $utc.ToString('o')
    purpose = 'Verified prerequisites for authoritative live GGUF suite rerun. Suite not executed.'
    verdict = $verdict
    token = $tokenOut
    suiteExecuted = $false
    blockers = $activeNamed
    checkFailures = @(
        foreach ($b in $blockers) {
            [ordered]@{ id = $b.id; detail = [string]$b.detail; severity = $b.severity }
        }
    )
    verifiedNotBlocking = @(
        'offline DEPLOYMENT_SAFETY_GATE_OK (when SR-60 PASS)',
        'EVIDENCE_RULES_OK / ledger integrity (when PASS)',
        'PROMOTION_EVIDENCE_BLOCKED does not block suite entry',
        'C: storage not suite-entry blocker'
    )
    dependencyOrder = @('B3', 'B2', 'B1', 'B4', 'authoritative suite rerun')
    packageReceipt = 'docs/runtime-proof/authoritative-suite-rerun-package-latest.json'
}
[System.IO.File]::WriteAllText($legacy, (ConvertTo-Json -InputObject $legacyDoc -Depth 10), $utf8)

# Markdown summary
$md = @(
    '# Authoritative live GGUF suite rerun package',
    '',
    ('**packageId:** `{0}`' -f $pkgId),
    ('**Verdict:** `{0}`' -f $verdict),
    ('**Token:** `{0}`' -f $tokenOut),
    ('**At UTC:** {0}' -f $utc.ToString('o')),
    '',
    ('**Reason:** {0}' -f $reason),
    '',
    '**Suite executed:** false (prereq package only)',
    '',
    ('## Summary: PASS={0} FAIL={1} WARN={2} (hardFail={3} liveFail={4})' -f $passN, $failN, $warnN, $hardFails.Count, $liveFails.Count),
    '',
    '## Area results',
    '| Area | Result |',
    '|------|--------|'
)
foreach ($a in $doc.areas) {
    $fa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'FAIL' }).Count
    $wa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'WARN' }).Count
    $pa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'PASS' }).Count
    $res = if ($fa -gt 0) { 'FAIL' } elseif ($wa -gt 0) { 'PASS w/ WARN' } else { 'PASS' }
    $md += ('| {0} | {1} (P={2} W={3} F={4}) |' -f $a, $res, $pa, $wa, $fa)
}
$md += ''
$md += '## Active named blockers'
if ($activeNamed.Count -eq 0) {
    $md += '_None._'
} else {
    foreach ($nb in $activeNamed) {
        $md += ('- **{0}** {1} - closure: {2}' -f $nb.id, $nb.title, $nb.closure)
    }
}
$md += ''
$md += '## Failures'
if ($blockers.Count -eq 0) {
    $md += '_None._'
} else {
    foreach ($b in $blockers) {
        $md += ('- **{0}** [{1}/{2}]: {3}' -f $b.id, $b.area, $b.severity, $b.detail)
    }
}
$md += ''
$md += '## Next operator action'
$md += $nextAction
$md += ''
$md += '## When verdict is GO'
$md += '```powershell'
$md += 'cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app'
$md += $suiteCommand
$md += '```'
$md += 'Do **not** use `-SkipSafetyGate` for authority. Manifest update only after qualified live PASS.'
$md += ''
$md += '## Explicit non-claims'
$md += '- Live suite was **not** run'
$md += '- Does **not** clear RB-C1 / Manifest PASS by itself'
$md += '- GO-WITH-BLOCKERS is **not** authorization to run the suite'

$mdPath = Join-Path $proofDir 'AUTHORITATIVE_LIVE_GGUF_SUITE_RERUN_PACKAGE.md'
[System.IO.File]::WriteAllLines($mdPath, $md, $utf8)

Write-Host ''
Write-Host ("Verdict: {0}" -f $verdict) -ForegroundColor $(if ($verdict -eq 'GO') { 'Green' } elseif ($verdict -eq 'GO-WITH-BLOCKERS') { 'Yellow' } else { 'Red' })
Write-Host ("Reason: {0}" -f $reason)
Write-Host ("Receipt: {0}" -f $latest)
Write-Host ("Doc:     {0}" -f $mdPath)
Write-Host $tokenOut

if ($verdict -eq 'NO-GO') { exit 2 }
if ($verdict -eq 'GO-WITH-BLOCKERS') { exit 1 }
exit 0
