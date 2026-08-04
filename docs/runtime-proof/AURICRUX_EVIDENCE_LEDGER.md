# Auricrux evidence ledger (GGUF suite)

**Ledger:** `docs/runtime-proof/auricrux_evidence_ledger_v1.json`  
**Append-only trail:** `docs/runtime-proof/auricrux_evidence_ledger_v1.jsonl`  
**Writer:** `scripts/Write-GgufSuiteEvidenceLedger.ps1`  
**Integrity assert:** `scripts/Assert-EvidenceLedgerIntegrity.ps1` → `EVIDENCE_LEDGER_INTEGRITY_OK`  
**Enforced after:** live suite (`run-gguf-construction-suite.ps1`) when safety gate passes  
**Policy:** [AURICRUX_EVIDENCE_RULES.md](./AURICRUX_EVIDENCE_RULES.md) (assert: `scripts/Assert-AuricruxEvidenceRules.ps1`)  
**Authority map:** [AURICRUX_AUTHORITY_MAP.md](./AURICRUX_AUTHORITY_MAP.md)

## Rules

- Append chronologically; never overwrite prior FAIL or PASS row scores/status.
- Suite reports use unique UTC stamps (`yyyy-MM-ddTHHmmssZ`); never clobber prior report files.
- Offline alias rescore is support-only and is **not** ledger authority (writer refuses offline modes).
- Live dated host validation rows are recorded for history; **only qualifying** live evidence may update `currentLiveAuthority` to PASS.
- Disqualified PASS (fallback contamination, missing `packageIdentity`, non-product host) is retained historically and listed under `supersededEvidence`.
- Manifest PASS requires dated **qualified** live validation; scores must not be upgraded without a new live rerun.
- Prior failures remain historically preserved.
- **Naive "latest PASS wins" is forbidden.** Authority derives only from qualifying evidence.

## Authority derivation

`currentLiveAuthority` may become **PASS** only when **all** are true:

1. `mode=gguf-generative-product-chat`
2. Product-host `baseUrl`
3. `suitePassed` and rate ≥ 80
4. Zero fallback / "no live model reachable" excerpts
5. Live `packageIdentity` present on the report
6. Ledger append with `authority=live-dated-host-validation-qualified` (or equivalent stamp)

Otherwise PASS scores are history only (`supersededEvidence` / AUTHORITY-CORRECTION). Offline evidence never elevates.

## Entry fields

dateUtc, host, modelName, suiteName, suiteVersion, suiteTarget, packageHashSha256, dllHashSha256, packageVersion, buildTimestampUtc, corpusSha256, dllSha256Live, stampFilePresent, hostReported, manifestEvalStatus, manifestModelId, evidenceLedgerPath, livePackageIdentity, totalScorePercent, perDomainScores, failedPrompts, recoveredPrompts, thresholdPercent, status (PASS/FAIL/AUTHORITY-CORRECTION), safetyGateToken, reportPath, runAtUtc, authority, authorityQualifiedPass (when present).

Ledger root also carries: `currentLiveAuthority`, `supersededEvidence`, `authorityDerivation`.

## Operator check

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-EvidenceLedgerIntegrity.ps1
.\scripts\Assert-AuricruxEvidenceRules.ps1
.\scripts\Assert-AuricruxAuthorityMap.ps1
```

If historical PASS rows exist without an explicit supersession index:

```powershell
.\scripts\Assert-EvidenceLedgerIntegrity.ps1 -RepairSupersessionIndex
```

Repair appends an AUTHORITY-CORRECTION + `supersededEvidence` index. It does **not** delete or rewrite FAIL/PASS scores.
