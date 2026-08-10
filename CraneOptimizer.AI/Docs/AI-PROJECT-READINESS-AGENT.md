# Project Readiness Agent

## Purpose

The Project Readiness Agent is a governed, read-only workflow assistant in `CraneOptimizer.AI`. It converts a user goal such as:

> Can I run the optimizer now, and what should I do next?

into an auditable plan of typed AI-owned tool calls against one immutable `AiWorkflowContextSnapshot`.

The agent is deterministic and can run offline. It does **not** reimplement the genetic algorithm or the optimizer dialog's built-in preflight.

## Optimizer authority boundary

The current `div` host contract exposes project facts, screen facts/issues, and completed-result facts, but it does not expose the optimizer dialog's authoritative **Current Run Readiness** decision. Therefore:

- the AI may classify visible project prerequisites such as project, IFC, zones, crane, and current crane/load-chart evidence;
- import/readiness candidate counts and weight warnings are troubleshooting evidence only;
- the AI must not translate those candidate/weight signals into an authoritative optimizer `Ready` or `Blocked` state;
- a completed optimization result proves only the scope/result captured for that run, not current rerun eligibility;
- the definitive run decision is made in **ANALYSIS → Run Optimizer → Current Run Readiness**;
- if the optimizer itself reports a blocker, the AI may explain existing evidence but must never suggest bypassing it.

## Runtime flow

```text
User goal
  -> readiness intent gate
  -> deterministic plan
  -> approval policy
  -> typed read-only tools
  -> bounded execution trace
  -> project RAG evidence retrieval
  -> authority-safe conclusion and ordered actions
  -> in-memory audit record
```

## Registered tools

| Tool | Purpose | Access |
|---|---|---|
| `get_project_overview` | Project identity, model revision, counts, and workflow baseline | Read-only |
| `get_bim_readiness` | BIM/model scope plus AI-visible import/readiness candidate and weight signals | Read-only |
| `get_zone_summary` | Allowed and forbidden placement-zone evidence | Read-only |
| `get_crane_setup` | Crane selection and current crane/load-chart evidence visible to AI | Read-only |
| `get_ga_readiness` | Visible optimizer prerequisites, advisory candidate evidence, and captured result state; does not replace optimizer preflight | Read-only |
| `get_critical_issues` | Ranked host-reported errors and warnings; severity alone does not make an optimizer blocker | Read-only |
| `search_project_evidence` | Hybrid RAG retrieval with citation IDs | Read-only |

Every tool publishes an AI-owned definition containing its input/output schema, access mode, permission level, timeout, and approval requirement.

## Governance

- Registered inspection tools are read-only.
- The default approval policy blocks every mutating tool until explicit user approval exists.
- Unregistered tools are never invoked.
- Tool execution is bounded to eight planned steps.
- Each tool has a timeout and isolated failure handling.
- The agent records model revision and fingerprint with each run.
- Tool results and RAG citations are exposed in the UI.
- No AI tool may change IFC, crane, zone, GA, report, or project state without its declared approval path.
- Engineering review remains mandatory.

## User experience

Readiness and blocker questions change the composer action from **Send** to **Run Agent**. The resulting answer includes:

- agent goal and run status;
- plan and tool execution (`T1`, `T2`, ...);
- deterministic findings;
- ordered recommended actions;
- retrieved evidence (`E1`, `E2`, ...);
- governance statement.

When visible project prerequisites are complete but current optimizer eligibility is not exposed to AI, the terminal AI readiness state is **optimizer verification required**, not `Ready`. The recommended next click is **ANALYSIS → Run Optimizer**, where the application's own Current Run Readiness remains authoritative.

## Isolation

All agent contracts, planning, policies, tools, audit storage, synthesis, and AI-side readiness interpretation live in `CraneOptimizer.AI`. Host projects are not modified by this strict-isolation implementation.

## Executable next actions

A completed readiness run can produce typed, evidence-linked action cards for navigation and explicitly governed artifacts. Navigation does not start GA. The optimizer's run decision remains outside AI ownership. See `AI-GOVERNED-AGENT-ACTIONS.md`.
