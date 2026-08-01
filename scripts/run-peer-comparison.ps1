<#
.SYNOPSIS
  AUX-027 peer comparison collector + provisional auto-scorer.
.EXAMPLE
  .\scripts\run-peer-comparison.ps1
  .\scripts\run-peer-comparison.ps1 -SampleSize 12 -AuricruxOnly -SkipScore
#>
[CmdletBinding()]
param(
    [int]$SampleSize = 12,
    [string]$AuricruxBaseUrl = "",
    [switch]$AuricruxOnly,
    [switch]$SkipScore
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$suitePath = Join-Path $root "eval\construction_god_suite_v1.json"
$reportsDir = Join-Path $root "eval\reports"
$runPath = Join-Path $reportsDir "peer_comparison_v1_run.json"
$reportJson = Join-Path $reportsDir "peer_comparison_v1_report.json"
$reportMd = Join-Path $reportsDir "peer_comparison_v1_report.md"
$envFile = Join-Path $root "eval\.peer-keys.env"

function Import-PeerEnv([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $parts = $_.Split('=', 2)
        $k = $parts[0].Trim()
        $v = $parts[1].Trim().Trim('"').Trim("'")
        if ($k -and $v) {
            Set-Item -Path ("Env:" + $k) -Value $v
        }
    }
}

function Get-EnvVal([string]$Name) {
    $v = [Environment]::GetEnvironmentVariable($Name, "Process")
    if (-not $v) { $v = [Environment]::GetEnvironmentVariable($Name, "User") }
    if (-not $v) {
        $item = Get-Item -Path ("Env:" + $Name) -ErrorAction SilentlyContinue
        if ($item) { $v = $item.Value }
    }
    return $v
}

function Select-StratifiedCases($cases, [int]$n) {
    $byCat = @($cases | Group-Object category)
    $picked = New-Object System.Collections.Generic.List[object]
    $iters = 0
    $limit = [Math]::Min($n, @($cases).Count)
    while ($picked.Count -lt $limit -and $iters -lt 200) {
        foreach ($g in $byCat) {
            if ($picked.Count -ge $limit) { break }
            $idx = [int][Math]::Floor($iters / [Math]::Max(1, $byCat.Count))
            if ($idx -lt @($g.Group).Count) {
                $c = @($g.Group)[$idx]
                $exists = $false
                foreach ($p in $picked) { if ($p.id -eq $c.id) { $exists = $true; break } }
                if (-not $exists) { [void]$picked.Add($c) }
            }
        }
        $iters++
    }
    return @($picked.ToArray())
}

function Invoke-JsonPost([string]$Url, $Headers, $Body, [int]$TimeoutSec = 120) {
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    $resp = Invoke-WebRequest -Uri $Url -Method Post -Headers $Headers -Body ([System.Text.Encoding]::UTF8.GetBytes($json)) `
        -ContentType "application/json; charset=utf-8" -TimeoutSec $TimeoutSec -UseBasicParsing
    return ($resp.Content | ConvertFrom-Json)
}

function Get-AuricruxAnswer([string]$Base, [string]$Query) {
    # Enums as ints: ThinkingMode.Auto=1, SearchScope.Both=2 (string enums 400 on prod)
    $body = @{ Query = $Query; ThinkingMode = 1; SearchScope = 2 }
    $r = Invoke-JsonPost -Url ($Base + "/api/chat") -Headers @{} -Body $body -TimeoutSec 180
    if ($r.content) { return [string]$r.content }
    if ($r.Content) { return [string]$r.Content }
    return ($r | ConvertTo-Json -Compress)
}

function Get-OpenAIAnswer([string]$Key, [string]$Model, [string]$Query) {
    $body = @{
        model = $Model
        temperature = 0.2
        messages = @(
            @{ role = "system"; content = "You are a helpful construction industry assistant. Be specific, practical, and safety-aware. Do not invent code section numbers you are unsure of." }
            @{ role = "user"; content = $Query }
        )
    }
    $r = Invoke-JsonPost -Url "https://api.openai.com/v1/chat/completions" `
        -Headers @{ Authorization = ("Bearer " + $Key) } -Body $body
    return [string]$r.choices[0].message.content
}

function Get-ClaudeAnswer([string]$Key, [string]$Model, [string]$Query) {
    $body = @{
        model = $Model
        max_tokens = 1200
        system = "You are a helpful construction industry assistant. Be specific, practical, and safety-aware. Do not invent code section numbers you are unsure of."
        messages = @(
            @{ role = "user"; content = $Query }
        )
    }
    $r = Invoke-JsonPost -Url "https://api.anthropic.com/v1/messages" `
        -Headers @{
            "x-api-key" = $Key
            "anthropic-version" = "2023-06-01"
        } -Body $body
    $parts = @($r.content | ForEach-Object { $_.text })
    return (($parts -join "`n").Trim())
}

function Get-GeminiAnswer([string]$Key, [string]$Model, [string]$Query) {
    $url = "https://generativelanguage.googleapis.com/v1beta/models/" + $Model + ":generateContent?key=" + $Key
    $body = @{
        system_instruction = @{
            parts = @(@{ text = "You are a helpful construction industry assistant. Be specific, practical, and safety-aware. Do not invent code section numbers you are unsure of." })
        }
        contents = @(
            @{ role = "user"; parts = @(@{ text = $Query }) }
        )
        generationConfig = @{ temperature = 0.2 }
    }
    $r = Invoke-JsonPost -Url $url -Headers @{} -Body $body
    return [string]$r.candidates[0].content.parts[0].text
}

function Invoke-BlindJudge([string]$JudgeProvider, [string]$Key, [string]$Model, [string]$Query, [hashtable]$Answers) {
    $labels = @("A", "B", "C", "D")
    $keys = @($Answers.Keys | Sort-Object)
    $map = @{}
    $block = ""
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $lab = $labels[$i]
        $map[$lab] = $keys[$i]
        $ans = $Answers[$keys[$i]]
        if (-not $ans) { $ans = "(no answer)" }
        $block += "`n### Answer " + $lab + "`n" + $ans + "`n"
    }

    $prompt = @(
        "You are a blind construction-SME rater. Score each answer 0-5 on:",
        "domainAccuracy, actionability, safetyComplianceDiligence, fieldPracticality, concision.",
        "0=wrong/useless, 3=broadly ok, 5=specific/field-ready. Prefer real thresholds. Do not invent model identity.",
        "Question: " + $Query,
        $block,
        "Return ONLY JSON object keyed by A/B/C/D with those five ints plus total (sum)."
    ) -join "`n"

    $raw = switch ($JudgeProvider) {
        "openai" { Get-OpenAIAnswer -Key $Key -Model $Model -Query $prompt }
        "anthropic" { Get-ClaudeAnswer -Key $Key -Model $Model -Query $prompt }
        "gemini" { Get-GeminiAnswer -Key $Key -Model $Model -Query $prompt }
        default { throw "No judge provider" }
    }

    $jsonText = $raw
    if ($raw -match '(?s)\{.*\}') { $jsonText = $Matches[0] }
    $scored = $jsonText | ConvertFrom-Json
    $out = @{}
    foreach ($lab in @($scored.PSObject.Properties.Name)) {
        $modelKey = $map[$lab]
        if (-not $modelKey) { continue }
        $s = $scored.$lab
        $out[$modelKey] = @{
            domainAccuracy = [int]$s.domainAccuracy
            actionability = [int]$s.actionability
            safetyComplianceDiligence = [int]$s.safetyComplianceDiligence
            fieldPracticality = [int]$s.fieldPracticality
            concision = [int]$s.concision
            total = [int]$s.total
            scoringMethod = ("automated-blind-judge:" + $JudgeProvider)
        }
    }
    return $out
}

