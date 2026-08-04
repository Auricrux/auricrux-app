# Authoritative live GGUF suite rerun package

**packageId:** `authoritative-suite-rerun-package-20260803T233652Z`
**Verdict:** `GO-WITH-BLOCKERS`
**Token:** `AUTHORITATIVE_SUITE_RERUN_GO_WITH_BLOCKERS`
**At UTC:** 2026-08-03T23:36:52.3442989Z

**Reason:** Offline package + rules + rollback ready; live host/gate blockers remain (RB-C2/RB-C3). Do not run authoritative suite yet.

**Suite executed:** false (prereq package only)

## Summary: PASS=25 FAIL=3 WARN=1 (hardFail=0 liveFail=3)

## Area results
| Area | Result |
|------|--------|
| deployment-package | PASS (P=6 W=0 F=0) |
| target-host | FAIL (P=2 W=0 F=2) |
| authority-rules | PASS (P=2 W=0 F=0) |
| manifest-rules | PASS (P=3 W=0 F=0) |
| ledger-rules | PASS (P=3 W=0 F=0) |
| safety-gates | FAIL (P=4 W=1 F=1) |
| rollback-package | PASS (P=5 W=0 F=0) |

## Active named blockers
- **B3** Operator package-web cutover not executed - closure: Follow RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md; gh workflow run gcp-cutover-build-auricrux.yml -f action=full
- **B2** Intended package not on product host - closure: Same cutover as B3; then PACKAGE_HOST_CONSISTENCY_OK + runtime-truth 200
- **B1** Host lacks packageIdentity - closure: Deploy package with PackageIdentityService + stamp
- **B4** Live safety gate blocks suite entry - closure: Clear B1-B3; Assert-GgufSuiteDeploymentSafetyGate.ps1 without SkipLiveProbes

## Failures
- **SR-22-package-host** [target-host/live]: PACKAGE_HOST_CONSISTENCY_BLOCKED
- **SR-23-runtime-truth** [target-host/live]: HTTP 404 - package cutover required (RB-C2)
- **SR-64-live-gate** [safety-gates/live]: DEPLOYMENT_SAFETY_GATE_BLOCKED

## Next operator action
Close live blockers (RB-C2 cutover -> RB-C3 live gate OK), re-run this package to GO, then run suite. Forbidden: -SkipSafetyGate for authority.

## When verdict is GO
```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\run-gguf-construction-suite.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com
```
Do **not** use `-SkipSafetyGate` for authority. Manifest update only after qualified live PASS.

## Explicit non-claims
- Live suite was **not** run
- Does **not** clear RB-C1 / Manifest PASS by itself
- GO-WITH-BLOCKERS is **not** authorization to run the suite
