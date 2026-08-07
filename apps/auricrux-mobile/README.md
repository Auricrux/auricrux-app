# Auricrux Mobile

Installable mobile app for Auricrux as a construction expert copilot with text chat, optional voice capture, and spoken responses.

## Features

- Copilot-style guided prompts by expert mode:
   - Executive
   - Estimator
   - Field Ops
   - Safety
   - Academy
- Chat with `POST /api/auricrux`
- Feedback loop with `rating: up|down`
- Spoken playback of Auricrux responses (Expo Speech)
- Push-to-talk hook (requires speech recognition package install)
- Endpoint profile switch:
   - Azure (current backend)
   - Google/Firebase-ready URL input

## Local Setup

1. Open this folder: `apps/auricrux-mobile`
2. Install dependencies:
   - `npm install`
3. Start Expo:
   - `npx expo start`
4. Open on phone using Expo Go (scan QR code).

## Backend URL

Set the API base URL in the app header field.

Examples:

- Local Functions host from Android emulator: `http://10.0.2.2:7071`
- Local Functions host from iOS simulator: `http://127.0.0.1:7071`
- Physical phone on same LAN: `http://<your-computer-lan-ip>:7071`
- Public endpoint: `https://<your-app>.azurewebsites.net`
- Google Cloud Run: `https://<service>-<hash>-uc.a.run.app`
- Firebase HTTPS Function: `https://us-central1-<project>.cloudfunctions.net/auricrux`

## Packaging Strategy (Sellable Add-On)

Recommended package tiers for rollout:

1. Core Copilot
   - Text chat + expert mode prompts + feedback learning capture
2. Voice Copilot
   - Includes spoken responses and push-to-talk
3. Training Boost
   - Adds Academy mode playbooks and specialist route context defaults

This lets you sell Auricrux as a premium training/performance add-on without forcing all customers into the same feature footprint.

## Firebase/Google Option

Yes, this can be pushed to Google stack while keeping the same app UX.

Practical low-risk path:

1. Keep Azure API as primary production lane now.
2. Mirror the same `POST /api/auricrux` contract behind Google Cloud Run or Firebase Functions.
3. Use the in-app endpoint switch to validate parity before any cutover.
4. Cut traffic gradually by tenant or package tier.

This avoids a risky all-at-once migration and gives you a clear multi-cloud sales story.

## Build Downloadable App Binaries

Use EAS Build to produce installable artifacts:

1. `npm install -g eas-cli`
2. `eas login`
3. `eas build:configure`
4. Android APK (downloadable):
   - `eas build -p android --profile preview`
5. iOS IPA/TestFlight:
   - `eas build -p ios --profile preview`

The build dashboard provides download links for the generated binaries.

## Push Out Now (Google + Downloadable App)

From this folder:

1. `powershell -ExecutionPolicy Bypass -File ./scripts/release-now.ps1`

This runs:

1. `npm.cmd install`
2. EAS Android preview APK build (downloadable)
3. Expo web export
4. Firebase Hosting deploy to `live`

Prerequisites already expected on machine/account:

- Expo account authenticated (`eas login`)
- Firebase account authenticated (`firebase login`)
- Firebase project `auricrux-mobile-prod` created and access granted

If you prefer running each command manually:

1. `npm.cmd install`
2. `npx eas-cli build -p android --profile preview --non-interactive`
3. `npx expo export --platform web`
4. `npx firebase-tools hosting:channel:deploy live --project auricrux-mobile-prod`

## No-Local-Terminal Approach (Recommended)

Use the repository workflow at .github/workflows/auricrux-mobile-release.yml.

One-time setup in GitHub repository settings:

1. Generate an Expo token for EAS build
2. Generate a Firebase token for hosting deploy (only if deploying Firebase)

Run flow:

1. Open GitHub Actions
2. Select Auricrux Mobile Release
3. Click Run workflow
4. Set deploy_firebase to true if you want Firebase publish in same run
5. Paste expo_token input
6. If deploy_firebase is true, paste firebase_token input

Outputs:

1. Android APK build details link in workflow summary
2. Firebase URL in workflow summary when deploy_firebase is true

This avoids local shell instability and lets you ship directly from GitHub cloud runners.