Import-PeerEnv $envFile

if (-not $AuricruxBaseUrl) { $AuricruxBaseUrl = Get-EnvVal "AURICRUX_PEER_BASE_URL" }
if (-not $AuricruxBaseUrl) { $AuricruxBaseUrl = "https://auricrux.futurecontractorsofamerica.com" }
$AuricruxBaseUrl = $AuricruxBaseUrl.TrimEnd('/')

$openaiKey = Get-EnvVal "OPENAI_API_KEY"
$anthropicKey = Get-EnvVal "ANTHROPIC_API_KEY"
$googleKey = Get-EnvVal "GOOGLE_API_KEY"
if (-not $googleKey) { $googleKey = Get-EnvVal "GEMINI_API_KEY" }

$openaiModel = Get-EnvVal "OPENAI_PEER_MODEL"
if (-not $openaiModel) { $openaiModel = "gpt-4o" }
$anthropicModel = Get-EnvVal "ANTHROPIC_PEER_MODEL"
if (-not $anthropicModel) { $anthropicModel = "claude-sonnet-4-20250514" }
$geminiModel = Get-EnvVal "GEMINI_PEER_MODEL"
if (-not $geminiModel) { $geminiModel = "gemini-2.0-flash" }

$envSample = Get-EnvVal "PEER_SAMPLE_SIZE"
if ($envSample -and [int]$envSample -gt 0) { $SampleSize = [int]$envSample }

