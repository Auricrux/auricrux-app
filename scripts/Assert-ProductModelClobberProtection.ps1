<#
.SYNOPSIS
  Assert product model clobber protection across warm/compose/init/import/fallback/deploy paths.
.DESCRIPTION
  Ensures auricrux-fca cannot be overwritten/recreated except via authorized cutover paths
  that require explicit authorization and produce evidence.
  Token: PRODUCT_MODEL_CLOBBER_PROTECTION_OK / PRODUCT_MODEL_CLOBBER_PROTECTION_BLOCKED
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

function Test-MutatesProductTag([string]$Text) {
    # Ignore comment lines so "Do NOT run: ollama create auricrux-fca" docs do not false-positive.
    $stripped = [regex]::Replace($Text, '(?m)^\s*#.*$', '')
    # Ignore PowerShell -match/-replace detection strings and single-quoted refusal messages.
    $stripped = [regex]::Replace($stripped, "(?m)^.*-match\s+'[^']*'.*$", '')
    $stripped = [regex]::Replace($stripped, "(?m)^.*-match\s+""[^""]*"".*$", '')
    $stripped = [regex]::Replace($stripped, "(?m)^.*Add-(Check|Blocker)\s+'[^']*'.*$", '')
    $stripped = [regex]::Replace($stripped, "(?m)^.*'(Do NOT|Refuse|BLOCKED|must not)[^']*'.*$", '')
    $create = [regex]::IsMatch($stripped, 'ollama\s+create\s+auricrux-fca(\s|$|;|"|'')')
    $rm = [regex]::IsMatch($stripped, 'ollama\s+rm\s+auricrux-fca(\s|$|;|"|'')')
    $cpOnto = [regex]::IsMatch($stripped, 'ollama\s+cp\s+\S+\s+auricrux-fca(\s|$|;|"|'')')
    return ($create -or $rm -or $cpOnto)
}

function Test-HasCutoverAuthGate([string]$Text) {
    return (
        ($Text -match 'authorize_product_model_cutover') -and
        ($Text -match 'cutover_reason') -and
        ($Text -match 'PRODUCT_MODEL_CLOBBER_BLOCKED' -or $Text -match 'CLOBBER_BLOCKED')
    )
}

Write-Host '=== Auricrux product model clobber protection ===' -ForegroundColor Cyan
Write-Host 'Refuse overwrite/recreate/replace of auricrux-fca without authorized cutover + evidence.'

$policyPath = Join-Path $repoRoot 'auricrux\system\product_model_clobber_policy.json'
if (-not (Test-Path $policyPath)) {
    Add-Check 'PC-01-policy' 'FAIL' 'product_model_clobber_policy.json missing'
    $policy = $null
} else {
    try {
        $policy = Get-Content $policyPath -Raw | ConvertFrom-Json
        if ([string]$policy.productModelTag -ne 'auricrux-fca') {
            Add-Check 'PC-01-policy' 'FAIL' 'policy productModelTag must be auricrux-fca'
        } else {
            Add-Check 'PC-01-policy' 'PASS' 'Clobber policy present for auricrux-fca'
        }
    } catch {
        Add-Check 'PC-01-policy' 'FAIL' ("policy parse error: {0}" -f $_.Exception.Message)
        $policy = $null
    }
}

$requireScript = Join-Path $repoRoot 'scripts\Require-ProductModelCutoverAuthorization.ps1'
if (-not (Test-Path $requireScript)) {
    Add-Check 'PC-02-require-helper' 'FAIL' 'Require-ProductModelCutoverAuthorization.ps1 missing'
} else {
    $rs = Get-Content $requireScript -Raw
    if ($rs -match 'PRODUCT_MODEL_CLOBBER_BLOCKED' -and $rs -match 'PRODUCT_MODEL_CUTOVER_AUTHORIZED' -and $rs -match 'cutover-auth') {
        Add-Check 'PC-02-require-helper' 'PASS' 'Authorization helper refuses without flag/reason and writes evidence'
    } else {
        Add-Check 'PC-02-require-helper' 'FAIL' 'Authorization helper missing block/ok tokens or evidence write'
    }
}

# --- PC-03 paths that must never mutate product tag ---
$neverPaths = @(
    'docker-compose.yml',
    '.github\workflows\gcp-warm-auricrux-fca.yml',
    '.github\workflows\gcp-cutover-build-auricrux.yml',
    '.github\workflows\gcp-vm-cutover-deploy.yml',
    '.github\workflows\gcp-vm-fix-auricrux-ollama.yml',
    'scripts\deploy_azure.ps1',
    'auricrux\system\Modelfile.auricrux-fca'
)
$neverFail = @()
foreach ($rel in $neverPaths) {
    $p = Join-Path $repoRoot $rel
    if (-not (Test-Path $p)) {
        $neverFail += ("missing:{0}" -f $rel)
        continue
    }
    $t = Get-Content $p -Raw
    if (Test-MutatesProductTag $t) {
        $neverFail += ("mutates:{0}" -f $rel)
    }
}
if ($neverFail.Count -gt 0) {
    Add-Check 'PC-03-never-mutate-paths' 'FAIL' ($neverFail -join '; ')
} else {
    Add-Check 'PC-03-never-mutate-paths' 'PASS' ("Checked {0} warm/compose/init/fallback/deploy paths - no product-tag mutate" -f $neverPaths.Count)
}

