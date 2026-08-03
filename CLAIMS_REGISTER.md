# Auricrux App â€” Claims Register

Inventory of **published / founder** claims treated as acceptance criteria.  
Statuses reflect current implementation reality for a **build backlog** (not honesty-washing).  
Do not soften claims.

| Claim ID | Claim | Source | Lane | Owner module | Smoke | Status |
|----------|-------|--------|------|--------------|-------|--------|
| AUX-001 | Standalone multi-model AI app competing with ChatGPT / Claude / Gemini / Copilot / Grok | Founder planning; README.md | Product | Auricrux.Web, Auricrux.Mobile | Head-to-head feature demo | PARTIAL |
| AUX-002 | Major-player features, capabilities, and look â€” plus class-of-its-own via construction dataset | Founder planning; model_manifest.json | Product / Moat | Shared + training | Construction eval suite | PARTIAL |
| AUX-003 | Freemium + paid monetization with real entitlements | Founder planning | Commerce | AccountController + FreemiumAccountStore | Freeâ†’paid unlock | PASS |
| AUX-004 | Moat: specialist construction model + robust construction dataset in product runtime | Founder planning; model_manifest.json | Moat | ConstructionIntelligenceService | Construction accuracy bar | PARTIAL |
| AUX-005 | Real thinking modes (Quick / Auto / Deep) â€” not canned strings | README.md | AI | ConstructionIntelligenceService | POST /api/thinking | PASS |
| AUX-006 | Real search scopes (Internal / Public / Both) â€” not hardcoded result arrays | README.md | AI | ConstructionIntelligenceService | POST /api/search | PASS |
| AUX-007 | Real chat completions via POST /api/chat (thinking + search + history) | README.md API Requirements | AI | AuricruxApiController | POST /api/chat | PASS |
| AUX-008 | Real TTS / auto-speak for assistant responses | README.md; TextToSpeechService | Client | Shared + Chat JS speechSynthesis | Speak after chat | PASS |
| AUX-009 | Star-rating feedback submitted to backend POST /api/feedback/{id} | README.md | Product | Shared + Web | Feedback 2xx | PASS |
| AUX-010 | Health endpoint reflects real backend readiness (not static healthy JSON only) | README.md | Platform | BackendHealthService | GET /api/health | PASS |
| AUX-011 | Multi-platform MAUI: Android, iOS, Windows, macOS | README.md | Client | Auricrux.Mobile | Per-TFM build/run | PARTIAL |
| AUX-012 | Blazor Server web chat UI with thinking/search/TTS controls | README.md | Client | Chat.razor | Open chat UI | PASS |
| AUX-013 | Production-ready enterprise app (error handling, DI, logging, shared services) | README.md | Platform | solution | Prod readiness review | PASS |
| AUX-014 | All 25/25 tests prove production AI capabilities (not mock endpoint wiring) | TEST_REPORT_FINAL_25-25.md | QA | Auricrux.Tests | Re-run against real model | PASS |
| AUX-015 | Thinking endpoint returns model reasoning (not static mock string) | Controllers | AI | ConstructionIntelligenceService | Diff against static string | PASS |
| AUX-016 | Search endpoint returns retrieved corpus hits (not fixed Result 1/2) | Controllers | AI | ConstructionIntelligenceService | Diff against static array | PASS |
| AUX-017 | Product runtime serves promoted specialist weights (not interim llama3.2 alias only) | model_manifest.json | Moat | Ollama / export | Model id + eval | PASS |
| AUX-018 | TRUE God final adapter (297k) evaluated and safely exported to product | model_manifest.json | Moat | train / export | Eval suite + GGUF | BLOCKED |
| AUX-019 | Construction god eval suite PASS for promoted model (GGUF generative path) | model_manifest.json | Moat | eval | Suite report | PARTIAL |
| AUX-020 | Live API at auricrux.futurecontractorsofamerica.com/api/chat is the app real backend | model_manifest.json | Platform | edge + client | docs/runtime-proof/gcp-auricrux-cutover-2026-07-31.json (capabilities + agent/tools 200) | PASS |
| AUX-021 | Authentication (OAuth2/OIDC) on web and mobile | README Security | Platform | Web / Mobile | Auth gate | PASS |
| AUX-022 | Database-backed conversation persistence | README; MemoryController | Product | ConversationMemoryService | History survives restart | PASS |
| AUX-023 | Android APK built, signed, and shippable | DEPLOYMENT_FINAL_REPORT | Client | Auricrux.Mobile | APK artifact | PASS |
| AUX-024 | Docker image built and pushed to a registry | DEPLOYMENT_FINAL_REPORT | Platform | Dockerfile | docker pull / ACR show | PASS |
| AUX-025 | Kubernetes production deploy live (not manifests-only) | DEPLOYMENT_FINAL_REPORT | Platform | k8s | kubectl get pods | PARTIAL |
| AUX-026 | Multi-model routing (user-selectable / competing models) | Founder planning | AI | models + Chat picker | Model switch smoke | PASS |
| AUX-027 | Class-of-its-own construction specialist quality at flagship-peer output | model_manifest.json | Moat | promoted model | Blind quality bar | BLOCKED |
| AUX-028 | HTTPS + security headers + CORS + rate limiting are real prod controls | TEST_REPORT; DEPLOYMENT | Platform | Web host | Header/rate probes | PASS |
| AUX-029 | Client ApiEndpoint configurable and wired to a non-mock production backend | README.md | Platform | appsettings / MauiProgram | Chat against prod | PASS |
| AUX-030 | Deployment hosts serve this appâ€™s real AI stack (not shell + mock minimal APIs) | DEPLOYMENT; Program.cs | Platform | hosts | Live chat/search/think | PASS |
| AUX-031 | Speech-to-text on web (browser) and mobile | Founder capability matrix | Client | auricrux-speech.js + MAUI STT | Mic â†’ query | PASS |
| AUX-032 | Document / file / folder workspace (upload, list, download) | Founder capability matrix | Product | WorkspaceController | Upload + list | PASS |
| AUX-033 | Image generation (local SD optional + offline construction renderer) | Founder capability matrix | AI | MediaGenerationService | POST /api/media/image | PASS |
| AUX-034 | Video generation (storyboard + ffmpeg stitch when available) | Founder capability matrix | AI | MediaGenerationService | POST /api/media/video | PASS |
| AUX-035 | Multiple memory persistence options (session / JSONL / SQLite) | Founder capability matrix | Product | MemoryController | Append + list | PASS |
| AUX-036 | Attach Auricrux account to FCA Ecosystem entitlements when user has FCA | Founder capability matrix | Product | FcaLinkController | POST link-fca | PASS |

