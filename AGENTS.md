# FCA Ecosystem & Auricrux — Agent Handoff (2026-08-28)

## FOR THE NEXT AGENT: Read This First

This document provides everything needed to continue work on Auricrux and FCA Ecosystem. All code is in GitHub under `FCA-Ecosystem` organization. All state is verifiable from live endpoints and MongoDB Atlas.

---

## GitHub Repositories

| Repo | URL | Current State |
|------|-----|---------------|
| **FCA Ecosystem** | https://github.com/FCA-Ecosystem/fca-ecosystem | ✅ Production (main branch) |
| **Auricrux App** | https://github.com/FCA-Ecosystem/auricrux-app | ✅ Production (main branch) |

### Clone Commands

```bash
git clone https://github.com/FCA-Ecosystem/fca-ecosystem.git /workspace/fca-ecosystem
git clone https://github.com/FCA-Ecosystem/auricrux-app.git /workspace/auricrux-app
```

---

## Live System Status (Verified 2026-08-28)

| System | Status | URL / Location |
|--------|--------|----------------|
| Auricrux App | ✅ LIVE v1.3.0 | https://auricrux.futurecontractorsofamerica.com |
| Intelligence Dashboard | ✅ LIVE | https://auricrux.futurecontractorsofamerica.com/intelligence |
| FCA Ecosystem Web | ✅ LIVE | https://futurecontractorsofamerica.com |
| MongoDB Atlas | ✅ ACTIVE | auricrux-prod.plzuwk.mongodb.net |
| Ollama (auricrux-fca) | ✅ RUNNING | Oracle Cloud VM (127.0.0.1:11434) |

---

## Current Implementation Status

### ✅ COMPLETE: Full Learning Loop (Phases 6-10)

**Phase 6: Context-Aware Guidance**
- Service: `ContextAwareGuidanceService.cs`
- API: `/api/context/*`
- Status: Operational

**Phase 7: Learning Recommendations**
- Service: `LearningRecommendationService.cs`
- API: `/api/knowledge/recommendations`
- Status: Operational

**Phase 8: FCA Ecosystem Integration**
- Services: `FcaEcosystemApiService.cs`, `AcademyLessonMatcherService.cs`
- Models: `FcaDomain.cs` (Project, Member, AcademyLesson)
- Status: Integrated with typed references

**Phase 9: Continuous Improvement**
- Service: `ContinuousImprovementService.cs`
- Background Worker: `LearningPipelineWorker.cs` (weekly analysis)
- API: `/api/knowledge/auto-proposals`, `/api/knowledge/quality-trends`
- Status: Operational

**Phase 10: Audit Trail & Provenance**
- Services: `AuditTrailService.cs`, `ProvenanceService.cs`
- API: `/api/knowledge/audit`, `/api/knowledge/provenance/{id}`
- Status: Complete observability

### ✅ COMPLETE: Predictive Intelligence Transfer (Phase 9A)

**Breakthrough Feature**: Cross-project intelligence that predicts and prevents issues

**Services**:
- `PredictiveIntelligenceService.cs` - Causal factor extraction + project similarity
- `FcaEcosystemApiService.cs` - Live FCA API integration
- `AcademyLessonMatcherService.cs` - Link gaps to Academy lessons

**Background Worker**:
- `PredictiveIntelligenceOrchestrator.cs` - Scans every 5 minutes for new verified outcomes

**API**:
- `POST /api/predictive/transfer/{outcomeId}` - Trigger intelligence transfer
- `POST /api/predictive/link-lessons` - Link recommendations to lessons
- `GET /api/predictive/recommendations/{projectId}` - Project recommendations
- `GET /api/predictive/health` - System health

