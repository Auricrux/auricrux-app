# Auricrux-3B Model Card

**Family label:** Auricrux-3B (TRUE God train lane)  
**Product Ollama tag:** `auricrux-fca`  
**Updated:** 2026-08-02  

## Current production cutover (honest)

| Field | Value |
|-------|--------|
| Status | Mid-train product cutover — **not** TRUE God final |
| Base | `unsloth/Llama-3.2-3B-Instruct-bnb-4bit` |
| Adapter tip (observed) | `checkpoint-120000` / 297000 (~40.4%) |
| Product weights | Merged LoRA → Q8_0 GGUF (`auricrux-fca-ckpt120000-Q8_0.gguf`) |
| Train | PID live — **do not interrupt**; do not tamper token factory |
| Manifest | `auricrux/system/model_manifest.json` |

## Intended use

Construction specialist assist in Auricrux App (chat, corpus-grounded search, thinking modes, bounded agent tools). Not a general coding agent (`auricrux-coder` is separate).

## Training data (live mix)

Referenced by `config/auricrux_training_config_3b_true_god_1b5.json` (model-lab):

- `local_token_factory_true_god_sanitized_v1.jsonl`
- `auricrux_true_god_core_principles_v1.jsonl`
- `auricrux_3b_retrain_pack_v1.jsonl`
- `auricrux_fca_construction_expansion_pack_v1.jsonl`

Growth packs are prepared for **next resume only** after live PID ends.

## Evaluation

| Suite | Path | Status |
|-------|------|--------|
| Corpus/DI construction god | `eval/construction_god_suite_v1.json` | 30/30 PASS (2026-07-28) — not GGUF generative proof |
| GGUF generative | `scripts/run-gguf-construction-suite.ps1` (+ `-ResumeFromReport`) | **23/30 (76.7%) FAIL** vs 80% on 2026-08-02 — report `eval/reports/construction_god_suite_gguf_generative_2026-08-02.json`; failure analysis + safe fixes in `docs/GGUF_GENERATIVE_SUITE_FAILURE_ANALYSIS.md` |
| TRUE God post-run gate | model-lab `72_true_god_post_run_gate.sh` | Not run — train unfinished |
| Peer triad (AUX-027) | `eval/PEER_COMPARISON_*` | BLOCKED — peer API keys / SME not provided |

## Known limitations / failure modes

- Mid-train tip can still miss domain anchors (e.g. OSHA heights) — do not claim flagship-peer quality.
- Must not invent building-code section numbers as binding law.
- Dev Modelfile alias (`Modelfile.auricrux-fca`) must not overwrite product GGUF.

## Promotion gates (TRUE God production-ready)

1. Train reaches 297k / `final_adapter`
2. Post-run gate PASS (G2 + TRUE broad)
3. Final CPU-only GGUF export → product Ollama
4. GGUF generative suite ≥80% dated report
5. Peer / SME bar for AUX-027 (or documented founder waiver)
6. Capabilities + CLAIMS honesty already synced to manifest

## Non-claims

- Not Auricrux-5B / Auricrux-X
- Not finished TRUE God solely because mid-train GGUF is loaded
