# Package-to-host consistency

**Assert:** `scripts/Assert-PackageHostConsistency.ps1`  
**Token:** `PACKAGE_HOST_CONSISTENCY_OK` / `PACKAGE_HOST_CONSISTENCY_BLOCKED`  
**Receipt:** `docs/runtime-proof/package-host-consistency-latest.json`  
**Wired as:** safety gate **SG-20** (live probes)

Compares the **intended publish package** (`_publish/web`) to the **live product host**.  
**Fails loudly** when the host is stale, mismatched, or ambiguous.

## What is verified

| Check | Intended package | Live host |
|-------|------------------|-----------|
| DLL identity | `Auricrux.Web.dll` SHA + ExpandSearchTerms bytes | `packageIdentity.dllSha256` / file version |
| Search expansion | ExpandSearchTerms in publish DLL | `POST /api/search` silica synonym probe |
| Corpus files | `Data/construction-corpus.json` SHA + entries | `packageIdentity.corpusSha256` / entries |
| Configuration | `appsettings.json` PrimaryModel | health/capabilities primaryModel |
| Manifest version | publish `model_manifest.json` evalStatus | `packageIdentity.manifestEvalStatus` |
| Suite target | stamp `suiteTarget` | `packageIdentity.suiteTarget` |
| Model endpoint | — | health `ollamaReachable` + not corpus-fallback |
| Product model name | expected `auricrux-fca` | health `primaryModel` + ready |
| Env / config signals | — | `envPrimaryModelSet`, `envOllamaUrlSet`, `envPublicHostSet`, `ollamaEndpointHost` (after package deploy) |

**Absent `packageIdentity` = FAIL** (ambiguous). Emergency only: `-AllowMissingPackageIdentity`.

## Commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Write-AuricruxPackageStamp.ps1
.\scripts\Assert-PackageHostConsistency.ps1

# Emergency only
.\scripts\Assert-PackageHostConsistency.ps1 -AllowMissingPackageIdentity
```

## Related

- [AURICRUX_PACKAGE_IDENTITY.md](./AURICRUX_PACKAGE_IDENTITY.md)
- [GGUF_DEPLOYMENT_SAFETY_GATE.md](./GGUF_DEPLOYMENT_SAFETY_GATE.md)
