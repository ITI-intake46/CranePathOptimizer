# User-Facing Reports

## Post-GA Result Source

Post-GA report families use one resolved GA run snapshot. Report Builder must not mix rows from one run with crane, settings, timestamp, or diagnostics metadata from another run.

Resolution priority:

1. Explicitly selected report run.
2. Analyze tab selected saved run.
3. Matching current-session snapshot.
4. Latest completed `Current` production JSON.
5. Latest completed `CurrentByIfcHash` production JSON.
6. Latest completed `CurrentProjectUnverified` production JSON.
7. Latest same-project `Historic` production JSON with a warning.
8. Supported legacy CSV snapshot.

Preview, PDF export, and Excel export use the same `ResolvedGaResultSnapshot` and immutable `ReportDocumentDto`, and therefore the same `OptimizationRunId`, rows, metadata, warnings, capability checks, and user-facing `ReportReference`.

## Capability Rules

Legacy CSV snapshots can support some reports without the complete final-position matrix:

- Basic Lifting Result and Final Assignment require final outcomes plus one final or best-failed evaluation per row.
- Capacity Utilization Review requires final outcomes and available capacity fields.
- Failed Lifts Only requires final outcomes and failure evidence.
- Crane Setup Summary requires saved positions and final assigned counts.
- Selected Point Feasibility, GA Assignment Review, and All Feasible Debug require the complete position/evaluation matrix.

Unsupported legacy modes must show a limitation message instead of silently returning empty rows or placeholder dashes.

This document describes the first user-facing reporting system for CraneOptimizer. It is separate from GA diagnostics.

## Report Families and Names

The implementation has two internal report families:

- Element List Report
- Lifting Result Report

The internal family is not displayed in PDF or Excel metadata. The dialog labels the chooser as **Report Template**. The exported user-facing **Report Name** is always the selected template name.

Normal user-facing templates:

- `Basic Element List`
- `GA Result Report`

`GA Readiness`, `Basic Lifting Result`, capacity/failed-lift/assignment-review/debug templates, and data-source audit templates remain internal compatibility or diagnostic templates. They are not listed by the normal report selector. Readiness information that users need is exposed through the Element List columns `Included in GA`, `Validation Status`, and `Blocking Reasons`; `Ready for GA` is not a normal user-facing column.

## Items Included

The UI and exports use **Items Included**, not "Report Source".

Element List options:

- `All Elements`
- `Rendered Elements Only`
- `GA-Ready Elements`
- `Needs Review Elements`
- `Selected Elements Only`
- `Current Filter Result`

Lifting Result options:

- `Final GA Result`
- `Optimization Result`
- `Liftable Items Only`
- `Failed Items Only`
- `Selected Setup Only`

## GA Result Source

Post-GA reports use one immutable `GaResultSnapshotDto` source. The report builder resolves sources in this order:

1. Explicitly selected completed persisted run.
2. Matching current-session snapshot for the active project/model.
3. Latest completed production JSON for the active project/model.
4. Latest historic JSON for the same project with an explicit historic-model warning.
5. Legacy CSV adapter with partial capabilities.
6. No result.

The JSON snapshot is the production contract. Memory is only the current-session fast path. CSV files are audit/research artifacts and legacy fallback. A report must not combine metadata from one run with rows from another run.

Pre-GA/current-model reports are separate:

- Element List

Post-GA reports are snapshot-backed:

- GA Result Report

If an Element List or GA Readiness template contains post-optimization columns, the builder should show an explicit validation message instead of silently filling dashes:

```text
This template contains post-optimization columns. Open it as a Lifting Result report or use a supported GA-aware report family.
```

The current template catalog also prevents this state during normal selection. Pre-GA available-column lists do not contain post-GA fields, and older user-saved templates are sanitized when loaded for Element List or GA Readiness. This keeps current-model reports from reaching into a restored GA snapshot or an unrelated diagnostics file.