**How It Works**:
1. Verified outcome triggers analysis
2. Extract causal factors (WHY it happened)
3. Find similar active projects (similarity score ≥ 0.7)
4. Predict timeframe (when they'll encounter it)
5. Proactively deliver knowledge to prevent issues

### ✅ COMPLETE: Observability Dashboard (Phase 9B)

**Location**: `/intelligence` page

**Features**:
- Executive overview (events, outcomes, transfers, estimated savings)
- Learning loop pipeline visualization (8 stages)
- Recent predictive transfers feed
- Active knowledge gaps analysis
- Real-time audit trail
- System health indicators (5 services)

**Services**:
- `IntelligenceDashboardService.cs` - Metrics aggregation

**API**:
- `GET /api/intelligence/dashboard/overview` - Executive metrics
- `GET /api/intelligence/dashboard/learning-loop` - Pipeline stats
- `GET /api/intelligence/dashboard/predictive-transfers` - Recent transfers
- `GET /api/intelligence/dashboard/knowledge-gaps` - Active gaps
- `GET /api/intelligence/dashboard/audit-trail` - Recent actions
- `GET /api/intelligence/dashboard/health` - System status

---

## MongoDB Atlas Configuration

### Collections (Auto-Created)

| Collection | Purpose |
|------------|---------|
| `corpus` | RAG knowledge base |
| `conversation_memory` | Chat history |
| `model_routes` | Model routing config |
| `feedback` | User feedback |
| `interactions` | User interactions tracking |
| `knowledge_gaps` | Identified knowledge gaps |
| `construction_events` | Field activity capture |
| `construction_outcomes` | Outcome tracking |
| `construction_evidence` | Evidence attachments |
| `guidance_effectiveness` | Context guidance metrics |
| `learning_recommendations` | AI recommendations |
| `improvement_proposals` | Corpus improvements |
| `quality_metrics` | Quality trend data |
| `audit_trail` | Complete provenance |
| `fca_entity_cache` | FCA API cache (30min-6hr TTL) |

### Connection Configuration

**⚠️ CRITICAL SECURITY**: Atlas credentials MUST use environment variables, never commit to git!

```bash
# Required environment variable
export Atlas__ConnectionString="mongodb+srv://[user]:[password]@auricrux-prod.plzuwk.mongodb.net/auricrux?appName=auricrux-prod"
export Atlas__Database="auricrux"
```

**For GitHub Actions**: Add `ATLAS_CONNECTION_STRING` to repository secrets

**For Oracle Cloud VM**: Add to `/etc/auricrux/environment` file

---

## Architecture Overview

### Auricrux Intelligence Layer

```
Auricrux (Intelligence Platform)
├── Web Application (Blazor Server)
│   ├── Chat Interface (/chat)
│   └── Intelligence Dashboard (/intelligence)
├── API Layer (RESTful)
│   ├── Core: /api/chat, /thinking, /search, /health
│   ├── Knowledge: /api/knowledge/* (gaps, proposals, recommendations)
│   ├── Context: /api/context/* (tracking, guidance)
│   ├── Predictive: /api/predictive/* (intelligence transfer)
│   └── Dashboard: /api/intelligence/dashboard/* (metrics)
├── Services (15+ intelligence services)
│   ├── AtlasService - MongoDB integration
│   ├── AuricruxModelRouter - 5-tier model routing
│   ├── ConstructionIntelligenceService - Core AI
│   ├── Learning Loop Services (Phases 6-10)
│   └── Predictive Intelligence Services (Phase 9A)
├── Background Workers
│   ├── LearningPipelineWorker - Weekly analysis
│   └── PredictiveIntelligenceOrchestrator - 5-min scan
└── Data Layer
    ├── MongoDB Atlas - Learning pipeline data
    └── Ollama - LLM backend (auricrux-fca model)
```

### FCA Ecosystem Integration

**Current Integration Points**:
- `AuricruxPresence` component on ~806/817 pages
- `publishAuricruxEvent` in ~627 services
- Verify/Interpret/Act APIs (`/api/v1/auricrux/*`)
- Basic Ollama provider (`IAuricruxProvider`)

**Integration Gap**: Advanced learning loop from auricrux-app NOT yet embedded in fca-ecosystem provider. See "Next Phase" below.

---

## What's Working (Verified)

✅ **Learning Loop**: Complete Phases 6-10 operational
✅ **Predictive Intelligence**: Cross-project knowledge transfer active
✅ **Observability**: Real-time dashboard at `/intelligence`
✅ **MongoDB Atlas**: All collections active, learning pipeline processing
✅ **Ollama Integration**: 5-tier model routing operational
✅ **FCA API Integration**: Live Project/Member/Academy data
✅ **Background Workers**: Weekly analysis + 5-min predictive scan
✅ **Audit Trail**: Complete provenance tracking

---

## Critical Security Issues

### ⚠️ RESOLVED: Atlas Credentials

**Issue**: Connection string with credentials was committed to `appsettings.json`

**Resolution** (2026-08-28):
- ✅ Credentials removed from code
- ✅ Documentation updated for environment variables
- ⚠️ **ACTION REQUIRED**: Rotate Atlas password and update GitHub secrets

**See**: `SECURITY_ALERT_ATLAS_CREDENTIALS.md` for complete remediation checklist

---

## Next Phase: Architecture Unification

### The Problem

Auricrux exists in TWO forms:
1. **auricrux-app**: Advanced standalone with full learning loop
2. **fca-ecosystem**: Basic embedded provider (just Ollama + optional RAG)

**This violates architectural doctrine**: Auricrux must be ONE unified intelligence layer.

### The Solution

**Phase 2 (Planned)**: Create shared intelligence package in fca-ecosystem

```
packages/auricrux-intelligence/
├── Auricrux.Intelligence.Core/
│   └── Services/ (migrate from auricrux-app)
└── Auricrux.Intelligence.Atlas/
    └── Atlas integration
```

**Then**: Enhance `IAuricruxProvider` interface to include learning loop methods

**Result**: FCA ecosystem gets full predictive intelligence, auricrux-app becomes specialized admin UI or deprecated

---

## Key Files by Location

### auricrux-app

**Services** (`Auricrux.Web/Services/`):
- `AtlasService.cs` - MongoDB client
- `AuricruxModelRouter.cs` - 5-tier routing
- `ConstructionIntelligenceService.cs` - Core AI
- `KnowledgeGapAnalysisService.cs` - Gap detection
- `ConstructionEventService.cs` - Event capture
- `ContextAwareGuidanceService.cs` - Phase 6
- `LearningRecommendationService.cs` - Phase 7
- `ContinuousImprovementService.cs` - Phase 9
- `PredictiveIntelligenceService.cs` - Phase 9A breakthrough
- `FcaEcosystemApiService.cs` - FCA integration
- `AcademyLessonMatcherService.cs` - Lesson linking
- `IntelligenceDashboardService.cs` - Phase 9B metrics
- `AuditTrailService.cs` - Phase 10 provenance
- `ProvenanceService.cs` - Phase 10 lineage

**Controllers** (`Auricrux.Web/Controllers/`):
- `ChatController.cs`, `KnowledgeController.cs`, `ContextController.cs`
- `PredictiveIntelligenceController.cs`, `IntelligenceDashboardController.cs`

**Background** (`Auricrux.Web/BackgroundServices/`):
- `LearningPipelineWorker.cs` - Weekly improvement analysis
- `PredictiveIntelligenceOrchestrator.cs` - 5-min outcome scan

**UI** (`Auricrux.Web/Components/`):
- `Pages/Chat.razor` - Main chat interface
- `Pages/Intelligence/Dashboard.razor` - Intelligence dashboard
- `Shared/MetricCard.razor`, `Shared/HealthIndicator.razor`

### fca-ecosystem

**Integration** (`apps/api/FcaEcosystem.Application/Auricrux/`):
- `IAuricruxProvider.cs` - Basic LLM provider interface
- `AuricruxOperationalVerifier.cs` - Structured verification
- `AuricruxActExecutor.cs` - Governed actions

**Frontend** (`apps/web/src/`):
- `features/auricrux-orchestration/` - Track 54 entities
- `lib/auricruxAct.ts`, `lib/auricruxEvents.ts` - Client helpers

---

## Environment Variables Required

### auricrux-app

```bash
# MongoDB Atlas (CRITICAL - use secrets, never commit)
Atlas__ConnectionString="mongodb+srv://[user]:[password]@auricrux-prod.plzuwk.mongodb.net/auricrux"
Atlas__Database="auricrux"

# Ollama
Auricrux__OllamaUrl="http://127.0.0.1:11434"
Auricrux__PrimaryModel="auricrux-fca"
Auricrux__SecondaryModel="llama3.2"
Auricrux__TertiaryModel="mistral"
Auricrux__ExtendedModel="llama3.1:70b"
Auricrux__VisionModel="llava"

# FCA Integration
FcaEcosystem__ApiBaseUrl="https://futurecontractorsofamerica.com/api"
```

### fca-ecosystem

```bash
# MongoDB Atlas
ATLAS__CONNECTIONSTRING="mongodb+srv://..."
ATLAS__DATABASE="auricrux"

# Auricrux Integration
AURICRUX__RAGTOPK=5
```

---

## Deployment

### Production URL

**Auricrux**: https://auricrux.futurecontractorsofamerica.com
**FCA Ecosystem**: https://futurecontractorsofamerica.com

### Oracle Cloud VM

**Platform**: Oracle Cloud Infrastructure (OCI)
**IP**: 150.136.115.97
**Service**: Systemd service (`auricrux.service`)

### Deployment Process

```bash
# Build
cd /workspace/auricrux-app/Auricrux.Web
dotnet publish -c Release -o ./publish

# Deploy (see deployment-packages/DEPLOYMENT_GUIDE.md)
# Transfer to Oracle VM and restart service
```

### Docker Alternative

```bash
docker build -t auricrux/web:1.3.0 .
docker run -d -p 80:80 \
  -e Atlas__ConnectionString="..." \
  auricrux/web:1.3.0
```

---

## Verification Commands

```bash
# Health check
curl https://auricrux.futurecontractorsofamerica.com/api/health

# Predictive intelligence status
curl https://auricrux.futurecontractorsofamerica.com/api/predictive/health

# Dashboard metrics
curl https://auricrux.futurecontractorsofamerica.com/api/intelligence/dashboard/overview

# Ollama status
curl http://127.0.0.1:11434/api/tags

# Atlas connection (from dashboard API)
curl https://auricrux.futurecontractorsofamerica.com/api/knowledge/health
```

---

## Documentation

| Document | Purpose |
|----------|---------|
| [`README.md`](README.md) | Complete platform overview |
| [`CLAIMS_REGISTER.md`](CLAIMS_REGISTER.md) | Honest capability claims |
| [`docs/FCA_SYSTEM_LAW.md`](docs/FCA_SYSTEM_LAW.md) | Architectural governance |
| [`AURICRUX_LEARNING_LOOP_IMPLEMENTATION.md`](AURICRUX_LEARNING_LOOP_IMPLEMENTATION.md) | Phases 1-5 design |
| [`IMPLEMENTATION_COMPLETE_PHASES_6_10.md`](IMPLEMENTATION_COMPLETE_PHASES_6_10.md) | Phases 6-10 summary |
| [`PHASE_9A_PREDICTIVE_INTELLIGENCE.md`](PHASE_9A_PREDICTIVE_INTELLIGENCE.md) | Breakthrough feature architecture |
| [`PHASE_9B_OBSERVABILITY_DASHBOARD_PLAN.md`](PHASE_9B_OBSERVABILITY_DASHBOARD_PLAN.md) | Dashboard design |
| [`deployment-packages/DEPLOYMENT_GUIDE.md`](deployment-packages/DEPLOYMENT_GUIDE.md) | Complete deployment instructions |
| [`SECURITY_ALERT_ATLAS_CREDENTIALS.md`](SECURITY_ALERT_ATLAS_CREDENTIALS.md) | Security remediation checklist |

---

## What MUST NOT Be Touched

```
⛔ MongoDB Atlas Production Data — Read/Write with care
⛔ Ollama auricrux-fca model — Production model in use
⛔ Oracle Cloud VM systemd service — Restart only during maintenance window
⛔ FCA Ecosystem PostgreSQL — Atlas is additive, never replaces PG
```

---

## Data Safety Rules

- **Atlas is additive**: Never delete production data
- **Backup before major changes**: Use `mongodump` for Atlas backups
- **Test in development**: Use separate Atlas cluster for dev/staging
- **Audit trail is sacred**: Never tamper with `audit_trail` collection
- **Learning pipeline data**: Outcomes and events are immutable once verified

---

## Contact & Support

- **GitHub Issues**: https://github.com/FCA-Ecosystem/auricrux-app/issues
- **Email**: michael@futurecontractorsofamerica.com
- **Documentation**: See `docs/` folder for comprehensive guides

---

## Agent Handoff Checklist

When handing off to the next agent:

- [ ] Verify all commits pushed to GitHub `main` branch
- [ ] Confirm production deployment is current
- [ ] Check MongoDB Atlas connection healthy
- [ ] Verify Ollama service running
- [ ] Test all health check endpoints
- [ ] Review and update this document with latest changes
- [ ] Document any new blockers or issues
- [ ] Ensure no credentials committed to git

---

*Last Updated: 2026-08-28 | Version 1.3.0 | Repositories: FCA-Ecosystem/auricrux-app, FCA-Ecosystem/fca-ecosystem*

*Current Status: Phases 6-10 + 9A/9B complete. Next phase: Architecture unification + Self-Correcting Intelligence.*
