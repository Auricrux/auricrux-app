# Product model clobber protection

**Policy:** `auricrux/system/product_model_clobber_policy.json`  
**Assert:** `scripts/Assert-ProductModelClobberProtection.ps1` → `PRODUCT_MODEL_CLOBBER_PROTECTION_OK`  
**Auth helper:** `scripts/Require-ProductModelCutoverAuthorization.ps1`  
**Related:** [OLLAMA_INIT_SAFE_UNSAFE_PATHS.md](./OLLAMA_INIT_SAFE_UNSAFE_PATHS.md)

## Rule

The system must **refuse** to overwrite, recreate, rename, or replace existing product tag **`auricrux-fca`** unless an **explicit authorized cutover path** is used.

Any script/workflow that can change the product model must:

1. Require explicit authorization (`authorize_product_model_cutover=true` / `AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED=1`)
2. Require a non-empty reason (`cutover_reason` / `AURICRUX_PRODUCT_MODEL_CUTOVER_REASON`)
3. Produce cutover evidence under `docs/runtime-proof/product-model-cutover-evidence/`

Without those, exit with **`PRODUCT_MODEL_CLOBBER_BLOCKED`**.

---

## SAFE (must never mutate `auricrux-fca`)

| Path | Role |
|------|------|
| `docker compose up` (default) | Starts ollama + web only |
| `ollama-model-init` (`--profile dev-fallback`) | Creates **`auricrux-fca-dev-fallback` only** |
| `gcp-warm-auricrux-fca.yml` | Warms existing tag; fails closed if missing |
| `gcp-cutover-build-auricrux.yml` | Rebuilds web image/container |
| `gcp-vm-cutover-deploy.yml` | Web deploy |
| `gcp-vm-fix-auricrux-ollama.yml` | Ops (vision peers); no product recreate |
| `scripts/deploy_azure.ps1` | Azure zip deploy (no Ollama mutate) |
| `Modelfile.auricrux-fca` | DEV FALLBACK ONLY; forbids `ollama create auricrux-fca` |

---

## AUTHORIZED cutover (mutates product tag)

| Path | Ops | Gate |
|------|-----|------|
| `.github/workflows/gcp-load-ckpt110000-gguf.yml` | `ollama rm` + `ollama create auricrux-fca` from GCS GGUF | `authorize_product_model_cutover=true` + `cutover_reason` + evidence artifact |

### Dispatch example

```bash
gh workflow run "GCP Load ckpt-110000 GGUF" \
  -f object_name=auricrux-fca-ckpt120000-Q8_0.gguf \
  -f authorize_product_model_cutover=true \
  -f cutover_reason="Mid-train product load ckpt-120000 Q8 after CPU merge"
```

Dispatch with `authorize_product_model_cutover=false` (default) → **`PRODUCT_MODEL_CLOBBER_BLOCKED`**.

### Local helper (for future local load scripts)

```powershell
$env:AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED = '1'
$env:AURICRUX_PRODUCT_MODEL_CUTOVER_REASON = 'Describe why'
.\scripts\Require-ProductModelCutoverAuthorization.ps1 `
  -Actor 'my-load-script' `
  -Operation 'ollama-rm-create-from-gguf' `
  -ObjectName 'auricrux-fca-ckptXXXXX-Q8_0.gguf'
# Only then perform ollama rm/create
```

---

## BLOCKED / forbidden

| Action | Status |
|--------|--------|
| Warm recreating from Modelfile / llama3.2 | BLOCKED |
| Compose/init `ollama create auricrux-fca` | BLOCKED |
| Manual `ollama create auricrux-fca -f Modelfile.auricrux-fca` | FORBIDDEN |
| Load workflow without authorize input | `PRODUCT_MODEL_CLOBBER_BLOCKED` |
| Load workflow without cutover_reason | `PRODUCT_MODEL_CLOBBER_BLOCKED` |
| Silent rename/replace of product tag | BLOCKED |

---

## Operator checks

```powershell
.\scripts\Assert-ProductModelClobberProtection.ps1
.\scripts\Assert-OllamaInitSafety.ps1
dotnet test Auricrux.Tests -c Release --filter FullyQualifiedName~ProductModelClobber
```
