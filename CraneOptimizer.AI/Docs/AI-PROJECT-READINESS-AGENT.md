# Project Readiness Agent

## Purpose

The Project Readiness Agent is the first governed agentic vertical slice in `CraneOptimizer.AI`. It converts a user goal such as:

> Why is optimization blocked, which evidence proves it, and what should I do next?

into an auditable plan of typed, read-only tool calls against one immutable `AiWorkflowContextSnapshot`.

It does not depend on Ollama, a cloud model, or prompt-only reasoning. The agent is deterministic and can run offline.

## Runtime flow

```text
User goal
  -> readiness intent gate
  -> deterministic plan
  -> approval policy
  -> typed read-only tools
  -> bounded execution trace
  -> project RAG evidence retrieval
  -> conclusion and ordered actions
  -> in-memory audit record
```

## Registered tools

| Tool | Purpose | Access |
|---|---|---|
| `get_project_overview` | Project identity, model revision, counts, and readiness baseline | Read-only |
| `get_bim_readiness` | Scene/planning counts and trusted GA-ready lift-unit evidence | Read-only |
| `get_zone_summary` | Allowed and forbidden placement zones | Read-only |
| `get_crane_setup` | Crane selection and load-chart evidence | Read-only |
| `get_ga_readiness` | GA pre-run blocker and result state | Read-only |
| `get_critical_issues` | Ranked host-reported errors and warnings | Read-only |
| `search_project_evidence` | Hybrid RAG retrieval with citation IDs | Read-only |

Every tool publishes an AI-owned definition containing its input/output schema, access mode, permission level, timeout, and approval requirement.

## Governance

- The first agent milestone registers only read-only tools.
- The default approval policy blocks every mutating tool until explicit user approval exists.
- Unregistered tools are never invoked.
- Tool execution is bounded to eight planned steps.
- Each tool has a timeout and isolated failure handling.
- The agent records model revision and fingerprint with each run.
- Tool results and RAG citations are exposed in the UI.
- No tool may change IFC, crane, zone, GA, report, or project state.
- Engineering review remains mandatory.

## User experience

Readiness and blocker questions change the composer action from **Send** to **Run Agent**. The resulting answer includes:

- agent goal and run status;
- plan and tool execution (`T1`, `T2`, ...);
- deterministic findings;
- ordered recommended actions;
- retrieved evidence (`E1`, `E2`, ...);
- governance statement.

AI Assist shows a live agent trace above RAG evidence. Full Copilot exposes the same trace in the **Agent Run** audit tab.

## Isolation

All agent contracts, planning, policies, tools, audit storage, synthesis, and UI bindings live in `CraneOptimizer.AI`. Host projects continue to provide only AI-owned immutable snapshots through existing host contracts.
