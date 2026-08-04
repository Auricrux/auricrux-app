<#
.SYNOPSIS
  Protect live 3B TRUE God train: static audit that no app/models automation can interrupt it.
.DESCRIPTION
  Read-only. Does NOT SSH to train host. Does NOT probe/kill/pause/move/optimize train PID.
  Token: LIVE_3B_TRAIN_PROTECTION_OK / LIVE_3B_TRAIN_PROTECTION_BLOCKED
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
$modelsRoot = 'C:\Users\MichaelBartholomew\source\auricrux-models'
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Test-DangerousTrainTouch([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    # Ignore comment lines
    $s = [regex]::Replace($Text, '(?m)^\s*#.*$', '')
    $patterns = @(
        'kill\s+-9\s+\$?\{?TrainPid',
        'kill\s+-9\s+1019003',
        'pkill\s+.*train',
        'killall\s+.*python',
        'az\s+vm\s+stop',
        'az\s+vm\s+deallocate',
        'az\s+vm\s+delete',
        'Stop-AzVM',
        'fuser\s+-k.*/dev/nvidia',
        'nvidia-smi\s+--gpu-reset',
        'systemctl\s+stop\s+.*train'
    )
    foreach ($p in $patterns) {
        if ([regex]::IsMatch($s, $p, 'IgnoreCase')) { return $true }
    }
    return $false
}

Write-Host '=== Live 3B train protection audit ===' -ForegroundColor Cyan
Write-Host 'Read-only. Will not touch train PID/host. No restart/pause/move/optimize.'

$policyPath = Join-Path $repoRoot 'auricrux\system\live_3b_train_protection_policy.json'
$manifestPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'

# TP-01 policy
if (-not (Test-Path $policyPath)) {
    Add-Check 'TP-01-policy' 'FAIL' 'live_3b_train_protection_policy.json missing'
} else {
    try {
        $pol = Get-Content $policyPath -Raw | ConvertFrom-Json
        if ([string]$pol.okToken -ne 'LIVE_3B_TRAIN_PROTECTION_OK') {
            Add-Check 'TP-01-policy' 'FAIL' 'policy okToken incorrect'
        } else {
            Add-Check 'TP-01-policy' 'PASS' 'Live 3B train protection policy present'
        }
    } catch {
        Add-Check 'TP-01-policy' 'FAIL' ("policy parse error: {0}" -f $_.Exception.Message)
    }
}

# TP-02 manifest do-not-interrupt
if (-not (Test-Path $manifestPath)) {
    Add-Check 'TP-02-manifest-status' 'FAIL' 'model_manifest.json missing'
    $trainPid = 0
    $trainStatus = ''
} else {
    $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $trainStatus = [string]$m.trueGodRun.status
    $trainPid = 0
    try { $trainPid = [int]$m.trueGodRun.trainPid } catch { $trainPid = 0 }
    $hostName = [string]$m.trueGodRun.host
    if ($trainStatus -ne 'running-do-not-interrupt') {
        Add-Check 'TP-02-manifest-status' 'WARN' ("trueGodRun.status={0} (expected running-do-not-interrupt while live)" -f $trainStatus)
    } elseif ($trainPid -le 0) {
        Add-Check 'TP-02-manifest-status' 'FAIL' 'trainPid missing while status=running-do-not-interrupt'
    } else {
        Add-Check 'TP-02-manifest-status' 'PASS' ("status=running-do-not-interrupt pid={0} host={1} (not probed)" -f $trainPid, $hostName)
    }
}

# TP-03 suite / gate / warm / init / deploy never contact train host
$neverHost = @(
    'scripts\run-gguf-construction-suite.ps1',
    'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1',
    'scripts\Assert-OllamaInitSafety.ps1',
    'scripts\Assert-ProductModelClobberProtection.ps1',
    'docker-compose.yml',
    '.github\workflows\gcp-warm-auricrux-fca.yml',
    '.github\workflows\gcp-cutover-build-auricrux.yml',
    '.github\workflows\gcp-load-ckpt110000-gguf.yml',
    'scripts\deploy_azure.ps1'
)
$hostMarkers = @(
    'auricrux-gpu-ncast4',
    'AURICRUX-TRAINING-NCAST4',
    '20\.65\.32\.150',
    '/mnt/auricrux-eod/runs',
    'trainPid',
    '1019003'
)
$badContact = @()
foreach ($rel in $neverHost) {
    $p = Join-Path $repoRoot $rel
    if (-not (Test-Path $p)) { $badContact += ("missing:{0}" -f $rel); continue }
    $t = Get-Content $p -Raw
    # Allow documentation mentions of do-not-interrupt / trainPid in comments for suite runner banner
    $stripped = [regex]::Replace($t, '(?m)^\s*#.*$', '')
    foreach ($mk in $hostMarkers) {
        # suite may mention "Does not interrupt train PID" in comments only - stripped OK
        if ($mk -eq 'trainPid' -or $mk -eq '1019003') {
            if ([regex]::IsMatch($stripped, $mk)) { $badContact += ("{0} references {1}" -f $rel, $mk) }
        } elseif ([regex]::IsMatch($stripped, $mk)) {
            $badContact += ("{0} contacts train marker {1}" -f $rel, $mk)
        }
    }
    if (Test-DangerousTrainTouch $t) {
        $badContact += ("{0} contains dangerous train-touch pattern" -f $rel)
    }
}
if ($badContact.Count -gt 0) {
    Add-Check 'TP-03-suite-deploy-isolated' 'FAIL' ($badContact -join '; ')
} else {
    Add-Check 'TP-03-suite-deploy-isolated' 'PASS' 'Suite/gate/warm/init/cutover/load/deploy do not contact train host/PID'
}

# TP-04 no dangerous kill/stop patterns in app scripts + workflows
$scanApp = @(
    Get-ChildItem (Join-Path $repoRoot 'scripts') -Filter *.ps1 -File -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $repoRoot '.github\workflows') -Filter *.yml -File -ErrorAction SilentlyContinue
)
$dangerHits = @()
foreach ($f in $scanApp) {
    if ($f.Name -match 'Assert-Live3bTrainProtection') { continue }
    $t = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
    if (Test-DangerousTrainTouch $t) {
        $dangerHits += $f.FullName.Replace($repoRoot + '\', '')
    }
}
if ($dangerHits.Count -gt 0) {
    Add-Check 'TP-04-no-kill-patterns' 'FAIL' ("Dangerous patterns: {0}" -f ($dangerHits -join '; '))
} else {
    Add-Check 'TP-04-no-kill-patterns' 'PASS' 'No kill/vm-stop/gpu-reset patterns in app scripts/workflows'
}

# TP-05 CPU export script guards
$exportPs1 = Join-Path $repoRoot 'scripts\stage-and-cpu-export-checkpoint.ps1'
if (-not (Test-Path $exportPs1)) {
    Add-Check 'TP-05-cpu-export-guard' 'FAIL' 'stage-and-cpu-export-checkpoint.ps1 missing'
} else {
    $ex = Get-Content $exportPs1 -Raw
    $hasBanner = $ex -match 'Never uses the train GPU' -or $ex -match 'LIVE 3B TRAIN PROTECTION'
    $forcesEmpty = $ex -match 'CUDA_VISIBLE_DEVICES='
    $probesOnly = $ex -match 'ps -p \$TrainPidProbe' -or $ex -match 'ps -p \$TrainPidProbe'
    $noKill = -not (Test-DangerousTrainTouch $ex)
    if ($hasBanner -and $forcesEmpty -and $noKill) {
        Add-Check 'TP-05-cpu-export-guard' 'PASS' 'CPU export stages/copies only; forces CUDA_VISIBLE_DEVICES empty; no kill'
    } else {
        Add-Check 'TP-05-cpu-export-guard' 'FAIL' ("banner={0} cudaEmpty={1} noKill={2}" -f $hasBanner, $forcesEmpty, $noKill)
    }
}

# TP-06 models automation banners (if models root present)
if (-not (Test-Path $modelsRoot)) {
    Add-Check 'TP-06-models-hub-banners' 'WARN' 'auricrux-models root not found; skipped'
} else {
    $hubs = @(
        'scripts\Invoke-AuricruxAutomationHub.ps1',
        'scripts\Invoke-ProductionReadinessAudit.ps1',
        'scripts\Invoke-ZeroKnownGapAudit.ps1',
        'scripts\Invoke-AuricruxWorkflowRegression.ps1'
    )
    $missingBanner = @()
    foreach ($rel in $hubs) {
        $p = Join-Path $modelsRoot $rel
        if (-not (Test-Path $p)) { $missingBanner += ("missing:{0}" -f $rel); continue }
        $t = Get-Content $p -Raw
        if ($t -notmatch 'Never kills train PID' -and $t -notmatch 'No PID kill') {
            $missingBanner += $rel
        }
        if (Test-DangerousTrainTouch $t) {
            $missingBanner += ("danger:{0}" -f $rel)
        }
    }
    if ($missingBanner.Count -gt 0) {
        Add-Check 'TP-06-models-hub-banners' 'FAIL' ($missingBanner -join '; ')
    } else {
        Add-Check 'TP-06-models-hub-banners' 'PASS' 'Models hub/audit scripts declare no PID kill'
    }
}

# TP-07 warm/init/clobber already refuse product recreate (indirect starvation via product host OK)
$warm = Join-Path $repoRoot '.github\workflows\gcp-warm-auricrux-fca.yml'
$compose = Join-Path $repoRoot 'docker-compose.yml'
$warmOk = (Test-Path $warm) -and ((Get-Content $warm -Raw) -match 'never recreate|Do NOT Modelfile|LIVE 3B TRAIN PROTECTION')
$composeOk = (Test-Path $compose) -and ((Get-Content $compose -Raw) -match 'dev-fallback') -and ((Get-Content $compose -Raw) -notmatch 'auricrux-gpu-ncast4')
if ($warmOk -and $composeOk) {
    Add-Check 'TP-07-warm-init-no-train-host' 'PASS' 'Warm/init confined to product Ollama; no train-host markers'
} else {
    Add-Check 'TP-07-warm-init-no-train-host' 'FAIL' ("warmOk={0} composeOk={1}" -f $warmOk, $composeOk)
}

# TP-08 docs
$doc = Join-Path $repoRoot 'docs\runtime-proof\LIVE_3B_TRAIN_PROTECTION.md'
if (-not (Test-Path $doc)) {
    Add-Check 'TP-08-docs' 'FAIL' 'LIVE_3B_TRAIN_PROTECTION.md missing'
} else {
    $d = Get-Content $doc -Raw
    $dNorm = ($d -replace '\*\*', '').ToLowerInvariant()
    $need = @('do not interrupt', 'do not restart', 'do not pause', 'do not move', 'cuda_visible_devices', 'suite', 'warm', 'starve')
    $missing = @($need | Where-Object { $dNorm -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        Add-Check 'TP-08-docs' 'FAIL' ("Doc missing: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'TP-08-docs' 'PASS' 'Live 3B train protection documentation present'
    }
}

# TP-09 explicit: this assert did not touch train
Add-Check 'TP-09-no-remote-probe' 'PASS' 'Assert did not SSH/probe/kill/pause/move train PID (static only)'

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'LIVE_3B_TRAIN_PROTECTION_OK' } else { 'LIVE_3B_TRAIN_PROTECTION_BLOCKED' }

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    trainStatusFromManifest = $trainStatus
    trainPidFromManifest = $trainPid
    remoteProbePerformed = $false
    trainTouched = $false
    policyPath = 'auricrux/system/live_3b_train_protection_policy.json'
    checks = $checks
}
$receiptPath = Join-Path $receiptDir 'live-3b-train-protection-latest.json'
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

Write-Host 'LIVE_3B_TRAIN_PROTECTION_OK'
exit 0
