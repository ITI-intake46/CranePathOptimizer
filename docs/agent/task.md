# Current Task

## Phase 6B Reports Structural Closure

Implemented:

- GA Readiness rows now use application-layer readiness diagnostics instead of simplified weight-only readiness.
- Per-element readiness diagnostics are exposed through `ImportReadinessSummary.ElementDiagnostics`.
- GA Readiness default scope is explicit `All Elements`; `Current GA Scope` remains documented future work.
- Remaining PDF, Excel, and template polish work is documented in `docs/reports/remaining-work-on-reports.md`.

Explicitly deferred:

- Full PDF redesign.
- Full Excel redesign.
- WPF Reports tab redesign.
- Current GA Scope if it requires F8 viewport/service coupling.
- GA optimizer logic changes.
- IFC extraction or Tekla alias changes.
