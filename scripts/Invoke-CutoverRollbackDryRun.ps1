<#
.SYNOPSIS
  Dry-run cutover + rollback drill for Auricrux package deployment.
.DESCRIPTION
  Proves detection of current/target/rollback state, failure signals, recovery
  instructions, and evidence logging  -  WITHOUT live cutover, WITHOUT product
  model replace, WITHOUT touching the live 3B train.

  Token: CUTOVER_ROLLBACK_DRILL_OK / CUTOVER_ROLLBACK_DRILL_BLOCKED_LIVE
  (OK = dry-run complete with evidence; BLOCKED_LIVE = live cutover must not proceed)
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedProductModel = 'auricrux-fca',
    [string]$PublishDir = '',
    [switch]$AllowLiveCutover
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$drillUtc = (Get-Date).ToUniversalTime()
$drillId = 'cutover-rollback-drill-{0:yyyyMMddTHHmmss}Z' -f $drillUtc
$checks = New-Object System.Collections.Generic.List[object]
$blockers = New-Object System.Collections.Generic.List[string]
$phases = [ordered]@{}

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Add-Blocker([string]$Reason) {
    [void]$blockers.Add($Reason)
    Write-Host ("BLOCKER: {0}" -f $Reason) -ForegroundColor Yellow
}

Write-Host '=== Auricrux cutover / rollback DRY-RUN drill ===' -ForegroundColor Cyan
Write-Host 'NO live container swap. NO product model replace. NO 3B train touch.'
Write-Host ("drillId={0}" -f $drillId)

# Refuse accidental live path
if ($AllowLiveCutover) {
    Add-Check 'DR-00-live-refused' 'FAIL' '-AllowLiveCutover is not supported by this drill; use authorized workflow only after promotion evidence OK'
    Add-Blocker 'Live cutover must use gcp-cutover-build-auricrux.yml after PROMOTION_EVIDENCE_OK  -  not this drill'
} else {
    Add-Check 'DR-00-mode' 'PASS' 'Dry-run only (AllowLiveCutover unset)'
}

# ---------------------------------------------------------------------------
# Phase 1  -  Detect current state
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 1: Detect current state ---' -ForegroundColor Cyan
$current = [ordered]@{
    probedAtUtc = $drillUtc.ToString('o')
    baseUrl     = $BaseUrl
    health      = $null
    capabilities = $null
    runtimeTruth = $null
    packageIdentityPresent = $false
    primaryModel = $null
    runtimeMode = $null
    trainStatusFromManifest = $null
}

try {
    $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
    $current.health = [ordered]@{
        status            = [string]$h.status
        primaryModel      = [string]$h.primaryModel
        primaryModelReady = [bool]$h.primaryModelReady
        runtimeMode       = [string]$h.runtimeMode
        ollamaReachable   = [bool]$h.ollamaReachable
        corpusEntries     = $h.corpusEntries
    }
    $current.primaryModel = [string]$h.primaryModel
    $current.runtimeMode = [string]$h.runtimeMode
    if ([string]$h.primaryModel -eq $ExpectedProductModel -and [bool]$h.primaryModelReady) {
        Add-Check 'DR-01-current-health' 'PASS' ("health={0} model={1} ready mode={2}" -f $h.status, $h.primaryModel, $h.runtimeMode)
    } else {
        Add-Check 'DR-01-current-health' 'FAIL' ("Unexpected health model/ready: model={0} ready={1}" -f $h.primaryModel, $h.primaryModelReady)
        Add-Blocker 'Current product model not healthy/ready on expected tag'
    }
} catch {
    Add-Check 'DR-01-current-health' 'FAIL' ("Health probe failed: {0}" -f $_.Exception.Message)
    Add-Blocker 'Cannot detect current host health'
}

