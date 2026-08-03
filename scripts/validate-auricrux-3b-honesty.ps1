# Fail if capabilities honesty drifts from model_manifest.json (alias lies, stale 70000, etc.).
param(
    [string]$BaseUrl = '',
    [switch]$LocalArtifactsOnly,
    [string]$ManifestPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
}
$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$errors = @()
$merged = ($manifest.status -match 'product-ollama-loaded') -or ($manifest.auricruxFcaAlias.kind -match 'merged-lora-gguf')

# Local artifact gates (always)
$card = Join-Path $repoRoot 'auricrux\system\AURICRUX_3B_MODEL_CARD.md'
if (-not (Test-Path $card)) { $errors += 'Missing AURICRUX_3B_MODEL_CARD.md' }
$modelfile = Get-Content (Join-Path $repoRoot 'auricrux\system\Modelfile.auricrux-fca') -Raw
if ($modelfile -notmatch 'DEV FALLBACK ONLY') {
    $errors += 'Modelfile.auricrux-fca missing DEV FALLBACK ONLY banner'
}
if ($manifest.migrationPolicy.productionFinalLabel -match '70000') {
    $errors += 'model_manifest migrationPolicy still targets obsolete checkpoint-70000'
}
if (-not $merged) {
    $errors += 'model_manifest does not record merged LoRA GGUF product load'
}

if (-not $LocalArtifactsOnly -and -not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $caps = Invoke-RestMethod -Uri "$BaseUrl/api/capabilities" -TimeoutSec 30
    if ($merged -and -not $caps.constructionMoat.promotedFineTuneLive) {
        $errors += 'Capabilities PromotedFineTuneLive=false but manifest says merged GGUF live (deploy honesty fix)'
    }
    if ($caps.constructionMoat.notes -match 'llama3\.2 alias|checkpoint-70000 not yet exported') {
        $errors += 'Capabilities notes still describe obsolete alias / checkpoint-70000 export gap'
    }
    $ft = $caps.features | Where-Object { $_.name -match 'Fine-tuned' } | Select-Object -First 1
    if ($merged -and $ft -and $ft.status -eq 'blocked' -and ($ft.detail -match '70000')) {
        $errors += 'Fine-tuned feature still blocked on obsolete checkpoint-70000 wording'
    }
    if ($caps.constructionMoat.notes -match 'alias with construction system prompt') {
        $errors += 'Capabilities still claim system-prompt alias as live product weights'
    }
    Write-Host "Live capabilities notes: $($caps.constructionMoat.notes)"
}

if ($errors.Count -gt 0) {
    Write-Host 'HONESTY VALIDATION FAILED:'
    $errors | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host 'HONESTY VALIDATION PASS'
exit 0
