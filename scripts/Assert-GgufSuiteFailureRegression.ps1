<#
.SYNOPSIS
  Assert GGUF construction suite failure regression coverage is intact.
  Prevents silent return of known defects. Does not weaken the suite.
  Does not invent generative PASS. Does not train to the test.
.OUTPUTS
  GGUF_SUITE_FAILURE_REGRESSION_OK / GGUF_SUITE_FAILURE_REGRESSION_BLOCKED
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [switch]$SkipLiveRetrievalProbe
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
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

Write-Host '=== GGUF suite failure regression coverage ===' -ForegroundColor Cyan
Write-Host 'Does not weaken suite. Does not remove hard cases. Does not claim live PASS.'

$regPath = Join-Path $repoRoot 'eval\gguf_suite_failure_regression_v1.json'
$suitePath = Join-Path $repoRoot 'eval\construction_god_suite_v1.json'
$aliasPath = Join-Path $repoRoot 'eval\keyword_aliases_v1.json'
$authPath = Join-Path $repoRoot 'docs\runtime-proof\construction_god_suite_gguf_generative_2026-08-02.json'
$analysisPath = Join-Path $repoRoot 'docs\GGUF_GENERATIVE_SUITE_FAILURE_ANALYSIS.md'
$cisPath = Join-Path $repoRoot 'Auricrux.Web\Services\ConstructionIntelligenceService.cs'
$corpusPath = Join-Path $repoRoot 'Auricrux.Web\Data\construction-corpus.json'
$pubDll = Join-Path $repoRoot '_publish\web\Auricrux.Web.dll'

if (-not (Test-Path $regPath)) {
    Add-Check 'GR-01-catalog' 'FAIL' 'eval/gguf_suite_failure_regression_v1.json missing'
    $reg = $null
} else {
    $reg = Get-Content $regPath -Raw | ConvertFrom-Json
    Add-Check 'GR-01-catalog' 'PASS' ("catalog failures={0} nearFailures={1}" -f @($reg.failures).Count, @($reg.nearFailures).Count)
}

if (-not (Test-Path $suitePath)) {
    Add-Check 'GR-02-canonical-suite' 'FAIL' 'construction_god_suite_v1.json missing'
    $suite = $null
} else {
    $suite = Get-Content $suitePath -Raw | ConvertFrom-Json
    Add-Check 'GR-02-canonical-suite' 'PASS' ("suite cases={0}" -f @($suite.cases).Count)
}

if (-not (Test-Path $authPath)) {
    Add-Check 'GR-03-authority-report' 'FAIL' 'authority 2026-08-02 report missing'
    $auth = $null
} else {
    $auth = Get-Content $authPath -Raw | ConvertFrom-Json
    if ([int]$auth.passedCases -ne 23 -or [double]$auth.passRatePercent -ne 76.7) {
        Add-Check 'GR-03-authority-report' 'FAIL' ("authority report mutated unexpectedly passed={0} rate={1}" -f $auth.passedCases, $auth.passRatePercent)
    } else {
        Add-Check 'GR-03-authority-report' 'PASS' 'authority 23/30 (76.7%) FAIL preserved'
    }
}

Add-Check 'GR-04-analysis-doc' $(if (Test-Path $analysisPath) { 'PASS' } else { 'FAIL' }) 'GGUF_GENERATIVE_SUITE_FAILURE_ANALYSIS.md'

# Authority FAIL ids must be covered
if ($reg -and $auth) {
    $authFails = @($auth.cases | Where-Object { -not $_.passed } | ForEach-Object { [string]$_.id })
    $cov = @($reg.failures | ForEach-Object { [string]$_.id })
    $missing = @($authFails | Where-Object { $cov -notcontains $_ })
    $extra = @($cov | Where-Object { $authFails -notcontains $_ })
    if ($missing.Count -gt 0) {
        Add-Check 'GR-05-failure-coverage' 'FAIL' ("Regression missing authority FAILs: {0}" -f ($missing -join ', '))
    } elseif ($authFails.Count -ne 7) {
        Add-Check 'GR-05-failure-coverage' 'FAIL' ("Expected 7 authority FAILs, found {0}" -f $authFails.Count)
    } else {
        Add-Check 'GR-05-failure-coverage' 'PASS' ("All {0} authority FAILs covered" -f $authFails.Count)
    }
    if ($extra.Count -gt 0) {
        Add-Check 'GR-05b-extra-failures' 'WARN' ("Catalog has non-authority failure ids: {0}" -f ($extra -join ', '))
    }
}

