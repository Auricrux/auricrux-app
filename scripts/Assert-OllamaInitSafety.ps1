<#
.SYNOPSIS
  Prove default Ollama/compose startup cannot silently fall back to llama3.2 / Modelfile recreate.
.DESCRIPTION
  Audits docker-compose.yml, Modelfile, warm/cutover workflows, and related scripts.
  Token: OLLAMA_INIT_SAFETY_OK / OLLAMA_INIT_SAFETY_BLOCKED
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

Write-Host '=== Auricrux Ollama init safety ===' -ForegroundColor Cyan
Write-Host 'Default startup must not pull llama3.2, recreate auricrux-fca, or silently substitute fallback.'

$composePath = Join-Path $repoRoot 'docker-compose.yml'
$mfPath = Join-Path $repoRoot 'auricrux\system\Modelfile.auricrux-fca'
$warmPath = Join-Path $repoRoot '.github\workflows\gcp-warm-auricrux-fca.yml'
$cutoverPath = Join-Path $repoRoot '.github\workflows\gcp-cutover-build-auricrux.yml'
$fixOllama = Join-Path $repoRoot '.github\workflows\gcp-vm-fix-auricrux-ollama.yml'

# --- OI-01 compose present + profile gate ---
if (-not (Test-Path $composePath)) {
    Add-Check 'OI-01-profile-gate' 'FAIL' 'docker-compose.yml missing'
} else {
    $cText = Get-Content $composePath -Raw
    $hasInit = $cText -match '(?m)^\s*ollama-model-init:'
    $hasProfile = $cText -match '(?ms)ollama-model-init:.*?profiles:\s*\[["'']?dev-fallback["'']?\]'
    if (-not $hasProfile) {
        $hasProfile = $cText -match '(?ms)ollama-model-init:.*?profiles:\s*\n\s*-\s*["'']?dev-fallback'
    }
    if (-not $hasInit) {
        Add-Check 'OI-01-profile-gate' 'FAIL' 'ollama-model-init service missing'
    } elseif (-not $hasProfile) {
        Add-Check 'OI-01-profile-gate' 'FAIL' 'ollama-model-init missing profiles: [dev-fallback]'
    } else {
        Add-Check 'OI-01-profile-gate' 'PASS' 'ollama-model-init requires profiles: [dev-fallback]'
    }
}

# --- OI-02 default services do not pull/create ---
if (Test-Path $composePath) {
    $cText = Get-Content $composePath -Raw
    # Split: everything before ollama-model-init is "default path" for ollama + auricrux-web definitions
    # More precise: auricrux-web and ollama service blocks must not contain ollama pull/create
    $webMatch = [regex]::Match($cText, '(?ms)^\s*auricrux-web:(.*?)(?=^\s*\w|\z)')
    $ollamaSvc = [regex]::Match($cText, '(?ms)^\s*ollama:(.*?)(?=^\s{2}\w|\z)')
    $defaultCombined = ($ollamaSvc.Groups[1].Value + "`n" + $webMatch.Groups[1].Value)
    $badPull = $defaultCombined -match 'ollama\s+pull'
    $badCreate = $defaultCombined -match 'ollama\s+create'
    $webDependsInit = $false
    $webBlock = [regex]::Match($cText, '(?ms)^\s*auricrux-web:(.*?)(?=^\s{2}[a-zA-Z]|^\s*volumes:|\z)')
    if ($webBlock.Success) {
        # Only treat as silent fallback if depends_on lists the init service name as a dependency entry.
        $webDependsInit = $webBlock.Value -match '(?ms)depends_on:\s*(?:\n\s*-\s*ollama-model-init|\n\s*ollama-model-init:)'
    }
    if ($badPull -or $badCreate) {
        Add-Check 'OI-02-default-no-pull-create' 'FAIL' 'Default ollama/auricrux-web blocks contain ollama pull/create'
    } elseif ($webDependsInit) {
        Add-Check 'OI-02-default-no-pull-create' 'FAIL' 'auricrux-web depends_on ollama-model-init (silent fallback)'
    } else {
        Add-Check 'OI-02-default-no-pull-create' 'PASS' 'Default ollama + auricrux-web have no pull/create; web does not depend on model-init'
    }
}

