# Promotion evidence gate

**Assert:** `scripts/Assert-PromotionEvidenceGate.ps1` → `PROMOTION_EVIDENCE_OK` / `PROMOTION_EVIDENCE_BLOCKED`  
**Policy:** `auricrux/system/promotion_evidence_policy_v1.json`  
**Receipt:** `docs/runtime-proof/promotion-evidence-gate-latest.json`  
**Lifecycle:** `Assert-PromotionAllowed` (models) requires this gate before promote.

Promotion of a **model or package** is **evidence-based only**. Missing evidence is **BLOCKED** — never assumed.

## Required proof

| Requirement | How proven |
|-------------|------------|
| Correct host | Product BaseUrl host + runtime-truth host profile |
| Correct package | `Assert-PackageHostConsistency` (stamp/corpus/DLL vs live) |
| Correct model | Live `activeModel` / `primaryModel` == expected (`auricrux-fca`) and ready |
| Suite score met | Ledger `currentLiveAuthority` PASS ≥ 80% citing dated **live** product-host report |
| No unsafe fallback | `fallbackModeActive=false` (not corpus-fallback / degraded / llama3.2 interim) |
| No clobber event | `Assert-ProductModelClobberProtection` + no unauthorized cutover evidence |
| Rollback exists | Cutover procedure + precutover baseline + prev-container rename in cutover workflow |
| Manifest truthful | `Assert-AuricruxEvidenceRules` + manifest agrees with cited live report; `productHostDeployRequired` blocks |
| Evidence ledger updated | Ledger JSON+JSONL present, authority report on disk, append writer present |

## Forbidden assumptions

- Offline alias rescore ≠ live PASS  
- Local/package-only checks ≠ host authority  
- Missing `packageIdentity` / runtime-truth ≠ “probably fine”  
- Manifest PASS without matching dated live report ≠ promotion-green  

## Commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-PromotionEvidenceGate.ps1

# Models lifecycle (also invokes this gate)
cd C:\Users\MichaelBartholomew\source\auricrux-models
.\scripts\Test-PromotionAuthorizeGates.ps1 -Tier 5B
```

Emergency only: `-AllowEvidenceIncomplete` (records BLOCKED reasons; does not claim PASS).

## Current truth

Until product-host package cutover + dated live suite PASS clear the blockers, expect **`PROMOTION_EVIDENCE_BLOCKED`**. That is correct behavior.
