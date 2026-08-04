# Ollama init: safe vs unsafe paths

**Assert:** `scripts/Assert-OllamaInitSafety.ps1` → `OLLAMA_INIT_SAFETY_OK`  
**Also covered by:** deployment safety gate SG-08 / SG-09 / SG-10  
**Related:** [GGUF_DEPLOYMENT_SAFETY_GATE.md](./GGUF_DEPLOYMENT_SAFETY_GATE.md), [AURICRUX_EVIDENCE_RULES.md](./AURICRUX_EVIDENCE_RULES.md)

## Rule

Fallback behavior requires an **explicit** Compose profile:

```bash
docker compose --profile dev-fallback up ollama-model-init
```

Default startup (`docker compose up`, cutover rebuild, warm) must **not**:

- pull `llama3.2`
- recreate `auricrux-fca`
- overwrite an existing product model
- clobber a product Modelfile/GGUF tag
- silently substitute fallback behavior

---

## SAFE paths

| Path | What it does | What it does **not** do |
|------|----------------|-------------------------|
| `docker compose up` (default) | Starts `ollama` + `auricrux-web` | No pull, no create, no Modelfile, no `ollama-model-init` |
| GCP cutover build (`gcp-cutover-build-auricrux.yml`) | Rebuilds/restarts web container | Does not run model-init; does not pull llama3.2; does not `ollama create auricrux-fca` |
| GCP warm (`gcp-warm-auricrux-fca.yml`) | Runs a generate against **existing** `auricrux-fca` | Fails closed if tag missing; no Modelfile recreate; no llama3.2 pull |
| Product GGUF load (GCS load workflow) | Explicit authorized product weight cutover | Requires `authorize_product_model_cutover=true` + `cutover_reason` + evidence |
| `Auricrux__PrimaryModel=auricrux-fca` (compose default) | Expects product GGUF tag | Does not auto-point at `auricrux-fca-dev-fallback` |

---

## UNSAFE / explicit-only paths

| Path | Risk | Required explicit action |
|------|------|--------------------------|
| `ollama-model-init` | Pulls `llama3.2:3b` and creates a Modelfile alias | `--profile dev-fallback` |
| Manual `ollama create auricrux-fca -f Modelfile.auricrux-fca` | **Clobbers product GGUF tag** | **Forbidden.** Modelfile and assert ban this. |
| Manual `ollama create auricrux-fca-dev-fallback -f Modelfile.auricrux-fca` | Local alias only | Allowed for local/dev; set `Auricrux__PrimaryModel=auricrux-fca-dev-fallback` deliberately |
| Running `--profile dev-fallback` on a product host | Can add llama3.2 + fallback tag beside product | Do not use on GCE product VM; use GCS load for product weights |

### Hardened fallback (current compose)

Even with `--profile dev-fallback`, init creates **`auricrux-fca-dev-fallback` only**. It does **not** run `ollama create auricrux-fca`, so an existing product tag is left untouched.

---

Related clobber policy (product tag overwrite): [PRODUCT_MODEL_CLOBBER_PROTECTION.md](./PRODUCT_MODEL_CLOBBER_PROTECTION.md).

## Operator checks

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app

# Focused init safety
.\scripts\Assert-OllamaInitSafety.ps1

# Included in broader deploy gate
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes

# Unit tests (parses compose/Modelfile/workflows)
dotnet test Auricrux.Tests -c Release --filter FullyQualifiedName~OllamaInitSafetyTests
```

---

## Forbidden claim language

| Forbidden | Required instead |
|-----------|------------------|
| “Compose up installs the model” | “Default compose starts Ollama; product GGUF is loaded via GCS cutover” |
| “Warm recreates auricrux-fca” | “Warm only generates against an existing product tag” |
| “Fallback runs automatically if GGUF missing” | “Missing product tag fails closed; fallback requires `--profile dev-fallback`” |
