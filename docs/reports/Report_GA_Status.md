# Report Builder / GA Data Mapping Status Report

## Git Summary

### What was attempted

The recent work attempted to fix the Report Builder data-source problem by:

* Adding/using `ReportBuildRequestDto.LiftingResults`.
* Resolving current in-memory GA rows from:

```csharp
CranePlacementViewModel.Result.Positions[].ElementResults
```

* Falling back to diagnostics CSV only when current GA result is missing.
* Adding `SourceType` to `LiftingResultSourceRowDto`.
* Adding debug counters before report generation.
* Adding/expanding `Report Data Source Audit`.
* Adding logic for:

  * Element Weight
  * Hook / Rigging Weight
  * Required Capacity
  * Raw Chart Capacity
  * Available Capacity
  * Capacity Utilization
  * Safety Margin
* Adding some tests around report data and audit behavior.

### Current result

The implementation is still incomplete.

The real UI report preview still does not show GA result data after running GA analysis.

The issue is not confirmed fixed because the tests appear to validate isolated DTO/request behavior, not the actual UI flow from GA analysis → Report Builder → Preview/PDF/Excel.

---

# Current Problem

After running GA analysis, report preview still shows missing or incorrect values such as:

* GA fields still show `—`.
* Lift Status / Radius / Boom Length / Capacity are missing.
* Crane metadata is incomplete or not clearly from the GA run.
* `Element Mark` may still show `—`.
* `Ready for GA` may still show `No`.
* `Missing Readiness Reason` may still say the element is not included.
* Hook / Required Capacity columns may exist in code but are not confirmed visible/exported in the real report.
* Report output does not prove it uses current in-memory `PlacementResult`.

---

# Important Finding

The likely issue is not only the GA bridge.

The likely issue is:

```text
Report template family / provider routing
```

Even if `ReportBuildRequestDto.LiftingResults` is populated, the report may still use the wrong provider.

For example:

```text
If ReportFamily == ElementList
→ UserFacingReportBuilder uses ElementReportDataProvider
→ request.LiftingResults is ignored
```

So if the user opens an Element List / GA Readiness template, GA data will not appear, even after the GA bridge is filled.

This means the current implementation may be building GA rows but not displaying them because the selected template is not routed to a GA-aware provider.

---

# Key Diagnosis

## 1. GA Readiness is not a post-GA report

`GA Readiness` appears to be a pre-GA template.

It is meant to show:

* IncludeInGa
* ReadyForGA
* MissingReadinessReason
* Weight status
* Element readiness

It is not necessarily meant to show:

* Lift Status
* Radius
* Boom Length
* Required Capacity
* Assigned GA Point

Therefore, using `GA Readiness` to verify post-GA results is wrong unless the template is redesigned as a post-GA report.

---

## 2. Lifting Result must be a real post-GA template

There must be a real post-GA template such as:

```text
Lifting Result
GA Result Report
GA Assignment Review
```

This template must have:

```text
ReportFamily = LiftingResult
```

And it must use:

```text
LiftingResultReportDataProvider
```

or a properly merged GA-aware provider.

---

## 3. ElementList templates must not silently ignore GA columns

If an ElementList template contains GA result columns, the system must not silently show `—`.

It must either:

1. Route through a merged provider:

```text
ElementWithGaReportDataProvider
```

or

2. Block the report with a clear message:

```text
GA result columns require a Lifting Result report template.
```

Silent failure is not acceptable.

---

# What Has Been Done

## 1. GA bridge attempt

The agent claims:

* `ReportBuilderViewModel` resolves current in-memory GA rows first.
* Diagnostics CSV is used only as fallback.
* `ReportBuildRequestDto.LiftingResults` is built before preview/export.
* Preview/PDF/Excel use the same request path.
* Debug line was added.
* `SourceType` was added to `LiftingResultSourceRowDto`.
* Audit template is GA-aware.

This is directionally correct, but not yet proven in the real UI.

---

## 2. Hook / Required Capacity logic

The intended definitions are correct:

```text
Element Weight = element's own weight
Hook / Rigging Weight = GASettings.AdditionalWt
Required Capacity = Element Weight + Hook / Rigging Weight
```

