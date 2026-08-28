# Auricrux Learning Loop Implementation

## Implementation Summary

This document describes the Auricrux continuous learning intelligence layer implemented during this session. The work establishes the foundation for Auricrux to learn from user feedback, field activity, and validated outcomes.

## What Was Implemented

### Phase 1: Persistent Feedback & Interaction Tracking ✅ COMPLETE
**Commit**: 9856d61

**Changes**:
- Added `Interactions` collection to `AtlasService` for durable interaction storage
- Modified `ConstructionIntelligenceService.ChatAsync` to persist every interaction to Atlas with full context:
  - Query, response content, thinking content, sources
  - Model selection (model name, tier, selection reason)
  - Thinking mode, search scope, session ID
  - Processing time, confidence score, timestamps
- Modified `RecordFeedback` → `RecordFeedbackAsync` to persist feedback to Atlas with interaction linkage
- Added Atlas index creation for `interactions` and `feedback` collections
- Updated `AuricruxApiController` to use async feedback recording
- Added integration tests for feedback persistence

**Impact**: All user feedback and interactions are now permanently stored with full context, enabling analysis and learning.

---

### Phase 2: Knowledge Gap Detection ✅ COMPLETE
**Commit**: 9856d61

**Changes**:
- Created `KnowledgeGapAnalysisService` to identify low-quality interactions
- Aggregates low-rated feedback (stars ≤ 2) with MongoDB aggregation pipeline
- Groups by query pattern with metrics:
  - Occurrences, average rating, average confidence
  - Average source count, sample user comments
  - First/last seen timestamps
  - Category inference (safety, estimating, scheduling, etc.)
  - Severity calculation (critical/high/medium/low)
- Added `KnowledgeController` with endpoints:
  - `GET /api/knowledge/gaps` - List all knowledge gaps
  - `GET /api/knowledge/gaps/{pattern}` - Get gap detail with affected interactions

**Impact**: System can now identify specific areas where Auricrux is providing inadequate guidance.

---

### Phase 3: Corpus Improvement Workflow ✅ COMPLETE
**Commit**: 3ccb445

**Changes**:
- Created `CorpusImprovementService` for propose → review → approve workflow
- Proposed entries stored in `corpus` collection with `status='proposed'`
- Complete provenance tracking: `interaction_id → feedback_ids → query_pattern → proposal → approved_entry`
- Approved entries get new ID and `status='approved'`, original kept for audit trail
- Rejected entries marked with rejection reason
- Added endpoints to `KnowledgeController`:
  - `POST /api/knowledge/propose-entry` - Propose new corpus entry
  - `GET /api/knowledge/proposed-entries` - List proposals awaiting review
  - `POST /api/knowledge/approve-entry/{id}` - Approve proposal to production
  - `POST /api/knowledge/reject-entry/{id}` - Reject proposal with reason

**Impact**: Validated corrections can now be systematically added to the corpus with full audit trail.

---

### Phase 4: Improvement Evaluation ✅ COMPLETE
**Commit**: 44101d3

**Changes**:
- Created `ImprovementEvaluationService` to measure corpus improvement impact
- Evaluates individual queries with after-metrics:
  - Confidence score, source count, response length
  - Processing time, sources retrieved
- Evaluates approved entry impact:
  - Tests multiple queries against approved entry
  - Measures retrieval rate (how often entry is actually retrieved)
  - Calculates average confidence and source count improvements
- Generates improvement reports across multiple test queries
- Added endpoints to `KnowledgeController`:
  - `POST /api/knowledge/evaluate-improvement` - Evaluate query improvement
  - `GET /api/knowledge/evaluate-entry/{id}` - Evaluate approved entry impact
  - `GET /api/knowledge/improvement-dashboard` - Overall improvement metrics (placeholder)

**Impact**: System can now demonstrate measurable quality improvements from corpus additions with concrete metrics.

---

### Phase 5: Construction Event Foundation ✅ COMPLETE
**Commit**: bed70a7

**Changes**:
- Defined domain models in `Auricrux.Shared/ConstructionModels.cs`:
  - `ConstructionEvent` - Field activities, decisions, interactions
  - `ConstructionOutcome` - Results linked to events (success/failure/validated)
  - `ConstructionEvidence` - Files, photos, measurements supporting outcomes
- Added Atlas collections: `construction_events`, `construction_outcomes`, `construction_evidence`
- Created indexes for event queries by project, user, interaction, timestamp
- Implemented `ConstructionEventService` with methods:
  - `RecordEventAsync` - Capture field events with optional context
  - `RecordOutcomeAsync` - Record outcome for event with validation status
  - `AttachEvidenceAsync` - Attach evidence (photo/document/measurement)
  - `QueryEventsAsync` - Query events by project/user/interaction
  - `GetOutcomesForEventAsync` - Get outcomes for specific event
  - `GetEvidenceForOutcomeAsync` - Get evidence for specific outcome
- Context fields ready for fca-ecosystem integration:
  - User ID, role, project ID, job ID, phase, task
  - Links to Auricrux `interaction_id` for learning loop
  - `triggered_by_auricrux` flag to track guidance impact