Preview, PDF, and Excel export are generated from the same `ReportDocumentDto`. Export commands use the current preview document when present, preserving the resolved `OptimizationRunId`, source kind, rows, metadata, display values, warnings, and `ReportReference`. The document includes internal `DocumentId`, `ReportBuildSessionId`, input signature, and source signature for diagnostics and tests, but normal preview/export metadata does not expose these internals to users.

`Rendered Elements Only` is an Items Included option, not a template.

## Metadata

PDF and Excel exports include a separate metadata area before the report table. Metadata includes:

- Report Reference.
- Company logo and company name when provided.
- Report Name.
- Items Included.
- Project.
- Generated On.
- Created By.
- Issued To.
- Revision.
- Summary counts and percentages.
- Crane Information when available or relevant.
- Lifting Accessories for lifting reports.

No exported metadata field is named `Report Type`.
No exported user-facing metadata field is named `Document ID`, `Build Session ID`, `Source Signature`, or `Input Signature`.
The user does not edit the report date manually. `Generated On` is set automatically from the report document creation time when Preview, Export PDF, or Export Excel builds the report.

## Dialog Workflow

The report builder opens on the `Report Info` tab, not on a preview table.

- Top setup strip: `Report Template`, `Items Included`, read-only `Project`, and read-only `Generated On`.
- `Report Info`: optional company, logo, created by, issued to, revision, and general notes.
- `Columns`: Revit Schedule style `Available Columns` and `Selected Columns` lists.
- `Preview & Export`: friendly empty state until the user clicks Preview.

The columns tab supports add, remove, add all, remove all, move up, move down, reset to template, and basic selected-column settings for header, format, alignment, PDF visibility, and Excel visibility.

## Notes

General notes are user-entered free text.

- PDF adds a final `General Notes` page only when notes are provided.
- Excel adds a `Notes` sheet only when notes are provided.

## Element List Export Rule

Element List reports export one row per reportable item:

- If an element is a child inside an assembly, the parent assembly is exported.
- If an element is standalone, single unit, or lift unit, the element itself is exported.
- Duplicate reportable identities are removed.

The resolver deduplicates by stable element identity, preferring `ElementID`.

The normal Element List default columns are:

- No.
- Element Mark
- Element Name
- Element Type
- Material
- Length (m)
- Width (m)
- Height (m)
- Volume (m3)
- Weight (t)
- COG X (m)
- COG Y (m)
- COG Z (m)
- Assembly Member Count
- Included in GA
- Validation Status
- Blocking Reasons

The normal GA Result Report uses one row per submitted lift unit and excludes internal diagnostics such as assignment reason/score, final evaluation IDs, report reference as a table column, and technical IDs. Technical identifiers remain available only through explicit diagnostic/internal columns.

## Weight Mapping

Weight Verified display values:

- `Yes`
- `No`

Weight Status display values:

- `OK`
- `Estimated`
- `Missing`
- `Invalid`
- `Needs Review`

Warning codes are kept as enum values in data logic and mapped to user messages for display/export.

## Lifting Capacity

Use this user-facing wording:

```text
Capacity Factor: 0.90
```

Do not call it `Safety Factor: 1.10`.

Meaning:

```text
Available Capacity = Load Chart Capacity * Capacity Factor
```

Example:

```text
Load Chart Capacity = 12.00 tons
Capacity Factor = 0.90
Available Capacity = 10.80 tons
```

For exported optimization result rows, `AvailableCapacity` is already stored after the optimizer's capacity reductions. The reporting layer uses that value directly for utilization and does not derate it again.

```text
Capacity Utilization (%) = Required Capacity / Available Capacity * 100
```

Coverage is stored as a ratio from 0 to 1 and formatted with percentage formatting, for example `1.0` displays as `100.00%`.

`RequiredBoomLength` is distinct from generic boom length. Lifting reports map it from saved required-boom evidence first (`RequiredBoomLength` or `RequiredBoomLengthM`), then best-failed required-boom evidence, and only use generic `BoomLength` as a legacy compatibility fallback.

