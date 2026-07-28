# Build and push Auricrux.Web to Azure Container Registry (no local Docker required).
# Usage: pwsh -File scripts/acr-build-push.ps1 [-Registry auricruxacr]
param(
    [string]$Registry = 'auricruxacr',
    [string]$Repository = 'auricrux-web',
    [string]$Tag = $(Get-Date -Format 'yyyyMMddHHmm')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host "==> az acr build -r $Registry -t ${Repository}:latest -t ${Repository}:pass-$Tag"
    az acr build -r $Registry `
        -t "${Repository}:latest" `
        -t "${Repository}:pass-$Tag" `
        -f Dockerfile .
    $meta = az acr repository show -n $Registry --image "${Repository}:latest" -o json | ConvertFrom-Json
    Write-Host "==> Pushed $($meta.name) digest=$($meta.digest)"
}
finally {
    Pop-Location
}
