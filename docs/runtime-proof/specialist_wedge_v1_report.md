# Specialist Wedge Proof v1 (Google narrow-scope response)

**Captured:** 2026-07-31  
**Related claims:** AUX-019 PASS (domain suite); AUX-027 remains FAIL (flagship peer blind run still needed)

## Why this file exists

Google advised narrowing scope vs. “beat ChatGPT.” This report documents the **narrow beachhead proof** we already have:

> Construction-specialist accuracy on a fixed, auditable suite — without claiming general flagship-peer chat quality.

This is **not** a substitute for AUX-027. It is the honest wedge evidence to answer “why hasn’t anyone done this?” with “here is the construction beachhead that is already measurable.”

## Evidence

| Metric | Result | Source |
|--------|--------|--------|
| Construction god suite | **30/30 (100%)** | `eval/reports/construction_god_suite_v1_report.json` |
| Corpus depth | **80 entries / 11 categories** | `GET /api/capabilities` → `corpusStats` |
| Live capabilities | vision + agent + calc + browse shipped | `CapabilitiesService` |
| Peer blind comparison | **not run** (no OpenAI/Anthropic/Google keys) | `eval/PEER_COMPARISON_RUBRIC.md` |

## Verdict for pitch / Google

- **Wedge claim:** PASS — specialist construction correctness is proven on the locked suite.
- **General AI peer claim (AUX-027):** FAIL until a real blind peer run exists.

## Next when keys exist

Execute `eval/PEER_COMPARISON_RUBRIC.md` → write `eval/reports/peer_comparison_v1_report.{json,md}` → only then flip AUX-027.