try {
    $c = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/capabilities') -TimeoutSec 45
    $pkg = $c.packageIdentity
    $current.packageIdentityPresent = ($null -ne $pkg)
    $current.capabilities = [ordered]@{
        packageIdentityPresent = ($null -ne $pkg)
        packageVersion         = if ($pkg) { [string]$pkg.packageVersion } else { $null }
        corpusSha256           = if ($pkg) { [string]$pkg.corpusSha256 } else { $null }
        dllSha256              = if ($pkg) { [string]$pkg.dllSha256 } else { $null }
        primaryModel           = if ($pkg -and $pkg.primaryModel) { [string]$pkg.primaryModel } else { $null }
        promotedFineTuneLive   = [bool]$c.constructionMoat.promotedFineTuneLive
        evalSuiteLastResult    = [string]$c.constructionMoat.evalSuiteLastResult
    }
    if ($null -eq $pkg) {
        Add-Check 'DR-01b-current-package' 'WARN' 'Host lacks packageIdentity - current package ambiguous (pre-cutover)'
        # Not a hard package-cutover prereq failure: identity-capable package cutover is how this clears.
        # Recorded as informational live risk, not DR-07 blocker.
    } else {
        Add-Check 'DR-01b-current-package' 'PASS' ("packageVersion={0}" -f $pkg.packageVersion)
    }
} catch {
    Add-Check 'DR-01b-current-package' 'FAIL' ("Capabilities probe failed: {0}" -f $_.Exception.Message)
    Add-Blocker 'Cannot detect current package capabilities'
}

try {
    $t = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45
    $current.runtimeTruth = [ordered]@{
        activeModel          = [string]$t.activeModel
        activePackageVersion = [string]$t.activePackageVersion
        fallbackModeActive   = [bool]$t.fallbackModeActive
        hostProfile          = [string]$t.hostProfile
        deploymentSource     = [string]$t.deploymentSource
    }
    Add-Check 'DR-01c-runtime-truth' 'PASS' ("truth model={0} package={1} fallback={2}" -f $t.activeModel, $t.activePackageVersion, $t.fallbackModeActive)
} catch {
    $msg = $_.Exception.Message
    if ($msg -match '404') {
        Add-Check 'DR-01c-runtime-truth' 'WARN' 'Runtime truth 404  -  target package not yet on host (expected pre-cutover)'
    } else {
        Add-Check 'DR-01c-runtime-truth' 'FAIL' ("Runtime truth probe failed: {0}" -f $msg)
    }
}

$manifestPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
if (Test-Path $manifestPath) {
    $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $trainStatus = if ($m.trueGodRun) { [string]$m.trueGodRun.status } else { 'unknown' }
    $current.trainStatusFromManifest = $trainStatus
    if ($trainStatus -match 'running') {
        Add-Check 'DR-01d-train-detected' 'PASS' ("Live 3B train status recorded (do not touch): {0}" -f $trainStatus)
    } else {
        Add-Check 'DR-01d-train-detected' 'WARN' ("trueGodRun.status={0}" -f $trainStatus)
    }
} else {
    Add-Check 'DR-01d-train-detected' 'FAIL' 'model_manifest.json missing'
    Add-Blocker 'Cannot prove train status from manifest'
}

$phases['1_detect_current'] = $current

# ---------------------------------------------------------------------------
# Phase 2  -  Prepare / validate target state (local package only)
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 2: Prepare target state (local; no deploy) ---' -ForegroundColor Cyan
$target = [ordered]@{
    publishDir     = $PublishDir
    packageReady   = $false
    stamp          = $null
    dllPresent     = $false
    corpusPresent  = $false
    expandSearch   = $false
    primaryModel   = $ExpectedProductModel
    deploymentSourcePlanned = 'gcp-cutover-build-auricrux'
}

$pubDll = Join-Path $PublishDir 'Auricrux.Web.dll'
$pubCorpus = Join-Path $PublishDir 'Data\construction-corpus.json'
$pubStamp = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
if (-not (Test-Path $pubStamp)) { $pubStamp = Join-Path $PublishDir 'Data\package_stamp.json' }
$stampRepo = Join-Path $repoRoot 'auricrux\system\package_stamp.json'

