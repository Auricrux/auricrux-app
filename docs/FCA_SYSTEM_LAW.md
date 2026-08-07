# FCA System Law

## Purpose

This file defines the non-negotiable operating rules for FCA and Auricrux.
It is the canonical governance baseline for architecture, automation, and repository execution.

## Core Laws

1. **Continuous operation over ad hoc prompting**  
   The system should favor deterministic runtime execution, scheduled review, and governed work queues over manual prompt-only behavior.

2. **No disconnected features**  
   No feature should be treated as complete unless it connects to the FCA system spine where applicable:
   - Tenant / company
   - User / role
   - Project / job
   - Files / evidence
   - Outputs / deliverables
   - Audit / event log
   - Auricrux read / act / review capability

3. **Project and file linkage are mandatory**  
   Operational work must anchor to a project or job and must support source artifact linkage and output traceability.

4. **Every action creates evidence**  
   User actions, automation, and Auricrux actions must produce observable state, artifacts, or audit events.

5. **Autonomy must be governed**  
   Automated execution may not rely on uncontrolled cross-repo mutation, silent failure, or unverifiable self-loop behavior.

6. **Stable runtime before scope expansion**  
   Governance artifacts, runtime health, and the Project / File / Audit spine take priority over broad feature proliferation.

7. **Customer-facing continuity matters**  
   Website, Portal, Academy, Comms, and SaaS surfaces are one FCA system and should present consistent language, state, and operating continuity.

8. **Optional integrations do not define completeness**  
   External systems such as SharePoint, OneDrive, Microsoft 365, Stripe, or other connectors may accelerate delivery, but FCA must not structurally depend on them to be a coherent system.

9. **Validation gates are required**  
   Code, workflows, and automation should pass lint, build, smoke, and policy checks appropriate to their scope before they are treated as stable.

10. **Truth must be distinguishable from aspiration**  
    Current-state inventory, verified runtime behavior, and documented gaps must remain clearly separated from future architecture intent.

## Decision Gate For Any New Work

Before approving a feature, workflow, or automation change, answer all of the following:

- What tenant, customer, or company context does it attach to?
- What project or job does it attach to?
- What file inputs can it consume?
- What outputs does it create?
- What audit or event record does it write?
- How can Auricrux read it?
- How can Auricrux act on it?
- How can Auricrux review or correct it?
- What validation proves it works?

If these cannot be answered for the proposed scope, the work is not ready.

## Enforcement Priority

When priorities conflict, use this order:

1. Security and least privilege
2. Runtime health and deterministic execution
3. Project / File / Audit continuity
4. Customer-facing path continuity
5. New lifecycle expansion
6. Nice-to-have surface polish
