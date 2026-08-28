# Auricrux - Construction Intelligence Platform

**Version 1.3.0** | August 28, 2026

Auricrux is the integrated intelligence layer for the Future Contractors of America (FCA) Construction Operating System. It's not a chatbot or standalone app—it's the operating intelligence that observes, understands, learns from, acts upon, and continuously improves all FCA platform activities.

## What is Auricrux?

Auricrux implements a continuously learning cycle that transforms construction field activity into institutional knowledge:

```
FIELD ACTIVITY → EVENT CAPTURE → CONTEXTUALIZATION → 
OUTCOME TRACKING → VALIDATION → KNOWLEDGE EXTRACTION → 
LEARNING RECOMMENDATIONS → PREDICTIVE INTELLIGENCE TRANSFER → 
WORKFLOW IMPROVEMENT → MEASURED OUTCOMES → CONTINUOUS LEARNING
```

### Core Capabilities

- **Learning Loop** (Phases 6-10): Complete feedback-to-improvement pipeline
- **Predictive Intelligence** (Phase 9A): Cross-project knowledge transfer that predicts and prevents issues
- **Observability Dashboard** (Phase 9B): Real-time intelligence metrics and visualization
- **Construction Events**: Capture field activities, decisions, and interactions
- **Outcome Tracking**: Link results back to events with evidence
- **Knowledge Gap Analysis**: Identify what teams don't know and need to learn
- **Context-Aware Guidance**: Real-time recommendations based on user/project context
- **Continuous Improvement**: Automated analysis and quality trend tracking
- **Audit Trail & Provenance**: Complete lineage tracking of all learning actions

## Architecture

```
Auricrux Intelligence Layer
├── Atlas (MongoDB): Persistent learning pipeline data
├── Ollama: Local LLM backend (auricrux-fca model)
├── Model Router: 5-tier routing (auricrux-fca → llama3.2 → mistral → llama3.1:70b → llava)
├── Learning Services: 10+ specialized intelligence services
├── API: RESTful endpoints for intelligence operations
└── UI: Blazor dashboard + chat interface
```

### Repository Structure

```
auricrux-app/
├── Auricrux.Web/                    # Primary intelligence platform
│   ├── Components/
│   │   ├── Pages/
│   │   │   ├── Chat.razor           # AI chat interface
│   │   │   └── Intelligence/
│   │   │       └── Dashboard.razor  # Intelligence observability
│   │   └── Shared/                  # Reusable components
│   ├── Controllers/                 # API endpoints
│   │   ├── ChatController.cs
│   │   ├── KnowledgeController.cs
│   │   ├── ContextController.cs
│   │   ├── PredictiveIntelligenceController.cs
│   │   └── IntelligenceDashboardController.cs
│   ├── Services/                    # Core intelligence services
│   │   ├── AtlasService.cs          # MongoDB Atlas integration
│   │   ├── AuricruxModelRouter.cs   # 5-tier model routing
│   │   ├── ConstructionIntelligenceService.cs
│   │   ├── KnowledgeGapAnalysisService.cs
│   │   ├── ConstructionEventService.cs
│   │   ├── ContextAwareGuidanceService.cs
│   │   ├── LearningRecommendationService.cs
│   │   ├── ContinuousImprovementService.cs
│   │   ├── PredictiveIntelligenceService.cs
│   │   ├── AuditTrailService.cs
│   │   └── ProvenanceService.cs
│   ├── BackgroundServices/
│   │   ├── LearningPipelineWorker.cs
│   │   └── PredictiveIntelligenceOrchestrator.cs
│   └── Program.cs                   # Service registration & configuration
├── Auricrux.Shared/                 # Shared libraries
│   ├── Models.cs                    # Core data models
│   ├── ConstructionModels.cs        # Construction domain models
│   ├── FcaDomain.cs                 # FCA ecosystem integration models
│   └── Services.cs                  # HTTP client, TTS, business logic
├── Auricrux.Mobile/                 # MAUI cross-platform app
├── Auricrux.Tests/                  # Integration tests
└── docs/                            # Comprehensive documentation
```

## Key Features

### 1. Learning Loop (Phases 6-10)

**Phase 6: Context-Aware Guidance**
- Track user activity and provide relevant recommendations
- Project/role-based context
- Effectiveness measurement

**Phase 7: Learning Recommendations**
- Generate personalized training suggestions
- Link to Academy lessons
- Priority-based delivery

