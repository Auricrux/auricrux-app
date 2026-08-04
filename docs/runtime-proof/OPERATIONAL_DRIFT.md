# Operational drift checks

**Policy:** `auricrux/system/auricrux_operational_drift_v1.json`  
**Assert:** `scripts/Assert-OperationalDrift.ps1`  
**Tokens:** `OPERATIONAL_DRIFT_OK` · `OPERATIONAL_DRIFT_WARN` · `OPERATIONAL_DRIFT_BLOCKED`

Detects divergence among **intended local package**, **evidence**, and optional **live host** — without tribal knowledge.

## What is checked

| Class | Hard FAIL (high confidence) | WARN (soft / expected) |
|-------|-----------------------------|-------------------------|
| Stale publish package | Repo vs `_publish` stamp version mismatch; prepared DLL SHA ≠ publish DLL | Re-stamp after prepare; deploymentSource text drift |
| Stale corpora | Source vs publish corpus SHA mismatch (both present) | Source missing, publish only |
| Stale manifests | Repo vs publish eval/rate diverge; Manifest PASS while ledger FAIL | Report path leaf differs but both FAIL same rate |
| Stale ledgers | JSON↔JSONL id gap; authority report file missing | Soft age lag vs newest suite report |
| Stale runtime versions | `suiteTarget` mismatch repo vs publish | Live version behind publish (`-ProbeLive`) |
| Stale deployment artifacts | Prepared claims cutover done but host identity missing/mismatch | Pre-cutover host without identity (expected) |

## False-positive controls

1. Live host missing `packageIdentity` while `liveCutoverExecuted=false` → **WARN**, not FAIL.  
2. Absolute age alone never FAILs unless `-StrictAge`.  
3. Honest Manifest FAIL aligned with ledger FAIL → **PASS**.  
4. Live probes off by default (`-ProbeLive` to enable).  
5. DLL-only Windows vs Linux difference → **WARN** unless version/corpus also diverge.

## Commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app

# Local drift only (default; low false positives)
.\scripts\Assert-OperationalDrift.ps1

# Include live host compare
.\scripts\Assert-OperationalDrift.ps1 -ProbeLive

# Age soft signals become FAIL
.\scripts\Assert-OperationalDrift.ps1 -StrictAge
```

## Related

- [PACKAGE_HOST_CONSISTENCY.md](./PACKAGE_HOST_CONSISTENCY.md)
- [AURICRUX_PACKAGE_IDENTITY.md](./AURICRUX_PACKAGE_IDENTITY.md)
- [AURICRUX_EVIDENCE_LEDGER.md](./AURICRUX_EVIDENCE_LEDGER.md)
- [CUTOVER_GO_NO_GO_CHECKLIST.md](./CUTOVER_GO_NO_GO_CHECKLIST.md)
