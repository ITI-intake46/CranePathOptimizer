# PDF Load Chart Import

This document describes the PDF load-chart import workflow and the current extraction rules used by
`CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs`.

The PDF importer is a preview/review workflow. It helps the user extract manufacturer load-chart
tables from PDF pages before saving them through the existing crane load-chart import/catalog flow.
The user must still review the preview before saving.

## Architectural boundaries

The importer must preserve the project layering rules:

- PDF parsing and preview-table reconstruction live in the UI feature service because the input is a
  user-selected PDF file and the output is a preview/import DTO for the WPF workflow.
- Business optimization logic remains outside WPF views and code-behind.
- Domain, GA, database, and persistence behavior must not be changed for PDF-layout cleanup work.
- Saved exact-visual charts still flow through the existing DTO/import services and capacity lookup
  behavior.

## Downstream GA operating setup selection

The PDF importer preserves imported load-chart operating metadata, including `NumberOfLines`,
`OperatingConditionLabel`, `VisualColumnIndex`, and `VisualColumnHeader`, but it must not decide the
crane reeving or operating setup for the optimizer.

The user-facing runtime choice belongs in the F8 **Crane Position Optimizer** dialog under
**Load Chart Operating Setup**. The default **Any available line count / condition** option keeps the
previous optimizer behavior by leaving `GASettings.LoadChartNumberOfLinesConstraint` and
`GASettings.LoadChartOperatingConditionConstraint` unset. When the user chooses a specific line count
or operating condition, the dialog forwards that choice through `CraneOptimizerDialogWorkflow` into
`GASettings`, and the existing `LoadChartCapacityResolver` applies the constraint during capacity
lookup.

### Exact-PDF visual provenance versus runtime setup choices

Exact PDF imports may create `OperatingConditionLabel` values such as `39.1 / 4 lines` when a
manufacturer chart repeats the same numeric boom header across multiple visual columns. Those labels
are audit/provenance metadata: they identify the visual PDF column that produced a capacity cell.
They are not standalone crane operating-condition choices.

The F8 **Load Chart Operating Setup** selector therefore exposes imported `NumberOfLines` values as
line-count setup constraints, but suppresses operating-condition choices that consist only of a
numeric visual boom header plus a line-count suffix. Real semantic operating-condition labels remain
available, including labels that contain numbers, such as `On outriggers 360 degrees` or
`7.6m x 7.6m outriggers`.

### Maximum line-count eligibility semantics

The F8 **Load Chart Operating Setup** line-count selector is interpreted as a maximum available
reeving/setup constraint. Selecting `4 lines` does not mean "use only cells labelled exactly
4 lines"; it means "the optimizer may use imported chart cells requiring up to 4 lines." Therefore
a `4 lines` selection keeps `2`, `3`, and `4` line chart cells eligible, while excluding cells that
require more than 4 lines.

This keeps the GA search space from being unnecessarily starved while still preserving the safety
model: the resolver never calculates or inflates capacity from line count. It only chooses among
manufacturer/imported load-chart cells whose original capacities remain unchanged.

### Usable capacity coverage counts and scrollable details

The F8 **Load Chart Operating Setup** coverage text reports usable positive imported capacity
values, not every preserved exact-PDF grid cell. Exact visual imports intentionally preserve blank
or zero-capacity grid positions for audit/provenance, but those preserved cells are not counted as
usable capacity values in the selector description.

The automatic `Any` option reports the total usable imported capacity values and usable visual
columns for the selected crane. Constrained line-count options report the usable subset requiring up
to the selected line count. These display counts are diagnostic only. They do not recalculate,
inflate, or certify crane capacity; the resolver still performs final liftability checks against the
imported manufacturer chart values and the selected setup eligibility constraint.

The expanded operating-setup details are scrollable so explanatory text, intuition cards, and safety
notes remain reachable without pushing the Run/Cancel/Apply controls out of view.
## Table detection overview

The importer reads positioned PDF words and reconstructs candidate load-chart tables from geometry.
PDF files normally do not contain semantic table cells, so the importer must infer rows, columns,
headers, and body values from word bounding boxes and X/Y positions.

The row-stream extractor:

1. Locates likely numeric boom-header rows.
2. Locates radius/body rows underneath the header.
3. Builds capacity rows from PDF word positions.
4. Scores candidate tables by row count, column count, density, coverage, radius ordering, capacity
   trends, and nearby load-chart cues.
