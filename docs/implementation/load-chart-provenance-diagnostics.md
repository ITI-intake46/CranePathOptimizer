# CranePathOptimizer Branch Implementation Dossier  
## `feature/load-chart-provenance-diagnostics` — Load-Chart Provenance Preservation Through GA Diagnostics

**Repository:** `ITI-intake46/CranePathOptimizer`  
**Branch:** `feature/load-chart-provenance-diagnostics`  
**Base branch used for review:** `origin/enhancemnt_CSV_filesToStartModels`  
**Primary commit:** `be27178`  
**Pull request:** `#63` — *Preserve load chart provenance through GA diagnostics*  
**Branch status observed in supplied GitHub screenshots:** merged into `div` from `feature/load-chart-provenance-diagnostics`  
**Primary implementation theme:** preserve imported load-chart line-count / operating-condition provenance from import through optimizer decisions and diagnostics.  
**Document purpose:** engineering-grade implementation dossier, branch technical report, maintenance guide, and reproducibility record.  

---

## Table of Contents

1. [Document Control and Evidence Basis](#1-document-control-and-evidence-basis)  
2. [Executive Summary](#2-executive-summary)  
3. [Professional Documentation Methodology](#3-professional-documentation-methodology)  
4. [Problem Statement and Engineering Motivation](#4-problem-statement-and-engineering-motivation)  
5. [Scope, Non-Scope, and Branch Boundaries](#5-scope-non-scope-and-branch-boundaries)  
6. [Architecture Before and After the Branch](#6-architecture-before-and-after-the-branch)  
7. [End-to-End Provenance Flow](#7-end-to-end-provenance-flow)  
8. [Subsystem-Level Implementation Analysis](#8-subsystem-level-implementation-analysis)  
9. [File-by-File Change Ledger](#9-file-by-file-change-ledger)  
10. [Resolver Semantics and Algorithmic Behavior](#10-resolver-semantics-and-algorithmic-behavior)  
11. [Diagnostics and Output Contract Changes](#11-diagnostics-and-output-contract-changes)  
12. [Database, Migration, and Runtime Compatibility Notes](#12-database-migration-and-runtime-compatibility-notes)  
13. [Verification and Reproducibility Record](#13-verification-and-reproducibility-record)  
14. [Project-Wide Cross-Impact Assessment](#14-project-wide-cross-impact-assessment)  
15. [Backward Compatibility and Regression Prevention](#15-backward-compatibility-and-regression-prevention)  
16. [Performance and Reliability Considerations](#16-performance-and-reliability-considerations)  
17. [Known Limitations and Open Follow-Up Items](#17-known-limitations-and-open-follow-up-items)  
18. [Reviewer Guide](#18-reviewer-guide)  
19. [Appendix A — Changed Files by Layer](#appendix-a-changed-files-by-layer)  
20. [Appendix B — Evidence Artifacts Used](#appendix-b-terminology)  
21. [Appendix C — Terminology](#appendix-b-terminology)  
22. [Appendix D — Suggested Repository Placement](#appendix-c-suggested-repository-placement)  
23. [Appendix E — External Documentation Standards Consulted](#appendix-d-external-documentation-standards-consulted)  

---

## 1. Document Control and Evidence Basis

### 1.1 Purpose of This Document

This document records the technical intent, implementation details, architectural impact, verification evidence, and maintenance consequences of the `feature/load-chart-provenance-diagnostics` branch. It is written to be committed alongside the code so that future maintainers can understand *why* the branch exists, *what* it changed, *how* the changes propagate through the system, and *how* to validate or extend the behavior.

This is not a lightweight changelog. It is a branch-level implementation dossier intended for:

- senior maintainers reviewing architectural fit;
- contributors extending load-chart import or GA diagnostics;
- researchers relying on reproducible GA outputs;
- QA engineers validating runtime behavior;
- future developers troubleshooting migration or diagnostics issues;
- project owners deciding whether follow-up UI work is needed.

### 1.2 Repository State Captured by Evidence

The reviewed branch state was:

```text
Branch: feature/load-chart-provenance-diagnostics
Tracking: origin/feature/load-chart-provenance-diagnostics
Commit: be27178
Base for code review: origin/enhancemnt_CSV_filesToStartModels
Working tree: clean
```

The primary diff review reports:

```text
38 files changed, 1819 insertions(+), 133 deletions(-)
```

The fresh verification log reports successful restore, build, and tests:

```text
dotnet restore: exit code 0
dotnet build: exit code 0
dotnet test Application.Tests: exit code 0
Failed: 0
Passed: 352
Skipped: 1
Total: 353
```

---

## 2. Executive Summary

### 2.1 Branch Outcome

This branch preserves load-chart provenance through the import, persistence, optimizer, assignment, placement, and diagnostics pipeline. The central outcome is that GA outputs now expose the actual load-chart capacity source selected by the resolver, particularly when a numeric radius/boom pair maps to multiple visual columns representing different line counts or operating conditions.

Before this branch, capacity lookup could select a conservative capacity from duplicate load-chart entries, but the downstream diagnostics did not fully expose the selected visual column, line-count metadata, operating condition, or duplicate alternatives. That created ambiguity in GA reports and made it difficult to audit why the optimizer selected a capacity value.

After this branch, the selected load-chart entry carries provenance fields through the system:

```text
SelectedVisualColumnIndex
SelectedVisualColumnHeader
SelectedNumberOfLines
SelectedOperatingConditionLabel
IsConservativeDuplicateFallback
DuplicateAlternativeCount
DuplicateAlternativeSummary
```

### 2.2 Main Capabilities Added

The branch adds or completes the following capabilities:

1. **Imported load-chart metadata persistence**
   - `NumberOfLines` is preserved on catalog load-chart points.
   - `NumberOfLines` is preserved on created-project crane load-chart snapshots.
   - `OperatingConditionLabel` is persisted for catalog and created-project load-chart rows.
   - Visual column index/header provenance remains available.

2. **CSV load-chart metadata parsing**
   - Raw-grid CSV import detects `Number of lines` rows.
   - The row is treated as metadata, not capacity data.
   - Per-column line counts are assigned to generated load-chart points.

3. **PDF load-chart metadata extraction**
   - PDF import attempts to detect line-count rows from PDF word geometry.
   - Detected line-count values are mapped to visual load-chart column centers.
   - Exact visual grids preserve the metadata row when detected.

4. **Resolver-selected provenance propagation**
   - The resolver records the selected entry’s visual column, header, line count, operating label, duplicate status, and alternative summary.
   - Both normal and indexed resolver paths are covered by tests.

5. **Condition-aware resolver capability**
   - `GASettings.LoadChartNumberOfLinesConstraint` can constrain lookup to matching line-count entries.
   - `GASettings.LoadChartOperatingConditionConstraint` can constrain lookup to matching operating-condition entries.
   - If a requested condition cannot be matched, the resolver returns `LoadChartConditionMismatch` rather than inventing selected metadata.

6. **Diagnostics contract enhancement**
   - Summary-mode `ga_element_assignments.csv` gains selected provenance columns.
   - Research-mode detailed diagnostics gain selected provenance columns.
   - `ga_settings_snapshot.csv` records `DiagnosticsLevel`.
   - Summary-mode `ga_run_history.csv` no longer points to missing Research-only detailed diagnostics files.

7. **SQLite runtime compatibility**
   - A compatibility guard repairs runtime databases that already partially migrated but are missing `OperatingConditionLabel`.
   - The guard is wired into app startup and project workspace migration flows.

8. **Test and verification hardening**
   - Focused load-chart metadata tests were added.
   - Resolver tests validate duplicate-column metadata and condition mismatch behavior.
   - Diagnostics separation tests validate Summary versus Research behavior.
   - Hungarian large-dataset timing guardrail was stabilized against noisy Debug-build timing.

### 2.3 High-Level Engineering Judgment

The branch is architecturally coherent. It preserves the layered structure:

```text
UI / Presentation
→ DTOs and load-chart import services
→ Application / GA resolver and diagnostics
→ Domain entities
→ Infrastructure persistence and migrations
```

The core implementation avoids moving optimizer decision logic into WPF views. UI-facing load-chart code remains responsible for collecting/importing/displaying data; resolver and diagnostics logic remains in Application; persistence and compatibility repair remain in Infrastructure.

The most important remaining limitation is product exposure: the resolver supports `LoadChartNumberOfLinesConstraint` and `LoadChartOperatingConditionConstraint`, but a normal WPF user-facing selection workflow for these constraints is not evident in the reviewed branch. The capability exists programmatically and is tested at the resolver level; exposing it as a polished user workflow should be treated as follow-up work unless explicitly out of scope.

---

## 3. Professional Documentation Methodology

This dossier follows engineering documentation practices commonly used for design docs, architecture decision records, changelogs, reproducibility reports, and auditable software change records.

The structure intentionally combines:

- **design-document style goals and implementation strategy;**
- **ADR-style context, decision, tradeoffs, and consequences;**
- **human-readable changelog grouping by subsystem;**
- **reproducibility-oriented evidence and environment capture;**
- **branch-level file-by-file implementation ledger;**
- **cross-impact assessment and risk inventory.**

The resulting format is designed to make the branch reviewable both as code and as an engineering decision.

Key documentation principles applied:

| Principle | Applied In This Dossier |
|---|---|
| Human-readable change grouping | Executive summary, subsystem analysis, file-by-file ledger. |
| Context and consequences | Architecture, resolver semantics, backward compatibility, risks. |
| Reproducibility | Verification record, evidence artifacts, exact branch/base/commit. |
| Maintainability | Explicit ownership by layer and future-work boundaries. |
| Auditability | Changed files, why/where/how, diagnostic outputs, test evidence. |
| Research-grade provenance | Metadata lineage from import to GA outputs. |

---

## 4. Problem Statement and Engineering Motivation

### 4.1 Business and Engineering Problem

Crane load charts can contain multiple visual columns that share the same numeric boom length or radius/boom pair but represent materially different real-world operating conditions. Examples include:

- different parts-of-line / number-of-lines configurations;
- different operating regions such as rear/side/360-degree;
- manufacturer-specific visual columns with duplicated numeric headers;
- conservative duplicate capacity values for the same numeric lookup point.

A capacity lookup that only reports numeric radius, boom length, and available capacity is insufficient for auditing. A downstream reviewer needs to know which exact visual load-chart column produced the selected capacity.

### 4.2 Specific Failure Mode Prevented

The branch prevents a subtle but important provenance failure:

```text
Same radius + same numeric boom length
→ multiple visual load-chart columns
→ resolver selects a capacity conservatively
→ diagnostics previously could obscure which visual column was selected
```

Without selected provenance metadata, a report consumer could not reliably distinguish:

```text
39.1 / 10 lines
39.1 / 8 lines
39.1 / 6 lines
```

or similar operating variants. This is particularly dangerous because these variants may have different rated capacities and may determine whether a lift is considered feasible.

### 4.3 Design Requirement Clarified During Implementation

A key requirement clarified during the implementation history was:

> `SelectedNumberOfLines` and `SelectedOperatingConditionLabel` must come from the exact selected load-chart capacity entry chosen by the resolver, not from an optimizer setting invented or inferred after the fact.

This requirement is now reflected in the resolver and tests. When a condition mismatch occurs, the resolver fails with a condition mismatch and leaves selected metadata empty/null instead of fabricating provenance.

---

## 5. Scope, Non-Scope, and Branch Boundaries

### 5.1 In Scope

The branch intentionally changes:

- Domain load-chart point metadata;
- created-project load-chart snapshot metadata;
- EF mappings and migrations;
- SQLite runtime compatibility guard;
- CSV load-chart metadata parsing;
- PDF load-chart metadata extraction;
- load-chart DTO and mapper propagation;
- catalog and project snapshot persistence;
- load-chart resolver selection semantics;
- GA settings for optional load-chart condition constraints;
- GA result DTOs and intermediate result models;
- GA assignment and placement propagation;
- Summary and Research diagnostics output contracts;
- Benchmark V2 CSV output contract;
- focused tests and verification guardrails.

### 5.2 Explicitly or Effectively Out of Scope

The branch does **not** appear to change:

| Area | Status |
|---|---|
| Installer packaging | No installer/project installer files appear in the 38-file diff. |
| Dependency packages | No `.csproj`, `packages.config`, or NuGet dependency files appear in the 38-file diff. |
| Streamlit UI | No Streamlit project is evident in the reviewed WPF/.NET solution. The affected UI is WPF load-chart import/catalog code. |
| General GA algorithm strategy | The branch propagates metadata and constraints; it does not redesign selection, crossover, mutation, or global optimization strategy. |
| IFC parsing | No direct IFC parser changes are part of the branch. |
| DXF/rendering/GIS/pathfinding | No direct changes are visible in those subsystems. |
| Full user-facing condition-selection UX | Resolver settings exist; a normal WPF user-facing selector is not evident in this branch. |

### 5.3 Non-Goal: Replacing Existing Architecture

The branch does not attempt to replace the project’s layered design. It keeps responsibilities separated:

```text
Domain: entity metadata fields
Infrastructure: schema and migrations
UI load-chart import/catalog services: importing and preserving metadata
Application/GA: resolver decisions and diagnostics
Tests: focused regression coverage
```

---

## 6. Architecture Before and After the Branch

### 6.1 Existing Architectural Pattern

CranePathOptimizer is a layered C#/.NET WPF application. The branch continues the existing pattern:

```text
CraneOptimizer.UI
  WPF dialogs, load-chart import workflow, catalog browser, project workspace orchestration

CraneOptimizer.Application
  GA engines, resolver, assignment services, diagnostics writers, DTOs

CraneOptimizer.Domain
  Core entities and schemas such as TargetPoint and project crane snapshots

CraneOptimizer.Infrastructure
  EF DbContexts, mappings, migrations, SQLite compatibility behavior

CraneOptimizer.Application.Tests
  Unit and integration-style tests for diagnostics, GA, load chart parsing, persistence
```

### 6.2 Before the Branch

Before this branch, the system already supported load-chart import and capacity lookup, including full-fidelity visual-column preservation from earlier work. However, selected provenance was incomplete in downstream outputs:

- line-count metadata was not fully persisted everywhere it needed to be;
- operating-condition labels were not fully persisted;
- selected visual/line metadata was not fully propagated to GA outputs;
- condition mismatch behavior was not explicit;
- diagnostics contracts did not expose selected provenance consistently;
- runtime SQLite databases could fail if migration history and actual schema drifted.

### 6.3 After the Branch

After this branch, imported load-chart metadata becomes part of the end-to-end data model and diagnostics contract.

The architecture now supports this chain:

```text
PDF/CSV input
  ↓
Raw visual grid with optional Number of lines row
  ↓
TargetPoint:
  Radius
  BoomLength
  Capacity
  VisualColumnIndex
  VisualColumnHeader
  NumberOfLines
  OperatingConditionLabel
  ↓
Persistence:
  CraneDbContext / TargetPoints
  CreatedProjectDbContext / ProjectCraneLoadChartSnapshots
  ↓
LoadChartCapacityResolver:
  conservative duplicate selection
  optional line-count / condition constraints
  selected provenance fields
  condition mismatch failure
  ↓
ElementEvaluationResult / PlacementResult
  ↓
GACsvDiagnosticsService / BenchmarkV2CsvWriter
  ↓
GA output CSVs visible to researchers and users
```

---

## 7. End-to-End Provenance Flow

### 7.1 Metadata Creation

Metadata originates during load-chart import. The relevant input forms are:

- PDF visual rows and columns;
- CSV raw grids;
- exact visual raw grids with duplicated numeric headers;
- rows labeled like `Number of lines`.

The branch extracts:

```text
NumberOfLines
OperatingConditionLabel
VisualColumnIndex
VisualColumnHeader
```

### 7.2 Metadata Persistence

Metadata is stored on:

```text
TargetPoint
ProjectCraneLoadChartSnapshotEntity
```

and mapped through:

```text
CraneDbContext
ProjectCraneLoadChartSnapshotConfiguration
EF migrations
CreatedProjectDb migrations
Model snapshots
```

### 7.3 Metadata Selection

The resolver groups duplicate boom entries by rounded boom length and chooses a capacity entry. If there are multiple positive entries for the same numeric boom group, the resolver continues the conservative approach: choose the minimum positive capacity unless an explicit condition constraint narrows the set.

The selected entry is then used as the sole source of selected metadata.

### 7.4 Metadata Propagation

The selected provenance fields flow through:

```text
LoadChartResolverResult
ElementEvaluationResult
PlacementResult
ElementAssignmentService
HungarianAssignmentService
CranePlacementEngine
GACsvDiagnosticsService
BenchmarkV2CsvWriter
```

### 7.5 Metadata Emission

The metadata appears in:

- Summary-mode `ga_element_assignments.csv`;
- Research-mode detailed diagnostics CSV;
- Benchmark V2 element assignment output;
- run history and settings snapshots where relevant.

---

## 8. Subsystem-Level Implementation Analysis

### 8.1 Domain Layer

#### What Changed

The Domain layer now stores load-chart metadata as data, not as UI-only display state.

Affected entities:

```text
CraneOptimizer.Domain/Entities/TargetPoint.cs
CraneOptimizer.Domain/CreatedProject/CraneFiles/ProjectCraneLoadChartSnapshotEntity.cs
```

#### Why It Changed

A resolver cannot correctly report selected load-chart provenance if the imported metadata is discarded before it reaches the Application layer. The Domain layer must carry the metadata because it is intrinsic to the load-chart entry.

#### Downstream Impact

- Persistence now needs new columns.
- DTOs and mappers must carry the metadata.
- Resolver can select and report exact source metadata.
- Diagnostics can be audited against original chart structure.

### 8.2 Infrastructure and Data Access

#### What Changed

The branch updates EF mappings, migrations, model snapshots, and SQLite compatibility handling.

Affected areas:

```text
CraneDbContext
ProjectCraneLoadChartSnapshotConfiguration
CraneDbContextModelSnapshot
CreatedProjectDbContextModelSnapshot
new catalog migrations
new created-project migrations
SqliteMigrationCompatibilityGuard
```

#### Why It Changed

Two database contexts persist related but distinct load-chart data:

1. the main catalog database stores crane/load-chart catalog points;
2. created-project databases store project-specific crane load-chart snapshots.

Both must preserve metadata to avoid losing provenance between catalog import and project execution.

#### Downstream Impact

- Existing runtime databases may need migration or compatibility repair.
- EF model snapshots reflect the new schema.
- Runtime startup/project-open flows call the compatibility guard.

### 8.3 Load-Chart CSV Import

#### What Changed

`LoadChartCsvService` now recognizes `Number of lines` metadata rows, parses line-count values, and applies them to generated load-chart points.

#### Why It Changed

CSV/raw-grid imports can represent line-count metadata in a row rather than in a header. Treating that row as capacity data would generate invalid load-chart points. Ignoring it would lose provenance.

#### Downstream Impact

- The parser avoids creating fake capacity rows.
- Duplicate visual headers remain distinguishable.
- Generated `TargetPoint` entries include `NumberOfLines` and `OperatingConditionLabel`.
- Tests now lock the expected behavior.

### 8.4 Load-Chart PDF Import

#### What Changed

`LoadChartPdfService` now attempts to append a line-count metadata row when PDF geometry indicates such a row exists. The implementation maps detected line-count tokens into visual column centers.

#### Why It Changed

PDF load charts often encode line-count / reeving information visually rather than as a machine-readable field. Since the project’s PDF importer already reconstructs visual grids, preserving a `Number of lines` row maintains fidelity.

#### Downstream Impact

- Exact visual grids are more complete.
- Conservative duplicate-column selection can report line-count provenance.
- PDF import becomes more audit-friendly.
- The implementation remains heuristic because PDF geometry extraction depends on document layout quality.

### 8.5 Application / GA Resolver

#### What Changed

`LoadChartCapacityResolver` now:

- applies optional operating-condition constraints;
- returns `LoadChartConditionMismatch` when constraints eliminate all candidates;
- records selected visual column, header, number of lines, operating condition, duplicate fallback status, and alternative summary;
- supports comparable behavior in legacy and indexed paths.

#### Why It Changed

The resolver is the authority for capacity selection. Therefore, it must be the authority for selected provenance. Reporting selected metadata anywhere else risks inconsistency.

#### Downstream Impact

- GA diagnostics become auditable.
- Condition-aware selection becomes possible.
- Tests validate indexed and non-indexed path consistency.
- Future UI can bind to the GA settings if user-facing selection is implemented.

### 8.6 GA Assignment and Placement

#### What Changed

The assignment and placement result models now carry selected load-chart provenance.

Affected files include:

```text
PlacementResult.cs
ElementEvaluationResult.cs
LoadChartResolverResult.cs
ElementAssignmentService.cs
HungarianAssignmentService.cs
CranePlacementEngine.cs
```

#### Why It Changed

Metadata selected by the resolver would be lost if intermediate GA results did not expose the new fields.

#### Downstream Impact

- Both normal and Hungarian assignment paths preserve metadata.
- Final placement outputs can report actual selected chart source.
- Diagnostics no longer need to recompute/reparse the load chart.

### 8.7 Diagnostics and Reporting

#### What Changed

`GACsvDiagnosticsService` and `BenchmarkV2CsvWriter` now emit selected provenance fields.

Summary mode now exposes selected metadata in `ga_element_assignments.csv`.

Research mode keeps detailed diagnostics while also including selected provenance.

`ga_settings_snapshot.csv` now records `DiagnosticsLevel`.

`ga_run_history.csv` avoids pointing to missing Research-only detailed diagnostics files during Summary mode.

#### Why It Changed

Diagnostics are the primary mechanism for auditing GA decisions. If the selected load-chart entry is not present in diagnostics, the implementation cannot be trusted for research-grade reproducibility.

#### Downstream Impact

- CSV contracts changed by adding columns.
- Existing downstream CSV readers should be checked if they assume positional column indexes.
- Research workflows gain stronger provenance.

### 8.8 WPF UI / Presentation Layer

#### What Changed

The branch touches load-chart import and catalog display/copy pathways:

```text
LoadChartCraneDto.cs
LoadChartCraneMapper.cs
LoadChartCatalogService.cs
LoadChartCsvService.cs
LoadChartPdfService.cs
CraneCatalogBrowserDialog.xaml.cs
LoadChartImportDialog.xaml.cs
```

#### Why It Changed

The UI import pipeline must preserve metadata from imported charts and display/export grids without losing the line-count row.

#### Downstream Impact

- Imported crane catalog entries keep richer metadata.
- Displayed raw grids can reconstruct `Number of lines` rows when present.
- The UI layer remains responsible for import/display flow, not optimizer decisions.

### 8.9 Streamlit UI

No Streamlit UI project or Streamlit-specific files appear in the reviewed .NET/WPF branch evidence. Therefore:

```text
Streamlit UI impact: none detected.
```

The relevant UI impact is WPF-specific.

### 8.10 Installers and Packaging

No installer, setup project, deployment manifest, or package installer file appears in the 38-file diff. Therefore:

```text
Installer impact: none detected.
```

### 8.11 Dependency Management

No `.csproj`, package lock, or dependency manifest appears in the 38-file diff. Therefore:

```text
Dependency impact: no new NuGet/package dependency detected.
```

The verification output does show existing package compatibility warnings, but those warnings pre-exist the branch’s dependency surface and did not block restore/build/test.

---

## 9. File-by-File Change Ledger

This section records every file in the 38-file branch diff, grouped by layer.

### 9.1 Tests

#### `CraneOptimizer.Application.Tests/Diagnostics/CoordinateReferenceTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Test inputs now explicitly set `DiagnosticsLevel = Research` where tests expect full diagnostic artifacts. |
| Why | Summary/Research diagnostics behavior was made distinct. Tests that depend on detailed diagnostics must opt into Research mode. |
| Impact | Prevents false failures after Summary mode became intentionally lighter. |
| Risk | Low; test-only change. |

#### `CraneOptimizer.Application.Tests/GA/BenchmarkV2DiagnosticsContractTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Benchmark diagnostics test settings now include `DiagnosticsLevel = Research`. |
| Why | Benchmark diagnostics contract expects detailed/research artifacts. |
| Impact | Keeps test expectations aligned with diagnostics-level semantics. |
| Risk | Low; test-only change. |

#### `CraneOptimizer.Application.Tests/GA/DiagnosticsSeparationTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds Summary-mode test verifying selected load-chart metadata is present in `ga_element_assignments.csv`; adds CSV parsing helper; updates test fixtures with selected metadata. |
| Why | Summary diagnostics should remain lightweight but still expose selected provenance. |
| Impact | Locks behavior that Summary mode does not generate Research-only detailed diagnostics, while still reporting provenance in assignment output. |
| Risk | Low. Test parsing logic is simple but sufficient for current CSV assertions. |

#### `CraneOptimizer.Application.Tests/GA/HungarianAssignmentServiceTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Large-dataset timing benchmark now uses warmups, repeated timed iterations, median/worst-run guardrails, and more tolerant thresholds. |
| Why | The earlier single-run `< 50ms` check was too sensitive to Debug-build noise, JIT, scheduling, antivirus, or machine load. |
| Impact | Preserves regression detection while reducing flaky test failures. |
| Risk | Medium-low. A timing test remains environment-sensitive, but this is more robust than the previous single-sample guard. |

#### `CraneOptimizer.Application.Tests/GA/LoadChartCapacityResolverTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds tests for duplicate visual-column metadata, indexed resolver path consistency, number-of-lines constraint selection, and mismatch failure without invented metadata. |
| Why | The resolver is the source of truth for selected load-chart provenance. |
| Impact | Strong regression coverage for core branch semantics. |
| Risk | Low; tests encode expected behavior. |

#### `CraneOptimizer.Application.Tests/LoadChart/LoadChartNumberOfLinesMetadataTests.cs`

| Aspect | Detail |
|---|---|
| What changed | New test file verifying CSV/raw-grid metadata parsing. |
| Why | Ensures `Number of lines` rows are metadata, not fake capacity rows. |
| Impact | Prevents regressions in CSV import and exact visual grid parsing. |
| Risk | Low. |

#### `CraneOptimizer.Application.Tests/ProjectPersistence/GoldenMasterTests.cs`

| Aspect | Detail |
|---|---|
| What changed | Sets `DiagnosticsLevel = Research` for a high-coverage golden-master test. |
| Why | The test expects diagnostics artifacts that are now gated by diagnostics level. |
| Impact | Maintains golden-master behavior while respecting Summary/Research separation. |
| Risk | Low; test-only. |

### 9.2 Application / GA Result Models and Services

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/DTOs/PlacementResult.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds selected load-chart provenance fields to placement results. |
| Why | Final placement outputs must carry selected resolver metadata. |
| Impact | Diagnostics/report writers can consume selected metadata without recomputing. |
| Risk | Low; additive properties preserve binary/source compatibility for most consumers. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs`

| Aspect | Detail |
|---|---|
| What changed | Copies resolver selected-provenance fields into element evaluation results in multiple assignment paths. |
| Why | Metadata selected during capacity resolution must not be dropped before final evaluation/reporting. |
| Impact | Standard GA assignment path preserves provenance. |
| Risk | Low-medium; any future assignment path must continue copying these fields. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs`

| Aspect | Detail |
|---|---|
| What changed | Copies selected-provenance fields from evaluation results into placement results. |
| Why | Final placement result is the reportable optimization output. |
| Impact | Provenance reaches final GA outputs. |
| Risk | Low; mapping expansion only. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CapacityFailureReason.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `LoadChartConditionMismatch`. |
| Why | Explicitly distinguishes “chart exists but not under requested condition” from generic no-capacity cases. |
| Impact | Diagnostics can classify condition-filter failures more accurately. |
| Risk | Low; enum addition is additive but downstream string matching should be aware. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/ElementEvaluationResult.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds selected-provenance fields. |
| Why | Evaluation results bridge resolver output and placement/diagnostics. |
| Impact | Enables metadata propagation through optimizer internals. |
| Risk | Low. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/LoadChartResolverResult.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds selected-provenance fields. |
| Why | The resolver must report the provenance of its selected capacity entry. |
| Impact | Establishes source-of-truth metadata for downstream systems. |
| Risk | Low. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/BenchmarkV2CsvWriter.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `DiagnosticsLevel` to settings snapshot; ensures generation summary headers; adds selected provenance columns to element assignment output. |
| Why | Benchmark output must remain self-describing and reproducible. |
| Impact | Benchmark CSV contract changes by adding columns. |
| Risk | Medium for external consumers that rely on positional CSV indexes. Low for header-driven readers. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/GACsvDiagnosticsService.cs`

| Aspect | Detail |
|---|---|
| What changed | Supports `DiagnosticsLevel.Off`; adds selected provenance fields to Summary and Research outputs; prevents Summary history from referencing missing detailed diagnostics; returns detailed path only when Research diagnostics exist. |
| Why | Diagnostics-level separation requires outputs to be accurate and not point to absent files. |
| Impact | More accurate diagnostics contracts and safer Summary behavior. |
| Risk | Medium for consumers expecting detailed diagnostics path in Summary mode. This is an intended correction. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/HungarianAssignmentService.cs`

| Aspect | Detail |
|---|---|
| What changed | Copies resolver selected-provenance fields into evaluation results in Hungarian assignment paths. |
| Why | Hungarian assignment must preserve the same metadata as standard assignment. |
| Impact | Ensures consistency across assignment algorithms. |
| Risk | Low-medium; future assignment paths require the same propagation discipline. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds condition filtering, selected provenance population, duplicate-alternative summaries, mismatch classification, and helper methods for normalization/line-count matching. |
| Why | Capacity selection is the only authoritative point where selected load-chart provenance can be known. |
| Impact | Core behavioral change for condition-aware load-chart selection and diagnostics. |
| Risk | Medium. Resolver semantics are central; tests mitigate risk, but real load charts should be smoke-tested. |

#### `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds optional `LoadChartNumberOfLinesConstraint` and `LoadChartOperatingConditionConstraint`. |
| Why | Enables programmatic condition-aware resolver selection. |
| Impact | Future UI/workflow can supply explicit load-chart condition constraints. |
| Risk | Low when null/default; medium if exposed later without careful UX. |

### 9.3 Domain

#### `CraneOptimizer.Domain/CreatedProject/CraneFiles/ProjectCraneLoadChartSnapshotEntity.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds line-count and operating-condition metadata to project crane load-chart snapshot rows. |
| Why | Project snapshots must preserve the chart metadata used at run time. |
| Impact | Project-level reproducibility improves. |
| Risk | Requires schema migration and compatibility handling. |

#### `CraneOptimizer.Domain/Entities/TargetPoint.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds metadata fields to catalog/load-chart target points. |
| Why | Load-chart metadata must live with the capacity entry. |
| Impact | Catalog entries become provenance-aware. |
| Risk | Requires schema migration. |

### 9.4 Infrastructure

#### `CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs`

| Aspect | Detail |
|---|---|
| What changed | Maps `NumberOfLines` and `OperatingConditionLabel` for catalog `TargetPoint` entities. |
| Why | EF must persist the new domain metadata. |
| Impact | Catalog database schema includes new columns. |
| Risk | Requires migration compatibility. |

#### `CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectConfigurations/ProjectCraneLoadChartSnapshotConfiguration.cs`

| Aspect | Detail |
|---|---|
| What changed | Maps project snapshot `NumberOfLines` and `OperatingConditionLabel`. |
| Why | Created-project databases must preserve metadata. |
| Impact | Project database schema includes new columns. |
| Risk | Existing project databases require migration/repair. |

#### `CraneOptimizer.Infrastructure/Contexts/SqliteMigrationCompatibilityGuard.cs`

| Aspect | Detail |
|---|---|
| What changed | New compatibility guard for SQLite schema/migration-history repair. |
| Why | Runtime databases may have migration history drift or missing `OperatingConditionLabel`, causing runtime errors such as `no such column: t.OperatingConditionLabel`. |
| Impact | Startup/project-open flows can repair the specific known schema issue safely. |
| Risk | Guard is targeted, not a general database-repair framework. |

#### `CraneOptimizer.Infrastructure/Migrations/20260608165000_AddLoadChartNumberOfLinesMetadata.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `NumberOfLines` to catalog load-chart table. |
| Why | Catalog persistence of line-count metadata. |
| Impact | Required for catalog metadata retention. |
| Risk | Standard migration risk. |

#### `CraneOptimizer.Infrastructure/Migrations/20260609011000_AddLoadChartOperatingConditionLabel.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `OperatingConditionLabel` to catalog load-chart table. |
| Why | Catalog persistence of operating condition metadata. |
| Impact | Required for resolver diagnostics and condition filtering. |
| Risk | Runtime databases may require compatibility guard if partially migrated. |

#### `CraneOptimizer.Infrastructure/Migrations/CraneDbContextModelSnapshot.cs`

| Aspect | Detail |
|---|---|
| What changed | Updates catalog EF model snapshot. |
| Why | Keeps EF model snapshot aligned with migrations. |
| Impact | Future EF migrations start from correct schema model. |
| Risk | Low; required migration hygiene. |

#### `CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260608165100_AddLoadChartNumberOfLinesSnapshotMetadata.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `NumberOfLines` to created-project snapshot table. |
| Why | Project snapshots must persist line-count metadata. |
| Impact | Project database migration. |
| Risk | Standard migration risk. |

#### `CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260609011100_AddLoadChartSnapshotOperatingConditionLabel.cs`

| Aspect | Detail |
|---|---|
| What changed | Adds `OperatingConditionLabel` to created-project snapshot table. |
| Why | Project snapshots must persist operating-condition metadata. |
| Impact | Project database migration. |
| Risk | Existing project DBs may need compatibility repair. |

#### `CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/CreatedProjectDbContextModelSnapshot.cs`

| Aspect | Detail |
|---|---|
| What changed | Updates created-project EF model snapshot. |
| Why | Keeps EF migration model aligned. |
| Impact | Future migrations start from correct schema. |
| Risk | Low. |

### 9.5 UI / Load-Chart Services

#### `CraneOptimizer.UI/App.xaml.cs`

| Aspect | Detail |
|---|---|
| What changed | Wires SQLite compatibility guard into app startup migration behavior. |
| Why | Prevents runtime failure when catalog DB is missing known new columns. |
| Impact | Safer startup for existing user databases. |
| Risk | Low-medium; guard must remain narrowly targeted and safe. |

#### `CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectWorkspaceService.cs`

| Aspect | Detail |
|---|---|
| What changed | Wires compatibility guard around created-project database migration/open flow. |
| Why | Existing project databases may need schema repair. |
| Impact | Reduces runtime project-open failures. |
| Risk | Low-medium; database repair should be logged/observable if expanded later. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/DTOs/LoadChartCraneDto.cs`

| Aspect | Detail |
|---|---|
| What changed | Extends load-chart point DTO shape with line-count and operating-condition metadata. |
| Why | UI import/catalog services need to pass metadata through DTO boundaries. |
| Impact | DTO contract is richer and more faithful to imported charts. |
| Risk | Low; additive. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Mappers/LoadChartCraneMapper.cs`

| Aspect | Detail |
|---|---|
| What changed | Maps metadata between domain entities and load-chart DTOs. |
| Why | Avoid losing metadata at service boundaries. |
| Impact | Catalog/project load-chart conversions remain provenance-preserving. |
| Risk | Low. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCatalogService.cs`

| Aspect | Detail |
|---|---|
| What changed | Persists and reconstructs metadata for catalog load-chart entries. |
| Why | Catalog save/load workflows must preserve metadata. |
| Impact | Imported crane catalogs remain auditable after persistence. |
| Risk | Low-medium; existing catalog entries will have null metadata until reimported or migrated. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCsvService.cs`

| Aspect | Detail |
|---|---|
| What changed | Detects line-count rows, parses line-count cells, skips metadata rows from capacity parsing, and builds operating labels. |
| Why | CSV import must preserve line-count metadata while avoiding invalid capacity entries. |
| Impact | CSV import becomes provenance-aware. |
| Risk | Medium for unusual CSV variants; tests cover representative cases. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs`

| Aspect | Detail |
|---|---|
| What changed | Detects/appends line-count metadata rows from PDF word geometry and column centers; integrates with exact visual grids and conservative import grids. |
| Why | PDF charts commonly encode line-count metadata visually. |
| Impact | PDF import becomes more faithful and diagnostically useful. |
| Risk | Medium because PDF extraction is layout-dependent and should be smoke-tested with multiple real manufacturer charts. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneCatalogBrowserDialog.xaml.cs`

| Aspect | Detail |
|---|---|
| What changed | Reconstructs/displays line-count row when chart metadata exists. |
| Why | Users/reviewers should see preserved metadata in catalog browsing. |
| Impact | Improved catalog visibility. |
| Risk | Low. |

#### `CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml.cs`

| Aspect | Detail |
|---|---|
| What changed | Preserves metadata during import dialog processing and display. |
| Why | The import review workflow must not drop line-count/condition metadata before saving. |
| Impact | Imported load charts carry metadata into catalog/project flow. |
| Risk | Low-medium; UI smoke testing is recommended. |

---

## 10. Resolver Semantics and Algorithmic Behavior

### 10.1 Default Behavior

When no explicit line-count or operating-condition constraint is supplied, the resolver preserves the existing conservative duplicate-column behavior.

For same-radius/same-boom duplicate visual columns:

```text
candidate entries
→ filter positive capacities
→ sort by capacity ascending
→ choose minimum positive capacity
→ mark conservative duplicate fallback when appropriate
→ report duplicate alternatives
```

This is intentionally conservative and backward compatible.

### 10.2 Constraint-Aware Behavior

When constraints are supplied:

```text
LoadChartNumberOfLinesConstraint
LoadChartOperatingConditionConstraint
```

the resolver filters candidate entries before selecting the capacity.

If entries match the requested condition, the resolver selects from those entries. If no entries match, the resolver returns:

```text
LiftStatus: NotCharted
ChartStatus: NoListedCapacity
FailureReason: LoadChartConditionMismatch
SelectedNumberOfLines: null
SelectedVisualColumnHeader: empty
SelectedOperatingConditionLabel: empty
```

This prevents the system from inventing provenance.

### 10.3 Operating Condition Label Semantics

The implementation distinguishes:

| Field | Meaning |
|---|---|
| `SelectedVisualColumnHeader` | Exact visual provenance from the imported chart column, including duplicate suffixes such as `39.1 (2)` when present. |
| `SelectedOperatingConditionLabel` | Engineering-normalized condition label, such as `39.1 / 4 lines`. |
| `SelectedNumberOfLines` | Imported line-count metadata from the exact selected entry. |

This distinction is important because duplicate visual headers may include UI/visual disambiguators that should remain in `SelectedVisualColumnHeader`, while the operating condition should be normalized for engineering/reporting purposes.

### 10.4 Pseudocode Summary

```text
Resolve(craneSpec, requiredRadius, requiredBoom, elementWeight, settings):
    validate chart and geometric envelope

    radius = first chart radius >= requiredRadius
    candidates = entries at selected radius with boom >= requiredBoom

    for each boom group:
        groupEntries = entries with same rounded boom
        selectable = apply number-of-lines / operating-condition constraints

        if constraints exist and selectable is empty:
            record ConditionMismatch rejection
            continue

        positive = selectable entries with capacity > 0, sorted by capacity
        if positive is empty:
            record InvalidBlankCell or InvalidZeroCell
            continue

        selected = minimum positive capacity entry
        available = selected.capacity * derating * safety

        if available >= elementWeight:
            populate result from selected entry
            include duplicate alternatives
            return Liftable

        track best overweight entry

    if constraints eliminated all meaningful candidates:
        return LoadChartConditionMismatch

    otherwise populate overweight/no-capacity result as appropriate
```

### 10.5 Why the Resolver Owns This Logic

The resolver is the only subsystem that knows:

- required radius;
- required boom;
- selected chart radius;
- selected chart boom;
- duplicate entries considered;
- condition filters applied;
- selected capacity entry;
- rejected candidates;
- final lift/chart status.

Therefore, it is the correct and only safe authority for selected provenance.

---

## 11. Diagnostics and Output Contract Changes

### 11.1 Summary-Mode Output

Summary mode remains lightweight but now exposes selected provenance in `ga_element_assignments.csv`.

New/added columns include:

```text
SelectedVisualColumnIndex
SelectedVisualColumnHeader
SelectedNumberOfLines
SelectedOperatingConditionLabel
IsConservativeDuplicateFallback
DuplicateAlternativeCount
DuplicateAlternativeSummary
```

### 11.2 Research-Mode Output

Research mode keeps detailed candidate-level diagnostics and now includes the selected provenance fields in detailed rows.

This supports forensic review of:

- candidate boom checks;
- rejected candidates;
- selected load-chart row;
- actual line-count source;
- duplicate alternatives.

### 11.3 Run History Contract

The diagnostics service prevents Summary-mode history rows from pointing to a missing Research-only detailed diagnostics CSV.

This is a correctness fix. A path field should not claim a file exists when Summary mode intentionally does not generate that file.

### 11.4 Settings Snapshot

`ga_settings_snapshot.csv` now includes `DiagnosticsLevel`.

This improves reproducibility because diagnostics output shape depends on diagnostics level.

### 11.5 Backward Compatibility of CSV Outputs

The branch adds columns rather than removing existing columns. Header-aware consumers should remain compatible. Positional consumers may require adjustment.

Recommended downstream consumer strategy:

```text
Read columns by header name, not ordinal index.
Treat missing provenance columns as null/empty for older reports.
```

---

## 12. Database, Migration, and Runtime Compatibility Notes

### 12.1 Schema Additions

Catalog load-chart rows gain:

```text
NumberOfLines
OperatingConditionLabel
```

Created-project load-chart snapshot rows gain:

```text
NumberOfLines
OperatingConditionLabel
```

### 12.2 Migration Split

The branch separates catalog and created-project database migrations:

```text
20260608165000_AddLoadChartNumberOfLinesMetadata
20260608165100_AddLoadChartNumberOfLinesSnapshotMetadata
20260609011000_AddLoadChartOperatingConditionLabel
20260609011100_AddLoadChartSnapshotOperatingConditionLabel
```

This split is appropriate because catalog and project databases are separate persistence contexts.

### 12.3 SQLite Compatibility Guard

The compatibility guard exists because runtime testing exposed a schema drift issue:

```text
no such column: t.OperatingConditionLabel
```

The guard repairs the known condition by:

- checking for the target table;
- checking for the missing column;
- adding the missing column where appropriate;
- inserting the corresponding migration-history row where needed;
- skipping absent project snapshot tables rather than failing.

### 12.4 Compatibility Guard Boundary

This guard is targeted. It should not be treated as a general-purpose schema reconciliation engine.

Future migrations should still be implemented normally. The guard is justified here because the project had a known runtime partial-migration scenario and user databases must not fail at startup/project open.

---

## 13. Verification and Reproducibility Record

### 13.1 Current Verification Evidence

Fresh branch verification reported:

```text
Git diff check: exit code 0
dotnet restore: exit code 0
dotnet build: exit code 0
dotnet test CraneOptimizer.Application.Tests: exit code 0
```

Application tests:

```text
Failed: 0
Passed: 352
Skipped: 1
Total: 353
```

The full forensic package reported no recorded native command failures.

### 13.2 Verification Coverage

The tests cover:

- CSV exact visual raw-grid metadata preservation;
- CSV raw-grid line-count row exclusion from capacity parsing;
- duplicate visual-column conservative metadata selection;
- indexed and non-indexed resolver consistency;
- line-count constraint selection;
- line-count mismatch failure without invented metadata;
- Summary diagnostics selected metadata exposure;
- Research diagnostics behavior;
- benchmark diagnostics contract;
- Hungarian timing stability;
- project persistence golden-master behavior adjusted for diagnostics level.

### 13.3 Verification Gaps

Automated verification does not fully replace manual WPF runtime smoke testing.

Recommended manual smoke tests:

```text
1. Launch the WPF app.
2. Open/create a project.
3. Import a CSV load chart with a Number of lines row.
4. Import a PDF load chart known to contain line-count metadata.
5. Save crane to catalog.
6. Reopen catalog entry and confirm metadata is visible.
7. Run GA from UI.
8. Inspect ga_element_assignments.csv for selected provenance columns.
9. Confirm Summary mode does not claim a missing Research-only diagnostics file.
10. Confirm existing projects open without OperatingConditionLabel schema errors.
```

---

## 14. Project-Wide Cross-Impact Assessment

### 14.1 Directly Affected Components

| Component | Impact |
|---|---|
| Load-chart import | Metadata detection and preservation. |
| Catalog persistence | New metadata fields stored. |
| Created-project snapshots | New metadata fields stored. |
| Capacity resolver | Selected provenance and optional constraints. |
| GA assignment paths | Metadata propagation. |
| Diagnostics/reporting | New provenance columns and diagnostics-level behavior. |
| EF migrations | New schema columns and snapshots. |
| Runtime migration safety | Compatibility guard. |
| Tests | New focused tests and adjusted diagnostics-level expectations. |

### 14.2 Indirectly Affected Components

| Component | Indirect Impact |
|---|---|
| Research workflows | More auditable GA results. |
| Benchmark V2 output consumers | New columns and diagnostics-level context. |
| Existing runtime databases | May be repaired by compatibility guard. |
| CSV post-processing tools | Should read by header, not ordinal. |
| QA workflows | New smoke-test areas around PDF/CSV provenance. |

### 14.3 Not Directly Affected

| Component | Reason |
|---|---|
| IFC extraction | No direct code changes. |
| DXF/rendering | No direct code changes. |
| Pathfinding/relocation | No direct code changes evident. |
| Installer/deployment | No installer files changed. |
| Streamlit | No Streamlit subsystem detected. |
| NuGet dependencies | No project/dependency file changed. |

### 14.4 Architectural Consequences

The branch strengthens architectural separation:

- Domain stores entity data.
- Infrastructure owns database persistence and schema compatibility.
- UI import services own import/display workflows.
- Application resolver owns capacity selection.
- Diagnostics consume selected results instead of reparsing load charts.

This is the correct direction for maintainability.

### 14.5 Technical Debt Resolved

- Missing provenance in GA outputs.
- Ambiguous duplicate-column capacity selection reports.
- Summary-mode diagnostics pointing to missing Research-only files.
- Flaky single-run Hungarian benchmark threshold.
- Runtime SQLite `OperatingConditionLabel` schema drift.

### 14.6 Technical Debt Introduced or Remaining

- Condition constraints exist in settings but are not clearly exposed in a normal user workflow.
- PDF metadata extraction remains heuristic.
- Diagnostics CSV schema expansion may require downstream consumer updates.
- Encoding/mojibake markers in diff displays suggest source encoding hygiene should be monitored.
- Compatibility guard is targeted and may need expansion if similar migration drift appears later.

---

## 15. Backward Compatibility and Regression Prevention

### 15.1 Backward Compatibility

The branch is primarily additive:

- new nullable entity fields;
- new diagnostics columns;
- new optional settings;
- new enum member;
- new migrations;
- new tests.

Default behavior remains conservative when no explicit load-chart condition constraint is supplied.

### 15.2 Regression Prevention

Regression prevention is implemented through:

- focused resolver tests;
- load-chart metadata parsing tests;
- diagnostics separation tests;
- benchmark contract tests;
- golden-master test updates;
- final restore/build/test verification.

### 15.3 Important Compatibility Boundary

CSV schema additions may affect external tools. The safest consumer guidance is:

```text
Use header-based lookup.
Do not assume fixed ordinal positions.
Treat absent columns as null/empty for older runs.
```

---

## 16. Performance and Reliability Considerations

### 16.1 Resolver Performance

The resolver now performs additional grouping/filtering/reporting work. The expected overhead is small relative to GA evaluation cost, but it does add work for duplicate visual-column groups.

The main added operations are:

- grouping entries by rounded boom length;
- filtering by optional constraints;
- sorting positive duplicate entries by capacity and visual column;
- building duplicate summaries when diagnostics are requested.

### 16.2 Diagnostics Performance

Summary mode remains lightweight. Detailed candidate diagnostics remain Research-mode gated.

This is important because full Research diagnostics can be large and expensive during GA runs.

### 16.3 Hungarian Timing Guardrail

The large-dataset Hungarian test was changed from a single strict measurement to a warmup + median/worst-run guardrail. This reduces false failures caused by ordinary machine noise while still detecting major performance regressions.

---

## 17. Known Limitations and Open Follow-Up Items

### 17.1 User-Facing Condition Selection

`GASettings` contains:

```text
LoadChartNumberOfLinesConstraint
LoadChartOperatingConditionConstraint
```

The resolver honors them. However, a normal WPF user-facing mechanism to select these constraints is not evident in this branch.

Recommended follow-up:

```text
Design and implement a UI workflow for selecting load-chart operating condition / line count,
if product requirements call for explicit user selection rather than conservative automatic selection.
```

### 17.2 PDF Variability

PDF line-count extraction depends on layout, text extraction quality, visual row detection, and column-center mapping. It should be tested on multiple real crane manufacturer PDFs.

### 17.3 Downstream Diagnostics Consumers

Any scripts consuming `ga_element_assignments.csv`, detailed diagnostics CSVs, or Benchmark V2 CSVs should be reviewed for ordinal column assumptions.

### 17.4 Database Repair Observability

The compatibility guard repairs known schema drift, but future maintainers may want more explicit logging/reporting around repairs.

### 17.5 Encoding Hygiene

Diff previews showed mojibake/BOM markers such as `ï»¿` or `∩╗┐` in some outputs. The branch builds/tests successfully, but encoding normalization may be considered as a separate hygiene task.

---

## 18. Reviewer Guide

### 18.1 What Reviewers Should Focus On

Reviewers should focus on:

1. `LoadChartCapacityResolver.cs`
   - selection semantics;
   - constraint filtering;
   - duplicate alternatives;
   - selected metadata population;
   - indexed/non-indexed consistency.

2. `LoadChartCsvService.cs`
   - detection of `Number of lines` rows;
   - exclusion from capacity parsing;
   - exact visual duplicate-header behavior.

3. `LoadChartPdfService.cs`
   - geometry-based line-count detection;
   - column-center mapping;
   - false-positive/false-negative risk.

4. Diagnostics writers
   - Summary vs Research behavior;
   - CSV column additions;
   - missing detailed diagnostics path prevention.

5. Infrastructure
   - migration correctness;
   - compatibility guard safety;
   - catalog vs created-project schema split.

6. Tests
   - whether test coverage matches desired behavior;
   - whether manual UI smoke testing is needed before release.

### 18.2 Suggested Review Questions

- Does the selected metadata always come from the selected `TargetPoint`?
- Can any code path still drop selected provenance before diagnostics?
- Are Summary-mode outputs sufficiently useful without being too heavy?
- Are existing CSV consumers robust to added columns?
- Does the PDF line-count heuristic avoid misclassifying capacity rows?
- Is the SQLite compatibility guard safe for existing user databases?
- Should condition constraints remain programmatic, or be exposed in the UI?

---

## Appendix A — Changed Files by Layer

### Tests

```text
CraneOptimizer.Application.Tests/Diagnostics/CoordinateReferenceTests.cs
CraneOptimizer.Application.Tests/GA/BenchmarkV2DiagnosticsContractTests.cs
CraneOptimizer.Application.Tests/GA/DiagnosticsSeparationTests.cs
CraneOptimizer.Application.Tests/GA/HungarianAssignmentServiceTests.cs
CraneOptimizer.Application.Tests/GA/LoadChartCapacityResolverTests.cs
CraneOptimizer.Application.Tests/LoadChart/LoadChartNumberOfLinesMetadataTests.cs
CraneOptimizer.Application.Tests/ProjectPersistence/GoldenMasterTests.cs
```

### Application

```text
CraneOptimizer.Application/Optimization/GeneticAlgorithms/DTOs/PlacementResult.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CapacityFailureReason.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/ElementEvaluationResult.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/LoadChartResolverResult.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/BenchmarkV2CsvWriter.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/GACsvDiagnosticsService.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/HungarianAssignmentService.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs
CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs
```

### Domain

```text
CraneOptimizer.Domain/CreatedProject/CraneFiles/ProjectCraneLoadChartSnapshotEntity.cs
CraneOptimizer.Domain/Entities/TargetPoint.cs
```

### Infrastructure

```text
CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs
CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectConfigurations/ProjectCraneLoadChartSnapshotConfiguration.cs
CraneOptimizer.Infrastructure/Contexts/SqliteMigrationCompatibilityGuard.cs
CraneOptimizer.Infrastructure/Migrations/20260608165000_AddLoadChartNumberOfLinesMetadata.cs
CraneOptimizer.Infrastructure/Migrations/20260609011000_AddLoadChartOperatingConditionLabel.cs
CraneOptimizer.Infrastructure/Migrations/CraneDbContextModelSnapshot.cs
CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260608165100_AddLoadChartNumberOfLinesSnapshotMetadata.cs
CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260609011100_AddLoadChartSnapshotOperatingConditionLabel.cs
CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/CreatedProjectDbContextModelSnapshot.cs
```

### UI / Presentation

```text
CraneOptimizer.UI/App.xaml.cs
CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectWorkspaceService.cs
CraneOptimizer.UI/Features/F4_LoadChart/DTOs/LoadChartCraneDto.cs
CraneOptimizer.UI/Features/F4_LoadChart/Mappers/LoadChartCraneMapper.cs
CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCatalogService.cs
CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCsvService.cs
CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneCatalogBrowserDialog.xaml.cs
CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml.cs
```

---

## Appendix C — Terminology

| Term | Meaning |
|---|---|
| Load-chart provenance | Metadata identifying where a selected capacity came from in the imported chart. |
| Visual column | A chart column as it appears visually in a PDF/CSV grid, not merely its numeric boom value. |
| Number of lines | Reeving / parts-of-line metadata associated with a load-chart column. |
| Operating condition | Engineering condition label combining visual/line-count context. |
| Conservative duplicate fallback | Selection of the minimum positive capacity among duplicate numeric radius/boom entries. |
| Exact visual grid | A raw grid preserving visual duplicate columns rather than collapsing them. |
| Summary diagnostics | Lightweight GA diagnostics intended for ordinary runs. |
| Research diagnostics | Full/detailed diagnostics intended for deeper analysis. |
| Condition mismatch | Resolver failure where chart entries exist, but none match the requested line-count/condition constraint. |

---

## Appendix D — Suggested Repository Placement

Recommended filename:

```text
docs/implementation/load-chart-provenance-diagnostics.md
```

Alternative filename if implementation dossiers are stored by PR:

```text
docs/implementation/PR-63-load-chart-provenance-diagnostics.md
```

This location keeps the document separate from release notes while making it discoverable for maintainers.

---

## Appendix E — External Documentation Standards Consulted

This dossier’s structure is informed by publicly available engineering documentation guidance and reproducibility practices. These references shaped the *format* of the report; project-specific source code, diffs, verification logs, screenshots, and CranePathOptimizer documentation remain the authoritative basis for branch-specific conclusions.

| Reference | URL | Influence on This Dossier |
|---|---|---|
| Google Engineering Practices Documentation | `https://google.github.io/eng-practices/` | Reinforced reviewability, clarity, maintainability, and engineering audience assumptions. |
| Software Engineering at Google — Documentation chapter | `https://abseil.io/resources/swe-book/html/ch10.html` | Informed inclusion of goals, implementation strategy, key decisions, and tradeoffs. |
| Google Developer Documentation Style Guide | `https://developers.google.com/style` | Informed consistent technical language and developer-facing clarity. |
| Google Documentation Best Practices | `https://google.github.io/styleguide/docguide/best_practices.html` | Informed preference for useful, accurate, maintainable documentation rather than raw dump format. |
| Architectural Decision Records overview | `https://adr.github.io/` | Informed context/decision/consequence structure. |
| AWS Prescriptive Guidance — ADR process | `https://docs.aws.amazon.com/prescriptive-guidance/latest/architectural-decision-records/adr-process.html` | Informed decision-log and project-context framing. |
| Keep a Changelog | `https://keepachangelog.com/en/1.0.0/` | Informed human-readable grouping by change type/subsystem instead of raw git history. |
| The Turing Way — Guide for Reproducible Research | `https://book.the-turing-way.org/reproducible-research/reproducible-research/` | Informed reproducibility, rerunnable evidence, and environment/output capture sections. |
| FAIR Principles | `https://www.go-fair.org/fair-principles/` | Informed metadata/provenance emphasis for reusability and auditability. |
| NASA Software Engineering Requirements / documentation requirements | `https://nodis3.gsfc.nasa.gov/displayCA.cfm?Internal_ID=N_PR_7150_0002_&page_name=Chapter5` | Informed verification, configuration, evidence, and lifecycle-documentation framing. |
