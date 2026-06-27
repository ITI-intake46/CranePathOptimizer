# User-Facing Reports

This document describes the first user-facing reporting system for CraneOptimizer. It is separate from GA diagnostics.

## Report Families and Names

The implementation has two internal report families:

- Element List Report
- Lifting Result Report

The internal family is not displayed in PDF or Excel metadata. The dialog labels the chooser as **Report Template**. The exported user-facing **Report Name** is always the selected template name.

Element List templates:

- `Basic Element List`
- `Weight Review`
- `Missing / Estimated Weight`
- `GA Readiness`

Lifting Result templates:

- `Basic Lifting Result`
- `Capacity Utilization Review`
- `Failed Lifts Only`

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

- `Optimization Result`
- `Liftable Items Only`
- `Failed Items Only`
- `Selected Setup Only`

`Rendered Elements Only` is an Items Included option, not a template.

## Metadata

PDF and Excel exports include a separate metadata area before the report table. Metadata includes:

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
- A3 landscape is used when selected column count is high.

Excel:

- `Report Info`
- `Report Data`
- `Crane Setups`, only for lifting reports with setup data.
- `Notes`, only when entered.

Templates save selected columns and settings only. They do not save report rows, element IDs, or optimization result data.
