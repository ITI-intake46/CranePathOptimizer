# Remaining Work on Reports

## Purpose

The active report architecture is now structurally stable. Future report work should focus on visual layout, PDF pagination, Excel usability, and template-specific polish without changing the active builder/provider/exporter ownership model.

## Current Stable Structure

- Active engine: `UserFacingReportBuilder`
- Active PDF exporter: `UserFacingReportPdfExporter`
- Active Excel exporter: `UserFacingReportExcelExporter`
- Active DTO: `ReportDocumentDto`
- Active paths: `IProjectOutputPathService`
- Identity SSOT: `CanonicalMarkResolver`
- Weight SSOT: `ReportWeightMapper`
- Obsolete headless reporting foundation must not be used for new work.

## Phase 6B Closure Scope

- Fixed GA Readiness alignment with readiness rules.
- No full PDF/Excel redesign was implemented.
- No new builders/exporters/providers were introduced.
- No folder contract changes were made.

## Required Audit Steps Before Any Future Report Work

For every future report task, agents must first inspect:

- `ReportTemplateCatalog`
- `UserFacingReportBuilder`
- `ElementReportDataProvider`
- `LiftingResultReportDataProvider`
- `ReportDataSourceAuditProvider`
- `UserFacingReportPdfExporter`
- `UserFacingReportExcelExporter`
- `ReportBuilderViewModel`
- `IProjectOutputPathService`
- `CanonicalMarkResolver`
- `ReportWeightMapper`
- `ReportValueFormatter`

They must answer:

- Is this user-facing or diagnostic?
- Which provider owns the data?
- Which DTO carries the data?
- Which exporter writes it?
- Which folder owns the output?
- Does it use `CanonicalMarkResolver`?
- Does it use `ReportWeightMapper`?
- Does it bypass `IProjectOutputPathService`?
- Is there already an existing class that does this?

## Remaining Phase 6C - PDF Layout Improvements

- Add repeating report title/header on table pages.
- Add project name, report name, export timestamp.
- Improve column width strategy.
- Reduce horizontal page splitting.
- Add conditional row highlights for blocked/warning/failed rows.
- Add alternating row backgrounds.
- Keep exporters presentation-only.
- Do not query domain entities from exporters.

## Remaining Phase 6D - Excel Export Improvements

- Auto-calculate column widths.
- Keep numeric cells numeric.
- Use blank cells for missing numeric values instead of text dashes.
- Freeze headers.
- Improve header styling.
- Add warning/failure cell styles.
- Preserve filters.
- Keep exporters presentation-only.

## Remaining Phase 6E - Template-Specific Polish

Reports:

- Basic Element List
- Weight Review
- GA Readiness
- Basic Lifting Result
- Capacity Utilization Review
- Failed Lifts Only
- GA Assignment Review
- Report Data Source Audit
- Missing / Estimated Weight

For each report:

- Verify columns.
- Verify units.
- Verify row ordering.
- Verify filters.
- Verify warning messages.
- Verify PDF and Excel output.

## Known Decisions

- Do not revive obsolete headless reporting foundation.
- Keep `BenchmarkV2CsvWriter` `ga_audit_*` names for external analytics compatibility.
- Keep final exports under `Exports/`.
- Keep diagnostics under `Reports/`.
- Keep logs under `Logs/`.
- Keep model imports under `Models/`.
- Do not change GA logic during report work.
- Do not change IFC extraction during report work.
- Current GA Scope is future work unless it can be resolved without coupling Reports to F8 viewport services.

## Acceptance Criteria for Future Report Work

- No duplicate builders/exporters/providers.
- No direct database queries from exporters.
- No path construction outside `IProjectOutputPathService`.
- No changes to GA/IFC logic.
- Tests pass.
- Build has 0 errors.
- Warning count does not exceed accepted gate.
