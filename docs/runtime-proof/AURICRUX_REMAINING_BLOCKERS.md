# Auricrux remaining blockers (authoritative)

**As of:** 2026-08-03 (blocker burn-down pass)  
**Scope:** Current Auricrux operational truth only  
**Excludes:** Resolved safeguards, historical-only scores (86.7% / 93.3% disqualified PASS), controlled non-issues  
**Receipt:** `docs/runtime-proof/auricrux-remaining-blockers-latest.json`  
**Burn-down:** `docs/runtime-proof/BLOCKER_BURNDOWN_2026-08-03.md` + `blocker-burndown-latest.json`  
**Elimination:** `docs/runtime-proof/BLOCKER_ELIMINATION_2026-08-03.md` — RB-C2 **Preconditions GO / PREPARED**; suite **READY_EXCEPT_RB_C2**; cutover **not** executed  
**Does not claim:** Manifest PASS · Release PASS · Promotion OK · Host package current

---

## Burn-down classification (stop condition met)

| Class | IDs |
|-------|-----|
| **EXTERNAL_AUTHORITY** | RB-C2 |
| **UPSTREAM** (of C2 / suite) | RB-C3, RB-C1, RB-H1 |
| **INFRASTRUCTURE** | RB-H2, RB-L1 |
| **OPERATIONAL_WAIT** | RB-H3 |
| **OPERATIONAL_ACCESS** | RB-H4 (Prepare reduced: `applyReady=true`; Apply/ACL still open) |
| **CONTENT_AUTHORING** | RB-M1 |
| **CLOSED** | RB-M2 |

No further local burn-down without operator cutover, disk, ACL/Apply, train completion, or L3 authoring.

---

## Snapshot

| Token / fact | Current |
|--------------|---------|
| Live suite authority | **FAIL 76.7%** (2026-08-02 report) |
| Offline alias 80% | Support-only (not a remaining claim blocker — policy holds) |
| Package cutover | **Not executed** — local readiness `RB_C2_CUTOVER_PRECONDITIONS_GO` + prepared refreshed |
| `PACKAGE_HOST_CONSISTENCY_*` | **BLOCKED** |
| Live `DEPLOYMENT_SAFETY_GATE_*` | **BLOCKED** (SG-20) |
| Offline gate (`-SkipLiveProbes`) | **OK** |
| `PROMOTION_EVIDENCE_*` | **BLOCKED** |
| C: free | **~36.06 GB** (`C_DRIVE_STORAGE_RISK_BLOCKED`) — see `RB_H2_C_DRIVE_FLOOR_REMEDIATION.md` |
| Live 3B train | `running-do-not-interrupt` PID 1019003 (**do not touch**) |
| model-lab writable | **False** (Prepare `applyReady=true`) |
| L3 10B→X | Intentionally **empty** (classified) |
| Operational drift | **OK** (`OPERATIONAL_DRIFT_OK` — RB-M2 closed) |

---

## CRITICAL

### RB-C1 — Live generative suite below threshold (authority FAIL)

| | |
|--|--|
| **Evidence** | Ledger `currentLiveAuthority` FAIL 76.7%; report `docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json`; manifest `evalStatus=PRODUCT-LOADED-GGUF-GENERATIVE-76.7-FAIL-THRESHOLD-80`; `ggufGenerativeSuitePassed=false` |
| **Impact** | Blocks Manifest PASS, Release PASS, and suite-score class of promotion evidence. Product quality authority remains FAIL. |
| **Closure** | New dated live product-host suite with `suitePassed=true`, `passRatePercent >= 80`, zero fallback contamination, `packageIdentity` present; ledger append updates `currentLiveAuthority` to qualified PASS. |
| **Validation** | `currentLiveAuthority.status=PASS` + rate ≥80 citing new report; `Assert-EvidenceLedgerIntegrity.ps1` OK; manifest may then cite that report only. |

### RB-C2 — Intended package not proven on product host

| | |
|--|--|
| **Evidence** | `PACKAGE_HOST_CONSISTENCY_BLOCKED` (PH-09…PH-19: no `packageIdentity`); `productHostDeployRequired=true`; `/api/runtime-truth` not serving intended package (404 until cutover); `package-prepared-latest.json` has `liveCutoverExecuted=false`; cutover GO/NO-GO Section C not run |
| **Impact** | Cannot prove host runs prepared package (DLL/corpus/stamp). Blocks authoritative suite entry (with RB-C3), live runtime-truth proof, and package class of promotion evidence. |
| **Closure** | Follow `RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md`: Preconditions GO → Section B → manual `gh workflow run gcp-cutover-build-auricrux.yml -f action=full` → PostVerify PASS. Does **not** replace `auricrux-fca` weights. |
| **Validation** | `RB_C2_CUTOVER_POSTVERIFY_PASS` + `PACKAGE_HOST_CONSISTENCY_OK`; live `/api/runtime-truth` HTTP 200; publish corpus SHA matches live `packageIdentity.corpusSha256`. |

