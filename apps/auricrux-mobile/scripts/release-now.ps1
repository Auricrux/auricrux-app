$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "[1/4] Installing dependencies..."
npm.cmd install

Write-Host "[2/4] Building downloadable Android binary (EAS preview APK)..."
npx eas-cli build -p android --profile preview --non-interactive

Write-Host "[3/4] Exporting web build for Firebase Hosting..."
npx expo export --platform web

Write-Host "[4/4] Deploying to Firebase Hosting channel 'live'..."
npx firebase-tools hosting:channel:deploy live --project auricrux-mobile-prod

Write-Host "Release command sequence finished."
