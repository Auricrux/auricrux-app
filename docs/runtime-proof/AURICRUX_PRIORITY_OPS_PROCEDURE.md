# Auricrux priority ops procedure (repeatable)

**Scope:** Auricrux only. No FCA/Academy/SaaS. No speculative redesign.  
**Rule:** No false PASS. Offline ≠ live. Do not touch live 3B train. Do not clobber `auricrux-fca` without authorized path.

## Priority order (execute in sequence)

### 1. Protect live 3B training job
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-Live3bTrainProtection.ps1
# Expect: LIVE_3B_TRAIN_PROTECTION_OK
```
**Stop if blocked.** Do not kill/pause/move/optimize train PID.

### 2. Prevent product model clobber
```powershell
.\scripts\Assert-ProductModelClobberProtection.ps1
# Expect: PRODUCT_MODEL_CLOBBER_PROTECTION_OK
```
**Stop if blocked.** Do not set `AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED=1` unless intentional weight cutover.

### 3. Prepare refreshed package safely (no host mutate yet)
```powershell
dotnet publish Auricrux.Web\Auricrux.Web.csproj -c Release -o _publish\web
.\scripts\Write-AuricruxPackageStamp.ps1 -PublishDir _publish\web `
  -DeploymentSource gcp-cutover-prepared -HostProfile product-gce -RecipeProfile product_gguf_serve_v1
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes
# Expect: DEPLOYMENT_SAFETY_GATE_OK (offline package validation only)
.\scripts\Invoke-CutoverRollbackDryRun.ps1
# Expect: CUTOVER_ROLLBACK_DRILL_OK or CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED
```
**Live host cutover (operator only, after dry-run):**  
`gh workflow run gcp-cutover-build-auricrux.yml -f action=full`  
Does **not** replace Ollama product weights.

### 4. Rerun live GGUF construction suite
**Blocked until** host has intended package (`PACKAGE_HOST_CONSISTENCY_OK` + preferably `/api/runtime-truth` 200).
```powershell
.\scripts\Assert-PackageHostConsistency.ps1
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
.\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com
```
Authority only if dated live report on product host. Do not treat offline rescore as PASS.

### 5. Update evidence ledger
Automatic on successful suite runner path via `Write-GgufSuiteEvidenceLedger.ps1` (append-only).  
Do **not** rewrite prior FAIL rows. Do **not** append fake PASS.

### 6. Update manifest truthfully
Only after step 4 live report exists and matches claimed score:
- If FAIL: keep FAIL status (current).
- If PASS ≥80%: set `evalStatus` / `ggufGenerative*` to cite **that** dated live report only.
Never promote offline 80% into Manifest PASS.

### 7. Storage and L3 (safe only)
```powershell
cd C:\Users\MichaelBartholomew\source\auricrux-models
.\scripts\Assert-CDriveStorageRisk.ps1   # may BLOCK if C: < 50GB — do not mass-delete
.\scripts\Assert-L3EmptyBehavior.ps1     # empty stubs classified; do not fabricate
```

### 7b. Operational drift (immediate divergence)
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-OperationalDrift.ps1              # local identity drift
.\scripts\Assert-OperationalDrift.ps1 -ProbeLive   # include host (pre-cutover WARN ok)
```
Token `OPERATIONAL_DRIFT_OK` / `_WARN` / `_BLOCKED`. FAIL only on hard mismatches.

### 8. Classify anything still blocked
Update/consult `docs/runtime-proof/AURICRUX_OPERATIONAL_CLOSURE_2026-08-03.md` and this procedure’s latest evidence receipt.

## Latest package-prep evidence
See `docs/runtime-proof/package-prepared-2026-08-03.json`.
