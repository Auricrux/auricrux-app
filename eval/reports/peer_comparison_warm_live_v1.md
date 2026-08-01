# AUX-027 Peer Comparison — Warm Live Alias

**Runtime:** ollama-live / primaryReady=true (uricrux-fca interim alias)
**Verdict:** PARTIAL_EVIDENCE
**Auricrux at-parity:** 8.3% of 12
**Avg totals:** Auricrux 13.83 · Cursor-agent 22.33

## Checkpoint status
- Latest train disk: **checkpoint-110000** (train still running)
- Staged for export: `/mnt/auricrux-eod/export-staging/checkpoint-110000`
- Product Ollama still serves interim llama3.2 alias — **not** fine-tune weights yet
- `auricrux-export-clean` has **no GPU**; merge must use another GPU without killing train PID

| Case | Auricrux | Cursor-agent |
|------|----------|--------------|
| csi-03-concrete | 14 | 23 |
| osha-fall-protection | 23 | 24 |
| estimating-takeoff | 18 | 22 |
| billing-payapp | 10 | 22 |
| scheduling-cpm | 20 | 23 |
| closeout-punchlist | 10 | 23 |
| code-egress | 10 | 21 |
| contracts-aia-a201 | 10 | 20 |
| insurance-bonding | 16 | 22 |
| sitework-erosion | 11 | 24 |
| csi-04-masonry | 9 | 20 |
| osha-trenching | 15 | 24 |
