# Governed Agent Actions

The Project Readiness Agent can now convert deterministic recommendations into user-invoked application actions without bypassing engineering controls.

## Safety model

- The agent never executes a navigation action in the background.
- A navigation tool runs only after the user selects its action card.
- Navigation tools may open a workspace, but they cannot alter IFC, crane, zone, GA, report, or project data.
- Mutating action tools are denied until an explicit one-time user approval is supplied.
- Every attempted action is recorded with an audit ID, agent run ID, model revision, model fingerprint, evidence IDs, initiator, timing, status, and governance result.

## Initial action tools

| Tool | Target | Access |
|---|---|---|
| `open_crane_setup` | CRANES | Navigation |
| `open_load_chart_import` | CRANES | Navigation |
| `open_element_export` | IFC MODEL | Navigation |
| `open_element_management` | IFC MODEL | Navigation |
| `open_zone_manager` | ZONES | Navigation |
| `open_ga_readiness` | ANALYZE | Navigation |

## Runtime flow

```text
Readiness agent run
  -> deterministic recommended actions
  -> typed action proposals with evidence IDs
  -> explicit user click
  -> approval-policy evaluation
  -> UI navigation request
  -> action execution audit
```

## Host boundary

Action implementations remain in `CraneOptimizer.AI`. They communicate through the existing AI-owned `IUiInteractionContextService`. The WPF host only interprets the navigation request and selects the corresponding ribbon workspace.

## Future mutating tools

A future tool that changes GA settings, crane configuration, report drafts, or project data must declare `AiToolAccessMode.Mutating`. The default approval policy blocks it unless the execution request includes explicit user approval. Rejection and blocked attempts are audited without invoking the tool.

## Approval-gated draft artifact

The action registry also contains `prepare_readiness_remediation_draft`, a mutating but reversible local-artifact tool. It is blocked until a named desktop user selects **Approve once**. Approval is run-bound, model-bound, evidence-bound, definition-bound, and single-use. The generated Markdown draft is stored outside the repository and does not alter engineering or project state. See `AI-APPROVAL-GATED-DRAFT-ARTIFACTS.md`.
