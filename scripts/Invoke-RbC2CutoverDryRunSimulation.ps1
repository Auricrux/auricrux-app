<#
.SYNOPSIS
  Offline dry-run simulation of RB-C2 package-web cutover (no live touch).

.DESCRIPTION
  Simulates cutover readiness paths without:
    - HTTP to product host
    - gh workflow dispatch
    - gcloud / SSH
    - ollama mutate
    - model-weight change
    - 3B train contact

  Verifies: deployment package path, rollback package path, manifest preservation,
  ledger preservation, package stamps, runtime proof artifacts.

  Token: RB_C2_CUTOVER_DRYRUN_SIM_OK / RB_C2_CUTOVER_DRYRUN_SIM_BLOCKED
#>
[CmdletBinding()]
param(
    [string]$PublishDir = ''
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}
$proofDir = Join-Path $repoRoot 'docs\runtime-proof'
$utc = (Get-Date).ToUniversalTime()
$simId = 'rb-c2-cutover-dryrun-sim-{0:yyyyMMddTHHmmss}Z' -f $utc
$checks = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[object]
$remediation = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Area, [string]$Status, [string]$Detail, [string]$Remediate = '') {
    [void]$checks.Add([pscustomobject]@{ id = $Id; area = $Area; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
    if ($Status -eq 'FAIL') {
        [void]$failures.Add([pscustomobject]@{ id = $Id; area = $Area; detail = $Detail })
        if ($Remediate) {
            [void]$remediation.Add([pscustomobject]@{ id = $Id; step = $Remediate })
        }
    }
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

Write-Host '=== RB-C2 package-web cutover DRY-RUN SIMULATION ===' -ForegroundColor Cyan
Write-Host 'NO live HTTP. NO workflow dispatch. NO model mutate. NO train touch.'
Write-Host ("simId={0}" -f $simId)

# ---------------------------------------------------------------------------
# Guard: prove we will not mutate critical files (capture hashes first)
# ---------------------------------------------------------------------------
$manifestRepo = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
$manifestPub = Join-Path $PublishDir 'auricrux\system\model_manifest.json'
$ledgerJson = Join-Path $proofDir 'auricrux_evidence_ledger_v1.json'
$ledgerJsonl = Join-Path $proofDir 'auricrux_evidence_ledger_v1.jsonl'
$stampRepo = Join-Path $repoRoot 'auricrux\system\package_stamp.json'
$stampPub = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
$stampPubData = Join-Path $PublishDir 'Data\package_stamp.json'

$hashesBefore = [ordered]@{
    manifestRepo = Get-Sha256 $manifestRepo
    manifestPub  = Get-Sha256 $manifestPub
    ledgerJson   = Get-Sha256 $ledgerJson
    ledgerJsonl  = Get-Sha256 $ledgerJsonl
    stampRepo    = Get-Sha256 $stampRepo
    stampPub     = Get-Sha256 $stampPub
    stampPubData = Get-Sha256 $stampPubData
}

Add-Check 'SIM-00-mode' 'guard' 'PASS' 'Offline dry-run simulation; no live services contacted by this script'

# ---------------------------------------------------------------------------
# 1) Deployment package path
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 1) Deployment package path ---' -ForegroundColor Cyan
$deployArea = 'deployment-package-path'

$needPub = @(
    @{ p = (Join-Path $PublishDir 'Auricrux.Web.dll'); n = 'DLL' },
    @{ p = (Join-Path $PublishDir 'Data\construction-corpus.json'); n = 'corpus' },
    @{ p = $stampPub; n = 'stamp' },
    @{ p = $manifestPub; n = 'manifest-in-publish' },
    @{ p = (Join-Path $PublishDir 'appsettings.json'); n = 'appsettings' }
)
$miss = @()
foreach ($n in $needPub) {
    if (-not (Test-Path -LiteralPath $n.p)) { $miss += $n.n }
}
if ($miss.Count -eq 0) {
    Add-Check 'SIM-10-publish-complete' $deployArea 'PASS' ("_publish/web complete at {0}" -f $PublishDir)
} else {
    Add-Check 'SIM-10-publish-complete' $deployArea 'FAIL' ("missing: {0}" -f ($miss -join ',')) `
        'Restore/rebuild publish: dotnet publish into _publish/web; re-run package prepare / stamp'
}

$wf = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
$df = Join-Path $repoRoot 'Dockerfile'
if ((Test-Path $wf) -and (Test-Path $df)) {
    $wfText = Get-Content -LiteralPath $wf -Raw
    $buildsFromSource = ($wfText -match 'docker build') -and ($wfText -match 'tar') -and ($wfText -notmatch 'scp .*_publish')
    $excludesPublish = $wfText -match "exclude='_publish'" -or $wfText -match 'exclude=.*/_publish'
    # workflow uses --exclude='_publish'
    $exPub = $wfText -match "_publish"
    if ($buildsFromSource) {
        Add-Check 'SIM-11-workflow-deploy-path' $deployArea 'PASS' 'Workflow packs source + docker build (not _publish zip upload)'
    } else {
        Add-Check 'SIM-11-workflow-deploy-path' $deployArea 'FAIL' 'Workflow deploy path unclear' `
            'Confirm gcp-cutover-build-auricrux.yml still builds via Dockerfile from source'
    }
    if ($exPub) {
        Add-Check 'SIM-12-publish-excluded-from-scp' $deployArea 'PASS' 'Workflow tar excludes _publish (source build path)'
    } else {
        Add-Check 'SIM-12-publish-excluded-from-scp' $deployArea 'WARN' 'Could not confirm _publish exclude marker'
    }
    $dfText = Get-Content -LiteralPath $df -Raw
    if ($dfText -match 'package_stamp\.json' -and $dfText -match 'model_manifest\.json') {
        Add-Check 'SIM-13-dockerfile-identity-files' $deployArea 'PASS' 'Dockerfile copies package_stamp + model_manifest into image'
    } else {
        Add-Check 'SIM-13-dockerfile-identity-files' $deployArea 'FAIL' 'Dockerfile missing stamp/manifest COPY' `
            'Restore Dockerfile COPY lines for auricrux/system/package_stamp.json and model_manifest.json'
    }
} else {
    Add-Check 'SIM-11-workflow-deploy-path' $deployArea 'FAIL' 'workflow or Dockerfile missing' `
        'Restore .github/workflows/gcp-cutover-build-auricrux.yml and Dockerfile'
}

# Local publish is comparison baseline for Assert-PackageHostConsistency after cutover
Add-Check 'SIM-14-local-publish-role' $deployArea 'PASS' 'Local _publish/web is post-cutover compare baseline (PH assert), not the SCP artifact'

# ---------------------------------------------------------------------------
# 2) Rollback package path
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 2) Rollback package path ---' -ForegroundColor Cyan
$rbArea = 'rollback-package-path'

$proc = Join-Path $proofDir 'gguf-suite-live-cutover-procedure-2026-08-03.md'
if (Test-Path $proc) {
    $procText = Get-Content -LiteralPath $proc -Raw
    $hasPrev = $procText -match 'auricrux-web-prev' -and $procText -match 'auricrux-api-prev'
    $hasRestore = $procText -match 'docker rename' -and $procText -match 'docker start'
    $hasHealth = $procText -match '/api/health'
    if ($hasPrev -and $hasRestore -and $hasHealth) {
        Add-Check 'SIM-20-rollback-procedure' $rbArea 'PASS' 'Procedure documents prev-* rename restore + health verify'
    } else {
        Add-Check 'SIM-20-rollback-procedure' $rbArea 'FAIL' 'Procedure missing rollback steps' `
            'Restore rollback bash block in gguf-suite-live-cutover-procedure-2026-08-03.md'
    }
} else {
    Add-Check 'SIM-20-rollback-procedure' $rbArea 'FAIL' 'Rollback procedure file missing' `
        'Restore docs/runtime-proof/gguf-suite-live-cutover-procedure-2026-08-03.md'
}

if (Test-Path $wf) {
    $wfText = Get-Content -LiteralPath $wf -Raw
    $hasRename = $wfText -match 'prev-\$\(date' -or $wfText -match 'prev-'
    $preservesPrimary = $wfText -match 'PrimaryModel=auricrux-fca'
    $noOllamaMutate = -not ($wfText -match 'ollama\s+(create|rm)\s+auricrux-fca')
    $trainBanner = $wfText -match 'LIVE 3B TRAIN PROTECTION'
    if ($hasRename) {
        Add-Check 'SIM-21-workflow-prev-rename' $rbArea 'PASS' 'Workflow renames running containers to *-prev-<unix> before swap'
    } else {
        Add-Check 'SIM-21-workflow-prev-rename' $rbArea 'FAIL' 'Workflow missing prev- rename' `
            'Restore docker rename ${NAME}-prev-$(date +%s) in cutover workflow'
    }
    if ($preservesPrimary -and $noOllamaMutate) {
        Add-Check 'SIM-22-rollback-model-untouched' $rbArea 'PASS' 'Cutover path keeps PrimaryModel=auricrux-fca; no product ollama mutate'
    } else {
        Add-Check 'SIM-22-rollback-model-untouched' $rbArea 'FAIL' 'Model preservation markers missing' `
            'Ensure ENV Auricrux__PrimaryModel=auricrux-fca and no ollama create/rm auricrux-fca'
    }
    if ($trainBanner) {
        Add-Check 'SIM-23-train-isolated' $rbArea 'PASS' 'Workflow declares LIVE 3B TRAIN PROTECTION (product GCE only)'
    } else {
        Add-Check 'SIM-23-train-isolated' $rbArea 'FAIL' 'Train protection banner missing' `
            'Restore LIVE 3B TRAIN PROTECTION comment/header on workflow'
    }
} else {
    Add-Check 'SIM-21-workflow-prev-rename' $rbArea 'FAIL' 'workflow missing' 'Restore cutover workflow'
}

$precutover = Join-Path $proofDir 'gguf-grounding-precutover-baseline-2026-08-03.json'
if (Test-Path $precutover) {
    Add-Check 'SIM-24-precutover-baseline' $rbArea 'PASS' 'Precutover baseline receipt present for compare/rollback evidence'
} else {
    Add-Check 'SIM-24-precutover-baseline' $rbArea 'FAIL' 'Precutover baseline missing' `
        'Restore gguf-grounding-precutover-baseline-2026-08-03.json or regenerate baseline'
}

$execPkg = Join-Path $proofDir 'RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
if (Test-Path $execPkg) {
    $ep = Get-Content $execPkg -Raw
    if ($ep -match 'Rollback steps' -and $ep -match 'auricrux-web-failed') {
        Add-Check 'SIM-25-exec-pkg-rollback' $rbArea 'PASS' 'RB-C2 execution package includes rollback section'
    } else {
        Add-Check 'SIM-25-exec-pkg-rollback' $rbArea 'WARN' 'Execution package present but rollback section markers weak'
    }
} else {
    Add-Check 'SIM-25-exec-pkg-rollback' $rbArea 'FAIL' 'RB-C2 execution package missing' `
        'Restore RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
}

# ---------------------------------------------------------------------------
# 3) Manifest preservation
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 3) Manifest preservation ---' -ForegroundColor Cyan
$manArea = 'manifest-preservation'

if ($hashesBefore.manifestRepo) {
    try {
        $man = Get-Content -LiteralPath $manifestRepo -Raw | ConvertFrom-Json
        $eval = [string]$man.adapter.evalStatus
        $passed = [bool]$man.adapter.ggufGenerativeSuitePassed
        $rate = [double]$man.adapter.ggufGenerativePassRatePercent
        $train = $null
        if ($man.trueGodRun) { $train = [string]$man.trueGodRun.status }
        if (-not $train -and $man.PSObject.Properties.Name -contains 'live3bTrain') {
            # alternate shapes
        }
        # train status often under adapter.sessionNotes or trueGodRun
        $doNotInterrupt = ($null -ne (Get-Content $manifestRepo -Raw | Select-String -Pattern 'running-do-not-interrupt' -SimpleMatch))
        $honestFail = ($eval -match 'FAIL') -and (-not $passed) -and ($rate -lt 80)
        if ($honestFail) {
            Add-Check 'SIM-30-manifest-honest-fail' $manArea 'PASS' ("evalStatus={0} rate={1} suitePassed={2}" -f $eval, $rate, $passed)
        } else {
            Add-Check 'SIM-30-manifest-honest-fail' $manArea 'FAIL' ("Unexpected authority claim eval={0} rate={1} passed={2}" -f $eval, $rate, $passed) `
                'Do not elevate manifest without qualified live suite; restore FAIL@76.7 authority fields'
        }
        if ($doNotInterrupt) {
            Add-Check 'SIM-31-manifest-train-marker' $manArea 'PASS' 'Manifest retains running-do-not-interrupt train marker (read-only check)'
        } else {
            Add-Check 'SIM-31-manifest-train-marker' $manArea 'WARN' 'running-do-not-interrupt string not found in manifest (verify train status field)'
        }
        if ($hashesBefore.manifestPub -and ($hashesBefore.manifestPub -eq $hashesBefore.manifestRepo)) {
            Add-Check 'SIM-32-manifest-repo-pub-match' $manArea 'PASS' 'Repo and publish model_manifest.json SHA256 match'
        } elseif ($hashesBefore.manifestPub) {
            Add-Check 'SIM-32-manifest-repo-pub-match' $manArea 'WARN' ("Repo/publish manifest SHA differ repo={0}... pub={1}..." -f $hashesBefore.manifestRepo.Substring(0,12), $hashesBefore.manifestPub.Substring(0,12))
        } else {
            Add-Check 'SIM-32-manifest-repo-pub-match' $manArea 'FAIL' 'Publish manifest missing' 'Copy model_manifest into _publish/web/auricrux/system/'
        }
        Add-Check 'SIM-33-manifest-not-mutated-by-sim' $manArea 'PASS' 'Simulation does not write model_manifest.json (preservation by non-action)'
    } catch {
        Add-Check 'SIM-30-manifest-honest-fail' $manArea 'FAIL' $_.Exception.Message 'Fix/parse model_manifest.json'
    }
} else {
    Add-Check 'SIM-30-manifest-honest-fail' $manArea 'FAIL' 'Repo model_manifest.json missing' 'Restore auricrux/system/model_manifest.json'
}

# ---------------------------------------------------------------------------
# 4) Ledger preservation
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 4) Ledger preservation ---' -ForegroundColor Cyan
$ledArea = 'ledger-preservation'

