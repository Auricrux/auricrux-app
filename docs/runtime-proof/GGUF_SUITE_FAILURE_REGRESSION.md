# GGUF suite failure regression coverage

**Catalog:** `eval/gguf_suite_failure_regression_v1.json`  
**Assert:** `scripts/Assert-GgufSuiteFailureRegression.ps1` → `GGUF_SUITE_FAILURE_REGRESSION_OK`  
**Authority FAIL report:** `docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json` (23/30 = 76.7%)  
**Analysis:** `docs/GGUF_GENERATIVE_SUITE_FAILURE_ANALYSIS.md`

## Rules

- Do **not** weaken `construction_god_suite_v1` expectedKeywords.
- Do **not** remove difficult cases.
- Do **not** train to the test.
- Do **not** claim live PASS from this assert (retrieval probe ≠ generative suite PASS).

## Coverage

### Failures (7) — full regression records

Each entry includes: original failed prompt, expected retrieval / grounding / alias / scoring behavior, prior failure reason, corrections applied.

| ID | Prior class | Key correction |
|----|-------------|----------------|
| `billing-payapp` | Scoring | payapp aliases |
| `csi-05-steel` | Prompt/eval + model | grounding excerpts + rcsc/bolt aliases |
| `csi-23-hvac` | Prompt/eval + variance | grounding + manual/hvac aliases |
| `osha-confined-space` | Prompt/eval + model | grounding + atmospheric/attendant aliases |
| `osha-silica` | Prompt/eval + scoring | ExpandSearchTerms + silica corpus + respiratory aliases |
| `scheduling-delay` | Model (+ prompt) | grounding + fragnet aliases (keep hard keywords) |
| `earthwork-compaction` | Model (+ prompt) | grounding + proctor/density aliases |

### Near-failures (13)

Authority PASSes at **2/3** keywords — watchlist so they cannot silently fall below 50%.

## Commands

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-GgufSuiteFailureRegression.ps1
```

Wired as safety gate **SG-21**.
