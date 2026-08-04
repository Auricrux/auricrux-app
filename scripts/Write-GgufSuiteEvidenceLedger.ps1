<#
.SYNOPSIS
  Append a GGUF suite evidence row to the Auricrux evidence ledger (chronological, never overwrite).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$PriorFailReportPath = '',
    [string]$SafetyGateReceiptPath = '',
    [string]$LedgerPath = '',
    [string]$DllPath = '',
    [string]$PublishDir = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($LedgerPath)) {
    $LedgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
}
if ([string]::IsNullOrWhiteSpace($SafetyGateReceiptPath)) {
    $SafetyGateReceiptPath = Join-Path $repoRoot 'docs\runtime-proof\gguf-deployment-safety-gate-latest.json'
}
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}
if ([string]::IsNullOrWhiteSpace($DllPath)) {
    $DllPath = Join-Path $PublishDir 'Auricrux.Web.dll'
}

if (-not (Test-Path $ReportPath)) { throw "Report missing: $ReportPath" }
$report = Get-Content $ReportPath -Raw | ConvertFrom-Json

# Evidence rule: offline alias rescore / non-live modes must never enter the authority ledger.
$mode = [string]$report.mode
$reportAuthority = [string]$report.authority
if ($mode -match 'offline|alias.rescore|excerpt-rescore' -or $reportAuthority -match 'support-only|offline') {
    Write-Host 'EVIDENCE_RULES_BLOCKED: refusing to append offline/alias-rescore report to live authority ledger.' -ForegroundColor Red
    Write-Host ("mode={0} authority={1} path={2}" -f $mode, $reportAuthority, $ReportPath)
    exit 2
}
if ($mode -notmatch 'gguf-generative-product-chat') {
    Write-Host 'EVIDENCE_RULES_BLOCKED: ledger accepts only live product-chat suite modes.' -ForegroundColor Red
    Write-Host ("mode={0}" -f $mode)
    exit 2
}

# Qualify / disqualify PASS for currentLiveAuthority (historical append still allowed).
function Test-SuiteReportFallbackContamination($ReportObj) {
    $hits = 0
    $cases = @()
    if ($ReportObj.results) { $cases = @($ReportObj.results) }
    elseif ($ReportObj.cases) { $cases = @($ReportObj.cases) }
    elseif ($ReportObj.caseResults) { $cases = @($ReportObj.caseResults) }
    foreach ($c in $cases) {
        $ex = ''
        if ($c.excerpt) { $ex = [string]$c.excerpt }
        elseif ($c.responseExcerpt) { $ex = [string]$c.responseExcerpt }
        elseif ($c.answerExcerpt) { $ex = [string]$c.answerExcerpt }
        elseif ($c.response) { $ex = [string]$c.response }
        if ($ex -match 'no live model reachable' -or $ex -match 'corpus-fallback' -or $ex -match 'corpus response \(grounded') {
            $hits++
        }
    }
    return $hits
}

$fallbackHits = Test-SuiteReportFallbackContamination $report
$hasPackageIdentity = ($null -ne $report.packageIdentity) -and (
    -not [string]::IsNullOrWhiteSpace([string]$report.packageIdentity.packageVersion) -or
    -not [string]::IsNullOrWhiteSpace([string]$report.packageIdentity.corpusSha256) -or
    [bool]$report.packageIdentity.stampFilePresent
)
$baseUrl = [string]$report.baseUrl
$hostOk = $baseUrl -match 'auricrux\.futurecontractorsofamerica\.com'
$wantsPass = [bool]$report.suitePassed -and ([double]$report.passRatePercent -ge [double]$report.passThresholdPercent)
$qualifiedPass = $wantsPass -and $hostOk -and ($fallbackHits -eq 0) -and $hasPackageIdentity
$authorityClass = 'live-dated-host-validation'
if ($wantsPass -and -not $qualifiedPass) {
    $authorityClass = 'live-dated-host-validation-disqualified'
    $reasons = @()
    if ($fallbackHits -gt 0) { $reasons += ("fallbackContaminationCases={0}" -f $fallbackHits) }
    if (-not $hasPackageIdentity) { $reasons += 'missingPackageIdentity' }
    if (-not $hostOk) { $reasons += 'baseUrlNotProductHost' }
    Write-Host ("AUTHORITY_DISQUALIFY PASS score={0}% reasons={1} (row still appended; currentLiveAuthority NOT promoted)" -f $report.passRatePercent, ($reasons -join ',')) -ForegroundColor Yellow
}

