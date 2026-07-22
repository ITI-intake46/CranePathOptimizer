# Approval-Gated Draft Artifacts

The Project Readiness Agent can now propose one deliberately scoped state-changing action:

- `prepare_readiness_remediation_draft`

The tool creates a Markdown remediation draft under the user's local application-data folder:

`CraneOptimizer/AI/Drafts`

## Governance contract

The proposal is classified as `Mutating` because it writes a file. It therefore cannot execute from planning, background processing, or an anonymous caller. The desktop user must select **Approve once** on the proposal card.

Approval is bound to:

- the proposal ID;
- the agent run ID;
- the captured model revision and fingerprint;
- the registered tool access mode, permission, action type, and target;
- the evidence IDs attached to that agent run.

A consumed approval cannot be replayed. Stale, downgraded, or evidence-mismatched proposals fail before the tool is invoked.

## Impact and reversibility

The approved action creates one local Markdown file containing:

- project and agent-run provenance;
- deterministic conclusion;
- typed-tool findings and facts;
- ordered remediation actions;
- RAG evidence citations;
- the approval and governance statement.

The file is fully reversible by deleting it. The action does **not** modify project, IFC, crane, zone, GA, report, optimizer, or engineering-input state and never starts optimization.

## User experience

Mutating proposal cards expose:

- evidence references;
- risk;
- **Review details** with impact and reversibility;
- **Approve once**;
- **Reject**.

Successful execution records the artifact path, SHA-256, size, user identity, model revision, evidence IDs, duration, status, and audit ID.
