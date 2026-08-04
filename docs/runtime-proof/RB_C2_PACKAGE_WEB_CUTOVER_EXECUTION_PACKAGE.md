# RB-C2 package-web cutover — execution package

**Blocker:** RB-C2 — intended package not proven on product host  
**Scope:** Package **web/API container** cutover only  
**This package does NOT:** replace `auricrux-fca` weights · touch 3B train · claim Manifest/suite/Release PASS · auto-dispatch cutover  

**Status of this document:** Preparation / operator runbook. **Cutover is not claimed complete** by presence of this file.

| Field | Value |
|-------|--------|
| Product host | `https://auricrux.futurecontractorsofamerica.com` |
| GCP project | `auricrux-mobile-prod` |
| Zone / instance | `us-central1-b` / `instance-20260715-113528` |
| Workflow | `.github/workflows/gcp-cutover-build-auricrux.yml` |
| Dispatch input | `-f action=full` |
| Orchestrator | `scripts/Invoke-RbC2PackageWebCutoverPackage.ps1` |
| GO/NO-GO detail | `docs/runtime-proof/CUTOVER_GO_NO_GO_CHECKLIST.md` |
| Rollback procedure | `docs/runtime-proof/gguf-suite-live-cutover-procedure-2026-08-03.md` |
| Rollback drill | `scripts/Invoke-CutoverRollbackDryRun.ps1` |
| App root | `C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app` |

---

## 0. What cutover does and does not do

### Does
1. Checks out repo on GitHub Actions runner.
2. Packs **source** (excludes `_publish`, bin/obj) and SCPs to product GCE.
3. `docker build` from `Dockerfile` → tag `auricrux-web:cutover`.
4. Renames live containers to `auricrux-web-prev-<unix>` / `auricrux-api-prev-<unix>`.
5. Starts new `auricrux-web` (:4000→80) and `auricrux-api` (:5001→80) with `Auricrux__PrimaryModel=auricrux-fca`.
6. Runs in-job public smoke (`/health`, capabilities, tools, vision).

### Does not
- Contact Azure train host or PID (`LIVE 3B TRAIN PROTECTION` in workflow header).
- `ollama create` / `ollama rm` / Modelfile recreate of `auricrux-fca`.
- Set `AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED`.
- Upload local `_publish\web` as the deploy artifact (image is **Dockerfile build of git source**).
- Clear RB-C1 (suite score), RB-H1 (promotion), or Manifest PASS by itself.

### Operator implication (no tribal knowledge)
- Intended code + `auricrux/system/package_stamp.json` + corpus in **git** must be **pushed** to the branch the workflow checks out (default branch) **before** dispatch.
- Local `_publish\web` is the **comparison baseline** for `Assert-PackageHostConsistency.ps1` after cutover; keep stamp/corpus aligned with what was pushed.
- DLL SHA on host may differ from local publish (container rebuild) — corpus SHA + packageVersion + suiteTarget + `packageIdentity` presence are the hard identity proofs (see PH-14…PH-16).

---

## 1. Prerequisites (must all pass before dispatch)

### 1.1 Access
| # | Requirement | How to prove |
|---|-------------|--------------|
| P1 | PowerShell on operator workstation | `pwsh` or Windows PowerShell 5.1+ |
| P2 | Repo checkout at app root | `cd` path above; `Test-Path .\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1` |
| P3 | `gh` authenticated to repo that owns the workflow | `gh auth status`; `gh workflow list --repo <owner/auricrux-app>` shows `GCP Cutover Build Auricrux` |
| P4 | Network reachability to product host | `Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/health` |
| P5 | GCP Actions secrets already configured | Workflow historically green for this repo (operator does not invent secrets mid-cutover) |