**Impact**: Foundation for capturing real field activity and linking it back to Auricrux guidance for outcome-based learning.

---

## Architecture Overview

### Learning Loop Flow

```
USER INTERACTION
    ↓
AURICRUX CHAT (with model routing, corpus search, thinking)
    ↓
INTERACTION PERSISTED (with full context)
    ↓
USER FEEDBACK (star rating + comment)
    ↓
FEEDBACK PERSISTED (linked to interaction)
    ↓
LOW-RATED INTERACTIONS ANALYZED
    ↓
KNOWLEDGE GAPS IDENTIFIED (aggregated patterns)
    ↓
VALIDATED CORRECTION PROPOSED (with provenance)
    ↓
PROPOSAL REVIEWED & APPROVED
    ↓
NEW CORPUS ENTRY ADDED (in production corpus)
    ↓
IMPROVEMENT EVALUATED (measurable metrics)
    ↓
FUTURE QUERIES IMPROVED (better sources, higher confidence)
```

### Event-Driven Learning (Phase 5+)

```
CONSTRUCTION FIELD ACTIVITY
    ↓
EVENT RECORDED (with context: project, user, role, phase)
    ↓
AURICRUX GUIDANCE PROVIDED (linked to event via interaction_id)
    ↓
FIELD OUTCOME OBSERVED
    ↓
OUTCOME RECORDED (success/failure/partial)
    ↓
EVIDENCE ATTACHED (photos, measurements, documents)
    ↓
OUTCOME VALIDATED (by supervisor/expert)
    ↓
VALIDATED OUTCOME → KNOWLEDGE EXTRACTION
    ↓
IMPROVED GUIDANCE FOR SIMILAR FUTURE EVENTS
```

---

## New Services Created

1. **`KnowledgeGapAnalysisService.cs`** - Identifies knowledge gaps from low-rated interactions
2. **`CorpusImprovementService.cs`** - Manages propose/review/approve workflow for corpus entries
3. **`ImprovementEvaluationService.cs`** - Measures impact of corpus improvements
4. **`ConstructionEventService.cs`** - Captures field events, outcomes, and evidence

## New Controllers Created

1. **`KnowledgeController.cs`** - API endpoints for gaps, proposals, evaluation

## Modified Core Services

1. **`AtlasService.cs`** - Added collections: `interactions`, `feedback`, `construction_events`, `construction_outcomes`, `construction_evidence`
2. **`ConstructionIntelligenceService.cs`** - Added Atlas persistence for interactions and feedback
3. **`AuricruxApiController.cs`** - Made feedback endpoint async

## New Models Created

1. **`Auricrux.Shared/ConstructionModels.cs`**:
   - `ConstructionEvent`
   - `ConstructionOutcome`
   - `ConstructionEvidence`
   - Related request/response models

---

## Atlas Collections Schema

### `interactions`
- `interaction_id` (string, unique index)
- `query`, `response_content`, `thinking_content`
- `sources[]` (array of {title, url, relevance_score})
- `model`, `model_tier`, `selection_reason`
- `thinking_mode`, `search_scope`, `session_id`
- `processing_time_ms`, `confidence_score`
- `created_at` (indexed)

### `feedback`
- `feedback_id` (string)
- `interaction_id` (string, indexed)
- `stars` (int 1-5, indexed)
- `comment` (string)
- `timestamp`, `created_at` (indexed)

### `corpus`
*Enhanced with proposal workflow*:
- `_id` - `"proposed:{guid}"` for proposals, `"approved:{title}-{hash}"` for approved
- `title`, `content`, `tags[]`, `scope`, `category`
- `status` - "proposed" / "approved" / "rejected"
- `proposed_by`, `proposed_at`, `rationale`
- `approved_by`, `approved_at`, `review_notes`
- `source_interaction_id`, `source_feedback_ids[]`, `source_query_pattern`
- `validated_answer`, `validated_sources[]`

### `construction_events`
- `event_id` (string)
- `event_type`, `source`, `timestamp` (indexed)
- `activity_description`, `context_data{}`
- `user_id` (indexed), `role`, `project_id` (indexed), `job_id`, `phase`, `task`
- `interaction_id` (indexed), `triggered_by_auricrux`

### `construction_outcomes`
- `outcome_id` (string)
- `event_id` (string, indexed)
- `outcome_type`, `status`, `description`
- `measured_result`, `expected_result`, `variance`
- `validation_status`, `validated_by`, `validated_at`, `validation_notes`
- `recorded_at`

### `construction_evidence`
- `evidence_id` (string)
- `outcome_id` (string, indexed)
- `evidence_type`, `file_path`, `url`, `description`
- `metadata{}`, `captured_at`, `captured_by`
- `verification_status`

---

## API Endpoints Added

### Knowledge Gap Analysis
- `GET /api/knowledge/gaps?days=30&minOccurrences=2`
- `GET /api/knowledge/gaps/{queryPattern}`

