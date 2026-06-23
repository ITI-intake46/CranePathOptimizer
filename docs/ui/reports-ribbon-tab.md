# Reports Ribbon Tab

The Reports tab now contains the first user-facing reporting workflow in addition to the existing GA diagnostics shortcuts.

## Active Report Buttons

- **All Elements List** opens the report builder for the internal Element List Report family.
  - Default report template: `Basic Element List`
  - Default items included: `All Elements`
- **Lifting Summary Report** opens the report builder for the internal Lifting Result Report family.
  - Default report template: `Basic Lifting Result`
  - Default items included: `Optimization Result`
  - The button is disabled until an optimization result exists.

The exported report metadata does not show the internal report family. For users, **Report Name** is the selected template name.

## Report Builder

The report builder dialog supports:

- Metadata entry: company, logo path, created by, issued to, revision, and general notes.
- A compact setup strip for **Report Template**, **Items Included**, read-only project, and automatic **Generated On**.
- A tabbed workflow: `Report Info`, `Columns`, and `Preview & Export`.
- Revit Schedule style column selection using **Available Columns** and **Selected Columns** lists.
- Column add/remove, add all/remove all, move up/down, reset to built-in template defaults, and basic column settings.
- Preview of the first 50 rows with report name, items included, preview row count, total item count, and warning count.
- PDF export.
- Excel export.
- Save As Template.

Logo browsing accepts `.png`, `.jpg`, and `.jpeg`. The selected logo path is used for the current export and is not copied into project assets.
The report date is not editable. It is generated automatically when a preview/export document is built.

## Export Structure

PDF exports are metadata-first:

- Page 1: report information, summary, optional crane information, optional lifting accessories, and lifting setup summary.
- Page 2 onward: main report table.
- Final page: general notes, only when notes are provided.

Excel exports use separate sheets:

- `Report Info`
- `Report Data`
- `Crane Setups`, only for lifting reports with setup data.
- `Notes`, only when notes are provided.

## Terminology

- **Report Name** means the selected template name, for example `Basic Element List` or `Capacity Utilization Review`.
- **Report Template** is the chooser label in the dialog.
- **Items Included** replaces the older "Report Source" wording.
- **Generated On** replaces manually edited report dates.
- **Report Type** is internal only and must not appear in exported metadata.
- `Rendered Elements Only` is an Items Included option, not a report template.

## Element List Rules

Element List reports export reportable lift items only:

- Assembly children resolve to the parent assembly when the parent is available.
- Standalone elements export as themselves.
- Duplicate parent rows are removed.

User-facing Element List templates intentionally exclude internal/debug IFC columns such as raw IFC type, processing role, bounding box min/max, geometry owner IDs, and element tree visibility flags.

## Lifting Result Rules

Lifting reports are based on the latest optimization result. Capacity utilization is calculated from the already reduced available capacity stored in the optimization result:

```text
Capacity Utilization (%) = Required Capacity / Available Capacity * 100
```

The metadata shows:

```text
Capacity Factor: 0.90
```

This means available crane capacity is reported as 90% of chart capacity for user-facing report context. The reporting layer does not apply the factor a second time to optimization result rows.

## Buttons Still Stubbed

The following report buttons remain placeholders:

- All Cranes in Database
- Compare 2 Cranes
- Project Overview
- Site Zones Report

The shared **GA Diagnostics** and **View Results** buttons remain wired to the existing `CranePlacementViewModel` commands.