New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null
$suite = Get-Content $suitePath -Raw | ConvertFrom-Json
$cases = Select-StratifiedCases $suite.cases $SampleSize

Write-Host "AUX-027 peer comparison" -ForegroundColor Cyan
Write-Host ("Auricrux: " + $AuricruxBaseUrl)
Write-Host ("Cases: {0} stratified from {1}" -f @($cases).Count, @($suite.cases).Count)
Write-Host ("OpenAI keyed: {0}" -f [bool]$openaiKey)
Write-Host ("Anthropic keyed: {0}" -f [bool]$anthropicKey)
Write-Host ("Gemini keyed: {0}" -f [bool]$googleKey)

$run = [ordered]@{
    suiteId = "peer_comparison_v1"
    status = "RUNNING"
    capturedAt = (Get-Date).ToUniversalTime().ToString("o")
    auricruxBaseUrl = $AuricruxBaseUrl
    models = @{
        "auricrux-fca" = @{ endpoint = ($AuricruxBaseUrl + "/api/chat"); keyed = $true }
        chatgpt = @{ model = $openaiModel; keyed = [bool]$openaiKey }
        claude = @{ model = $anthropicModel; keyed = [bool]$anthropicKey }
        gemini = @{ model = $geminiModel; keyed = [bool]$googleKey }
    }
    rubricDimensions = @(
        "domainAccuracy", "actionability", "safetyComplianceDiligence",
        "fieldPracticality", "concision"
    )
    cases = @()
}

$caseNum = 0
foreach ($c in $cases) {
    $caseNum++
    Write-Host ""
    Write-Host ("[{0}/{1}] {2} - {3}" -f $caseNum, @($cases).Count, $c.id, $c.query) -ForegroundColor Yellow
    $answers = [ordered]@{
        "auricrux-fca" = $null
        chatgpt = $null
        claude = $null
        gemini = $null
    }
    $errors = @{}

    try {
        Write-Host "  Auricrux..." -NoNewline
        $ax = Get-AuricruxAnswer -Base $AuricruxBaseUrl -Query $c.query
        $answers["auricrux-fca"] = $ax
        Write-Host (" ok ({0} chars)" -f $ax.Length)
    } catch {
        $errors["auricrux-fca"] = "$_"
        Write-Host (" FAIL " + $_) -ForegroundColor Red
    }

    if ((-not $AuricruxOnly) -and $openaiKey) {
        try {
            Write-Host "  ChatGPT..." -NoNewline
            $ans = Get-OpenAIAnswer -Key $openaiKey -Model $openaiModel -Query $c.query
            $answers.chatgpt = $ans
            Write-Host (" ok ({0} chars)" -f $ans.Length)
        } catch {
            $errors.chatgpt = "$_"
            Write-Host (" FAIL " + $_) -ForegroundColor Red
        }
    }
    if ((-not $AuricruxOnly) -and $anthropicKey) {
        try {
            Write-Host "  Claude..." -NoNewline
            $ans = Get-ClaudeAnswer -Key $anthropicKey -Model $anthropicModel -Query $c.query
            $answers.claude = $ans
            Write-Host (" ok ({0} chars)" -f $ans.Length)
        } catch {
            $errors.claude = "$_"
            Write-Host (" FAIL " + $_) -ForegroundColor Red
        }
    }
    if ((-not $AuricruxOnly) -and $googleKey) {
        try {
            Write-Host "  Gemini..." -NoNewline
            $ans = Get-GeminiAnswer -Key $googleKey -Model $geminiModel -Query $c.query
            $answers.gemini = $ans
            Write-Host (" ok ({0} chars)" -f $ans.Length)
        } catch {
            $errors.gemini = "$_"
            Write-Host (" FAIL " + $_) -ForegroundColor Red
        }
    }

    $scores = $null
    $present = @{}
    foreach ($k in @("auricrux-fca", "chatgpt", "claude", "gemini")) {
        if ($answers[$k]) { $present[$k] = $answers[$k] }
    }

    if ((-not $SkipScore) -and ($present.Count -ge 2)) {
        $judge = $null
        if ($openaiKey) { $judge = @{ p = "openai"; k = $openaiKey; m = $openaiModel } }
        elseif ($anthropicKey) { $judge = @{ p = "anthropic"; k = $anthropicKey; m = $anthropicModel } }
        elseif ($googleKey) { $judge = @{ p = "gemini"; k = $googleKey; m = $geminiModel } }

        if ($judge) {
            try {
                Write-Host ("  Blind judge (" + $judge.p + ")...") -NoNewline
                $scores = Invoke-BlindJudge -JudgeProvider $judge.p -Key $judge.k -Model $judge.m -Query $c.query -Answers $present
                Write-Host " ok"
            } catch {
                $errors.judge = "$_"
                Write-Host (" FAIL " + $_) -ForegroundColor Red
            }
        }
    }

    $run.cases += [ordered]@{
        id = $c.id
        category = $c.category
        query = $c.query
        answers = $answers
        scores = $scores
        errors = $errors
    }

    ($run | ConvertTo-Json -Depth 14) | Set-Content -Path $runPath -Encoding utf8
}

