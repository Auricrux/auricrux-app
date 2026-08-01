# AUX-027 Peer Comparison — Setup

## Reality check (founder constraints)

- Separate **OpenAI / Anthropic API** billing is optional, not required to make progress.
- A Cursor agent is **one model session** — it cannot honestly mint real ChatGPT/Claude/Gemini API transcripts without those APIs (or GitHub Models).
- GitHub Models was probed for Copilot multi-model access and returned **410 retirement brownout**.
- **Available-peer path (in use):** live Auricrux + Cursor-agent answers (+ Gemini when you paste one AI Studio key).
- Full triad AUX-027 stays **FAIL/BLOCKED** until real vendor peers exist; interim report is `eval/reports/peer_comparison_available_peers_v1.md`.

## Gemini-only (recommended next)

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
# Create key at https://aistudio.google.com/apikey — paste when prompted (skip OpenAI/Anthropic)
.\scripts\setup-peer-keys.ps1
.\scripts\run-peer-comparison.ps1
```

## Full vendor triad (optional)

| Peer | Console | Env var | GitHub secret |
|------|---------|---------|---------------|
| ChatGPT | https://platform.openai.com/api-keys | `OPENAI_API_KEY` | `PEER_OPENAI_API_KEY` |
| Claude | https://console.anthropic.com/settings/keys | `ANTHROPIC_API_KEY` | `PEER_ANTHROPIC_API_KEY` |
| Gemini | https://aistudio.google.com/apikey | `GOOGLE_API_KEY` | `PEER_GOOGLE_API_KEY` |

```powershell
.\scripts\setup-peer-keys.ps1
.\scripts\run-peer-comparison.ps1
```

## Manual `.env` alternative

```powershell
copy eval\peer-comparison.env.example eval\.peer-keys.env
notepad eval\.peer-keys.env
```

Paste keys, save, then `.\scripts\run-peer-comparison.ps1`.

## What the runner does

- Pulls a stratified sample (default 12) from `eval/construction_god_suite_v1.json`
- Asks Auricrux live + each keyed peer the same prompt
- Blind auto-scores with a judge model (identities stripped)
- Writes `eval/reports/peer_comparison_v1_report.{json,md}`

**Claim gate:** AUX-027 is not flipped to PASS automatically. Review the report first.

## CI (optional)

Workflow: `.github/workflows/peer-comparison.yml` (`workflow_dispatch`).  
Requires the three `PEER_*` secrets above.
