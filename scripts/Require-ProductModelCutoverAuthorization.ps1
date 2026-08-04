<#
.SYNOPSIS
  Gate for any operation that would overwrite/recreate/replace product tag auricrux-fca.
.DESCRIPTION
  Requires AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED=1 and a non-empty
  AURICRUX_PRODUCT_MODEL_CUTOVER_REASON. Writes cutover authorization evidence.
  Exit 0 = PRODUCT_MODEL_CUTOVER_AUTHORIZED; Exit 1 = PRODUCT_MODEL_CLOBBER_BLOCKED.
.PARAMETER Actor
  Script or workflow id requesting authorization.
.PARAMETER Operation
  Intended op (e.g. ollama-rm-create-from-gguf).
.PARAMETER AllowMissingReason
  Do not use for real cutovers. Test-only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Actor,
    [Parameter(Mandatory)][string]$Operation,
    [string]$ObjectName = '',
    [switch]$AllowMissingReason
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$flag = [string]$env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED
$reason = [string]$env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON

$authorized = ($flag -eq '1' -or $flag -eq 'true')
if (-not $authorized) {
    Write-Host 'PRODUCT_MODEL_CLOBBER_BLOCKED: AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED must be 1' -ForegroundColor Red
    Write-Host 'Product tag auricrux-fca cannot be overwritten/recreated without explicit authorized cutover.' -ForegroundColor Red
    exit 1
}
if ([string]::IsNullOrWhiteSpace($reason) -and -not $AllowMissingReason) {
    Write-Host 'PRODUCT_MODEL_CLOBBER_BLOCKED: AURICRUX_PRODUCT_MODEL_CUTOVER_REASON is required evidence' -ForegroundColor Red
    exit 1
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHHmmssZ')
$evidenceDir = Join-Path $repoRoot 'docs\runtime-proof\product-model-cutover-evidence'
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$evidence = [ordered]@{
    schemaVersion = 1
    evidenceId = ("product-model-cutover-auth-{0}" -f $stamp)
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = 'PRODUCT_MODEL_CUTOVER_AUTHORIZED'
    productModelTag = 'auricrux-fca'
    actor = $Actor
    operation = $Operation
    objectName = $ObjectName
    reason = $reason
    policyPath = 'auricrux/system/product_model_clobber_policy.json'
}
$path = Join-Path $evidenceDir ("cutover-auth-{0}.json" -f $stamp)
($evidence | ConvertTo-Json -Depth 5) | Set-Content $path -Encoding UTF8
$latest = Join-Path $evidenceDir 'cutover-auth-latest.json'
Copy-Item -Force $path $latest

Write-Host ("PRODUCT_MODEL_CUTOVER_AUTHORIZED actor={0} op={1}" -f $Actor, $Operation) -ForegroundColor Green
Write-Host ("Evidence: {0}" -f $path)
exit 0
