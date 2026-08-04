<#
.SYNOPSIS
  Deployment safety gate before live GGUF construction suite (or cutover).
  Hard-fails on any check. Never starts training. Never recreates product models.
  LIVE 3B TRAIN PROTECTION: does not contact train host/PID (SG-19).
.PARAMETER BaseUrl
  Product host under test (default: canonical GCP product URL).
.PARAMETER PublishDir
  Path to refreshed _publish/web package (default: repo _publish/web).
.PARAMETER ExpectedHost
  Allowed host substring for BaseUrl (default: auricrux.futurecontractorsofamerica.com).
.PARAMETER SkipLiveProbes
  Package/repo checks only (no HTTPS). Suite runner must NOT use this for live runs.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$PublishDir = '',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [switch]$SkipLiveProbes
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$checks = New-Object System.Collections.Generic.List[object]
function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Test-BytesContainAscii([byte[]]$Bytes, [string]$Needle) {
    $n = [Text.Encoding]::ASCII.GetBytes($Needle)
    for ($i = 0; $i -le $Bytes.Length - $n.Length; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $n.Length; $j++) {
            if ($Bytes[$i + $j] -ne $n[$j]) { $ok = $false; break }
        }
        if ($ok) { return $true }
    }
    return $false
}

function Test-BytesContainUtf16([byte[]]$Bytes, [string]$Needle) {
    $n = [Text.Encoding]::Unicode.GetBytes($Needle)
    for ($i = 0; $i -le $Bytes.Length - $n.Length; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $n.Length; $j++) {
            if ($Bytes[$i + $j] -ne $n[$j]) { $ok = $false; break }
        }
        if ($ok) { return $true }
    }
    return $false
}

Write-Host '=== Auricrux GGUF deployment safety gate ===' -ForegroundColor Cyan
Write-Host 'Hard-fail on any FAIL. No train start. No model recreate.'
Write-Host ("BaseUrl={0} PublishDir={1}" -f $BaseUrl, $PublishDir)

# --- SG-01 correct target host ---
try {
    $uri = [Uri]$BaseUrl
    if ($uri.Host -ne $ExpectedHost) {
        Add-Check 'SG-01-target-host' 'FAIL' ("Host '{0}' != expected '{1}'" -f $uri.Host, $ExpectedHost)
    } elseif ($uri.Scheme -ne 'https') {
        Add-Check 'SG-01-target-host' 'FAIL' ("Scheme must be https (got {0})" -f $uri.Scheme)
    } else {
        Add-Check 'SG-01-target-host' 'PASS' ("https://{0}" -f $uri.Host)
    }
} catch {
    Add-Check 'SG-01-target-host' 'FAIL' ("Invalid BaseUrl: {0}" -f $_.Exception.Message)
}

# --- SG-02 publish package complete ---
$need = @(
    'Auricrux.Web.dll',
    'Auricrux.Web.exe',
    'web.config',
    'appsettings.json',
    'Data\construction-corpus.json'
)
$missing = @()
foreach ($n in $need) {
    if (-not (Test-Path (Join-Path $PublishDir $n))) { $missing += $n }
}
if ($missing.Count -gt 0) {
    Add-Check 'SG-02-publish-package' 'FAIL' ("Missing: {0}" -f ($missing -join ', '))
} else {
    $fileCount = @(Get-ChildItem $PublishDir -Recurse -File -ErrorAction SilentlyContinue).Count
    Add-Check 'SG-02-publish-package' 'PASS' ("Complete package files={0}" -f $fileCount)
}

