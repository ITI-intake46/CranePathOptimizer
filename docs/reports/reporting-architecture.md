# Reporting Architecture

## Purpose

The reporting feature provides reusable, user-facing exports for project summaries and detailed optimization results. Reports are intended for engineers and project stakeholders who need readable CSV or PDF output from completed workflows.

## Reports vs GA Diagnostics

Reports and GA diagnostics are separate contracts.

Reports are user-facing artifacts built from typed report inputs and exported through reusable report exporters. GA diagnostics are research, debug, benchmark, and audit artifacts used to inspect optimizer behavior. Do not mix report models with GA diagnostic DTOs, CSV layouts, or benchmark output contracts.

## Architecture

The reporting flow is:

```text
Typed Report Input
        |
Typed Report Builder
        |
Generic ReportDocument
        |
Report Validation
        |
Reusable Export Service
        |
CsvReportExporter / PdfReportExporter
```

Application owns the report models, report interfaces, typed builders, validation, formatting, report definitions, and orchestration. Infrastructure owns CSV, PDF, and file-system writing. UI should only trigger export commands, build report inputs from already-available workflow data, and choose output paths.

Application does not reference Infrastructure. Domain is not involved in report persistence or export concerns.

## Current Report

The first implemented report is the Optimization Summary Report.

It is built from `OptimizationReportInput`, which wraps the existing `PlacementInput` and `PlacementResult`. `OptimizationSummaryReportBuilder` produces a generic `ReportDocument` containing:

- Summary metrics
- Crane positions table
- Failure breakdown table
- Element lift details table

## Supported Formats

Current supported formats:

- CSV
- PDF

## CSV Behavior

`CsvReportExporter` can export any `ReportDocument` that contains at least one report table.

Metrics-only reports can still be valid report documents and can be rendered as PDF, but CSV export returns a clear failure result when no tables exist. For a single-table report written to a `.csv` path, the exporter writes one CSV file. For multi-table reports, or directory/package output paths, it writes:

- `_metadata.csv`
- One CSV file per report table
- `report_manifest.json`

CSV values use invariant formatting through `IReportValueFormatter`. Headers include units when available, such as `Weight (ton)`. If multiple tables sanitize to the same file name, the exporter writes unique names such as `element_lift_details.csv` and `element_lift_details_2.csv`; the manifest records the actual generated file names.

## PDF Behavior

`PdfReportExporter` can render any `ReportDocument` to a simple professional PDF layout with:

- Report header
- Project, IFC, crane, and created-date metadata
- Summary metrics
- Sections and tables in document order
- Footer with generator text and page number

PDF generation is performed off the caller thread, so UI commands can await export without running QuestPDF work synchronously on the WPF UI thread. Large tables are truncated in PDF using `PdfExportOptions.MaxRowsPerTable`. The PDF includes a note when truncation occurs. CSV export remains the full-data format for detailed table data.

## Adding a New Report

To add a new report:

1. Create a typed report input DTO if the report needs one.
2. Create a builder implementing `IReportBuilder<TInput>`.
3. Return a generic `ReportDocument` from the builder.
4. Add a `ReportDefinition` to `ReportDefinitionProvider` for user-facing reports.
5. Reuse `IReportExportService`, `CsvReportExporter`, and `PdfReportExporter`.
6. Register the builder in DI if it is resolved by the UI or another service.
7. Add focused builder, validation, and export tests.

When adding CSV-heavy reports, prefer stable table names and column keys. The CSV exporter guards against duplicate file names, but meaningful unique table names make packages easier for users to navigate.

## Limitations

Not implemented in this foundation:

- Excel export
- Report history database
- Template editor
- Chart rendering
- Full Reports tab or dashboard
- Report preview UI
- Digital signatures or stamps