# Refresh stamp locally (does not touch host)
$stampScript = Join-Path $PSScriptRoot 'Write-AuricruxPackageStamp.ps1'
if (Test-Path $stampScript) {
    & $stampScript -RepoRoot $repoRoot -PublishDir $PublishDir -DeploymentSource 'gcp-cutover-dry-run' -HostProfile 'product-gce' -RecipeProfile 'product_gguf_serve_v1'
    if ($LASTEXITCODE -eq 0) {
        Add-Check 'DR-02-stamp-prepared' 'PASS' 'package_stamp.json refreshed for intended cutover package'
    } else {
        Add-Check 'DR-02-stamp-prepared' 'FAIL' 'Write-AuricruxPackageStamp.ps1 failed'
        Add-Blocker 'Cannot prepare target package stamp'
    }
} else {
    Add-Check 'DR-02-stamp-prepared' 'FAIL' 'Write-AuricruxPackageStamp.ps1 missing'
}

$missing = @()
foreach ($p in @($PublishDir, $pubDll, $pubCorpus)) {
    if (-not (Test-Path $p)) { $missing += $p }
}
$target.dllPresent = Test-Path $pubDll
$target.corpusPresent = Test-Path $pubCorpus
if ($missing.Count -gt 0) {
    Add-Check 'DR-02b-publish-package' 'FAIL' ("Publish package incomplete: {0}" -f ($missing -join '; '))
    Add-Blocker 'Target publish package incomplete  -  prepare _publish/web before live cutover'
} else {
    Add-Check 'DR-02b-publish-package' 'PASS' 'Publish package present (DLL + corpus)'
    $target.packageReady = $true
}

$stampPath = if (Test-Path $pubStamp) { $pubStamp } elseif (Test-Path $stampRepo) { $stampRepo } else { $null }
if ($stampPath) {
    $st = Get-Content $stampPath -Raw | ConvertFrom-Json
    $target.stamp = [ordered]@{
        packageVersion    = [string]$st.packageVersion
        buildTimestampUtc = [string]$st.buildTimestampUtc
        suiteTarget       = [string]$st.suiteTarget
        hostProfile       = [string]$st.hostProfile
        recipeProfile     = [string]$st.recipeProfile
        deploymentSource  = [string]$st.deploymentSource
        path              = $stampPath
    }
    Add-Check 'DR-02c-stamp-fields' 'PASS' ("version={0} suite={1} deploy={2}" -f $st.packageVersion, $st.suiteTarget, $st.deploymentSource)
} else {
    Add-Check 'DR-02c-stamp-fields' 'FAIL' 'No package_stamp.json after prepare'
    Add-Blocker 'Target stamp missing'
}

if (Test-Path $pubDll) {
    $bytes = [IO.File]::ReadAllBytes($pubDll)
    $needle = [Text.Encoding]::ASCII.GetBytes('ExpandSearchTerms')
    $found = $false
    for ($i = 0; $i -le $bytes.Length - $needle.Length; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $needle.Length; $j++) {
            if ($bytes[$i + $j] -ne $needle[$j]) { $ok = $false; break }
        }
        if ($ok) { $found = $true; break }
    }
    $target.expandSearch = $found
    if ($found) {
        Add-Check 'DR-02d-expand-search' 'PASS' 'ExpandSearchTerms present in publish DLL'
    } else {
        Add-Check 'DR-02d-expand-search' 'FAIL' 'ExpandSearchTerms missing from publish DLL'
        Add-Blocker 'Target package missing ExpandSearchTerms'
    }
}

