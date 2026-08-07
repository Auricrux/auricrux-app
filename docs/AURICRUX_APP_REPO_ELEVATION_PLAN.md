# Auricrux App Repo Elevation Plan

Date: 2026-07-17

## Decision

Yes. Auricrux App should be promoted into its own repository now.

Current state in [apps/auricrux-mobile](apps/auricrux-mobile) is good for early iteration, but it is not sufficient for the quality, governance, and training-ground role requested for Auricrux.

## Evidence From Current Implementation

1. App is currently embedded in a large multi-domain monorepo: [apps/auricrux-mobile](apps/auricrux-mobile).
2. App runtime is a single-screen React Native/Expo surface with direct backend POST to /api/auricrux and feedback posts, but no dedicated app-domain audit, identity, or learning pipeline boundaries: [apps/auricrux-mobile/App.tsx](apps/auricrux-mobile/App.tsx).
3. Release workflow exists and works for EAS builds, but CI is release-oriented rather than quality-gate-oriented: [.github/workflows/auricrux-mobile-release.yml](.github/workflows/auricrux-mobile-release.yml).
4. No first-class mobile test suite or app-specific policy gates detected in the app folder.
5. FCA law baseline requires validated, auditable, integrated behavior before declaring completeness: [FCA_SYSTEM_LAW.md](FCA_SYSTEM_LAW.md), [auricrux-system-law.rules.json](auricrux-system-law.rules.json).

## Why Separate Repo Is The Correct Move

1. Product identity
   Auricrux App is becoming a standalone customer-facing intelligence product and training gateway, not just a UI client.
2. Quality velocity
   Mobile release cadence, app UX experimentation, and conversation quality tuning need independent pipelines from core FCA backend changes.
3. Governance clarity
   A dedicated repository allows explicit app laws, app-specific validation gates, and release compliance checks without being diluted by broader system churn.
4. Training and seeding reliability
   Conversation traces, feedback signals, and domain corrections can be versioned and audited as a product data lifecycle.

## Target Product Standard (Copilot/Gemini/Grok/ChatGPT Feel, Construction-Only)

1. Conversation quality
   - Fast turn latency and stable streaming.
   - High answer structure quality (plans, takeoffs, checklists, risk calls, cost rationale).
   - Deterministic refusal and escalation for unsafe construction guidance.
2. Domain focus
   - Construction-specific persona pack (estimator, field ops, PM, safety, closeout, warranty).
   - Job-context memory and artifact-grounded answers.
3. Trust and transparency
   - Visible confidence markers, source badges, and assumption flags.
   - Clear distinction between verified project facts and generalized guidance.
4. Training-ground behavior
   - Every user feedback event tied to trace ID and scenario taxonomy.
   - Replayable evaluation packs and measurable quality deltas per release.

## Mandatory Law Parity And Exceedance

Auricrux App must satisfy all FCA law principles and add app-specific controls.

1. Must keep Project/File/Audit spine compatibility with FCA core law: [FCA_SYSTEM_LAW.md](FCA_SYSTEM_LAW.md).
2. Must produce auditable evidence for every app action that can influence model behavior.
3. Must enforce validation gates before release promotion.
4. Must never treat UI polish as completion without data lineage, guardrails, and runtime verification.

## Proposed Repository Split

New repository name:
- auricrux-app

Repository scope:
1. Mobile clients (primary)
2. Optional web companion shell
3. App inference gateway layer (contract adapter, not core model training code)
4. App evaluation harness and red-team suites
5. App analytics and audit event schema

Move from current repo:
1. [apps/auricrux-mobile](apps/auricrux-mobile)
2. [.github/workflows/auricrux-mobile-release.yml](.github/workflows/auricrux-mobile-release.yml)
3. [.github/workflows/deploy_auricrux_mobile_cloudrun.yml](.github/workflows/deploy_auricrux_mobile_cloudrun.yml)
4. [.github/workflows/operate_auricrux_mobile_cloudrun.yml](.github/workflows/operate_auricrux_mobile_cloudrun.yml)

Retain in auricrux-central:
1. Core intelligence services and system law authority
2. Cross-system governance and shared contracts

## Required New Quality Gates In auricrux-app

1. Build and static quality
   - Type check
   - Lint
   - Dependency vulnerability scan
2. Mobile correctness
   - Unit tests for prompt shaping and response rendering
   - Integration tests for chat transport and retries
   - Device smoke tests on Android and iOS profiles
3. Safety and policy
   - Construction safety refusal/guardrail tests
   - Hallucination and citation stress tests
4. Data and training signal integrity
   - Feedback event schema validation
   - Trace completeness checks (trace ID, role, route, scenario tag)
5. Release governance
   - Promotion gates: dev -> staging -> production with measurable eval thresholds

## Reference App Architecture

1. Client layer
   - React Native app shell
   - Conversation UI, job context panel, artifact attachment panel
2. App gateway layer
   - Auth, tenant routing, request shaping, policy enforcement
   - Streaming response normalization
3. Auricrux intelligence layer
   - Existing /api/auricrux compatibility and specialist routing
4. Telemetry and training layer
   - Structured conversation events
   - Explicit training-candidate queue with human-review policy toggles

## Migration Plan

Phase 1: Foundation (1-2 days)
1. Create auricrux-app repository.
2. Move mobile app and workflows listed above.
3. Stand up CI baseline gates (typecheck, lint, unit tests).

Phase 2: Product hardening (3-5 days)
1. Implement app-level auth + tenant context envelope.
2. Add structured trace IDs and feedback schema.
3. Add safety/eval packs aligned with construction domain.

Phase 3: Training-ground readiness (3-5 days)
1. Add quality scoring pipeline for user sessions.
2. Add seed-export pipeline for FCA ecosystem modules.
3. Add promotion policy requiring eval delta improvement before release.

## Definition Of Done For This Transition

1. Auricrux App has its own repo, release lanes, and quality gates.
2. App passes FCA law parity checklist and app-specific extensions.
3. Feedback and trace data are auditable and training-ready.
4. Production release can be justified with evidence, not aspiration.

## Immediate Next Action

Approve repository split and execute Phase 1 in a dedicated branch this week.