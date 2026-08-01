# AUX-027 Peer Comparison — checkpoint-110000 live GGUF

**Captured:** 2026-08-01T19:50:36Z
**Runtime:** ollama-live / primaryReady=true / **auricrux-fca = ckpt-110000 Q8 GGUF (3.4GB)**
**Verdict:** PARTIAL_EVIDENCE
**Auricrux at-parity:** 91.7% of 12 (bar >=70% of cursor total)
**Avg totals:** Auricrux 18.42 · Cursor-agent 22.33

## Checkpoint status
- Product Ollama now serves **merged checkpoint-110000** (not interim llama3.2 alias)
- CPU merge on train host with CUDA_VISIBLE_DEVICES empty; train PID left alone
- GGUF: `gs://auricrux-mobile-prod-model-xfer/auricrux-fca-ckpt110000-Q8_0.gguf`
- Load: https://github.com/Auricrux/auricrux-app/actions/runs/30715067119

## Quality note (honest)
- Live weights confirmed (ollama list size **3.4 GB**, create success).
- Mid-train quality still fails peer bar on several anchors (e.g. OSHA fall protection answered with wrong heights).

| Case | Auricrux | Cursor-agent |
|------|----------|--------------|
| csi-03-concrete | 16 | 23 |
| osha-fall-protection | 18 | 24 |
| estimating-takeoff | 19 | 22 |
| billing-payapp | 17 | 22 |
| scheduling-cpm | 20 | 23 |
| closeout-punchlist | 19 | 23 |
| code-egress | 20 | 21 |
| contracts-aia-a201 | 18 | 20 |
| insurance-bonding | 18 | 22 |
| sitework-erosion | 19 | 24 |
| csi-04-masonry | 18 | 20 |
| osha-trenching | 19 | 24 |

## Claim gate
- AUX-027 flagship triad: still **FAIL/BLOCKED** (no OpenAI/Anthropic API keys).
- AUX-017/018: **not PASS** — live ckpt loaded, but construction-god suite / peer bar not cleared.
