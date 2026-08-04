# Auricrux authority map (definitive)

**Policy:** `auricrux/system/auricrux_authority_chain_v1.json`  
**Assert:** `scripts/Assert-AuricruxAuthorityMap.ps1` → `AUTHORITY_MAP_OK`  
**Rule:** No PASS promotion without qualifying evidence. Missing evidence = no authority change.

## Current authorities (do not invent)

| Authority | Current | Pointer |
|-----------|---------|---------|
| Live suite | **FAIL 76.7%** | Ledger `currentLiveAuthority` → `construction_god_suite_gguf_generative_2026-08-02.json` |
| Manifest | **FAIL** (does not claim PASS) | `model_manifest.json` `evalStatus` …76.7-FAIL… |
| Promotion | **BLOCKED** | `PROMOTION_EVIDENCE_BLOCKED` |
| Deployment | Package **prepared**, host **not** proven current | `package-prepared-2026-08-03.json`; `PACKAGE_HOST_CONSISTENCY_BLOCKED` |

Historical 86.7% / 93.3% reports: **retained**, **not** Manifest/live PASS authority (fallback contamination / missing packageIdentity / AUTHORITY-CORRECTION).

---

## 1. Live suite authority — when it may change

**Pointer:** `auricrux_evidence_ledger_v1.json` → `currentLiveAuthority`

### May become FAIL when all true
1. Report `mode` = `gguf-generative-product-chat`  
2. `baseUrl` = product host  
3. Unique dated report path on disk  
4. Ledger append succeeds  
5. `suitePassed=false` **or** `passRatePercent < 80`  
6. Prefer: zero fallback-contamination excerpts (clean FAIL)

### May become PASS when **all** true (qualified)
1. Items 1–4 above  
2. `suitePassed=true` and `passRatePercent >= 80`  
3. Safety gate `DEPLOYMENT_SAFETY_GATE_OK` for that run  
4. **Zero** excerpts matching `no live model reachable` / corpus-fallback markers  
5. **Live `packageIdentity` present** on the suite report  
6. Ledger records `authority=live-dated-host-validation-qualified` and updates `currentLiveAuthority`

### Disqualifies PASS (row may still append as history)
- Fallback / “no live model” excerpts  
- Missing `packageIdentity`  
- Offline / alias-rescore mode  
- Non-product `baseUrl`  
- Overwriting a prior FAIL report file  

**Ledger writer enforces:** disqualified PASS does **not** move `currentLiveAuthority`.

---

## 2. Manifest authority — when it may change

**Pointer:** `auricrux/system/model_manifest.json` → `adapter.evalStatus` / `ggufGenerative*`

### May claim PASS when **all** true
1. `currentLiveAuthority.status == PASS` (qualified)  
2. Manifest cites **that exact** report path (exists)  
3. Claimed rate matches report within 0.1  
4. `ggufGenerativeSuitePassed=true` + `ValidatedAtUtc` set  
5. Cited report still passes live PASS qualification  
6. `productHostDeployRequired` false **or** host identity proves current package  

### Must remain FAIL when
- Live authority is FAIL  
- Only higher scores are offline rescore or disqualified reports  
- Host deploy still required without identity proof  

**Forbidden:** PASS from offline 80%; PASS from disqualified 86.7/93.3 files; rewriting FAIL report content.

---

## 3. Promotion authority — when it may change

**Pointer:** `Assert-PromotionEvidenceGate.ps1` → `PROMOTION_EVIDENCE_OK`

### May become OK when **all** nine evidence classes pass
| # | Requirement | Proof |
|---|-------------|-------|
| 1 | Correct host | Product host + runtime-truth |
| 2 | Correct package | `PACKAGE_HOST_CONSISTENCY_OK` |
| 3 | Correct model | `auricrux-fca` ready |
| 4 | Suite score | Qualified `currentLiveAuthority` PASS ≥ 80 |
| 5 | No unsafe fallback | `fallbackModeActive=false` |
| 6 | No clobber | `PRODUCT_MODEL_CLOBBER_PROTECTION_OK` |
| 7 | Rollback exists | Procedure + baseline + prev-rename |
| 8 | Manifest truthful | Does not claim unqualified PASS |
| 9 | Ledger updated | JSON + JSONL + authority report on disk |

**Never assume.** Missing evidence = `PROMOTION_EVIDENCE_BLOCKED`.

---

## 4. Deployment authority — two kinds (do not conflate)

### A. Package web cutover (containers only)
**May execute when:** train protection OK · clobber protection OK · offline safety gate OK · rollback present · workflow does not mutate `auricrux-fca` · **operator** runs `gcp-cutover-build-auricrux.yml`.

**Does not grant:** live suite PASS · Manifest PASS · promotion OK · weight replace.

**After cutover must prove:** `PACKAGE_HOST_CONSISTENCY_OK` (+ `/api/runtime-truth` when shipped).

### B. Model weight cutover (`auricrux-fca` replace)
**May execute when:** `Require-ProductModelCutoverAuthorization` + authorized load workflow + evidence under `product-model-cutover-evidence/`.

**Forbidden without auth:** `ollama create/rm/cp` onto `auricrux-fca`; warm/default init recreate.

---

## Dependency order (no skips)

```
package web cutover (optional enabler)
    → qualified live suite authority update
        → manifest may cite live suite
            → promotion OK (all nine)
                → model weight / PrimaryModel switch (as applicable)
```

## Non-authorities (never promote)

Offline package validation · offline alias rescore · local/dev suite · fallback-contaminated suite scores · assumptions.

## Commands

```powershell
.\scripts\Assert-AuricruxAuthorityMap.ps1
.\scripts\Assert-AuricruxEvidenceRules.ps1
.\scripts\Assert-PromotionEvidenceGate.ps1
.\scripts\Assert-PackageHostConsistency.ps1
```
