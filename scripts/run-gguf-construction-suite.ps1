# Generative construction_god suite against live product auricrux-fca (GGUF path).
# Does not interrupt train PID. Writes dated report under eval/reports/.
# -ResumeFromReport: continuation — keep prior PASSes, re-run FAIL/missing only.
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$SuitePath = '',
    [double]$PassThresholdPercent = 80,
    [string]$ResumeFromReport = '',
    [string]$AliasPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $repoRoot 'eval'))) {
    throw "Could not locate eval/ under $repoRoot"
}
if ([string]::IsNullOrWhiteSpace($SuitePath)) {
    $SuitePath = Join-Path $repoRoot 'eval\construction_god_suite_v1.json'
}
if ([string]::IsNullOrWhiteSpace($AliasPath)) {
    $AliasPath = Join-Path $repoRoot 'eval\keyword_aliases_v1.json'
}
$reportsDir = Join-Path $repoRoot 'eval\reports'
New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
$suite = Get-Content $SuitePath -Raw | ConvertFrom-Json
$cases = @($suite.cases)

$aliasMap = @{}
if (Test-Path $AliasPath) {
    $aliasDoc = Get-Content $AliasPath -Raw | ConvertFrom-Json
    foreach ($p in $aliasDoc.aliases.PSObject.Properties) {
        $aliasMap[$p.Name.ToLowerInvariant()] = @($p.Value | ForEach-Object { [string]$_ })
    }
}

function Test-KeywordMatch {
    param([string]$Content, [string]$Keyword)
    $hay = $Content.ToLowerInvariant()
    $needle = $Keyword.ToLowerInvariant()
    if ($hay.Contains($needle)) { return $true }
    $alts = @()
    if ($aliasMap.ContainsKey($needle)) { $alts = $aliasMap[$needle] }
    foreach ($a in $alts) {
        if ($hay.Contains(([string]$a).ToLowerInvariant())) { return $true }
    }
    return $false
}

$priorById = @{}
$resumeMode = $false
if (-not [string]::IsNullOrWhiteSpace($ResumeFromReport)) {
    if (-not (Test-Path $ResumeFromReport)) { throw "Resume report missing: $ResumeFromReport" }
    $prior = Get-Content $ResumeFromReport -Raw | ConvertFrom-Json
    foreach ($pc in @($prior.cases)) {
        $priorById[[string]$pc.id] = $pc
    }
    $resumeMode = $true
    Write-Host "RESUME from $ResumeFromReport (re-run FAIL/missing only; keep prior PASS)" -ForegroundColor Cyan
}

$results = @()
$passed = 0
$reran = 0
$kept = 0

Write-Host "GGUF generative suite against $BaseUrl ($($cases.Count) cases)"

foreach ($c in $cases) {
    $id = [string]$c.id
    if ($resumeMode -and $priorById.ContainsKey($id) -and $priorById[$id].passed -eq $true) {
        $prev = $priorById[$id]
        $results += [pscustomobject]@{
            id = $id
            category = $c.category
            passed = $true
            keywordsTotal = $prev.keywordsTotal
            keywordsMatched = $prev.keywordsMatched
            matched = @($prev.matched)
            excerpt = [string]$prev.excerpt
            resumedFromPriorPass = $true
        }
        $passed++
        $kept++
        Write-Host "[KEEP-PASS] $id"
        continue
    }

    $bodyObj = [ordered]@{
        query = [string]$c.query
        thinkingMode = 1
        searchScope = 2
        sessionId = [guid]::NewGuid().ToString()
        conversationHistory = @()
    }
    $body = $bodyObj | ConvertTo-Json -Compress
    $content = ''
    $okHttp = $false
    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/chat?model=auricrux-fca" -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -TimeoutSec 180
        $content = [string]$resp.content
        if ([string]::IsNullOrWhiteSpace($content)) { $content = [string]$resp.Content }
        $okHttp = -not [string]::IsNullOrWhiteSpace($content)
    } catch {
        $err = $_.Exception.Message
        try {
            $rs = $_.Exception.Response.GetResponseStream()
            if ($rs) { $err = (New-Object IO.StreamReader($rs)).ReadToEnd() }
        } catch {}
        $content = "HTTP_ERROR: $err"
    }

    $keywords = @($c.expectedKeywords)
    $matched = @()
    foreach ($k in $keywords) {
        if (Test-KeywordMatch -Content $content -Keyword ([string]$k)) {
            $matched += $k
        }
    }
    $casePass = $okHttp -and ($matched.Count -ge [Math]::Max(1, [Math]::Ceiling($keywords.Count * 0.5)))
    if ($casePass) { $passed++ }
    $reran++
    $results += [pscustomobject]@{
        id = $id
        category = $c.category
        passed = $casePass
        keywordsTotal = $keywords.Count
        keywordsMatched = $matched.Count
        matched = $matched
        excerpt = if ($content.Length -gt 280) { $content.Substring(0, 280) } else { $content }
        resumedFromPriorPass = $false
    }
    $mark = if ($casePass) { 'PASS' } else { 'FAIL' }
    Write-Host "[$mark] $id $($matched.Count)/$($keywords.Count)"
}

$rate = if ($cases.Count -eq 0) { 0 } else { [math]::Round(100.0 * $passed / $cases.Count, 1) }
$suitePassed = $rate -ge $PassThresholdPercent
$report = [ordered]@{
    suiteId = $suite.suiteId
    mode = if ($resumeMode) { 'gguf-generative-product-chat-resume' } else { 'gguf-generative-product-chat' }
    baseUrl = $BaseUrl
    model = 'auricrux-fca'
    runAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    resumeFromReport = if ($resumeMode) { $ResumeFromReport } else { $null }
    keptPriorPasses = $kept
    reranCases = $reran
    keywordAliasPath = if (Test-Path $AliasPath) { $AliasPath } else { $null }
    totalCases = $cases.Count
    passedCases = $passed
    passRatePercent = $rate
    passThresholdPercent = $PassThresholdPercent
    suitePassed = $suitePassed
    cases = $results
}
$suffix = if ($resumeMode) { 'resume' } else { 'generative' }
$jsonPath = Join-Path $reportsDir "construction_god_suite_gguf_${suffix}_$stamp.json"
$mdPath = Join-Path $reportsDir "construction_god_suite_gguf_${suffix}_$stamp.md"
$report | ConvertTo-Json -Depth 6 | Set-Content $jsonPath -Encoding UTF8
@"
# Construction god suite - GGUF ($stamp)

- Base: $BaseUrl
- Model: auricrux-fca
- Mode: $(if ($resumeMode) { 'RESUME (continuation)' } else { 'full' })
- Kept prior PASS: $kept / Re-ran: $reran
- Keyword aliases: $(if (Test-Path $AliasPath) { 'enabled' } else { 'none' })
- Result: $passed/$($cases.Count) ($rate%) - suite $(if ($suitePassed) { 'PASS' } else { 'FAIL' }) at >= $PassThresholdPercent%
- JSON: $jsonPath
"@ | Set-Content $mdPath -Encoding UTF8

Write-Host "Wrote $jsonPath"
Write-Host "Suite $(if ($suitePassed) { 'PASS' } else { 'FAIL' }) ($rate%) kept=$kept reran=$reran"
if (-not $suitePassed) { exit 1 }
exit 0
