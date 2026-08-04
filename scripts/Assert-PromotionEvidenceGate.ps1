<#
.SYNOPSIS
  Evidence-based promotion gate for Auricrux model/package promotion.
.DESCRIPTION
  A model or package may not be promoted unless evidence proves:
  correct host, correct package, correct model, required suite score,
  no unsafe fallback, no clobber, rollback exists, truthful manifest,
  evidence ledger updated.

  Missing evidence = BLOCKED (never assume).
  Token: PROMOTION_EVIDENCE_OK / PROMOTION_EVIDENCE_BLOCKED
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedProductModel = 'auricrux-fca',
    [string]$ExpectedHostProfile = 'product-gce',
    [double]$PassThresholdPercent = 80,
    [string]$PublishDir = '',
    [switch]$SkipLiveProbes,
    [switch]$AllowEvidenceIncomplete
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$checks = New-Object System.Collections.Generic.List[object]
$evidence = [ordered]@{}

function Add-Check([string]$Id, [string]$Status, [string]$Detail, [string]$EvidenceRef = '') {
    [void]$checks.Add([pscustomobject]@{
            id          = $Id
            status      = $Status
            detail      = $Detail
            evidenceRef = $EvidenceRef
        })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Get-Sha256Lower([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

Write-Host '=== Auricrux promotion evidence gate ===' -ForegroundColor Cyan
Write-Host 'Evidence-based only. Missing evidence = BLOCKED.'
Write-Host ("BaseUrl={0} model={1} threshold={2}%" -f $BaseUrl, $ExpectedProductModel, $PassThresholdPercent)

# --- PG-00 policy present ---
$policyPath = Join-Path $repoRoot 'auricrux\system\promotion_evidence_policy_v1.json'
if (-not (Test-Path $policyPath)) {
    Add-Check 'PG-00-policy' 'FAIL' 'promotion_evidence_policy_v1.json missing' $policyPath
} else {
    try {
        $policy = Get-Content $policyPath -Raw | ConvertFrom-Json
        $need = @('correct_host', 'correct_package', 'correct_model', 'suite_score_met', 'no_unsafe_fallback', 'no_clobber_event', 'rollback_exists', 'manifest_truthful', 'evidence_ledger_updated')
        $ids = @($policy.requiredEvidence | ForEach-Object { [string]$_.id })
        $missing = @($need | Where-Object { $_ -notin $ids })
        if ($missing.Count -gt 0) {
            Add-Check 'PG-00-policy' 'FAIL' ("Policy missing required ids: {0}" -f ($missing -join ', ')) $policyPath
        } else {
            Add-Check 'PG-00-policy' 'PASS' 'Policy lists all nine required evidence classes' $policyPath
            if ($policy.defaults.passThresholdPercent) {
                $PassThresholdPercent = [double]$policy.defaults.passThresholdPercent
            }
        }
    } catch {
        Add-Check 'PG-00-policy' 'FAIL' ("Policy parse failed: {0}" -f $_.Exception.Message) $policyPath
    }
}

# --- Shared live probes ---
$liveHealth = $null
$liveTruth = $null
$liveCap = $null
$livePkg = $null
$hostOk = $false

if ($SkipLiveProbes) {
    Add-Check 'PG-LIVE' 'WARN' 'Live probes skipped (-SkipLiveProbes). Host/package/model/fallback cannot be proven — treat as incomplete for promotion.'
} else {
    try {
        $uriHost = ([Uri]$BaseUrl).Host
        if ($uriHost -ne $ExpectedHost) {
            Add-Check 'PG-01-correct-host-url' 'FAIL' ("BaseUrl host={0} expected={1}" -f $uriHost, $ExpectedHost) $BaseUrl
        } else {
            Add-Check 'PG-01-correct-host-url' 'PASS' ("BaseUrl host={0}" -f $uriHost) $BaseUrl
            $hostOk = $true
        }
    } catch {
        Add-Check 'PG-01-correct-host-url' 'FAIL' ("BaseUrl parse failed: {0}" -f $_.Exception.Message) $BaseUrl
    }

    try {
        $liveHealth = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
        $evidence['health'] = [ordered]@{
            primaryModel      = [string]$liveHealth.primaryModel
            primaryModelReady = [bool]$liveHealth.primaryModelReady
            runtimeMode       = [string]$liveHealth.runtimeMode
            ollamaReachable   = [bool]$liveHealth.ollamaReachable
            status            = [string]$liveHealth.status
        }
    } catch {
        Add-Check 'PG-01b-health' 'FAIL' ("Health probe failed (cannot prove host/model): {0}" -f $_.Exception.Message)
    }

    try {
        $liveCap = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/capabilities') -TimeoutSec 45
        if ($liveCap.packageIdentity) { $livePkg = $liveCap.packageIdentity }
    } catch {
        Add-Check 'PG-01c-capabilities' 'WARN' ("Capabilities probe failed: {0}" -f $_.Exception.Message)
    }

    try {
        $liveTruth = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45
        $evidence['runtimeTruth'] = [ordered]@{
            activeModel         = [string]$liveTruth.activeModel
            activePackageVersion = [string]$liveTruth.activePackageVersion
            hostProfile         = [string]$liveTruth.hostProfile
            fallbackModeActive  = [bool]$liveTruth.fallbackModeActive
            fallbackReason      = [string]$liveTruth.fallbackReason
            runtimeMode         = [string]$liveTruth.runtimeMode
            deploymentSource    = [string]$liveTruth.deploymentSource
        }
    } catch {
        $msg = $_.Exception.Message
        if ($msg -match '404') {
            Add-Check 'PG-01d-runtime-truth' 'FAIL' 'Runtime truth 404 — package cutover required before promotion (cannot assume healthy)'
        } else {
            Add-Check 'PG-01d-runtime-truth' 'FAIL' ("Runtime truth probe failed: {0}" -f $msg)
        }
    }
}

# --- PG-01 correct host (profile / reported) ---
if (-not $SkipLiveProbes) {
    if ($liveTruth -and $liveTruth.hostProfile) {
        if ([string]$liveTruth.hostProfile -eq $ExpectedHostProfile -or [string]$liveTruth.hostReported -match [regex]::Escape($ExpectedHost)) {
            Add-Check 'PG-01-correct-host' 'PASS' ("hostProfile={0} hostReported={1}" -f $liveTruth.hostProfile, $liveTruth.hostReported) '/api/runtime-truth'
        } else {
            Add-Check 'PG-01-correct-host' 'FAIL' ("Unexpected hostProfile={0} (expected {1})" -f $liveTruth.hostProfile, $ExpectedHostProfile) '/api/runtime-truth'
        }
    } elseif ($hostOk -and $liveHealth) {
        Add-Check 'PG-01-correct-host' 'WARN' 'Health reachable on expected host URL but runtime-truth absent — host class not fully proven'
    } elseif (-not ($checks | Where-Object { $_.id -eq 'PG-01-correct-host-url' -and $_.status -eq 'FAIL' })) {
        if (-not $liveHealth) {
            Add-Check 'PG-01-correct-host' 'FAIL' 'No live health/truth evidence for correct host'
        }
    }
}

# --- PG-02 correct package ---
$pkgAssert = Join-Path $PSScriptRoot 'Assert-PackageHostConsistency.ps1'
if ($SkipLiveProbes) {
    Add-Check 'PG-02-correct-package' 'FAIL' 'Cannot prove correct package without live package-host consistency (SkipLiveProbes)'
} elseif (Test-Path $pkgAssert) {
    & $pkgAssert -BaseUrl $BaseUrl -PublishDir $PublishDir -ExpectedHost $ExpectedHost -ExpectedProductModel $ExpectedProductModel
    $pkgExit = $LASTEXITCODE
    $pkgReceipt = Join-Path $repoRoot 'docs\runtime-proof\package-host-consistency-latest.json'
    $evidence['packageHostConsistencyExit'] = $pkgExit
    if ($pkgExit -eq 0) {
        Add-Check 'PG-02-correct-package' 'PASS' 'PACKAGE_HOST_CONSISTENCY_OK' $pkgReceipt
    } else {
        Add-Check 'PG-02-correct-package' 'FAIL' 'PACKAGE_HOST_CONSISTENCY_BLOCKED — stale/mismatched/ambiguous package (do not assume)' $pkgReceipt
    }
} else {
    Add-Check 'PG-02-correct-package' 'FAIL' 'Assert-PackageHostConsistency.ps1 missing'
}

# --- PG-03 correct model ---
if ($SkipLiveProbes) {
    Add-Check 'PG-03-correct-model' 'FAIL' 'Cannot prove correct model without live probes'
} else {
    $active = $null
    if ($liveTruth -and $liveTruth.activeModel) { $active = [string]$liveTruth.activeModel }
    elseif ($liveHealth -and $liveHealth.primaryModel) { $active = [string]$liveHealth.primaryModel }
    $ready = $false
    if ($liveTruth -and $null -ne $liveTruth.activeModelReady) { $ready = [bool]$liveTruth.activeModelReady }
    elseif ($liveHealth) { $ready = [bool]$liveHealth.primaryModelReady }

    if ([string]::IsNullOrWhiteSpace($active)) {
        Add-Check 'PG-03-correct-model' 'FAIL' 'No live active/primary model evidence'
    } elseif ($active -ne $ExpectedProductModel) {
        Add-Check 'PG-03-correct-model' 'FAIL' ("activeModel={0} expected={1}" -f $active, $ExpectedProductModel) '/api/runtime-truth|/api/health'
    } elseif (-not $ready) {
        Add-Check 'PG-03-correct-model' 'FAIL' ("Model tag present but not ready: {0}" -f $active)
    } else {
        Add-Check 'PG-03-correct-model' 'PASS' ("activeModel={0} ready=true" -f $active) '/api/health'
    }
}

# Local PrimaryModel config must still match expected (product promotion safety)
$appsettings = Join-Path $repoRoot 'Auricrux.Web\appsettings.json'
if (Test-Path $appsettings) {
    $asTxt = Get-Content $appsettings -Raw
    if ($asTxt -match ('"PrimaryModel"\s*:\s*"' + [regex]::Escape($ExpectedProductModel) + '"')) {
        Add-Check 'PG-03b-config-primary' 'PASS' ("appsettings PrimaryModel={0}" -f $ExpectedProductModel) $appsettings
    } else {
        Add-Check 'PG-03b-config-primary' 'FAIL' ("appsettings PrimaryModel is not {0}" -f $ExpectedProductModel) $appsettings
    }
} else {
    Add-Check 'PG-03b-config-primary' 'FAIL' 'appsettings.json missing'
}

# --- PG-04 suite score met (live authority only) ---
$ledgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
$suiteOk = $false
$suiteDetail = 'no live authority evidence'
$suiteReportPath = $null
$suiteRate = $null

if (Test-Path $ledgerPath) {
    try {
        $ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
        $auth = $ledger.currentLiveAuthority
        if ($auth) {
            $suiteRate = [double]$auth.passRatePercent
            $suiteReportPath = [string]$auth.report
            $status = [string]$auth.status
            $absReport = if ($suiteReportPath -match '^[A-Za-z]:\\' -or $suiteReportPath.StartsWith('/')) {
                $suiteReportPath
            } else {
                Join-Path $repoRoot ($suiteReportPath -replace '/', '\')
            }
            $reportExists = Test-Path $absReport
            $evidence['liveAuthority'] = [ordered]@{
                status           = $status
                passRatePercent  = $suiteRate
                report           = $suiteReportPath
                reportExists     = $reportExists
            }
            if ($status -eq 'PASS' -and $suiteRate -ge $PassThresholdPercent -and $reportExists) {
                # Verify report itself is live generative PASS
                $rep = Get-Content $absReport -Raw | ConvertFrom-Json
                $base = [string]$rep.baseUrl
                $mode = [string]$rep.mode
                $isOffline = ($mode -match 'offline') -or ($rep.limitation -match 'support-only')
                $hostMatch = $base -match [regex]::Escape($ExpectedHost)
                if ($isOffline) {
                    $suiteDetail = 'Ledger authority cites offline/support report — not promotion-green'
                } elseif (-not $hostMatch) {
                    $suiteDetail = ("Report baseUrl not product host: {0}" -f $base)
                } elseif ([bool]$rep.suitePassed -ne $true -or [double]$rep.passRatePercent -lt $PassThresholdPercent) {
                    $suiteDetail = ("Report not PASS at threshold: rate={0} passed={1}" -f $rep.passRatePercent, $rep.suitePassed)
                } else {
                    $suiteOk = $true
                    $suiteDetail = ("Live authority PASS rate={0} report={1}" -f $suiteRate, $suiteReportPath)
                }
            } else {
                $suiteDetail = ("Live authority status={0} rate={1} threshold={2} reportExists={3}" -f $status, $suiteRate, $PassThresholdPercent, $reportExists)
            }
        } else {
            $suiteDetail = 'Ledger missing currentLiveAuthority'
        }
    } catch {
        $suiteDetail = ("Ledger parse failed: {0}" -f $_.Exception.Message)
    }
} else {
    $suiteDetail = 'Evidence ledger missing'
}

if ($suiteOk) {
    Add-Check 'PG-04-suite-score' 'PASS' $suiteDetail $(if ($suiteReportPath) { $suiteReportPath } else { $ledgerPath })
} else {
    Add-Check 'PG-04-suite-score' 'FAIL' ("Required suite score not met: {0}" -f $suiteDetail) $ledgerPath
}

# --- PG-05 no unsafe fallback ---
if ($SkipLiveProbes) {
    Add-Check 'PG-05-no-unsafe-fallback' 'FAIL' 'Cannot prove fallback-free without live probes'
} else {
    $fallbackActive = $false
    $fallbackReason = 'unknown'
    if ($liveTruth) {
        $fallbackActive = [bool]$liveTruth.fallbackModeActive
        $fallbackReason = [string]$liveTruth.fallbackReason
    } elseif ($liveHealth) {
        $mode = [string]$liveHealth.runtimeMode
        $fallbackActive = ($mode -match 'corpus-fallback|ollama-degraded')
        $fallbackReason = $mode
        $pm = [string]$liveHealth.primaryModel
        if ($pm -match 'dev-fallback' -or $pm -match '^llama3\.2') { $fallbackActive = $true; $fallbackReason = "primaryModel=$pm" }
    } else {
        Add-Check 'PG-05-no-unsafe-fallback' 'FAIL' 'No live evidence for fallback state'
    }

    if ($checks | Where-Object { $_.id -eq 'PG-05-no-unsafe-fallback' }) {
        # already failed for no evidence
    } elseif ($fallbackActive) {
        Add-Check 'PG-05-no-unsafe-fallback' 'FAIL' ("Unsafe fallback active: {0}" -f $fallbackReason) '/api/runtime-truth'
    } else {
        Add-Check 'PG-05-no-unsafe-fallback' 'PASS' ("fallbackModeActive=false reason={0}" -f $fallbackReason) '/api/runtime-truth'
    }
}

# --- PG-06 no clobber event ---
$clobberAssert = Join-Path $PSScriptRoot 'Assert-ProductModelClobberProtection.ps1'
$clobberReceipt = Join-Path $repoRoot 'docs\runtime-proof\product-model-clobber-protection-latest.json'
if (Test-Path $clobberAssert) {
    & $clobberAssert | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'PG-06-no-clobber' 'FAIL' 'PRODUCT_MODEL_CLOBBER_PROTECTION blocked — refuse promotion' $clobberReceipt
    } else {
        Add-Check 'PG-06-no-clobber-policy' 'PASS' 'PRODUCT_MODEL_CLOBBER_PROTECTION_OK' $clobberReceipt
    }
} else {
    Add-Check 'PG-06-no-clobber-policy' 'FAIL' 'Assert-ProductModelClobberProtection.ps1 missing'
}

# Unauthorized clobber evidence markers
$cutoverEvDir = Join-Path $repoRoot 'docs\runtime-proof\product-model-cutover-evidence'
$unauthorizedHit = $false
if (Test-Path $cutoverEvDir) {
    $bad = Get-ChildItem $cutoverEvDir -File -ErrorAction SilentlyContinue | Where-Object {
        $t = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        $t -and ($t -match 'UNAUTHORIZED_CLOBBER|CLOBBER_WITHOUT_AUTH|product.?tag.?clobbered') -and ($t -notmatch 'PRODUCT_MODEL_CUTOVER_AUTHORIZED')
    }
    if ($bad) {
        $unauthorizedHit = $true
        Add-Check 'PG-06-no-clobber-event' 'FAIL' ("Unauthorized clobber evidence present: {0}" -f (($bad | Select-Object -First 3).Name -join ', ')) $cutoverEvDir
    }
}
if (-not $unauthorizedHit -and -not ($checks | Where-Object { $_.id -eq 'PG-06-no-clobber-policy' -and $_.status -eq 'FAIL' })) {
    if (-not ($checks | Where-Object { $_.id -eq 'PG-06-no-clobber' -and $_.status -eq 'FAIL' })) {
        Add-Check 'PG-06-no-clobber-event' 'PASS' 'No unauthorized clobber evidence markers found'
    }
}

# --- PG-07 rollback exists ---
$proc = Join-Path $repoRoot 'docs\runtime-proof\gguf-suite-live-cutover-procedure-2026-08-03.md'
$baseline = Join-Path $repoRoot 'docs\runtime-proof\gguf-grounding-precutover-baseline-2026-08-03.json'
$cutoverWf = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
$rbOk = $true
$rbDetail = @()
if (-not (Test-Path $proc)) { $rbOk = $false; $rbDetail += 'procedure missing' }
elseif ((Get-Content $proc -Raw) -notmatch 'auricrux-web-prev') { $rbOk = $false; $rbDetail += 'procedure missing prev-container rollback' }
if (-not (Test-Path $baseline)) { $rbOk = $false; $rbDetail += 'precutover baseline missing' }
if (-not (Test-Path $cutoverWf)) { $rbOk = $false; $rbDetail += 'cutover workflow missing' }
elseif ((Get-Content $cutoverWf -Raw) -notmatch 'prev-\$\(date' -and (Get-Content $cutoverWf -Raw) -notmatch 'rename.*"\$\{NAME\}-prev-') {
    $rbOk = $false; $rbDetail += 'cutover workflow missing prev rename'
}
if ($rbOk) {
    Add-Check 'PG-07-rollback-exists' 'PASS' 'Procedure + baseline + cutover prev-rename present' $proc
} else {
    Add-Check 'PG-07-rollback-exists' 'FAIL' ($rbDetail -join '; ')
}

# --- PG-08 manifest truthful ---
$manifestPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
$erAssert = Join-Path $PSScriptRoot 'Assert-AuricruxEvidenceRules.ps1'
if (Test-Path $erAssert) {
    & $erAssert | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'PG-08-evidence-rules' 'FAIL' 'EVIDENCE_RULES_BLOCKED — manifest/evidence policy not green' (Join-Path $repoRoot 'docs\runtime-proof\auricrux-evidence-rules-latest.json')
    } else {
        Add-Check 'PG-08-evidence-rules' 'PASS' 'EVIDENCE_RULES_OK' (Join-Path $repoRoot 'docs\runtime-proof\auricrux-evidence-rules-latest.json')
    }
} else {
    Add-Check 'PG-08-evidence-rules' 'FAIL' 'Assert-AuricruxEvidenceRules.ps1 missing'
}

if (-not (Test-Path $manifestPath)) {
    Add-Check 'PG-08-manifest-truthful' 'FAIL' 'model_manifest.json missing'
} else {
    try {
        $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $eval = [string]$m.adapter.evalStatus
        $claimedPass = [bool]$m.adapter.ggufGenerativeSuitePassed
        $claimedRate = $null
        if ($null -ne $m.adapter.ggufGenerativePassRatePercent) { $claimedRate = [double]$m.adapter.ggufGenerativePassRatePercent }
        $cited = [string]$m.adapter.ggufGenerativeReport
        $deployReq = $false
        if ($null -ne $m.adapter.productHostDeployRequired) { $deployReq = [bool]$m.adapter.productHostDeployRequired }

        $truthIssues = @()
        if ($eval -match 'PASS' -and ($eval -match 'FAIL' -or -not $claimedPass)) {
            $truthIssues += 'evalStatus ambiguous PASS/FAIL'
        }
        if ($claimedPass -eq $true) {
            if ([string]::IsNullOrWhiteSpace($cited)) {
                $truthIssues += 'PASS claimed without ggufGenerativeReport'
            } else {
                $citedAbs = Join-Path $repoRoot ($cited -replace '/', '\')
                if (-not (Test-Path $citedAbs)) {
                    # also try docs/runtime-proof sibling of eval/reports
                    $alt = Join-Path $repoRoot ('docs\runtime-proof\' + (Split-Path $cited -Leaf))
                    if (Test-Path $alt) { $citedAbs = $alt }
                }
                if (-not (Test-Path $citedAbs)) {
                    $truthIssues += ("Cited report missing: {0}" -f $cited)
                } else {
                    $cr = Get-Content $citedAbs -Raw | ConvertFrom-Json
                    if ([bool]$cr.suitePassed -ne $true) {
                        $truthIssues += 'Manifest claims PASS but cited report suitePassed!=true'
                    }
                    if ($null -ne $claimedRate -and [Math]::Abs([double]$cr.passRatePercent - $claimedRate) -gt 0.1) {
                        $truthIssues += ("Manifest rate {0} != report rate {1}" -f $claimedRate, $cr.passRatePercent)
                    }
                    if ([string]$cr.baseUrl -notmatch [regex]::Escape($ExpectedHost)) {
                        $truthIssues += 'Cited PASS report is not product-host live'
                    }
                }
            }
        }
        # Failures must not be dressed as promotion-green
        if ($eval -match 'FAIL' -or $claimedPass -eq $false) {
            if ($suiteOk) {
                $truthIssues += 'Manifest still FAIL while ledger claims PASS — reconcile before promote'
            }
        }
        if ($deployReq -eq $true) {
            $truthIssues += 'productHostDeployRequired=true (host package not proven current)'
        }

        $evidence['manifest'] = [ordered]@{
            evalStatus                  = $eval
            ggufGenerativeSuitePassed   = $claimedPass
            ggufGenerativePassRatePercent = $claimedRate
            ggufGenerativeReport        = $cited
            productHostDeployRequired   = $deployReq
        }

        if ($truthIssues.Count -gt 0) {
            Add-Check 'PG-08-manifest-truthful' 'FAIL' ($truthIssues -join '; ') $manifestPath
        } else {
            Add-Check 'PG-08-manifest-truthful' 'PASS' ("evalStatus={0}; suitePassed={1}; rate={2}" -f $eval, $claimedPass, $claimedRate) $manifestPath
        }
    } catch {
        Add-Check 'PG-08-manifest-truthful' 'FAIL' ("Manifest parse failed: {0}" -f $_.Exception.Message) $manifestPath
    }
}

# --- PG-09 evidence ledger updated ---
$ledgerWriter = Join-Path $repoRoot 'scripts\Write-GgufSuiteEvidenceLedger.ps1'
$ledgerJsonl = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.jsonl'
if (-not (Test-Path $ledgerWriter)) {
    Add-Check 'PG-09-ledger-writer' 'FAIL' 'Write-GgufSuiteEvidenceLedger.ps1 missing'
} else {
    $lw = Get-Content $ledgerWriter -Raw
    if ($lw -match 'append' -or $lw -match 'Append' -or $lw -match 'entries') {
        Add-Check 'PG-09-ledger-writer' 'PASS' 'Ledger writer present' $ledgerWriter
    } else {
        Add-Check 'PG-09-ledger-writer' 'FAIL' 'Ledger writer does not appear append-oriented'
    }
}

if (-not (Test-Path $ledgerPath)) {
    Add-Check 'PG-09-ledger-updated' 'FAIL' 'auricrux_evidence_ledger_v1.json missing'
} elseif (-not (Test-Path $ledgerJsonl)) {
    Add-Check 'PG-09-ledger-updated' 'FAIL' 'auricrux_evidence_ledger_v1.jsonl missing'
} else {
    try {
        $ledger2 = Get-Content $ledgerPath -Raw | ConvertFrom-Json
        $entryCount = 0
        if ($null -ne $ledger2.entryCount) { $entryCount = [int]$ledger2.entryCount }
        elseif ($ledger2.entries) { $entryCount = @($ledger2.entries).Count }
        $updated = [string]$ledger2.updatedAtUtc
        $authReport = if ($ledger2.currentLiveAuthority) { [string]$ledger2.currentLiveAuthority.report } else { '' }
        $authStatus = if ($ledger2.currentLiveAuthority) { [string]$ledger2.currentLiveAuthority.status } else { '' }

        $issues = @()
        if ($entryCount -lt 1) { $issues += 'entryCount < 1' }
        if ([string]::IsNullOrWhiteSpace($updated)) { $issues += 'updatedAtUtc empty' }
        if ([string]::IsNullOrWhiteSpace($authReport)) { $issues += 'currentLiveAuthority.report empty' }
        else {
            $abs = Join-Path $repoRoot ($authReport -replace '/', '\')
            if (-not (Test-Path $abs)) { $issues += ("authority report missing on disk: {0}" -f $authReport) }
        }
        # Promotion requires ledger to reflect a PASS authority when suite gate passed;
        # if suite failed, ledger must still be present and consistent (already checked).
        if ($suiteOk -and $authStatus -ne 'PASS') {
            $issues += 'Suite PASS claimed but ledger authority status != PASS'
        }

        if ($issues.Count -gt 0) {
            Add-Check 'PG-09-ledger-updated' 'FAIL' ($issues -join '; ') $ledgerPath
        } else {
            Add-Check 'PG-09-ledger-updated' 'PASS' ("entries={0}; updatedAtUtc={1}; authority={2}" -f $entryCount, $updated, $authStatus) $ledgerPath
        }
    } catch {
        Add-Check 'PG-09-ledger-updated' 'FAIL' ("Ledger parse failed: {0}" -f $_.Exception.Message) $ledgerPath
    }
}

# --- Verdict ---
$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count

$token = if ($fail -eq 0) { 'PROMOTION_EVIDENCE_OK' } else { 'PROMOTION_EVIDENCE_BLOCKED' }
if ($AllowEvidenceIncomplete -and $fail -gt 0) {
    Write-Host 'AllowEvidenceIncomplete set — recording BLOCKED reasons but exiting 0 (emergency only).' -ForegroundColor Yellow
    $token = 'PROMOTION_EVIDENCE_BLOCKED_BYPASS'
}

$receipt = [ordered]@{
    atUtc                 = (Get-Date).ToUniversalTime().ToString('o')
    token                 = $token
    purpose               = 'evidence-based-promotion-gate'
    assumptionBased       = $false
    passCount             = $pass
    failCount             = $fail
    warnCount             = $warn
    baseUrl               = $BaseUrl
    expectedHost          = $ExpectedHost
    expectedProductModel  = $ExpectedProductModel
    passThresholdPercent  = $PassThresholdPercent
    skipLiveProbes        = [bool]$SkipLiveProbes
    allowEvidenceIncomplete = [bool]$AllowEvidenceIncomplete
    evidenceSnapshots     = $evidence
    checks                = $checks
    requiredProof         = @(
        'correct_host', 'correct_package', 'correct_model', 'suite_score_met',
        'no_unsafe_fallback', 'no_clobber_event', 'rollback_exists',
        'manifest_truthful', 'evidence_ledger_updated'
    )
}

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receiptPath = Join-Path $receiptDir 'promotion-evidence-gate-latest.json'
($receipt | ConvertTo-Json -Depth 10) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3})" -f $token, $pass, $fail, $warn) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)

if ($AllowEvidenceIncomplete) {
    Write-Host 'PROMOTION_EVIDENCE_BLOCKED_BYPASS'
    exit 0
}
if ($fail -gt 0) {
    Write-Host 'PROMOTION_EVIDENCE_BLOCKED'
    exit 1
}
Write-Host 'PROMOTION_EVIDENCE_OK'
exit 0
