# Construction OS Wedge — Narrowed Scope (Google critique response)

**Updated:** 2026-07-31  
**Audience:** founders, Google/Cloud partners, investors who said the vision is too broad.

## What Google got right

Nobody has shipped a credible **construction operating system + specialist AI + Academy/CTE** stack as one product because the surface area is enormous if you try to beat Procore *and* ChatGPT *and* Autodesk at once.

Lofty “compete with every major AI + every construction SaaS” framing invites that criticism. We accept it.

## What we are proving instead (the wedge)

**Beachhead claim (narrow, falsifiable):**

> For contractors, estimators, supers, and CTE trainees: a sovereign construction OS with grounded specialist AI for field Q&A, takeoff/calc, photo→RFI intake, and Academy/CTE teaching — not a general-purpose ChatGPT clone.

Out of wedge (explicit non-goals for this proof cycle):

| Non-goal | Why deferred |
|----------|----------------|
| Flagship-peer general chat quality (AUX-027) | Needs blind peer keys; not the beachhead |
| Promoted LoRA weights live (AUX-017/018) | Founder export gate |
| UHD diffusion generative video (FCA-022) | Accepted residual |
| Full Bluebeam / Autodesk feature parity | Depth over breadth; takeoff path is usable, not clone |
| M365 Graph connectors | Sovereignty Track B stays Graph-free |

## Proof already in hand

| Proof | Artifact |
|-------|----------|
| Domain correctness 30/30 | `auricrux-app/eval/reports/construction_god_suite_v1_report.md` |
| Live capabilities matrix (honest gaps) | `GET /api/capabilities` on auricrux.* |
| Agent + calc + browse | `/api/agent`, `/api/calc`, `/api/browse` |
| Field photo → checklist + RFI draft | `POST /api/vision` |
| Public API cutover | `api.*` catalog 200 on GCP (`docs/runtime-proof/gcp-api-catalog-cutover-2026-07-31.json`) |
| Ecosystem claim lock | `fca-ecosystem/CLAIMS_REGISTER.md` (~54 PASS) |

## How we set the trend

1. **Narrow public language** to the beachhead sentence above — never “we beat ChatGPT.”
2. **Ship the field loop** end-to-end: question → corpus/agent/calc → photo intake → RFI draft → spine audit (ecosystem).
3. **Keep claims hard** in registers (no honesty-washing) while **pitching the wedge** externally.
4. **Close only engineerable residuals** (vision shipped; DualRun sole-writer; peer rubric ready). Founder gates stay labeled founder.

## One-liner for Google

We are not trying to be everything. We are proving the first sovereign **construction specialist OS + teachable AI** beachhead — and every remaining FAIL is either a named founder gate or an accepted residual, not vapor.
