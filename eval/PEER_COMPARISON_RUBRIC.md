# AUX-027 — Flagship-Peer Comparative Rubric

**Claim under test:** "Class-of-its-own construction specialist quality at flagship-peer output" (AUX-027).

## Why this file exists

`eval/construction_god_suite_v1.json` proves **domain correctness** (30/30 keyword-grounded
construction answers). It does **not** prove "flagship-peer output" — that requires a **blind,
side-by-side quality comparison** against ChatGPT, Claude, and Gemini answering the *same*
prompts, judged on more than keyword presence. This rubric defines how that comparison must be
run and scored so the eventual result is auditable instead of asserted.

**No peer comparison run has been executed yet in this repository.** No third-party API keys
(OpenAI/Anthropic/Google) are configured in this dev environment, so peer answers cannot be
collected here. Until a real run exists, AUX-027 stays **FAIL** — the rubric alone is not
evidence of parity, only a methodology for producing that evidence honestly.

## Scoring rubric (0–5 per dimension, per question)

| Dimension | 0 | 3 | 5 |
|---|---|---|---|
| **Domain accuracy** | Factually wrong or fabricated code/spec numbers | Broadly correct, some hedging/vagueness | Correct, specific (cites real thresholds/standards) |
| **Actionability** | Generic platitude, no next step | States one usable next step | Structured, decision-ready (assumptions stated, missing-info called out) |
| **Safety/compliance diligence** | Omits known hazard/code caveat | Mentions hazard/caveat in passing | Explicitly flags jurisdiction variance and refuses unsafe shortcuts |
| **Field practicality** | Textbook-only, ignores real job-site constraints | Reasonable but generic | Reflects real trade sequencing/material/labor tradeoffs |
| **Concision** | Rambling or padded | Adequate length | Tight, no filler, easy to act on under time pressure |

Total per question: 0–25. A model is judged **at-parity** on a question if its total is within
2 points of the highest-scoring model among the four (Auricrux, ChatGPT, Claude, Gemini) for
that question.

## Required run procedure (not yet executed)

1. Select a fixed, blind subset of `eval/construction_god_suite_v1.json` cases (recommend all
   30, or a stratified 12-question sample covering every `category`).
2. For each case, collect one answer each from: Auricrux (`auricrux-fca` via
   `POST /api/chat`), ChatGPT (GPT-4-class), Claude (Sonnet/Opus-class), Gemini (Pro-class) —
   same prompt text, no model-identifying preamble.
3. Have a human rater (construction SME if available) score each answer blind (model identity
   hidden) using the rubric table above.
4. Compute per-model average total and count of at-parity questions.
5. Write results to `eval/reports/peer_comparison_v1_report.{json,md}` with raw scores, rater
   notes, and the verdict: **PASS** (Auricrux at-parity or ahead on ≥70% of questions) /
   **PARTIAL** (at-parity on some, clearly behind on others) / **FAIL** (behind on most).
6. Only then update `CLAIMS_REGISTER.md` AUX-027 to reflect the actual measured verdict.

## Template

`eval/peer_comparison_template.json` contains the empty scaffold (question, four blank answer
slots, four blank rubric score blocks) ready to fill in once API access to the peer models is
available in an environment permitted to call them.

## Current status

- Domain-eval evidence (keyword-grounded, self-graded): **exists**, 30/30 — see
  `eval/reports/construction_god_suite_v1_report.md`.
- Specialist wedge (narrow-scope) report: **exists** — see
  `docs/runtime-proof/specialist_wedge_v1_report.md` (answers Google "narrow the scope" without
  claiming flagship peer parity).
- Blind peer-quality comparison: **does not exist yet** — this file + the template below are the
  methodology only, not a result.
- AUX-027 verdict: stays **FAIL** per the claim's explicit "flagship-peer output" bar, which
  requires the peer comparison, not the domain suite alone.
