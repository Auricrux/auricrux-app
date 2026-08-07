# Auricrux App Repo Phase 3 Execution

Date: 2026-07-17

## Goal

Make the standalone auricrux-app release process evidence-driven by adding training-candidate export, promotion gate checks, and red-team safety regression validation.

## Implemented

1. Training-candidate export pipeline:
   - [apps/auricrux-mobile/scripts/export_training_candidates.mjs](apps/auricrux-mobile/scripts/export_training_candidates.mjs)
   - [apps/auricrux-mobile/data/training/conversation_events.sample.jsonl](apps/auricrux-mobile/data/training/conversation_events.sample.jsonl)
2. Promotion-gate evaluator and metric baselines:
   - [apps/auricrux-mobile/scripts/eval_promotion_gate.mjs](apps/auricrux-mobile/scripts/eval_promotion_gate.mjs)
   - [apps/auricrux-mobile/eval/baseline_metrics.json](apps/auricrux-mobile/eval/baseline_metrics.json)
   - [apps/auricrux-mobile/eval/candidate_metrics.json](apps/auricrux-mobile/eval/candidate_metrics.json)
3. Red-team construction safety suite:
   - [apps/auricrux-mobile/scripts/eval_redteam_safety.mjs](apps/auricrux-mobile/scripts/eval_redteam_safety.mjs)
   - [apps/auricrux-mobile/policy/redteam_construction_suite.json](apps/auricrux-mobile/policy/redteam_construction_suite.json)
   - [apps/auricrux-mobile/policy/fixtures/redteam_candidate_responses.json](apps/auricrux-mobile/policy/fixtures/redteam_candidate_responses.json)
4. CI/release workflow enforcement:
   - [apps/auricrux-mobile/package.json](apps/auricrux-mobile/package.json)
   - [apps/auricrux-mobile/scripts/quality_gate.mjs](apps/auricrux-mobile/scripts/quality_gate.mjs)
   - [.github/workflows/auricrux-mobile-quality.yml](.github/workflows/auricrux-mobile-quality.yml)
   - [.github/workflows/auricrux-mobile-release.yml](.github/workflows/auricrux-mobile-release.yml)

## New Commands

1. npm --prefix apps/auricrux-mobile run ci:quality
2. npm --prefix apps/auricrux-mobile run ci:seed

## Outputs

1. Eval reports:
   - apps/auricrux-mobile/artifacts/eval/redteam_safety_report.json
   - apps/auricrux-mobile/artifacts/eval/promotion_gate_report.json
2. Training seed artifacts:
   - apps/auricrux-mobile/artifacts/training/candidates.jsonl
   - apps/auricrux-mobile/artifacts/training/summary.json

## Law Alignment

1. Construction-only route contract remains pinned to /construction.
2. Safety refusal/caution expectations are tested before release.
3. Promotion requires non-regression and threshold checks.
4. Positive, safe, construction-domain interactions are transformed into training seed candidates.
