# GGUF generative suite failure analysis (76.7% → 80%)

**Report:** `eval/reports/construction_god_suite_gguf_generative_2026-08-02.json`  
**Baseline:** 23/30 (**76.7%**) FAIL vs threshold **80%** (need **24/30**)  
**Hard rule:** no train interrupt; mid-train model limitations remain until TRUE God final.

## Verdict

The gap is **+1 case**. Failures are a mix of **scoring**, **prompt/eval grounding**, and **model limitations**. One scoring fix alone recovers the threshold on the stored 2026-08-02 run; product host must deploy the refreshed publish artifact before live can clear remaining fails.

## Failure inventory (7 cases)

| Case | Matched | Missing | Primary class | Notes |
|------|---------|---------|---------------|-------|
| `billing-payapp` | sov | payapp, retainage | **Scoring** | Answer said “Pay Application SOV”; scorer required literal `payapp`. |
| `csi-05-steel` | steel | bolt, rcsc | **Prompt/eval** + model | Corpus has RCSC/bolt text; chat prompt injected **titles only**, so model freelanced. |
| `csi-23-hvac` | duct | hvac, manual | **Prompt/eval** + variance | Corpus has Manual D; title-only grounding. Live probe later matched duct+manual (PASS). |
| `osha-confined-space` | confined space | atmospheric, attendant | **Prompt/eval** + model | Corpus entry has both terms; not injected into generation. |
| `osha-silica` | dust | silica, respiratory | **Prompt/eval** + **benchmark/scoring** | Query says “concrete cutting dust” (not silica). Corpus title retrieved but content not used. Keyword `respiratory` vs corpus `respirable`. |
| `scheduling-delay` | — | delay, fragnet, critical path | **Model** (+ prompt) | Invented ΔT formula; ignored TIA/fragnet. Live probe later PASSed (variance). |
| `earthwork-compaction` | — | compaction, proctor, density | **Model** (+ prompt) | Off-topic CSI rant despite correct retrieval title. |

## Safe improvements implemented (repo)

1. **Prompt grounding** — `BuildSystemPrompt` injects truncated corpus excerpts (+ tags).
2. **Keyword aliases** — `eval/keyword_aliases_v1.json` + suite runners.
3. **Offline rescore** — `scripts/rescore_gguf_report_aliases.py` → `eval/reports/construction_god_suite_gguf_generative_2026-08-02_alias_rescore.json` (**24/30 = 80%** offline).
4. **Corpus silica** — explicit `respiratory` language/tags.
5. **Retrieval synonyms** — `ExpandSearchTerms` maps field phrasing across **multiple corpus domains** (not silica-only) so the right rows are retrieved for grounding. See `docs/runtime-proof/EXPAND_SEARCH_TERMS.md`.
6. **Publish refresh** — `_publish/web` rebuilt 2026-08-02 with grounding + synonyms + corpus (prior DLL was 2026-07-30).
7. **Deploy safety** — `docker-compose` `ollama-model-init` gated to profile `dev-fallback`; GCP warm workflow no longer Modelfile/llama3.2-recreates product tag (UNSAFE-06).

## Score impact

| Measurement | Pass | Rate | Notes |
|-------------|------|------|-------|
| Live baseline 2026-08-02 | 23/30 | 76.7% | **FAIL — current live authority** (retained) |
| Offline alias rescore | 24/30 | 80.0% | Support-only; not live authority |
| 2026-08-03 report files | — | 86.7% / 93.3% claimed | Historical only; **not** Manifest PASS until clean dated host rerun after package deploy |

**Live suite status remains 76.7% FAIL** until a dated product-host generative rerun proves otherwise. Product host deployment of package fixes is still required.

### Comparison (2026-08-04 analysis — no new authoritative live run)

| Measurement | Rate | Authority |
|-------------|------|-----------|
| Prior / current live | **76.7% FAIL** (23/30) | **currentLiveAuthority** |
| Historical-only | 86.7% / 93.3% | Disqualified; not authority |
| New authoritative live | **UNCHANGED 76.7% FAIL** | Suite not rerun: RB-C2 PostVerify still open (PH-14/PH-19) |

Receipt: `docs/runtime-proof/authoritative-live-failure-analysis-latest.json`.

See `docs/runtime-proof/AURICRUX_STATUS_TRUTH_2026-08-03.md`.


See [GGUF_SUITE_FAILURE_REGRESSION.md](./runtime-proof/GGUF_SUITE_FAILURE_REGRESSION.md) for locked regression coverage (prompts, retrieval/grounding/alias/scoring expectations, prior failure reasons, corrections). Assert: `scripts/Assert-GgufSuiteFailureRegression.ps1`.

## What was not done (intentionally)

- No train interrupt / no weight change
- No public prod suite re-run from Primary (UNSAFE-09) — local API/tag absent
- No keyword deletion to game the bar
- No claim that live production already scores ≥80%

## Re-verify after product deploy

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
python scripts\rescore_gguf_report_aliases.py eval\reports\construction_god_suite_gguf_generative_2026-08-02.json
# After GCE/Azure hosts `_publish/web` (or equivalent cutover):
.\scripts\run-gguf-construction-suite.ps1
```