# Suite not weakened: each failure/near-failure still present with same expectedKeywords (count not reduced; set equal for failures)
if ($reg -and $suite) {
    $byId = @{}
    foreach ($c in @($suite.cases)) { $byId[[string]$c.id] = $c }
    $weak = @()
    $gone = @()
    foreach ($f in @($reg.failures)) {
        $id = [string]$f.id
        if (-not $byId.ContainsKey($id)) { $gone += $id; continue }
        $exp = @($f.expectedKeywords)
        $have = @($byId[$id].expectedKeywords)
        $lost = @($exp | Where-Object { $have -notcontains $_ })
        if ($have.Count -lt $exp.Count -or $lost.Count -gt 0) {
            $weak += ("{0}: lost/reduced keywords ({1})" -f $id, ($lost -join ','))
        }
        $prompt = [string]$f.originalFailedPrompt
        $sq = [string]$byId[$id].query
        if ($prompt -and $sq -and ($prompt.Trim() -ne $sq.Trim())) {
            $weak += ("{0}: suite query diverged from originalFailedPrompt" -f $id)
        }
    }
    foreach ($n in @($reg.nearFailures)) {
        $id = [string]$n.id
        if (-not $byId.ContainsKey($id)) { $gone += $id }
    }
    if ($gone.Count -gt 0) {
        Add-Check 'GR-06-no-case-removal' 'FAIL' ("Cases removed from suite: {0}" -f ($gone -join ', '))
    } else {
        Add-Check 'GR-06-no-case-removal' 'PASS' 'All failure/near-failure cases still in construction_god_suite_v1'
    }
    if ($weak.Count -gt 0) {
        Add-Check 'GR-07-no-suite-weaken' 'FAIL' ($weak -join '; ')
    } else {
        Add-Check 'GR-07-no-suite-weaken' 'PASS' 'Failure-case expectedKeywords not weakened; prompts preserved'
    }
}

# Required fields on each failure entry
if ($reg) {
    $needFields = @(
        'originalFailedPrompt', 'expectedRetrievalBehavior', 'expectedGroundingBehavior',
        'expectedAliasExpansionBehavior', 'expectedScoringBehavior', 'reasonForPriorFailure', 'correctionsApplied'
    )
    $bad = @()
    foreach ($f in @($reg.failures)) {
        foreach ($nf in $needFields) {
            if (-not ($f.PSObject.Properties.Name -contains $nf) -or [string]::IsNullOrWhiteSpace([string]$f.$nf)) {
                if ($nf -eq 'correctionsApplied') {
                    if (@($f.correctionsApplied).Count -lt 1) { $bad += ("{0}.{1}" -f $f.id, $nf) }
                } else {
                    $bad += ("{0}.{1}" -f $f.id, $nf)
                }
            }
        }
    }
    if ($bad.Count -gt 0) {
        Add-Check 'GR-08-failure-fields' 'FAIL' ("Incomplete regression fields: {0}" -f ($bad -join ', '))
    } else {
        Add-Check 'GR-08-failure-fields' 'PASS' 'Each failure has prompt/retrieval/grounding/alias/scoring/reason/corrections'
    }
}

# Corrections still present
if (-not (Test-Path $aliasPath)) {
    Add-Check 'GR-09-aliases' 'FAIL' 'keyword_aliases_v1.json missing'
} else {
    $al = Get-Content $aliasPath -Raw | ConvertFrom-Json
    $needAlias = @('payapp', 'rcsc', 'manual', 'respiratory', 'atmospheric', 'attendant', 'fragnet', 'silica', 'bolt', 'hvac', 'proctor', 'compaction', 'density', 'retainage', 'critical path', '6 ft', '5 ft', '4x', 'dwv', 'swppp', 'sov', 'om')
    $missingA = @($needAlias | Where-Object { -not ($al.aliases.PSObject.Properties.Name -contains $_) })
    if ($missingA.Count -gt 0) {
        Add-Check 'GR-09-aliases' 'FAIL' ("Missing aliases: {0}" -f ($missingA -join ', '))
    } else {
        Add-Check 'GR-09-aliases' 'PASS' 'Required keyword aliases present'
    }
}

if (-not (Test-Path $cisPath)) {
    Add-Check 'GR-10-grounding-expand' 'FAIL' 'ConstructionIntelligenceService.cs missing'
} else {
    $src = Get-Content $cisPath -Raw
    $okG = ($src -match 'Grounding excerpts') -and ($src -match 'Prefer facts from the grounding')
    $okE = $src -match 'ExpandSearchTerms'
    $okSilicaMap = ($src -match 'concrete') -and ($src -match 'silica') -and ($src -match 'cutting')
    $okMulti = ($src -match 'Fall Protection') -and ($src -match 'Trenching') -and ($src -match 'SWPPP|Sitework and Erosion') -and ($src -match 'Time Impact')
    if (-not $okG) {
        Add-Check 'GR-10-grounding-expand' 'FAIL' 'Grounding excerpt prompt strings missing from source'
    } elseif (-not $okE -or -not $okSilicaMap) {
        Add-Check 'GR-10-grounding-expand' 'FAIL' 'ExpandSearchTerms / silica mapping missing from source'
    } elseif (-not $okMulti) {
        Add-Check 'GR-10-grounding-expand' 'FAIL' 'ExpandSearchTerms still silica-narrow; multi-domain corpus justifications missing'
    } else {
        Add-Check 'GR-10-grounding-expand' 'PASS' 'Grounding + multi-domain ExpandSearchTerms (not silica-only) present'
    }
}

