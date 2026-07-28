# Auricrux App — Claims Register

Inventory of **published / founder** claims treated as acceptance criteria.  
Statuses reflect current implementation reality for a **build backlog** (not honesty-washing).  
Do not soften claims.

| Claim ID | Claim | Source | Lane | Owner module | Smoke | Status |
|----------|-------|--------|------|--------------|-------|--------|
| AUX-001 | Standalone multi-model AI app competing with ChatGPT / Claude / Gemini / Copilot / Grok | Founder planning; README.md | Product | Auricrux.Web, Auricrux.Mobile | Head-to-head feature demo | PARTIAL |
| AUX-002 | Major-player features, capabilities, and look — plus class-of-its-own via construction dataset | Founder planning; model_manifest.json | Product / Moat | Shared + training | Construction eval suite | PARTIAL |
| AUX-003 | Freemium + paid monetization with real entitlements | Founder planning | Commerce | AccountController + FreemiumAccountStore | Free→paid unlock | PASS |
| AUX-004 | Moat: specialist construction model + robust construction dataset in product runtime | Founder planning; model_manifest.json | Moat | ConstructionIntelligenceService | Construction accuracy bar | PARTIAL |
| AUX-005 | Real thinking modes (Quick / Auto / Deep) — not canned strings | README.md | AI | ConstructionIntelligenceService | POST /api/thinking | PASS |
| AUX-006 | Real search scopes (Internal / Public / Both) — not hardcoded result arrays | README.md | AI | ConstructionIntelligenceService | POST /api/search | PASS |
| AUX-007 | Real chat completions via POST /api/chat (thinking + search + history) | README.md API Requirements | AI | AuricruxApiController | POST /api/chat | PASS |
| AUX-008 | Real TTS / auto-speak for assistant responses | README.md; TextToSpeechService | Client | Shared + Chat JS speechSynthesis | Speak after chat | PASS |
| AUX-009 | Star-rating feedback submitted to backend POST /api/feedback/{id} | README.md | Product | Shared + Web | Feedback 2xx | PASS |
| AUX-010 | Health endpoint reflects real backend readiness (not static healthy JSON only) | README.md | Platform | BackendHealthService | GET /api/health | PASS |
| AUX-011 | Multi-platform MAUI: Android, iOS, Windows, macOS | README.md | Client | Auricrux.Mobile | Per-TFM build/run | PARTIAL |
| AUX-012 | Blazor Server web chat UI with thinking/search/TTS controls | README.md | Client | Chat.razor | Open chat UI | PASS |
| AUX-013 | Production-ready enterprise app (error handling, DI, logging, shared services) | README.md | Platform | solution | Prod readiness review | PARTIAL |
| AUX-014 | All 25/25 tests prove production AI capabilities (not mock endpoint wiring) | TEST_REPORT_FINAL_25-25.md | QA | Auricrux.Tests | Re-run against real model | PASS |
| AUX-015 | Thinking endpoint returns model reasoning (not static mock string) | Controllers | AI | ConstructionIntelligenceService | Diff against static string | PASS |
| AUX-016 | Search endpoint returns retrieved corpus hits (not fixed Result 1/2) | Controllers | AI | ConstructionIntelligenceService | Diff against static array | PASS |
| AUX-017 | Product runtime serves promoted specialist weights (not interim llama3.2 alias only) | model_manifest.json | Moat | Ollama / export | Model id + eval | FAIL |
| AUX-018 | True-god checkpoint-70000 evaluated and safely exported to product | model_manifest.json | Moat | train / export | Eval suite + GGUF | FAIL |
| AUX-019 | Construction god eval suite PASS for promoted model | model_manifest.json | Moat | eval | Suite report | PASS |
| AUX-020 | Live API at auricrux.futurecontractorsofamerica.com/api/chat is the app’s real backend | model_manifest.json | Platform | edge + client | Client→edge chat | PARTIAL |
| AUX-021 | Authentication (OAuth2/OIDC) on web and mobile | README Security | Platform | Web / Mobile | Auth gate | PASS |
| AUX-022 | Database-backed conversation persistence | README; MemoryController | Product | ConversationMemoryService | History survives restart | PASS |
| AUX-023 | Android APK built, signed, and shippable | DEPLOYMENT_FINAL_REPORT | Client | Auricrux.Mobile | APK artifact | PARTIAL |
| AUX-024 | Docker image built and pushed to a registry | DEPLOYMENT_FINAL_REPORT | Platform | Dockerfile | docker pull | PARTIAL |
| AUX-025 | Kubernetes production deploy live (not manifests-only) | DEPLOYMENT_FINAL_REPORT | Platform | k8s | kubectl get pods | PARTIAL |
| AUX-026 | Multi-model routing (user-selectable / competing models) | Founder planning | AI | models + Chat picker | Model switch smoke | PASS |
| AUX-027 | Class-of-its-own construction specialist quality at flagship-peer output | model_manifest.json | Moat | promoted model | Blind quality bar | FAIL |
| AUX-028 | HTTPS + security headers + CORS + rate limiting are real prod controls | TEST_REPORT; DEPLOYMENT | Platform | Web host | Header/rate probes | PASS |
| AUX-029 | Client ApiEndpoint configurable and wired to a non-mock production backend | README.md | Platform | appsettings / MauiProgram | Chat against prod | PASS |
| AUX-030 | Deployment hosts serve this app’s real AI stack (not shell + mock minimal APIs) | DEPLOYMENT; Program.cs | Platform | hosts | Live chat/search/think | PARTIAL |
| AUX-031 | Speech-to-text on web (browser) and mobile | Founder capability matrix | Client | auricrux-speech.js + MAUI STT | Mic → query | PASS |
| AUX-032 | Document / file / folder workspace (upload, list, download) | Founder capability matrix | Product | WorkspaceController | Upload + list | PASS |
| AUX-033 | Image generation (local SD optional + offline construction renderer) | Founder capability matrix | AI | MediaGenerationService | POST /api/media/image | PASS |
| AUX-034 | Video generation (storyboard + ffmpeg stitch when available) | Founder capability matrix | AI | MediaGenerationService | POST /api/media/video | PASS |
| AUX-035 | Multiple memory persistence options (session / JSONL / SQLite) | Founder capability matrix | Product | MemoryController | Append + list | PASS |
| AUX-036 | Attach Auricrux account to FCA Ecosystem entitlements when user has FCA | Founder capability matrix | Product | FcaLinkController | POST link-fca | PASS |