$suitePath = Join-Path $repoRoot 'eval\construction_god_suite_v1.json'
$suiteMeta = Get-Content $suitePath -Raw | ConvertFrom-Json

$dllHash = $null
$packageHash = $null
if (Test-Path $DllPath) {
    $dllHash = (Get-FileHash -Algorithm SHA256 -Path $DllPath).Hash.ToLowerInvariant()
}
if (Test-Path $PublishDir) {
    # Deterministic package fingerprint: sorted relative path + size + sha of DLL + corpus
    $corpus = Join-Path $PublishDir 'Data\construction-corpus.json'
    $parts = @()
    if ($dllHash) { $parts += "dll:$dllHash" }
    if (Test-Path $corpus) {
        $parts += ("corpus:" + (Get-FileHash -Algorithm SHA256 -Path $corpus).Hash.ToLowerInvariant())
    }
    $fileCount = @(Get-ChildItem $PublishDir -Recurse -File -ErrorAction SilentlyContinue).Count
    $parts += "files:$fileCount"
    $packageHash = (Get-FileHash -Algorithm SHA256 -InputStream ([IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes(($parts -join '|'))))).Hash.ToLowerInvariant()
}

# Per-domain (category) scores
$byDomain = @{}
foreach ($c in @($report.cases)) {
    $cat = [string]$c.category
    if ([string]::IsNullOrWhiteSpace($cat)) { $cat = 'unknown' }
    if (-not $byDomain.ContainsKey($cat)) {
        $byDomain[$cat] = [pscustomobject]@{ domain = $cat; passed = 0; failed = 0; total = 0; caseIds = @() }
    }
    $row = $byDomain[$cat]
    $row.total++
    if ($c.passed) { $row.passed++ } else { $row.failed++; $row.caseIds = @($row.caseIds + [string]$c.id) }
    $byDomain[$cat] = $row
}
$domainScores = @($byDomain.Values | ForEach-Object {
    $pct = if ($_.total -eq 0) { 0 } else { [math]::Round(100.0 * $_.passed / $_.total, 1) }
    [ordered]@{
        domain = $_.domain
        passed = $_.passed
        failed = $_.failed
        total = $_.total
        passRatePercent = $pct
        failedCaseIds = @($_.caseIds)
    }
} | Sort-Object { $_.domain })

$failedPrompts = @($report.cases | Where-Object { -not $_.passed } | ForEach-Object {
    [ordered]@{
        id = [string]$_.id
        category = [string]$_.category
        keywordsMatched = $_.keywordsMatched
        keywordsTotal = $_.keywordsTotal
        matched = @($_.matched)
        excerpt = [string]$_.excerpt
    }
})

# Recovered = failed in prior FAIL report, passed now
$recovered = @()
if (-not [string]::IsNullOrWhiteSpace($PriorFailReportPath) -and (Test-Path $PriorFailReportPath)) {
    $prior = Get-Content $PriorFailReportPath -Raw | ConvertFrom-Json
    $priorFailIds = @($prior.cases | Where-Object { -not $_.passed } | ForEach-Object { [string]$_.id })
    $nowPass = @{}
    foreach ($c in @($report.cases)) {
        if ($c.passed) { $nowPass[[string]$c.id] = $true }
    }
    foreach ($id in $priorFailIds) {
        if ($nowPass.ContainsKey($id)) {
            $recovered += $id
        }
    }
}

$gateToken = $null
$gateAt = $null
if (Test-Path $SafetyGateReceiptPath) {
    $gate = Get-Content $SafetyGateReceiptPath -Raw | ConvertFrom-Json
    $gateToken = [string]$gate.token
    $gateAt = [string]$gate.atUtc
}

