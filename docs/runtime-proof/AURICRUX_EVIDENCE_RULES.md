# Auricrux evidence rules

**Canonical policy for what counts as proof.**  
**Assert:** `scripts/Assert-AuricruxEvidenceRules.ps1` → token `EVIDENCE_RULES_OK`  
**Ledger:** [AURICRUX_EVIDENCE_LEDGER.md](./AURICRUX_EVIDENCE_LEDGER.md)  
**Package identity:** [AURICRUX_PACKAGE_IDENTITY.md](./AURICRUX_PACKAGE_IDENTITY.md)

These rules prevent stale-package confusion, offline-as-PASS claims, score inflation without rerun, and silent overwrite of history.

---

## Evidence classes (do not conflate)

| Class | What it is | Authority? | Typical artifacts |
|-------|------------|------------|-------------------|
| **Offline package validation** | Local `_publish/web` (or build output) checks: DLL freshness, ExpandSearchTerms, grounding/silica corpus, stamp, Modelfile banners, compose/warm guards. No live chat required. | **Support / gate only.** Proves the *intended package* is complete — not that the product host is serving it. | `Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes`; SG-02…SG-12, SG-15 |
| **Offline alias rescore** | Re-score stored suite excerpts with `keyword_aliases_v1.json`. No live `/api/chat`. | **Investigation support only.** Must never claim live PASS, must never upgrade manifest scores, must never replace a live dated report. | `scripts/rescore_gguf_report_aliases.py` → `*_alias_rescore.json` |
| **Local suite validation** | Suite run against localhost / non-product BaseUrl (dev compose, laptop publish). | **Local engineering signal.** Not product-host authority. Not sufficient for Manifest PASS. | Dated report with `baseUrl` ≠ product host |
| **Live product host validation** | Dated generative suite against `https://auricrux.futurecontractorsofamerica.com` after safety gate, with unique UTC report stamp + ledger append. | **Authority** for generative suite PASS/FAIL used in product claims. | `run-gguf-construction-suite.ps1`; ledger rows with `authority=live-dated-host-validation` |
| **Manifest PASS** | `model_manifest.json` `adapter.evalStatus` (and related `ggufGenerative*` fields) claiming suite PASS at threshold. | Allowed **only** when backed by a **dated live product host** report that still exists on disk and matches the claimed score/path. | `auricrux/system/model_manifest.json` |
| **Release PASS** | Cutover/release readiness: package identity on host, safety gate OK (including live probes), live suite PASS at threshold, rollback path available, prior FAIL history preserved. | **Ship/cutover gate.** Stricter than Manifest PASS alone (identity + gate + live suite + history). | Gate receipt + live suite report + package identity + ledger |
| **Promotion OK** | Evidence gate proves correct host/package/model, suite score, no unsafe fallback, no clobber, rollback, truthful manifest, ledger updated. | **Promote/cutover authorize.** Assumption-based promotion is forbidden. | `Assert-PromotionEvidenceGate.ps1` → `PROMOTION_EVIDENCE_OK` |

**Definitive authority map (conditions for every transition):** [AURICRUX_AUTHORITY_MAP.md](./AURICRUX_AUTHORITY_MAP.md) · policy `auricrux/system/auricrux_authority_chain_v1.json` · assert `Assert-AuricruxAuthorityMap.ps1`

### Live PASS qualification (eliminates ambiguity)

A live suite report may update `currentLiveAuthority` to **PASS** only if it also has:

- Zero excerpts with `no live model reachable` / corpus-fallback markers  
- Live `packageIdentity` present on the report  
- Product-host `baseUrl`, generative mode, threshold met  

Disqualified PASS scores (e.g. historical 86.7% / 93.3% with fallback excerpts) remain **history only** — they do not move Manifest or promotion authority.

### Authority ladder (highest → lowest)

1. Live product host validation (dated)  
2. Manifest PASS (must cite #1)  
3. Release PASS (must include #1 + gate + identity + history)  
4. Offline package validation / local suite (support)  
5. Offline alias rescore (support only; never authority)

---

## Hard rules

### 1. Offline evidence may support investigation
Offline package checks and alias rescores are useful to diagnose grounding, aliases, and package completeness. Document them as support.

### 2. Offline evidence may not replace live product validation
No script, doc, claim, or manifest field may treat offline alias rescore or offline package validation as a live generative PASS.

### 2b. Disqualified live PASS may not elevate authority
A suite row with status PASS that has fallback contamination, missing `packageIdentity`, or non-product `baseUrl` is **historical only**. It must appear under ledger `supersededEvidence` (or an AUTHORITY-CORRECTION listing it) and must not move `currentLiveAuthority` to PASS. Assert: `Assert-EvidenceLedgerIntegrity.ps1`.

### 3. Manifest PASS requires dated live validation
Before (or when) `evalStatus` / `ggufGenerativeSuitePassed` asserts PASS:

- `ggufGenerativeValidatedAtUtc` must be set (dated).
- `ggufGenerativeReport` must point to an existing dated live report file.
- Claimed `ggufGenerativePassRatePercent` must match that report’s `passRatePercent` (within 0.1).
- That report’s `baseUrl` must be the product host (or explicitly documented product URL).
- Report mode must be live generative (not `offline-excerpt-rescore-with-aliases`).

### 4. Scores must not be upgraded without rerun evidence
Do not raise `passRatePercent`, flip FAIL→PASS, or rewrite `evalStatus` based on alias rescore, manual edit of excerpts, or “expected” improvement. A higher score requires a **new** dated live suite run (unique report path) and ledger append.

### 5. Prior failures remain historically preserved
Prior FAIL reports (e.g. 2026-08-02 76.7%) stay on disk under their original paths. Do not delete, rename-over, or soft-replace them with PASS content.

### 6. New evidence must be appended, not overwritten
- Suite reports: unique UTC stamp filenames; never overwrite an existing evidence JSON.
- Ledger JSON + JSONL: append new rows; never rewrite prior FAIL entries’ scores/status.
- Alias rescore writes a **sidecar** `*_alias_rescore.json`, never mutates the source live report.

---

## Mapping to workflows (preserved)

```powershell
# Offline package validation (support / preflight)
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1 -SkipLiveProbes

# Offline alias rescore (support only)
python .\scripts\rescore_gguf_report_aliases.py .\docs\runtime-proof\construction_god_suite_gguf_generative_2026-08-02.json

# Live product host validation (authority)
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
.\scripts\run-gguf-construction-suite.ps1

# Evidence rules assert (policy check)
.\scripts\Assert-AuricruxEvidenceRules.ps1

# Ledger integrity (history + supersession + no elevation)
.\scripts\Assert-EvidenceLedgerIntegrity.ps1

# Promotion evidence (model/package) — hard-blocks promote until OK
.\scripts\Assert-PromotionEvidenceGate.ps1
```

Safety gate → live suite → ledger append remains the authority path. `-SkipSafetyGate` remains emergency-only and does not change these rules.

---

## Claim language (allowed vs forbidden)

| Allowed | Forbidden |
|---------|-----------|
| “Offline alias rescore reached 80% (support-only)” | “Offline 80% = live PASS” |
| “Live dated host suite 23/30 (76.7%) FAIL retained until new dated host run” | “Offline 80% = live PASS” or “Manifest PASS from 2026-08-03 report files without clean host rerun” |
| “Manifest PASS cites report X dated Y” | “Manifest PASS from package checks alone” |
| “Release PASS after gate + live suite + identity” | “Release PASS from local suite only” |
| “Prior FAIL retained at path Z” | Overwriting FAIL report with PASS content |