Required report columns should be:

* Element Weight (t)
* Hook / Rigging Weight (t)
* Required Capacity (t)
* Raw Chart Capacity (t)
* Available Capacity After Derating/Safety (t)
* Capacity Utilization (%)
* Safety Margin (t)

But these must be verified in the actual report template, Preview, PDF, and Excel.

---

## 3. Audit template

The new audit template is useful, but it is diagnostic only.

It answers:

```text
Where is this value supposed to come from?
Why may it show —?
```

It does not automatically fix the real report templates.

---

# What Is Still Not Solved

## 1. Real UI flow is not proven

The agent must prove the actual flow:

```text
Run GA
→ CranePlacementViewModel.Result has data
→ Open Report Builder
→ Select post-GA template
→ Build Preview
→ Preview rows show GA values
```

The current tests are not enough if they manually inject fake `LiftingResults`.

---

## 2. Missing runtime debug values

The agent must provide the actual runtime debug line from the real app, not just say it was added.

Required runtime debug values:

* SelectedTemplateName
* ReportFamily
* UsesGaData
* SelectedColumnKeys
* CurrentPlacementResultPresent
* CurrentPlacementResultPositionsCount
* CurrentPlacementResultElementRowsCount
* ReportBuildRequestDto.LiftingResultsCount
* DiagnosticsFallbackRowsCount

Expected after real GA run:

```text
CurrentPlacementResultPresent = true
CurrentPlacementResultPositionsCount > 0
CurrentPlacementResultElementRowsCount > 0
UsesGaData = true
ReportBuildRequestDto.LiftingResultsCount > 0
ReportFamily = LiftingResult for post-GA reports
```

If any of these are false/zero, the bridge or routing is still broken.

---

## 3. Template family routing is probably wrong

If the selected report is still:

```text
ReportFamily.ElementList
```

then GA rows may be ignored.

A post-GA template must be routed as:

```text
ReportFamily.LiftingResult
```

or through a merged provider.

---

## 4. GA metadata is still incomplete

The final report must show GA-run crane metadata, not the currently selected UI crane.

Required metadata after GA run:

* GA Run Status
* Crane Name
* Manufacturer
* Model / Specification
* Crane Type
* Max Capacity
* Counterweight
* Outrigger Configuration
* Vehicle Length
* Vehicle Width
* Vehicle Height
* Outrigger Length
* Outrigger Width
* Min Radius
* Max Radius
* Min Boom Length
* Max Boom Length
* Max Hook Height
* Derating Ratio
* Safety Factor
* Combined Capacity Factor
* Wire Length
* Hook / Rigging / Additional Weight
* Load Chart Operating Condition
* Selected Number of Lines
* Final GA Setup Count
* Final GA Setup Points X/Y/Z
* Assigned elements per setup

If `CraneModelOrSpecification` is still placeholder, the metadata implementation is incomplete.

---

# Required Final Target

At the end, the system must reach this behavior:

## 1. No IFC re-read during report

Reports must read from:

* Loaded `PrecastElement`
* Project/session entities
* Current in-memory `PlacementResult`
* Latest diagnostics CSV only as fallback

Reports must not parse IFC again.

---

## 2. Correct report families

### Pre-GA reports

Examples:

* Element List
* Rendered Elements
* GA Readiness

These show element/import/readiness data.

They may show:

* Element Mark
* Element Name
* Element Type
* Material
* Weight
* Volume
* Included in GA Input
* Ready for GA
* Missing Readiness Reason

They should not be used to verify post-GA results unless explicitly converted to a merged GA-aware template.

---

### Post-GA reports

Examples:

* Lifting Result
* GA Result Report
* GA Assignment Review

These must show:

* Lift Status
* Reach Status
* Chart Status
* Failure Reason
* Suggested Action
* Radius
* Boom Length
* Element Weight
* Hook / Rigging Weight
* Required Capacity
* Raw Chart Capacity
* Available Capacity After Derating/Safety
* Capacity Utilization %
* Safety Margin
* Assigned GA Point
* Final Owner Point
* Feasible Point IDs

These must use:

```text
PlacementResult.Positions[].ElementResults
```

first, then diagnostics CSV only if memory result is missing.

---

## 3. Correct source priority

GA report data source priority:

```text
1. Current in-memory PlacementResult
2. Latest GA diagnostics CSV
3. No GA run / —
```

Never:

```text
selected UI crane
raw IFC
fake zeros
```

---

## 4. Correct Element Mark fallback

Element Mark display rule:

```text
1. PrecastElement.ElementMark
2. PrecastElement.ElementName
3. —
```

Do not fallback to:

* Material
* Element Type
* Raw IFC Type

---

## 5. Correct Required Capacity

The report must clearly separate:

```text
Element Weight
Hook / Rigging Weight
Required Capacity
```

Formula:

```text
Required Capacity = Element Weight + Hook / Rigging Weight
```

Capacity calculations:

```text
Available Capacity = Raw Chart Capacity × Derating Ratio × Safety Factor
Capacity Utilization = Required Capacity / Available Capacity × 100
Safety Margin = Available Capacity - Required Capacity
```

---

# Required Next Agent Task

## Main instruction

Do not continue UI polish.

Fix and prove the real report routing.

## Required investigation

Run the real app flow and capture the actual runtime debug line:

```text
SelectedTemplateName
ReportFamily
UsesGaData
SelectedColumnKeys
CurrentPlacementResultPresent
CurrentPlacementResultPositionsCount
CurrentPlacementResultElementRowsCount
ReportBuildRequestDto.LiftingResultsCount
DiagnosticsFallbackRowsCount
```

Do not claim completion without showing these actual values.

---

## Required fix

### Option A — Preferred

Create or activate a true post-GA report template:

```text
GA Result Report
```

Rules:

```text
ReportFamily = LiftingResult
Provider = LiftingResultReportDataProvider
```

This template must appear in Report Builder and export through Preview/PDF/Excel.

---

### Option B — If GA fields must appear inside ElementList templates

Create:

```text
ElementWithGaReportDataProvider
```

This provider must:

* Build rows from `PrecastElement`
* Join GA result rows by `ElementID`
* Add GA result values into the same row
* Work in Preview/PDF/Excel
* Not silently ignore `LiftingResults`

---

# Required Tests

## Test 1 — Real ViewModel flow

* Set `CranePlacementViewModel.Result` with a real `PlacementResult`.
* Select the actual report template used in UI.
* Build Preview through `ReportBuilderViewModel`.
* Assert:

```text
UsesGaData = true
ReportBuildRequestDto.LiftingResultsCount > 0
Preview contains LiftStatus, Radius, BoomLength, RequiredCapacity
```

---

## Test 2 — Wrong family protection

If an `ElementList` template contains GA columns:

Expected:

```text
Either route through merged provider
or fail with a clear message
```

Not allowed:

```text
silent —
```

---

## Test 3 — LiftingResult template

Given current in-memory `PlacementResult`, selected `ReportFamily.LiftingResult` must show:

* LiftStatus
* Radius
* BoomLength
* RequiredCapacity
* RawChartCapacity
* AvailableCapacity
* AssignedGaPoint

---

## Test 4 — Template catalog

Verify:

* Post-GA template appears in Report Builder list.
* Selecting it sets `ReportFamily.LiftingResult`.

---

## Test 5 — Preview/PDF/Excel parity

The same resolved rows must be used for:

* Preview
* PDF
* Excel

---

# Acceptance Criteria

The work is complete only when:

* Real report preview shows GA values after real GA analysis.
* `ReportFamily.LiftingResult` template exists and is selectable.
* `LiftingResultsCount > 0` after GA run.
* Preview/PDF/Excel show the same GA result values.
* GA fields no longer show `—` when GA data exists.
* GA metadata page uses GA-run crane/settings.
* Hook / Rigging Weight appears separately.
* Required Capacity includes Hook / Rigging Weight.
* Raw Chart Capacity and Available Capacity are separate.
* No IFC file is re-read during report generation.
* No selected UI crane is used as GA metadata after GA run.
* No GA algorithm/load chart logic is changed.
* All tests pass.
* Full solution builds with zero errors.