**Phase 8: FCA Ecosystem Integration**
- Live Project, Member, Academy data
- Typed domain references
- Entity validation

**Phase 9: Continuous Improvement**
- Automated gap analysis
- Auto-proposal generation
- Quality trend tracking
- Weekly improvement reports

**Phase 10: Audit Trail & Provenance**
- Complete action lineage
- Resource tracking
- Compliance and observability

### 2. Predictive Intelligence Transfer (Phase 9A) - BREAKTHROUGH

When Auricrux learns something significant on Project A, it automatically:
1. Extracts causal factors (understands WHY it happened)
2. Identifies similar active projects
3. Predicts WHEN they'll encounter the same situation
4. Proactively delivers knowledge BEFORE the issue occurs

**This shifts construction intelligence from reactive to PREDICTIVE.**

### 3. Observability Dashboard (Phase 9B)

Real-time intelligence metrics at `/intelligence`:
- Executive overview (events, outcomes, transfers, savings)
- Learning loop pipeline visualization
- Recent predictive transfers
- Active knowledge gaps
- System health indicators

## Quick Start

### Prerequisites

- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/download))
- **MongoDB Atlas** account (or local MongoDB)
- **Ollama** with auricrux-fca model
- **Visual Studio 2024** or VS Code

### 1. Clone Repository

```bash
git clone https://github.com/FCA-Ecosystem/auricrux-app.git
cd auricrux-app
```

### 2. Configure Environment

Create `Auricrux.Web/appsettings.Development.json` (gitignored):

```json
{
  "Atlas": {
    "ConnectionString": "mongodb+srv://[user]:[password]@[cluster]/auricrux",
    "Database": "auricrux"
  },
  "Auricrux": {
    "OllamaUrl": "http://127.0.0.1:11434",
    "PrimaryModel": "auricrux-fca"
  },
  "FcaEcosystem": {
    "ApiBaseUrl": "https://futurecontractorsofamerica.com/api"
  }
}
```

**⚠️ NEVER commit credentials to git!**

### 3. Start Ollama

```bash
# Pull and run the auricrux-fca model
ollama pull auricrux-fca
ollama serve
```

### 4. Run the Application

```bash
cd Auricrux.Web
dotnet restore
dotnet run
```

Access:
- Chat interface: `https://localhost:7080`
- Intelligence dashboard: `https://localhost:7080/intelligence`
- API health: `https://localhost:7080/api/health`

## API Endpoints

### Core Intelligence

```
POST   /api/chat                      - AI chat with construction context
POST   /api/thinking                  - Deep reasoning mode
POST   /api/search                    - Corpus search
POST   /api/feedback/{id}             - Submit feedback
GET    /api/health                    - Health check
GET    /api/capabilities              - System capabilities
```

### Knowledge & Learning

```
GET    /api/knowledge/gaps            - Active knowledge gaps
POST   /api/knowledge/propose         - Propose corpus improvement
POST   /api/knowledge/approve/{id}    - Approve improvement
POST   /api/knowledge/reject/{id}     - Reject improvement
POST   /api/knowledge/evaluate        - Evaluate improvements
GET    /api/knowledge/recommendations - Learning recommendations
POST   /api/knowledge/run-analysis    - Trigger gap analysis
GET    /api/knowledge/quality-trends  - Quality metrics
GET    /api/knowledge/auto-proposals  - Auto-generated proposals
GET    /api/knowledge/pipeline-health - Learning pipeline status
GET    /api/knowledge/audit           - Audit trail
GET    /api/knowledge/provenance/{id} - Resource provenance
```

### Context & Guidance

```
POST   /api/context/track             - Track user activity
GET    /api/context/recent            - Recent activity
GET    /api/context/guidance-effectiveness - Guidance metrics
```

### Predictive Intelligence (Phase 9A)

```
POST   /api/predictive/transfer/{outcomeId}  - Trigger intelligence transfer
POST   /api/predictive/link-lessons          - Link recommendations to Academy
GET    /api/predictive/recommendations/{projectId} - Project recommendations
GET    /api/predictive/health                - Predictive system health
```

### Intelligence Dashboard (Phase 9B)

```
GET    /api/intelligence/dashboard/overview          - Executive metrics
GET    /api/intelligence/dashboard/learning-loop     - Loop stage metrics
GET    /api/intelligence/dashboard/predictive-transfers - Recent transfers
GET    /api/intelligence/dashboard/knowledge-gaps    - Active gaps
GET    /api/intelligence/dashboard/audit-trail       - Recent actions
GET    /api/intelligence/dashboard/health            - System health
```

