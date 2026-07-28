#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Bind auricrux.futurecontractorsofamerica.com to fca-auricrux-api (AUX-020 prep).

.DESCRIPTION
  Adds the custom hostname binding in Azure App Service. DNS at Porkbun must already
  CNAME auricrux -> fca-auricrux-api.azurewebsites.net before managed TLS will validate.
  See deployment-packages/CUSTOM_DOMAIN_SETUP.md for full instructions.
#>
param(
    [string]$WebAppName = "fca-auricrux-api",
    [string]$ResourceGroup = "Auricrux_group",
    [string]$Hostname = "auricrux.futurecontractorsofamerica.com"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking Azure login..." -ForegroundColor Cyan
az account show -o none

Write-Host "Current hostname bindings:" -ForegroundColor Cyan
az webapp config hostname list --webapp-name $WebAppName --resource-group $ResourceGroup -o table

$existing = az webapp config hostname list --webapp-name $WebAppName --resource-group $ResourceGroup --query "[?name=='$Hostname'].name" -o tsv
if ($existing) {
    Write-Host "Hostname $Hostname already bound." -ForegroundColor Yellow
} else {
    Write-Host "Adding hostname binding for $Hostname..." -ForegroundColor Cyan
    az webapp config hostname add --webapp-name $WebAppName --resource-group $ResourceGroup --hostname $Hostname
}

Write-Host ""
Write-Host "Next steps (founder):" -ForegroundColor Yellow
Write-Host "  1. Ensure Porkbun CNAME: auricrux -> $WebAppName.azurewebsites.net"
Write-Host "  2. Create managed cert: az webapp config ssl create --name $WebAppName --resource-group $ResourceGroup --hostname $Hostname --validation-method CNAME"
Write-Host "  3. Smoke test: ./scripts/smoke_prod.ps1 -BaseUrl https://$Hostname"
