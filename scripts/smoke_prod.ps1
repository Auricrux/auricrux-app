#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Live smoke test against the production Auricrux API (AUX-020 / AUX-029 / AUX-030).

.DESCRIPTION
  Hits the real production host (default: https://auricrux.futurecontractorsofamerica.com)
  to prove the deployed backend is the actual AI stack (health, chat, thinking, search),
  not a mock/minimal API shell. Intended to be run manually or wired into a scheduled
  GitHub Actions workflow for ongoing production verification.

.PARAMETER BaseUrl
  Production base URL to test. Defaults to the app's documented production endpoint.

.EXAMPLE
  ./scripts/smoke_prod.ps1
  ./scripts/smoke_prod.ps1 -BaseUrl "https://auricrux.futurecontractorsofamerica.com"
#>
param(
    [string]$BaseUrl = "https://fca-auricrux-api.azurewebsites.net"
)

$ErrorActionPreference = "Stop"
$baseUrl = $BaseUrl.TrimEnd('/')
$results = New-Object System.Collections.Generic.List[object]
$failures = 0

function Invoke-SmokeCheck {
    param(
        [string]$Name,
        [scriptblock]$Action
    )
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $result = & $Action
        $sw.Stop()
        Write-Host "[PASS] $Name ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Green
        $script:results.Add([ordered]@{ name = $Name; status = "PASS"; elapsedMs = $sw.ElapsedMilliseconds; detail = $result })
    }
    catch {
        Write-Host "[FAIL] $Name - $($_.Exception.Message)" -ForegroundColor Red
        $script:results.Add([ordered]@{ name = $Name; status = "FAIL"; error = $_.Exception.Message })
        $script:failures++
    }
}

Write-Host "Auricrux production smoke test against $baseUrl" -ForegroundColor Cyan
Write-Host "Run at (UTC): $((Get-Date).ToUniversalTime().ToString('o'))"
Write-Host ""

Invoke-SmokeCheck "GET /health" {
    $r = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -TimeoutSec 30
    $status = if ($r -is [string]) { $r } elseif ($r.status) { $r.status } else { $null }
    if (-not $status) { throw "No recognizable status in health response: $($r | ConvertTo-Json -Compress)" }
    "status=$status"
}

Invoke-SmokeCheck "GET /api/models" {
    $r = Invoke-RestMethod -Uri "$baseUrl/api/models" -Method Get -TimeoutSec 30
    if (-not $r.models -or $r.models.Count -lt 1) { throw "No models returned" }
    "models=$($r.models -join ',')"
}

Invoke-SmokeCheck "POST /api/chat (real construction query)" {
    $body = '{"query":"What is a sill plate?","thinkingMode":0,"searchScope":0}'
    $r = Invoke-RestMethod -Uri "$baseUrl/api/chat" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 120
    if (-not $r.content -or $r.content.Length -lt 10) { throw "Chat content missing or too short" }
    "contentLength=$($r.content.Length)"
}

Invoke-SmokeCheck "POST /api/thinking (non-mock reasoning)" {
    # ThinkingRequest binds `Mode` (enum), not chat's thinkingMode — Deep can overload Ollama/App Service (503).
    $body = '{"query":"How do I sequence a concrete pour after formwork?","mode":0}'
    $r = Invoke-RestMethod -Uri "$baseUrl/api/thinking" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 180
    if (-not $r.result) { throw "No result field in thinking response" }
    if ($r.result -match '(?i)mock|placeholder|lorem ipsum') { throw "Thinking result looks mocked" }
    "resultLength=$($r.result.Length)"
}

Invoke-SmokeCheck "POST /api/search (retrieved corpus hits)" {
    $body = @{ query = "OSHA fall protection height"; searchScope = "Internal" } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$baseUrl/api/search" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
    if (-not $r.results -or $r.results.Count -lt 1) { throw "No search results returned" }
    "resultCount=$($r.results.Count)"
}

Invoke-SmokeCheck "GET /api/capabilities (feature parity matrix)" {
    $r = Invoke-RestMethod -Uri "$baseUrl/api/capabilities" -Method Get -TimeoutSec 30
    if (-not $r.features -or $r.features.Count -lt 10) { throw "Capabilities matrix missing or too shallow" }
    if (-not $r.constructionMoat) { throw "Construction moat summary missing" }
    "shippedCore=$($r.parityScore.shippedCore) corpus=$($r.corpusEntries)"
}

Write-Host ""
$report = [ordered]@{
    baseUrl = $baseUrl
    runAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    totalChecks = $results.Count
    failures = $failures
    checks = $results
}
$reportDir = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "eval") "reports"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$reportPath = Join-Path $reportDir "prod_smoke_last_run.json"
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding utf8
Write-Host "Report written to $reportPath"

if ($failures -gt 0) {
    Write-Host "$failures check(s) FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "All checks PASSED against live production backend." -ForegroundColor Green
exit 0
