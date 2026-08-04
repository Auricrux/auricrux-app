# Auricrux package-web cutover — GO / NO-GO checklist

**Purpose:** Decide whether an operator may dispatch **package web cutover only**.  
**Does not:** replace `auricrux-fca` weights · touch 3B train · claim Manifest/suite PASS.  
**Cutover was NOT performed by the prep pass that generated this checklist.**

**Repo:** `C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app`  
**Host:** `https://auricrux.futurecontractorsofamerica.com`  
**Workflow:** `.github/workflows/gcp-cutover-build-auricrux.yml` (`action=full`)  
**Full execution package (RB-C2):** `docs/runtime-proof/RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md`  
**Orchestrator:** `scripts/Invoke-RbC2PackageWebCutoverPackage.ps1` (never auto-dispatches)  
**Offline dry-run simulation:** `scripts/Invoke-RbC2CutoverDryRunSimulation.ps1` → `RB_C2_CUTOVER_DRYRUN_SIMULATION.md`

---

## How to use

1. Open PowerShell.
2. `cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app`
3. Run every **Verify** command in Section A.
4. Mark each row GO or NO-GO from the **Pass token / Pass condition**.
5. **Dispatch cutover only if Section A is all GO** and Section B acknowledgements are checked.
6. After cutover, run Section C (post-cutover). Do not skip.

**Rule:** Any Section A NO-GO = do not dispatch.

---

## Section A — Pre-dispatch (must all be GO)

| ID | Check | Verify command | Pass condition | Prep result (2026-08-03) |
|----|-------|----------------|----------------|--------------------------|
| A1 | Deployment package present | `Test-Path _publish\web\Auricrux.Web.dll; Test-Path _publish\web\Data\construction-corpus.json; Test-Path _publish\web\auricrux\system\package_stamp.json` | All `True` | **GO** |
| A2 | Package contains cutover capabilities | After publish: DLL must contain ASCII `ExpandSearchTerms`, `RuntimeTruth`, `packageIdentity` (or re-run offline gate) | Offline gate PASS SG-04 + stamp present | **GO** |
| A3 | Offline safety gate | `.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes` | Prints `DEPLOYMENT_SAFETY_GATE_OK` exit 0 | **GO** |
| A4 | Package stamp | `Get-Content _publish\web\auricrux\system\package_stamp.json` | `packageVersion`, `buildTimestampUtc`, `suiteTarget=construction_god_suite_v1`, `hostProfile`, `recipeProfile` set | **GO** (1.3.0 / product-gce / product_gguf_serve_v1) |
| A5 | Rollback procedure | `Select-String -Path docs\runtime-proof\gguf-suite-live-cutover-procedure-2026-08-03.md -Pattern 'auricrux-web-prev','/api/health'` | Both patterns found | **GO** |
| A6 | Precutover baseline | `Test-Path docs\runtime-proof\gguf-grounding-precutover-baseline-2026-08-03.json` | `True` | **GO** |
| A7 | Cutover workflow safe | `Select-String -Path .github\workflows\gcp-cutover-build-auricrux.yml -Pattern 'PrimaryModel=auricrux-fca','prev-\$\(date','LIVE 3B TRAIN PROTECTION'` | All three found; workflow must **not** `ollama create/rm auricrux-fca` | **GO** |
| A8 | Dry-run drill | `.\scripts\Invoke-CutoverRollbackDryRun.ps1` | `CUTOVER_ROLLBACK_DRILL_OK` or `CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED`; `DR-07-package-cutover-prereqs` PASS; **no** workflow dispatched | **GO** |
| A9 | Train protection | `.\scripts\Assert-Live3bTrainProtection.ps1` | `LIVE_3B_TRAIN_PROTECTION_OK` | **GO** |
| A10 | Clobber protection | `.\scripts\Assert-ProductModelClobberProtection.ps1` | `PRODUCT_MODEL_CLOBBER_PROTECTION_OK` | **GO** |
| A11 | Fallback protection | `.\scripts\Assert-ProductFallbackProfile.ps1` | `PRODUCT_FALLBACK_PROFILE_OK` | **GO** |
| A12 | Model identity on live host (pre) | `Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/health \| Select primaryModel,primaryModelReady,runtimeMode` | `primaryModel=auricrux-fca`, ready, `ollama-live` (or documented degraded with reason) | **GO** |
| A13 | Manifest alignment (honest FAIL) | `.\scripts\Assert-AuricruxEvidenceRules.ps1` | `EVIDENCE_RULES_OK`; manifest does **not** claim generative PASS without qualified live authority | **GO** |
| A14 | Evidence ledger alignment | Compare ledger `currentLiveAuthority` to manifest `ggufGenerativePassRatePercent` / FAIL status | Both FAIL @ 76.7 citing 2026-08-02 report (until new qualified live run) | **GO** |
| A15 | Authority map | `.\scripts\Assert-AuricruxAuthorityMap.ps1` | `AUTHORITY_MAP_OK` | **GO** |
| A16 | Runtime proof pack present | `Test-Path` for: `docs\runtime-proof\AURICRUX_AUTHORITY_MAP.md`, `AURICRUX_PRIORITY_OPS_PROCEDURE.md`, `CUTOVER_ROLLBACK_DRILL.md`, `package-prepared-2026-08-03.json`, `authoritative-suite-rerun-prereqs-latest.json`, `auricrux_evidence_ledger_v1.json` | All `True` | **GO** |
| A17 | gh auth | `gh auth status` | Logged in; can dispatch workflows for this repo | **GO** (Auricrux account verified at prep) |
| A18 | Operational drift | `.\scripts\Assert-OperationalDrift.ps1` | Token `OPERATIONAL_DRIFT_OK` or `OPERATIONAL_DRIFT_WARN` with **0 FAIL**; prepared receipt aligned to publish stamp | Re-check before dispatch |

