# Firebase and Google Deployment Plan for Auricrux Mobile

## Objective

Offer a Google stack option without changing mobile UX or training feedback loops.

## Contract Rule

Keep request and response contract identical to current Azure endpoint.

Request body:

```json
{
  "message": "How do I protect margin this week?",
  "route": "/portal/platform",
  "context": {
    "source": "auricrux-mobile",
    "expertMode": "Executive",
    "specialistAgent": "executive-orchestrator"
  }
}
```

Response body:

```json
{
  "ok": true,
  "reply": "...",
  "action": { "type": "navigate", "href": "/portal/bids" },
  "route": "/portal/platform",
  "mode": "llm-assistant",
  "poweredByLlm": true
}
```

## Option A: Google Cloud Run (Recommended)

1. Containerize a thin API adapter that proxies to existing Auricrux core logic.
2. Expose POST /api/auricrux and pass through the same payload shape.
3. Use Cloud Run revisions for safe phased rollout.
4. Point app endpoint to Cloud Run URL for canary tenants.

## Option B: Firebase Functions

1. Create an HTTPS function named auricrux.
2. Implement POST handling with same JSON contract.
3. Keep feedback mode in same endpoint using rating field, matching Azure behavior.
4. Set mobile endpoint to:
   - https://us-central1-<project>.cloudfunctions.net/auricrux

## Suggested Rollout

1. Week 1: Internal QA on Google endpoint while Azure stays primary.
2. Week 2: 5-10% tenant traffic on Google endpoint.
3. Week 3: Keep dual-lane routing by package tier.
4. Week 4: Promote only if parity metrics hold.

## Metrics To Compare

- Reply success rate
- Median latency
- Thumbs up/down ratio
- Error rate by expert mode
- Voice playback completion rate

## Cost Control

- Use Cloud Run min-instances 0 for low idle cost.
- Keep request/response payload compact.
- Use capture-only mode for non-critical lanes.
- Enforce timeout budget and graceful fallback text.

## Why This Is Sellable

- Multi-cloud option reduces buyer risk concerns.
- Expert modes map to role-based training outcomes.
- Feedback loop supports measurable model improvement claims.
