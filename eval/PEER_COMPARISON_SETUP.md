# AUX-027 Peer API Keys — Setup

Peer keys let us collect **ChatGPT / Claude / Gemini** answers for the blind quality comparison against Auricrux. Without them AUX-027 stays blocked.

## One-command setup (Windows)

From the repo root:

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\setup-peer-keys.ps1
```

That script:

1. Opens each provider’s API-key page in your browser
2. Asks you to paste each key (hidden input)
3. Saves them to `eval/.peer-keys.env` (gitignored — never commit)
4. Optionally stores them as GitHub secrets for CI

Then run the comparison:

```powershell
.\scripts\run-peer-comparison.ps1
```

## Where to get keys

| Peer | Console | Env var | GitHub secret |
|------|---------|---------|---------------|
| ChatGPT | https://platform.openai.com/api-keys | `OPENAI_API_KEY` | `PEER_OPENAI_API_KEY` |
| Claude | https://console.anthropic.com/settings/keys | `ANTHROPIC_API_KEY` | `PEER_ANTHROPIC_API_KEY` |
| Gemini | https://aistudio.google.com/apikey | `GOOGLE_API_KEY` | `PEER_GOOGLE_API_KEY` |

**Gemini via AI Studio** is usually the fastest free start. OpenAI and Anthropic need paid API credits.

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
