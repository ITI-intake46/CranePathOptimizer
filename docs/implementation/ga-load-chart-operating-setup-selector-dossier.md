# CranePathOptimizer Branch Implementation Dossier

## `temp/div-implementation` — F8 Load Chart Operating Setup, Inclusive Line-Count Eligibility, Usable-Capacity Coverage, and UI Robustness

**Repository:** `ITI-intake46/CranePathOptimizer`  
**Working branch:** `temp/div-implementation`  
**Recommended repository path:** `docs/implementation/ga-load-chart-operating-setup-selector-dossier.md`  
**Document status:** Branch dossier / implementation report / architecture-impact record / reproducibility record  
**Generated:** 2026-06-15  
**Primary subsystem:** `CraneOptimizer.UI/Features/F8_GAOptimizer`  
**Primary upstream subsystem:** load-chart import and provenance metadata  
**Primary downstream subsystem:** genetic algorithm load-chart capacity resolution  
**Validation status represented:** `dotnet restore`, `dotnet build`, and `dotnet test` succeeded after final UI-polish patch  
**Final automated validation observed:** `407` tests total, `406` succeeded, `1` skipped, `0` failed  
**Commit status:** not committed at the time this document was prepared; stage only the intended tracked files and exclude local backup artifacts.

---

## Documentation strategy decision

This document is intended to **replace and supersede the older draft of**:

```text
docs/implementation/ga-load-chart-operating-setup-selector-dossier.md
```

The two other uploaded documents should remain as historical and upstream subsystem context, not be overwritten:

```text
docs/implementation/load-chart-provenance-diagnostics.md
docs/implementation/pdf-load-chart-import-robustness-dossier.md
```

The reason for this recommendation is consistency. The older operating-setup dossier describes an earlier implementation phase and does not fully capture the final branch behavior: inclusive line-count eligibility, dynamic `Any` coverage, usable positive capacity counts, synthetic-label suppression, scrollable details, and final UI polish. Keeping two active operating-setup dossiers would create contradictory maintenance signals. Replacing the operating-setup dossier with this file provides one authoritative source for the current branch while preserving the PDF-import and provenance dossiers as upstream historical design records.

---

## Table of contents

