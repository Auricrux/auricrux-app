# Auricrux web cutover for GGUF suite live validation (2026-08-02/03)

**Objective:** Deploy grounding + `ExpandSearchTerms` + silica corpus to product host, then run dated GGUF generative suite.  
**Product host:** `https://auricrux.futurecontractorsofamerica.com` (GCP VM `instance-20260715-113528`, Caddy → `:5001` API / `:4000` UI)  
**Workflow:** `.github/workflows/gcp-cutover-build-auricrux.yml` (`action=full`)  
**Do not:** interrupt 3B train PID; claim PASS from offline alias rescore alone.

## Pre-flight verification (done locally)

| Check | Result |
|-------|--------|
| **Safety gate** | `scripts/Assert-GgufSuiteDeploymentSafetyGate.ps1` → `DEPLOYMENT_SAFETY_GATE_OK` (required before live suite) |
| `_publish/web/Auricrux.Web.dll` contains `ExpandSearchTerms` | PASS (ASCII) |
| DLL contains grounding prompt (`Grounding excerpts`, `Prefer facts…`) | PASS (UTF-16LE) |
| `_publish/web/Data/construction-corpus.json` silica + `respiratory` | PASS |
| Publish complete (`dll/exe/web.config/appsettings/corpus`) | PASS (~381 files) |
| Product path | GCP Docker cutover from **source** (Dockerfile), not zip of `_publish` |
| Azure `_publish` zip path | Secondary (`scripts/deploy_azure.ps1`) — not used for this suite authority |

## Rollback ability (preserve before swap)

Cutover script **renames** running containers before starting new ones:

- `auricrux-web` → `auricrux-web-prev-<unix>`
- `auricrux-api` → `auricrux-api-prev-<unix>`

**Rollback (on VM):**

```bash
# Identify previous containers
sudo docker ps -a --filter name=auricrux-web-prev --format '{{.Names}} {{.CreatedAt}}'
sudo docker ps -a --filter name=auricrux-api-prev --format '{{.Names}} {{.CreatedAt}}'

# Stop failed new, restore previous (example names)
sudo docker stop auricrux-web auricrux-api || true
sudo docker rename auricrux-web auricrux-web-failed-$(date +%s) || true
sudo docker rename auricrux-api auricrux-api-failed-$(date +%s) || true
PREV_WEB=$(sudo docker ps -a --filter name=auricrux-web-prev --format '{{.Names}}' | head -1)
PREV_API=$(sudo docker ps -a --filter name=auricrux-api-prev --format '{{.Names}}' | head -1)
sudo docker rename "$PREV_WEB" auricrux-web
sudo docker rename "$PREV_API" auricrux-api
sudo docker start auricrux-web auricrux-api
curl -fsS http://127.0.0.1:5001/api/health
```

Ollama / `auricrux-fca` GGUF tag is **not** replaced by this cutover (warm workflow no longer Modelfile-recreates).

## Deploy steps

1. Push Auricrux grounding/retrieval + suite tooling commits to `origin/main` (no unrelated Mobile/FCA churn).
2. `gh workflow run gcp-cutover-build-auricrux.yml -f action=full`
3. Wait for green job; capture run id into runtime-proof JSON.
4. Public smoke: `/api/health`, `/api/capabilities` (expect updated constructionMoat notes if CapabilitiesService shipped).
5. Silica probe chat — expect silica/respirable language from grounded excerpts.
6. **Safety gate (hard):**
   ```powershell
   .\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
   # Must print DEPLOYMENT_SAFETY_GATE_OK; abort on FAIL
   ```
7. Run suite (gate enforced automatically):
   ```powershell
   .\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com
   ```
8. Update `model_manifest.json` **only if** dated live report `passRatePercent >= 80` and `suitePassed=true`.

## Evidence artifacts

- Suite report: `eval/reports/construction_god_suite_gguf_generative_YYYY-MM-DD.json`
- Cutover proof: `docs/runtime-proof/gguf-grounding-cutover-YYYY-MM-DD.json`
- Offline support only: `eval/reports/construction_god_suite_gguf_generative_2026-08-02_alias_rescore.json`
