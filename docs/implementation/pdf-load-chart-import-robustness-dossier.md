# CranePathOptimizer Branch Implementation Dossier
## `tmp/div-next-implementation` — PDF Load Chart Import Robustness, Continuation Merging, Reading-Order Reconstruction, and Source Preview Toggle

**Repository:** `CranePathOptimizer`  
**Working branch:** `tmp/div-next-implementation`  
**Tracked base:** `origin/div`  
**Document type:** Engineering-grade branch implementation dossier / technical change report / maintenance and validation record  
**Document generated:** 2026-06-11  
**Primary subsystem:** `CraneOptimizer.UI/Features/F4_LoadChart`  
**Primary implementation theme:** robust PDF load-chart import review and selection workflow for multi-table manufacturer load-chart PDFs  
**Recommended repository placement:** `docs/implementation/pdf-load-chart-import-robustness-dossier.md`  

---

## Table of Contents

1. [Document Control and Recommendation](#1-document-control-and-recommendation)
2. [Evidence Basis and Review Methodology](#2-evidence-basis-and-review-methodology)
3. [Executive Summary](#3-executive-summary)
4. [Problem Space and Engineering Motivation](#4-problem-space-and-engineering-motivation)
5. [Scope, Non-Scope, and Branch Boundaries](#5-scope-non-scope-and-branch-boundaries)
6. [Architecture Before and After This Branch](#6-architecture-before-and-after-this-branch)
7. [Feature Area 1 — Visual Reading-Order Reconstruction](#7-feature-area-1--visual-reading-order-reconstruction)
8. [Feature Area 2 — Verified Combined Continuation Tables](#8-feature-area-2--verified-combined-continuation-tables)
9. [Feature Area 3 — Professional Source PDF Preview Toggle](#9-feature-area-3--professional-source-pdf-preview-toggle)
10. [File-by-File Implementation Ledger](#10-file-by-file-implementation-ledger)
11. [Subsystem-Level Impact Assessment](#11-subsystem-level-impact-assessment)
12. [Algorithmic and UI Design Decisions](#12-algorithmic-and-ui-design-decisions)
13. [Validation, Diagnostics, and Reproducibility Record](#13-validation-diagnostics-and-reproducibility-record)
14. [Backward Compatibility and User Workflow Preservation](#14-backward-compatibility-and-user-workflow-preservation)
15. [Performance, Reliability, and Robustness Considerations](#15-performance-reliability-and-robustness-considerations)
16. [Known Limitations, Risks, and Follow-Up Items](#16-known-limitations-risks-and-follow-up-items)
17. [Reviewer Guide](#17-reviewer-guide)
18. [Commit and Integration Guidance](#18-commit-and-integration-guidance)
19. [Appendix A — Changed Files by Layer](#appendix-a--changed-files-by-layer)
20. [Appendix B — Validation Artifact Index](#appendix-b--validation-artifact-index)
21. [Appendix C — Terminology](#appendix-c--terminology)
22. [Appendix D — External Documentation and Engineering Standards Consulted](#appendix-d--external-documentation-and-engineering-standards-consulted)
23. [Appendix E — Suggested Manual Smoke-Test Protocol](#appendix-e--suggested-manual-smoke-test-protocol)

---

## 1. Document Control and Recommendation

### 1.1 Recommendation: Create a New Branch Dossier, Do Not Overwrite the Older Markdown File

An older Markdown implementation dossier was supplied as a reference document. That file documents a different branch and a different implementation theme: load-chart provenance preservation through Genetic Algorithm diagnostics. It is professionally structured and useful as a formatting benchmark, but it should **not** be overwritten for this branch.

This branch has a distinct implementation scope:

```text
Current branch: tmp/div-next-implementation
Primary theme: PDF Load Chart Import robustness and user review workflow
Main changed subsystem: WPF PDF Load Chart Import dialog + PDF table candidate extraction
Primary files modified: 4 tracked source/test files
```

The older dossier should remain a historical record for its original branch. The correct engineering action is to create a **new standalone dossier** dedicated to the current branch. This preserves auditability and prevents two unrelated branch histories from being merged into one ambiguous document.

### 1.2 Intended Audience

This document is written for:

- senior engineers reviewing architectural fit;
- maintainers responsible for the PDF load-chart importer;
- researchers who need reproducible import behavior and validation evidence;
- QA engineers validating manufacturer PDF behavior;
- future contributors modifying table extraction, preview UI, or load-chart import;
- project leads deciding what to merge, commit, or extend later.

### 1.3 Document Purpose

This is not a lightweight changelog. It is a branch-level implementation dossier that explains:

- what changed;
- why it changed;
- where it changed;
- how it changed;
- how it was validated;
- which systems were and were not affected;
- what future maintainers must preserve.

The document is intended to be committed alongside the branch so that a future reviewer can reconstruct the rationale and technical behavior without relying on chat history, screenshots, or local PowerShell logs.

### 1.4 Branch State Captured

The final validated branch state recorded by the latest patch log is:

```text
Branch: tmp/div-next-implementation
Tracking: origin/div

Modified tracked files:
  M CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
  M CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
  M CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml
  M CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs

Untracked file:
  ?? docs.zip

Final diff stat:
  4 files changed, 1130 insertions(+), 152 deletions(-)
```

`docs.zip` was present as an untracked file throughout the later validation stages and should not be staged or committed unless intentionally needed for repository documentation.

---

## 2. Evidence Basis and Review Methodology

### 2.1 Evidence Sources

This dossier is based on the following project-local evidence collected during the branch work:

| Evidence artifact | Purpose |
|---|---|
| Complete forensic package | Baseline project/source/build/test context before implementation. |
| PDF ordering diagnostics V4 | Confirmed score-order versus visual-reading-order mismatch on real PDF pages. |
| Continuation diagnostics V2 | Scanned real crane PDFs and classified safe/unsafe continuation-pair candidates. |
| Continuation patch context package | Captured current source and diff state before combined-table patching. |
| Continuation patch V2 validation log | Verified combined continuation implementation, targeted tests, full tests, and Git state. |
| Preview toggle context package | Captured exact WPF dialog layout/code-behind context before UI enhancement. |
| Preview toggle patch V2 validation log | Verified source-preview toggle installation and final branch validation. |
| Older Markdown implementation dossier | Used only as a structural/professional documentation benchmark, not as content to update. |

### 2.2 External Documentation and Engineering Standards Consulted

The structure of this document follows practices used in professional engineering branch documentation:

- ADR-style recording of context, decisions, consequences, and tradeoffs;
- human-readable changelog principles for grouping meaningful changes;
- reproducibility documentation practices for rerunnable validation;
- software architecture documentation practice emphasizing maintainability and future onboarding;
- WPF control/layout documentation for the source-preview toggle implementation;
- PDF layout-analysis guidance emphasizing geometric layout and reading order rather than raw extraction order.

External references are listed in [Appendix D](#appendix-d--external-documentation-and-engineering-standards-consulted).

### 2.3 Documentation Methodology

The dossier is intentionally structured around three axes:

1. **Feature-level explanation** — describes the user-visible and technical behavior.
2. **Subsystem-level impact** — explains how the change interacts with PDF import, UI, tests, GA, diagnostics, dependencies, installers, and runtime behavior.
3. **File-level ledger** — records exact files, responsibilities, and maintenance implications.

This is designed to support both high-level review and source-level maintenance.

### 2.4 Confidence and Limitations of Evidence

The branch was locally validated through restore, build, targeted tests, and full application tests. Manual UI smoke testing is still recommended because automated unit tests do not exercise every visual layout scenario in WPF.

The dossier avoids claiming that unexecuted runtime UI workflows were manually verified unless explicitly supported by the validation evidence. It documents the intended behavior and the compile/test validation that confirms the implementation is syntactically and logically integrated.

---

## 3. Executive Summary

### 3.1 Branch Outcome

This branch substantially improves the PDF Load Chart Import workflow in three tightly related ways:

1. **Tables are numbered and displayed in visual reading order.**
   - Physical left table becomes `Table 1`.
   - Physical right table becomes `Table 2`.
   - Best-table selection remains score-based for automatic import logic.

2. **Verified continuation tables are offered as additional selectable candidates.**
   - Original individual tables remain selectable.
   - A combined table is added only when two side-by-side table segments are strongly verified as one logical continuing load chart.
   - Ambiguous or weak continuation cases are rejected rather than merged automatically.

3. **The Source PDF Preview pane can be shown or hidden professionally.**
   - Preview remains visible by default.
   - A compact `Show/Hide PDF Preview` toggle is available from the Load Chart Preview header.
   - Hiding the source preview collapses the preview pane and splitter, allowing the parsed grid to expand.
   - Showing the preview restores the pane and re-renders the selected table preview when required.

### 3.2 Why This Matters

Manufacturer crane load-chart PDFs often use visually complex layouts:

- two side-by-side table panels on the same page;
- continuation segments where the right table continues the left table’s boom-length range;
- repeated boundary boom values such as `34.8 -> 34.8`;
- table extraction scores that do not match natural reading order;
- dense visual previews that may consume workspace when the user wants to focus on the parsed grid.

Before this branch, the importer could extract tables but displayed side-by-side tables in score order. On pages such as `130Tons.pdf`, page 14, that caused the right physical table to appear as `Table 1` even though it is visually the second table. The importer also did not provide a combined table option for verified continuation cases. Users had to manually choose either side of a logically continuous chart. Finally, the source preview was always visible, reducing working space when users wanted a larger parsed table view.

After this branch, the importer is more faithful to how engineers read manufacturer load-chart pages: left-to-right visual order, optional combined continuation where verified, and a professional preview workspace toggle.

### 3.3 Changed Files Summary

| File | Change category | Summary |
|---|---|---|
| `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs` | PDF extraction/service logic | Added visual reading-order reindexing and verified continuation candidate generation. |
| `CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs` | Regression tests | Added tests for reading order and continuation candidate generation/rejection. |
| `CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml` | WPF UI layout | Added preview toggle control, named preview columns, splitter/pane hooks, and toggle styling. |
| `CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs` | WPF code-behind | Added show/hide source-preview state, restore logic, render-state tracking, and guarded zoom/find behavior. |

### 3.4 Validation Summary

Final validation completed successfully after the preview toggle V2 validation run:

```text
dotnet restore: exit code 0
dotnet build: succeeded
dotnet test CraneOptimizer.Application.Tests: passed

Full application tests:
  Failed: 0
  Passed: 369
  Skipped: 1
  Total: 370
```

The earlier combined-table patch V2 also validated the PDF continuation work:

```text
Targeted PDF load-chart continuation tests:
  Failed: 0
  Passed: 13
  Skipped: 0
  Total: 13

Full application tests:
  Failed: 0
  Passed: 369
  Skipped: 1
  Total: 370
```

### 3.5 High-Level Engineering Judgment

The implementation is architecturally appropriate for the existing WPF/.NET codebase:

- PDF extraction and candidate-generation logic remains in `LoadChartPdfService`.
- UI layout behavior remains in `PdfLoadChartImportDialog.xaml` and `.xaml.cs`.
- Regression tests remain in the application test project.
- GA algorithms, persistence, database migrations, installers, and unrelated UI features are not directly modified.

The branch improves robustness while preserving the existing user workflow and avoiding broad architectural rewrites.

---

## 4. Problem Space and Engineering Motivation

### 4.1 Original PDF Table Ordering Problem

The PDF importer scored extracted table candidates by quality. This is useful for best-table selection but insufficient for user-facing table numbering. Score order is not the same as visual reading order.

Observed issue on a real crane PDF:

```text
PDF: 130Tons.pdf
Focus page: 14
Physical layout: two side-by-side tables

Left physical table:
  Boom range: approximately 13.3 -> 34.8
  Expected visible index: Table 1

Right physical table:
  Boom range: approximately 34.8 -> 60
  Expected visible index: Table 2
```

Before the reading-order patch, the right table could be displayed as `Table 1` because it had the higher extraction score.

This was misleading for users because visual table labels should follow how the engineer reads the page: top-to-bottom, then left-to-right within each visual row.

### 4.2 Original Continuation Table Problem

Many manufacturer load-chart pages split one logical chart into multiple visual panels. In such cases, the left table may end with a boom header and the right table may begin with the same boundary boom header.

Example:

```text
Left table headers:  13.3, 17.6, 21.9, 26.2, 30.5, 34.8
Right table headers: 34.8, 39.1, 43.4, 47.8, 52.1, 56.4, 60.0
Shared boundary:     34.8
```

The two tables are individually valid, but users may need an importable combined chart that represents the full logical load chart.

The design requirement was conservative:

```text
Do not merge all double-table pages.
Do not hardcode a page number or PDF file.
Do not remove individual table options.
Add a combined candidate only when the importer can verify continuation strongly.
```

### 4.3 Original Source Preview Layout Enhancement

The PDF Load Chart Import dialog already had a useful source preview pane. However, the preview pane always consumed space. When the user needed to inspect a large parsed grid or combined table, the ability to collapse the source preview became valuable.

The requirement was not simply to add a rough close button. The requested enhancement was a professional, top-software-style view option:

- visible by default;
- reversible;
- non-destructive;
- layout-aware;
- accessible through a control that remains available when the preview is hidden;
- preserving table selection and import state.

---

## 5. Scope, Non-Scope, and Branch Boundaries

### 5.1 In Scope

The branch intentionally changes:

- PDF table candidate ordering for user-facing table labels;
- PDF table candidate generation to add verified combined continuation candidates;
- regression tests covering reading order and continuation behavior;
- PDF Load Chart Import dialog layout and source-preview visibility behavior;
- validation scripts and logs used during implementation.

### 5.2 Out of Scope

The branch intentionally does **not** change:

| Area | Status |
|---|---|
| Genetic Algorithm optimization operators | No direct change. |
| GA assignment, placement, scoring, mutation, or selection algorithms | No direct change. |
| Database schema/migrations | No direct change. |
| Runtime persistence models | No direct change. |
| Installers/deployment packaging | No direct change. |
| NuGet dependencies | No direct change. |
| Streamlit UI | No Streamlit subsystem detected in the affected project evidence. |
| IFC/DXF/site-layout subsystems | No direct change. |
| Existing CSV import logic | No direct change. |
| Load-chart save/catalog persistence | No direct change in this branch segment. |

### 5.3 Boundary Between Current Branch and Older Uploaded Dossier

The uploaded older Markdown document describes a separate branch focused on preserving load-chart provenance through GA diagnostics. This current branch focuses on PDF import robustness and PDF import dialog usability. The two efforts are related by feature area but not identical.

Therefore, this document is intentionally new. It should be committed as a new branch implementation dossier rather than merged into the older provenance-diagnostics document.

### 5.4 Untracked Artifacts Not Intended for Commit

During validation, `docs.zip` remained untracked. Unless there is a separate reason to commit it, the recommended commit should exclude it.

Suggested staging command:

```bash
git add \
  CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs \
  CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs \
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml \
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs \
  docs/implementation/pdf-load-chart-import-robustness-dossier.md
```

---

## 6. Architecture Before and After This Branch

### 6.1 Relevant Existing Architecture

The project follows a layered structure. The relevant path for this branch is:

```text
CraneOptimizer.UI
  Features/F4_LoadChart
    Services
      LoadChartPdfService.cs
    Views
      PdfLoadChartImportDialog.xaml
      PdfLoadChartImportDialog.xaml.cs

CraneOptimizer.Application.Tests
  LoadChart
    PdfLoadChartMultiTableSegmentationTests.cs
```

The affected workflow is:

```text
User selects PDF
  -> LoadChartImportService invokes PDF scanning/import flow
  -> LoadChartPdfService scans pages and builds table candidates
  -> PdfLoadChartImportDialog displays pages/tables/preview
  -> User selects one table candidate
  -> ImportSelectedTable parses selected candidate
  -> Imported chart flows into existing load-chart save/import workflow
```

### 6.2 Before This Branch

Before this branch:

- table candidates were ordered primarily by extraction score;
- side-by-side tables could be numbered in non-visual order;
- no combined candidate was generated for verified continuation tables;
- the source PDF preview pane was always visible;
- the parsed grid could not expand into the source preview space;
- PDF preview render logic did not need to track hidden/visible state.

### 6.3 After This Branch

After this branch:

- extracted candidates are reindexed by visual reading order;
- score-based best-table selection remains available for automatic best-table logic;
- side-by-side continuation pairs can produce an additional combined candidate;
- individual source candidates remain selectable;
- combined candidates carry a clear display label: `Combined continuation`;
- ambiguous continuation cases are rejected;
- the PDF preview can be hidden and shown without destroying the import workflow;
- the parsed grid receives more screen space when the source preview is hidden.

### 6.4 Responsibility Boundaries Preserved

The branch preserves the existing architectural boundaries:

| Responsibility | Location |
|---|---|
| Table extraction, candidate ordering, continuation candidate creation | `LoadChartPdfService.cs` |
| Visual UI layout and preview toggle | `PdfLoadChartImportDialog.xaml` |
| UI preview state and rendering behavior | `PdfLoadChartImportDialog.xaml.cs` |
| Regression tests for extraction behavior | `PdfLoadChartMultiTableSegmentationTests.cs` |

No optimizer logic was moved into the WPF views. No WPF layout concerns were moved into the PDF extraction service.

---

## 7. Feature Area 1 — Visual Reading-Order Reconstruction

### 7.1 Problem Solved

PDF table extraction score is not a reliable substitute for visual reading order. A higher-scoring right-hand table can be displayed before a lower-scoring left-hand table, creating a mismatch between visual page structure and UI table labels.

### 7.2 Technical Approach

The branch adds a reading-order reindexing path for table candidates. The implementation groups table candidates into visual rows and orders them:

```text
1. Top-to-bottom by visual row.
2. Left-to-right within each visual row.
3. Score and original index only as tie breakers.
```

The reading-order logic uses candidate geometry:

- `Left`
- `Right`
- `Top`
- `Bottom`
- candidate height
- candidate center Y
- row overlap and row center tolerance

### 7.3 Key Implementation Concepts

The relevant helper methods include:

```text
ReindexCandidatesInReadingOrder(...)
IsSameVisualReadingRow(...)
GetCandidateCenterY(...)
GetCandidateHeight(...)
GetCandidateUpperY(...)
GetCandidateLowerY(...)
GetRowUpperY(...)
GetRowLowerY(...)
GetRowLeftX(...)
OrderCandidatesInReadingOrderForTesting(...)
```

The test-access helper exists so that reading-order behavior can be validated without relying on full PDF parsing for every test case.

### 7.4 Why Geometry-Based Ordering Was Chosen

PDF content streams do not guarantee human reading order. Geometry-based ordering is more robust for visual table extraction because it uses how the table appears on the page rather than the order in which text objects happen to be stored.

This is especially important for side-by-side charts, multi-column PDFs, and manufacturer layouts where the extracted word order may differ from the visible table order.

### 7.5 Regression Coverage

Tests added for this feature include:

```text
TableReadingOrder_ReindexesSideBySideTablesByLeftPositionEvenWhenRightTableScoresHigher
TableReadingOrder_SortsVisualRowsTopToBottomThenLeftToRight
TableReadingOrder_KeepsScoreBasedBestTableSelectionSeparateFromVisibleNumbering
```

These tests verify that:

- the left physical table becomes visible `Table 1` even if the right table has a higher score;
- multiple visual rows are ordered top-to-bottom;
- score-based `GetBestTable()` behavior remains separate from visible numbering.

### 7.6 Downstream Impact

| Downstream area | Impact |
|---|---|
| PDF import dialog | Table labels now align with visual order. |
| User selection workflow | Users can select `Table 1`, `Table 2`, etc. based on physical page layout. |
| Automatic best-table import | Preserved as score-based through `GetBestTable()`. |
| Tests | New geometry-order regression coverage. |

---

## 8. Feature Area 2 — Verified Combined Continuation Tables

### 8.1 Problem Solved

Some load-chart pages contain multiple table panels that are separate visually but continuous logically. Users need the option to import the full logical chart while retaining the ability to import either individual segment.

### 8.2 Core User-Facing Behavior

For a verified side-by-side continuation page, the dialog now presents:

```text
Table 1 - original left physical table
Table 2 - original right physical table
Table 3 - Combined continuation - combined logical chart
```

The exact index can vary depending on how many base candidates are present, but the design principle is stable:

```text
Original candidates remain available.
Combined candidate is appended after verified source candidates.
```

### 8.3 Verification Rules for Combining

A combined continuation candidate is generated only if the candidate pair satisfies strong compatibility checks.

The rules include:

| Rule | Rationale |
|---|---|
| Same PDF page | Continuation is a local page-layout relationship. |
| Not already a combined candidate | Prevent recursive/compound combined candidates. |
| Left-to-right physical order | Avoid reversed or overlapping candidate merges. |
| Same visual row / side-by-side layout | Avoid combining stacked or unrelated tables. |
| Acceptable horizontal gap | Avoid merging far-separated unrelated panels. |
| Compatible envelope/non-envelope kind | Avoid merging structurally different candidate types. |
| Non-decreasing headers within each table | Load-chart boom headers should be ordered. |
| Shared/touching boundary | Strong evidence of continuation, e.g. `34.8 -> 34.8`. |
| Right table extends boom range | Combined table must expand coverage. |
| No broad non-boundary overlap | Avoid false merge where tables overlap substantially. |
| Compatible radius axis | Tables should share enough radius rows to be one chart. |
| Combined grid validates | Candidate must be importable and dense enough. |

### 8.4 Conservative Design Decision

The implementation deliberately rejects small forward gaps without a shared boundary. For example:

```text
Left ends: 48.7
Right starts: 51.9
```

This may be a plausible continuation in some PDFs, but the diagnostics classified such cases as `POSSIBLE_REVIEW`, not automatically safe. The first production-quality implementation therefore requires a shared/touching boundary.

This is a conservative choice that prioritizes correctness over aggressive merging.

### 8.5 Raw Grid Construction

The combined table raw grid is built by:

1. Taking the left table header row.
2. Appending the right table header row.
3. Building a unified radius row dictionary.
4. Copying left capacity cells into the left column span.
5. Copying right capacity cells into the right column span.
6. Preserving metadata rows where present.
7. Sorting numeric radius rows by radius.
8. Building a conservative import grid from the combined visual grid.

This preserves exact visual columns while providing an import grid that can be safely parsed by existing load-chart parsing behavior.

### 8.6 Key Implementation Concepts

The relevant service helpers include:

```text
AppendVerifiedContinuationCandidates(...)
TryCreateVerifiedContinuationCandidate(...)
IsVerifiedContinuationPair(...)
AreSideBySideContinuationCandidates(...)
HaveCompatibleContinuationHeaders(...)
HaveCompatibleContinuationRadiusAxis(...)
BuildCombinedContinuationRawGrid(...)
CopyContinuationRows(...)
CopyContinuationCells(...)
CountNumericRadiusRows(...)
CountPositiveCapacityCells(...)
AppendVerifiedContinuationCandidatesForTesting(...)
```

The candidate DTO also gains combined-candidate presentation behavior through warning-based classification:

```text
COMBINED_CONTINUATION_TABLE
CandidateKindLabel: Combined continuation
DisplayTitle: Table {n} - Combined continuation - {columns} x {rows}
```

### 8.7 Regression Coverage

Continuation tests added include:

```text
TableContinuation_AddsCombinedCandidateForVerifiedSharedBoundaryTables
TableContinuation_DoesNotCombineBroadOverlappingBoomRanges
TableContinuation_DoesNotCombineUnrelatedSeparatedBoomRanges
TableContinuation_DoesNotCombineSmallForwardGapWithoutSharedBoundary
TableContinuation_DoesNotCombineDifferentRadiusAxes
```

These tests cover both positive and negative cases, which is essential because false-positive table merging would be more dangerous than failing to merge an ambiguous case.

### 8.8 Downstream Impact

| Downstream area | Impact |
|---|---|
| PDF import dialog | Users receive an additional combined candidate when verified. |
| Load-chart parsing | Uses existing raw-grid/import-grid parsing paths. |
| Optimizer | Indirect impact only: if the user imports a combined table, downstream optimizer capacity lookups may have a broader chart. The GA algorithm itself is unchanged. |
| Tests | Expanded PDF load-chart segmentation test coverage. |
| User workflow | Users retain original candidates and gain a verified combined candidate option. |

---

## 9. Feature Area 3 — Professional Source PDF Preview Toggle

### 9.1 Problem Solved

The source PDF preview is valuable for auditability, but it consumes space. Users inspecting wide parsed grids, especially combined continuation candidates, may need to hide the PDF preview temporarily.

The enhancement adds a professional reversible view toggle.

### 9.2 UI Behavior

Default state:

```text
Source PDF Preview visible.
Existing behavior preserved.
```

When hidden:

```text
Source PDF Preview pane collapses.
GridSplitter collapses.
Source preview column width becomes 0.
Parsed Load Chart Preview expands into freed space.
Current selected table/import state remains intact.
```

When shown again:

```text
Source PDF Preview pane is restored.
GridSplitter is restored.
Previous preview column width is restored where feasible.
Current selected table preview is rendered if needed.
Fit Page / Find Table / zoom controls continue working.
```

### 9.3 Control Placement Decision

The toggle is placed in the Load Chart Preview header rather than only inside the Source PDF Preview pane.

This is important because if the toggle existed only inside the pane, hiding the pane would hide the control needed to show it again.

### 9.4 WPF Design Pattern

The implementation uses a `ToggleButton`-style interaction model:

```text
Checked / visible: Hide PDF Preview
Unchecked / hidden: Show PDF Preview
```

The layout uses `Visibility.Collapsed` and column-width adjustment so that hidden preview space is actually freed rather than merely visually hidden.

### 9.5 Key Implementation Concepts

XAML additions include:

```text
PreviewToggleButtonStyle
BtnToggleSourcePreview
SourcePreviewSplitterColumn
SourcePreviewColumn
SourcePreviewSplitter
```

Code-behind additions include:

```text
_isSourcePreviewVisible
_sourcePreviewColumnWidth
_renderedSourcePreviewTable
InitializeSourcePreviewToggleState(...)
SourcePreviewVisibilityToggle_Click(...)
SetSourcePreviewVisibility(...)
IsCurrentSourcePreviewRenderedFor(...)
UpdateSourcePreviewToggleButton(...)
```

Existing preview commands were guarded so they do not attempt to perform preview operations while the pane is hidden:

```text
SourcePreviewFitPage_Click
SourcePreviewFindTable_Click
SourcePreviewZoomOut_Click
SourcePreviewZoomIn_Click
```

### 9.6 Downstream Impact

| Downstream area | Impact |
|---|---|
| WPF dialog usability | Users can focus on parsed grid or source preview as needed. |
| Source preview rendering | Rendering state is tracked to avoid unnecessary re-rendering. |
| Import workflow | Selection/import behavior remains unchanged. |
| Table extraction | No impact. |
| Tests | Build/full application tests validate compile-time integration. |

---

## 10. File-by-File Implementation Ledger

### 10.1 `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs`

#### Change Type

PDF load-chart extraction/service logic.

#### What Changed

This file now:

- reindexes table candidates by visual reading order;
- preserves score-based best-table selection separately;
- appends verified combined continuation candidates;
- labels combined candidates distinctly;
- builds combined raw grids and import grids;
- rejects ambiguous continuation pairs;
- exposes private test helpers for regression testing.

#### Why It Changed

The importer needed to represent the physical layout and logical continuity of manufacturer load-chart pages more accurately.

#### Main Technical Changes

1. **Visible table ordering**

   The candidate extraction path now returns candidates after reading-order reindexing rather than score-order reindexing.

2. **Continuation candidate generation**

   After reading-order reindexing, the service evaluates adjacent table candidates and appends combined candidates when verification succeeds.

3. **Combined candidate classification**

   Candidate display logic recognizes `COMBINED_CONTINUATION_TABLE` warnings and produces a `Combined continuation` label.

4. **Grid construction**

   Combined grids preserve both source table column sets and merge rows by radius.

#### Downstream Impact

- Directly affects PDF import candidate list.
- Indirectly affects load-chart data available for import.
- Does not directly modify saved crane database schema or GA algorithms.

#### Maintenance Notes

Future changes should preserve the conservative nature of continuation detection. Any relaxation of the shared-boundary rule should be supported by diagnostics and regression tests on real manufacturer PDFs.

---

### 10.2 `CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs`

#### Change Type

Regression tests for PDF table ordering and continuation candidate generation.

#### What Changed

This test file now contains regression coverage for:

- side-by-side reading-order reindexing;
- visual-row ordering;
- score-based best-table preservation;
- verified continuation candidate creation;
- rejection of broad overlapping ranges;
- rejection of unrelated separated ranges;
- rejection of small forward gaps without shared boundary;
- rejection of incompatible radius axes.

#### Why It Changed

The new extraction behavior is geometry- and data-dependent. Tests are required to prevent future regressions that would silently reintroduce wrong table numbering or unsafe merging.

#### Downstream Impact

- No runtime impact.
- Improves maintainability and confidence around PDF table segmentation.

#### Maintenance Notes

If future diagnostics justify merging small-gap continuation cases, add tests before modifying the merge rule.

---

### 10.3 `CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml`

#### Change Type

WPF layout and control markup.

#### What Changed

The XAML now includes:

- a local toggle button style;
- a `BtnToggleSourcePreview` control;
- named source-preview splitter and column definitions;
- layout structure that can collapse and restore the source preview area.

#### Why It Changed

The user needed the ability to hide/show the source PDF preview without losing selection/import context.

#### Downstream Impact

- Directly affects the PDF Load Chart Import dialog UI.
- Does not affect extraction algorithms.
- Does not affect data persistence.

#### Maintenance Notes

The toggle control should remain accessible outside the collapsible preview pane. Moving the toggle inside the source preview pane would create an unrecoverable hidden state.

---

### 10.4 `CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs`

#### Change Type

WPF code-behind for preview visibility and render-state management.

#### What Changed

The code-behind now tracks:

- whether the source preview is visible;
- the previous source-preview column width;
- which table preview was rendered;
- how to restore or skip rendering based on current visibility.

#### Why It Changed

The UI needed layout behavior that was reversible, state-safe, and integrated with existing preview commands.

#### Downstream Impact

- Directly affects the PDF Load Chart Import dialog runtime behavior.
- Existing source preview commands are guarded while hidden.
- Import selection and parsed-grid behavior remain unchanged.

#### Maintenance Notes

Any new source-preview toolbar commands should check `_isSourcePreviewVisible` or route through the same visibility-aware methods.

---

## 11. Subsystem-Level Impact Assessment

### 11.1 PDF Engine / PDF Import Subsystem

#### Direct Impact

This is the primary affected subsystem. The branch changes how PDF table candidates are ordered, labeled, and augmented.

#### Behavioral Impact

- Extracted tables are still parsed from the same underlying PDF word/geometry data.
- Candidate display order now matches visual reading order.
- Verified continuation candidates are added after original candidates.
- Combined candidates use existing raw-grid parsing mechanisms.

#### Regression Risk

Medium. PDF parsing is inherently layout-sensitive. The branch mitigates risk with targeted diagnostics and tests, but manual testing across multiple manufacturer PDFs remains important.

### 11.2 WPF PDF Load Chart Import UI

#### Direct Impact

The dialog receives a new preview-toggle workflow.

#### Behavioral Impact

- Preview visible by default.
- Parsed table grid can expand when preview is hidden.
- Table selection and import buttons remain functionally unchanged.

#### Regression Risk

Low to medium. XAML/layout changes can have edge-case visual effects at small window sizes. Manual UI smoke testing is recommended.

### 11.3 Load-Chart Parsing and Import

#### Direct Impact

The branch adds new candidate options but does not rewrite the core CSV/raw-grid parsing implementation.

#### Indirect Impact

If the user selects a combined continuation candidate, the import pipeline receives a larger grid than either individual source table. That can affect the saved load chart and downstream optimizer capacity coverage.

#### Regression Risk

Medium for combined candidate cases; low for ordinary single-table pages.

### 11.4 Genetic Algorithm / Optimizer

#### Direct Impact

No direct GA algorithm files were changed in this branch.

#### Indirect Impact

The optimizer consumes load charts saved/imported by the UI. If a user imports a combined continuation candidate, the optimizer may later have access to a broader set of load-chart points. This is an intended downstream data effect, not an algorithmic change.

#### Explicit Non-Changes

The branch does not modify:

- GA population generation;
- fitness evaluation rules;
- mutation/crossover;
- assignment algorithms;
- placement orchestration;
- diagnostics schemas for GA outputs.

### 11.5 Diagnostics and Verification Logic

#### Direct Impact

The branch adds/uses validation diagnostics during implementation and expands PDF load-chart regression tests.

#### Runtime Diagnostics Impact

No persistent runtime diagnostic schema is changed. The diagnostics used during implementation are external PowerShell/log artifacts, not production output pipelines.

### 11.6 Streamlit UI

No Streamlit subsystem was detected in the relevant project evidence. The UI affected by this branch is WPF.

```text
Streamlit impact: none detected.
```

### 11.7 Installers and Deployment

No installer, packaging, deployment manifest, or setup script was modified.

```text
Installer impact: none detected.
```

### 11.8 Dependencies

No `.csproj` or NuGet dependency file was modified.

```text
Dependency impact: none detected.
```

The build continues to show existing xBIM compatibility warnings, but those warnings were not introduced by this branch and did not block validation.

### 11.9 Outputs Pipeline

No GA output CSV schema was changed by this branch. The branch affects what load chart can be imported, not how GA outputs are written.

### 11.10 Reproducibility Systems

The branch improves reproducibility of PDF import review by making table numbering visual and by making combined candidates explicit and auditable. It does not change project-level reproducibility infrastructure or database migration systems.

---

## 12. Algorithmic and UI Design Decisions

### 12.1 Decision: Separate Visible Numbering from Best-Table Scoring

#### Context

High extraction score is useful for selecting a best candidate, but it is not a correct basis for user-facing numbering.

#### Decision

Use visual reading order for displayed table indices while retaining score-based `GetBestTable()` behavior.

#### Consequences

- UI labels match the page layout.
- Automatic best-table import remains quality-driven.
- Tests explicitly lock this separation.

### 12.2 Decision: Only Merge Strongly Verified Continuation Pairs

#### Context

Some side-by-side tables are true continuations, while others are unrelated, overlapping, or ambiguous.

#### Decision

Create combined candidates only when multiple signals agree: geometry, same row, shared boundary, non-overlap except boundary, compatible radius axis, compatible table kind, and importable combined grid.

#### Consequences

- False-positive merges are minimized.
- Some plausible but ambiguous continuation cases are intentionally not merged.
- Future rule relaxation must be evidence-driven.

### 12.3 Decision: Preserve Original Tables

#### Context

A combined table may be useful, but users may still want either individual source table.

#### Decision

Append combined candidates without removing or replacing original candidates.

#### Consequences

- The workflow remains reversible.
- Users can compare individual and combined previews.
- Existing single-table selection behavior is preserved.

### 12.4 Decision: Put Preview Toggle Outside the Preview Pane

#### Context

If the toggle lived inside the preview pane, hiding the pane would also hide the control needed to restore it.

#### Decision

Place the toggle in the Load Chart Preview header.

#### Consequences

- The control remains accessible in both visible and hidden states.
- The UI avoids a trapped hidden-preview state.

### 12.5 Decision: Collapse Layout, Not Merely Hide It

#### Context

A hidden preview pane that still reserves layout space would not solve the workspace issue.

#### Decision

Collapse the preview pane/splitter and set the source-preview column width to zero.

#### Consequences

- The parsed grid expands into freed space.
- Restoring requires remembering a previous column width.
- Code-behind owns the layout state.

### 12.6 Decision: Use Code-Behind Rather Than a New ViewModel

#### Context

`PdfLoadChartImportDialog` already uses code-behind for preview rendering, zooming, table selection, and import actions.

#### Decision

Implement preview visibility management in the existing code-behind rather than introducing a new ViewModel for a small localized dialog behavior.

#### Consequences

- Keeps the change localized.
- Avoids unnecessary architectural churn.
- Maintains consistency with existing dialog implementation style.

### 12.7 Decision: Add Private Test Helpers Rather Than Full PDF Fixtures for Every Case

#### Context

Full PDF fixture tests are slower and more brittle. The core logic can be tested with synthetic candidate DTOs.

#### Decision

Expose private testing helpers through reflection in the existing test file.

#### Consequences

- Tests remain fast and focused.
- Synthetic cases can isolate edge behavior precisely.
- Real-PDF diagnostics remain complementary evidence rather than every-test dependency.

---

## 13. Validation, Diagnostics, and Reproducibility Record

### 13.1 Baseline Context Collection

The branch work began from a clean local branch based on `origin/div`.

Initial forensic/complete packages verified:

```text
dotnet restore: exit code 0
dotnet build: exit code 0
dotnet test: exit code 0
```

They also confirmed the project structure, branch, and relevant source files.

### 13.2 Ordering Diagnostics

PDF ordering diagnostics confirmed the exact issue on `130Tons.pdf`, page 14:

```text
Current Table 1: right physical table
Current Table 2: left physical table
ORDERING_MISMATCH_DETECTED=True
Expected: physical left table first, physical right table second
```

This justified a geometry-based reading-order patch.

### 13.3 Reading-Order Patch Validation

The reading-order patch completed with:

```text
dotnet restore: exit code 0
dotnet build: exit code 0
Targeted PDF load-chart ordering tests: passed
Full application tests: passed
```

Post-patch state at that stage:

```text
Modified:
  CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
  CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
Untracked:
  docs.zip
```

### 13.4 Continuation Diagnostics

Continuation diagnostics scanned real PDFs from:

```text
C:\Users\Bahyg\OneDrive\Dokumenty\Crane_Project
```

The scanned set included:

```text
130Tons.pdf
100Tons.pdf
100Tons-Liebherr.pdf
12_Seiter_Prospekt_SRC550C_eng.pdf
180Tons.pdf
200 t cap.pdf
200Tons.pdf
70Tons-Liebherr.pdf
80Tons.pdf
```

The diagnostics confirmed safe continuation cases, including `130Tons.pdf` page 14:

```text
Table 1 boom range: 13.3 -> 34.8
Table 2 boom range: 34.8 -> 60
Shared boundary: 34.8
Recommendation: RECOMMENDED
```

The diagnostics also identified `POSSIBLE_REVIEW` cases, which informed the conservative implementation.

### 13.5 Continuation Patch Attempt and Recovery

The first continuation patch script failed because a patch anchor matched more than once:

```text
Found expected text more than once while patching:
append verified continuation candidates after reading-order numbering
```

A read-only verification confirmed no partial continuation markers were installed.

The corrected V2 patch used a precise extraction-method anchor and completed successfully.

### 13.6 Continuation Patch V2 Validation

Final continuation V2 validation:

```text
Patch markers verified.

dotnet restore: exit code 0
dotnet build solution: exit code 0
Targeted PDF load-chart continuation tests:
  Failed: 0
  Passed: 13
  Skipped: 0
  Total: 13

Full application test project:
  Failed: 0
  Passed: 369
  Skipped: 1
  Total: 370
```

Post-patch state at that stage:

```text
Modified:
  CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
  CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
Untracked:
  docs.zip
```

### 13.7 Preview Toggle Context Collection

Before UI patching, a context package captured the exact dialog XAML/code-behind and current Git state. It confirmed:

- the PDF import dialog already had parsed preview and source preview panes;
- the source preview used existing zoom/find/fit controls;
- no PDF-preview UI files were yet modified;
- the correct patch target was the dialog UI files.

### 13.8 Preview Toggle Patch Attempt and Recovery

The first preview toggle patch modified the intended files and verified markers but failed during validation because the script used `ProcessStartInfo.ArgumentList`, which is not available in Windows PowerShell 5.1.

The corrected V2 script detected that the markers were already installed and then ran validation using a PowerShell 5.1-compatible command runner.

### 13.9 Preview Toggle Patch V2 Validation

Final preview toggle V2 validation:

```text
Patch already installed.
Preview toggle markers already exist.
Patch markers verified.

dotnet restore: exit code 0
dotnet build solution: succeeded
dotnet test full application test project:
  Failed: 0
  Passed: 369
  Skipped: 1
  Total: 370
```

Final branch state:

```text
Modified:
  CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
  CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
Untracked:
  docs.zip
```

Final diff stat:

```text
4 files changed, 1130 insertions(+), 152 deletions(-)
```

### 13.10 Warnings Observed

Validation completed with warnings. The warnings included:

- existing xBIM package compatibility warnings (`NU1701`);
- nullable warnings in broader project files;
- analyzer warnings in tests;
- one transient MSBuild copy retry due to a temporarily locked DLL.

No validation step ended in an error.

### 13.11 Reproducibility Commands

The final validation can be reproduced with:

```powershell
dotnet restore C:\Users\Bahyg\source\repos\CranePathOptimizer\CraneOptimizer.sln

dotnet build C:\Users\Bahyg\source\repos\CranePathOptimizer\CraneOptimizer.sln -c Debug --no-restore

dotnet test C:\Users\Bahyg\source\repos\CranePathOptimizer\CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj -c Debug --no-restore
```

Targeted continuation test reproduction:

```powershell
dotnet test C:\Users\Bahyg\source\repos\CranePathOptimizer\CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj `
  -c Debug `
  --no-restore `
  --filter FullyQualifiedName~PdfLoadChartMultiTableSegmentationTests
```

---

## 14. Backward Compatibility and User Workflow Preservation

### 14.1 Existing Single-Table PDFs

Expected impact for ordinary single-table PDFs:

```text
No combined candidate is added.
Table remains selectable.
Import behavior remains unchanged.
Source preview remains visible by default.
```

### 14.2 Existing Multi-Table PDFs Without Verified Continuation

Expected impact:

```text
Tables are displayed in visual reading order.
No combined candidate is added unless verification succeeds.
User can still select individual tables.
```

### 14.3 Verified Continuation PDFs

Expected impact:

```text
Original segment tables remain selectable.
Additional combined candidate is appended.
Combined candidate can be selected and imported by the user.
```

### 14.4 Existing Source Preview Behavior

Expected impact:

```text
Default visible state preserves existing workflow.
Fit Page / Find Table / Zoom controls remain available when preview is visible.
Preview commands are safely guarded when preview is hidden.
```

### 14.5 Automatic Import Behavior

`ImportPdf(...)` still uses `GetBestTable()` for automatic best-table selection. The branch separates visible numbering from score-based best-table selection.

### 14.6 Database Compatibility

No database schema changes are introduced by this branch segment.

### 14.7 CSV/Diagnostics Compatibility

No GA diagnostics CSV schemas are modified by this branch segment.

---

## 15. Performance, Reliability, and Robustness Considerations

### 15.1 Candidate Ordering Performance

Reading-order reindexing operates on the list of table candidates per page. Candidate counts are small relative to PDF word count. The overhead is negligible compared with PDF parsing and rendering.

### 15.2 Continuation Detection Performance

Continuation detection evaluates adjacent candidates after reading-order indexing. It performs header/radius extraction from candidate grids and builds combined grids only for verified pairs.

The expected computational cost is low because:

- candidate counts per page are small;
- grids are modestly sized relative to full PDF rendering;
- expensive PDF parsing has already completed before continuation checks.

### 15.3 Source Preview Toggle Performance

Hiding the preview can reduce UI rendering pressure because preview operations are skipped while the pane is hidden. Showing it again may re-render if the current selected table preview is not already rendered.

### 15.4 Reliability Improvements

The branch improves reliability by:

- aligning labels with visual order;
- avoiding unsafe automatic merges;
- keeping original candidates available;
- guarding preview commands while hidden;
- adding regression tests for core PDF table behaviors.

### 15.5 Known Reliability Boundaries

PDF table extraction remains dependent on selectable text and layout geometry. Scanned/image-only PDFs remain outside the importer’s current text-based extraction capability.

---

## 16. Known Limitations, Risks, and Follow-Up Items

### 16.1 Manual UI Smoke Testing Still Recommended

Automated validation confirms compile/test integration, but WPF layout should be manually inspected.

Recommended manual checks are listed in [Appendix E](#appendix-e--suggested-manual-smoke-test-protocol).

### 16.2 Conservative Continuation Rule May Miss Some True Continuations

The current implementation requires a shared/touching boundary. This avoids unsafe merges but may exclude some legitimate continuation pairs where the right panel starts at a small forward gap.

Future enhancement path:

```text
1. Collect more real-PDF diagnostics.
2. Identify repeated true-positive small-gap patterns.
3. Add tests for those cases.
4. Relax the rule only when false-positive risk is controlled.
```

### 16.3 Combined Candidate User Education

Users should understand that a combined candidate is generated by verification logic and should still be reviewed before import. The warning text and display label help, but future UI text could make this even clearer.

### 16.4 Preview Toggle Preference Is Not Documented as Persisted

The source preview is visible by default. The current implementation is designed as a dialog-level view option. If product requirements later call for remembering the user’s preference across sessions, that should be a separate settings-level feature.

### 16.5 Small Window Layout Edge Cases

The preview toggle changes column widths and splitter visibility. Very small window sizes should be manually checked to ensure the parsed grid and action buttons remain usable.

### 16.6 Patch Script Lessons

Two script-level issues were encountered and resolved:

1. A non-unique patch anchor in the first continuation patch.
2. Use of `ProcessStartInfo.ArgumentList` in the first preview toggle patch, incompatible with Windows PowerShell 5.1.

Both were script issues, not final source-code validation failures. The final V2 patches validated successfully.

---

## 17. Reviewer Guide

### 17.1 High-Priority Review Areas

Reviewers should focus on:

1. `LoadChartPdfService.cs`
   - reading-order grouping and reindexing;
   - continuation pair verification rules;
   - combined raw-grid construction;
   - candidate labels/warnings;
   - preservation of `GetBestTable()` score behavior.

2. `PdfLoadChartMultiTableSegmentationTests.cs`
   - positive continuation test;
   - false-positive prevention tests;
   - reading-order tests.

3. `PdfLoadChartImportDialog.xaml`
   - toggle placement;
   - source preview column/splitter naming;
   - style consistency with existing UI.

4. `PdfLoadChartImportDialog.xaml.cs`
   - visibility state transitions;
   - source preview render-state tracking;
   - guarded zoom/find/fit behavior;
   - restoration of source preview column width.

### 17.2 Review Questions

- Does reading-order reindexing behave correctly for stacked and side-by-side tables?
- Are combined candidates appended without replacing originals?
- Are ambiguous continuation cases rejected?
- Does the shared-boundary rule match current product risk tolerance?
- Does `GetBestTable()` remain score-based?
- Is the preview toggle accessible when the preview is hidden?
- Does hiding the preview truly free layout space?
- Are preview commands safe while hidden?
- Are all changes limited to the intended subsystem?

### 17.3 Suggested Source Inspection Commands

```bash
git diff -- CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs

git diff -- CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs

git diff -- CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml

git diff -- CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
```

### 17.4 Suggested Validation Commands

```bash
dotnet restore CraneOptimizer.sln

dotnet build CraneOptimizer.sln -c Debug --no-restore

dotnet test CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj -c Debug --no-restore
```

Targeted:

```bash
dotnet test CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj \
  -c Debug \
  --no-restore \
  --filter FullyQualifiedName~PdfLoadChartMultiTableSegmentationTests
```

---

## 18. Commit and Integration Guidance

### 18.1 Recommended Commit Strategy

Because this branch contains related but separable changes, either of these commit strategies is defensible:

#### Option A — Single Cohesive Feature Commit

```text
feat(load-chart-pdf): improve multi-table import review workflow
```

Use when the branch is intended to merge as one cohesive PDF import enhancement.

#### Option B — Three Focused Commits

```text
fix(load-chart-pdf): order extracted tables by visual reading order
feat(load-chart-pdf): add verified continuation table candidates
feat(load-chart-pdf): add source preview visibility toggle
```

Use when reviewers prefer smaller isolated commits.

#### Documentation Commit

```text
docs(load-chart-pdf): add branch implementation dossier
```

### 18.2 Files to Stage

```bash
git add \
  CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs \
  CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs \
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml \
  CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs \
  docs/implementation/pdf-load-chart-import-robustness-dossier.md
```

### 18.3 Files Not to Stage Unless Intentional

```bash
docs.zip
```

### 18.4 Suggested Pull Request Summary

```markdown
## Summary

This branch improves the PDF Load Chart Import workflow for multi-table manufacturer PDFs.

- Reindexes extracted PDF table candidates by visual reading order.
- Preserves score-based best-table selection separately from visible numbering.
- Adds verified combined continuation candidates for strongly related side-by-side table panels.
- Keeps original table candidates selectable.
- Adds a professional Show/Hide PDF Preview toggle to the PDF import dialog.
- Adds regression tests for reading order and continuation candidate behavior.

## Validation

- dotnet restore: passed
- dotnet build -c Debug --no-restore: passed
- targeted PDF load-chart tests: 13 passed, 0 failed
- full Application.Tests project: 369 passed, 1 skipped, 0 failed
```

---

## Appendix A — Changed Files by Layer

### UI / PDF Load Chart Service

```text
CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
```

### UI / PDF Import Dialog

```text
CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml
CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
```

### Tests

```text
CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
```

### Untracked Local Artifact

```text
docs.zip
```

`docs.zip` is not part of the intended branch diff unless explicitly staged for a separate documentation purpose.

---

## Appendix B — Validation Artifact Index

### B.1 Ordering Investigation

```text
CranePathOptimizer_PDF_LOAD_CHART_ORDERING_DIAGNOSTICS_V4_20260611_074829.txt
```

Purpose:

- verified page-14 side-by-side table ordering issue;
- confirmed mismatch between score order and visual order;
- guided reading-order patch.

### B.2 Reading-Order Patch

```text
Apply-PdfLoadChartReadingOrderPatch.ps1
CranePathOptimizer_PdfLoadChartReadingOrderPatch_20260611_075637.log
```

Purpose:

- implemented visual reading-order reindexing;
- validated restore/build/tests.

### B.3 Continuation Diagnostics

```text
CranePathOptimizer_PDF_LOAD_CHART_CONTINUATION_DIAGNOSTICS_V2_20260611_081945.txt
```

Purpose:

- scanned multiple real crane PDFs;
- identified recommended and possible-review continuation pairs;
- justified conservative shared-boundary rule.

### B.4 Continuation Patch

```text
Apply-PdfLoadChartContinuationCombinedTablesPatch-v2.ps1
CranePathOptimizer_PdfLoadChartContinuationPatch_20260611_084232.log
```

Purpose:

- implemented verified combined continuation candidates;
- validated targeted tests and full test project.

### B.5 Preview Toggle Context

```text
CranePathOptimizer_PDF_LOAD_CHART_PREVIEW_TOGGLE_CONTEXT_20260611_085433.txt
```

Purpose:

- captured current WPF dialog structure and styles;
- confirmed safe target files for preview toggle.

### B.6 Preview Toggle Patch

```text
Apply-PdfLoadChartPreviewTogglePatch-v2.ps1
CranePathOptimizer_PdfLoadChartPreviewTogglePatch_V2_20260611_090534.log
```

Purpose:

- validated the already-installed preview-toggle source changes;
- confirmed final branch build/test success.

---

## Appendix C — Terminology

| Term | Meaning |
|---|---|
| PDF Load Chart Import dialog | WPF dialog used to review and import PDF-extracted crane load-chart tables. |
| Table candidate | A parsed potential load-chart table extracted from a PDF page. |
| Visual reading order | Human reading order based on page layout: top-to-bottom, then left-to-right. |
| Score order | Ordering by extraction confidence/quality score. Useful for best-table selection but not user-facing labels. |
| Continuation table | A logical load chart split across multiple physical table panels. |
| Shared boundary | A repeated/adjacent boom header value at the boundary between two table segments, e.g. left max `34.8` and right min `34.8`. |
| Combined continuation candidate | An additional generated candidate that merges verified source tables into one importable grid. |
| Source PDF Preview | The pane that renders the original PDF page and highlights the selected table. |
| Parsed Load Chart Preview | The grid preview of the extracted/importable table candidate. |
| GridSplitter | WPF control that allows resizing adjacent grid columns. |
| `Visibility.Collapsed` | WPF visibility state that hides an element and removes its layout space. |

---

## Appendix D — External Documentation and Engineering Standards Consulted

The document format and implementation rationale were informed by the following public references.

| Reference | URL | Relevance |
|---|---|---|
| Architecture Decision Records | `https://adr.github.io/` | Informed context/decision/consequence framing. |
| Architecture Decision Record repository | `https://github.com/architecture-decision-record/architecture-decision-record` | Reinforced the role of ADRs in capturing important decisions with context and consequences. |
| Keep a Changelog | `https://keepachangelog.com/en/1.1.0/` | Informed human-readable change grouping and avoidance of raw diff-only documentation. |
| The Turing Way — Reproducible Research | `https://book.the-turing-way.org/reproducible-research/reproducible-research/` | Informed reproducibility and rerunnable validation sections. |
| The Turing Way — Project Documentation | `https://book.the-turing-way.org/reproducible-research/code-documentation/code-documentation-project/` | Informed project-level documentation structure. |
| Microsoft WPF `ToggleButton.IsChecked` | `https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.togglebutton.ischecked` | Supports toggle-button state design. |
| Microsoft WPF `Visibility` enum | `https://learn.microsoft.com/en-us/dotnet/api/system.windows.visibility` | Supports using `Collapsed` to remove layout space. |
| Microsoft WPF `GridSplitter` column resizing | `https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-resize-columns-with-a-gridsplitter` | Supports splitter/column layout behavior. |
| PdfPig document layout analysis | `https://github.com/UglyToad/PdfPig/wiki/Document-Layout-Analysis` | Supports geometry/reading-order-aware PDF layout interpretation. |

---

## Appendix E — Suggested Manual Smoke-Test Protocol

Automated validation passed, but the following manual test is recommended before merge.

### E.1 Setup

```text
Branch: tmp/div-next-implementation
PDF: C:\Users\Bahyg\OneDrive\Dokumenty\Crane_Project\130Tons.pdf
Focus page: 14
```

### E.2 Reading Order

1. Launch the WPF application.
2. Open the PDF Load Chart Import flow.
3. Select `130Tons.pdf`.
4. Navigate to page 14.
5. Confirm that the physical left table is listed as `Table 1`.
6. Confirm that the physical right table is listed as `Table 2`.

### E.3 Combined Continuation Candidate

1. On page 14, inspect the detected tables list.
2. Confirm that original source tables remain selectable.
3. Confirm that an additional `Combined continuation` candidate appears when verification succeeds.
4. Select the combined candidate.
5. Confirm that the parsed preview includes boom headers from both source panels.
6. Confirm that import remains enabled for the combined candidate.

### E.4 Source PDF Preview Toggle

1. Confirm the Source PDF Preview pane is visible by default.
2. Click `Hide PDF Preview`.
3. Confirm the source preview pane and splitter disappear.
4. Confirm the parsed grid expands into the freed space.
5. Confirm the toggle remains visible and changes to `Show PDF Preview`.
6. Click `Show PDF Preview`.
7. Confirm the source preview pane returns.
8. Confirm `Fit Page`, `Find Table`, zoom out, and zoom in still work.

### E.5 Regression Check on Non-Continuation Page

1. Open a page with one table or unrelated multiple tables.
2. Confirm no unsafe combined continuation candidate appears.
3. Confirm individual table import remains unchanged.

### E.6 Final Git Hygiene Check

```bash
git status --short --branch
```

Expected before staging:

```text
M CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartMultiTableSegmentationTests.cs
M CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
M CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml
M CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
?? docs.zip
```

Stage only intended source files and this documentation file.