Element mark fallback for post-GA reports is:

1. `CanonicalMark`
2. `ElementMark`
3. `ElementName`
4. `—`

Technical identities such as `LiftUnitId` and `SourceElementGlobalId` stay in ID columns and are not used as display-mark fallback, especially for grouped/synthetic lift units.

Display values for lift status:

- `Liftable`
- `Out Of Radius`
- `Overweight`
- `Height Fail`
- `No Chart Data`

Capacity utilization is `N/A` for `Out Of Radius` and `No Chart Data`. `Overweight` can exceed 100%.

## Export Layout

PDF:

- Metadata page first.
- Main table starts after metadata.
- Notes appear at the end only when entered.
- A4 landscape by default for table reports.
- Wide table reports use a single full-width table with all selected columns together; rows continue vertically and headers repeat on continuation pages.
- Horizontal column bands such as `Columns 1 of 3` are not allowed.
- Page size escalates through A4, A3, A2, and A1 landscape before using a bounded custom-width landscape page for exceptionally wide reports.
- Detail font size is not reduced below 7 pt.
- Footer text includes report/run context and the user-facing Report Reference for preview/PDF/Excel traceability.

Excel:

- `Report Info`
- `Report Data`
- `Crane Setups`, only for lifting reports with setup data.
- `Notes`, only when entered.

Templates save selected columns and settings only. They do not save report rows, element IDs, or optimization result data.

## Assembly Summary UI presentation (F5 Panel)

When selecting the parent container, main part, or any child component of a valid assembly in the Element Management pane, an **"Assembly Summary"** group is displayed in the properties list.
The summary displays:
*   **Assembly**: The parent assembly mark (e.g. `W1`).
*   **Main Part**: The category name of the main part in uppercase (e.g. `WALL`).
*   **Has Secondary Parts**: `"Yes"` or `"No"`.
*   **Included Hidden Contributors**: `"Rebar weight included"` (if reinforcing/accessory parts are present) or `"None"`.
*   **Total Lift Weight**: The aggregate rolled-up weight (e.g. `9.28t`) formatted dynamically in unit settings format with a `t` suffix.
*   **Profile**: The profile dimension of the main part formatted as `Width*Length` in settings unit (e.g., `200*4000`), derived using `UnitSystem` display formatting.

## Hidden Contributor Filtering in Reports

Auxiliary elements classified as `HiddenWeightContributor` (reinforcement bars/meshes) or `AccessoryWeightContributor` (inserts/plates) are excluded from user-facing reports (such as normal element list reports and lifting reports) to keep user-facing views clean, while their weights are preserved in rolled-up assembly totals. Full breakdown of contributors is preserved in technical/debug reports only.

## Rollup Columns in Lifting Reports

The default **Basic Lifting Result** report template and available columns list include:
*   `ParentAssemblyGlobalId`: The ID of the parent container assembly.
*   `AssemblyLiftWeight`: The aggregate rolled-up lift weight of the assembly.
*   `AssemblyLiftCOG`: The aggregate weighted COG coordinate values.
*   `AssemblyMemberIds`: The list of all member element IDs included in the assembly.

## Debug Report Naming Convention

All generated diagnostic/debug CSV reports must follow this naming pattern:

`debug_<domain>_<target>_<purpose>.csv`

Where:
*   `debug` indicates that the file is diagnostic/internal and not a final user-facing report.
*   `<domain>` identifies the pipeline area, such as `ifc`, `ga`, `reports`, or `viewport`.
*   `<target>` identifies the inspected object or workflow, such as `assembly_lift_unit`, `normalization`, or `optimization_history`.
*   `<purpose>` describes the report purpose, such as `rollup_audit`, `classification_audit`, or `input_trace`.

Examples:
*   `debug_ifc_assembly_graph_classification_audit.csv`
*   `debug_ifc_assembly_lift_unit_rollup_audit.csv`
*   `debug_reports_lifting_result_input_trace.csv`