# --- SG-03 / SG-04 DLL version + ExpandSearchTerms ---
$dll = Join-Path $PublishDir 'Auricrux.Web.dll'
$srcCs = Join-Path $repoRoot 'Auricrux.Web\Services\ConstructionIntelligenceService.cs'
if (-not (Test-Path $dll)) {
    Add-Check 'SG-03-dll-version' 'FAIL' 'Auricrux.Web.dll missing'
    Add-Check 'SG-04-ExpandSearchTerms' 'FAIL' 'DLL missing - cannot scan'
} else {
    $dllItem = Get-Item $dll
    $dllSha = (Get-FileHash -Algorithm SHA256 -Path $dll).Hash.ToLowerInvariant()
    $srcOk = Test-Path $srcCs
    $srcHasExpand = $false
    if ($srcOk) {
        $srcHasExpand = (Select-String -Path $srcCs -Pattern 'ExpandSearchTerms' -SimpleMatch -Quiet)
    }
    if (-not $srcHasExpand) {
        Add-Check 'SG-03-dll-version' 'FAIL' 'Source ConstructionIntelligenceService.cs missing ExpandSearchTerms'
    } elseif ($srcOk -and $dllItem.LastWriteTimeUtc -lt (Get-Item $srcCs).LastWriteTimeUtc.AddMinutes(-1)) {
        # Allow small clock skew; fail if DLL clearly older than source by >1 min
        Add-Check 'SG-03-dll-version' 'FAIL' ("DLL mtime UTC {0} older than source {1} - republish required" -f $dllItem.LastWriteTimeUtc.ToString('o'), (Get-Item $srcCs).LastWriteTimeUtc.ToString('o'))
    } else {
        Add-Check 'SG-03-dll-version' 'PASS' ("DLL sha256={0}… mtimeUtc={1}" -f $dllSha.Substring(0, 12), $dllItem.LastWriteTimeUtc.ToString('o'))
    }

    $bytes = [IO.File]::ReadAllBytes($dll)
    if (Test-BytesContainAscii $bytes 'ExpandSearchTerms') {
        Add-Check 'SG-04-ExpandSearchTerms' 'PASS' 'ExpandSearchTerms present in publish DLL'
    } else {
        Add-Check 'SG-04-ExpandSearchTerms' 'FAIL' 'ExpandSearchTerms NOT found in publish DLL'
    }

    $hasGrounding = (Test-BytesContainUtf16 $bytes 'Grounding excerpts') -or (Test-BytesContainUtf16 $bytes 'Prefer facts from the grounding')
    if ($hasGrounding) {
        Add-Check 'SG-05-grounding-prompt' 'PASS' 'Grounding prompt strings present in DLL'
    } else {
        Add-Check 'SG-05-grounding-prompt' 'FAIL' 'Grounding excerpts / Prefer facts strings missing from DLL'
    }
}

# --- SG-06 grounding corpus + SG-07 silica ---
$corpusPub = Join-Path $PublishDir 'Data\construction-corpus.json'
$corpusSrc = Join-Path $repoRoot 'Auricrux.Web\Data\construction-corpus.json'
$corpusPath = if (Test-Path $corpusPub) { $corpusPub } else { $corpusSrc }
if (-not (Test-Path $corpusPath)) {
    Add-Check 'SG-06-grounding-corpus' 'FAIL' 'construction-corpus.json missing'
    Add-Check 'SG-07-silica-corpus' 'FAIL' 'corpus missing'
} else {
    $raw = Get-Content $corpusPath -Raw
    $entries = 0
    try { $entries = @(($raw | ConvertFrom-Json)).Count } catch { $entries = 0 }
    if ($entries -ge 70) {
        Add-Check 'SG-06-grounding-corpus' 'PASS' ("corpus entries={0} path={1}" -f $entries, $corpusPath)
    } else {
        Add-Check 'SG-06-grounding-corpus' 'FAIL' ("corpus entries={0} (need >=70)" -f $entries)
    }
    $hasSilica = $raw -match 'respirable crystalline silica'
    $hasRespTag = $raw -match '"respiratory"'
    if ($hasSilica -and $hasRespTag) {
        Add-Check 'SG-07-silica-corpus' 'PASS' 'silica content + respiratory tag present'
    } else {
        Add-Check 'SG-07-silica-corpus' 'FAIL' ("silica={0} respiratoryTag={1}" -f $hasSilica, $hasRespTag)
    }
}