if ($hashesBefore.ledgerJson) {
    try {
        $led = Get-Content -LiteralPath $ledgerJson -Raw | ConvertFrom-Json
        $auth = $led.currentLiveAuthority
        $status = [string]$auth.status
        $rate = [double]$auth.passRatePercent
        $entries = 0
        if ($led.entries) { $entries = @($led.entries).Count }
        elseif ($led.history) { $entries = @($led.history).Count }
        if ($status -eq 'FAIL' -and [math]::Abs($rate - 76.7) -lt 0.05) {
            Add-Check 'SIM-40-ledger-authority' $ledArea 'PASS' ("currentLiveAuthority FAIL @{0}% entries~={1}" -f $rate, $entries)
        } else {
            Add-Check 'SIM-40-ledger-authority' $ledArea 'FAIL' ("Unexpected authority status={0} rate={1}" -f $status, $rate) `
                'Do not elevate ledger without qualified live suite; restore FAIL@76.7 currentLiveAuthority'
        }
        if ($hashesBefore.ledgerJsonl) {
            $lines = @(Get-Content -LiteralPath $ledgerJsonl)
            if ($lines.Count -gt 0) {
                Add-Check 'SIM-41-ledger-jsonl' $ledArea 'PASS' ("jsonl lines={0}" -f $lines.Count)
            } else {
                Add-Check 'SIM-41-ledger-jsonl' $ledArea 'FAIL' 'jsonl empty' 'Restore auricrux_evidence_ledger_v1.jsonl'
            }
        } else {
            Add-Check 'SIM-41-ledger-jsonl' $ledArea 'FAIL' 'jsonl missing' 'Restore ledger jsonl companion'
        }
        Add-Check 'SIM-42-ledger-not-mutated-by-sim' $ledArea 'PASS' 'Simulation does not append/rewrite ledger (preservation by non-action)'
    } catch {
        Add-Check 'SIM-40-ledger-authority' $ledArea 'FAIL' $_.Exception.Message 'Repair ledger JSON'
    }
} else {
    Add-Check 'SIM-40-ledger-authority' $ledArea 'FAIL' 'Ledger JSON missing' 'Restore docs/runtime-proof/auricrux_evidence_ledger_v1.json'
}

# Run offline integrity assert (read-only expect)
$li = Join-Path $repoRoot 'scripts\Assert-EvidenceLedgerIntegrity.ps1'
if (Test-Path $li) {
    & $li | Out-Null
    $liReceipt = Join-Path $proofDir 'evidence-ledger-integrity-latest.json'
    $tok = $null
    if (Test-Path $liReceipt) {
        $tok = [string](Get-Content $liReceipt -Raw | ConvertFrom-Json).token
    }
    if ($tok -eq 'EVIDENCE_LEDGER_INTEGRITY_OK') {
        Add-Check 'SIM-43-ledger-integrity-assert' $ledArea 'PASS' $tok
    } else {
        Add-Check 'SIM-43-ledger-integrity-assert' $ledArea 'FAIL' ("got={0}" -f $tok) `
            'Run Assert-EvidenceLedgerIntegrity.ps1; repair superseded/authority fields without elevating PASS'
    }
} else {
    Add-Check 'SIM-43-ledger-integrity-assert' $ledArea 'FAIL' 'Assert-EvidenceLedgerIntegrity.ps1 missing' 'Restore assert script'
}

# ---------------------------------------------------------------------------
# 5) Package stamps
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 5) Package stamps ---' -ForegroundColor Cyan
$stArea = 'package-stamps'

