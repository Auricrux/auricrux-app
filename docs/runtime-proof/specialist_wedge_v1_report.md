# Specialist Proof v1 — Forward evidence (not a retreat)

**Captured:** 2026-07-31  
**Related claims:** AUX-019 PASS (domain suite); AUX-027 remains FAIL until blind peer run

## Why this file exists

Google said the scope is too broad. We keep the ambitious claim and publish measurable proof of specialist construction quality already in hand — then keep closing remaining gaps (vision shipped; weights + peer blind still open).

## Evidence

| Metric | Result | Source |
|--------|--------|--------|
| Construction god suite | **30/30 (100%)** | `eval/reports/construction_god_suite_v1_report.json` |
| Corpus depth | **80 entries / 11 categories** | `GET /api/capabilities` → `corpusStats` |
| Live capabilities | vision + agent + calc + browse shipped | `CapabilitiesService` |
| Peer blind comparison | **not run** (no OpenAI/Anthropic/Google keys) | `eval/PEER_COMPARISON_RUBRIC.md` |

## Verdict

- Specialist domain correctness: **PASS** (30/30).
- Flagship peer quality (AUX-027): **FAIL** until blind peer run — claim stays hard; we do not mark it PASS early.
- Stance: prove the full bar; do not redefine it down.

## Next when keys exist

Execute `eval/PEER_COMPARISON_RUBRIC.md` → write peer report → only then flip AUX-027.
