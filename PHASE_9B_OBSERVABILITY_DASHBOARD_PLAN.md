# Phase 9B: Observability Dashboard - Intelligence Visualization

**Status**: 🚧 PLANNING → EXECUTION
**Date**: August 28, 2026
**Focus**: Frontend visualization of Auricrux intelligence layer

---

## 🎯 Objective

Create a **real-time observability dashboard** that makes Auricrux's intelligence visible, measurable, and actionable for users. This dashboard demonstrates the learning loop in action and provides immediate visibility into the predictive intelligence system.

---

## 🏗️ Architecture Plan

### Component Structure
```
Auricrux.Web/Components/Pages/
├── Intelligence/
│   ├── Dashboard.razor              (Main dashboard page)
│   ├── LearningLoopView.razor       (Visual learning pipeline)
│   ├── PredictiveTransfersView.razor (Cross-project intelligence)
│   ├── KnowledgeGapsView.razor      (Identified gaps)
│   ├── RecommendationsView.razor    (Active recommendations)
│   └── AuditTrailView.razor         (Provenance & history)
└── Shared/
    ├── MetricCard.razor             (Reusable metric display)
    ├── ActivityFeed.razor           (Real-time event stream)
    └── TrendChart.razor             (Time-series visualization)
```

### API Services
```
Auricrux.Web/Services/
├── IntelligenceDashboardService.cs  (Aggregate metrics)
└── RealtimeIntelligenceHub.cs       (SignalR for live updates)
```

### New Controllers
```
Auricrux.Web/Controllers/
└── IntelligenceDashboardController.cs (Dashboard API endpoints)
```

---

## 📊 Dashboard Sections

### 1. Executive Overview (Top)
**Purpose**: At-a-glance system health and impact

**Metrics**:
- Total Outcomes Processed (24h / 7d / 30d)
- Predictive Transfers (active alerts)
- Issues Prevented (user-confirmed)
- Estimated Cost Savings
- Learning Loop Cycle Time
- System Health (all services operational)

**Visual**: Large metric cards with trend indicators (↑↓)

---

### 2. Learning Loop Visualization (Center Left)
**Purpose**: Show the complete loop in action

**Visual**: Circular flow diagram with live counters
```
┌─────────────────────────────────────────┐
│     CONSTRUCTION EVENT CAPTURE          │
│              ↓                          │
│        (234 events, 24h)                │
│              ↓                          │
│     CONTEXTUALIZATION & TRACKING        │
│              ↓                          │
│        (189 contextualized)             │
│              ↓                          │
│     OUTCOME & EVIDENCE CAPTURE          │
│              ↓                          │
│         (156 outcomes)                  │
│              ↓                          │
│          VALIDATION                     │
│              ↓                          │
│         (142 verified)                  │
│              ↓                          │
│      KNOWLEDGE EXTRACTION               │
│              ↓                          │
│        (87 lessons learned)             │
│              ↓                          │
│    INDIVIDUALIZED GUIDANCE              │
│              ↓                          │
│      (213 recommendations)              │
│              ↓                          │
│  PREDICTIVE INTELLIGENCE TRANSFER       │
│              ↓                          │
│      (45 proactive alerts)              │
│              ↓                          │
│     WORKFLOW IMPROVEMENT                │
│              ↓                          │
│   (8 auto-proposed improvements)        │
│              ↓                          │
│      MEASURED FUTURE OUTCOME            │
│              ↓                          │
│        (continuous loop)                │
└─────────────────────────────────────────┘
```

**Interaction**: Click each stage to see details

---

### 3. Predictive Intelligence Transfer (Center Right)
**Purpose**: Show cross-project knowledge transfer

**Components**:
- **Recent Transfers**: List of latest predictive alerts
  - Source project → Target projects
  - Similarity score
  - Matched factors
  - Predicted timeframe
  - Status (sent / viewed / acted upon)

- **Transfer Success Map**: Visual graph showing knowledge flow
  - Nodes = Projects
  - Edges = Knowledge transfers
  - Color = Success rate
  - Size = Impact magnitude

**Visual**: Network graph + timeline

---

### 4. Knowledge Gaps & Recommendations (Bottom Left)
**Purpose**: Show what Auricrux is learning and recommending