### 1.2 Safety / integrity tokens (Section A)
Run the orchestrator (preferred) **or** checklist Section A row-by-row:

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase Preconditions
# Must print: RB_C2_CUTOVER_PRECONDITIONS_GO
# Receipt: docs/runtime-proof/rb-c2-cutover-execution-package-latest.json
```

Required tokens / conditions (any FAIL = **NO-GO**):

| ID | Token / condition |
|----|-------------------|
| A1–A4 | Publish package + stamp present; offline `DEPLOYMENT_SAFETY_GATE_OK` |
| A5–A6 | Rollback procedure + precutover baseline files present |
| A7 | Workflow contains train protection + `PrimaryModel=auricrux-fca` + `prev-$(date` rename; **no** product-tag ollama mutate |
| A8 | `CUTOVER_ROLLBACK_DRILL_OK` or `CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED` |
| A9–A11 | `LIVE_3B_TRAIN_PROTECTION_OK` · `PRODUCT_MODEL_CLOBBER_PROTECTION_OK` · `PRODUCT_FALLBACK_PROFILE_OK` |
| A12 | Live health: `primaryModel=auricrux-fca`, ready, prefer `ollama-live` |
| A13–A15 | `EVIDENCE_RULES_OK` · ledger/manifest honest FAIL align · `AUTHORITY_MAP_OK` |
| A16 | Runtime-proof pack files present |
| A17 | `gh auth status` usable |
| A18 | `OPERATIONAL_DRIFT_OK` or WARN with **0 FAIL** |

### 1.3 Git readiness (cutover builds from source)
| # | Requirement | How to prove |
|---|-------------|--------------|
| G1 | Intended commits on remote default branch | `git status -sb`; `git log origin/HEAD..HEAD` empty (or push first) |
| G2 | Stamp in **repo** matches intended identity | `Get-Content auricrux\system\package_stamp.json` → version/suite/host/recipe |
| G3 | Corpus in build path | Dockerfile / publish path includes `Data/construction-corpus.json` in image |

### 1.4 Operator acknowledgements (Section B — human checklist)
Before typing the dispatch command, operator must affirm:

- [ ] Package **web** cutover only — not model-weight cutover  
- [ ] Will **not** set `AURICRUX_PRODUCT_MODEL_CUTOVER_AUTHORIZED=1`  
- [ ] Will **not** interrupt live 3B train (`running-do-not-interrupt`)  
- [ ] Accepts pre-cutover missing `packageIdentity` / `/api/runtime-truth` 404 — that is RB-C2  
- [ ] Will **not** treat cutover as Manifest / suite / Release PASS  
- [ ] Knows rollback steps (Section 3)  
- [ ] Will run **PostVerify** before any authoritative suite claim  

### 1.5 Explicit non-blockers for package-web dispatch
These may remain BLOCKED and still allow RB-C2 dispatch:

| Token / fact | Why not a package-web NO-GO |
|--------------|----------------------------|
| `PACKAGE_HOST_CONSISTENCY_BLOCKED` (pre) | Expected; reason for cutover |
| Live `DEPLOYMENT_SAFETY_GATE_BLOCKED` (SG-20) | Cleared **by** successful cutover |
| `PROMOTION_EVIDENCE_BLOCKED` | Blocks promote/Release, not web cutover |
| Live suite FAIL 76.7% | RB-C1; separate after host proof |
| `C_DRIVE_STORAGE_RISK_BLOCKED` | Local staging only |
| Empty L3 / model-lab ACL | Unrelated tiers / paths |

---

## 2. Validation steps

### 2.1 Pre-dispatch (automated)
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase Preconditions
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase CaptureBaseline
```

Pass = `RB_C2_CUTOVER_PRECONDITIONS_GO` and baseline receipt written.  
Fail = `RB_C2_CUTOVER_PRECONDITIONS_NO_GO` — **do not dispatch**.

### 2.2 Dispatch (manual — this package never auto-runs it)
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
# ONLY after PRECONDITIONS_GO + Section B checked + intended SHA pushed:
gh workflow run gcp-cutover-build-auricrux.yml -f action=full
gh run list --workflow=gcp-cutover-build-auricrux.yml --limit 3
gh run watch   # or: gh run view <id> --log-failed
```

Record in evidence: **run URL**, **run id**, **git SHA** Actions used, **start/end UTC**.

### 2.3 In-job workflow success criteria (GitHub)
Workflow job must be **success**, including Public smoke step:
- Public `/health` reachable (soft fail allowed on health line; continue)
- `/api/capabilities` HTTP success
- `/api/agent/tools` HTTP success
- `/api/vision` returns `"success":true`

If job **failed** after swap started → treat as cutover failure → **rollback** (Section 3) before retry.

### 2.4 Post-cutover (automated — required)
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase PostVerify -GhRunId <run-id>
```

Must print `RB_C2_CUTOVER_POSTVERIFY_PASS` for RB-C2 closure eligibility.

---

## 3. Rollback steps

**When:** Any of: workflow failed mid-swap; PostVerify FAIL on health/model; host unhealthy; wrong primaryModel; operator abort.

**Where:** Product GCE only (`instance-20260715-113528`). Never train host.

**How (SSH via IAP or console):**

```bash
# 1) Identify previous containers
sudo docker ps -a --filter name=auricrux-web-prev --format '{{.Names}} {{.CreatedAt}}'
sudo docker ps -a --filter name=auricrux-api-prev --format '{{.Names}} {{.CreatedAt}}'

# 2) Quarantine failed new containers
sudo docker stop auricrux-web auricrux-api || true
sudo docker rename auricrux-web auricrux-web-failed-$(date +%s) || true
sudo docker rename auricrux-api auricrux-api-failed-$(date +%s) || true

# 3) Restore newest prev-* (adjust if multiple; pick intended timestamp)
PREV_WEB=$(sudo docker ps -a --filter name=auricrux-web-prev --format '{{.Names}}' | head -1)
PREV_API=$(sudo docker ps -a --filter name=auricrux-api-prev --format '{{.Names}}' | head -1)
sudo docker rename "$PREV_WEB" auricrux-web
sudo docker rename "$PREV_API" auricrux-api
sudo docker start auricrux-web auricrux-api

# 4) Local probe on VM
curl -fsS http://127.0.0.1:4000/api/health
curl -fsS http://127.0.0.1:5001/api/health
```

**Then from operator workstation:**
```powershell
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/health
.\scripts\Assert-ProductModelClobberProtection.ps1
.\scripts\Assert-Live3bTrainProtection.ps1
```

**Rollback MUST NOT:** mutate `auricrux-fca`; set product-model auth env; contact train PID/host.

**Evidence after rollback:** dated note in `docs/runtime-proof/` + append orchestrator `-Phase RecordEvidence -Outcome ROLLBACK` (or update latest JSON `outcome`).

---

## 4. Evidence requirements

Every cutover attempt (success or fail) must leave an auditable trail.

### 4.1 Mandatory artifacts
| Artifact | Path / location |
|----------|-----------------|
| Preconditions receipt | `docs/runtime-proof/rb-c2-cutover-execution-package-latest.json` (+ dated copy) |
| Precutover baseline | Written by `-Phase CaptureBaseline` into same receipt / dated file |
| GitHub Actions run | URL + id + conclusion + head SHA |
| PostVerify receipt | Same latest JSON `phases.postVerify` |
| Package-host assert | `docs/runtime-proof/package-host-consistency-latest.json` |
| Live safety gate | `docs/runtime-proof/gguf-deployment-safety-gate-latest.json` |
| Operational drift (live) | `docs/runtime-proof/operational-drift-latest.json` with `-ProbeLive` after cutover |

### 4.2 Optional / follow-on (not required to close RB-C2 alone)
| Artifact | When |
|----------|------|
| Authoritative suite report | After PostVerify PASS only; closes toward RB-C1 |
| Ledger append | Only from suite runner on qualified live result |
| Manifest evalStatus change | Only if suite PASS qualified — **not** for cutover alone |

### 4.3 Forbidden evidence claims
- Offline alias rescore as live PASS  
- Cutover success as Manifest/Release/Promotion PASS  
- Historical 86.7% / 93.3% as current authority  
- `liveCutoverExecuted=true` without PostVerify PASS + Actions success  

---

## 5. Success criteria (RB-C2 closed)

All must be true:

| # | Criterion | Proof token / observation |
|---|-----------|---------------------------|
| S1 | Actions run `action=full` **success** | `gh run view <id> --json conclusion,url,headSha` → `success` |
| S2 | Public health: `primaryModel=auricrux-fca`, ready | `/api/health` |
| S3 | Host package identity present and consistent | `PACKAGE_HOST_CONSISTENCY_OK` |
| S4 | Runtime truth reachable | `/api/runtime-truth` HTTP 200; `fallbackModeActive=false` (or documented false equivalent) |
| S5 | Live deployment safety gate green | `DEPLOYMENT_SAFETY_GATE_OK` **without** `-SkipLiveProbes` |
| S6 | Product model not clobbered | health still `auricrux-fca`; `PRODUCT_MODEL_CLOBBER_PROTECTION_OK` |
| S7 | Train untouched | Manifest/train protection still `running-do-not-interrupt` / `LIVE_3B_TRAIN_PROTECTION_OK`; no train host contact |
| S8 | Orchestrator post phase | `RB_C2_CUTOVER_POSTVERIFY_PASS` |
| S9 | Evidence filed | latest + dated JSON with `cutoverExecuted=true` **only after S1–S8** |

**RB-C2 closed does not imply:** suite ≥80%, Manifest PASS, `PROMOTION_EVIDENCE_OK`.

---

## 6. Failure criteria (abort / rollback / do not claim RB-C2)

| # | Failure | Action |
|---|---------|--------|
| F1 | Preconditions NO-GO | Do not dispatch |
| F2 | Intended SHA not on remote | Do not dispatch; push or abort |
| F3 | Workflow cancelled / failed | Investigate logs; if containers swapped → rollback |
| F4 | Post-cutover health wrong model / not ready | Rollback |
| F5 | `PACKAGE_HOST_CONSISTENCY_BLOCKED` after green workflow | Treat as incomplete cutover; diagnose stamp/corpus/identity; rollback if host degraded |
| F6 | `/api/runtime-truth` still 404 after green workflow | Incomplete — do not claim RB-C2; fix package identity in image or rollback |
| F7 | Live safety gate still FAIL (esp. SG-20) | RB-C2 not closed |
| F8 | `primaryModel` ≠ `auricrux-fca` or fallback active for product path | Rollback; investigate env |
| F9 | Any train host/PID contact or model-weight auth set during attempt | Stop; document incident; do not claim success |
| F10 | Operator claims Manifest/suite PASS from cutover alone | Invalid — evidence integrity violation |

---

## 7. Post-cutover verification (ordered)

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app

# 7.1 Orchestrated (preferred)
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase PostVerify -GhRunId <run-id>

# 7.2 Manual equivalents if needed
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/health |
  Select-Object status, primaryModel, primaryModelReady, runtimeMode, ollamaReachable
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/runtime-truth
.\scripts\Assert-PackageHostConsistency.ps1
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
.\scripts\Assert-ProductModelClobberProtection.ps1
.\scripts\Assert-Live3bTrainProtection.ps1
.\scripts\Assert-OperationalDrift.ps1 -ProbeLive
```

### After RB-C2 closed (next blockers — not part of this package’s success)
1. RB-C3 should clear with live gate OK (same PostVerify S5).  
2. RB-C1: authoritative suite — only after S1–S8:  
   `.\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com`  
3. RB-H1: `Assert-PromotionEvidenceGate.ps1` only after qualified suite PASS.

---

## 8. Quick reference — one screen

| Step | Command / action | Pass token |
|------|------------------|------------|
| Pre | `Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase Preconditions` | `RB_C2_CUTOVER_PRECONDITIONS_GO` |
| Baseline | `-Phase CaptureBaseline` | baseline block in receipt |
| Dispatch | `gh workflow run gcp-cutover-build-auricrux.yml -f action=full` | Actions `success` |
| Post | `-Phase PostVerify -GhRunId …` | `RB_C2_CUTOVER_POSTVERIFY_PASS` |
| Fail | VM prev-rename restore (Section 3) | health restored; do not claim RB-C2 |

**Cutover completion is claimed only when PostVerify PASS + evidence filed — never by preparing this package alone.**
