# FCA Ecosystem & Auricrux — Agent Handoff (2026-08-11)

## FOR THE NEXT AGENT: Read This First

This document tells you everything you need to pick up work on FCA Ecosystem and Auricrux from any machine. All code is in GitHub. All state is verifiable from Azure CLI and live endpoints.

---

## GitHub Repositories

| Repo | URL | Last Commit | CI Status |
|------|-----|-------------|-----------|
| **FCA Ecosystem** | https://github.com/Auricrux/fca-ecosystem | `3fb6d708` | ✅ CI passing |
| **Auricrux App** | https://github.com/Auricrux/auricrux-app | `5f979f0` | ✅ Build passing |

### Clone commands (new machine)
```bash
git clone https://github.com/Auricrux/fca-ecosystem.git C:\repos\fca-ecosystem-reconcile
git clone https://github.com/Auricrux/auricrux-app.git C:\repos\auricrux-app
```

---

## Live System Status (verified 2026-08-11T03:30Z)

| System | Status | URL / Location |
|--------|--------|----------------|
| Auricrux App production | ✅ LIVE v1.3.0 | https://auricrux.futurecontractorsofamerica.com |
| auricrux-fca Ollama model | ✅ LIVE (smoke tested) | http://20.230.87.21:11434 |
| FCA Ecosystem web | ✅ LIVE | https://futurecontractorsofamerica.com |
| FCA API (staging) | ✅ LIVE | https://fca-ecosystem-api-stg.azurewebsites.net |
| Training run PID 1019003 | ✅ RUNNING | Azure VM `auricrux-gpu-ncast4-t4` (AURICRUX-TRAINING-NCAST4) |
| Latest checkpoint | **180,000** (Aug 10 23:27) | 60.7% through epoch |
| GPU utilization | **92%**, 13176/16384 MiB | T4 16GB |

---

## CRITICAL: What MUST NOT Be Touched

```
⛔ Training PID 1019003 on auricrux-gpu-ncast4-t4 — DO NOT INTERRUPT
⛔ auricrux-fca product model tag — DO NOT OVERWRITE
⛔ Token factory config — DO NOT TAMPER (/mnt/auricrux-eod/runs/...)
⛔ PostgreSQL database — Atlas is additive, never replaces PG
⛔ MinIO buckets — Atlas is additive, never replaces MinIO
```

---

## The One Remaining Action: Activate MongoDB Atlas

Everything is coded and committed. Atlas activates with ONE command once you have the connection string.

### Step 1 — Get the connection string
1. Go to https://cloud.mongodb.com (sign in with the FCA/Auricrux MongoDB account)
2. The $5,000 in startup credits should be applied to this account
3. Create cluster if needed: M30 tier, Azure East US 2, name: `auricrux-prod`
4. Connect → Drivers → copy `mongodb+srv://user:pass@cluster.mongodb.net/...`

### Step 2 — Run the activation script (from fca-ecosystem repo)
```powershell
cd C:\repos\fca-ecosystem-reconcile
.\scripts\ops\activate-atlas.ps1 -ConnectionString "mongodb+srv://YOUR_USER:YOUR_PASS@auricrux-prod.xxxxx.mongodb.net/?retryWrites=true&w=majority"
```

This script automatically:
- Writes `.env` to fca-ecosystem and auricrux-app
- Sets Azure Web App settings for staging
- Sets GitHub repository secrets for CI/CD
- Runs audit, backs up GGUF to Atlas GridFS, seeds construction corpus

### Step 3 — Stream the 22GB growth pack to Atlas (unblocks token factory)
```powershell
cd C:\repos\fca-ecosystem-reconcile\tools\atlas-pipeline
pip install -r requirements.txt
$env:ATLAS_CONNECTION_STRING = "mongodb+srv://..."
$env:AZURE_VM_HOST = "20.230.87.21"
$env:AZURE_VM_USER = "azureuser"
python ingest_training_dataset.py --azure-vm-path /mnt/auricrux-eod/next-run-datasets/growth_10b_net_new_v1.jsonl --domain training
```

### Step 4 — Trigger CI deploy for fca-ecosystem
```bash
gh workflow run "Deploy Staging" --repo Auricrux/fca-ecosystem
```

---

## Azure Infrastructure