### RB-C3 — Live deployment safety gate blocks authoritative suite entry

| | |
|--|--|
| **Evidence** | `DEPLOYMENT_SAFETY_GATE_BLOCKED`; SG-20 FAIL = `PACKAGE_HOST_CONSISTENCY_BLOCKED`; suite runner aborts on gate fail; prereqs receipt B1/B4 |
| **Impact** | Default path cannot start authoritative live GGUF suite; cannot clear RB-C1 without emergency `-SkipSafetyGate` (forbidden for authority claims). |
| **Closure** | Clear RB-C2 so SG-20 passes; live gate green; then `Invoke-AuthoritativeSuiteRerunPackage.ps1` → **GO**; then dated live suite. |
| **Validation** | `Assert-GgufSuiteDeploymentSafetyGate.ps1` → `DEPLOYMENT_SAFETY_GATE_OK` **without** `-SkipLiveProbes`; authoritative suite rerun package verdict **GO**. |

---

## HIGH

### RB-H1 — Promotion evidence blocked

| | |
|--|--|
| **Evidence** | `PROMOTION_EVIDENCE_BLOCKED`; PG failures include suite score FAIL 76.7 and package/host proof gaps (live consistency blocked) |
| **Impact** | Cannot authorize promotion / model-weight cutover / Release PASS. Assumption-based promote forbidden. |
| **Closure** | Satisfy promotion policy classes: qualified live suite PASS, correct host/package/model, no unsafe fallback, clobber OK, rollback exists, truthful manifest, ledger updated. |
| **Validation** | `Assert-PromotionEvidenceGate.ps1` → `PROMOTION_EVIDENCE_OK` |

### RB-H2 — Primary C: below KF 50 GB floor

| | |
|--|--|
| **Evidence** | `C_DRIVE_STORAGE_RISK_BLOCKED`; free **36.15 GB**; CS-02 FAIL; no D:/E:/F: volume; remediation doc `RB_H2_C_DRIVE_FLOOR_REMEDIATION.md` |
| **Impact** | Multi-GB GGUF/Ollama staging on Primary refused. Does **not** block remote package-web cutover or Azure train. Blocks local weight import growth. |
| **Closure** | Minimum-risk path: attach data disk → point work/Ollama off C: (dual-preserve) → optional soft reclaim → OneDrive On-Demand / IT reclaim for remaining gap. **No** evidence/train/production deletes. |
| **Validation** | `Assert-CDriveStorageRisk.ps1` → `C_DRIVE_STORAGE_RISK_OK` (free ≥50 GB) |

### RB-H3 — Live 3B train must not be interrupted

| | |
|--|--|
| **Evidence** | Manifest `trueGodRun.status=running-do-not-interrupt`, `trainPid=1019003`; `LIVE_3B_TRAIN_PROTECTION_OK` (protection in place; train still open) |
| **Impact** | Competing GPU train/export-on-GPU forbidden; no kill/pause/move/optimize. Constrains 5B+ train launch on same GPU and GPU merges. |
| **Closure** | Natural train completion (or founder-authorized stop **after** PID confirmed gone and tip integrity). Not an actionable “fix” while running. |
| **Validation** | Status leaves `running-do-not-interrupt`; protection assert still green during run; post-exit: integrity + resume/export per golden path. |

### RB-H4 — model-lab not writable

| | |
|--|--|
| **Evidence** | Verified 2026-08-03: `AzureAD\MichaelBartholomew` = **RX** on `C:\Users\Auricrux\auricrux-model-lab`; `AzureAD\Auricrux` = **(F)**; write probe **Access denied**. Plan: `RB_H4_MODEL_LAB_WRITE_CLOSURE_PLAN.md` |
| **Impact** | Local `-Apply` sync blocked for Michael; Prepare/staging on Primary OK. Train-host ops unaffected. |
| **Reduced (burn-down)** | Prepare `recovery schema mismatch` cleared — sync manifest `recoveryCatalog.requireSchemaVersion` aligned to catalog `schemaVersion=2`; Prepare **PASS**, `applyReady=true`, `modelLabWritable=false`. |
| **Closure** | Preferred: `Invoke-ModelLabSync.ps1 -Apply` as Auricrux. Alt: grant Michael Modify, or writable `AURICRUX_MODEL_LAB`. |
| **Validation** | Write probe create/delete OK for Apply identity; `-Apply` succeeds; re-Prepare clean |