**Tables**:
- **Active Knowledge Gaps**
  - Gap pattern
  - Frequency (how often encountered)
  - Severity
  - Coverage (% with recommendations)
  - Academy link status

- **Live Recommendations**
  - Project/User
  - Type (predictive / reactive)
  - Priority
  - Engagement status
  - Time since generated

**Filters**: By project, priority, engagement status

---

### 5. Audit Trail & Provenance (Bottom Right)
**Purpose**: Complete observability and traceability

**Components**:
- **Recent Actions**: Live feed of system actions
  ```
  15:32:04 - Predictive transfer triggered for outcome out_12345
  15:32:05 - Similarity analysis: 3 projects matched (score ≥ 0.7)
  15:32:06 - Recommendations created for proj_456, proj_789, proj_012
  15:32:07 - Audit trail recorded: 3 proactive alerts
  ```

- **Provenance Chain**: Click any item to see full lineage
  ```
  Recommendation rec_abc123
    ↓ Generated from
  Outcome out_12345 (validated)
    ↓ Linked to
  Event evt_67890 (foundation_pour_failed)
    ↓ Triggered by
  User michael@fca (Foreman, Project A)
    ↓ Context
  Project proj_123, Phase: Foundation, Weather: Rain
  ```

**Search**: Full-text search across all audit records

---

### 6. System Health (Top Right)
**Purpose**: Ensure all services operational

**Status Indicators**:
- ✅ MongoDB Atlas: Connected (42ms latency)
- ✅ FCA Ecosystem API: Operational (cache hit rate: 85%)
- ✅ Ollama Backend: Online (3 models loaded)
- ✅ Learning Pipeline Worker: Active (last cycle: 2m ago)
- ✅ Predictive Orchestrator: Active (last scan: 3m ago)
- ✅ Corpus: 2,847 documents indexed

**Visual**: Green/yellow/red status dots + expandable details

---

## 🔌 API Endpoints

### Dashboard Data Aggregation

```http
GET /api/intelligence/dashboard/overview
Response:
{
  "period": "24h",
  "metrics": {
    "events_captured": 234,
    "outcomes_recorded": 156,
    "outcomes_verified": 142,
    "knowledge_gaps_identified": 23,
    "recommendations_generated": 213,
    "predictive_transfers": 45,
    "issues_prevented": 8,
    "estimated_savings_usd": 127000,
    "learning_cycle_time_minutes": 18.5
  },
  "health": {
    "atlas_status": "healthy",
    "fca_api_status": "healthy",
    "ollama_status": "healthy",
    "worker_status": "active",
    "orchestrator_status": "active"
  }
}
```

```http
GET /api/intelligence/dashboard/learning-loop
Response:
{
  "stages": [
    {
      "name": "Event Capture",
      "count_24h": 234,
      "count_7d": 1823,
      "avg_per_hour": 9.75,
      "status": "active"
    },
    { ... }
  ],
  "current_throughput": 9.75,
  "bottleneck_stage": null
}
```

```http
GET /api/intelligence/dashboard/predictive-transfers?limit=20
Response:
{
  "transfers": [
    {
      "transfer_id": "xfer_abc123",
      "timestamp": "2026-08-28T15:32:04Z",
      "source_outcome": "out_12345",
      "source_project": "Project Alpha",
      "target_projects": ["Project Beta", "Project Gamma"],
      "similarity_scores": [0.85, 0.78],
      "matched_factors": ["construction_phase", "event_type"],
      "predicted_timeframe": "within 7 days",
      "status": "delivered"
    }
  ],
  "total_active": 45
}
```

```http
GET /api/intelligence/dashboard/knowledge-gaps?status=active
Response:
{
  "gaps": [
    {
      "gap_id": "gap_xyz789",
      "pattern": "concrete curing in humid conditions",
      "frequency": 12,
      "severity": "high",
      "recommendations_count": 5,
      "academy_link_status": "linked",
      "first_identified": "2026-08-15T10:00:00Z"
    }
  ],
  "total_active": 23,
  "coverage_percentage": 87.5
}
```