| Resource | Type | Resource Group | Status |
|----------|------|----------------|--------|
| `auricrux-gpu-ncast4-t4` | VM (NC4as T4) | AURICRUX-TRAINING-NCAST4 | **Running** |
| `auricrux-export-clean` | VM | AURICRUX-TRAINING-NCAST4 | **Running** |
| `auricrux-gpu-vm` | VM | RG-AURICRUX-ML | **Running** |
| `auricrux-llm-vm-01` | VM | AURICRUX-VM-RG-EUS2 | Deallocated |
| `fca-ecosystem-api-stg` | App Service | rg-fca-ecosystem-staging | Live |
| `fca-ecosystem-api-estimating` | App Service | rg-fca-ecosystem-staging | Live |

```bash
# Verify training VM live (no SSH key needed)
az vm run-command invoke --resource-group AURICRUX-TRAINING-NCAST4 --name auricrux-gpu-ncast4-t4 --command-id RunShellScript --scripts "ps aux | grep python | grep -v grep | head -3 && nvidia-smi --query-gpu=utilization.gpu,memory.used --format=csv,noheader"

# Check latest checkpoint
az vm run-command invoke --resource-group AURICRUX-TRAINING-NCAST4 --name auricrux-gpu-ncast4-t4 --command-id RunShellScript --scripts "ls -lt /mnt/auricrux-eod/runs/run-20260715T114454Z/outputs/auricrux_lora_adapter_3b_true_god_1b5/ | head -5"
```

---

## What Was Implemented (This Session)

### MongoDB Atlas Integration (fca-ecosystem)
- `IAtlasKnowledgeStore` + `AtlasKnowledgeStore` — full-text + vector search
- `AtlasRagAuricruxProvider` — RAG decorator over Ollama
- 4 API endpoints: `/api/v1/auricrux/knowledge/*`
- Conditional DI: Atlas only when `Atlas:ConnectionString` is set
- `AtlasRagStatusPanel.tsx` — frontend health indicator
- `tools/atlas-pipeline/`: audit, preserve_model, migrate_azure_vm, ingest, ingest_training_dataset, export_training_set
- `scripts/ops/activate-atlas.ps1` — one-command full activation

### MongoDB Atlas Integration (auricrux-app)
- `AtlasService.cs` — unified MongoDB client (corpus, memory, model_routes, feedback)
- `AuricruxModelRouter.cs` — 5-tier staged intelligence:
  - Primary: auricrux-fca (3B) — most queries
  - Secondary: llama3.2 (3B) — simple/fallback
  - Tertiary: mistral (7B) — complex reasoning
  - Extended: llama3.1:70b — delay claims, agent tasks
  - Vision: llava — blueprints, images
- `AtlasCorpusService.cs` — Atlas Search-backed corpus (fallback: local JSON)
- `ConversationMemoryService.cs` — 4 backends: Session/JSONL/SQLite/Atlas
- `ConstructionIntelligenceService.cs` — integrated router + Atlas corpus

### CI Fixes (auricrux-app)
- csproj stamp target: cross-platform (powershell on Windows, pwsh on Linux), non-fatal
- `AuricruxModelRouter.cs`: added `using MongoDB.Driver`
- `ConstructionKnowledgeEntry`: moved from private to public for cross-service access

---

## Key Files by Location

### fca-ecosystem (`C:\repos\fca-ecosystem-reconcile` or GitHub)
```
apps/api/FcaEcosystem.Application/Auricrux/IAtlasKnowledgeStore.cs
apps/api/FcaEcosystem.Infrastructure/Auricrux/AtlasKnowledgeStore.cs
apps/api/FcaEcosystem.Infrastructure/Auricrux/AtlasRagAuricruxProvider.cs
apps/api/FcaEcosystem.Infrastructure/DependencyInjection.cs        (Atlas conditional DI)
apps/api/FcaEcosystem.Api/Controllers/AuricruxController.cs        (4 new endpoints)
apps/web/src/features/auricrux-orchestration/services/atlasRagService.ts
apps/web/src/components/AtlasRagStatusPanel.tsx
tools/atlas-pipeline/audit.py                  — inventory all assets
tools/atlas-pipeline/preserve_model.py         — backup GGUF to Atlas GridFS
tools/atlas-pipeline/migrate_azure_vm.py       — read-only Azure VM → Atlas
tools/atlas-pipeline/ingest.py                 — PDF/TXT → Atlas chunks
tools/atlas-pipeline/ingest_training_dataset.py — stream 22GB+ JSONL
tools/atlas-pipeline/export_training_set.py    — Atlas → JSONL for fine-tune
tools/atlas-pipeline/requirements.txt
scripts/ops/activate-atlas.ps1                 — ONE-COMMAND ATLAS ACTIVATION
docs/ATLAS_DEPLOYMENT_GUIDE.md                 — detailed deployment guide
.env.example                                   — env var template
infra/k8s/api-deployment.yaml                  — optional Atlas secret
.github/workflows/deploy-staging.yml          — CI with Atlas config step
```