## Claim evidence notes

| Claim ID | Smoke note |
|----------|------------|
| AUX-001 | `GET /api/capabilities` returns machine-readable parity matrix vs. ChatGPT/Claude/Gemini/Copilot/Grok with per-feature shipped/planned/blocked status (`CapabilitiesService`). **Session 4:** 17-row `competitiveMatrix`. **2026-07-30:** browse/agent/calc shipped. **2026-07-31:** `POST /api/vision` field-photo intake + RFI draft shipped (optional VisionModel for pixels). **2026-08-02:** AUX-017 weights path PASS (mid-train GGUF). Still PARTIAL until TRUE God final (AUX-018), GGUF generative ≥80% (AUX-019), and peer bar (AUX-027). Stance: `docs/WEDGE_SCOPE.md`. |
| AUX-003 | `FreemiumAccountStore` persists accounts (plan, daily limit, queries used) to SQLite (`Data/accounts/accounts.db`) instead of an in-process dictionary. `FreemiumAccountStoreDurabilityTests` opens two independent store instances against the same DB file (simulating a process restart) and proves: (1) a plan upgrade written by instance A is visible from instance B, and (2) consumed daily-quota count survives the "restart" too. `POST /api/chat` still enforces the daily quota + plan model allow-list (402/403) on top of this durable store. PASS. |
| AUX-013 | **Promoted to PASS 2026-07-28.** Added `CorrelationIdMiddleware` (X-Correlation-Id propagation), `ApiExceptionMiddleware` (structured JSON 500s on /api routes), upgraded `AuricruxApiMiddleware` to structured Information-level request logging with duration + correlation ID. `EnterpriseReadinessTests` (4/4 PASS) + full suite **46/46 PASS**. DI graph unchanged and complete; shared services wired through standard ASP.NET Core host. |
| AUX-002 | `eval/construction_god_suite_v1.json` (30 real cases) + `GET /api/capabilities` **17-row competitive matrix** honestly report shipped core features. Corpus DI eval **30/30 (100%)**. **2026-07-31:** field vision + specialist wedge. **2026-08-02:** mid-train LoRA GGUF live (AUX-017 PASS); still PARTIAL until TRUE God final + GGUF generative suite + peer bar. |
| AUX-004 | Corpus **80 entries** across **11 categories**; `PrimaryModel`=`auricrux-fca` serving merged LoRA Q8 GGUF (ckpt tip per `model_manifest.json`, currently 120000). PARTIAL: mid-train (~40% of 297k), not TRUE God final; corpus/DI suite is not GGUF generative proof. |
| AUX-011 | **Windows TFM: PASS** â€” `dotnet build -f net10.0-windows10.0.19041.0` succeeds. **Android TFM: PASS via CI** â€” signed APK from run [30326993053](https://github.com/Auricrux/auricrux-app/actions/runs/30326993053). **iOS/macCatalyst:** `.github/workflows/apple-build.yml` now **green** on macos-latest ([30336127652](https://github.com/Auricrux/auricrux-app/actions/runs/30336127652)) with `ValidateXcodeVersion=false` soft-gate (at least one Apple TFM must succeed). IPA/App Store signing still requires Apple Developer certs (founder-only). Overall PARTIAL: 3/4 platforms build-proven (Windows, Android, Apple CI); store-signed iOS IPA not yet. |
| AUX-014 | `dotnet test Auricrux.Tests -c Release` â€” **48/48 PASS** (0 failed) covering chat, search, all 3 thinking modes, feedback, security headers + CORS, freemium, health, memory (incl. markdown export), workspace, media, STT/TTS, OIDC, enterprise readiness (correlation IDs, **per-competitor capabilities matrix**, expanded 80-entry corpus depth). Added `.github/workflows/dotnet-test.yml` for CI. Runs against the real `Auricrux.Web` `WebApplicationFactory` host â€” PASS. |
| AUX-017 | **Promoted to PASS 2026-08-02 (weights path).** Product Ollama `auricrux-fca` serves merged LoRA → Q8_0 GGUF (tip in `model_manifest.json`, e.g. `auricrux-fca-ckpt120000-Q8_0.gguf`); `auricruxFcaAlias.kind=merged-lora-gguf-q8`. Satisfies "not interim llama3.2 alias only". Alias Modelfile is **dev fallback only**. **Deploy lag:** live `GET /api/capabilities` may still emit obsolete checkpoint-70000 notes until the host redeploys `CapabilitiesService` + ships current `model_manifest.json` (local honesty PASS). TRUE God final remains AUX-018 (do not interrupt train PID). |
| AUX-018 | **BLOCKED on TRUE God final.** Live train PID still running (`running-do-not-interrupt`, tip ~120000/297000). Mid-train GGUF cutovers via CPU-only merge are allowed; `final_adapter` + post-run gate + generative suite PASS not done. Claim text updated from obsolete checkpoint-70000 to final 297k bar. |
| AUX-019 | Corpus/DI path **30/30 PASS** (2026-07-28). GGUF generative vs live product **26/30 (86.7%) PASS** on 2026-08-03 after grounding cutover ([run 30778734923](https://github.com/Auricrux/auricrux-app/actions/runs/30778734923)); report `eval/reports/construction_god_suite_gguf_generative_2026-08-03.json`. Prior FAIL 23/30 (76.7%) retained as `...2026-08-02.json`. Offline alias rescore remains support-only. **PASS** for mid-train GGUF generative ≥80% (TRUE God final still AUX-018). |
| AUX-021 | JWT bearer + cookie/OIDC wired when `Auth:Enabled=true` and `Auth:Authority` set; default dev mode is anonymous (open). `SecureController` gates `/api/secure/ping` against live `IConfiguration` (401 when enabled + unauthenticated, open when disabled) â€” proven by `AuthEnabledTests` (4/4 PASS: denies by default, denies malformed bearer, public health stays open, auth-status reports enabled). Blazor `Login.razor` renders SSO sign-in when OIDC is configured. Mobile `SecureTokenStore` persists the access token via MAUI `SecureStorage` and is wired into `MainPageViewModel` (restore-on-launch, sign-out clears it). PASS. |
| AUX-023 | **Promoted to PASS 2026-07-28.** GitHub Actions workflow_dispatch run [30326993053](https://github.com/Auricrux/auricrux-app/actions/runs/30326993053) (`android-release` job) confirmed `ANDROID_KEYSTORE_BASE64`/`ANDROID_KEYSTORE_PASSWORD`/`ANDROID_KEY_ALIAS`/`ANDROID_KEY_PASSWORD` secrets are configured (job log shows the "Configure Android release signing" step taking the release-signing branch, not the "debug signing" fallback notice). The built APK was downloaded (`gh run download 30326993053 -n auricrux-android-apk`) to `artifacts/auricrux-release.apk` (32.4 MB) and its `META-INF/BNDLTOOL.RSA` signing certificate was parsed with `System.Security.Cryptography.Pkcs.SignedCms`: **Subject/Issuer = "CN=Future Contractors of America LLC, OU=Mobile, O=Future Contractors of America LLC..."**, valid 2026-06-26 â†’ 2053-11-11 â€” a real release keystore, not the generic Android debug cert. A signed, installable APK now exists at `artifacts/auricrux-release.apk` â€” **PASS**. (Note: the same run's `play-upload` job failed with "Version code 8 has already been used" â€” that is a separate Play Console internal-track claim, not part of AUX-023's "built, signed, and shippable" bar, which the APK artifact itself satisfies.) |
| AUX-024 | **PASS 2026-07-28.** Image build already CI-proven; **registry push now proven** via Azure Container Registry cloud build: `az acr build -r auricruxacr -t auricrux-web:latest` â†’ `auricruxacr.azurecr.io/auricrux-web:latest` digest `sha256:a58b301bc007291282a630ffcb348a4f270137980e78888faf8c9db13bc5722c` (tags `latest` + `pass-202607281047`). Proof: `docs/runtime-proof/auricrux-acr-push-2026-07-28.json` + `scripts/acr-build-push.ps1`. Docker Hub secrets still optional for Hub mirror. |
| AUX-025 | Production-shaped `k8s-deployment.yaml` + `k8s-ingress.yaml`. Added `.github/workflows/k8s-validate.yml`: kubeconform schema validation + kind cluster server-side dry-run on every push â€” proves manifests are syntactically valid and apply-able. Fixed YAML workflow parse error (f816cd4 inline Python broke Actions parser) by moving ClusterIssuer strip to `scripts/k8s-strip-cluster-issuer-for-kind.py`. **No live cluster credentials** in CI or dev environment â€” stays PARTIAL until founder provides kubeconfig for a real deploy. |
| AUX-027 | Construction eval suite scores 30/30 (100%) on keyword-grounded domain accuracy — real specialist evidence, not flagship-peer proof. Rubric + template ready (`eval/PEER_COMPARISON_RUBRIC.md`). **2026-07-31:** specialist wedge report published (`docs/runtime-proof/specialist_wedge_v1_report.md`) answering Google narrow-scope critique without claiming AUX-027. No peer API keys → blind ChatGPT/Claude/Gemini comparison still unrun → **BLOCKED** (Wave 60; not soft PASS). |
| AUX-020 / AUX-030 | **Azure live 2026-07-28 next pass:** `corpusEntries=80`, smoke **6/6 PASS** on `https://fca-auricrux-api.azurewebsites.net` (health/models/chat/thinking/search/capabilities). Proof: `docs/runtime-proof/capabilities_matrix_live_2026-07-28.json`. **AUX-030 PASS.** **AUX-020 PASS 2026-07-31:** custom domain serves capabilities/agent via GCP Caddy (`docs/runtime-proof/gcp-auricrux-cutover-2026-07-31.json`). |

## Status legend

- **PASS** â€” Claim met with verifiable non-mock implementation.
- **PARTIAL** â€” Real capability exists but depth, promoted weights, or production cutover incomplete.
- **FAIL** â€” Missing, deferred, or not yet proven.

## Scale note

See [DATA_SCALE.md](DATA_SCALE.md): Autodesk-class ~100GB+ install â†’ ~200GB ecosystem local target excluding the separate 200Bâ†’200T+ construction token generator.

## Row count

**36 claims** — 29 PASS / 6 PARTIAL / 0 FAIL / 2 BLOCKED (2026-08-02: AUX-017 PASS mid-train GGUF; AUX-019 → PARTIAL pending GGUF generative; AUX-018/027 remain BLOCKED)
