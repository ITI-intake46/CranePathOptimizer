# CranePathOptimizer Branch Implementation Dossier

## `temp/div-implementation` — GA Load-Chart Operating Setup Selector, Engineering-Intuition UI, Validation Hardening, and Documentation Completion

**Repository:** `ITI-intake46/CranePathOptimizer`  
**Working branch:** `temp/div-implementation`  
**Base observed in review evidence:** `origin/div` at `04e9d37`  
**Document type:** Engineering-grade branch implementation dossier / technical change report / architecture-impact record / validation and reproducibility record  
**Document generated:** 2026-06-12  
**Primary subsystem:** `CraneOptimizer.UI/Features/F8_GAOptimizer`  
**Related upstream subsystems:** PDF load-chart import, load-chart provenance preservation, GA capacity resolution, GA diagnostics  
**Recommended repository placement:** `docs/implementation/ga-load-chart-operating-setup-selector-dossier.md`  
**Recommended commit pairing:** Commit alongside the branch changes that introduce the F8 Load Chart Operating Setup selector and its cleanup/hardening work.

---

## Documentation Strategy Decision

Create this as a **new branch dossier**, not as an in-place replacement of the older related Markdown documents.

Two related historical implementation dossiers were provided as context:

| Related dossier | Scope | Relationship to this branch |
|---|---|---|
| `load-chart-provenance-diagnostics.md` | Preserves imported load-chart provenance through GA diagnostics. | Upstream and adjacent. It explains how metadata such as line count and operating condition became meaningful through optimizer diagnostics. |
| `pdf-load-chart-import-robustness-dossier.md` | Hardens the PDF load-chart import review workflow, table continuation handling, reading-order reconstruction, and source preview behavior. | Upstream and adjacent. It explains how load-chart metadata is imported and reviewed before being saved into crane data. |

This branch is a **distinct downstream runtime-selection feature** in the F8 Crane Position Optimizer. It consumes imported/preserved metadata and exposes it as a user-facing operating-setup constraint during GA execution:

```text
PDF load-chart import / provenance capture
        ↓
Persisted crane load-chart metadata
        ↓
F8 Crane Position Optimizer dialog
        ↓
Load Chart Operating Setup selector
        ↓
CraneOptimizerDialogOptions
        ↓
CraneOptimizerDialogWorkflow.BuildSettings(...)
        ↓
GASettings.LoadChartNumberOfLinesConstraint
GASettings.LoadChartOperatingConditionConstraint
        ↓
Existing LoadChartCapacityResolver filtering
        ↓
GA fitness, liftability, diagnostics, and final optimizer results
```

Keeping this document separate preserves auditability:

- older dossiers remain historically accurate for their original branches;
- this branch receives its own complete decision record;
- future maintainers can distinguish upstream import/provenance work from downstream F8 runtime selection;
- reviewers can evaluate this branch without needing private chat context or patch-script history.

---

## Table of Contents