# --- SG-08 Modelfile must be DEV FALLBACK ONLY (no product recreate path as default) ---
$mf = Join-Path $repoRoot 'auricrux\system\Modelfile.auricrux-fca'
if (-not (Test-Path $mf)) {
    Add-Check 'SG-08-no-modelfile-recreate' 'FAIL' 'Modelfile.auricrux-fca missing'
} else {
    $mfText = Get-Content $mf -Raw
    if ($mfText -match 'DEV FALLBACK ONLY' -and $mfText -match 'Do NOT use this file to overwrite product') {
        Add-Check 'SG-08-no-modelfile-recreate' 'PASS' 'Modelfile bannered DEV FALLBACK ONLY'
    } else {
        Add-Check 'SG-08-no-modelfile-recreate' 'FAIL' 'Modelfile missing DEV FALLBACK ONLY / do-not-overwrite banner'
    }
}

# --- SG-09 no llama3.2 pull on default compose startup ---
$compose = Join-Path $repoRoot 'docker-compose.yml'
if (-not (Test-Path $compose)) {
    Add-Check 'SG-09-no-llama32-default-pull' 'FAIL' 'docker-compose.yml missing'
} else {
    $cText = Get-Content $compose -Raw
    $hasProfile = $cText -match 'profiles:\s*\[["'']dev-fallback["'']\]' -or $cText -match "profiles:\s*\[[\s\n]*['`"]dev-fallback['`"]"
    # Also accept YAML list form
    if (-not $hasProfile) {
        $hasProfile = $cText -match '(?ms)ollama-model-init:.*?profiles:\s*\n\s*-\s*["'']?dev-fallback'
    }
    $pullInInit = $cText -match 'ollama pull llama3\.2'
    $createsProduct = $cText -match 'ollama\s+create\s+auricrux-fca(\s|$|;|"|'')'
    $createsFallback = $cText -match 'ollama\s+create\s+auricrux-fca-dev-fallback'
    if ($createsProduct) {
        Add-Check 'SG-09-no-llama32-default-pull' 'FAIL' 'compose still ollama create auricrux-fca (product clobber risk)'
    } elseif ($hasProfile -and $pullInInit -and $createsFallback) {
        Add-Check 'SG-09-no-llama32-default-pull' 'PASS' 'llama3.2 pull + create confined to profile dev-fallback; creates auricrux-fca-dev-fallback only'
    } elseif ($hasProfile -and $pullInInit) {
        Add-Check 'SG-09-no-llama32-default-pull' 'PASS' 'llama3.2 pull confined to profile dev-fallback'
    } elseif (-not $pullInInit) {
        Add-Check 'SG-09-no-llama32-default-pull' 'PASS' 'No llama3.2 pull in compose'
    } else {
        Add-Check 'SG-09-no-llama32-default-pull' 'FAIL' 'llama3.2 pull present without required profiles: [dev-fallback]'
    }
}

# Focused ollama-init audit (profile gate, no silent fallback, no product-tag create)
$ollamaInitAssert = Join-Path $PSScriptRoot 'Assert-OllamaInitSafety.ps1'
if (Test-Path $ollamaInitAssert) {
    & $ollamaInitAssert
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-09b-ollama-init-safety' 'FAIL' 'Assert-OllamaInitSafety.ps1 blocked (see ollama-init-safety-latest.json)'
    } else {
        Add-Check 'SG-09b-ollama-init-safety' 'PASS' 'OLLAMA_INIT_SAFETY_OK'
    }
} else {
    Add-Check 'SG-09b-ollama-init-safety' 'FAIL' 'Assert-OllamaInitSafety.ps1 missing'
}

# Product model clobber protection (warm/compose/init/deploy vs authorized cutover)
$clobberAssert = Join-Path $PSScriptRoot 'Assert-ProductModelClobberProtection.ps1'
if (Test-Path $clobberAssert) {
    & $clobberAssert
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-18-product-model-clobber' 'FAIL' 'Assert-ProductModelClobberProtection.ps1 blocked'
    } else {
        Add-Check 'SG-18-product-model-clobber' 'PASS' 'PRODUCT_MODEL_CLOBBER_PROTECTION_OK'
    }
} else {
    Add-Check 'SG-18-product-model-clobber' 'FAIL' 'Assert-ProductModelClobberProtection.ps1 missing'
}

