#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publish Auricrux.Web and zip-deploy to Azure App Service fca-auricrux-api.
#>
param(
    [string]$WebAppName = "fca-auricrux-api",
    [string]$ResourceGroup = "Auricrux_group",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "_publish\web"
$zipPath = Join-Path $root "_publish\web.zip"

Write-Host "Publishing Auricrux.Web ($Configuration)..." -ForegroundColor Cyan
dotnet publish (Join-Path $root "Auricrux.Web\Auricrux.Web.csproj") -c $Configuration -o $publishDir

Write-Host "Creating deployment zip..." -ForegroundColor Cyan
# Ensure construction corpus is present in the published output (80 grounded entries).
$corpusPub = Join-Path $publishDir "Data\construction-corpus.json"
$corpusSrc = Join-Path $root "Auricrux.Web\Data\construction-corpus.json"
if (Test-Path $corpusSrc) {
    New-Item -ItemType Directory -Force -Path (Split-Path $corpusPub) | Out-Null
    Copy-Item -Force $corpusSrc $corpusPub
}
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Push-Location $publishDir
# tar produces forward-slash zip entries; Compress-Archive can leave stale/odd paths on Windows.
tar -a -cf $zipPath *
Pop-Location
Write-Host "Deploying to $WebAppName..." -ForegroundColor Cyan
az webapp deploy --resource-group $ResourceGroup --name $WebAppName --src-path $zipPath --type zip

Write-Host "Running smoke test..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "smoke_prod.ps1") -BaseUrl "https://$WebAppName.azurewebsites.net"

Write-Host "Deploy complete." -ForegroundColor Green
