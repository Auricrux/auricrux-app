# Auricrux App Repo Phase 2 Execution

Date: 2026-07-17

## Goal

Harden the standalone auricrux-app scaffold into a governed app product baseline with auditable envelopes, construction-only policy checks, and schema-evaluated feedback events.

## Phase 2 Changes

1. Added typed request and feedback envelope module:
   - [apps/auricrux-mobile/src/contracts/envelope.ts](apps/auricrux-mobile/src/contracts/envelope.ts)
2. Added trace/session IDs and optional app key header in runtime requests:
   - [apps/auricrux-mobile/App.tsx](apps/auricrux-mobile/App.tsx)
3. Added construction policy pack and evaluator:
   - [apps/auricrux-mobile/policy/construction_policy_pack.json](apps/auricrux-mobile/policy/construction_policy_pack.json)
   - [apps/auricrux-mobile/scripts/policy_eval.mjs](apps/auricrux-mobile/scripts/policy_eval.mjs)
4. Added feedback schema and fixture evaluator:
   - [apps/auricrux-mobile/policy/feedback_event.schema.json](apps/auricrux-mobile/policy/feedback_event.schema.json)
   - [apps/auricrux-mobile/policy/fixtures/feedback_event.valid.json](apps/auricrux-mobile/policy/fixtures/feedback_event.valid.json)
   - [apps/auricrux-mobile/scripts/feedback_schema_eval.mjs](apps/auricrux-mobile/scripts/feedback_schema_eval.mjs)
5. Wired policy/schema evals into CI quality command:
   - [apps/auricrux-mobile/package.json](apps/auricrux-mobile/package.json)
   - [apps/auricrux-mobile/scripts/quality_gate.mjs](apps/auricrux-mobile/scripts/quality_gate.mjs)

## New Quality Command Path

1. npm --prefix apps/auricrux-mobile run ci:quality
2. Includes: typecheck -> quality:gate -> eval:policy -> eval:feedback-schema

## Exit Criteria

1. ci:quality passes in standalone repo.
2. App requests include session and trace headers.
3. Feedback payloads follow typed envelope and schema harness.
4. Policy eval confirms construction-only route contract.