# --- OI-03 llama3.2 pull only inside profile-gated init ---
if (Test-Path $composePath) {
    $cText = Get-Content $composePath -Raw
    $pullMatches = [regex]::Matches($cText, 'ollama\s+pull\s+llama3\.2[^\s;]*')
    $initBlock = [regex]::Match($cText, '(?ms)ollama-model-init:(.*?)(?=^\s{2}[a-zA-Z]|\z)')
    $allInInit = $true
    foreach ($m in $pullMatches) {
        if (-not $initBlock.Success -or $initBlock.Value.IndexOf($m.Value) -lt 0) {
            $allInInit = $false
            break
        }
    }
    if ($pullMatches.Count -eq 0) {
        Add-Check 'OI-03-llama32-pull-confined' 'PASS' 'No llama3.2 pull in compose (even safer)'
    } elseif ($allInInit -and ($cText -match '(?ms)ollama-model-init:.*?profiles:\s*\[["'']?dev-fallback')) {
        Add-Check 'OI-03-llama32-pull-confined' 'PASS' ("llama3.2 pull confined to profile-gated ollama-model-init (count={0})" -f $pullMatches.Count)
    } else {
        Add-Check 'OI-03-llama32-pull-confined' 'FAIL' 'llama3.2 pull found outside profile-gated ollama-model-init'
    }
}

# --- OI-04 never create product tag auricrux-fca from compose Modelfile path ---
if (Test-Path $composePath) {
    $cText = Get-Content $composePath -Raw
    $createsProduct = $cText -match 'ollama\s+create\s+auricrux-fca(\s|$|;|"|'')'
    $createsFallback = $cText -match 'ollama\s+create\s+auricrux-fca-dev-fallback'
    if ($createsProduct) {
        Add-Check 'OI-04-no-product-tag-create' 'FAIL' 'compose still runs ollama create auricrux-fca (would clobber product)'
    } elseif ($createsFallback) {
        Add-Check 'OI-04-no-product-tag-create' 'PASS' 'init creates auricrux-fca-dev-fallback only (product tag untouched)'
    } else {
        Add-Check 'OI-04-no-product-tag-create' 'PASS' 'No ollama create of auricrux-fca in compose'
    }
}

# --- OI-05 Modelfile banner + forbid product create ---
if (-not (Test-Path $mfPath)) {
    Add-Check 'OI-05-modelfile-banner' 'FAIL' 'Modelfile.auricrux-fca missing'
} else {
    $mf = Get-Content $mfPath -Raw
    $ok = ($mf -match 'DEV FALLBACK ONLY') -and ($mf -match 'Do NOT use this file to overwrite product')
    $forbids = ($mf -match 'Do NOT run:\s*ollama create auricrux-fca') -or ($mf -match 'never create auricrux-fca' -or $mf -match 'Do NOT run: ollama create auricrux-fca')
    $devTag = $mf -match 'auricrux-fca-dev-fallback'
    if (-not $ok) {
        Add-Check 'OI-05-modelfile-banner' 'FAIL' 'Modelfile missing DEV FALLBACK ONLY / do-not-overwrite banner'
    } elseif (-not $forbids) {
        Add-Check 'OI-05-modelfile-banner' 'FAIL' 'Modelfile must explicitly forbid ollama create auricrux-fca'
    } elseif (-not $devTag) {
        Add-Check 'OI-05-modelfile-banner' 'FAIL' 'Modelfile must document auricrux-fca-dev-fallback tag'
    } else {
        Add-Check 'OI-05-modelfile-banner' 'PASS' 'Modelfile bans product overwrite; documents auricrux-fca-dev-fallback'
    }
}

# --- OI-06 warm workflow ---
if (-not (Test-Path $warmPath)) {
    Add-Check 'OI-06-warm-no-recreate' 'FAIL' 'gcp-warm-auricrux-fca.yml missing'
} else {
    $w = Get-Content $warmPath -Raw
    $badCreate = $w -match 'ollama\s+create\s+auricrux-fca'
    $badPull = $w -match 'ollama\s+pull\s+llama3\.2'
    $badModelfileCreate = $w -match 'ollama\s+create[^\n]*Modelfile'
    $good = $w -match '(?i)do NOT Modelfile' -or $w -match '(?i)no Modelfile recreate'
    $failsMissing = $w -match 'auricrux-fca missing'
    if ($badCreate -or $badPull -or $badModelfileCreate) {
        Add-Check 'OI-06-warm-no-recreate' 'FAIL' 'Warm workflow still creates/pulls/Modelfile-recreates'
    } elseif (-not $good -or -not $failsMissing) {
        Add-Check 'OI-06-warm-no-recreate' 'FAIL' 'Warm missing hard-fail when auricrux-fca absent / no-recreate guard'
    } else {
        Add-Check 'OI-06-warm-no-recreate' 'PASS' 'Warm warms existing tag only; fails closed if missing'
    }
}

