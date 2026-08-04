# Auricrux package identity (stale-package safeguard)

Operators must be able to tell whether a **live suite result** came from the **intended package** on the product host.

## What is reported

| Signal | Source | Where operators see it |
|--------|--------|------------------------|
| Package version stamp | `auricrux/system/package_stamp.json` + assembly version | `/api/capabilities` → `packageIdentity.packageVersion`, `/api/health` |
| Build timestamp | stamp `buildTimestampUtc` (refreshed at Docker image build) | `packageIdentity.buildTimestampUtc` |
| DLL version | `FileVersion` / `InformationalVersion` + SHA256 of running DLL | `dllFileVersion`, `dllInformationalVersion`, `dllSha256` |
| Corpus version | SHA256 + entry count of `construction-corpus.json` | `corpusSha256`, `corpusEntries` |
| Suite target | stamp + suite id | `suiteTarget`, `suiteVersion`, `suitePath` |
| Host | request host / public host | `hostReported` |
| Manifest linkage | `model_manifest.json` | `manifestEvalStatus`, `manifestModelId`, `manifestGgufGenerativeReport` |
| Evidence ledger linkage | fixed ledger paths | `evidenceLedgerPath`, `evidenceLedgerJsonlPath` |
| Runtime truth (ops) | model/package/DLL/corpus/host/recipe/suite/fallback | `GET /api/runtime-truth` (alias `/api/truth`) — see [RUNTIME_TRUTH.md](./RUNTIME_TRUTH.md) |

Runtime service: `Auricrux.Web/Services/PackageIdentityService.cs`  
Truth service: `Auricrux.Web/Services/RuntimeTruthService.cs`  
Stamp writer: `scripts/Write-AuricruxPackageStamp.ps1` (also MSBuild on Windows before build/publish)

## Operator check (is the host current?)

```powershell
# Strict package-to-host consistency (FAIL on stale / mismatched / ambiguous)
.\scripts\Assert-PackageHostConsistency.ps1

# 1) Live identity
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/capabilities |
  Select-Object -ExpandProperty packageIdentity |
  Format-List packageVersion, buildTimestampUtc, dllSha256, corpusSha256, suiteTarget, hostReported, stampFilePresent, manifestEvalStatus, primaryModel, ollamaEndpointHost

# 1b) Operational truth (model/package/DLL/corpus/host/recipe/suite/fallback)
.\scripts\Assert-RuntimeTruth.ps1
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/runtime-truth | Format-List

# 2) Local intended package (after publish)
.\scripts\Write-AuricruxPackageStamp.ps1
Get-FileHash -Algorithm SHA256 _publish\web\Data\construction-corpus.json

# 3) Full safety gate (includes SG-20 consistency + SG-22 runtime truth)
.\scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1
```

**Match rule (authoritative for stale detection):** live `corpusSha256` must equal publish `Data/construction-corpus.json` SHA256 when both are present. DLL SHA may differ across Windows publish vs Linux container rebuild; treat DLL compare as supporting unless version identity is also missing.

**Absent `packageIdentity` on host:** `Assert-PackageHostConsistency.ps1` **FAIL**s (ambiguous). The broader safety gate SG-16 may still WARN for transitional workflow, but SG-20 blocks authority runs until identity is present or `-AllowMissingPackageIdentity` is used (emergency only).

See [PACKAGE_HOST_CONSISTENCY.md](./PACKAGE_HOST_CONSISTENCY.md).  
Cross-artifact drift (publish/corpus/manifest/ledger/runtime/deploy): [OPERATIONAL_DRIFT.md](./OPERATIONAL_DRIFT.md) · `Assert-OperationalDrift.ps1`.

## Suite + ledger linkage

- `run-gguf-construction-suite.ps1` embeds `packageIdentity` into the dated suite JSON/MD.
- `Write-GgufSuiteEvidenceLedger.ps1` copies version, build UTC, corpus/DLL hashes, host, suite target, and manifest fields into each ledger row.

A live PASS/FAIL row is only interpretable when `packageIdentity` (or at least corpus SHA + host + runAtUtc) is on that report/ledger entry.

Evidence authority (offline vs live, Manifest PASS, Release PASS): [AURICRUX_EVIDENCE_RULES.md](./AURICRUX_EVIDENCE_RULES.md).

## Preserve workflows

- Existing gate → suite → ledger path unchanged.
- `-SkipSafetyGate` still available for emergency only.
- Pre-cutover hosts without `packageIdentity` do not hard-block the suite (WARN only).