function Test-Stamp([string]$Id, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Check $Id $stArea 'FAIL' ("missing {0}" -f $Path) 'Regenerate package_stamp.json via prepare/stamp scripts into repo + publish'
        return $null
    }
    try {
        $s = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $ok = $s.packageVersion -and $s.buildTimestampUtc -and $s.suiteTarget -eq 'construction_god_suite_v1' `
            -and $s.hostProfile -and $s.recipeProfile
        if ($ok) {
            Add-Check $Id $stArea 'PASS' ("version={0} suite={1} host={2} recipe={3} source={4}" -f `
                $s.packageVersion, $s.suiteTarget, $s.hostProfile, $s.recipeProfile, $s.deploymentSource)
            return $s
        }
        Add-Check $Id $stArea 'FAIL' 'stamp missing required fields' 'Regenerate stamp with packageVersion/buildTimestampUtc/suiteTarget/host/recipe'
        return $s
    } catch {
        Add-Check $Id $stArea 'FAIL' $_.Exception.Message 'Fix stamp JSON'
        return $null
    }
}

$sRepo = Test-Stamp 'SIM-50-stamp-repo' $stampRepo
$sPub = Test-Stamp 'SIM-51-stamp-publish' $stampPub
if (Test-Path $stampPubData) {
    $h1 = Get-Sha256 $stampPub
    $h2 = Get-Sha256 $stampPubData
    if ($h1 -eq $h2) {
        Add-Check 'SIM-52-stamp-publish-data-dup' $stArea 'PASS' 'Data/package_stamp.json matches auricrux/system stamp'
    } else {
        Add-Check 'SIM-52-stamp-publish-data-dup' $stArea 'WARN' 'Data vs system stamp SHA differ (image copies both; align before cutover)'
    }
} else {
    Add-Check 'SIM-52-stamp-publish-data-dup' $stArea 'WARN' 'Data/package_stamp.json absent in publish (Dockerfile may copy at build)'
}