# --- OI-07 cutover build does not run model-init / llama3.2 recreate ---
if (-not (Test-Path $cutoverPath)) {
    Add-Check 'OI-07-cutover-no-init' 'FAIL' 'gcp-cutover-build-auricrux.yml missing'
} else {
    $ct = Get-Content $cutoverPath -Raw
    if ($ct -match 'ollama-model-init' -or $ct -match 'dev-fallback' -or $ct -match 'ollama pull llama3\.2' -or $ct -match 'ollama create auricrux-fca') {
        Add-Check 'OI-07-cutover-no-init' 'FAIL' 'Cutover workflow references model-init / llama3.2 create'
    } else {
        Add-Check 'OI-07-cutover-no-init' 'PASS' 'Cutover rebuilds web only; does not Modelfile/llama3.2-init'
    }
}

# --- OI-08 fix-ollama workflow does not recreate auricrux-fca from llama3.2 ---
if (-not (Test-Path $fixOllama)) {
    Add-Check 'OI-08-fix-ollama-safe' 'WARN' 'gcp-vm-fix-auricrux-ollama.yml missing'
} else {
    $fx = Get-Content $fixOllama -Raw
    if ($fx -match 'ollama create auricrux-fca' -or $fx -match 'ollama pull llama3\.2') {
        Add-Check 'OI-08-fix-ollama-safe' 'FAIL' 'fix-ollama workflow recreates auricrux-fca / pulls llama3.2'
    } else {
        Add-Check 'OI-08-fix-ollama-safe' 'PASS' 'fix-ollama does not Modelfile/llama3.2-recreate product tag'
    }
}

# --- OI-09 PrimaryModel default remains product tag (no silent fallback name) ---
if (Test-Path $composePath) {
    $cText = Get-Content $composePath -Raw
    if ($cText -match 'Auricrux__PrimaryModel:\s*"auricrux-fca-dev-fallback"') {
        Add-Check 'OI-09-primary-not-silent-fallback' 'FAIL' 'Default PrimaryModel is fallback tag (silent substitution)'
    } elseif ($cText -match 'Auricrux__PrimaryModel:\s*"auricrux-fca"') {
        Add-Check 'OI-09-primary-not-silent-fallback' 'PASS' 'Default PrimaryModel=auricrux-fca (product); fallback requires explicit env'
    } else {
        Add-Check 'OI-09-primary-not-silent-fallback' 'WARN' 'PrimaryModel not found in compose'
    }
}

# --- OI-10 docs present ---
$doc = Join-Path $repoRoot 'docs\runtime-proof\OLLAMA_INIT_SAFE_UNSAFE_PATHS.md'
if (-not (Test-Path $doc)) {
    Add-Check 'OI-10-docs' 'FAIL' 'OLLAMA_INIT_SAFE_UNSAFE_PATHS.md missing'
} else {
    $d = Get-Content $doc -Raw
    $need = @('--profile dev-fallback', 'SAFE', 'UNSAFE', 'llama3.2', 'auricrux-fca', 'Modelfile')
    $missing = @($need | Where-Object { $d -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        Add-Check 'OI-10-docs' 'FAIL' ("Doc missing: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'OI-10-docs' 'PASS' 'Safe/unsafe path documentation present'
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'OLLAMA_INIT_SAFETY_OK' } else { 'OLLAMA_INIT_SAFETY_BLOCKED' }

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    checks = $checks
}
$receiptPath = Join-Path $receiptDir 'ollama-init-safety-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)

if ($fail -gt 0) {
    Write-Host 'BLOCKERS:' -ForegroundColor Red
    $checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.id, $_.detail) -ForegroundColor Red
    }
    exit 1
}

Write-Host 'OLLAMA_INIT_SAFETY_OK'
exit 0