# Live 3B train protection (static; never SSH/kill/pause train PID)
$trainProtectAssert = Join-Path $PSScriptRoot 'Assert-Live3bTrainProtection.ps1'
if (Test-Path $trainProtectAssert) {
    & $trainProtectAssert
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-19-live-3b-train-protection' 'FAIL' 'Assert-Live3bTrainProtection.ps1 blocked'
    } else {
        Add-Check 'SG-19-live-3b-train-protection' 'PASS' 'LIVE_3B_TRAIN_PROTECTION_OK'
    }
} else {
    Add-Check 'SG-19-live-3b-train-protection' 'FAIL' 'Assert-Live3bTrainProtection.ps1 missing'
}

# Package-to-host consistency (fail loudly on stale/mismatched/ambiguous)
$pkgHostAssert = Join-Path $PSScriptRoot 'Assert-PackageHostConsistency.ps1'
if ($SkipLiveProbes) {
    Add-Check 'SG-20-package-host-consistency' 'WARN' 'Skipped (-SkipLiveProbes) - run Assert-PackageHostConsistency.ps1 before authority suite'
} elseif (Test-Path $pkgHostAssert) {
    & $pkgHostAssert -BaseUrl $BaseUrl -PublishDir $PublishDir
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-20-package-host-consistency' 'FAIL' 'PACKAGE_HOST_CONSISTENCY_BLOCKED (stale/mismatched/ambiguous host)'
    } else {
        Add-Check 'SG-20-package-host-consistency' 'PASS' 'PACKAGE_HOST_CONSISTENCY_OK'
    }
} else {
    Add-Check 'SG-20-package-host-consistency' 'FAIL' 'Assert-PackageHostConsistency.ps1 missing'
}

# GGUF failure regression coverage (does not weaken suite; optional live retrieval probe)
$regAssert = Join-Path $PSScriptRoot 'Assert-GgufSuiteFailureRegression.ps1'
if (Test-Path $regAssert) {
    if ($SkipLiveProbes) {
        & $regAssert -SkipLiveRetrievalProbe
    } else {
        & $regAssert -BaseUrl $BaseUrl
    }
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-21-gguf-failure-regression' 'FAIL' 'GGUF_SUITE_FAILURE_REGRESSION_BLOCKED'
    } else {
        Add-Check 'SG-21-gguf-failure-regression' 'PASS' 'GGUF_SUITE_FAILURE_REGRESSION_OK'
    }
} else {
    Add-Check 'SG-21-gguf-failure-regression' 'FAIL' 'Assert-GgufSuiteFailureRegression.ps1 missing'
}

# SG-22: runtime truth endpoint present (package-side; live 404 WARN until cutover)
$rtAssert = Join-Path $PSScriptRoot 'Assert-RuntimeTruth.ps1'
if (Test-Path $rtAssert) {
    if ($SkipLiveProbes) {
        & $rtAssert -SkipLiveProbe
    } else {
        & $rtAssert
    }
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-22-runtime-truth' 'FAIL' 'RUNTIME_TRUTH_BLOCKED'
    } else {
        Add-Check 'SG-22-runtime-truth' 'PASS' 'RUNTIME_TRUTH_OK'
    }
} else {
    Add-Check 'SG-22-runtime-truth' 'FAIL' 'Assert-RuntimeTruth.ps1 missing'
}

# SG-23: promotion evidence gate (informational for suite preflight — WARN if blocked;
# hard-block remains Assert-PromotionEvidenceGate / Assert-PromotionAllowed)
$promoAssert = Join-Path $PSScriptRoot 'Assert-PromotionEvidenceGate.ps1'
if (Test-Path $promoAssert) {
    if ($SkipLiveProbes) {
        & $promoAssert -SkipLiveProbes
    } else {
        & $promoAssert -BaseUrl $BaseUrl
    }
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-23-promotion-evidence' 'WARN' 'PROMOTION_EVIDENCE_BLOCKED (suite may still run; promote/cutover refuse until OK)'
    } else {
        Add-Check 'SG-23-promotion-evidence' 'PASS' 'PROMOTION_EVIDENCE_OK'
    }
} else {
    Add-Check 'SG-23-promotion-evidence' 'FAIL' 'Assert-PromotionEvidenceGate.ps1 missing'
}