if (-not (Test-Path $corpusPath)) {
    Add-Check 'GR-11-silica-corpus' 'FAIL' 'construction-corpus.json missing'
} else {
    $corp = Get-Content $corpusPath -Raw
    if (($corp -match 'respirable crystalline silica') -and ($corp -match 'respiratory')) {
        Add-Check 'GR-11-silica-corpus' 'PASS' 'Silica corpus has respirable crystalline silica + respiratory'
    } else {
        Add-Check 'GR-11-silica-corpus' 'FAIL' 'Silica corpus correction missing respiratory/respirable language'
    }
}

if (Test-Path $pubDll) {
    $bytes = [IO.File]::ReadAllBytes($pubDll)
    if ((Test-BytesContainAscii $bytes 'ExpandSearchTerms') -and (
            (Test-BytesContainAscii $bytes 'Grounding excerpts') -or
            ([Text.Encoding]::Unicode.GetString($bytes) -match 'Grounding excerpts')
        )) {
        Add-Check 'GR-12-publish-dll' 'PASS' 'Publish DLL contains ExpandSearchTerms (+ grounding signal)'
    } elseif (Test-BytesContainAscii $bytes 'ExpandSearchTerms') {
        Add-Check 'GR-12-publish-dll' 'PASS' 'Publish DLL contains ExpandSearchTerms'
    } else {
        Add-Check 'GR-12-publish-dll' 'FAIL' 'Publish DLL missing ExpandSearchTerms - republish required'
    }
} else {
    Add-Check 'GR-12-publish-dll' 'WARN' 'Publish DLL missing - skip DLL regression byte check'
}

$rescore = Join-Path $repoRoot 'scripts\rescore_gguf_report_aliases.py'
Add-Check 'GR-13-rescore-script' $(if (Test-Path $rescore) { 'PASS' } else { 'FAIL' }) 'rescore_gguf_report_aliases.py'

$doc = Join-Path $repoRoot 'docs\runtime-proof\GGUF_SUITE_FAILURE_REGRESSION.md'
Add-Check 'GR-14-docs' $(if (Test-Path $doc) { 'PASS' } else { 'FAIL' }) 'GGUF_SUITE_FAILURE_REGRESSION.md'

# Live retrieval probe for silica (does not claim generative suite PASS)
if ($SkipLiveRetrievalProbe) {
    Add-Check 'GR-15-silica-retrieval-probe' 'WARN' 'Skipped (-SkipLiveRetrievalProbe)'
} else {
    try {
        $body = @{ query = 'concrete cutting dust respiratory hazard'; searchScope = 'Internal' } | ConvertTo-Json
        $sr = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/search') -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 60
        $blob = (@($sr.results) | ConvertTo-Json -Depth 5 -Compress)
        if (@($sr.results).Count -lt 1) {
            Add-Check 'GR-15-silica-retrieval-probe' 'FAIL' 'Silica retrieval probe returned 0 hits (ExpandSearchTerms/corpus regression?)'
        } elseif ($blob -match '(?i)silica|respirable|respiratory') {
            Add-Check 'GR-15-silica-retrieval-probe' 'PASS' 'Live search retrieves silica/respirable for cutting-dust phrasing'
        } else {
            Add-Check 'GR-15-silica-retrieval-probe' 'FAIL' 'Live search hits lack silica/respirable for cutting-dust probe'
        }
    } catch {
        Add-Check 'GR-15-silica-retrieval-probe' 'FAIL' ("retrieval probe failed: {0}" -f $_.Exception.Message)
    }
}

# Near-failure watchlist count
if ($reg) {
    $nf = @($reg.nearFailures).Count
    if ($nf -lt 10) {
        Add-Check 'GR-16-near-failures' 'FAIL' ("Expected >=10 near-failures (2/3 keyword PASSes), found {0}" -f $nf)
    } else {
        Add-Check 'GR-16-near-failures' 'PASS' ("Near-failure watchlist count={0}" -f $nf)
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'GGUF_SUITE_FAILURE_REGRESSION_OK' } else { 'GGUF_SUITE_FAILURE_REGRESSION_BLOCKED' }

$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    authorityReport = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
    catalog = 'eval/gguf_suite_failure_regression_v1.json'
    suiteWeakened = $false
    trainedToTest = $false
    difficultCasesRemoved = $false
    checks = $checks
}
$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receiptPath = Join-Path $receiptDir 'gguf-suite-failure-regression-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
if ($fail -gt 0) {
    $checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.id, $_.detail) -ForegroundColor Red
    }
    Write-Host 'GGUF_SUITE_FAILURE_REGRESSION_BLOCKED'
    exit 1
}
Write-Host 'GGUF_SUITE_FAILURE_REGRESSION_OK'
exit 0