### auricrux-app (`C:\repos\auricrux-app` or GitHub)
```
Auricrux.Web/Services/AtlasService.cs           — MongoDB client
Auricrux.Web/Services/AuricruxModelRouter.cs    — staged intelligence router
Auricrux.Web/Services/AtlasCorpusService.cs     — Atlas Search corpus
Auricrux.Web/Services/ConversationMemoryService.cs — 4-backend memory (Atlas added)
Auricrux.Web/Services/ConstructionIntelligenceService.cs — integrated
Auricrux.Web/Program.cs                         — DI registration
Auricrux.Web/appsettings.json                   — Atlas + model tier config
Auricrux.Web/Auricrux.Web.csproj               — cross-platform stamp target
docker-compose.yml                              — Atlas env var added
```

---

## Environment Variables Needed

```bash
# fca-ecosystem
ATLAS__CONNECTIONSTRING=mongodb+srv://...
ATLAS__DATABASE=auricrux
AURICRUX__RAGTOPK=5

# auricrux-app (same connection string)
ATLAS__CONNECTIONSTRING=mongodb+srv://...
ATLAS__DATABASE=auricrux
Auricrux__PrimaryModel=auricrux-fca
Auricrux__SecondaryModel=llama3.2
Auricrux__TertiaryModel=mistral
Auricrux__ExtendedModel=llama3.1:70b
Auricrux__VisionModel=llava
```

---

## OneDrive Backup Location

All pitch decks and this handoff document are saved to:
```
C:\Users\MichaelBartholomew\OneDrive - Future Contractors of America LLC\
  FCA-Pitch-Decks\           — All .pptx and .pdf pitch materials
  Auricrux-FCA-Handoff\      — This document and atlas guide
```

Also accessible via OneDrive sync on any signed-in device.

---

## Quick Verification Commands (run on any machine with Azure CLI + gh)

```bash
# Verify training still running
az vm run-command invoke -g AURICRUX-TRAINING-NCAST4 -n auricrux-gpu-ncast4-t4 \
  --command-id RunShellScript \
  --scripts "ps aux | grep 1019003 | grep -v grep | head -2 && nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader"

# Verify auricrux-fca model live
curl -s http://20.230.87.21:11434/api/tags | python3 -c "import json,sys; [print(m['name']) for m in json.load(sys.stdin)['models']]"

# Verify Auricrux App production
curl https://auricrux.futurecontractorsofamerica.com/api/health

# Verify fca-ecosystem CI
gh api repos/Auricrux/fca-ecosystem/actions/runs?per_page=2 --jq '.workflow_runs[] | "\(.name) \(.conclusion)"'
gh api repos/Auricrux/auricrux-app/actions/runs?per_page=3 --jq '.workflow_runs[] | "\(.name) \(.conclusion)"'

# Verify Atlas health (once activated)
curl https://fca-ecosystem-api-stg.azurewebsites.net/api/v1/auricrux/knowledge/health
```

---

## Data Safety Rules (never violate these)

- PostgreSQL, MinIO, Ollama models: **READ-ONLY context** — Atlas is additive only
- Training run PID 1019003: **DO NOT INTERRUPT** — it's been running 26 days
- `auricrux-fca` Ollama model tag: **DO NOT OVERWRITE** — product model in use
- All migration scripts: **READ-ONLY from source** — `"original_deleted": False` in all records
- Azure VM files: **NEVER delete** — copy to Atlas, keep originals

---

## Token Factory Note

The token factory (200B→200T+ construction token generation) runs locally and was stalled on C: drive space. The Atlas pipeline unblocks it — the 22GB `growth_10b_net_new_v1.jsonl` streams to Atlas via SSH without needing local disk space. Run Step 3 above after Atlas is activated.

---

*Generated: 2026-08-11 | Repos: Auricrux/fca-ecosystem, Auricrux/auricrux-app*