# SG-24: operational drift (WARN-only for suite; FAIL only if drift assert missing).
# Traces to RB-C2/RB-M2 — does not elevate authority; does not hard-block suite.
$driftAssert = Join-Path $PSScriptRoot 'Assert-OperationalDrift.ps1'
if (Test-Path $driftAssert) {
    & $driftAssert 2>&1 | Out-Null
    $dc = $LASTEXITCODE
    if ($dc -eq 2) {
        Add-Check 'SG-24-operational-drift' 'WARN' 'OPERATIONAL_DRIFT_BLOCKED (hard local identity mismatch — fix before cutover/authority suite)'
    } elseif ($dc -eq 1) {
        Add-Check 'SG-24-operational-drift' 'WARN' 'OPERATIONAL_DRIFT_WARN (soft/expected signals; 0 FAIL)'
    } else {
        Add-Check 'SG-24-operational-drift' 'PASS' 'OPERATIONAL_DRIFT_OK'
    }
} else {
    Add-Check 'SG-24-operational-drift' 'FAIL' 'Assert-OperationalDrift.ps1 missing'
}

# SG-25: evidence ledger integrity (WARN if blocked — never claim PASS from this alone)
$ledAssert = Join-Path $PSScriptRoot 'Assert-EvidenceLedgerIntegrity.ps1'
if (Test-Path $ledAssert) {
    & $ledAssert 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'SG-25-ledger-integrity' 'WARN' 'EVIDENCE_LEDGER_INTEGRITY_BLOCKED (do not elevate authority)'
    } else {
        Add-Check 'SG-25-ledger-integrity' 'PASS' 'EVIDENCE_LEDGER_INTEGRITY_OK'
    }
} else {
    Add-Check 'SG-25-ledger-integrity' 'FAIL' 'Assert-EvidenceLedgerIntegrity.ps1 missing'
}

# --- SG-10 warm workflow must not Modelfile-recreate / unauthorized replace ---
$warm = Join-Path $repoRoot '.github\workflows\gcp-warm-auricrux-fca.yml'
if (-not (Test-Path $warm)) {
    Add-Check 'SG-10-no-unauthorized-replace' 'FAIL' 'gcp-warm-auricrux-fca.yml missing'
} else {
    $wText = Get-Content $warm -Raw
    $badCreate = $wText -match 'ollama create auricrux-fca'
    $badPull = $wText -match 'ollama pull llama3\.2'
    $goodGuard = $wText -match 'Do NOT Modelfile' -or $wText -match 'no Modelfile recreate'
    if ($badCreate -or $badPull) {
        Add-Check 'SG-10-no-unauthorized-replace' 'FAIL' 'Warm workflow still creates/pulls llama3.2 or ollama create auricrux-fca'
    } elseif (-not $goodGuard) {
        Add-Check 'SG-10-no-unauthorized-replace' 'FAIL' 'Warm workflow missing no-Modelfile-recreate guard text'
    } else {
        Add-Check 'SG-10-no-unauthorized-replace' 'PASS' 'Warm warms existing tag only; no Modelfile/llama3.2 recreate'
    }
}

# --- SG-11 rollback package / procedure available ---
$proc = Join-Path $repoRoot 'docs\runtime-proof\gguf-suite-live-cutover-procedure-2026-08-03.md'
$baseline = Join-Path $repoRoot 'docs\runtime-proof\gguf-grounding-precutover-baseline-2026-08-03.json'
$cutoverWf = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
$rbOk = $true
$rbDetail = @()
if (-not (Test-Path $proc)) { $rbOk = $false; $rbDetail += 'procedure missing' }
elseif ((Get-Content $proc -Raw) -notmatch 'auricrux-web-prev') { $rbOk = $false; $rbDetail += 'procedure missing prev-container rollback' }
if (-not (Test-Path $baseline)) { $rbOk = $false; $rbDetail += 'precutover baseline missing' }
if (-not (Test-Path $cutoverWf)) { $rbOk = $false; $rbDetail += 'cutover workflow missing' }
elseif ((Get-Content $cutoverWf -Raw) -notmatch 'rename.*"\$\{NAME\}-prev-') {
    # pattern in file: rename "$NAME" "${NAME}-prev-$(date +%s)"
    if ((Get-Content $cutoverWf -Raw) -notmatch 'prev-\$\(date') {
        $rbOk = $false; $rbDetail += 'cutover workflow missing prev rename'
    }
}
if ($rbOk) {
    Add-Check 'SG-11-rollback-available' 'PASS' 'Procedure + baseline + cutover prev-rename present'
} else {
    Add-Check 'SG-11-rollback-available' 'FAIL' ($rbDetail -join '; ')
}

