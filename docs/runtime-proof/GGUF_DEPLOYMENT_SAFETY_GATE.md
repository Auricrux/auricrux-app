# GGUF suite deployment safety gate

**Script:** `scripts/Assert-GgufSuiteDeploymentSafetyGate.ps1`  
**Enforced by:** `scripts/run-gguf-construction-suite.ps1` (default; abort on FAIL)  
**Success token:** `DEPLOYMENT_SAFETY_GATE_OK`  
**Receipt:** `docs/runtime-proof/gguf-deployment-safety-gate-latest.json`

Never starts training. Never recreates product models. Do not proceed through a failed gate.

## Checks

| ID | Verifies |
|----|----------|
| SG-01 | Correct target host (`auricrux.futurecontractorsofamerica.com`, https) |
| SG-02 | Publish package complete (`_publish/web`) |
| SG-03 | DLL version not stale vs `ConstructionIntelligenceService.cs` |
| SG-04 | `ExpandSearchTerms` present in publish DLL |
| SG-05 | Grounding prompt strings present in DLL |
| SG-06 | Grounding corpus present (≥70 entries) |
| SG-07 | Silica corpus (`respirable crystalline silica` + `respiratory` tag) |
| SG-08 | Modelfile bannered DEV FALLBACK ONLY (no product overwrite) |
| SG-09 | `llama3.2` pull confined to compose profile `dev-fallback`; creates `auricrux-fca-dev-fallback` only |
| SG-09b | `Assert-OllamaInitSafety.ps1` (`OLLAMA_INIT_SAFETY_OK`) |
| SG-18 | `Assert-ProductModelClobberProtection.ps1` (`PRODUCT_MODEL_CLOBBER_PROTECTION_OK`) |
| SG-19 | `Assert-Live3bTrainProtection.ps1` (`LIVE_3B_TRAIN_PROTECTION_OK`) — static; never touches train PID |
| SG-20 | `Assert-PackageHostConsistency.ps1` (`PACKAGE_HOST_CONSISTENCY_OK`) — FAIL on stale/mismatched/ambiguous host |
| SG-21 | `Assert-GgufSuiteFailureRegression.ps1` (`GGUF_SUITE_FAILURE_REGRESSION_OK`) — known FAIL/near-FAIL coverage locked |
| SG-22 | `Assert-RuntimeTruth.ps1` (`RUNTIME_TRUTH_OK`) — operational truth endpoint present |
| SG-23 | `Assert-PromotionEvidenceGate.ps1` — WARN if `PROMOTION_EVIDENCE_BLOCKED` (suite may run; promote hard-blocks separately) |
| SG-10 | Warm workflow does not Modelfile/`llama3.2`-recreate `auricrux-fca` |
| SG-11 | Rollback procedure + precutover baseline + cutover `prev-` rename |
| SG-12 | `model_manifest.json` preserved (GGUF kind, `auricrux-fca`) |
| SG-13 | Live health: product `auricrux-fca` ready (not clobbered) |
| SG-14 | Live capabilities: GGUF signals present (not alias-only clobber) |
| SG-15 | Package stamp + `PackageIdentityService` + stamp script present |
| SG-16 | Live `packageIdentity` vs publish corpus SHA (FAIL = stale; WARN if host not yet reporting) |
| SG-17 | DLL SHA compare live vs publish (WARN if differ across OS/container; informational) |

See also: [AURICRUX_PACKAGE_IDENTITY.md](./AURICRUX_PACKAGE_IDENTITY.md) · [PACKAGE_HOST_CONSISTENCY.md](./PACKAGE_HOST_CONSISTENCY.md) · [AURICRUX_EVIDENCE_RULES.md](./AURICRUX_EVIDENCE_RULES.md) · [OLLAMA_INIT_SAFE_UNSAFE_PATHS.md](./OLLAMA_INIT_SAFE_UNSAFE_PATHS.md) · [PRODUCT_MODEL_CLOBBER_PROTECTION.md](./PRODUCT_MODEL_CLOBBER_PROTECTION.md) · [LIVE_3B_TRAIN_PROTECTION.md](./LIVE_3B_TRAIN_PROTECTION.md) · [RUNTIME_TRUTH.md](./RUNTIME_TRUTH.md) · [PROMOTION_EVIDENCE_GATE.md](./PROMOTION_EVIDENCE_GATE.md) · [AURICRUX_OPERATIONAL_CLOSURE_2026-08-03.md](./AURICRUX_OPERATIONAL_CLOSURE_2026-08-03.md)

## Commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app

# Gate only
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1

# Live suite (gate runs first; aborts on FAIL)
.\scripts\run-gguf-construction-suite.ps1

# Emergency only — do not use for authority runs
.\scripts\run-gguf-construction-suite.ps1 -SkipSafetyGate

# Evidence authority rules (offline vs live, Manifest/Release PASS)
.\scripts\Assert-AuricruxEvidenceRules.ps1

# Product model clobber protection
.\scripts\Assert-ProductModelClobberProtection.ps1

# Live 3B train protection (static; does not touch train)
.\scripts\Assert-Live3bTrainProtection.ps1

# Package-to-host consistency (FAIL on stale/mismatched/ambiguous)
.\scripts\Assert-PackageHostConsistency.ps1

# GGUF failure regression coverage (does not weaken suite)
.\scripts\Assert-GgufSuiteFailureRegression.ps1
```