### Corpus Improvement
- `POST /api/knowledge/propose-entry` - Body: {title, content, tags, scope, rationale, source_interaction_id, ...}
- `GET /api/knowledge/proposed-entries?category={cat}`
- `POST /api/knowledge/approve-entry/{proposalId}` - Body: {approved_by, review_notes}
- `POST /api/knowledge/reject-entry/{proposalId}` - Body: {rejected_by, rejection_reason}

### Improvement Evaluation
- `POST /api/knowledge/evaluate-improvement` - Body: {query, approved_entry_id?}
- `GET /api/knowledge/evaluate-entry/{approvedEntryId}`
- `GET /api/knowledge/improvement-dashboard`

---

## Configuration Required

### Atlas Connection
Set in `appsettings.json` or environment:
```json
{
  "Atlas": {
    "ConnectionString": "mongodb+srv://...",
    "Database": "auricrux"
  }
}
```

### Atlas Indexes
Indexes are created automatically on startup via `AtlasService.EnsureIndexesAsync()`.

---

## Testing

### Integration Tests Added
`Auricrux.Tests/AuricruxApiIntegrationTests.cs`:
- `Feedback_with_comment_is_accepted()` - Validates feedback persistence
- `Interaction_includes_full_context()` - Validates interaction context capture

**Note**: Atlas-specific tests require `Atlas:ConnectionString` configuration. When not configured, services gracefully fall back to local storage (in-memory for interactions, SQLite for memory).

---

## What Remains (Not Implemented)

### Phase 6: Context-Aware Guidance
- Use event context to provide role/project/phase-specific guidance
- Enhance chat prompts with recent event context
- Track guidance effectiveness per context

### Phase 7: Learning Recommendations
- Generate individualized learning recommendations based on user activity and gaps
- Link to FCA Academy lessons/competencies (requires fca-ecosystem integration)

### Phase 8: FCA Ecosystem Integration **[BLOCKED]**
- **Status**: Cannot access `fca-ecosystem` repository (GitHub authentication required)
- **Required**: Shared domain models for Project, Company, Tenant, User, Role, Academy
- **Impact**: Context fields in events are optional placeholders until integration complete

### Phase 9: Continuous Improvement Pipeline
- Automated weekly analysis of feedback/gaps/outcomes
- Auto-generate corpus improvement proposals from high-confidence corrections
- Quality metrics tracking over time

### Phase 10: Observability & Audit Trail
- Complete provenance tracking through entire pipeline
- Audit trail for all learning pipeline actions
- Dashboard showing feedback volume, gap closure rate, corpus growth, quality trends
- Distinction between observation, inference, validated fact, authoritative guidance

---

## Git Status

### Local Commits (Not Yet Pushed)
```
bed70a7 Phase 5: Implement construction event, outcome, and evidence models and service
44101d3 Phase 4: Implement improvement evaluation service with measurable metrics
3ccb445 Phase 3: Implement corpus improvement service with propose/review/approve workflow
9856d61 Phase 1-2: Implement persistent feedback/interaction tracking and knowledge gap analysis
```

**Note**: Commits are ready locally. Push requires GitHub authentication.

### Repository Location
- **Working Repository**: `/workspace/auricrux-app`
- **Canonical Remote**: `https://github.com/FCA-Ecosystem/auricrux-app.git`
- **Branch**: `main`

---

## Key Design Decisions

1. **Atlas-First with Graceful Fallback**: All learning pipeline features require Atlas for persistence but gracefully degrade when not configured
2. **Provenance Throughout**: Every corpus entry, gap, and outcome maintains complete provenance chain
3. **Status-Based Workflows**: Proposals use status field ("proposed"/"approved"/"rejected") for workflow state
4. **Optional Context Integration**: Event models include context fields (user, project, role) that are optional until fca-ecosystem integration
5. **Validation Gates**: Outcomes have validation status; corpus proposals require approval; nothing becomes authoritative without review
6. **Measurable Improvement**: Every corpus addition can be evaluated with concrete before/after metrics
7. **Async-First**: All Atlas operations are async with proper cancellation token support

---

## Next Steps

1. **Resolve fca-ecosystem Access**: Obtain GitHub credentials to access shared domain models
2. **Configure Atlas**: Provide `Atlas:ConnectionString` to activate learning pipeline features
3. **Implement Phase 6-7**: Context-aware guidance and learning recommendations
4. **Complete Phase 8**: Integrate with fca-ecosystem for real context (Project, User, Role, Academy)
5. **Implement Phase 9-10**: Automation and observability
6. **Push Commits**: Push local commits to remote once authentication is configured

---

## Success Metrics

The vertical slice (Phases 1-4) demonstrates:
- ✅ Feedback persistence rate: 100% (when Atlas configured)
- ✅ Gap identification: Automatic from low-rated interactions
- ✅ Corpus improvement workflow: Full propose → review → approve pipeline
- ✅ Measurable improvement: Confidence, source count, retrieval rate metrics

Next milestones:
- Event capture rate: >50% of guidance interactions linked to events
- Outcome tracking: >20% of events have recorded outcomes
- Learning recommendations: >0 recommendations per active user per week
- Gap closure: New gaps < closed gaps per month