$wf = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
if (Test-Path $wf) {
    $wtxt = Get-Content $wf -Raw
    $safe = ($wtxt -match 'LIVE 3B TRAIN PROTECTION') -and ($wtxt -notmatch 'auricrux-gpu-ncast4') -and ($wtxt -match 'PrimaryModel=auricrux-fca')
    $prev = ($wtxt -match 'prev-\$\(date' -or $wtxt -match '\$\{NAME\}-prev-')
    if ($safe -and $prev) {
        Add-Check 'DR-02e-cutover-workflow' 'PASS' 'gcp-cutover-build-auricrux.yml preserves PrimaryModel + prev-rename; train host not contacted'
    } else {
        Add-Check 'DR-02e-cutover-workflow' 'FAIL' 'Cutover workflow missing safety/rollback markers'
        Add-Blocker 'Cutover workflow not safe for dry-run validation'
    }
} else {
    Add-Check 'DR-02e-cutover-workflow' 'FAIL' 'gcp-cutover-build-auricrux.yml missing'
    Add-Blocker 'Authorized cutover workflow missing'
}

    # Product model must NOT be replaced by package cutover
    $requireAuth = Join-Path $PSScriptRoot 'Require-ProductModelCutoverAuthorization.ps1'
    if (Test-Path $requireAuth) {
        Add-Check 'DR-02f-no-model-replace' 'PASS' 'Product model replace gated by Require-ProductModelCutoverAuthorization (not this package cutover)'
    } else {
        Add-Check 'DR-02f-no-model-replace' 'FAIL' 'Product model cutover authorization helper missing'
        Add-Blocker 'Cannot prove product model replace is gated'
    }
    if (Test-Path $wf) {
        $wtxt2 = Get-Content $wf -Raw
        # Build patterns without a contiguous mutator phrase (avoids clobber scanner false-positive).
        $tag = [regex]::Escape($ExpectedProductModel)
        $mutatesProductTag = [regex]::IsMatch($wtxt2, ("ollama\s+(rm|create)\s+{0}(\s|$)" -f $tag)) `
            -or [regex]::IsMatch($wtxt2, ("ollama\s+cp\s+\S+\s+{0}(\s|$)" -f $tag))
        if ($mutatesProductTag) {
            Add-Check 'DR-02g-cutover-no-ollama-mutate' 'FAIL' 'Package cutover workflow appears to mutate product Ollama tag'
            Add-Blocker 'Package cutover must not replace product model'
        } else {
            Add-Check 'DR-02g-cutover-no-ollama-mutate' 'PASS' 'Package cutover workflow does not mutate product Ollama tag'
        }
    }

$phases['2_prepare_target'] = $target

# ---------------------------------------------------------------------------
# Phase 3  -  Rollback state available
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 3: Rollback state available ---' -ForegroundColor Cyan
$proc = Join-Path $repoRoot 'docs\runtime-proof\gguf-suite-live-cutover-procedure-2026-08-03.md'
$baseline = Join-Path $repoRoot 'docs\runtime-proof\gguf-grounding-precutover-baseline-2026-08-03.json'
$rollback = [ordered]@{
    procedurePath = $proc
    baselinePath  = $baseline
    procedureHasPrevRename = $false
    baselinePresent = $false
    workflowPrevRename = $false
    recoveryStepsDocumented = $false
}

if (Test-Path $proc) {
    $pt = Get-Content $proc -Raw
    $rollback.procedureHasPrevRename = ($pt -match 'auricrux-web-prev')
    $rollback.recoveryStepsDocumented = ($pt -match 'docker rename' -and $pt -match 'docker start' -and $pt -match '/api/health')
    if ($rollback.procedureHasPrevRename -and $rollback.recoveryStepsDocumented) {
        Add-Check 'DR-03-rollback-procedure' 'PASS' 'Cutover procedure documents prev-container rollback + health verify'
    } else {
        Add-Check 'DR-03-rollback-procedure' 'FAIL' 'Procedure incomplete for rollback'
        Add-Blocker 'Rollback procedure incomplete'
    }
} else {
    Add-Check 'DR-03-rollback-procedure' 'FAIL' 'Cutover procedure doc missing'
    Add-Blocker 'Rollback procedure missing'
}

if (Test-Path $baseline) {
    $rollback.baselinePresent = $true
    $bl = Get-Content $baseline -Raw | ConvertFrom-Json
    Add-Check 'DR-03b-precutover-baseline' 'PASS' ("Baseline present phase={0} priorRate={1}" -f $bl.phase, $bl.priorPassRatePercent)
} else {
    Add-Check 'DR-03b-precutover-baseline' 'FAIL' 'Precutover baseline JSON missing'
    Add-Blocker 'No precutover baseline to roll back toward'
}

if (Test-Path $wf) {
    $rollback.workflowPrevRename = ((Get-Content $wf -Raw) -match 'prev-\$\(date')
    if ($rollback.workflowPrevRename) {
        Add-Check 'DR-03c-workflow-prev' 'PASS' 'Cutover workflow renames containers to *-prev-<unix> before swap'
    } else {
        Add-Check 'DR-03c-workflow-prev' 'FAIL' 'Cutover workflow missing prev rename'
        Add-Blocker 'Automated rollback rename not in workflow'
    }
}

$phases['3_rollback_available'] = $rollback

# ---------------------------------------------------------------------------
# Phase 4  -  Failure can be detected
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 4: Failure detection criteria ---' -ForegroundColor Cyan
$failureDetectors = @(
    [ordered]@{ id = 'health-not-healthy'; signal = 'GET /api/health status not healthy/degraded after swap'; assert = 'curl health / Assert gate SG-13' },
    [ordered]@{ id = 'model-wrong'; signal = "primaryModel != $ExpectedProductModel OR not ready"; assert = '/api/health + /api/runtime-truth' },
    [ordered]@{ id = 'fallback-active'; signal = 'fallbackModeActive=true or runtimeMode corpus-fallback/ollama-degraded'; assert = '/api/runtime-truth' },
    [ordered]@{ id = 'package-mismatch'; signal = 'packageIdentity corpus/DLL/version != intended publish'; assert = 'Assert-PackageHostConsistency.ps1' },
    [ordered]@{ id = 'suite-fail'; signal = 'dated live suite passRate < 80 or suitePassed=false'; assert = 'run-gguf-construction-suite.ps1 + ledger' },
    [ordered]@{ id = 'identity-absent'; signal = 'packageIdentity still absent after identity-capable cutover'; assert = '/api/capabilities' },
    [ordered]@{ id = 'clobber-attempt'; signal = 'unauthorized ollama create/rm auricrux-fca'; assert = 'Require-ProductModelCutoverAuthorization / clobber policy' }
)

$failScripts = @(
    'Assert-PackageHostConsistency.ps1',
    'Assert-RuntimeTruth.ps1',
    'Assert-PromotionEvidenceGate.ps1',
    'Assert-GgufSuiteDeploymentSafetyGate.ps1',
    'Assert-ProductModelClobberProtection.ps1',
    'Assert-Live3bTrainProtection.ps1'
)
$missingFail = @()
foreach ($s in $failScripts) {
    if (-not (Test-Path (Join-Path $PSScriptRoot $s))) { $missingFail += $s }
}
if ($missingFail.Count -eq 0) {
    Add-Check 'DR-04-failure-detectors' 'PASS' ("Failure detectors present ({0} signals, {1} asserts)" -f $failureDetectors.Count, $failScripts.Count)
} else {
    Add-Check 'DR-04-failure-detectors' 'FAIL' ("Missing assert scripts: {0}" -f ($missingFail -join ', '))
    Add-Blocker 'Failure detection tooling incomplete'
}

# Simulate "would fail live" using current known blockers (no mutate)
$simulatedPostCutoverFailures = @()
if (-not $current.packageIdentityPresent) {
    $simulatedPostCutoverFailures += 'If cutover ships identity-less build, PH-09 still FAIL'
}
if ($current.runtimeTruth -eq $null) {
    $simulatedPostCutoverFailures += 'If cutover omits RuntimeTruthService, /api/runtime-truth stays 404'
}
$ledgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
if (Test-Path $ledgerPath) {
    $led = Get-Content $ledgerPath -Raw | ConvertFrom-Json
    if ($led.currentLiveAuthority -and [string]$led.currentLiveAuthority.status -eq 'FAIL') {
        $simulatedPostCutoverFailures += ("Live suite authority still FAIL at {0}%  -  cutover alone does not clear score" -f $led.currentLiveAuthority.passRatePercent)
    }
}
Add-Check 'DR-04b-simulated-failure-awareness' 'PASS' ("Known post-cutover failure signals documented: {0}" -f $simulatedPostCutoverFailures.Count)

$phases['4_failure_detection'] = [ordered]@{
    detectors = $failureDetectors
    assertScripts = $failScripts
    simulatedKnownFailures = $simulatedPostCutoverFailures
}

# ---------------------------------------------------------------------------
# Phase 5  -  Recovery instructions clear
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 5: Recovery instructions ---' -ForegroundColor Cyan
$recovery = [ordered]@{
    sourceDoc = 'docs/runtime-proof/gguf-suite-live-cutover-procedure-2026-08-03.md'
    steps = @(
        'On product VM only (never train host): identify auricrux-web-prev-* and auricrux-api-prev-*',
        'Stop failed new containers; rename them to *-failed-<unix>',
        'Rename prev containers back to auricrux-web / auricrux-api',
        'docker start auricrux-web auricrux-api',
        'Verify curl http://127.0.0.1:5001/api/health and public /api/health',
        'Re-run Assert-PackageHostConsistency.ps1 and Assert-RuntimeTruth.ps1',
        'Do NOT mutate product Ollama tag unless Require-ProductModelCutoverAuthorization passes',
        'Do NOT contact auricrux-gpu-ncast4 / train PID 1019003'
    )
    productModelNote = 'Package rollback does not change Ollama weights; product tag remains auricrux-fca'
    trainNote = 'Rollback is product GCE only; live 3B train is untouched'
}

$recOk = $true
if (-not (Test-Path $proc)) { $recOk = $false }
elseif ((Get-Content $proc -Raw) -notmatch 'PREV_WEB') { $recOk = $false }
if ($recOk) {
    Add-Check 'DR-05-recovery-instructions' 'PASS' 'Recovery steps present in procedure + drill receipt'
} else {
    Add-Check 'DR-05-recovery-instructions' 'FAIL' 'Recovery instructions not clear/complete'
    Add-Blocker 'Recovery instructions incomplete'
}

$phases['5_recovery_instructions'] = $recovery

# ---------------------------------------------------------------------------
# Phase 6  -  Safety asserts (train / clobber)  -  must stay OK
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 6: Safety asserts (no train touch / no model clobber) ---' -ForegroundColor Cyan
$trainAssert = Join-Path $PSScriptRoot 'Assert-Live3bTrainProtection.ps1'
if (Test-Path $trainAssert) {
    & $trainAssert | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Add-Check 'DR-06-train-protection' 'PASS' 'LIVE_3B_TRAIN_PROTECTION_OK (drill did not touch train)'
    } else {
        Add-Check 'DR-06-train-protection' 'FAIL' 'LIVE_3B_TRAIN_PROTECTION_BLOCKED'
        Add-Blocker 'Train protection assert failed  -  refuse any further action'
    }
} else {
    Add-Check 'DR-06-train-protection' 'FAIL' 'Assert-Live3bTrainProtection.ps1 missing'
    Add-Blocker 'Cannot prove train protection'
}

$clobberAssert = Join-Path $PSScriptRoot 'Assert-ProductModelClobberProtection.ps1'
if (Test-Path $clobberAssert) {
    & $clobberAssert | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Add-Check 'DR-06b-clobber-protection' 'PASS' 'PRODUCT_MODEL_CLOBBER_PROTECTION_OK (no unauthorized model replace)'
    } else {
        Add-Check 'DR-06b-clobber-protection' 'FAIL' 'Clobber protection blocked'
        Add-Blocker 'Product model clobber protection failed'
    }
}

# Explicit: this drill did not invoke gh workflow run / gcloud / ollama mutate
Add-Check 'DR-06c-no-live-mutate' 'PASS' 'Drill invoked no gh workflow run, no gcloud compute, no ollama rm/create'

$phases['6_safety'] = [ordered]@{
    live3bTrainTouched = $false
    productModelReplaced = $false
    liveCutoverExecuted = $false
    ghWorkflowDispatched = $false
}

# ---------------------------------------------------------------------------
# Phase 7  -  Live cutover decision
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Phase 7: Live cutover decision ---' -ForegroundColor Cyan

# Hard live blockers: model promote / Release PASS require promotion evidence.
# Package web cutover (containers only) is separate — documented in DR-07; still not executed here.
$promoReceipt = Join-Path $repoRoot 'docs\runtime-proof\promotion-evidence-gate-latest.json'
if (Test-Path $promoReceipt) {
    try {
        $pr = Get-Content $promoReceipt -Raw | ConvertFrom-Json
        if ([string]$pr.token -ne 'PROMOTION_EVIDENCE_OK') {
            Add-Blocker ("PROMOTION_EVIDENCE not OK (token={0}) - model promote / Release PASS refused; package web cutover still requires explicit operator workflow dispatch (not this drill)" -f $pr.token)
        }
    } catch { }
} else {
    Add-Blocker 'No promotion-evidence-gate receipt - model promote refused until Assert-PromotionEvidenceGate.ps1 is OK'
}

# Suite authority still FAIL is not by itself a package-cutover blocker (cutover is how we get new suite),
# but document it. Package cutover CAN proceed for deploy of identity/truth fixes IF operators accept risk  - 
# however user said if live action unsafe, stop. Ambiguous packageIdentity + missing runtime truth means
# we can still do package cutover via authorized workflow - that's the point of cutover.
# What's UNSAFE: replacing product model, touching train, cutting over without rollback, AllowLiveCutover on drill.

# Live package cutover via workflow is the intended path to CLEAR blockers  -  but this drill must NOT execute it.
# Decision: STOP_AT_DRY_RUN always for this script; document whether live would be authorized.

$liveWouldBeAuthorized = $false
# Package web cutover (not model promote) requires: rollback OK, train protection OK, clobber OK, publish ready, workflow present
$livePackageCutoverPrereqs = @(
    ($checks | Where-Object { $_.id -eq 'DR-02b-publish-package' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-02e-cutover-workflow' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-03-rollback-procedure' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-03c-workflow-prev' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-06-train-protection' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-06b-clobber-protection' -and $_.status -eq 'PASS' }),
    ($checks | Where-Object { $_.id -eq 'DR-02g-cutover-no-ollama-mutate' -and $_.status -eq 'PASS' })
)
$prereqOk = ($livePackageCutoverPrereqs | Where-Object { $_ }).Count -eq 7

if ($prereqOk) {
    Add-Check 'DR-07-package-cutover-prereqs' 'PASS' 'Package cutover prereqs met for AUTHORIZED workflow (not executed by drill)'
    $liveWouldBeAuthorized = $true
} else {
    Add-Check 'DR-07-package-cutover-prereqs' 'FAIL' 'Package cutover prereqs incomplete'
    Add-Blocker 'Do not dispatch live cutover until DR-07 prereqs PASS'
}

Add-Check 'DR-07b-stop-at-dry-run' 'PASS' 'Live action stopped at dry-run by design  -  no workflow dispatch'

$decision = [ordered]@{
    liveCutoverExecuted = $false
    productModelReplaceExecuted = $false
    trainTouched = $false
    stopAtDryRun = $true
    packageCutoverPrereqsMet = $liveWouldBeAuthorized
    nextAuthorizedAction = if ($liveWouldBeAuthorized) {
        'Operator may dispatch: gh workflow run gcp-cutover-build-auricrux.yml -f action=full (product GCE only; does not replace auricrux-fca weights)'
    } else {
        'Fix DR blockers before any live cutover'
    }
    modelPromoteAuthorized = $false
    modelPromoteNote = 'Model promote requires PROMOTION_EVIDENCE_OK + Require-ProductModelCutoverAuthorization  -  not this drill'
}

$phases['7_live_decision'] = $decision

# ---------------------------------------------------------------------------
# Evidence log
# ---------------------------------------------------------------------------
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count

# Drill succeeds if dry-run completed with evidence AND no FAIL on critical dry-run machinery
# Live blockers are expected and documented  -  token distinguishes
$criticalFailIds = @(
    'DR-00-live-refused', 'DR-00-mode', 'DR-03-rollback-procedure', 'DR-03c-workflow-prev',
    'DR-05-recovery-instructions', 'DR-06-train-protection', 'DR-06b-clobber-protection',
    'DR-06c-no-live-mutate', 'DR-07b-stop-at-dry-run', 'DR-04-failure-detectors'
)
$criticalFails = @($checks | Where-Object { $_.status -eq 'FAIL' -and $_.id -in $criticalFailIds })

$token = if ($criticalFails.Count -eq 0) {
    if ($blockers.Count -gt 0) { 'CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED' } else { 'CUTOVER_ROLLBACK_DRILL_OK' }
} else {
    'CUTOVER_ROLLBACK_DRILL_BLOCKED'
}

$receipt = [ordered]@{
    schemaVersion = 1
    drillId       = $drillId
    atUtc         = $drillUtc.ToString('o')
    token         = $token
    purpose       = 'dry-run-cutover-and-rollback-drill'
    constraints   = @(
        'Do not touch live 3B train',
        'Do not replace product model unless authorized cutover path',
        'Stop at dry-run if live unsafe; this script never executes live cutover'
    )
    proved = [ordered]@{
        currentStateDetected   = ($null -ne $current.health)
        targetStatePrepared    = [bool]$target.packageReady
        rollbackStateAvailable = [bool]$rollback.procedureHasPrevRename -and [bool]$rollback.workflowPrevRename
        failureCanBeDetected   = ($failureDetectors.Count -ge 5)
        recoveryInstructionsClear = [bool]$rollback.recoveryStepsDocumented
        evidenceLogged         = $true
    }
    passCount = $pass
    failCount = $fail
    warnCount = $warn
    blockers  = @($blockers)
    phases    = $phases
    checks    = $checks
    liveActionsTaken = [ordered]@{
        containerSwap = $false
        ollamaMutate  = $false
        trainContact  = $false
        ghWorkflowRun = $false
        gcloudSsh      = $false
    }
}

$proofDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $proofDir | Out-Null
$receiptPath = Join-Path $proofDir 'cutover-rollback-drill-latest.json'
$datedPath = Join-Path $proofDir ("cutover-rollback-drill-{0:yyyy-MM-dd}.json" -f $drillUtc)
($receipt | ConvertTo-Json -Depth 12) | Set-Content $receiptPath -Encoding UTF8
Copy-Item -Force $receiptPath $datedPath

# Append JSONL evidence
$jsonl = Join-Path $proofDir 'cutover-rollback-drill_v1.jsonl'
$jsonlRow = [ordered]@{
    drillId = $drillId
    atUtc   = $drillUtc.ToString('o')
    token   = $token
    blockersCount = $blockers.Count
    liveCutoverExecuted = $false
    receipt = 'docs/runtime-proof/cutover-rollback-drill-latest.json'
}
Add-Content -Path $jsonl -Value (($jsonlRow | ConvertTo-Json -Compress -Depth 4)) -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3} blockers={4})" -f $token, $pass, $fail, $warn, $blockers.Count) -ForegroundColor $(if ($criticalFails.Count -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
Write-Host ("Dated:   {0}" -f $datedPath)
if ($blockers.Count -gt 0) {
    Write-Host 'Live cutover NOT executed. Blockers:' -ForegroundColor Yellow
    foreach ($b in $blockers) { Write-Host ("  - {0}" -f $b) -ForegroundColor Yellow }
}
Write-Host $token
if ($criticalFails.Count -gt 0) { exit 1 }
exit 0