1. [Document Control and Evidence Basis](#1-document-control-and-evidence-basis)
2. [Professional Documentation Methodology](#2-professional-documentation-methodology)
3. [Executive Summary](#3-executive-summary)
4. [Problem Statement and Engineering Motivation](#4-problem-statement-and-engineering-motivation)
5. [Scope, Non-Scope, and Branch Boundaries](#5-scope-non-scope-and-branch-boundaries)
6. [Prior-Chat and Current-Chat Implementation Continuity](#6-prior-chat-and-current-chat-implementation-continuity)
7. [Architecture Context and Project Rules](#7-architecture-context-and-project-rules)
8. [Architecture Before and After This Branch](#8-architecture-before-and-after-this-branch)
9. [End-to-End Runtime Flow](#9-end-to-end-runtime-flow)
10. [Feature-Level Implementation Details](#10-feature-level-implementation-details)
11. [File-by-File Implementation Ledger](#11-file-by-file-implementation-ledger)
12. [Subsystem-Level Impact Assessment](#12-subsystem-level-impact-assessment)
13. [Genetic Algorithm and Capacity Resolution Impact](#13-genetic-algorithm-and-capacity-resolution-impact)
14. [PDF Load-Chart Import and Provenance Interaction](#14-pdf-load-chart-import-and-provenance-interaction)
15. [UI, MVVM, Workflow, and Layering Review](#15-ui-mvvm-workflow-and-layering-review)
16. [Diagnostics, Outputs, Reproducibility, and Artifact Hygiene](#16-diagnostics-outputs-reproducibility-and-artifact-hygiene)
17. [Validation and Reproducibility Record](#17-validation-and-reproducibility-record)
18. [Regression-Prevention Strategy](#18-regression-prevention-strategy)
19. [Technical Tradeoffs and Design Decisions](#19-technical-tradeoffs-and-design-decisions)
20. [Known Limitations and Future Work](#20-known-limitations-and-future-work)
21. [Risk Assessment](#21-risk-assessment)
22. [Maintainer Review Checklist](#22-maintainer-review-checklist)
23. [Appendix A — Changed File Summary](#appendix-a--changed-file-summary)
24. [Appendix B — Implementation Mapping](#appendix-b--implementation-mapping)
25. [Appendix C — Validation Commands](#appendix-c--validation-commands)
26. [Appendix D — Evidence Artifacts](#appendix-d--evidence-artifacts)
27. [Appendix E — Documentation References and Related Dossiers](#appendix-e--documentation-references-and-related-dossiers)
28. [Appendix F — Manual Smoke-Test Protocol](#appendix-f--manual-smoke-test-protocol)

---

## 1. Document Control and Evidence Basis

### 1.1 Purpose of This Document

This document records the technical intent, implementation details, architecture impact, validation evidence, reproducibility considerations, and maintenance consequences of the `temp/div-implementation` branch work that introduced a **GA Load-Chart Operating Setup selector**.

This is not a lightweight changelog. It is intended as a long-lived engineering artifact for:

- senior maintainers reviewing architectural fit;
- developers extending the GA optimizer dialog;
- contributors working on PDF load-chart import or provenance metadata;
- QA engineers reproducing validation results;
- researchers interpreting GA outputs that depend on load-chart operating setup constraints;
- future maintainers diagnosing how a selected line count or operating condition reaches GA capacity lookup.

### 1.2 Evidence Sources

This dossier incorporates evidence from both the **previous uploaded chat** and the **current chat**. It was prepared from:

| Evidence artifact | Purpose |
|---|---|
| `CranePathOptimizer_FullChangeReviewPackage_SAFE_20260612_201910.txt` | Prior review package showing the state after the collapsible selector implementation and validation. |
| `CranePathOptimizer_COMPLETE_ForensicPackage_V5_20260612_203811.txt` | Complete source/forensic package used in the current chat before cleanup/hardening. |
| `CranePathOptimizer_CODE_CHANGES_REVIEW_20260612_205001.txt` | Code-changes review package used for critical review of all current diff changes before the final cleanup patch. |
| `CraneOptimizer_GA_LineSelector_CleanupValidationV4_20260612_210334.txt` | Full validation log after cleanup/hardening with focused and full tests. |
| `CranePathOptimizer_FINAL_PostPatchReview_20260612_211134.txt` | Final post-patch review package showing final diff, current file contents, build output, focused test output, diagnostic inventory, and final status. |
| `docs.zip` | Project documentation set containing agent, architecture, coding-standard, domain, workflow, and project documentation. |
| `load-chart-provenance-diagnostics.md` | Related upstream implementation dossier for provenance preservation. |
| `pdf-load-chart-import-robustness-dossier.md` | Related upstream implementation dossier for PDF import robustness. |

### 1.3 Repository State Represented

The final reviewed branch state was:

```text
Branch: temp/div-implementation
Base observed: origin/div at 04e9d37
Tracked changed files: 7
Untracked source artifacts in final status: none
Staged changes: none
Commit/push performed by scripts: no
```

Final `git status --short --branch` reported:

```text
## temp/div-implementation
 M .gitignore
 M CraneOptimizer.Application.Tests/GA/CraneOptimizerDialogViewModelTests.cs
 M CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CraneOptimizerDialogViewModel.cs
 M CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml
 M CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml.cs
 M CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs
 M docs/domain/pdf-load-chart-import.md
```

### 1.4 Validation Summary

Final validation evidence includes:

| Validation pass | Result |
|---|---|
| `git diff --check` | No whitespace errors; LF/CRLF warnings only for two text files. |
| `dotnet restore CraneOptimizer.sln` | Passed; existing `NU1701` warnings related to Xbim packages. |
| `dotnet build CraneOptimizer.sln --configuration Debug --no-restore` | Passed; `0` errors. |
| Focused `CraneOptimizerDialogViewModelTests` | Passed; `13` passed, `0` failed, `0` skipped. |
| Full solution test run from cleanup validation | Passed; `396` passed, `0` failed, `1` skipped, `397` total. |

---

## 2. Professional Documentation Methodology

### 2.1 Documentation Model Used

This dossier follows a combined pattern inspired by:

- design-document structure: context, problem, alternatives, implementation, validation, and impact;
- architecture decision records: decision, context, consequences, and future implications;
- engineering change reports: file-level ledger, subsystem impact, validation evidence, and rollback/readiness notes;
- reproducible-computing practice: explicit commands, environment notes, validation outputs, and artifact inventory.

### 2.2 Why This Level of Detail Is Needed

This branch touches the boundary between **user interaction**, **engineering semantics**, and **algorithmic capacity lookup**. Small wording or mapping mistakes could create false confidence in lifting behavior. Therefore, the documentation records:

- what the UI lets the user choose;
- what the setting does and does not mean;
- how the choice reaches `GASettings`;
- why capacity remains governed by imported manufacturer load-chart data;
- how tests prevent regressions in default behavior and mixed metadata handling;
- which adjacent subsystems were intentionally not changed.

### 2.3 Documentation Integrity Principles

This dossier intentionally avoids overstating the branch. It distinguishes:

| Category | Documentation treatment |
|---|---|
| Direct code changes | Fully documented file-by-file. |
| Indirect runtime effects | Documented through flow and subsystem impact. |
| Unchanged adjacent systems | Explicitly marked as not modified. |
| Validation evidence | Reported with exact commands and observed results. |
| Known limitations | Listed as future maintenance concerns. |
| Patch-script failures | Treated as process history, not source behavior, unless they influenced final code state. |

---

## 3. Executive Summary

### 3.1 What This Branch Adds

This branch adds a user-facing **Load Chart Operating Setup** selector to the F8 **Crane Position Optimizer** dialog.

The selector allows the user to keep the existing default behavior or constrain GA capacity lookup by imported load-chart metadata:

- **Any available line count / condition** — default, preserves previous behavior;
- a specific imported **number of lines** option, such as `2 lines`, `4 lines`, or `8 lines`;
- a specific imported **operating condition** option, such as `Condition: On outriggers 360 degrees`.

The selector is presented as a collapsible UI panel with:

- an automatic/constrained badge;
- a collapsed summary and subtext;
- a details toggle;
- a discrete slider over dynamic imported setup stops;
- scale labels;
- selected setup summary;
- line-support and hook-speed intuition cards;
- explicit safety wording stating that certified capacity still comes from imported manufacturer load charts.

### 3.2 What Problem It Solves

Prior upstream work preserved load-chart provenance such as line count and operating condition, and the GA capacity resolver already supported constraints through `GASettings`. However, the F8 optimizer dialog did not expose a clear user-facing runtime control for choosing those constraints.

The missing UI bridge meant users could import charts with meaningful operating metadata but could not intentionally constrain the optimizer to a specific operating setup from the main GA dialog.

This branch closes that gap.

### 3.3 What It Does Not Change

This branch does **not**:

- change the PDF load-chart extraction algorithm;
- change database schema;
- change crane catalog persistence schema;
- change GA chromosome design;
- change GA search operators;
- change capacity equations;
- change manufacturer capacity values;
- change Streamlit UI;
- change installers;
- change external dependencies;
- change diagnostics schema;
- stage, commit, push, or rename branches.

### 3.4 Final Implementation Assessment

The branch is functionally valid and architecture-aligned:

- default `Any` behavior leaves constraints unset;
- specific setup selections are passed through existing dialog options and workflow mapping;
- `GASettings` receives constraints through established application workflow code;
- existing `LoadChartCapacityResolver` remains responsible for capacity filtering;
- ViewModel owns UI state and presentation text;
- WPF code-behind performs only dialog orchestration;
- tests cover default behavior, constrained behavior, mixed metadata, collapse/expand behavior, and workflow mapping.

---

## 4. Problem Statement and Engineering Motivation

### 4.1 Domain Context

Crane load charts encode manufacturer-rated capacities under specific operating setups. These setup dimensions may include:

- number of hoist lines / parts of line;
- operating condition labels;
- visual column identity;
- boom length and radius;
- chart section/header context.

Prior project work made these metadata fields available in the domain model and diagnostics. The optimizer can already apply constraints through `GASettings` and `LoadChartCapacityResolver`.

### 4.2 User Workflow Gap

The F8 Crane Position Optimizer is where users run GA optimization. Before this branch, the runtime UI did not provide a clear, safe, discoverable control for choosing an imported load-chart operating setup.

This created several usability and engineering problems:

1. Users could import load-chart metadata but not intentionally use it in the optimizer dialog.
2. The GA could support constraints internally, but users lacked a runtime selection mechanism.
3. The default behavior was implicit rather than explicitly communicated.
4. Future diagnostics involving operating setup would be harder to explain without an upstream UI choice.
5. Users could not easily understand the tradeoff between more supporting lines and slower hook travel.

### 4.3 Safety Motivation

The UI must not imply that line count alone computes certified capacity. Certified lifting capacity must remain derived from imported manufacturer load-chart data. The branch therefore separates:

| Concept | Meaning in this branch |
|---|---|
| Line count | A setup constraint used to select eligible chart entries. |
| Operating condition | A setup label constraint used to select eligible chart entries. |
| Engineering intuition | Non-certifying explanatory UI text about expected support/speed tradeoffs. |
| Certified capacity | Still comes from imported manufacturer chart data via existing resolver logic. |

---

## 5. Scope, Non-Scope, and Branch Boundaries

### 5.1 In Scope

The branch includes:

- F8 dialog ViewModel state for operating-setup options;
- dynamic extraction of selector options from the selected crane's saved load chart metadata;
- default `Any` option preserving previous behavior;
- number-of-lines setup options;
- operating-condition setup options;
- mixed metadata handling when a chart point has both `NumberOfLines` and `OperatingConditionLabel`;
- collapsed/expanded UI panel state;
- discrete slider selection over dynamic setup stops;
- engineering-intuition text and percentages;
- safety note wording;
- code-behind refresh when selected crane changes;
- workflow mapping into `GASettings`;
- tests covering ViewModel and workflow behavior;
- `.gitignore` artifact hygiene;
- domain documentation update for downstream GA operating setup selection.

### 5.2 Out of Scope

The branch intentionally excludes:

- rewriting PDF import services;
- rewriting load-chart DTOs;
- adding new database migrations;
- changing `TargetPoint` schema;
- changing `GASettings` schema beyond using existing fields;
- changing `LoadChartCapacityResolver` filtering semantics;
- changing GA evolution algorithms;
- changing output file formats;
- changing Streamlit dashboards;
- changing installer or packaging behavior.

### 5.3 Boundary Rationale

The branch focuses on a missing UI/workflow bridge. The core resolver and metadata model already existed. Changing those layers would increase risk and violate feature isolation.

---

## 6. Prior-Chat and Current-Chat Implementation Continuity

This document deliberately covers **both** the previous uploaded chat and the current chat.

### 6.1 Previous Chat Work Captured

The previous chat established and iteratively implemented the main feature:

1. Created/used branch `temp/div-implementation` from `origin/div`.
2. Generated and ran patch scripts from the Downloads folder.
3. Implemented the first GA line-count / operating-condition selector plumbing.
4. Verified the initial selector markers:
   - `LoadChartOperatingSetupOption`;
   - `RefreshLoadChartOperatingSetupOptions(Crane? crane)`;
   - `LoadChartOperatingSetupOptions`;
   - `SelectedLoadChartOperatingSetupOption`;
   - code-behind capture;
   - workflow mapping into `GASettings`.
5. Iterated on UI design from a simple selector toward a richer UX panel.
6. Added engineering-intuition UI for line support and hook speed.
7. Replaced/augmented dropdown-style selection with a discrete slider over imported setup stops.
8. Removed misleading or noisy slider tooltip/index behavior.
9. Added collapsed/expanded panel behavior with clearer default-state communication.
10. Fixed a WPF binding issue involving read-only properties and hook-speed tendency binding.
11. Validated intermediate states with restore, build, `git diff --check`, and focused tests.
12. Stopped at the point where a full read-only review package was needed.

### 6.2 Current Chat Work Captured

The current chat continued from that cut-off state and completed the final engineering hardening:

1. Reconstructed the previous chat context from uploaded transcript/package evidence.
2. Generated review/forensic packages to inspect the final source state.
3. Verified project documentation from `docs.zip` and related dossiers.
4. Critically reviewed the implementation for correctness, cleanliness, robustness, and architecture fit.
5. Identified remaining cleanup items:
   - untracked `docs.zip` and `TestResults` artifacts;
   - XAML formatting roughness;
   - `Assert.Single(...Where(...))` analyzer issue;
   - condition grouping not exposing labels when line metadata was also present;
   - need for mixed metadata regression coverage.
6. Applied cleanup/hardening patch after several safe script revisions.
7. Hardened condition grouping to include all non-empty `OperatingConditionLabel` values, regardless of whether `NumberOfLines` is also present.
8. Added mixed metadata regression test.
9. Added `.gitignore` rules for local artifacts.
10. Moved untracked local artifacts out of final status.
11. Ran final validation with focused and full tests.
12. Generated final post-patch review package.
13. Created this complete branch dossier.

### 6.3 Why Patch-Script Attempts Are Mentioned but Not Treated as Product Behavior

Several PowerShell scripts were generated during development and review. Some failed due to pathspec, block-matching, or PowerShell string-interpolation issues. Those failures did not commit, push, stage, switch branches, or alter final source behavior beyond safe partial edits later validated by final runs.

For product documentation, the meaningful source state is the final branch diff and validation result. The process history is recorded here only to make the audit trail complete.

---

## 7. Architecture Context and Project Rules

### 7.1 Layered Architecture

The project is a layered C#/.NET WPF application. The branch adheres to the documented separation between:

| Layer | Responsibility | Branch behavior |
|---|---|---|
| Presentation/UI | WPF views, binding, user interaction, visual layout | Adds F8 dialog panel and binding only. |
| UI ViewModel | UI state, presentation-friendly option construction, collapse/slider state | Adds operating setup ViewModel state and text. |
| Application/workflow | Converts UI options into application settings | Maps selected constraints into `GASettings`. |
| Domain | Entities and core data model | No domain entity changes. Uses existing `Crane`, `CraneSpecification`, `TargetPoint`. |
| Infrastructure/data access | Persistence, repositories, external services | No infrastructure changes. |
| Resolver/engine | Capacity lookup and GA behavior | No resolver implementation changes; existing constraints are exercised. |

### 7.2 Project Documentation Consulted

The following documentation categories from `docs.zip` were treated as project rules:

- agent guidance: `docs/agent/start-here.md`, `agent-context.md`, `definition-of-done.md`, `do-not-do.md`;
- architecture: `docs/architecture/system-overview.md`, `folder-structure.md`, `engines.md`, `database.md`;
- coding standards: `clean-code.md`, `dependency-injection.md`, `dto-rules.md`, `mvvm.md`, `naming-conventions.md`;
- domain documents: `crane-engine.md`, `lifting-engine.md`, `ga-engine.md`, `ifc.md`, `path-engine.md` where present;
- workflows: `feature-development.md`, `bug-fixing.md`, `code-review.md`;
- project documents: `scope.md`, `vision.md`, `refactoring-opportunities.md`, documentation audit and coverage reports;
- load-chart import documentation: `docs/domain/pdf-load-chart-import.md`.

### 7.3 Architecture Rules Applied

The branch observes the following constraints:

- no business logic placed in WPF views;
- no bypass of workflow/settings plumbing;
- no direct mutation of GA internals from XAML/code-behind;
- no changes to unrelated features;
- no new dependency injection registrations needed;
- no DTO or mapper bypass added;
- no database or persistence changes introduced;
- no engine-layer logic duplicated in UI.

---

## 8. Architecture Before and After This Branch

### 8.1 Before

Before this branch:

- load-chart metadata could be imported and persisted;
- `TargetPoint` entries could contain line-count / operating-condition metadata;
- `GASettings` already contained capacity lookup constraint fields;
- `LoadChartCapacityResolver` already understood those constraints;
- the F8 optimizer dialog did not expose a professional runtime control for choosing those constraints.

Conceptual pre-branch flow:

```text
Crane selection
    ↓
GA settings built from existing dialog fields
    ↓
Capacity resolver uses unconstrained or preconfigured settings
    ↓
GA optimization
```

### 8.2 After

After this branch:

```text
Crane selection
    ↓
ViewModel scans selected crane load-chart metadata
    ↓
Operating setup options generated dynamically
    ↓
User keeps Any or chooses line count / condition
    ↓
Dialog captures selected option
    ↓
Workflow maps option into GASettings
    ↓
Existing resolver filters eligible chart entries
    ↓
GA optimization uses selected setup constraint
```

### 8.3 Architectural Importance

The branch does not move capacity logic into UI. The UI only selects a constraint. The existing resolver remains the authority for capacity lookup. This preserves the project layering and avoids duplicating engineering rules in WPF.

---

## 9. End-to-End Runtime Flow

### 9.1 Crane Selection

When the dialog loads cranes, and when the selected crane changes, code-behind asks the ViewModel to refresh operating setup options based on the selected crane:

```csharp
RefreshLoadChartOperatingSetupOptions(CboCrane.SelectedItem as Crane);
```

This is UI orchestration, not business logic.

### 9.2 Metadata Enumeration

The ViewModel reads load-chart entries from:

```text
Crane
  → Specifications
    → LoadChart
      → TargetPoint
```

It extracts:

- `NumberOfLines` for line-count options;
- `OperatingConditionLabel` for condition options;
- `VisualColumnIndex` / `VisualColumnHeader` / `BoomLength` to describe chart coverage.

### 9.3 Option Construction

The ViewModel always creates an `Any` default option first. It then appends imported line-count and operating-condition options.

Important final hardening: operating-condition options are generated from all non-empty labels, not only from points with no line-count metadata. This is necessary because real chart entries can carry both pieces of metadata.

### 9.4 User Selection

The UI presents the options through a collapsible panel and discrete slider. The slider value is an index into the current option list, but the UI labels expose user-meaningful setup names rather than internal indices.

### 9.5 Dialog Option Capture

When the optimizer is run, the code-behind includes these values in `CraneOptimizerDialogOptions`:

```csharp
vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint,
vm.SelectedLoadChartOperatingSetupOption.OperatingConditionConstraint
```

### 9.6 Workflow Mapping

The workflow maps the values into `GASettings`:

```csharp
LoadChartNumberOfLinesConstraint = options.LoadChartNumberOfLinesConstraint,
LoadChartOperatingConditionConstraint = NullIfWhiteSpace(options.LoadChartOperatingConditionConstraint)
```

### 9.7 Capacity Lookup

The branch relies on existing resolver behavior. If both constraints are null, previous behavior is preserved. If a constraint is present, the resolver filters eligible load-chart points according to the selected setting.

---

## 10. Feature-Level Implementation Details

### 10.1 Default `Any` Behavior

The default option is:

```text
Display: Any available line count / condition
NumberOfLinesConstraint: null
OperatingConditionConstraint: null
IsDefault: true
```

This preserves previous behavior because unset constraints allow every eligible imported chart entry to remain available to the resolver.

### 10.2 Line-Count Options

For each distinct positive `NumberOfLines` value, the ViewModel creates an option such as:

```text
2 lines
4 lines
8 lines
```

The option description includes imported chart cell count and visual column coverage.

### 10.3 Operating-Condition Options

For each distinct non-empty `OperatingConditionLabel`, the ViewModel creates an option such as:

```text
Condition: On outriggers 360 degrees
```

Final hardening ensures condition options are generated even when the same chart entries also contain `NumberOfLines`.

### 10.4 Slider Semantics

The slider is a discrete selector over generated options:

```text
Slider index 0 → Any
Slider index 1 → first imported setup option
Slider index 2 → second imported setup option
...
```

The implementation avoids exposing internal index values as user-facing tooltips. User-facing labels are derived from option display values.

### 10.5 Collapsible Panel Semantics

The panel is collapsed by default to preserve a compact optimizer dialog. The collapsed summary still communicates whether the optimizer is in automatic or constrained mode.

Key states:

| State | User-visible behavior |
|---|---|
| Default collapsed | Badge: `Automatic`; summary says any eligible chart entry is used. |
| Expanded | Slider, scale labels, coverage summary, intuition cards, safety note are visible. |
| Constrained then collapsed | Badge changes to `Constrained`; collapsed summary shows chosen setup. |

### 10.6 Engineering-Intuition UI

The branch adds explanatory UI for selected line counts:

- line-support index: relative support intuition from selected parts of line;
- hook-speed intuition: approximate inverse-speed intuition;
- selected tone: lighter/faster, balanced, heavier/slower;
- safety note: capacity still comes from imported manufacturer load charts.

This is intentionally explanatory, not a new capacity algorithm.

### 10.7 Safety Wording

The safety note is central:

```text
Engineering intuition only. The optimizer still uses imported manufacturer load-chart capacities; line count is a setup constraint, not a standalone certified capacity calculation.
```

This prevents users and future maintainers from interpreting the UI as a certified capacity calculation.

---

## 11. File-by-File Implementation Ledger

### 11.1 `.gitignore`

**What changed**

Added ignore rules for local test results and uploaded review bundles:

```gitignore
# Local test result artifacts
**/TestResults/
*.trx

# Local uploaded review bundles
docs.zip
```

**Why**

During review and validation, generated `.trx` files and uploaded `docs.zip` appeared as untracked artifacts. These should not be committed with source changes.

**Impact**

- Prevents accidental commits of local validation outputs.
- Keeps future Git status cleaner after tests.
- Does not affect runtime behavior.

### 11.2 `CraneOptimizer.Application.Tests/GA/CraneOptimizerDialogViewModelTests.cs`

**What changed**

The test file was expanded from basic ViewModel filtering/default tests to comprehensive coverage for the operating setup selector.

New coverage includes:

- engineering-intuition tradeoff behavior;
- discrete slider stop selection;
- default collapsed disclosure behavior;
- preservation of selected constraint when collapsed;
- imported number-of-lines choices;
- workflow mapping for selected line constraints;
- condition-only metadata exposure;
- trimming of operating-condition constraints;
- null constraints for `Any`;
- mixed metadata exposure when points have both `NumberOfLines` and `OperatingConditionLabel`.

**Why**

The selector is a safety-sensitive UI-to-GA bridge. Tests must prove:

- default behavior remains unchanged;
- constraints are correctly represented;
- workflow mapping is accurate;
- mixed metadata is not lost;
- UI state transitions do not reset selected constraints.

**How**

Tests construct in-memory `Crane`, `CraneSpecification`, and `TargetPoint` objects with representative load-chart metadata, refresh the ViewModel, and assert generated options and workflow settings.

**Downstream impact**

- Prevents regressions where `Any` stops mapping to null constraints.
- Prevents regressions where mixed line/condition metadata loses the condition option.
- Prevents accidental workflow mapping breakage.

### 11.3 `CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CraneOptimizerDialogViewModel.cs`

**What changed**

Added:

- `LoadChartOperatingSetupOption` model;
- `LoadChartOperatingSetupOptions` collection;
- `SelectedLoadChartOperatingSetupOption`;
- `LoadChartOperatingSetupStatus`;
- `IsLoadChartOperatingSetupExpanded`;
- visibility/disclosure text/glyph properties;
- collapsed summary/subtext;
- slider value and slider maximum;
- scale label generation;
- selected setup title/tone;
- line-support and hook-speed intuition properties;
- `RefreshLoadChartOperatingSetupOptions(Crane? crane)`;
- helper methods for visual column identity, line formatting, status text, clamping, and slider sync.

**Why**

The ViewModel is the correct WPF/MVVM location for UI state, bindable options, and presentation-friendly summaries. It keeps the XAML declarative and avoids placing logic in WPF views.

**How**

The refresh method enumerates selected crane load-chart points and builds an ordered option list:

```csharp
var options = new List<LoadChartOperatingSetupOption>
{
    LoadChartOperatingSetupOption.Any
};
```

Line-count groups are generated from positive `NumberOfLines`. Condition groups are generated from all non-empty `OperatingConditionLabel` values.

**Key final hardening**

The initial implementation exposed condition options only for points without line-count metadata. The final implementation removed that exclusion so mixed metadata is represented correctly.

**Downstream impact**

- A selected crane with imported line metadata now provides setup choices.
- A selected crane without metadata safely reports fallback behavior.
- A selected crane with mixed metadata provides both line-count and condition choices.
- GA settings remain untouched until the user runs the optimizer.

### 11.4 `CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml`

**What changed**

Added a new `LOAD CHART OPERATING SETUP` panel between crane selection and results.

The panel includes:

- header and explanatory text;
- automatic/constrained badge;
- toggle button for details;
- collapsed summary and subtext;
- slider and scale labels;
- coverage summary;
- selected setup intuition card;
- line-support and hook-speed cards;
- safety note.

**Why**

The selector must be discoverable but not overwhelming. A collapsed default preserves UI compactness while still making the current operating mode visible.

**How**

The XAML binds to ViewModel properties. It does not compute option lists or capacity behavior.

**Downstream impact**

- F8 users can explicitly choose operating setup constraints.
- Default users see no required extra configuration.
- Future maintainers can update text/layout without changing resolver code.

### 11.5 `CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml.cs`

**What changed**

Added:

- a refresh call after loading cranes;
- a `CboCrane_SelectionChanged` handler that refreshes ViewModel options;
- `RefreshLoadChartOperatingSetupOptions(Crane? crane)` helper;
- selected setup constraints in `CaptureOptions()`.

**Why**

Code-behind already owns dialog control orchestration and option capture. This change keeps WPF event handling local while delegating option construction to the ViewModel.

**How**

The code-behind passes only the selected `Crane` into the ViewModel and later reads selected constraint values from the ViewModel when creating `CraneOptimizerDialogOptions`.

**Downstream impact**

- The selector updates when the user changes crane.
- The optimizer receives the selected setup only when the run options are captured.

### 11.6 `CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs`

**What changed**

`CraneOptimizerDialogOptions` gained:

```csharp
int? LoadChartNumberOfLinesConstraint,
string? LoadChartOperatingConditionConstraint
```

`BuildSettings(...)` now maps them into `GASettings`:

```csharp
LoadChartNumberOfLinesConstraint = options.LoadChartNumberOfLinesConstraint,
LoadChartOperatingConditionConstraint = NullIfWhiteSpace(options.LoadChartOperatingConditionConstraint)
```

Added helper:

```csharp
private static string? NullIfWhiteSpace(string? value)
```

**Why**

The workflow is the appropriate bridge between dialog input and application settings. It preserves layering by avoiding direct UI-to-engine calls.

**Downstream impact**

- The existing capacity resolver receives line/condition constraints through `GASettings`.
- Whitespace-only condition values become `null`.
- Default `Any` behavior remains unchanged.

### 11.7 `docs/domain/pdf-load-chart-import.md`

**What changed**

Added a section documenting downstream GA operating setup selection.

**Why**

The PDF importer preserves metadata, but it must not decide crane reeving or operating setup for optimization. The runtime choice belongs in F8.

**Downstream impact**

- Clarifies separation of responsibility between PDF import and GA runtime selection.
- Documents how metadata flows into `GASettings` and the resolver.

---

## 12. Subsystem-Level Impact Assessment

### 12.1 F8 Crane Position Optimizer UI

**Impact:** Direct.

The UI gains a new operating setup panel and dynamic selector behavior. This is the primary modified subsystem.

### 12.2 F8 ViewModel / MVVM State

**Impact:** Direct.

The ViewModel now owns selector state, option generation, slider synchronization, collapsed/expanded state, and presentation text.

### 12.3 Dialog Workflow / Application Settings

**Impact:** Direct.

The workflow maps selected constraints into `GASettings`.

### 12.4 Genetic Algorithm Engine

**Impact:** Indirect.

No GA algorithm code was changed. However, GA capacity lookup can now be constrained from the UI using existing settings. This may change optimization results when the user selects a specific setup.

### 12.5 `LoadChartCapacityResolver`

**Impact:** Indirect, no code changes.

The resolver already applies `LoadChartNumberOfLinesConstraint` and `LoadChartOperatingConditionConstraint`. This branch increases the likelihood those settings are intentionally populated by users.

### 12.6 PDF Load-Chart Import Engine

**Impact:** Documentation only.

No importer code changed. The importer remains responsible for preserving metadata, not choosing runtime setup.

### 12.7 Streamlit UI

**Impact:** None.

No Streamlit code, dashboards, or notebooks were modified. If Streamlit consumes optimizer outputs, it may indirectly observe different results when users choose constraints, but no Streamlit implementation changed.

### 12.8 Diagnostics

**Impact:** Indirect.

No diagnostics schema was changed. However, constrained GA runs can influence diagnostics because selected setup constraints affect capacity resolution and liftability outcomes.

### 12.9 Verification Logic

**Impact:** Direct.

Focused test coverage was significantly expanded. Full solution tests were also run after cleanup/hardening.

### 12.10 Installers and Packaging

**Impact:** None.

No installer scripts, package manifests, deployment scripts, or release pipelines were modified.

### 12.11 Outputs Pipeline

**Impact:** Indirect.

No output format changed. Results may differ when users intentionally constrain capacity lookup. This is expected runtime behavior, not an output pipeline schema change.

### 12.12 Runtime Configuration

**Impact:** Direct through `GASettings` values only.

No config files changed. Runtime behavior changes only when dialog options produce non-null constraints.

### 12.13 Database and Persistence

**Impact:** None.

No schema changes, migrations, repositories, or persistence DTOs were modified.

### 12.14 Dependency Management

**Impact:** None.

No NuGet packages or external libraries were added or changed.

---

## 13. Genetic Algorithm and Capacity Resolution Impact

### 13.1 What Changes in GA Behavior

The GA search algorithm remains the same. The branch changes the settings used during capacity lookup when the user selects a constraint.

Potential runtime effect:

| User selection | Capacity lookup behavior |
|---|---|
| `Any` | Existing behavior; all eligible chart entries remain available. |
| `4 lines` | Resolver may filter to chart entries with `NumberOfLines == 4`. |
| `Condition: On outriggers 360 degrees` | Resolver may filter to entries matching the condition label. |

### 13.2 What Does Not Change

The branch does not change:

- population generation;
- mutation;
- crossover;
- scoring weights;
- crane geometry;
- radius calculations;
- load extraction;
- resolver fallback semantics;
- manufacturer capacity values.

### 13.3 Why This Is Correct

The UI choice belongs upstream of `GASettings`, not inside the resolver. The resolver should remain a deterministic capacity lookup component based on settings and chart entries.

---

## 14. PDF Load-Chart Import and Provenance Interaction

### 14.1 Relationship to PDF Import Robustness Work

The PDF importer and review workflow make load-chart data available. This branch consumes that data but does not modify extraction.

### 14.2 Relationship to Provenance Diagnostics Work

Prior provenance work preserved and surfaced metadata such as:

- number of lines;
- operating condition;
- visual column identity;
- source/load-chart context.

This branch converts that preserved metadata into a runtime user decision in the F8 optimizer dialog.

### 14.3 Responsibility Boundary

The PDF importer must not decide which operating setup the optimizer should use. This branch explicitly documents and implements that the user-facing runtime choice belongs in F8.

---

## 15. UI, MVVM, Workflow, and Layering Review

### 15.1 UI Layer

The XAML is declarative and binds to ViewModel properties. It does not scan crane data, build options, or compute constraints.

### 15.2 ViewModel Layer

The ViewModel owns UI state and presentation semantics. It does contain a significant amount of UI text and option generation logic. This is acceptable for the current branch because the logic is presentation-oriented and does not duplicate resolver capacity algorithms.

### 15.3 Code-Behind Layer

The code-behind performs orchestration:

- refresh ViewModel when selected crane changes;
- capture selected option into dialog options.

It does not implement capacity lookup or GA business logic.

### 15.4 Workflow Layer

The workflow maps string/nullable dialog inputs into typed `GASettings` values. This preserves established settings construction boundaries.

### 15.5 Domain and Infrastructure Layers

No changes.

---

## 16. Diagnostics, Outputs, Reproducibility, and Artifact Hygiene

### 16.1 Diagnostics

No diagnostics schema changed. However, selected setup constraints affect capacity resolution and therefore can indirectly affect diagnostics results.

### 16.2 Outputs

No output pipeline format changed. Optimizer results can differ when constrained settings are selected.

### 16.3 Reproducibility

The branch improves reproducibility by making operating setup explicit in runtime configuration rather than implicit or unavailable.

Reproducibility implications:

- users can intentionally rerun with the same crane and selected setup;
- tests verify default and constrained settings mapping;
- final documentation records validation commands;
- `.gitignore` prevents local test artifacts from polluting source diffs.

### 16.4 Artifact Hygiene

The final branch ignores:

- `**/TestResults/`;
- `*.trx`;
- `docs.zip`.

This prevents generated validation artifacts from being accidentally committed.

---

## 17. Validation and Reproducibility Record

### 17.1 Final Focused Validation

Command class:

```powershell
dotnet test CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj `
  --no-build `
  --configuration Debug `
  --filter "FullyQualifiedName~CraneOptimizerDialogViewModelTests"
```

Observed result:

```text
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13
```

### 17.2 Full Solution Validation

Command class:

```powershell
dotnet test CraneOptimizer.sln --no-build --configuration Debug
```

Observed result from cleanup validation:

```text
Passed! - Failed: 0, Passed: 396, Skipped: 1, Total: 397
```

### 17.3 Restore and Build

Observed final build result:

```text
Build succeeded.
12 Warning(s)
0 Error(s)
```

The earlier full cleanup validation also showed:

```text
236 Warning(s)
0 Error(s)
```

The warning count varies based on command context and projects included, but no errors were observed.

### 17.4 Known Warnings

Two categories of warnings appeared:

1. LF/CRLF Git warnings for text files.
2. `NU1701` warnings for Xbim packages restored using .NET Framework target assets against `net8.0-windows7.0` projects.

These warnings were pre-existing/environmental and did not represent branch-specific build failures.

---

## 18. Regression-Prevention Strategy

### 18.1 Default Behavior Regression

Tests assert that `Any` maps to null constraints. This prevents accidental behavior changes where the optimizer becomes constrained by default.

### 18.2 Workflow Mapping Regression

Tests assert that selected line count and operating condition are correctly mapped into `GASettings`.

### 18.3 Mixed Metadata Regression

Final hardening added coverage for chart points that have both `NumberOfLines` and `OperatingConditionLabel`. This prevents future changes from hiding condition labels when line metadata is present.

### 18.4 UI State Regression

Tests cover collapsed/expanded state and selected constraint preservation when the panel is collapsed.

### 18.5 Encoding/Tooltip Regression

Prior-chat tests checked that scale labels did not contain mojibake-like characters such as `\u00e2` or `\u251c`, protecting against bad encoded dash/box glyphs in slider labels.

---

## 19. Technical Tradeoffs and Design Decisions

### 19.1 New Dossier vs. Updating Older Dossier

Decision: create a new dossier.

Rationale:

- older dossiers describe different branches;
- this branch is downstream runtime selection, not import/provenance infrastructure;
- a new file avoids corrupting historical documentation;
- cross-references preserve context.

### 19.2 ViewModel Option Generation

Decision: generate options in the F8 ViewModel.

Rationale:

- option construction is UI-specific;
- no resolver algorithm duplication is required;
- code remains close to binding state;
- no DI/service changes needed.

Tradeoff:

- ViewModel grows larger. If more setup dimensions are added, extraction into a small helper/service may be appropriate.

### 19.3 Slider Rather Than ComboBox

Decision: final UI uses a discrete slider and labels rather than only a plain dropdown.

Rationale:

- line-count choices have an intuitive ordering;
- the UI can communicate fewer/more lines and hook-speed tradeoffs;
- selected setup can be summarized visually.

Tradeoff:

- many operating-condition labels may crowd the slider label row.

### 19.4 Collapsed by Default

Decision: panel is collapsed by default.

Rationale:

- default optimizer behavior remains simple;
- advanced load-chart setup selection does not overwhelm basic users;
- badge and summary still expose active mode.

### 19.5 Safety Text Included in UI

Decision: include explicit engineering safety note.

Rationale:

- line count should not be mistaken for certified capacity calculation;
- manufacturer load-chart data remains authoritative;
- UI must avoid unsafe engineering interpretation.

---

## 20. Known Limitations and Future Work

### 20.1 Many Conditions May Crowded Slider Labels

If a crane has many unique operating conditions, the slider label row may become crowded. Future UX options:

- switch to combo/list when option count exceeds threshold;
- show selected label only and use a dropdown for detailed selection;
- split line-count and operating-condition constraints into two controls.

### 20.2 Single Constraint Dimension at a Time

The current option model can represent a line-count option or an operating-condition option, but not a combined option requiring both simultaneously from one user selection.

Future enhancement could support compound options:

```text
4 lines + On outriggers 360 degrees
```

### 20.3 Manual UI Smoke Test Still Valuable

Automated tests validate ViewModel/workflow behavior. A manual WPF smoke test should still confirm visual layout, toggle behavior, and slider readability with real crane data.

### 20.4 ViewModel Size

The ViewModel is larger after this branch. This is acceptable now but should be revisited if additional setup semantics are added.

---

## 21. Risk Assessment

| Risk | Severity | Mitigation |
|---|---:|---|
| Default optimizer behavior accidentally changes | High | `Any` maps to null constraints; tests assert this. |
| UI implies certified capacity calculation | High | Safety note states manufacturer chart capacity remains authoritative. |
| Operating-condition metadata hidden for mixed metadata points | Medium | Final hardening generates condition options from all labels and adds regression test. |
| Crowded slider labels with many options | Medium | Documented as future UX improvement. |
| ViewModel grows too large | Low/Medium | Future extraction recommended if feature expands. |
| Generated artifacts accidentally committed | Low | `.gitignore` updated and final status cleaned. |
| Existing Xbim package warnings misunderstood as branch failures | Low | Documented as warnings, not errors. |

---

## 22. Maintainer Review Checklist

Before merging or committing, reviewers should confirm:

- [ ] Branch is still based on intended `div` lineage.
- [ ] Only expected files are modified.
- [ ] No local review bundles or test artifacts are staged.
- [ ] `Any` remains the default operating setup.
- [ ] `Any` maps to null GA constraints.
- [ ] Line-count selection maps to `LoadChartNumberOfLinesConstraint`.
- [ ] Condition selection maps to trimmed `LoadChartOperatingConditionConstraint`.
- [ ] Mixed metadata test exists and passes.
- [ ] XAML panel is visually acceptable.
- [ ] Safety wording is retained.
- [ ] Focused tests pass.
- [ ] Full solution tests have passed or are intentionally deferred with documented rationale.

---

## Appendix A — Changed File Summary

| File | Layer | Change type | Directness |
|---|---|---|---|
| `.gitignore` | Repository hygiene | Ignore test artifacts and uploaded docs bundle | Direct |
| `CraneOptimizerDialogViewModelTests.cs` | Tests | Added selector/workflow/hardening tests | Direct |
| `CraneOptimizerDialogViewModel.cs` | UI ViewModel | Added operating setup option state, slider state, intuition text, metadata scanning | Direct |
| `CraneOptimizerDialog.xaml` | WPF UI | Added collapsible selector panel | Direct |
| `CraneOptimizerDialog.xaml.cs` | UI orchestration | Refresh options and capture selected constraints | Direct |
| `CraneOptimizerDialogWorkflow.cs` | Application workflow | Map selected constraints into `GASettings` | Direct |
| `docs/domain/pdf-load-chart-import.md` | Documentation | Document downstream GA setup selection responsibility | Direct |

---

## Appendix B — Implementation Mapping

| Requirement | Implemented by |
|---|---|
| Preserve default behavior | `LoadChartOperatingSetupOption.Any`, workflow null mapping, tests |
| Dynamic line-count choices | `RefreshLoadChartOperatingSetupOptions(...)` line grouping |
| Dynamic condition choices | `RefreshLoadChartOperatingSetupOptions(...)` condition grouping |
| Mixed metadata robustness | Removed `!NumberOfLines.HasValue` exclusion; mixed metadata test |
| Collapsible UI | XAML panel + `IsLoadChartOperatingSetupExpanded` |
| Slider selection | `SelectedLoadChartOperatingSetupSliderValue`, `OperatingSetupSliderMaximum`, scale labels |
| Engineering intuition | ViewModel text/percentage properties + XAML cards |
| Safe capacity framing | `OperatingSetupSafetyNote` |
| Workflow mapping | `CraneOptimizerDialogWorkflow.BuildSettings(...)` |
| Artifact hygiene | `.gitignore` changes |
| Documentation | `docs/domain/pdf-load-chart-import.md` and this dossier |

---

## Appendix C — Validation Commands

Recommended final validation commands:

```powershell
cd "$env:USERPROFILE\source\repos\CranePathOptimizer"

git status --short --branch
git diff --check

dotnet restore .\CraneOptimizer.sln
dotnet build .\CraneOptimizer.sln --configuration Debug --no-restore

dotnet test .\CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj `
  --configuration Debug `
  --no-build `
  --filter "FullyQualifiedName~CraneOptimizerDialogViewModelTests"

# Optional full-suite validation before merge:
dotnet test .\CraneOptimizer.sln --configuration Debug --no-build
```

---

## Appendix D — Evidence Artifacts

| Artifact | Status |
|---|---|
| Prior chat transcript | Incorporated |
| Prior SAFE review package | Incorporated |
| Current V5 forensic package | Incorporated |
| Code changes review package | Incorporated |
| Cleanup V4 validation logs | Incorporated |
| Final post-patch review package | Incorporated |
| Older related dossiers | Cross-referenced |
| Project docs package | Consulted |

---

## Appendix E — Documentation References and Related Dossiers

### E.1 Related Project Dossiers

- `docs/implementation/load-chart-provenance-diagnostics.md`
- `docs/implementation/pdf-load-chart-import-robustness-dossier.md`
- `docs/domain/pdf-load-chart-import.md`

### E.2 External Documentation Practice References

This dossier was structured using practices consistent with professional design documents, architecture decision records, and reproducibility-oriented computational engineering reports. The themes applied include:

- design context before implementation details;
- explicit decision rationale and consequences;
- file-by-file impact ledgers;
- validation and reproducibility commands;
- known limitations and future work;
- treating documentation as part of the engineering workflow.

---

## Appendix F — Manual Smoke-Test Protocol

Recommended WPF smoke test before final merge:

1. Launch the WPF application.
2. Open the F8 Crane Position Optimizer dialog.
3. Select a crane with imported load-chart metadata.
4. Confirm `LOAD CHART OPERATING SETUP` appears collapsed by default.
5. Confirm badge reads `Automatic` under default state.
6. Expand details.
7. Confirm slider shows `Any` and imported setup stops.
8. Select a line-count stop.
9. Confirm badge changes to `Constrained`.
10. Confirm collapsed summary preserves selected constraint when details are hidden.
11. Run optimizer.
12. Inspect results/diagnostics to confirm no crash and expected constrained behavior.
13. Repeat with `Any` to confirm default behavior remains available.

---

## Final Statement

This branch completes the missing runtime bridge between imported load-chart operating metadata and GA capacity lookup settings. It does so without modifying PDF extraction, persistence schema, resolver algorithms, GA evolution logic, diagnostics schema, Streamlit UI, installers, or dependencies.

The feature is validated by focused and full test evidence, preserves default behavior, exposes constraints through a professional WPF/MVVM panel, and documents downstream responsibility clearly. Remaining work is limited to optional UI scalability improvements for very large numbers of operating-condition labels and future refactoring if the ViewModel grows further.