$modelTotals = @{}
foreach ($m in @("auricrux-fca", "chatgpt", "claude", "gemini")) {
    $modelTotals[$m] = @{ sum = 0; n = 0; atParity = 0 }
}

$scoredCases = 0
foreach ($case in $run.cases) {
    if (-not $case.scores) { continue }
    $scoredCases++
    $best = 0
    foreach ($m in @($case.scores.Keys)) {
        $t = [int]$case.scores[$m].total
        if ($t -gt $best) { $best = $t }
    }
    foreach ($m in @($case.scores.Keys)) {
        $t = [int]$case.scores[$m].total
        $modelTotals[$m].sum += $t
        $modelTotals[$m].n++
        if (($best - $t) -le 2) { $modelTotals[$m].atParity++ }
    }
}

$peerKeyed = (@($openaiKey, $anthropicKey, $googleKey) | Where-Object { $_ }).Count
$averages = @{}
foreach ($m in @($modelTotals.Keys)) {
    if ($modelTotals[$m].n -gt 0) {
        $averages[$m] = [math]::Round($modelTotals[$m].sum / $modelTotals[$m].n, 2)
    }
}

$auricParityPct = $null
$verdict = "INCOMPLETE"
$verdictNote = ""
if ($peerKeyed -lt 3 -and (-not $AuricruxOnly)) {
    $verdict = "INCOMPLETE"
    $verdictNote = "Need all three peer keys (OpenAI + Anthropic + Google). Have $peerKeyed/3. Run scripts/setup-peer-keys.ps1"
} elseif ($scoredCases -eq 0) {
    $verdict = "INCOMPLETE"
    $verdictNote = "Answers collected but no scores yet. Add peer keys and re-run without -SkipScore / -AuricruxOnly."
} else {
    $auricParityPct = [math]::Round(100.0 * $modelTotals["auricrux-fca"].atParity / $scoredCases, 1)
    if ($auricParityPct -ge 70) { $verdict = "PASS_CANDIDATE" }
    elseif ($auricParityPct -gt 0) { $verdict = "PARTIAL_CANDIDATE" }
    else { $verdict = "FAIL_CANDIDATE" }
    $verdictNote = "Automated blind-judge provisional only. Human SME review required before flipping AUX-027. Auricrux at-parity on $auricParityPct% (bar >=70%)."
}

