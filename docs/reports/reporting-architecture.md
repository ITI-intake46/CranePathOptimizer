
# Reporting Architecture

## GA-Aware Reports

Pre-GA reports use the current project/model state. The normal user-facing report is Element List; readiness evidence is included as Element List validation/blocking columns rather than exposed as a separate normal report.

Post-GA reports use `IGaResultSnapshotProvider` and `IGaSnapshotReportAdapter`. They must build rows from one `ResolvedGaResultSnapshot` and must not combine saved result rows with current optimizer dialog settings, current crane selection, latest history rows, or unrelated diagnostics files.

`ReportBuilderViewModel` resolves GA data asynchronously for preview/export. It prefers the Analyze selected saved run, then current-session snapshots, production JSON, historic same-project JSON with a warning, and finally supported legacy CSV. The resolved snapshot supplies report metadata, source warnings, rows, crane setup summaries, and export evidence for Preview, PDF, and Excel.

Preview/export parity is required: once a preview document has been built, PDF and Excel export that same immutable `ReportDocumentDto`. If the user exports before preview, the view model builds and caches one document, then exports it. Export commands must not rebuild post-GA rows from a different selected run, live crane, current optimizer dialog settings, or changed scene state.

Each built `ReportDocumentDto` carries a short user-facing `ReportReference` plus internal `DocumentId`, `ReportBuildSessionId`, UTC generation timestamp, input signature, and source signature. Preview, PDF, and Excel expose the same `ReportReference` so a user can confirm exported files came from the exact previewed document. Internal IDs and signatures remain available for diagnostics/tests but are not normal user-facing metadata.

Lifting Summary is resolved through this report-provider path. The Analyze command opens the `GA Result Report` template with `Final GA Result` selected and passes the selected saved run identity into Report Builder. It must not open the GA Results window just to populate report data. Report dialog initialization is async; user-template loading is awaited instead of synchronously blocking the WPF thread.

Legacy CSV snapshots have schema version 0 and partial capabilities. Basic final-assignment reports can use final outcome rows without a full matrix; selected-point feasibility and assignment-review reports require `HasPositionEvaluationMatrix`.

## Column Evidence Scope

Report columns carry an explicit evidence scope and supported report families. The template catalog is the authoritative source for whether a column belongs to current-model reports or post-GA snapshot reports.

- Element List exposes current-model/readiness columns only. It does not expose `LiftStatus`, `RequiredCapacity`, `RequiredBoomLength`, assignment ownership, final-owner, or capacity result columns.
- The normal Lifting Result template is `GA Result Report`, backed by one resolved `GaResultSnapshotDto`.
- User templates saved before this separation are sanitized when loaded for a family. Unsupported post-GA columns are dropped from Element List/GA Readiness selection instead of causing the builder to join unrelated lifting data into a pre-GA report.
- Shared display identity columns such as `ElementMark` remain available to current-model reports, but technical GA result columns stay in the Lifting Result family.

`ReportBuilderViewModel` determines whether GA data is required from catalog metadata, not from a local hard-coded list. This keeps preview, warning text, PDF, and Excel export aligned with the same report-family decision.

## Engineering Field Mapping

Post-GA reports use saved snapshot values as authoritative. `RequiredBoomLength` maps from the saved required boom field first (`RequiredBoomLength`/`RequiredBoomLengthM`), then best-failed required boom evidence when applicable, and only falls back to generic boom length for legacy compatibility.

Element mark display fallback is `CanonicalMark`, `ElementMark`, `ElementName`, then `--`. `LiftUnitId` and `SourceElementGlobalId` remain technical identity fields and are not used as display-mark fallbacks.