1. [Purpose and audience](#1-purpose-and-audience)
2. [Evidence basis](#2-evidence-basis)
3. [Documentation methodology](#3-documentation-methodology)
4. [Executive summary](#4-executive-summary)
5. [Problem statement](#5-problem-statement)
6. [Scope, non-scope, and branch boundaries](#6-scope-non-scope-and-branch-boundaries)
7. [Architecture context](#7-architecture-context)
8. [Before-and-after behavior](#8-before-and-after-behavior)
9. [End-to-end runtime flow](#9-end-to-end-runtime-flow)
10. [Core engineering semantics](#10-core-engineering-semantics)
11. [Feature implementation details](#11-feature-implementation-details)
12. [File-by-file implementation ledger](#12-file-by-file-implementation-ledger)
13. [Subsystem impact assessment](#13-subsystem-impact-assessment)
14. [Genetic algorithm and resolver impact](#14-genetic-algorithm-and-resolver-impact)
15. [PDF import and provenance interaction](#15-pdf-import-and-provenance-interaction)
16. [Diagnostics, outputs, and reproducibility](#16-diagnostics-outputs-and-reproducibility)
17. [Validation record](#17-validation-record)
18. [Backward compatibility and migration behavior](#18-backward-compatibility-and-migration-behavior)
19. [Regression-prevention strategy](#19-regression-prevention-strategy)
20. [Technical decisions and tradeoffs](#20-technical-decisions-and-tradeoffs)
21. [Risks, limitations, and future work](#21-risks-limitations-and-future-work)
22. [Maintainer and reviewer checklist](#22-maintainer-and-reviewer-checklist)
23. [Commit and staging guidance](#23-commit-and-staging-guidance)
24. [Appendices](#24-appendices)

---

## 1. Purpose and audience

This document is a branch-level engineering dossier for the implementation work performed on `temp/div-implementation` around the **F8 Crane Position Optimizer** and its **Load Chart Operating Setup** selector.

It is written for:

- maintainers reviewing or merging the branch;
- future contributors who need to understand why line-count selection behaves as a maximum eligibility constraint;
- UI engineers modifying the F8 optimizer dialog;
- GA engineers maintaining load-chart capacity resolution;
- researchers using GA diagnostics or load-chart outputs for audit or validation;
- project maintainers reconciling PDF import, load-chart provenance, and runtime optimizer behavior.

This document is intentionally more detailed than release notes. It records design rationale, boundaries, implementation effects, validation evidence, and future maintenance guidance.

---

## 2. Evidence basis

### 2.1 Supplied project evidence

This document is based on the following project-specific evidence:

| Evidence source | Role in this document |
|---|---|
| Current branch state and patch outputs | Establishes the final validated implementation state. |
| Final PowerShell output from `Apply-OperatingSetupUiPolishV2Patch-v4.ps1` | Confirms successful XAML patch, docs patch, restore, build, and test. |
| Uploaded older `ga-load-chart-operating-setup-selector-dossier.md` | Historical operating-setup implementation plan and earlier dossier. |
| Uploaded `load-chart-provenance-diagnostics.md` | Historical provenance pipeline context. |
| Uploaded `pdf-load-chart-import-robustness-dossier.md` | Historical PDF import robustness context. |
| UI screenshots after patching | Confirms final user-facing behavior and layout. |
| Prior forensic/source-review evidence from this branch | Establishes touched files and architecture boundaries. |

### 2.2 Validation evidence

The final validation evidence showed the following successful sequence:

```text
Apply-OperatingSetupUiPolishV2Patch-v4.ps1
Repository located
Current branch: temp/div-implementation
XAML baseline XML-well-formed
Backups written
XAML patched
Documentation patched
dotnet restore succeeded
dotnet build succeeded
dotnet test succeeded
Test summary: total: 407, failed: 0, succeeded: 406, skipped: 1
```

### 2.3 Evidence confidence

The implementation is considered validated because:

1. the final XAML was XML-well-formed before and after patching;
2. the WPF UI project compiled successfully;
3. the application test suite executed successfully with `0` failed tests;
4. manual screenshots show the intended F8 UI behavior;
5. no schema migration, package change, installer change, or persistence change was introduced by this branch work.

---

## 3. Documentation methodology

This dossier follows a hybrid documentation method:

- **Architecture decision record discipline:** major choices are documented with context, decision, alternatives, and consequences.
- **Change-log discipline:** changes are curated by meaning and impact rather than being a raw Git diff.
- **Technical reference discipline:** affected files, functions, properties, workflows, and subsystem boundaries are enumerated explicitly.
- **Reproducibility discipline:** validation commands, observed outcomes, warnings, and manual smoke-test procedures are included.
- **Explanation discipline:** domain semantics are explained so future contributors understand not only what changed, but why it is correct.

This structure intentionally separates:

| Documentation need | Covered by |
|---|---|
| “What changed?” | Executive summary, feature details, file ledger |
| “Why was it changed?” | Problem statement, technical decisions, rationale |
| “How does it work?” | Runtime flow, core semantics, implementation details |
| “How do I verify it?” | Validation record, smoke-test protocol |
| “What might break?” | Impact assessment, risks, regression prevention |
| “What should I not touch?” | Non-scope, boundaries, staging guidance |

External documentation practices used as inspiration include Diátaxis for separating explanation/reference/procedure user needs, ADR practice for recording decisions and consequences, Google developer documentation guidance for clear technical style, and Keep a Changelog’s emphasis on curated notable changes instead of raw logs.

---

## 4. Executive summary

### 4.1 Branch outcome

This branch completes and hardens the F8 **Load Chart Operating Setup** workflow. It allows the optimizer user to select a load-chart operating setup constraint derived from imported load-chart metadata while preserving the previous automatic/default behavior.

The final implementation provides:

- dynamic `Any` option coverage;
- inclusive line-count eligibility;
- usable-capacity coverage summaries;
- suppression of synthetic PDF provenance labels as operating-condition choices;
- engineering-intuition text for the selected setup;
- scrollable and compact expanded UI details;
- documentation updates and validation coverage.

### 4.2 Primary behavior implemented

When a crane is selected in the F8 optimizer dialog, the ViewModel scans the crane’s imported load-chart entries and constructs a set of operating-setup options.

The options are:

| Option type | Meaning |
|---|---|
| `Any` | No line-count or operating-condition constraint. The GA may use every eligible imported capacity value. |
| `Up to N lines` | The GA may use imported capacity values whose load-chart entry requires `N` or fewer parts of line. |
| True operating condition | The GA may constrain lookup to a real operating-condition label when such a label is present and actionable. |

The selector intentionally does **not** expose labels such as `39.1 / 4 lines` as operating conditions. Those labels are useful provenance text, not true user-facing operating-condition choices.

### 4.3 Primary engineering correction

The central modeling correction is that selecting a line count is not an exact-column-only filter. Instead, it is a **maximum setup eligibility constraint**:

```text
Selected setup: up to N lines
Eligible entries: all load-chart entries requiring <= N lines
```

This better reflects the operational meaning of choosing an available line-support setup. If a setup supports up to `5` lines, entries requiring `2`, `3`, `4`, or `5` lines remain eligible; entries requiring more than `5` lines do not.

### 4.4 Primary UI correction

The UI now shows useful coverage even for the default `Any` state:

```text
Uses 271 usable imported capacity values across 14 visual columns.
No line-count or operating-condition constraint is applied.
```

For constrained states, it shows the usable subset, for example:

```text
Allows capacity lookup to use 104 usable imported capacity values requiring up to 2 lines across 5 visual columns.
```

The displayed coverage count means **usable positive imported capacity values**, not every preserved exact-PDF grid cell.

### 4.5 Validation summary

The final branch state passed automated validation:

```text
dotnet restore
dotnet build CraneOptimizer.sln -c Debug --no-restore --no-incremental
dotnet test CraneOptimizer.sln -c Debug --no-build
```

Observed result:

```text
Test summary: total: 407, failed: 0, succeeded: 406, skipped: 1
Build succeeded
```

---

## 5. Problem statement

### 5.1 Original workflow gap

The PDF load-chart import pipeline preserved rich metadata from manufacturer charts, including:

- visual column index;
- visual column header;
- number of lines;
- operating-condition label.

However, the F8 GA optimizer initially had no robust, user-facing mechanism for selecting the intended load-chart operating setup at runtime.

Without this selector, the optimizer either used default load-chart behavior or relied on implicit metadata. This was insufficient for a workflow where imported manufacturer charts include different line-count columns and where the user needs to choose a setup constraint before running the optimizer.

### 5.2 Original UI ambiguity

The previous implementation path risked conflating two different concepts:

| Concept | Example | Correct role |
|---|---|---|
| Provenance / diagnostics label | `39.1 / 4 lines` | Explain which imported visual chart column produced a selected capacity. |
| User-facing operating setup | `Up to 4 lines` | Constrain runtime capacity lookup. |

Exposing every synthetic provenance label as a selectable operating setup created clutter and could mislead users into thinking boom-length labels were real operating-condition controls.

### 5.3 Original count ambiguity

The UI originally described the selected option in terms of “imported chart cells.” That wording was too broad because exact PDF import intentionally preserves blank and zero-capacity grid positions for audit/provenance. The selector needs to tell the user how many **usable positive capacity values** can participate in capacity lookup.

### 5.4 Original line-count semantics ambiguity

A strict equality model, such as “selected `4 lines` means exactly columns marked `4` lines,” would be too restrictive. The adopted model is inclusive:

```text
Selected up to 4 lines => entries requiring 2, 3, or 4 lines are eligible.
```

### 5.5 Original expanded UI density

The expanded **Load Chart Operating Setup** panel became visually large, repeated the same coverage text, and risked clipping or crowding detail text. The final implementation makes the details scrollable, removes duplicated coverage text, and keeps the Run / Cancel / Apply controls visible.

---

## 6. Scope, non-scope, and branch boundaries

### 6.1 In scope

This branch includes:

- F8 operating setup selector behavior;
- dynamic option generation from selected crane load-chart metadata;
- inclusive line-count eligibility;
- usable positive capacity-value coverage counts;
- default `Any` coverage summary;
- suppression of synthetic PDF provenance labels as operating-condition choices;
- ViewModel text and UI state behavior;
- XAML layout, scrollability, and compactness improvements;
- validation tests for ViewModel and resolver behavior;
- documentation updates.

### 6.2 Out of scope

This branch intentionally does **not** include:

- PDF extraction algorithm changes;
- PDF page/table-specific assumptions;
- database schema migrations;
- installer or packaging changes;
- dependency upgrades;
- Streamlit UI changes;
- GA algorithm redesign;
- crane physics simulation;
- certified capacity recalculation;
- replacement of manufacturer load-chart values.

### 6.3 Boundary rationale

The branch preserves project layering:

```text
Domain → Application → Infrastructure → UI
```

The UI constructs user-facing options. The application resolver applies capacity lookup constraints. The importer preserves manufacturer data. The branch does not move business logic into XAML or view code-behind.

---

## 7. Architecture context

### 7.1 Relevant architecture

The relevant system components are:

| Layer | Component | Responsibility |
|---|---|---|
| Domain | `TargetPoint` | Stores imported capacity entry metadata. |
| Application | `GASettings` | Carries runtime optimizer settings. |
| Application | `LoadChartCapacityResolver` | Resolves capacity under configured constraints. |
| UI / ViewModel | `CraneOptimizerDialogViewModel` | Builds user-facing setup options and UI text. |
| UI / XAML | `CraneOptimizerDialog.xaml` | Renders selector, details, and controls. |
| UI / Workflow | `CraneOptimizerDialogWorkflow` | Maps dialog selection into `GASettings`. |
| Tests | GA and ViewModel tests | Prevent regression in option construction and resolver semantics. |

### 7.2 Key metadata fields

The imported load-chart entry model includes:

```csharp
public int? VisualColumnIndex { get; set; }
public string? VisualColumnHeader { get; set; }
public int? NumberOfLines { get; set; }
public string? OperatingConditionLabel { get; set; }
```

These fields are used differently:

| Field | Runtime role |
|---|---|
| `VisualColumnIndex` | Visual provenance and visual-column count reporting. |
| `VisualColumnHeader` | Provenance; may also assist parsing fallback. |
| `NumberOfLines` | Primary input for line-count eligibility. |
| `OperatingConditionLabel` | True condition constraint only when actionable; otherwise provenance. |

### 7.3 Settings fields

The runtime constraint is carried by `GASettings`:

```csharp
public int? LoadChartNumberOfLinesConstraint { get; set; }
public string? LoadChartOperatingConditionConstraint { get; set; }
```

Interpretation:

| Field | `null` | Non-null |
|---|---|---|
| `LoadChartNumberOfLinesConstraint` | No line-count constraint | Maximum eligible line count. |
| `LoadChartOperatingConditionConstraint` | No condition constraint | Restrict lookup to matching condition. |

---

## 8. Before-and-after behavior

### 8.1 Before

Before this branch work:

- the F8 optimizer did not present a polished, robust setup selector;
- line-count metadata existed but was not consistently exposed as a runtime setup choice;
- synthetic provenance labels risked being displayed as selectable conditions;
- counts could imply preserved grid cells rather than usable capacity values;
- expanded details could become visually dense.

### 8.2 After

After this branch work:

- F8 displays a dedicated **Load Chart Operating Setup** card;
- `Any` remains the default and reports total usable imported capacity coverage;
- line-count options are generated dynamically from imported chart metadata;
- selecting `N` means “up to `N` lines”;
- coverage text counts only usable positive capacity values;
- true operating-condition labels remain available when present;
- synthetic labels derived from boom-header plus line-count metadata are suppressed;
- the details region is scrollable and compact;
- tests cover the critical semantics.

### 8.3 Representative final UI behavior

For the STC1300C imported data shown during smoke testing:

| Selection | Coverage shown |
|---|---|
| `Any` | `271` usable values across `14` visual columns |
| `Up to 2 lines` | `104` usable values across `5` visual columns |
| `Up to 3 lines` | `201` usable values across `10` visual columns |
| `Up to 4 lines` | `255` usable values across `13` visual columns |
| `Up to 5 lines` | `271` usable values across `14` visual columns |

These numbers are not hardcoded. They are computed from the selected crane’s imported load-chart entries.

---

## 9. End-to-end runtime flow

```text
User opens F8 Crane Position Optimizer
        ↓
User selects a crane
        ↓
ViewModel scans selected crane load-chart entries
        ↓
ViewModel builds Load Chart Operating Setup options
        ↓
User keeps Any or selects Up to N lines / true operating condition
        ↓
Dialog captures selected option
        ↓
Workflow maps selection into GASettings
        ↓
LoadChartCapacityResolver filters eligible load-chart entries
        ↓
GA optimization uses imported manufacturer capacities under selected constraint
        ↓
Diagnostics and UI outputs report selected capacity/provenance
```

### 9.1 Crane selection

The selected crane is the input source for option construction. This branch does not create global options or use static manufacturer assumptions.

### 9.2 Option generation

The ViewModel scans the selected crane’s load chart and generates:

1. default `Any`;
2. line-count options for positive `NumberOfLines` values;
3. true operating-condition options from actionable labels.

### 9.3 Option capture

The selected ViewModel option carries:

```text
NumberOfLinesConstraint
OperatingConditionConstraint
IsDefault
Description
```

### 9.4 Settings mapping

The workflow maps the selected option into `GASettings`.

### 9.5 Resolver application

The resolver applies the settings when selecting eligible load-chart entries. The resolver remains the authority for final liftability.

---

## 10. Core engineering semantics

### 10.1 `Any` means unconstrained, not unknown

`Any` means no user-imposed line-count or condition constraint. It does not mean the system lacks metadata. The UI now reports the total usable capacity coverage available under the unconstrained state.

### 10.2 Line count is maximum eligibility

The selected line count is a maximum setup eligibility constraint:

```text
Constraint = 4
Eligible entries = entries requiring 1, 2, 3, or 4 lines
Ineligible entries = entries requiring 5+ lines
```

The UI intentionally labels these options as `Up to N lines`.

### 10.3 Capacity values remain manufacturer values

The implementation does not calculate a new crane capacity from line count. It still uses the imported manufacturer load-chart capacity values.

Line count controls eligibility; it does not certify, scale, or synthesize capacity.

### 10.4 Usable capacity value definition

A usable capacity value is a capacity value that is:

```text
> 0
not NaN
not infinite
```

Blank, zero, negative, `NaN`, and infinite values are not counted as usable coverage.

### 10.5 Visual column count definition

Visual column coverage is computed from imported visual-column identity. Where possible, visual column identity uses visual provenance fields so repeated boom headers remain distinguishable.

### 10.6 Provenance label vs. operating condition

Synthetic provenance labels remain important for diagnostics and audit, but they are not automatically user-facing operating setup choices.

Example:

```text
39.1 / 4 lines
```

This is useful to describe which PDF visual column produced a capacity entry. It is not necessarily a real operating condition.

---

## 11. Feature implementation details

### 11.1 Dynamic `Any` option

The `Any` option is now generated from actual load-chart coverage:

```text
Uses 271 usable imported capacity values across 14 visual columns.
No line-count or operating-condition constraint is applied.
```

The default option remains safe and backward compatible because it leaves both constraints unset.

### 11.2 Inclusive line-count options

Line-count options are generated from the set of positive imported `NumberOfLines` metadata values.

For each option:

```text
option.NumberOfLinesConstraint = N
option label = "Up to N lines"
eligible entries = usable entries with NumberOfLines <= N
```

### 11.3 True operating-condition options

True operating-condition labels can still be exposed when they represent meaningful setup labels. Synthetic numeric boom-header labels are suppressed.

This prevents crowded or misleading selector choices.

### 11.4 Usable coverage descriptions

Coverage descriptions use precise wording:

```text
usable imported capacity value(s)
visual column(s)
Blank/zero preserved chart cells remain non-liftable.
```

The implementation includes singular/plural formatting so the UI does not display awkward `(s)` wording.

### 11.5 Engineering-intuition details

The expanded details show intuition about:

- line-support index;
- hook-speed intuition;
- selected setup effect;
- safety note that line count is an eligibility constraint, not a certified capacity calculation.

### 11.6 Scrollable details

The expanded details region is scrollable, and Run / Cancel / Apply remain outside the scrollable content. This prevents explanatory content from pushing primary action controls out of view.

### 11.7 Final UI polish

The final UI polish pass:

- removed duplicate coverage text inside expanded details;
- kept the compact summary at the top;
- made the detail scroll region non-focusable;
- removed or reduced the dotted scroll focus outline;
- compacted detail rows after removing the duplicate summary;
- consolidated duplicated documentation sections.

---

## 12. File-by-file implementation ledger

### 12.1 `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs`

#### What changed

The load-chart setup settings were clarified and/or updated to represent:

```csharp
public int? LoadChartNumberOfLinesConstraint { get; set; }
public string? LoadChartOperatingConditionConstraint { get; set; }
```

#### Why it changed

The optimizer needs a stable settings contract for F8 to pass the selected setup constraint to the application layer.

#### Engineering meaning

`LoadChartNumberOfLinesConstraint` is a maximum setup eligibility constraint, not an exact-line-only selector.

#### Downstream impact

`LoadChartCapacityResolver` reads these values. GA engine behavior changes only when a constraint is selected.

---

### 12.2 `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs`

#### What changed

The resolver applies the line-count constraint as an inclusive eligibility rule:

```text
NumberOfLines <= LoadChartNumberOfLinesConstraint
```

It also preserves default behavior when no constraint is specified.

#### Why it changed

Exact-only line count filtering would incorrectly exclude capacity values requiring fewer parts of line than the selected maximum setup supports.

#### Problem solved

Selecting `Up to 5 lines` now includes usable 2-, 3-, 4-, and 5-line entries rather than only 5-line entries.

#### Downstream impact

The GA has a more realistic feasible set under user-selected line constraints.

#### Side effects

A less restrictive feasible set may change optimizer results compared with exact-only filtering. This is intentional.

---

### 12.3 `CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CraneOptimizerDialogViewModel.cs`

#### What changed

The ViewModel now:

- builds dynamic `Any` coverage from actual usable entries;
- builds line-count options dynamically;
- interprets line-count options as `Up to N lines`;
- counts usable positive capacity values;
- counts visual columns;
- suppresses synthetic numeric provenance labels as operating-condition options;
- exposes compact UI strings and engineering-intuition text;
- uses selected option descriptions for collapsed summary text;
- supports singular/plural formatting.

#### Why it changed

The ViewModel is the correct MVVM location for user-facing option construction and display text.

#### Problem solved

The UI now presents meaningful setup choices without exposing internal provenance labels or misleading cell counts.

#### Downstream impact

The user can select a setup constraint confidently. The selected option flows into `GASettings`.

---

### 12.4 `CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml`

#### What changed

The XAML now includes a polished **Load Chart Operating Setup** details layout:

- scrollable details area;
- non-focusable `ScrollViewer`;
- compacted row definitions;
- duplicate coverage summary removed;
- Run / Cancel / Apply kept visible.

Representative final behavior:

```text
Top compact summary: visible
Expanded details: scrollable
Duplicate coverage summary inside details: removed
Action buttons: fixed and visible
```

#### Why it changed

The expanded panel had become visually dense and could repeat information. The final layout is more maintainable and clearer.

#### Downstream impact

No application-layer behavior changes. This is UI-only.

---

### 12.5 `CraneOptimizer.Application.Tests/GA/CraneOptimizerDialogViewModelTests.cs`

#### What changed

Tests were added or updated to cover:

- dynamic `Any` option coverage;
- singular/plural formatting;
- zero/negative/non-finite capacity exclusion;
- line-count coverage descriptions;
- synthetic provenance label suppression;
- selected-option summaries;
- ViewModel state transitions.

#### Why it changed

This behavior is subtle and must not regress.

#### Downstream impact

Future UI/ViewModel edits that reintroduce misleading option labels or incorrect counts should fail tests.

---

### 12.6 `CraneOptimizer.Application.Tests/GA/LoadChartCapacityResolverTests.cs`

#### What changed

Resolver tests were added or updated to validate inclusive line-count eligibility.

#### Why it changed

The application layer must enforce the same semantics that the UI communicates.

#### Downstream impact

Prevents a mismatch where the UI says “up to N” but the resolver implements exact-only filtering.

---

### 12.7 `docs/domain/pdf-load-chart-import.md`

#### What changed

The domain documentation now clarifies:

- PDF import preserves exact visual provenance;
- F8 operating setup is a runtime selector;
- `Any` reports total usable coverage;
- constrained options report usable subset coverage;
- displayed counts are diagnostic and do not certify capacity;
- expanded details are scrollable.

#### Why it changed

Future contributors need a single source of truth for how import metadata relates to optimizer setup constraints.

---

### 12.8 Local patch scripts and backups

Several local `.ps1` patch scripts and `.chatgpt_patch_backups/` folders were created during implementation.

#### Commit status

These are local tooling artifacts and should not be staged unless intentionally preserved for an internal audit trail.

---

## 13. Subsystem impact assessment

### 13.1 F8 Crane Position Optimizer UI

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | New/polished setup selector behavior |
| User-visible | Yes |
| Risk | Low after validation |
| Notes | The panel now communicates setup choices and coverage. |

### 13.2 F8 ViewModel / MVVM state

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | Option construction and display logic |
| User-visible | Yes |
| Risk | Medium due to semantic complexity |
| Mitigation | ViewModel tests added/updated. |

### 13.3 F8 dialog workflow

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | Selected setup option flows into `GASettings` |
| User-visible | Indirect |
| Risk | Low |
| Mitigation | Workflow mapping preserved and tested. |

### 13.4 Genetic Algorithm engine

| Impact type | Assessment |
|---|---|
| Direct algorithm change | No |
| Indirect behavior change | Yes, through capacity eligibility settings |
| Runtime effect | GA feasible set can change when user selects a line-count constraint |
| Risk | Controlled |
| Mitigation | Resolver tests and default `Any` behavior. |

The GA engine itself is not redesigned. The branch changes the input capacity set available to the GA through documented settings.

### 13.5 Load-chart capacity resolver

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | Inclusive line-count filtering |
| User-visible | Indirect |
| Risk | Medium |
| Mitigation | Unit tests for line-count semantics. |

### 13.6 PDF load-chart import engine

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Indirect impact | Yes, metadata consumed downstream |
| Runtime effect | Preserved `NumberOfLines` and provenance metadata become more valuable |
| Risk | Low |
| Mitigation | Importer responsibilities remain unchanged. |

### 13.7 Streamlit UI

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Indirect impact | None known |
| Required action | None |
| Notes | This branch is WPF/F8-focused. |

### 13.8 Diagnostics

| Impact type | Assessment |
|---|---|
| Direct impact | Minimal |
| Indirect impact | Selected resolver output remains tied to provenance |
| Required action | None |
| Notes | This branch preserves diagnostics semantics; it does not replace provenance reporting. |

### 13.9 Verification logic

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | New tests and updated expectations |
| Required action | Continue running full application test suite |
| Notes | Tests are the main regression guard. |

### 13.10 Installers and packaging

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Required action | None |
| Notes | No installer, packaging, or deployment scripts changed. |

### 13.11 Outputs pipeline

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Indirect impact | Optimizer results may differ under selected constraint |
| Required action | Ensure diagnostics remain consistent with selected setup |
| Notes | No CSV schema changes are introduced by this branch. |

### 13.12 Runtime configuration

| Impact type | Assessment |
|---|---|
| Direct impact | Yes |
| Nature | `GASettings` carries selected load-chart setup constraint |
| Persistence | No new persisted configuration introduced |
| Notes | Runtime options are selected per optimizer dialog run. |

### 13.13 Database and migrations

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Schema migration | No |
| Required action | None |
| Notes | Existing metadata fields are consumed; no schema additions are made. |

### 13.14 Dependency management

| Impact type | Assessment |
|---|---|
| Direct impact | No |
| Package changes | None |
| Warnings | Existing xBIM `NU1701` warnings remain |
| Notes | The branch does not upgrade packages. |

---

## 14. Genetic algorithm and resolver impact

### 14.1 What changes in GA behavior

When the user chooses a setup constraint, the GA capacity lookup can now be restricted by line-count eligibility.

Example:

```text
Selected: Up to 3 lines
Eligible load-chart entries: entries requiring 1, 2, or 3 lines
Excluded entries: entries requiring 4 or more lines
```

### 14.2 What does not change

The branch does not change:

- chromosome representation;
- selection, crossover, mutation, or fitness algorithm;
- placement geometry;
- crane swing radius logic;
- cost model;
- output schema;
- diagnostics file structure;
- imported manufacturer capacity values.

### 14.3 Why this is the correct modeling level

The selector constrains the **source capacity entries** before or during capacity resolution. It does not attempt to calculate crane mechanics or infer capacity from line count. This keeps the GA tied to imported manufacturer load-chart values.

### 14.4 Default behavior preserved

When `Any` is selected:

```text
LoadChartNumberOfLinesConstraint = null
LoadChartOperatingConditionConstraint = null
```

The optimizer remains unconstrained by setup selection.

---

## 15. PDF import and provenance interaction

### 15.1 Relationship to PDF import robustness work

The PDF import robustness work ensures manufacturer PDF load-chart tables are detected, ordered, merged when appropriate, previewed, and imported accurately. This branch does not alter that behavior.

### 15.2 Relationship to provenance diagnostics work

The provenance diagnostics work preserved metadata through runtime evaluation and diagnostics. This branch consumes that metadata for user-facing setup selection and coverage reporting.

### 15.3 Responsibility boundary

| Responsibility | Owner |
|---|---|
| Extract/load chart table values | PDF/CSV import services |
| Preserve exact visual provenance | Import and domain metadata |
| Present runtime setup choices | F8 ViewModel/UI |
| Apply setup constraint | `LoadChartCapacityResolver` |
| Report selected capacity/provenance | Diagnostics/output pipeline |

### 15.4 Contradiction prevention

Future contributors should not interpret this branch as changing PDF import semantics. Import should continue preserving exact visual chart structure and metadata. F8 should interpret metadata for runtime setup selection without modifying imported values.

---

## 16. Diagnostics, outputs, and reproducibility

### 16.1 Diagnostics

This branch does not redesign diagnostics. It preserves the separation between:

- selected runtime setup constraint;
- selected capacity value;
- selected provenance fields;
- load-chart resolver failure reasons.

### 16.2 Outputs

No intentional output schema changes are introduced. However, optimizer results can differ when a user selects a constraint because the eligible load-chart subset changes.

### 16.3 Reproducibility

A reproducible evaluation requires recording:

- selected crane;
- imported load-chart data;
- selected operating setup option;
- `GASettings` snapshot;
- random seed / GA run settings;
- solver version / branch commit.

### 16.4 Artifact hygiene

Do not commit local backup folders unless explicitly desired:

```text
.chatgpt_patch_backups/
```

Do not stage uploaded archive artifacts unless intentionally part of the repo:

```text
docs.rar
```

---

## 17. Validation record

### 17.1 Final automated validation

Observed final validation:

```text
dotnet restore CraneOptimizer.sln -v minimal
dotnet build CraneOptimizer.sln -c Debug --no-restore --no-incremental -v minimal
dotnet test CraneOptimizer.sln -c Debug --no-build -v normal
```

Final test result:

```text
Test summary: total: 407, failed: 0, succeeded: 406, skipped: 1
```

### 17.2 Validation coverage

| Area | Validation mechanism |
|---|---|
| XAML well-formedness | XML parse and WPF build |
| UI compile-time bindings | `CraneOptimizer.UI` build |
| ViewModel behavior | ViewModel unit tests |
| Resolver behavior | Resolver unit tests |
| Full solution integrity | `dotnet build` |
| Regression suite | `dotnet test` |
| Manual UI behavior | Screenshots and smoke testing |

### 17.3 Known warnings

Warnings observed during validation include:

- `NU1701` package compatibility warnings for xBIM packages;
- nullable reference warnings;
- unused event warnings in tests;
- Git LF/CRLF normalization warnings.

These warnings existed outside the central implementation and did not block validation.

---

## 18. Backward compatibility and migration behavior

### 18.1 Existing projects

Existing projects remain loadable because no database migration or schema change is introduced by this branch.

### 18.2 Existing load charts

Existing load charts without `NumberOfLines` metadata still operate under `Any`. If line-count options cannot be generated, the UI falls back to default behavior.

### 18.3 Existing GA behavior

`Any` preserves previous default behavior. Only explicit user selection constrains capacity lookup.

### 18.4 Existing diagnostics

Diagnostics continue to report imported capacity and provenance information. This branch does not remove diagnostic fields.

### 18.5 Existing PDF import

PDF import behavior remains intact. The branch consumes imported metadata but does not alter table detection or parsing.

---

## 19. Regression-prevention strategy

### 19.1 Default behavior regression

Guard: tests verify `Any` remains default and uses no constraints.

### 19.2 Line-count semantics regression

Guard: resolver tests verify `Up to N` behavior.

### 19.3 Coverage count regression

Guard: ViewModel tests verify usable positive capacity values are counted, while zero/negative/non-finite values are excluded.

### 19.4 Synthetic-label regression

Guard: ViewModel tests verify synthetic PDF provenance labels are suppressed from user-facing condition options.

### 19.5 UI layout regression

Guard: WPF build verifies XAML validity. Manual smoke testing verifies layout and scroll behavior.

### 19.6 Documentation regression

Guard: this dossier and `docs/domain/pdf-load-chart-import.md` state the current semantics explicitly.

---

## 20. Technical decisions and tradeoffs

### 20.1 Decision: update the operating-setup dossier rather than creating a separate parallel one

**Context:** Three related dossiers exist, but only the operating-setup dossier directly describes this branch feature.

**Decision:** Replace the older operating-setup dossier with this consolidated current document.

**Consequences:**

- avoids contradictory active docs;
- preserves upstream PDF/provenance docs as historical context;
- gives future contributors one authoritative current source.

### 20.2 Decision: line-count selection is inclusive

**Context:** Exact-only filtering is possible but too restrictive.

**Decision:** Model selected line count as maximum eligibility.

**Consequences:**

- user selecting `Up to 5 lines` can still use entries requiring fewer lines;
- feasible set is more realistic;
- tests must verify inclusive behavior.

### 20.3 Decision: suppress synthetic provenance labels from setup selector

**Context:** Labels like `39.1 / 4 lines` are derived from chart provenance, not necessarily real setup modes.

**Decision:** Do not expose synthetic numeric provenance labels as condition options.

**Consequences:**

- cleaner UI;
- reduced risk of misleading users;
- provenance still preserved for diagnostics.

### 20.4 Decision: count usable capacity values, not grid cells

**Context:** Exact visual imports preserve blank and zero grid cells.

**Decision:** UI coverage counts only positive finite capacity values.

**Consequences:**

- coverage text better reflects optimizer-relevant values;
- import audit/provenance remains intact;
- zero/blank cells remain non-liftable.

### 20.5 Decision: keep capacity certification out of UI intuition

**Context:** More lines imply mechanical support intuition, but capacity remains manufacturer-rated.

**Decision:** UI text emphasizes that line count is a setup eligibility constraint, not a standalone capacity calculation.

**Consequences:**

- avoids false engineering certification;
- keeps liability-sensitive semantics clear;
- preserves manufacturer chart as authority.

### 20.6 Decision: scrollable details with fixed action buttons

**Context:** Details can be text-heavy.

**Decision:** Details scroll; Run / Cancel / Apply stay fixed.

**Consequences:**

- better usability on smaller windows;
- actions remain visible;
- panel remains informative without overwhelming layout.

---

## 21. Risks, limitations, and future work

### 21.1 Remaining UI text density

The panel is much cleaner, but coverage text can still wrap on narrow windows. A future UI polish pass could separate coverage and safety text into structured short lines.

### 21.2 Manual smoke testing remains important

Automated tests validate behavior and build integrity, but manual UI smoke tests remain useful for WPF layout.

### 21.3 True operating-condition classification

Synthetic numeric labels are suppressed, but future cranes may introduce real textual setup conditions. The actionable-label classifier may need future expansion as more manufacturer chart formats are imported.

### 21.4 Documentation drift risk

Future changes to resolver semantics must update this dossier and `docs/domain/pdf-load-chart-import.md`.

### 21.5 Warnings remain

The branch does not resolve pre-existing nullable or xBIM package warnings. That is acceptable but should remain visible to maintainers.

---

## 22. Maintainer and reviewer checklist

### 22.1 Code review checklist

- [ ] Confirm `Any` leaves both load-chart constraints unset.
- [ ] Confirm line-count selection maps to maximum eligibility.
- [ ] Confirm resolver implements inclusive filtering.
- [ ] Confirm `NumberOfLines` metadata is not treated as a capacity multiplier.
- [ ] Confirm synthetic provenance labels are not exposed as condition options.
- [ ] Confirm true operating-condition labels remain supported.
- [ ] Confirm XAML remains MVVM-oriented and does not introduce business logic.
- [ ] Confirm Run / Cancel / Apply remain outside scrollable details.
- [ ] Confirm no schema migration was introduced.
- [ ] Confirm local backup artifacts are not staged.

### 22.2 Manual smoke-test checklist

1. Open the application.
2. Select the crane that has imported line-count metadata.
3. Open **F8 Crane Position Optimizer**.
4. Confirm `Any` is the default.
5. Confirm `Any` displays total usable imported capacity coverage.
6. Select `Up to 2 lines`, `Up to 3 lines`, `Up to 4 lines`, and `Up to 5 lines`.
7. Confirm each option displays the expected usable subset coverage.
8. Confirm the expanded details do not duplicate the same coverage summary.
9. Confirm details remain scrollable if content exceeds the region.
10. Confirm Run / Cancel / Apply remain visible.
11. Run the optimizer and verify no runtime exception occurs.
12. Confirm diagnostics remain coherent.

---

## 23. Commit and staging guidance

### 23.1 Intended tracked files

Stage only the intended tracked implementation files:

```bash
git add CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CraneOptimizerDialogViewModel.cs
git add CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml
git add CraneOptimizer.Application.Tests/GA/CraneOptimizerDialogViewModelTests.cs
git add CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs
git add CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs
git add CraneOptimizer.Application.Tests/GA/LoadChartCapacityResolverTests.cs
git add docs/domain/pdf-load-chart-import.md
git add docs/implementation/ga-load-chart-operating-setup-selector-dossier.md
```

### 23.2 Files not to stage unless intentional

Do not use `git add .`.

Avoid staging:

```text
.chatgpt_patch_backups/
docs.rar
```

### 23.3 Suggested commit message

```text
feat(ga): add robust load-chart operating setup selector

- add F8 load-chart operating setup selection with dynamic Any coverage
- interpret line-count setup as inclusive up-to-N eligibility
- count usable positive imported capacity values in UI coverage summaries
- suppress synthetic visual provenance labels from setup condition choices
- polish expanded setup details with scrollable compact layout
- update resolver/settings tests and load-chart documentation
```

---

## 24. Appendices

### Appendix A — Changed file summary

| File | Change category | Purpose |
|---|---|---|
| `GASettings.cs` | Settings contract | Carries load-chart setup constraints. |
| `LoadChartCapacityResolver.cs` | Resolver semantics | Applies inclusive line-count eligibility. |
| `CraneOptimizerDialogViewModel.cs` | UI logic | Builds options, counts coverage, formats descriptions. |
| `CraneOptimizerDialog.xaml` | UI layout | Renders compact scrollable operating setup card. |
| `CraneOptimizerDialogViewModelTests.cs` | Tests | Validates ViewModel behavior. |
| `LoadChartCapacityResolverTests.cs` | Tests | Validates resolver behavior. |
| `docs/domain/pdf-load-chart-import.md` | Documentation | Clarifies import/runtime setup boundaries. |
| `docs/implementation/ga-load-chart-operating-setup-selector-dossier.md` | Documentation | This authoritative branch dossier. |

### Appendix B — Implementation mapping

| Requirement | Implementation location |
|---|---|
| Default automatic setup | `LoadChartOperatingSetupOption.Any`, ViewModel refresh logic |
| Dynamic `Any` coverage | `CreateAnyOperatingSetupOption` and usable coverage helpers |
| Inclusive line count | `LoadChartCapacityResolver` |
| UI line-count options | `CraneOptimizerDialogViewModel` |
| Coverage counts | ViewModel usable-capacity helpers |
| Visual column counts | ViewModel visual-column identity helper |
| Scrollable details | `CraneOptimizerDialog.xaml` |
| Safety wording | ViewModel and XAML bindings |
| Regression tests | GA test project |

### Appendix C — Reproducibility commands

```powershell
cd "C:\Users\Bahyg\Source\Repos\CranePathOptimizer"
dotnet restore CraneOptimizer.sln -v minimal
dotnet build CraneOptimizer.sln -c Debug --no-restore --no-incremental -v minimal
dotnet test CraneOptimizer.sln -c Debug --no-build -v normal
git status --short --branch --untracked-files=all
```

### Appendix D — Related documentation

| Document | Relationship |
|---|---|
| `docs/implementation/pdf-load-chart-import-robustness-dossier.md` | Upstream PDF import context. |
| `docs/implementation/load-chart-provenance-diagnostics.md` | Provenance preservation and diagnostics context. |
| `docs/domain/pdf-load-chart-import.md` | Current domain reference for import/runtime setup boundary. |
| `docs/implementation/ga-load-chart-operating-setup-selector-dossier.md` | This current branch dossier. |

### Appendix E — External documentation references

- Diátaxis documentation framework: https://diataxis.fr/
- Google Developer Documentation Style Guide: https://developers.google.com/style
- Architectural Decision Records: https://adr.github.io/
- Keep a Changelog: https://keepachangelog.com/

### Appendix F — Glossary

| Term | Meaning |
|---|---|
| `Any` | Default setup option with no line-count or condition constraint. |
| Usable capacity value | Positive finite imported manufacturer capacity value. |
| Preserved grid cell | Exact visual PDF table cell retained for provenance/audit. |
| Number of lines | Imported hoist line / parts-of-line metadata. |
| Up to N lines | Runtime eligibility constraint allowing entries requiring no more than `N` lines. |
| Operating condition | Real imported condition label, such as a meaningful manufacturer setup condition. |
| Synthetic provenance label | Generated audit label such as `39.1 / 4 lines`; not a user setup condition. |
| Visual column | Imported load-chart visual column, distinct even when headers repeat. |

---

## Final statement

This branch establishes a robust, data-driven, and validated F8 Load Chart Operating Setup workflow. It preserves manufacturer load-chart authority, avoids hardcoded PDF/table assumptions, keeps provenance and runtime setup semantics separate, and gives the user a clear way to constrain GA capacity lookup by setup eligibility while preserving default unconstrained behavior.

The final implementation is validated by a successful restore/build/test run and should be committed with this dossier as the authoritative implementation record for future maintainers.
