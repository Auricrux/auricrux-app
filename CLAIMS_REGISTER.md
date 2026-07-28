# Auricrux App — Claims Register

Inventory of **published / founder** claims treated as acceptance criteria.  
Statuses reflect current implementation reality for a **build backlog** (not honesty-washing).  
Do not soften claims.

| Claim ID | Claim | Source | Lane | Owner module | Smoke | Status |
|----------|-------|--------|------|--------------|-------|--------|
| AUX-001 | Standalone multi-model AI app competing with ChatGPT / Claude / Gemini / Copilot / Grok | Founder planning; README.md | Product | Auricrux.Web, Auricrux.Mobile | Head-to-head feature demo | PARTIAL |
| AUX-002 | Major-player features, capabilities, and look — plus class-of-its-own via construction dataset | Founder planning; model_manifest.json | Product / Moat | Shared + training | Construction eval suite | PARTIAL |
| AUX-003 | Freemium + paid monetization with real entitlements | Founder planning | Commerce | AccountController + Chat | Free→paid unlock | PARTIAL |
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
| AUX-014 | All 25/25 tests prove production AI capabilities (not mock endpoint wiring) | TEST_REPORT_FINAL_25-25.md | QA | Auricrux.Tests | Re-run against real model | PARTIAL |
| AUX-015 | Thinking endpoint returns model reasoning (not static mock string) | Controllers | AI | ConstructionIntelligenceService | Diff against static string | PASS |
| AUX-016 | Search endpoint returns retrieved corpus hits (not fixed Result 1/2) | Controllers | AI | ConstructionIntelligenceService | Diff against static array | PASS |
| AUX-017 | Product runtime serves promoted specialist weights (not interim llama3.2 alias only) | model_manifest.json | Moat | Ollama / export | Model id + eval | FAIL |
| AUX-018 | True-god checkpoint-70000 evaluated and safely exported to product | model_manifest.json | Moat | train / export | Eval suite + GGUF | FAIL |
| AUX-019 | Construction god eval suite PASS for promoted model | model_manifest.json | Moat | eval | Suite report | FAIL |
| AUX-020 | Live API at auricrux.futurecontractorsofamerica.com/api/chat is the app’s real backend | model_manifest.json | Platform | edge + client | Client→edge chat | PARTIAL |
| AUX-021 | Authentication (OAuth2/OIDC) on web and mobile | README Security | Platform | Web / Mobile | Auth gate | PARTIAL |
| AUX-022 | Database-backed conversation persistence | README; MemoryController | Product | ConversationMemoryService | History survives restart | PASS |
| AUX-023 | Android APK built, signed, and shippable | DEPLOYMENT_FINAL_REPORT | Client | Auricrux.Mobile | APK artifact | FAIL |
| AUX-024 | Docker image built and pushed to a registry | DEPLOYMENT_FINAL_REPORT | Platform | Dockerfile | docker pull | FAIL |
| AUX-025 | Kubernetes production deploy live (not manifests-only) | DEPLOYMENT_FINAL_REPORT | Platform | k8s | kubectl get pods | FAIL |
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

## Partial claim smoke notes

| Claim ID | Smoke note |
|----------|------------|
| AUX-003 | `POST /api/chat` with `X-Auricrux-Email` enforces daily quota + plan model allow-list (402/403). Accounts are in-process — no payment processor or durable billing store yet. |
| AUX-014 | `dotnet test Auricrux.Tests` — 6/6 integration tests PASS (health, corpus search, thinking modes, security headers, freemium gates). Original 25-test manual report not re-run against live promoted model — claim stays PARTIAL. |
| AUX-021 | JWT bearer wired when `Auth:Enabled=true` and `Auth:Authority` set; default dev mode is anonymous. Mobile OIDC login UI not shipped. |

## Status legend

- **PASS** — Claim met with verifiable non-mock implementation.
- **PARTIAL** — Real capability exists but depth, promoted weights, or production cutover incomplete.
- **FAIL** — Missing, deferred, or not yet proven.

## Scale note

See [DATA_SCALE.md](DATA_SCALE.md): Autodesk-class ~100GB+ install → ~200GB ecosystem local target excluding the separate 200B→200T+ construction token generator.

## Row count

**36 claims**