---

## MEDIUM

### RB-M1 — Empty L3 overlays (10B→X)

| | |
|--|--|
| **Evidence** | `L3_EMPTY_BEHAVIOR_OK` with intentionally_empty for 10B→X; `Test-AuricruxL3PromotionReady` ready=false for those tiers; manifest `emptyL3Blocker=unchanged-intentionally-empty-10b-x` |
| **Impact** | Blocks **tier promotion** to 10B→X. Does **not** affect live 3B suite score, corpus, or package cutover. |
| **Closure** | Author real L3 cases/slices for target tier (no fabricate); pass shape validation. |
| **Validation** | `Test-AuricruxL3PromotionReady -Tier <T>` → `ready=true`; `Assert-L3EmptyBehavior.ps1` shows authored for that tier. |

---

## LOW

### RB-L1 — Work root still on Primary C:

| | |
|--|--|
| **Evidence** | `work-root-latest.json`: `source\_auricrux-work`, `onPrimaryC=true`, `fallbackAbsolute`; CS-08 no D:/E:/F:; plan `RB_L1_WORK_ROOT_MIGRATION_PLAN.md` (**migration not executed**) |
| **Impact** | Scratch consolidation remains on C:; cannot physically relocate until disk exists. Does not block remote package-web cutover. |
| **Closure** | Attach data disk → set `AURICRUX_WORK_ROOT` (+ staging/publish env) → `Initialize-AuricruxWorkRoot.ps1` → optional junction for old path → validate `onPrimaryC=false`. Preserve repos/evidence in place. |
| **Validation** | `work-root-latest.json` `onPrimaryC=false`; `Test-AuricruxPathRegistry.ps1` OK; storage assert work-root off C: |

---

## Explicitly excluded (not remaining blockers)

| Item | Why excluded |
|------|----------------|
| Offline alias 80% “claim risk” | **Controlled** — `EVIDENCE_RULES_OK`; not an open defect |
| Historical 86.7% / 93.3% PASS rows | **Historical-only** — superseded; ledger integrity OK |
| Clobber / fallback / evidence / authority map / golden-path structure / train survivability / L3 classification asserts | **Resolved safeguards** (tokens OK) |
| Cutover dry-run / promotion appRoot token bug | **Resolved** |
| Manifest honest FAIL | Correct state of RB-C1 — not a separate blocker |
| Package prepare (local) | **Done** — offline gate OK; remaining issue is deploy (RB-C2) |
| RB-M2 prepared/stamp lag | **Closed** — `OPERATIONAL_DRIFT_OK`; live host WARN until RB-C2 is expected |

---

## Dependency order (operator)

See full graph: [REMAINING_BLOCKERS_DEPENDENCY_GRAPH.md](./REMAINING_BLOCKERS_DEPENDENCY_GRAPH.md)

```
RB-C2 (package cutover)
  → RB-C3 (live safety gate OK)   [usually auto-clears with C2]
    → RB-C1 (authoritative live suite ≥80% qualified)
      → RB-H1 (PROMOTION_EVIDENCE_OK)   [HIGH — after criticals]
```

**Shortest path to zero CRITICAL:** RB-C2 → RB-C3 (verify) → RB-C1.

Parallel / independent:
- **RB-H3** — never interrupt while running  
- **RB-H2 / RB-L1** — data disk; before local multi-GB import (not required for package-web cutover)  
- **RB-H4** — before model-lab sync Apply  
- **RB-M1** — before promoting 10B→X  

### Closed / reduced (no authority elevation)
- **RB-M2** Operational drift prepared/stamp lag → `OPERATIONAL_DRIFT_OK` (live ProbeLive may still WARN until RB-C2)
- **RB-H4 companion** Prepare recovery schema gate → `applyReady=true` (Apply/ACL still open — OPERATIONAL_ACCESS)

---

## Refresh commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase Preconditions   # RB-C2 prep only
.\scripts\Assert-PackageHostConsistency.ps1
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
.\scripts\Assert-PromotionEvidenceGate.ps1
.\scripts\Assert-OperationalDrift.ps1 -ProbeLive
cd C:\Users\MichaelBartholomew\source\auricrux-models
.\scripts\Assert-CDriveStorageRisk.ps1
.\scripts\Assert-L3EmptyBehavior.ps1
.\scripts\Assert-Live3bTrainProtection.ps1   # from app root also
```
