# Agent Walkthrough

## Phase 6B Reports Closure Flow

1. Reports UI resolves the requested element scope (`All Elements`, `Selected Elements Only`, rendered/filter result, GA-ready, or needs-review) before building the report request.
2. `UserFacingReportBuilder` keeps the active user-facing flow: `ReportBuildRequestDto` -> providers -> `ReportDocumentDto` -> PDF/Excel exporters.
3. `ElementReportDataProvider` maps scoped elements and evaluates `ImportReadinessService.BuildSummary` once for that report element collection.
4. `ImportReadinessSummary.ElementDiagnostics` is the reporting-safe per-element readiness source for GA Readiness rows.
5. GA Readiness rows expose canonical label, mark confidence, validation status/warnings, weight trust level, IncludeInGa, ReadyForGa, readiness status, and blocking reasons.
6. Exporters remain presentation-only and outputs stay under `Exports/` through `IProjectOutputPathService`.

See `docs/reports/remaining-work-on-reports.md` before starting future report layout/export work.