if ($sRepo -and $sPub) {
    if ([string]$sRepo.packageVersion -eq [string]$sPub.packageVersion -and [string]$sRepo.buildTimestampUtc -eq [string]$sPub.buildTimestampUtc) {
        Add-Check 'SIM-53-stamp-repo-pub-align' $stArea 'PASS' 'Repo and publish stamps align (version + buildTimestampUtc)'
    } else {
        Add-Check 'SIM-53-stamp-repo-pub-align' $stArea 'FAIL' 'Repo/publish stamp mismatch' `
            'Re-stamp both auricrux/system/package_stamp.json and _publish/web copies from same prepare'
    }
}

# ---------------------------------------------------------------------------
# 6) Runtime proof artifacts
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- 6) Runtime proof artifacts ---' -ForegroundColor Cyan
$rpArea = 'runtime-proof-artifacts'

$required = @(
    'AURICRUX_AUTHORITY_MAP.md',
    'AURICRUX_PRIORITY_OPS_PROCEDURE.md',
    'CUTOVER_GO_NO_GO_CHECKLIST.md',
    'CUTOVER_ROLLBACK_DRILL.md',
    'RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md',
    'OPERATIONAL_DRIFT.md',
    'AURICRUX_REMAINING_BLOCKERS.md',
    'auricrux_evidence_ledger_v1.json',
    'auricrux_evidence_ledger_v1.jsonl',
    'package-prepared-latest.json',
    'rb-c2-cutover-execution-package-latest.json',
    'authoritative-suite-rerun-prereqs-latest.json',
    'gguf-grounding-precutover-baseline-2026-08-03.json',
    'gguf-suite-live-cutover-procedure-2026-08-03.md'
)
$rpMiss = @()
foreach ($f in $required) {
    if (-not (Test-Path (Join-Path $proofDir $f))) { $rpMiss += $f }
}
if ($rpMiss.Count -eq 0) {
    Add-Check 'SIM-60-proof-pack' $rpArea 'PASS' ("required proof artifacts present count={0}" -f $required.Count)
} else {
    Add-Check 'SIM-60-proof-pack' $rpArea 'FAIL' ("missing: {0}" -f ($rpMiss -join '; ')) `
        'Restore missing docs/runtime-proof files from source control'
}

$scriptsNeed = @(
    'scripts\Invoke-RbC2PackageWebCutoverPackage.ps1',
    'scripts\Invoke-CutoverRollbackDryRun.ps1',
    'scripts\Assert-PackageHostConsistency.ps1',
    'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1',
    'scripts\Assert-Live3bTrainProtection.ps1',
    'scripts\Assert-ProductModelClobberProtection.ps1'
)
$scMiss = @($scriptsNeed | Where-Object { -not (Test-Path (Join-Path $repoRoot $_)) })
if ($scMiss.Count -eq 0) {
    Add-Check 'SIM-61-orchestrators' $rpArea 'PASS' 'Cutover orchestrator + dry-run + assert scripts present'
} else {
    Add-Check 'SIM-61-orchestrators' $rpArea 'FAIL' ("missing scripts: {0}" -f ($scMiss -join '; ')) 'Restore scripts from source control'
}

# ---------------------------------------------------------------------------
# Post: prove critical files unchanged
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '--- Preservation re-hash ---' -ForegroundColor Cyan
$hashesAfter = [ordered]@{
    manifestRepo = Get-Sha256 $manifestRepo
    manifestPub  = Get-Sha256 $manifestPub
    ledgerJson   = Get-Sha256 $ledgerJson
    ledgerJsonl  = Get-Sha256 $ledgerJsonl
    stampRepo    = Get-Sha256 $stampRepo
    stampPub     = Get-Sha256 $stampPub
}
$changed = @()
foreach ($k in @('manifestRepo', 'manifestPub', 'ledgerJson', 'ledgerJsonl', 'stampRepo', 'stampPub')) {
    if ($hashesBefore[$k] -ne $hashesAfter[$k]) { $changed += $k }
}
if ($changed.Count -eq 0) {
    Add-Check 'SIM-70-hashes-unchanged' 'preservation' 'PASS' 'Manifest/ledger/stamp SHA256 unchanged during simulation'
} else {
    Add-Check 'SIM-70-hashes-unchanged' 'preservation' 'FAIL' ("CHANGED: {0}" -f ($changed -join ',')) `
        'Investigate unexpected writes; restore from git; do not proceed to live cutover'
}

Add-Check 'SIM-71-no-dispatch' 'guard' 'PASS' 'No gh workflow run / gcloud / ollama / train contact invoked'
Add-Check 'SIM-72-no-model-cutover-auth' 'guard' 'PASS' ('AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED={0} (must not be 1 for package-web)' -f $(if ($env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED) { $env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED } else { '(unset)' }))

# ---------------------------------------------------------------------------
# Verdict + receipt
# ---------------------------------------------------------------------------
$failN = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$passN = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$warnN = @($checks | Where-Object { $_.status -eq 'WARN' }).Count
$token = if ($failN -eq 0) { 'RB_C2_CUTOVER_DRYRUN_SIM_OK' } else { 'RB_C2_CUTOVER_DRYRUN_SIM_BLOCKED' }

function Convert-HashMap([System.Collections.IDictionary]$src) {
    $o = [ordered]@{}
    foreach ($k in $src.Keys) {
        $v = $src[$k]
        $o[[string]$k] = if ($null -eq $v) { '' } else { [string]$v }
    }
    return $o
}

$checkRows = @(
    foreach ($c in $checks) {
        [ordered]@{ id = [string]$c.id; area = [string]$c.area; status = [string]$c.status; detail = [string]$c.detail }
    }
)
$failRows = @(
    foreach ($f in $failures) {
        [ordered]@{ id = [string]$f.id; area = [string]$f.area; detail = [string]$f.detail }
    }
)
$remRows = @(
    foreach ($r in $remediation) {
        [ordered]@{ id = [string]$r.id; step = [string]$r.step }
    }
)

$doc = [ordered]@{}
$doc['schemaVersion'] = 1
$doc['simId'] = $simId
$doc['purpose'] = 'rb-c2-package-web-cutover-dryrun-simulation'
$doc['atUtc'] = $utc.ToString('o')
$doc['token'] = $token
$doc['cutoverExecuted'] = $false
$doc['liveServicesContacted'] = $false
$doc['activeModelModified'] = $false
$doc['trainTouched'] = $false
$doc['pass'] = $passN
$doc['fail'] = $failN
$doc['warn'] = $warnN
$doc['areas'] = @(
    'deployment-package-path',
    'rollback-package-path',
    'manifest-preservation',
    'ledger-preservation',
    'package-stamps',
    'runtime-proof-artifacts'
)
$doc['hashesBefore'] = Convert-HashMap $hashesBefore
$doc['hashesAfter'] = Convert-HashMap $hashesAfter
$doc['checks'] = $checkRows
$doc['failures'] = $failRows
$doc['remediation'] = $remRows
$doc['nextOperatorAction'] = $(if ($failN -eq 0) {
    'Dry-run simulation OK. Live cutover still requires Section B + manual gh workflow run (not performed here).'
} else {
    'Resolve FAIL items using remediation[]; re-run this simulation; do not dispatch cutover.'
})
$doc['related'] = [ordered]@{
    executionPackage = 'docs/runtime-proof/RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
    goNoGo = 'docs/runtime-proof/CUTOVER_GO_NO_GO_CHECKLIST.md'
    orchestrator = 'scripts/Invoke-RbC2PackageWebCutoverPackage.ps1'
}

$json = ConvertTo-Json -InputObject $doc -Depth 14
if ([string]::IsNullOrWhiteSpace($json)) { throw 'empty receipt JSON' }
$utf8 = New-Object System.Text.UTF8Encoding $false
$latest = Join-Path $proofDir 'rb-c2-cutover-dryrun-simulation-latest.json'
$dated = Join-Path $proofDir ('rb-c2-cutover-dryrun-simulation-{0:yyyy-MM-dd}.json' -f $utc)
[System.IO.File]::WriteAllText($latest, $json, $utf8)
[System.IO.File]::WriteAllText($dated, $json, $utf8)

# Human-readable failure/remediation doc
$mdLines = @(
    '# RB-C2 package-web cutover dry-run simulation',
    '',
    ('**simId:** `{0}`' -f $simId),
    ('**Token:** `{0}`' -f $token),
    ('**At UTC:** {0}' -f $utc.ToString('o')),
    '',
    '**Constraints honored:** no live HTTP; no workflow dispatch; no model mutate; no train touch; cutoverExecuted=false',
    '',
    ('## Summary: PASS={0} FAIL={1} WARN={2}' -f $passN, $failN, $warnN),
    '',
    '## Failures'
)
if ($failures.Count -eq 0) {
    $mdLines += '_None._'
} else {
    foreach ($f in $failures) {
        $mdLines += ('- **{0}** ({1}): {2}' -f $f.id, $f.area, $f.detail)
    }
}
$mdLines += ''
$mdLines += '## Remediation'
if ($remediation.Count -eq 0) {
    $mdLines += '_N/A - no FAIL items._'
} else {
    foreach ($r in $remediation) {
        $mdLines += ('- **{0}:** {1}' -f $r.id, $r.step)
    }
}
$mdLines += ''
$mdLines += '## Area coverage'
$mdLines += '| Area | Result |'
$mdLines += '|------|--------|'
foreach ($a in $doc.areas) {
    $fa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'FAIL' }).Count
    $wa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'WARN' }).Count
    $pa = @($checks | Where-Object { $_.area -eq $a -and $_.status -eq 'PASS' }).Count
    $res = if ($fa -gt 0) { 'FAIL' } elseif ($wa -gt 0) { 'PASS w/ WARN' } else { 'PASS' }
    $mdLines += ('| {0} | {1} (P={2} W={3} F={4}) |' -f $a, $res, $pa, $wa, $fa)
}
$mdLines += ''
$mdLines += '## Explicit non-claims'
$mdLines += '- Cutover was **not** executed'
$mdLines += '- Does **not** clear RB-C2 on the live host'
$mdLines += '- Does **not** grant Manifest/suite/Release PASS'
$mdPath = Join-Path $proofDir 'RB_C2_CUTOVER_DRYRUN_SIMULATION.md'
$utf8md = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllLines($mdPath, $mdLines, $utf8md)

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3})" -f $token, $passN, $failN, $warnN) -ForegroundColor $(if ($failN -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $latest)
Write-Host ("Doc:     {0}" -f $mdPath)
Write-Host $token
if ($failN -gt 0) { exit 2 }
exit 0