$run.status = $verdict
$run.summary = [ordered]@{
    cases = @($run.cases).Count
    scoredCases = $scoredCases
    peersKeyed = $peerKeyed
    averages = $averages
    auricruxAtParityPercent = $auricParityPct
    verdict = $verdict
    verdictNote = $verdictNote
    claimAction = "Do not change AUX-027 in CLAIMS_REGISTER.md until founder accepts this report."
}

($run | ConvertTo-Json -Depth 14) | Set-Content -Path $runPath -Encoding utf8
($run | ConvertTo-Json -Depth 14) | Set-Content -Path $reportJson -Encoding utf8

$avgAx = if ($averages.ContainsKey("auricrux-fca")) { $averages["auricrux-fca"] } else { "-" }
$avgGpt = if ($averages.ContainsKey("chatgpt")) { $averages["chatgpt"] } else { "-" }
$avgCl = if ($averages.ContainsKey("claude")) { $averages["claude"] } else { "-" }
$avgGe = if ($averages.ContainsKey("gemini")) { $averages["gemini"] } else { "-" }
$parityText = if ($null -eq $auricParityPct) { "n/a" } else { "$auricParityPct%" }

$mdLines = New-Object System.Collections.Generic.List[string]
[void]$mdLines.Add("# AUX-027 Peer Comparison Report (v1)")
[void]$mdLines.Add("")
[void]$mdLines.Add("**Captured:** $($run.capturedAt)")
[void]$mdLines.Add("**Verdict:** **$verdict**")
[void]$mdLines.Add("**Auricrux at-parity:** $parityText of $scoredCases scored cases (bar >=70%)")
[void]$mdLines.Add("")
[void]$mdLines.Add($verdictNote)
[void]$mdLines.Add("")
[void]$mdLines.Add("## Models")
[void]$mdLines.Add("")
[void]$mdLines.Add("| Model | Keyed | Avg total (0-25) |")
[void]$mdLines.Add("|-------|-------|------------------|")
[void]$mdLines.Add("| auricrux-fca | yes | $avgAx |")
[void]$mdLines.Add("| chatgpt ($openaiModel) | $([bool]$openaiKey) | $avgGpt |")
[void]$mdLines.Add("| claude ($anthropicModel) | $([bool]$anthropicKey) | $avgCl |")
[void]$mdLines.Add("| gemini ($geminiModel) | $([bool]$googleKey) | $avgGe |")
[void]$mdLines.Add("")
[void]$mdLines.Add("## Cases")
[void]$mdLines.Add("")

foreach ($case in $run.cases) {
    [void]$mdLines.Add("### $($case.id) ($($case.category))")
    [void]$mdLines.Add("**Q:** $($case.query)")
    [void]$mdLines.Add("")
    if ($case.scores) {
        [void]$mdLines.Add("| Model | Total |")
        [void]$mdLines.Add("|-------|-------|")
        foreach ($m in @("auricrux-fca", "chatgpt", "claude", "gemini")) {
            if ($case.scores[$m]) {
                [void]$mdLines.Add("| $m | $($case.scores[$m].total) |")
            }
        }
        [void]$mdLines.Add("")
    } else {
        [void]$mdLines.Add("_No scores (missing peers or -SkipScore)._")
        [void]$mdLines.Add("")
    }
}

[void]$mdLines.Add("## Claim gate")
[void]$mdLines.Add("")
[void]$mdLines.Add("AUX-027 stays FAIL/BLOCKED until this report is accepted after review.")
[void]$mdLines.Add("Automated judge scores are provisional.")
[void]$mdLines.Add("")
[void]$mdLines.Add("Artifacts: eval/reports/peer_comparison_v1_run.json, peer_comparison_v1_report.json")

($mdLines -join "`n") | Set-Content -Path $reportMd -Encoding utf8

Write-Host ""
Write-Host ("Done. Verdict: " + $verdict) -ForegroundColor Yellow
Write-Host $verdictNote
Write-Host "Wrote:"
Write-Host ("  " + $runPath)
Write-Host ("  " + $reportJson)
Write-Host ("  " + $reportMd)

if ($peerKeyed -lt 3) {
    Write-Host ""
    Write-Host "Next: paste keys with  .\scripts\setup-peer-keys.ps1" -ForegroundColor Yellow
}
