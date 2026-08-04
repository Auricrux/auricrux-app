# Cutover / rollback dry-run drill

**Script:** `scripts/Invoke-CutoverRollbackDryRun.ps1`  
**Token:** `CUTOVER_ROLLBACK_DRILL_OK` / `CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED` / `CUTOVER_ROLLBACK_DRILL_BLOCKED`  
**Receipt:** `docs/runtime-proof/cutover-rollback-drill-latest.json` (+ dated JSON + JSONL append)  
**Constraints:** Never touches live 3B train. Never replaces product model. Never dispatches live cutover.

## What the drill proves

| Proof | How |
|-------|-----|
| Current state detected | Live `/api/health`, `/api/capabilities`, `/api/runtime-truth`; manifest train status recorded |
| Target state prepared | Local stamp refresh, `_publish/web` DLL+corpus+ExpandSearchTerms, cutover workflow safety markers |
| Rollback available | Procedure + precutover baseline + workflow `*-prev-<unix>` rename |
| Failure detectable | 7 failure signals + assert scripts (package-host, truth, promotion, safety gate, clobber, train) |
| Recovery instructions clear | Documented VM rollback steps in procedure + drill receipt |
| Evidence logged | Dated JSON receipt + `cutover-rollback-drill_v1.jsonl` |

## Explicit non-actions

- No `gh workflow run`
- No `gcloud` / SSH to product or train hosts
- No `ollama` mutate of `auricrux-fca`
- No train PID/host contact

## Latest drill result (2026-08-03)

**Verdict:** `CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED`

| Item | Result |
|------|--------|
| Current health | `healthy`, `auricrux-fca` ready, `ollama-live` |
| Package identity on host | **Absent** (WARN — pre-cutover ambiguity) |
| Runtime truth on host | **404** (WARN — package not cut over yet) |
| Live 3B train | `running-do-not-interrupt` recorded; **not touched** |
| Target package | Ready (stamp `1.3.0`, ExpandSearchTerms present) |
| Rollback | Procedure + baseline + prev-rename **PASS** |
| Train protection | `LIVE_3B_TRAIN_PROTECTION_OK` |
| Clobber protection | `PRODUCT_MODEL_CLOBBER_PROTECTION_OK` |
| Package cutover prereqs | **PASS** (authorized workflow path exists; **not executed**) |
| Live action | **Stopped at dry-run** |

### Why live was not executed

1. This drill never executes live cutover by design.  
2. `PROMOTION_EVIDENCE_BLOCKED` — model promote / Release PASS refused (suite still 76.7% FAIL authority; host identity/truth not current).  
3. Package web cutover prereqs **PASS**, but dispatch remains a **separate explicit** operator action (not performed here):  
   `gh workflow run gcp-cutover-build-auricrux.yml -f action=full`  
   (product GCE only; does not replace `auricrux-fca` weights).

### Recovery (if a future live cutover fails)

See `docs/runtime-proof/gguf-suite-live-cutover-procedure-2026-08-03.md`:

1. On **product** VM only: find `auricrux-web-prev-*` / `auricrux-api-prev-*`  
2. Stop/rename failed new containers to `*-failed-<unix>`  
3. Rename prev back to `auricrux-web` / `auricrux-api` and start  
4. Verify `/api/health`  
5. Do not mutate product Ollama tag; do not contact train host/PID  

## Command

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Invoke-CutoverRollbackDryRun.ps1
```