```http
GET /api/intelligence/dashboard/audit-trail?limit=50
Response:
{
  "actions": [
    {
      "timestamp": "2026-08-28T15:32:04Z",
      "actor": "system:predictive-intelligence",
      "action_type": "predictive_transfer",
      "resource_type": "project",
      "resource_id": "proj_456",
      "description": "Proactive knowledge transfer triggered",
      "details": { ... },
      "result": "success"
    }
  ]
}
```

### Real-Time Updates (SignalR Hub)

```javascript
// Client subscribes to real-time intelligence updates
connection.on("NewOutcome", (outcome) => {
  // Update outcome counter
});

connection.on("PredictiveTransfer", (transfer) => {
  // Add to predictive transfers feed
});

connection.on("RecommendationGenerated", (rec) => {
  // Update recommendations list
});

connection.on("MetricsUpdate", (metrics) => {
  // Refresh dashboard metrics
});
```

---

## 🎨 UI/UX Design Principles

### Visual Hierarchy
1. **Executive metrics** (top, large)
2. **Learning loop** (center left, visual)
3. **Predictive transfers** (center right, actionable)
4. **Details** (bottom, tables/feeds)

### Color Scheme
- **Green**: Success, healthy, verified
- **Blue**: In progress, active, information
- **Yellow**: Warning, attention needed
- **Red**: Failed, critical, blocked
- **Purple**: Predictive, innovative, AI-powered

### Interaction Patterns
- **Real-time updates**: No refresh needed, uses SignalR
- **Drill-down**: Click any metric/item for details
- **Filters**: Quick filtering without page reload
- **Responsive**: Works on desktop, tablet, mobile

### Accessibility
- ARIA labels on all interactive elements
- Keyboard navigation support
- High-contrast mode option
- Screen reader compatible

---

## 🚀 Implementation Plan

### Phase 1: Backend API (Priority)
1. ✅ `IntelligenceDashboardService.cs` - Aggregate metrics
2. ✅ `IntelligenceDashboardController.cs` - API endpoints
3. ✅ `RealtimeIntelligenceHub.cs` - SignalR hub

### Phase 2: Core Dashboard Components
4. ✅ `Dashboard.razor` - Main page layout
5. ✅ `MetricCard.razor` - Reusable metric display
6. ✅ `LearningLoopView.razor` - Visual pipeline

### Phase 3: Intelligence Visualizations
7. ✅ `PredictiveTransfersView.razor` - Transfer feed
8. ✅ `KnowledgeGapsView.razor` - Gap analysis
9. ✅ `RecommendationsView.razor` - Active recommendations

### Phase 4: Observability & Audit
10. ✅ `AuditTrailView.razor` - Activity feed
11. ✅ `SystemHealthView.razor` - Service status
12. ✅ `ActivityFeed.razor` - Real-time event stream

### Phase 5: Polish & Testing
13. ✅ Responsive styling (Tailwind CSS)
14. ✅ Real-time SignalR integration
15. ✅ Error handling & loading states
16. ✅ Documentation

---

## 📈 Success Criteria

### Functional
- ✅ Dashboard loads in < 2 seconds
- ✅ Real-time updates without page refresh
- ✅ All metrics accurate (verified against Atlas)
- ✅ Drill-down navigation works for all items
- ✅ Mobile responsive (≥ 768px)

### User Experience
- ✅ At-a-glance system understanding
- ✅ Clear visual hierarchy
- ✅ Actionable intelligence (not just data)
- ✅ Feels "alive" (real-time updates visible)

### Technical
- ✅ No N+1 queries (efficient aggregation)
- ✅ SignalR connections stable
- ✅ Graceful degradation if SignalR unavailable
- ✅ Audit trail queryable and searchable

---

## 🔮 Future Enhancements

### Advanced Visualizations
- Interactive network graph for project knowledge flow
- Heatmap of knowledge gaps by phase/role
- Time-series charts for learning velocity
- Predictive trend analysis (ML-powered)

### Customization
- User-specific dashboard configurations
- Role-based metric visibility
- Custom alert thresholds
- Export/share dashboard snapshots

### Integration
- Slack notifications for critical alerts
- Email digests of weekly intelligence
- Mobile app push notifications
- Third-party BI tool connectors

---

**Next**: Immediate execution - building backend services and dashboard components.
