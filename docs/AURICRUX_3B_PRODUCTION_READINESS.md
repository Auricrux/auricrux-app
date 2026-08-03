# Auricrux-3B Production Readiness

**Generated after in-place defect fixes (2026-08-02).**  
**Hard rule:** do not interrupt live TRUE God train PID / token factory.

## Verdict

**NOT TRUE God production-ready — known gaps remain.**

| Lane | Status |
|------|--------|
| Mid-train LoRA GGUF in product Ollama | Live (ckpt tip per manifest) |
| Honesty artifacts in repo | Fixed (deploy still required for live `/api/capabilities`) |
| GGUF generative suite (2026-08-02) | **23/30 (76.7%) FAIL** vs 80% threshold |
| Alias-only offline rescore of that report | **24/30 (80.0%)** — see `docs/GGUF_GENERATIVE_SUITE_FAILURE_ANALYSIS.md` (not live PASS until redeploy + dated run) |
| Train finish 297k + post-run gate | Open — do not interrupt PID |
| AUX-027 peer bar | BLOCKED |

Do not call this production-ready TRUE God while generative suite fails and train is unfinished.

## Defects fixed in this pass (repo)

| Defect | Fix |
|--------|-----|
| Capabilities claimed llama3.2 alias / ckpt-70000 | `CapabilitiesService` reads `model_manifest.json`; reports mid-train GGUF live as `partial` |
| Manifest `migrationPolicy` targeted obsolete 70000 | Updated to 297k + post-run + GGUF generative eval |
| Modelfile looked like product recipe | Bannered **DEV FALLBACK ONLY** |
| Missing model card | `auricrux/system/AURICRUX_3B_MODEL_CARD.md` |
| Missing GGUF generative harness | `scripts/run-gguf-construction-suite.ps1` |
| Missing honesty validator | `scripts/validate-auricrux-3b-honesty.ps1` |
| CLAIMS AUX-017/018/019 stale | AUX-017 PASS (weights path); AUX-018 BLOCKED final; AUX-019 PARTIAL |
| Tests asserted alias/blocked | `EnterpriseReadinessTests` updated |
| Manifest not copied to output | `Auricrux.Web.csproj` Content include |

## Remaining hard gates (cannot fake)

1. Train finish **297000** / `final_adapter` (PID live — do not interrupt)
2. Post-run gate `72_true_god_post_run_gate` PASS
3. Final CPU-only GGUF export → product Ollama
4. GGUF generative suite ≥80% on **final** tip (script + dated report)
5. AUX-027 peer/SME bar (or founder waiver)
6. Deploy honesty fix so live `/api/capabilities` matches repo
7. Apply model-lab `STATUS.json` train-progress sync under Auricrux ACL

## How to re-verify

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\validate-auricrux-3b-honesty.ps1 -LocalArtifactsOnly
dotnet test Auricrux.Tests -c Release --filter FullyQualifiedName~EnterpriseReadinessTests
.\scripts\run-gguf-construction-suite.ps1
# After deploy:
.\scripts\validate-auricrux-3b-honesty.ps1 -BaseUrl https://auricrux.futurecontractorsofamerica.com
```

## Artifact index

- Model card: `auricrux/system/AURICRUX_3B_MODEL_CARD.md`
- Manifest (canonical): `auricrux/system/model_manifest.json`
- Suite: `eval/construction_god_suite_v1.json`
- GGUF reports: `eval/reports/construction_god_suite_gguf_generative_*.json`
- CLAIMS: `CLAIMS_REGISTER.md`
- Superseded alias proof: `auricrux/system/auricrux-fca-ollama-show-proof.json` (do not treat as live)
- Dated capability JSON under `docs/runtime-proof/` is point-in-time — see `docs/runtime-proof/README.md`