### Section A verdict (prep 2026-08-03)

**GO for package-web cutover dispatch** when A1–A18 satisfied (re-run before dispatch).  
**Cutover not executed by prep.** A18 drift must show 0 FAIL (WARN for pre-cutover host identity is acceptable until after cutover).

---

## Section B — Operator acknowledgements (check before dispatch)

- [ ] I am dispatching **package web cutover only** (containers), **not** model weight cutover.
- [ ] I will **not** set `AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED=1` for this action.
- [ ] I will **not** interrupt the live 3B train (`running-do-not-interrupt`).
- [ ] I accept host currently lacks `packageIdentity` / `/api/runtime-truth` — that is why cutover is needed.
- [ ] I will **not** treat this cutover as Manifest PASS or suite PASS.
- [ ] I know rollback steps are in `docs/runtime-proof/gguf-suite-live-cutover-procedure-2026-08-03.md`.
- [ ] After cutover I will run **Section C** before any authoritative suite claim.

**Dispatch command (only after A=GO and B checked):**

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
gh workflow run gcp-cutover-build-auricrux.yml -f action=full
gh run watch
```

---

## Section C — Post-cutover (must all be GO before authoritative suite)

| ID | Check | Verify command | Pass condition |
|----|-------|----------------|----------------|
| C1 | Health | `Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/health` | healthy/degraded; `primaryModel=auricrux-fca` ready; prefer `ollama-live` |
| C2 | Package identity | `.\scripts\Assert-PackageHostConsistency.ps1` | `PACKAGE_HOST_CONSISTENCY_OK` |
| C3 | Runtime truth | `Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/runtime-truth` | HTTP 200; `fallbackModeActive=false`; model/package fields present |
| C4 | Live safety gate | `.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1` | `DEPLOYMENT_SAFETY_GATE_OK` (no FAIL) |
| C5 | Model not clobbered | Same health + `.\scripts\Assert-ProductModelClobberProtection.ps1` | Still `auricrux-fca`; clobber assert OK |
| C6 | Authoritative suite | `.\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com` | Completes; ledger append; **PASS only if** qualified (packageIdentity + zero fallback contamination + ≥80%) |

**If any C1–C5 NO-GO:** execute rollback in cutover procedure; do not run authoritative suite for Manifest claims.

---

## Explicit NO-GO meanings (do not confuse)

| Situation | Meaning |
|-----------|---------|
| Host missing `packageIdentity` **before** cutover | Expected; **not** a Section A NO-GO |
| `PROMOTION_EVIDENCE_BLOCKED` | Blocks **promotion/Release PASS**, not package-web cutover |
| Live suite still 76.7% FAIL | Historical authority; cutover does not auto-clear it |
| C: storage BLOCKED | Does not block remote package-web cutover |
| Model weight replace requested | **NO-GO** for this checklist — use authorized GGUF load path instead |

---

## Quick re-run (copy/paste)

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-Live3bTrainProtection.ps1
.\scripts\Assert-ProductModelClobberProtection.ps1
.\scripts\Assert-ProductFallbackProfile.ps1
.\scripts\Assert-AuricruxEvidenceRules.ps1
.\scripts\Assert-AuricruxAuthorityMap.ps1
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes
.\scripts\Invoke-CutoverRollbackDryRun.ps1
# If all tokens OK and Section B checked:
# gh workflow run gcp-cutover-build-auricrux.yml -f action=full
```