5. Emits a preview grid and warnings for user review.

## Physical lane reconstruction

Some manufacturer PDFs draw one numeric boom-length header over multiple physical capacity columns.
A common example is a first boom length with a local `*` sub-column and a second unmarked visual
sub-column under the same numeric boom length.

The importer must not assume that numeric header count always equals physical capacity-lane count.
When body-value X clusters prove that the table contains more physical capacity lanes than numeric
boom headers, the importer reconstructs the physical lanes from body-cell X positions.

For example, a PDF can visually mean:

```text
Radius (m), 13.1 *, 13.1, 17.6, 22.1, ...
```

even if the numeric header row contains only:

```text
13.1, 17.6, 22.1, ...
```

The preview preserves those reconstructed exact visual columns rather than shifting body values into
the wrong boom-length columns.

## Split and spanning header handling

When a header visually spans multiple body lanes, the importer duplicates the numeric boom header in
the exact visual grid. The duplicated columns are intentional and represent distinct visual columns
from the manufacturer table.

For the verified split-header case, the first two visual columns both parse numerically as `13.1`,
but their visual labels may differ depending on local PDF annotations.

Correct behavior:

```text
13.1 *, 13.1, 17.6, 22.1, ...
```

Incorrect behavior:

```text
13.1 *, 13.1 360Â°, 17.6, 22.1, ...
```

unless `360Â°` is actually printed as a local subheader marker inside the second visual lane.

## Local annotation labels

Semantic duplicate-header labels must be faithful to the actual PDF table annotation.

A visual column label may use a marker such as `*`, `360Â°`, `rear`, `front`, or `side` only when
that marker appears inside the local table header/subheader band above the body rows and inside that
column's exclusive horizontal lane region.

Global page icons, chart configuration symbols, legend text, or operation-mode markers above/outside
the local table header band must not be assigned to individual columns.

Examples:

| PDF evidence | Preview labels |
|---|---|
| First duplicate lane has a local `*`; second duplicate lane has no local marker | `13.1 *`, `13.1` |
| First duplicate lane has local `*`; second duplicate lane has local `360Â°` | `13.1 *`, `13.1 360Â°` |
| Duplicate lanes have no local marker evidence | `13.1`, `13.1` |
| A `360Â°` icon appears globally above the table, not in the local subheader band | do not attach `360Â°` to a lane |

Each local marker is assigned to at most one visual lane. A single `*` marker must not label multiple
duplicate columns.


## Annotated visual boom-header parsing

Exact PDF visual import preserves the manufacturer-facing column label for review and audit, but
parses the numeric boom length from a single leading number when the remaining text is a local
operational annotation.

Examples that resolve to numeric boom length `13.1` while preserving their visual labels include:

```text
13.1 *
13.1*
13,1 *
13.1 m *
13.1 360°
13.1 over rear
13.1 rear
```

This rule is intentionally generic. It is not tied to a specific PDF, page, or manufacturer table.
It accepts a single leading boom length followed by a local annotation marker or text, while still
rejecting ambiguous headers that contain another non-360 numeric boom-like value, such as
`13.1 17.6`, `13.1 / 17.6`, or `13.1-17.6`.

The original visual header remains stored in `VisualColumnHeader`; only the internal numeric
`BoomLength` uses the parsed leading number. Duplicate exact-visual columns continue to use the
conservative minimum capacity policy during optimization lookup.

## Exact visual import and conservative capacity behavior

The exact visual grid can contain repeated numeric boom lengths. This is expected for split or
spanning-header tables.

Numeric parsing still extracts the boom length from each visual header, so labels such as `13.1 *`
and `13.1 360Â°` both resolve numerically to boom length `13.1`.

Optimization capacity lookup remains conservative when duplicate same-radius/same-boom visual
columns exist. The resolver uses the minimum positive capacity among duplicates, preserving the
project's safety-first load-chart behavior.

## Expected review behavior

The PDF preview should be checked against the actual manufacturer page before saving.

Review especially:

- radius rows,
- capacity values,
- split/spanning headers,
- duplicate numeric boom headers,
- local annotations such as `*`,
- warnings shown by the importer.

If the preview does not match the source PDF table, do not save it as a crane load chart.