$runAt = [string]$report.runAtUtc
if ([string]::IsNullOrWhiteSpace($runAt)) { $runAt = (Get-Date).ToUniversalTime().ToString('o') }
$runDt = $null
try { $runDt = [DateTime]::Parse($runAt).ToUniversalTime() } catch { $runDt = (Get-Date).ToUniversalTime() }

$entry = [ordered]@{
    evidenceId = ("gguf-suite-{0}-{1}" -f $runDt.ToString('yyyyMMddTHHmmssZ'), $(if ($report.suitePassed) { 'PASS' } else { 'FAIL' }))
    recordedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    dateUtc = $runDt.ToString('yyyy-MM-dd')
    host = [string]$report.baseUrl
    modelName = [string]$report.model
    suiteName = [string]$suiteMeta.suiteId
    suiteVersion = 'v1'
    suitePath = 'eval/construction_god_suite_v1.json'
    suiteTarget = if ($report.suiteTarget) { [string]$report.suiteTarget } else { [string]$suiteMeta.suiteId }
    reportPath = ($ReportPath.Replace($repoRoot + '\', '').Replace($repoRoot + '/', '') -replace '\\', '/')
    packageHashSha256 = $packageHash
    dllHashSha256 = $dllHash
    packageVersion = $null
    buildTimestampUtc = $null
    corpusSha256 = $null
    dllSha256Live = $null
    stampFilePresent = $null
    stampSource = $null
    hostReported = $null
    manifestPath = $null
    manifestModelId = $null
    manifestEvalStatus = $null
    manifestGgufGenerativeReport = $null
    evidenceLedgerPath = 'docs/runtime-proof/auricrux_evidence_ledger_v1.json'
    evidenceLedgerJsonlPath = 'docs/runtime-proof/auricrux_evidence_ledger_v1.jsonl'
    livePackageIdentity = $null
    totalPassed = [int]$report.passedCases
    totalCases = [int]$report.totalCases
    totalScorePercent = [double]$report.passRatePercent
    thresholdPercent = [double]$report.passThresholdPercent
    status = $(if ($report.suitePassed) { 'PASS' } else { 'FAIL' })
    perDomainScores = $domainScores
    failedPrompts = $failedPrompts
    recoveredPrompts = $recovered
    recoveredCount = $recovered.Count
    priorFailReport = if ($PriorFailReportPath) { ($PriorFailReportPath -replace '\\', '/') } else { $null }
    safetyGateToken = $gateToken
    safetyGateAtUtc = $gateAt
    runAtUtc = $runAt
    keywordAliasesEnabled = -not [string]::IsNullOrWhiteSpace([string]$report.keywordAliasPath)
    trainInterrupted = $false
    authority = $authorityClass
    authorityQualifiedPass = [bool]$qualifiedPass
    fallbackContaminationCases = [int]$fallbackHits
    packageIdentityPresentAtRun = [bool]$hasPackageIdentity
}

# Link package identity from suite report (captured from live /api/capabilities)
if ($null -ne $report.packageIdentity) {
    $pi = $report.packageIdentity
    $entry.livePackageIdentity = $pi
    $entry.packageVersion = [string]$pi.packageVersion
    $entry.buildTimestampUtc = [string]$pi.buildTimestampUtc
    $entry.corpusSha256 = [string]$pi.corpusSha256
    $entry.dllSha256Live = [string]$pi.dllSha256
    $entry.stampFilePresent = [bool]$pi.stampFilePresent
    $entry.stampSource = [string]$pi.stampSource
    $entry.hostReported = [string]$pi.hostReported
    $entry.manifestPath = [string]$pi.manifestPath
    $entry.manifestModelId = [string]$pi.manifestModelId
    $entry.manifestEvalStatus = [string]$pi.manifestEvalStatus
    $entry.manifestGgufGenerativeReport = [string]$pi.manifestGgufGenerativeReport
    if ($pi.evidenceLedgerPath) { $entry.evidenceLedgerPath = [string]$pi.evidenceLedgerPath }
    if ($pi.suiteTarget) { $entry.suiteTarget = [string]$pi.suiteTarget }
    if ($pi.suiteVersion) { $entry.suiteVersion = [string]$pi.suiteVersion }
} elseif ($report.manifestEvalStatus) {
    $entry.manifestEvalStatus = [string]$report.manifestEvalStatus
    $entry.hostReported = [string]$report.hostReported
}

# Prefer publish corpus hash when live absent
if ([string]::IsNullOrWhiteSpace([string]$entry.corpusSha256)) {
    $corpusPub = Join-Path $PublishDir 'Data\construction-corpus.json'
    if (Test-Path $corpusPub) {
        $entry.corpusSha256 = (Get-FileHash -Algorithm SHA256 -Path $corpusPub).Hash.ToLowerInvariant()
    }
}

# Load or init ledger; APPEND only
$ledger = $null
if (Test-Path $LedgerPath) {
    $ledger = Get-Content $LedgerPath -Raw | ConvertFrom-Json
} else {
    $ledger = [pscustomobject]@{
        schemaVersion = 1
        ledgerId = 'auricrux_evidence_ledger_v1'
        purpose = 'Chronological Auricrux eval/deploy evidence. Append only; never overwrite prior FAIL rows or reports.'
        entries = @()
    }
}

$entries = @($ledger.entries)

# Evidence rule: never mutate prior FAIL rows (score/status upgrade forbidden).
foreach ($existing in $entries) {
    if ([string]$existing.status -ne 'FAIL') { continue }
    if ([string]$existing.evidenceId -eq [string]$entry.evidenceId) {
        # Same id re-write attempt
        if ([double]$existing.totalScorePercent -ne [double]$entry.totalScorePercent -or [string]$entry.status -ne 'FAIL') {
            Write-Host 'EVIDENCE_RULES_BLOCKED: refusing to mutate prior FAIL ledger row.' -ForegroundColor Red
            exit 2
        }
    }
}

# De-dupe by evidenceId / reportPath+runAtUtc if re-run of same writer
$dup = $false
foreach ($e in $entries) {
    if ([string]$e.runAtUtc -eq [string]$entry.runAtUtc -and [string]$e.reportPath -eq [string]$entry.reportPath) {
        $dup = $true; break
    }
    # Never replace a FAIL row with a different status under the same evidenceId
    if ([string]$e.evidenceId -eq [string]$entry.evidenceId -and [string]$e.status -eq 'FAIL' -and [string]$entry.status -ne 'FAIL') {
        Write-Host 'EVIDENCE_RULES_BLOCKED: refusing FAIL->PASS overwrite of ledger evidenceId.' -ForegroundColor Red
        exit 2
    }
}
if (-not $dup) {
    $entries += [pscustomobject]$entry
} else {
    Write-Host 'LEDGER_SKIP duplicate runAtUtc+reportPath (append-only; no overwrite)' -ForegroundColor Yellow
}

$out = [ordered]@{
    schemaVersion = 1
    ledgerId = 'auricrux_evidence_ledger_v1'
    purpose = 'Chronological Auricrux eval/deploy evidence. Append only; never overwrite prior FAIL rows or reports. AUTHORITY-CORRECTION rows may demote later claims without deleting history. Authority elevates only from qualifying live evidence.'
    updatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    entryCount = $entries.Count
    authorityDerivation = $null
    supersededEvidence = $null
    currentLiveAuthority = $null
    entries = $entries
}

# Preserve authority index fields (never drop supersession marks on append).
if ($ledger -and $ledger.PSObject.Properties.Name -contains 'authorityDerivation') {
    $out.authorityDerivation = $ledger.authorityDerivation
} else {
    $out.authorityDerivation = [ordered]@{
        rule = 'currentLiveAuthority may become PASS only from qualifying live product-host evidence (mode=gguf-generative-product-chat, rate>=80, zero fallback contamination, packageIdentity present). Historical PASS rows may be retained without elevating authority.'
        offlineMayElevate = $false
        disqualifiedPassMayElevate = $false
        naiveLatestPassWins = $false
    }
}
if ($ledger -and $ledger.PSObject.Properties.Name -contains 'supersededEvidence' -and $null -ne $ledger.supersededEvidence) {
    $out.supersededEvidence = @($ledger.supersededEvidence)
} else {
    $out.supersededEvidence = @()
}
# Auto-index this row when PASS is disqualified (append-only metadata; prior rows untouched).
if (-not $dup -and $wantsPass -and -not $qualifiedPass) {
    $sup = [System.Collections.Generic.List[object]]::new()
    foreach ($s in @($out.supersededEvidence)) { if ($null -ne $s) { [void]$sup.Add($s) } }
    $already = $false
    foreach ($s in $sup) {
        if ([string]$s.evidenceId -eq [string]$entry.evidenceId) { $already = $true; break }
    }
    if (-not $already) {
        $reasons = @()
        if ($fallbackHits -gt 0) { $reasons += ("fallbackContaminationCases={0}" -f $fallbackHits) }
        if (-not $hasPackageIdentity) { $reasons += 'missingPackageIdentity' }
        if (-not $hostOk) { $reasons += 'baseUrlNotProductHost' }
        [void]$sup.Add([ordered]@{
            evidenceId = [string]$entry.evidenceId
            reportPath = [string]$entry.reportPath
            retainedHistorically = $true
            authorityQualified = $false
            reasons = $reasons
            supersededAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            supersededBy = 'ledger-writer-disqualify'
        })
    }
    $out.supersededEvidence = @($sup)
}

# Preserve prior currentLiveAuthority unless this append qualifies a change.
$priorAuth = $null
if ($ledger -and $ledger.PSObject.Properties.Name -contains 'currentLiveAuthority') {
    $priorAuth = $ledger.currentLiveAuthority
}

$relReport = ($ReportPath.Replace($repoRoot + '\', '').Replace($repoRoot + '/', '') -replace '\\', '/')
if (-not $dup) {
    if ($qualifiedPass) {
        $out.currentLiveAuthority = [ordered]@{
            status = 'PASS'
            report = $relReport
            passRatePercent = [double]$report.passRatePercent
            authorityClass = 'live-dated-host-validation-qualified'
            updatedByEvidenceId = [string]$entry.evidenceId
        }
    } elseif (-not $wantsPass -and $hostOk -and $fallbackHits -eq 0) {
        # Clean live FAIL may update authority pointer
        $out.currentLiveAuthority = [ordered]@{
            status = 'FAIL'
            report = $relReport
            passRatePercent = [double]$report.passRatePercent
            authorityClass = 'live-dated-host-validation'
            updatedByEvidenceId = [string]$entry.evidenceId
        }
    } elseif ($priorAuth) {
        $out.currentLiveAuthority = $priorAuth
        Write-Host 'currentLiveAuthority unchanged (new row disqualified or unclean for authority pointer)' -ForegroundColor Yellow
    } else {
        $out.currentLiveAuthority = [ordered]@{
            status = 'FAIL'
            report = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
            passRatePercent = 76.7
            authorityClass = 'live-dated-host-validation'
            note = 'fallback-default-until-qualified-run'
        }
    }
} else {
    $out.currentLiveAuthority = $priorAuth
}

New-Item -ItemType Directory -Force -Path (Split-Path $LedgerPath) | Out-Null
# Atomic write via temp
$tmp = "$LedgerPath.tmp"
($out | ConvertTo-Json -Depth 10) | Set-Content $tmp -Encoding UTF8
Move-Item -Force $tmp $LedgerPath

# Also append a one-line JSONL mirror for forensic append-only trail (only when newly appended)
$jsonl = Join-Path (Split-Path $LedgerPath) 'auricrux_evidence_ledger_v1.jsonl'
if (-not $dup) {
    ($entry | ConvertTo-Json -Depth 10 -Compress) | Add-Content $jsonl -Encoding UTF8
    Write-Host ("LEDGER_APPEND evidenceId={0} status={1} score={2}% recovered={3}" -f $entry.evidenceId, $entry.status, $entry.totalScorePercent, $entry.recoveredCount)
} else {
    Write-Host ("LEDGER_UNCHANGED evidenceId={0} (duplicate skipped)" -f $entry.evidenceId)
}
Write-Host ("Ledger: {0}" -f $LedgerPath)
exit 0
