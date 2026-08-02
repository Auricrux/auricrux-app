<#
.SYNOPSIS
  Stage latest Azure TRUE God checkpoint and (optionally) CPU-export to GGUF.
  Never uses the train GPU. Safe while PID is alive.

.EXAMPLE
  .\scripts\stage-and-cpu-export-checkpoint.ps1
  .\scripts\stage-and-cpu-export-checkpoint.ps1 -Step 115000 -Export
#>
[CmdletBinding()]
param(
    [string]$Remote = "azureuser@20.65.32.150",
    [string]$SshKey = "$env:TEMP\auricrux_export_rsa",
    [string]$RunRoot = "/mnt/auricrux-eod/runs/run-20260715T114454Z",
    [string]$OutputDir = "outputs/auricrux_lora_adapter_3b_true_god_1b5",
    [int]$Step = 0,
    [switch]$Export,
    [string]$TrainPidProbe = "1019003"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $SshKey)) {
    throw "SSH key missing: $SshKey (use the ephemeral export key authorized on the train VM)"
}

function Invoke-Remote([string]$Cmd) {
    & ssh -o BatchMode=yes -o ConnectTimeout=30 -i $SshKey $Remote $Cmd
    if ($LASTEXITCODE -ne 0) { throw "remote failed: $Cmd" }
}

Write-Host "Probing train + latest checkpoint..." -ForegroundColor Cyan
$probe = Invoke-Remote "ps -p $TrainPidProbe -o pid= 2>/dev/null || echo GONE; ls -1d $RunRoot/$OutputDir/checkpoint-* 2>/dev/null | sort -V | tail -n 3"
Write-Host $probe

if ($Step -le 0) {
    $latest = Invoke-Remote "ls -1d $RunRoot/$OutputDir/checkpoint-* 2>/dev/null | sort -V | tail -n 1"
    if ($latest -match 'checkpoint-(\d+)$') { $Step = [int]$Matches[1] }
    else { throw "could not detect latest checkpoint" }
}

$src = "$RunRoot/$OutputDir/checkpoint-$Step"
$dst = "/mnt/auricrux-eod/export-staging/checkpoint-$Step"
Write-Host "Staging checkpoint-$Step (file copy only)..." -ForegroundColor Yellow
Invoke-Remote "set -e; test -f $src/adapter_model.safetensors; rm -rf $dst; cp -a $src $dst; ls -lah $dst/adapter_model.safetensors"

if (-not $Export) {
    Write-Host "Staged only. Re-run with -Export to CPU-merge + GGUF (uses system RAM, not GPU)." -ForegroundColor Green
    return
}

$localExport = Join-Path $env:TEMP "export_ckpt${Step}.py"
# Expect caller to have uploaded export script, or reuse pattern under export-staging
Write-Host "Starting CPU export for checkpoint-$Step (nohup)..." -ForegroundColor Yellow
Invoke-Remote "test -f /mnt/auricrux-eod/export-staging/export_ckpt${Step}.py || test -f /mnt/auricrux-eod/export-staging/export_ckpt110000.py; export CUDA_VISIBLE_DEVICES=; nohup /mnt/auricrux-eod/runs/run-20260715T114454Z/training_env/bin/python /mnt/auricrux-eod/export-staging/export_ckpt${Step}.py > /mnt/auricrux-eod/exports/ckpt-${Step}/logs/nohup.out 2>&1 & echo EXPORT_PID=`$!"
Write-Host "Export launched. Tail logs on VM: /mnt/auricrux-eod/exports/ckpt-$Step/logs/" -ForegroundColor Green