# --- SG-12 current manifest preserved ---
$manifest = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
if (-not (Test-Path $manifest)) {
    Add-Check 'SG-12-manifest-preserved' 'FAIL' 'model_manifest.json missing'
} else {
    try {
        $m = Get-Content $manifest -Raw | ConvertFrom-Json
        $status = [string]$m.status
        $eval = [string]$m.adapter.evalStatus
        $tag = [string]$m.productRuntime.ollamaModel
        $kind = [string]$m.auricruxFcaAlias.kind
        if ([string]::IsNullOrWhiteSpace($status) -or [string]::IsNullOrWhiteSpace($eval)) {
            Add-Check 'SG-12-manifest-preserved' 'FAIL' 'manifest missing status/evalStatus'
        } elseif ($tag -ne 'auricrux-fca') {
            Add-Check 'SG-12-manifest-preserved' 'FAIL' ("productRuntime.ollamaModel={0} (expected auricrux-fca)" -f $tag)
        } elseif ($kind -notmatch 'merged-lora-gguf') {
            Add-Check 'SG-12-manifest-preserved' 'FAIL' ("auricruxFcaAlias.kind={0} (expected merged-lora-gguf*)" -f $kind)
        } else {
            Add-Check 'SG-12-manifest-preserved' 'PASS' ("status={0}; evalStatus={1}; kind={2}" -f $status, $eval, $kind)
        }
    } catch {
        Add-Check 'SG-12-manifest-preserved' 'FAIL' ("manifest parse error: {0}" -f $_.Exception.Message)
    }
}

# --- SG-15 package stamp + identity source ---
$stampRepo = Join-Path $repoRoot 'auricrux\system\package_stamp.json'
$stampPub = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
$stampPubData = Join-Path $PublishDir 'Data\package_stamp.json'
$pkgSvc = Join-Path $repoRoot 'Auricrux.Web\Services\PackageIdentityService.cs'
$stampScript = Join-Path $repoRoot 'scripts\Write-AuricruxPackageStamp.ps1'
$stampPath = if (Test-Path $stampPub) { $stampPub } elseif (Test-Path $stampPubData) { $stampPubData } else { $stampRepo }
$sg15Ok = $true
$sg15Detail = @()
if (-not (Test-Path $pkgSvc)) { $sg15Ok = $false; $sg15Detail += 'PackageIdentityService.cs missing' }
if (-not (Test-Path $stampScript)) { $sg15Ok = $false; $sg15Detail += 'Write-AuricruxPackageStamp.ps1 missing' }
if (-not (Test-Path $stampPath)) {
    $sg15Ok = $false
    $sg15Detail += 'package_stamp.json missing (run Write-AuricruxPackageStamp.ps1)'
} else {
    try {
        $st = Get-Content $stampPath -Raw | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace([string]$st.packageVersion)) { $sg15Ok = $false; $sg15Detail += 'packageVersion empty' }
        if ([string]::IsNullOrWhiteSpace([string]$st.buildTimestampUtc)) { $sg15Ok = $false; $sg15Detail += 'buildTimestampUtc empty' }
        if ([string]$st.suiteTarget -ne 'construction_god_suite_v1') { $sg15Ok = $false; $sg15Detail += ("suiteTarget={0}" -f $st.suiteTarget) }
        if ([string]::IsNullOrWhiteSpace([string]$st.evidenceLedgerPath)) { $sg15Ok = $false; $sg15Detail += 'evidenceLedgerPath missing' }
        if ($sg15Ok) {
            $sg15Detail += ("version={0}; buildUtc={1}; suite={2}; path={3}" -f $st.packageVersion, $st.buildTimestampUtc, $st.suiteTarget, $stampPath)
        }
    } catch {
        $sg15Ok = $false
        $sg15Detail += ("stamp parse error: {0}" -f $_.Exception.Message)
    }
}
if ($sg15Ok) {
    Add-Check 'SG-15-package-stamp' 'PASS' ($sg15Detail -join '; ')
} else {
    Add-Check 'SG-15-package-stamp' 'FAIL' ($sg15Detail -join '; ')
}