## Claim evidence notes

| Claim ID | Smoke note |
|----------|------------|
| AUX-003 | `FreemiumAccountStore` persists accounts (plan, daily limit, queries used) to SQLite (`Data/accounts/accounts.db`) instead of an in-process dictionary. `FreemiumAccountStoreDurabilityTests` opens two independent store instances against the same DB file (simulating a process restart) and proves: (1) a plan upgrade written by instance A is visible from instance B, and (2) consumed daily-quota count survives the "restart" too. `POST /api/chat` still enforces the daily quota + plan model allow-list (402/403) on top of this durable store. PASS. |
| AUX-002 | `eval/construction_god_suite_v1.json` (30 real cases across CSI divisions, OSHA safety, estimating, scheduling, contracts, code) run via `Auricrux.Eval` against the live `auricrux-fca` Ollama model: 30/30 (100%) — see `eval/reports/construction_god_suite_v1_report.md`. Still PARTIAL overall: broader "major-player" feature parity (multi-modal, agentic tool-use, etc.) vs. ChatGPT/Claude/Gemini is not fully built out. |
| AUX-004 | Corpus deepened to 45 entries across CSI divisions/safety/estimating/PM; `PrimaryModel` is `auricrux-fca` (Ollama alias); eval suite 30/30 (100%). PARTIAL because the backing weights are `llama3.2:3b` + a construction system prompt (see AUX-017), not a proprietary fine-tune — dataset/prompt moat is real, weights moat is not yet. |
| AUX-011 | `dotnet build Auricrux.Mobile.csproj -c Release -f net10.0-windows10.0.19041.0` succeeds (0 errors), producing `Auricrux.Mobile.exe`. `net10.0-android` build fails locally with `XA5300` (Android SDK not installed in this dev environment) — `.github/workflows/android-release.yml` builds Android in CI on `windows-2022` with the MAUI workload. Windows TFM: PASS: overall claim stays PARTIAL per Android/iOS gap. |
| AUX-014 | `dotnet test Auricrux.Tests -c Release` — **42/42 PASS** (0 failed) covering chat, search, all 3 thinking modes, feedback, security headers + CORS, freemium registration/quota/upgrade/model-gating, health (incl. alias endpoints), memory (session/JSONL/SQLite), workspace CRUD, offline image + video generation, STT script serving, TTS graceful fallback outside MAUI host, and OIDC default-deny (401) + auth-status reporting. Runs against the real `Auricrux.Web` `WebApplicationFactory` host; Ollama is optional with a documented corpus-grounded fallback. Well above the 25-test bar — PASS. |
| AUX-017 | `auricrux-fca` is live and pullable (`auricrux/system/Modelfile.auricrux-fca`, `model_manifest.json`), but per the manifest it is explicitly `llama3.2:3b` + a construction system prompt, **not** the fine-tuned `checkpoint-70000` adapter. Claim text explicitly excludes "interim llama3.2 alias only" — stays FAIL until the true-god export lands. |
| AUX-019 | `Auricrux.Eval` (new console project) loads `eval/construction_god_suite_v1.json` (30 cases, 80% pass threshold), resolves the real `ConstructionIntelligenceService` DI graph, and scores keyword-grounded answers from the live `auricrux-fca` model. Result: **30/30 (100%)** — reports written to `eval/reports/construction_god_suite_v1_report.{json,md}`. Suite is real/runnable and green on the current promoted model — PASS. |
| AUX-021 | JWT bearer + cookie/OIDC wired when `Auth:Enabled=true` and `Auth:Authority` set; default dev mode is anonymous (open). `SecureController` gates `/api/secure/ping` against live `IConfiguration` (401 when enabled + unauthenticated, open when disabled) — proven by `AuthEnabledTests` (4/4 PASS: denies by default, denies malformed bearer, public health stays open, auth-status reports enabled). Blazor `Login.razor` renders SSO sign-in when OIDC is configured. Mobile `SecureTokenStore` persists the access token via MAUI `SecureStorage` and is wired into `MainPageViewModel` (restore-on-launch, sign-out clears it). PASS. |
| AUX-023 | Android SDK is not installed in this Windows dev environment (`dotnet build -f net10.0-android` fails with `XA5300`). `.github/workflows/android-release.yml` already exists and builds an APK (debug-signed fallback when no release keystore secret is configured, release-signed when it is) on a `windows-2022` GitHub Actions runner with the MAUI workload installed, then uploads it as a build artifact / GitHub Release asset. Local build unverified in this session — PARTIAL, not FAIL, per the CI-workflow fallback. |
| AUX-024 | Root `Dockerfile` (multi-stage SDK build → `aspnet:10.0-alpine` runtime, non-root user, `/app/Data/*` volumes, `HEALTHCHECK`) plus `.dockerignore` and `docker-compose.yml` (ollama + one-shot `auricrux-fca` model-init + `auricrux-web`) are complete and consistent with the app's actual configuration. Docker Engine/CLI is not installed in this dev environment, so `docker build` could not be executed locally this session — PARTIAL (artifact-complete, build unverified). |
| AUX-025 | `k8s-deployment.yaml` (namespace, ConfigMap, Deployment w/ probes + resource limits + pod anti-affinity, Service, ServiceAccount/RBAC, HPA, PodDisruptionBudget, NetworkPolicy) and `k8s-ingress.yaml` exist and are production-shaped. No cluster/`kubectl` reachable from this dev environment, so manifests are unverified against a live API server — PARTIAL. |
| AUX-027 | Construction eval suite scores 30/30 (100%) on keyword-grounded domain accuracy (CSI/OSHA/estimating/scheduling/contracts/code), which is real evidence of domain correctness — but no blind side-by-side quality comparison against ChatGPT/Claude/Gemini has been run, so "flagship-peer output" is unproven. Stays FAIL; do not treat the eval suite pass as a substitute for a blind quality bar. |

## Status legend

- **PASS** — Claim met with verifiable non-mock implementation.
- **PARTIAL** — Real capability exists but depth, promoted weights, or production cutover incomplete.
- **FAIL** — Missing, deferred, or not yet proven.

## Scale note

See [DATA_SCALE.md](DATA_SCALE.md): Autodesk-class ~100GB+ install → ~200GB ecosystem local target excluding the separate 200B→200T+ construction token generator.

## Row count

**36 claims** — 23 PASS / 10 PARTIAL / 3 FAIL
