# AI Context Bridge v2

This patch moves the AI layer from a mostly high-level companion to a richer deterministic evidence interpreter.

## Design rule

AI remains advisory. It may explain, summarize, draft notes, and propose confirmable UI helper actions. It must not silently mutate engineering data, override load-chart calculations, certify lift safety, or replace deterministic readiness checks.

## New context contributor contract

`CraneOptimizer.UI.AIIntegration.IAiScreenContextContributor` lets each UI feature contribute a read-only `AiScreenContextSnapshot` to `AiHostContextBridge`.

Implemented contributors:

- `IfcAiContextContributor` — import readiness, GA candidate/block counts, identity/weight/COG/model quality messages.
- `ElementManagementAiContextContributor` — selected element identity, Assembly Mark source/confidence/explicit flag, validation warnings, weight trust, hidden contributors.
- `CraneLoadChartAiContextContributor` — crane count, selected crane, active specification/capacity/radius evidence, not-charted result signals.
- `GaAiContextContributor` — latest result coverage, dominant failure class, selected point failures, reachability/candidate evidence, diagnostics path.
- `ReportAiContextContributor` — report readiness, import/optimization blockers, diagnostics path, and AI narrative draft readiness.

## Workflow readiness integration

`WorkflowReadinessService` now imports warning/error issues from screen contexts into the deterministic readiness checklist. This means the AI panel and cards can surface the same concrete evidence visible in F2/F5/F8/F12 instead of only high-level counts.

## Confirmable actions

The companion still requires user confirmation. Supported safe actions now include:

- Navigate to a recommended tab.
- Copy readiness evidence.
- Copy an AI-assisted report narrative draft for explicit reviewer approval.

The report narrative is intentionally clipboard-only. It is not inserted into final reports automatically.

## Prompt context

The hidden live UI context sent to the local Ollama assistant now includes screen evidence summaries, metrics, warning/error issues, diagnostics paths, and the deterministic next action. The prompt explicitly instructs the model not to invent project facts beyond that evidence.

## Safety boundary

The AI layer can say what deterministic services found. It cannot make a lift safe, validate manufacturer data, or certify reports. Final engineering review remains required.

## No-GA-candidate blocker handling

The bridge now treats zero valid GA lift units as a first-class AI workflow blocker instead of a generic “open IFC model” hint. When `NoGaCandidates`, `GaCannotStartNoCandidates`, `GaPreRunNoCandidates`, or `EstimatedWeightBlockedFromGA` appears, AI guidance must explain that the IFC can be loaded and visible while still lacking trusted lift-unit data for GA.

Additional safe actions are clipboard/prompt only:

- Copy BIM exporter checklist.
- Copy optimizer issue summary.
- Stage a focused blocker question for local Ollama.

These actions do not bypass optimizer eligibility and do not change domain/import/load-chart/report rules.