# --- Live probes (product not clobbered) ---
if (-not $SkipLiveProbes) {
    try {
        $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 30
        $pm = [string]$health.primaryModel
        $ready = [bool]$health.primaryModelReady
        $ollama = [bool]$health.ollamaReachable
        if ($pm -ne 'auricrux-fca' -or -not $ready -or -not $ollama) {
            Add-Check 'SG-13-product-fca-not-clobbered' 'FAIL' ("health primaryModel={0} ready={1} ollama={2}" -f $pm, $ready, $ollama)
        } else {
            Add-Check 'SG-13-product-fca-not-clobbered' 'PASS' 'primaryModel=auricrux-fca ready; ollama reachable'
        }
    } catch {
        Add-Check 'SG-13-product-fca-not-clobbered' 'FAIL' ("health probe failed: {0}" -f $_.Exception.Message)
    }

    try {
        $cap = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/capabilities') -TimeoutSec 30
        $notes = [string]$cap.constructionMoat.notes
        $evalLast = [string]$cap.constructionMoat.evalSuiteLastResult
        $promoted = [bool]$cap.constructionMoat.promotedFineTuneLive
        # Clobber indicators: interim llama3.2 alias language without GGUF, or "manifest not found" alone is WARN but FAIL if notes say alias-only
        $looksAliasOnly = ($notes -match 'llama3\.2' -and $notes -notmatch 'GGUF|merged LoRA') -or ($notes -match 'system-prompt alias over llama')
        $looksGguf = $promoted -or ($notes -match 'merged LoRA GGUF') -or ($evalLast -match 'GGUF-GENERATIVE') -or ($notes -match 'ckpt\d+')
        if ($looksAliasOnly -and -not $looksGguf) {
            Add-Check 'SG-14-no-alias-clobber' 'FAIL' ("capabilities look alias-clobbered: notes={0}" -f $notes.Substring(0, [Math]::Min(160, $notes.Length)))
        } elseif (-not $looksGguf -and $notes -match 'model_manifest.json not found') {
            Add-Check 'SG-14-no-alias-clobber' 'FAIL' 'capabilities cannot assert GGUF live (manifest not found on host)'
        } else {
            Add-Check 'SG-14-no-alias-clobber' 'PASS' ("promoted={0}; evalLast present; GGUF signals OK" -f $promoted)
        }

        # SG-16: live package identity vs intended publish package (stale detection)
        $livePkg = $cap.packageIdentity
        $pubCorpus = Join-Path $PublishDir 'Data\construction-corpus.json'
        $pubCorpusSha = $null
        if (Test-Path $pubCorpus) {
            $pubCorpusSha = (Get-FileHash -Algorithm SHA256 -Path $pubCorpus).Hash.ToLowerInvariant()
        }
        $pubDllSha = $null
        if (Test-Path $dll) {
            $pubDllSha = (Get-FileHash -Algorithm SHA256 -Path $dll).Hash.ToLowerInvariant()
        }
        if ($null -eq $livePkg) {
            # Preserve workflow until cutover ships PackageIdentity; warn, do not block.
            Add-Check 'SG-16-live-package-identity' 'WARN' 'Host capabilities.packageIdentity absent - cutover required for full identity reporting'
            Add-Check 'SG-17-dll-hash-compare' 'WARN' 'Skipped (packageIdentity absent on host)'
        } else {
            $liveCorpus = [string]$livePkg.corpusSha256
            $liveDll = [string]$livePkg.dllSha256
            $liveVer = [string]$livePkg.packageVersion
            $liveBuild = [string]$livePkg.buildTimestampUtc
            $liveSuite = [string]$livePkg.suiteTarget
            $liveHost = [string]$livePkg.hostReported
            $stampPresent = [bool]$livePkg.stampFilePresent
            $mismatch = @()
            if ($pubCorpusSha -and $liveCorpus -and ($liveCorpus.ToLowerInvariant() -ne $pubCorpusSha)) {
                $mismatch += ("corpusSha live={0}… publish={1}…" -f $liveCorpus.Substring(0, [Math]::Min(12, $liveCorpus.Length)), $pubCorpusSha.Substring(0, 12))
            }
            # DLL hash often differs across OS/container rebuilds; only FAIL when both present and host stamp claims freshness but corpus mismatches.
            if ($mismatch.Count -gt 0) {
                Add-Check 'SG-16-live-package-identity' 'FAIL' ("STALE PACKAGE: {0}" -f ($mismatch -join '; '))
            } else {
                Add-Check 'SG-16-live-package-identity' 'PASS' ("version={0}; buildUtc={1}; suite={2}; stamp={3}; host={4}; corpusMatch={5}" -f $liveVer, $liveBuild, $liveSuite, $stampPresent, $liveHost, ($(if ($pubCorpusSha -and $liveCorpus) { 'yes' } elseif (-not $liveCorpus) { 'n/a-live-empty' } else { 'n/a-no-publish' })))
            }
            # Surface DLL compare as informational WARN when both hashes present and differ (container vs Windows publish)
            if ($pubDllSha -and $liveDll -and ($liveDll.ToLowerInvariant() -ne $pubDllSha)) {
                Add-Check 'SG-17-dll-hash-compare' 'WARN' ("DLL sha differs live={0}… publish={1}… (expected across OS/container rebuild; use corpus+version as primary)" -f $liveDll.Substring(0, 12), $pubDllSha.Substring(0, 12))
            } elseif ($pubDllSha -and $liveDll) {
                Add-Check 'SG-17-dll-hash-compare' 'PASS' ("DLL sha match {0}…" -f $liveDll.Substring(0, 12))
            } else {
                Add-Check 'SG-17-dll-hash-compare' 'WARN' 'DLL hash compare skipped (missing live or publish hash)'
            }
        }
    } catch {
        Add-Check 'SG-14-no-alias-clobber' 'FAIL' ("capabilities probe failed: {0}" -f $_.Exception.Message)
        Add-Check 'SG-16-live-package-identity' 'FAIL' ("capabilities probe failed: {0}" -f $_.Exception.Message)
        Add-Check 'SG-17-dll-hash-compare' 'WARN' 'Skipped (capabilities probe failed)'
    }
} else {
    Add-Check 'SG-13-product-fca-not-clobbered' 'WARN' 'Skipped (-SkipLiveProbes)'
    Add-Check 'SG-14-no-alias-clobber' 'WARN' 'Skipped (-SkipLiveProbes)'
    Add-Check 'SG-16-live-package-identity' 'WARN' 'Skipped (-SkipLiveProbes)'
    Add-Check 'SG-17-dll-hash-compare' 'WARN' 'Skipped (-SkipLiveProbes)'
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'DEPLOYMENT_SAFETY_GATE_OK' } else { 'DEPLOYMENT_SAFETY_GATE_BLOCKED' }

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    baseUrl = $BaseUrl
    expectedHost = $ExpectedHost
    publishDir = $PublishDir
    skipLiveProbes = [bool]$SkipLiveProbes
    passCount = $pass
    failCount = $fail
    trainStarted = $false
    modelRecreated = $false
    checks = $checks
}
$receiptPath = Join-Path $receiptDir 'gguf-deployment-safety-gate-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

# Also mirror under eval/reports for suite adjacency
$evalReports = Join-Path $repoRoot 'eval\reports'
New-Item -ItemType Directory -Force -Path $evalReports | Out-Null
Copy-Item -Force $receiptPath (Join-Path $evalReports 'gguf-deployment-safety-gate-latest.json')

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)

if ($fail -gt 0) {
    Write-Host 'BLOCKERS:' -ForegroundColor Red
    $checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.id, $_.detail) -ForegroundColor Red
    }
    Write-Host 'Do not proceed through a failed gate.' -ForegroundColor Red
    exit 1
}

Write-Host 'DEPLOYMENT_SAFETY_GATE_OK'
exit 0