# --- PC-04 compose fallback creates only -dev-fallback ---
$compose = Join-Path $repoRoot 'docker-compose.yml'
if (Test-Path $compose) {
    $c = Get-Content $compose -Raw
    if ($c -match 'ollama\s+create\s+auricrux-fca-dev-fallback' -and -not (Test-MutatesProductTag $c)) {
        Add-Check 'PC-04-fallback-tag' 'PASS' 'dev-fallback creates auricrux-fca-dev-fallback only'
    } else {
        Add-Check 'PC-04-fallback-tag' 'FAIL' 'Fallback init must create auricrux-fca-dev-fallback and never auricrux-fca'
    }
}

# --- PC-05 authorized cutover path must gate + evidence ---
$loadPath = Join-Path $repoRoot '.github\workflows\gcp-load-ckpt110000-gguf.yml'
if (-not (Test-Path $loadPath)) {
    Add-Check 'PC-05-authorized-cutover-gated' 'FAIL' 'gcp-load GGUF workflow missing'
} else {
    $lt = Get-Content $loadPath -Raw
    if (-not (Test-MutatesProductTag $lt)) {
        Add-Check 'PC-05-authorized-cutover-gated' 'FAIL' 'Authorized load workflow no longer mutates product tag (unexpected)'
    } elseif (-not (Test-HasCutoverAuthGate $lt)) {
        Add-Check 'PC-05-authorized-cutover-gated' 'FAIL' 'Load workflow mutates auricrux-fca without authorize_product_model_cutover + cutover_reason + CLOBBER_BLOCKED gate'
    } elseif ($lt -notmatch 'product-model-cutover-evidence' -and $lt -notmatch 'cutover-auth' -and $lt -notmatch 'PRODUCT_MODEL_CUTOVER_EVIDENCE') {
        Add-Check 'PC-05-authorized-cutover-gated' 'FAIL' 'Load workflow missing cutover evidence write'
    } else {
        Add-Check 'PC-05-authorized-cutover-gated' 'PASS' 'GCS load cutover requires explicit auth input + reason + evidence'
    }
}

# --- PC-06 no unauthorized mutators elsewhere ---
$scanRoots = @(
    (Join-Path $repoRoot 'scripts'),
    (Join-Path $repoRoot '.github\workflows')
)
$allowedMutators = @(
    (Join-Path $repoRoot '.github\workflows\gcp-load-ckpt110000-gguf.yml').ToLowerInvariant()
)
$rogue = @()
foreach ($root in $scanRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem $root -Recurse -File -Include *.ps1,*.yml,*.yaml,*.sh | ForEach-Object {
        $full = $_.FullName
        if ($allowedMutators -contains $full.ToLowerInvariant()) { return }
        # Skip this assert script and require helper (they mention patterns in strings)
        if ($_.Name -match 'Assert-ProductModelClobber|Require-ProductModelCutover|Assert-OllamaInit|Assert-GgufSuite') { return }
        $t = Get-Content $full -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($t)) { return }
        if (Test-MutatesProductTag $t) {
            $rogue += ($full.Replace($repoRoot + '\', '').Replace($repoRoot + '/', ''))
        }
    }
}
if ($rogue.Count -gt 0) {
    Add-Check 'PC-06-no-rogue-mutators' 'FAIL' ("Unauthorized product-tag mutators: {0}" -f ($rogue -join '; '))
} else {
    Add-Check 'PC-06-no-rogue-mutators' 'PASS' 'No unauthorized scripts/workflows mutate auricrux-fca'
}

# --- PC-07 docs ---
$doc = Join-Path $repoRoot 'docs\runtime-proof\PRODUCT_MODEL_CLOBBER_PROTECTION.md'
if (-not (Test-Path $doc)) {
    Add-Check 'PC-07-docs' 'FAIL' 'PRODUCT_MODEL_CLOBBER_PROTECTION.md missing'
} else {
    $d = Get-Content $doc -Raw
    $need = @('authorize_product_model_cutover', 'auricrux-fca', 'evidence', 'SAFE', 'AUTHORIZED', 'BLOCKED')
    $missing = @($need | Where-Object { $d -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        Add-Check 'PC-07-docs' 'FAIL' ("Doc missing: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'PC-07-docs' 'PASS' 'Clobber protection documentation present'
    }
}

# --- PC-08 require helper refuses without auth (smoke) ---
if (Test-Path $requireScript) {
    $prevA = $env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED
    $prevR = $env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON
    try {
        Remove-Item Env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED -ErrorAction SilentlyContinue
        Remove-Item Env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON -ErrorAction SilentlyContinue
        & $requireScript -Actor 'assert-smoke' -Operation 'test-refuse' 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Add-Check 'PC-08-refuse-without-auth' 'FAIL' 'Require helper authorized without env flag'
        } else {
            Add-Check 'PC-08-refuse-without-auth' 'PASS' 'Require helper blocks when authorization env unset'
        }
    } finally {
        if ($null -ne $prevA) { $env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED = $prevA } else { Remove-Item Env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED -ErrorAction SilentlyContinue }
        if ($null -ne $prevR) { $env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON = $prevR } else { Remove-Item Env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON -ErrorAction SilentlyContinue }
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'PRODUCT_MODEL_CLOBBER_PROTECTION_OK' } else { 'PRODUCT_MODEL_CLOBBER_PROTECTION_BLOCKED' }

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    productModelTag = 'auricrux-fca'
    policyPath = 'auricrux/system/product_model_clobber_policy.json'
    checks = $checks
}
$receiptPath = Join-Path $receiptDir 'product-model-clobber-protection-latest.json'
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

Write-Host 'PRODUCT_MODEL_CLOBBER_PROTECTION_OK'
exit 0