## MongoDB Atlas Collections

Auricrux uses these collections for the learning pipeline:

- `corpus` - RAG knowledge base
- `conversation_memory` - Chat history
- `model_routes` - Model routing configuration
- `feedback` - User feedback
- `interactions` - User interactions
- `knowledge_gaps` - Identified knowledge gaps
- `construction_events` - Field activities
- `construction_outcomes` - Outcome tracking
- `construction_evidence` - Evidence attachments
- `guidance_effectiveness` - Context guidance metrics
- `learning_recommendations` - AI-generated recommendations
- `improvement_proposals` - Corpus improvement proposals
- `quality_metrics` - Quality trend data
- `audit_trail` - Complete action provenance
- `fca_entity_cache` - FCA API response cache

## Deployment

### Production (Oracle Cloud VM)

See [`deployment-packages/DEPLOYMENT_GUIDE.md`](deployment-packages/DEPLOYMENT_GUIDE.md) for complete deployment instructions.

Quick deployment:
```bash
dotnet publish -c Release -o ./publish
# Transfer to Oracle VM and configure systemd service
```

### Docker

```bash
docker build -t auricrux/web:1.3.0 .
docker run -d \
  -p 80:80 \
  -e Atlas__ConnectionString="mongodb+srv://..." \
  -e Auricrux__OllamaUrl="http://ollama:11434" \
  --name auricrux-web \
  auricrux/web:1.3.0
```

### Kubernetes

```bash
kubectl apply -f k8s-deployment.yaml
kubectl apply -f k8s-ingress.yaml
```

## Development

### Run Tests

```bash
dotnet test
```

### Build All Projects

```bash
dotnet build
```

### Code Style

Using C# 12 with nullable reference types enabled for type safety.

## Integration with FCA Ecosystem

Auricrux is designed to be the intelligence layer for the complete FCA ecosystem:

- **Bidding**: Learn from bid outcomes, predict win probability
- **Estimating**: Identify gaps in estimates, recommend improvements
- **Project Execution**: Track field events, capture outcomes
- **Workforce Development**: Personalized learning recommendations
- **Financial Intelligence**: Pattern recognition in billing/invoicing
- **Compliance**: Automated verification and guidance

See [`fca-ecosystem` repository](https://github.com/FCA-Ecosystem/fca-ecosystem) for complete integration.

## Architectural Principles

From [`docs/FCA_SYSTEM_LAW.md`](docs/FCA_SYSTEM_LAW.md):

1. **Every action creates evidence** - Complete audit trail
2. **No disconnected features** - All capabilities link to the spine
3. **Governed autonomy** - AI acts within defined boundaries
4. **Continuous learning** - System improves with every interaction
5. **Construction-specific** - Not generic AI, built for construction

## Documentation

- [`AGENTS.md`](AGENTS.md) - Agent handoff and operational details
- [`CLAIMS_REGISTER.md`](CLAIMS_REGISTER.md) - Honest capability claims
- [`docs/FCA_SYSTEM_LAW.md`](docs/FCA_SYSTEM_LAW.md) - Architectural governance
- [`AURICRUX_LEARNING_LOOP_IMPLEMENTATION.md`](AURICRUX_LEARNING_LOOP_IMPLEMENTATION.md) - Learning loop design
- [`IMPLEMENTATION_COMPLETE_PHASES_6_10.md`](IMPLEMENTATION_COMPLETE_PHASES_6_10.md) - Implementation summary
- [`PHASE_9A_PREDICTIVE_INTELLIGENCE.md`](PHASE_9A_PREDICTIVE_INTELLIGENCE.md) - Predictive intelligence architecture
- [`deployment-packages/DEPLOYMENT_GUIDE.md`](deployment-packages/DEPLOYMENT_GUIDE.md) - Deployment instructions

## License

This project is part of Future Contractors of America LLC.

## Support

- **GitHub Issues**: https://github.com/FCA-Ecosystem/auricrux-app/issues
- **Email**: michael@futurecontractorsofamerica.com
- **Documentation**: See docs/ folder for comprehensive guides

---

**Current Status**: Production-ready with complete learning loop, predictive intelligence, and observability dashboard.

**Live URL**: https://auricrux.futurecontractorsofamerica.com

**Next Phase**: Self-Correcting Construction Intelligence with physical verification and provable reasoning.
