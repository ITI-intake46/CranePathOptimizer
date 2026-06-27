# CranePathOptimizer Branch Implementation Dossier

## `feature/pdf-load-chart-metadata-evidence` - PDF Load Chart Extraction Robustness, Exact Visual Semantics, Corpus Regression Gate, and Validation Baseline

**Repository:** `ITI-intake46/CranePathOptimizer`
**Working branch observed:** `feature/pdf-load-chart-metadata-evidence`
**Recommended repository path:** `docs/implementation/pdf-load-chart-extraction-robustness-corpus-regression-dossier.md`
**Document type:** branch implementation dossier / architecture-impact record / validation dossier / reproducibility and maintainer handoff
**Document generated:** 2026-06-22
**Primary subsystem:** `CraneOptimizer.UI/Features/F4_LoadChart`
**Primary validation subsystem:** `CraneOptimizer.Application.Tests/LoadChart`
**Primary implementation theme:** robust, evidence-based PDF crane load-chart extraction with exact visual semantics, line-row preservation, context-aware continuation handling, full-corpus regression validation, and production-readiness reporting.
**Current validation status represented:** `dotnet build` passes; PDF corpus regression gate passes; 9/9 PDFs are found and imported; observed baseline rows equal 9; full solution test has classified non-PDF CoordinateReference/UI failures; PDF-related failures are zero.
**Baseline/golden-truth scope:** corpus baselines are observed regression baselines. Only `100Tons.pdf` pages 17/18/19 are manually golden-verified in this branch.
**Commit status at PR-preparation cleanup:** branch changes were committed and pushed before this documentation-only cleanup; verify the current final commit with `git log --oneline -n 3`.

---

## Documentation Strategy Decision

### Recommendation: create this new dossier and keep the older Markdown files as related historical context

The uploaded Markdown files are high-quality branch dossiers, but they document earlier and adjacent implementation layers rather than the exact current work. The safest documentation strategy is therefore to create a **new standalone dossier** for the current branch layer instead of overwriting one of the older reports.

This avoids contradictory guidance because the older documents remain accurate for their own scopes:

| Existing document | Scope represented | Recommended action |
|---|---|---|
| `pdf-load-chart-import-robustness-dossier.md` | Earlier PDF import UI robustness: visual reading order, continuation candidates, and source-preview toggle. | Keep as upstream/historical F4 PDF import context. |
| `pdf-load-chart-metadata-evidence-implementation-report.md` | PDF metadata extraction, evidence presentation, exact visual preservation, and metadata prefill. | Keep as upstream metadata/evidence context. |
| `load-chart-provenance-diagnostics.md` | Load-chart provenance propagation through GA diagnostics and output contracts. | Keep as downstream diagnostics/provenance context. |
| `ga-load-chart-operating-setup-selector-dossier.md` | F8 operating setup selector, inclusive line-count eligibility, usable-capacity coverage. | Keep as downstream GA runtime setup context. |

This document is the authoritative record for the **current robustness and validation layer**: the `100Tons.pdf` page 17/18/19 extractor defect, exact visual semantics, line-row expansion, page-specific golden expectations, corpus regression gating, observed baseline generation, and production-readiness classification.

---

## Table of Contents

1. [Purpose and Audience](#1-purpose-and-audience)
2. [Evidence Basis](#2-evidence-basis)
3. [Documentation Methodology](#3-documentation-methodology)
4. [Executive Summary](#4-executive-summary)
5. [Problem Statement](#5-problem-statement)
6. [Scope, Non-Scope, and Branch Boundaries](#6-scope-non-scope-and-branch-boundaries)
7. [Architecture Context](#7-architecture-context)
8. [Implementation Chronology](#8-implementation-chronology)
9. [Core Extraction Semantics Implemented](#9-core-extraction-semantics-implemented)
10. [`100Tons.pdf` Defect Resolution](#10-100tonspdf-defect-resolution)
11. [Full-Corpus Regression Gate](#11-full-corpus-regression-gate)
12. [Observed Baseline and Production-Readiness Reporting](#12-observed-baseline-and-production-readiness-reporting)
13. [File-by-File Implementation Ledger](#13-file-by-file-implementation-ledger)
14. [Subsystem Impact Assessment](#14-subsystem-impact-assessment)
15. [Runtime Behavior and User Workflow Impact](#15-runtime-behavior-and-user-workflow-impact)
16. [Diagnostics, Outputs, and Reproducibility](#16-diagnostics-outputs-and-reproducibility)
17. [Validation Record](#17-validation-record)
18. [Backward Compatibility and Migration Behavior](#18-backward-compatibility-and-migration-behavior)
19. [Technical Decisions and Tradeoffs](#19-technical-decisions-and-tradeoffs)
20. [Known Limitations and Open Risks](#20-known-limitations-and-open-risks)
21. [Maintainer and Reviewer Checklist](#21-maintainer-and-reviewer-checklist)
22. [Commit and Staging Guidance](#22-commit-and-staging-guidance)
23. [Appendices](#23-appendices)

---

## 1. Purpose and Audience

This dossier documents the current branch work that hardened the crane PDF load-chart extraction workflow from a successful targeted fix into a regression-protected, auditable subsystem. It is intended to be committed with the branch so future maintainers can understand exactly what changed, why it changed, how it was validated, and which remaining failures are intentionally outside the PDF extraction scope.

The intended audience includes:

- senior engineers reviewing the branch for merge readiness;
- maintainers of `CraneOptimizer.UI/Features/F4_LoadChart`;
- engineers extending PDF extraction, CSV load-chart parsing, or exact visual import behavior;
- QA engineers reproducing PDF corpus validation;
- researchers or collaborators relying on reproducible extraction outputs;
- future contributors who need to distinguish exact visual chart preservation from derived envelope/reference capacity lookup;
- GA and diagnostics maintainers who need to know what was and was not changed.

This document is intentionally more rigorous than release notes. It records architecture impact, validation evidence, implementation rationale, file-level changes, subsystem boundaries, limitations, and future maintenance rules.

---

## 2. Evidence Basis

### 2.1 Project-local evidence

This report is based on the following project-local evidence:

| Evidence | Role |
|---|---|
| Current branch screenshots and PowerShell outputs | Established implementation sequence, failures, successful gates, and final report status. |
| `CranePdfLineRowExpansionPatch_20260622_163544.zip` | Verified line-row span expansion and `100Tons.pdf` pages 17/18/19 targeted regression pass. |
| `CranePdfCorpusGateAssertionFix_20260622_172134.zip` | Verified corpus regression gate and corrected global-selection assertion behavior. |
| `CranePdfFullValidationAndBaselineReportV4_20260622_185423.zip` | Final validation/report evidence used as the authoritative current state. |
| `installed_100Tons.pages17-19.expected.json` | Page-specific golden expectations for the critical `100Tons.pdf` defect. |
| `installed_PdfCorpusRegressionGateTests.cs` | Installed permanent corpus regression gate. |
| `extractor_audit/*.audit.json` | Per-PDF full-scan and selected-candidate extraction evidence. |
| `observed_baseline/pdf_corpus_observed_baseline.*` | Observed, non-golden corpus baseline for nine PDFs. |
| `production_readiness/*` | Classified test failures and production-readiness status. |
| Uploaded older Markdown files | Related documentation context and formatting/style benchmark. |

### 2.2 Documentation standards consulted

The structure of this document follows professional branch-dossier practice: decision context, subsystem impact, reproducibility, file-by-file mapping, validation evidence, risks, and maintainer guidance. The format was informed by public engineering documentation practices including code-review maintainability guidance, ADR context/decision/consequence patterns, human-readable changelog grouping, and reproducible research documentation.

Project-specific evidence remains authoritative for branch-specific claims; external references shaped the documentation structure only.

---

## 3. Documentation Methodology

The document uses a layered documentation structure:

| Method | Why it is used here |
|---|---|
| Branch implementation dossier | Captures a complete change set rather than isolated release notes. |
| Architecture-impact record | Explains subsystem boundaries and non-impacts. |
| ADR-style decision notes | Documents why robust behavior was chosen and what alternatives were rejected. |
| Reproducibility report | Records commands, artifacts, corpus files, gates, and expected outputs. |
| File-by-file ledger | Makes maintenance ownership explicit. |
| Risk and limitation inventory | Prevents future contributors from overinterpreting the validation results. |

The document avoids treating observed extractor output as manually verified truth unless a golden file explicitly exists. This is critical for engineering integrity: observed baselines detect changes, while manually verified golden expectations prove correctness for specific facts.

---

## 4. Executive Summary

### 4.1 Branch outcome

This branch layer turns the PDF load-chart extractor into a more robust and auditable subsystem by implementing and validating:

1. **Exact visual chart semantics**
   - repeated boom-length columns are preserved as separate visual columns;
   - blank/unavailable cells remain blank/null in exact visual grids;
   - exact visual charts are distinguished from derived envelope/reference charts.

2. **Semantic row typing**
   - capacity radius rows, line-requirement rows, footer metadata rows, header rows, note rows, and separators are classified separately;
   - `Number of lines` rows are treated as load-chart metadata, not rated-capacity rows.

3. **Header/body robustness**
   - numeric body values must not be promoted to boom headers;
   - candidate warnings and diagnostics make invalid fragments visible without automatically recommending them.

4. **Context-aware continuation handling**
   - compatible side-by-side fragments can be merged into a combined continuation candidate;
   - incompatible fragments, especially those with conflicting counterweight context, remain separate.

5. **Line-row span expansion**
   - sparse `Number of lines` values centered over merged spans are expanded to every visual boom column they cover.

6. **Page-specific golden regression for `100Tons.pdf`**
   - page 17 separate left/right charts;
   - page 18 29-column combined continuation;
   - page 19 30-column combined continuation;
   - full line-count vectors.

7. **Full-corpus regression gate**
   - nine known crane PDFs are scanned and structurally validated;
   - audit JSON, summary CSV/JSON, and failure JSON are produced;
   - the corpus gate passes with zero PDF-related failures.

8. **Observed baseline and production-readiness report**
   - one observed baseline row per PDF;
   - full-suite failures are classified;
   - known non-PDF CoordinateReference/UI issues are isolated from PDF-extractor readiness.

### 4.2 Current validation status

Final V4 report status:

```text
SUCCESS_WITH_CLASSIFIED_NON_PDF_TEST_FAILURES
```

Key values:

| Signal | Value |
|---|---:|
| Build exit code | 0 |
| Full-solution test exit code | 1 |
| PDF corpus gate exit code | 0 |
| PDF corpus failure count | 0 |
| PDF count | 9 |
| Missing PDF count | 0 |
| Observed baseline rows | 9 |
| PDF-related full-suite failures | 0 |
| Classified non-PDF failures | 4 |

Interpretation:

```text
PDF extraction/corpus robustness is green.
The full solution is not globally green because known non-PDF CoordinateReference/UI failures remain.
Those failures are classified and intentionally not fixed by this branch layer.
```

### 4.3 Engineering judgment

The branch is appropriate to checkpoint and review as a PDF-extraction robustness milestone. The PDF-specific validation is strong for the available corpus, and the known non-PDF test failures are isolated. However, the branch should not be described as a fully green repository until the CoordinateReference/UI test failures are fixed in a separate, non-PDF change.

---

## 5. Problem Statement

### 5.1 Defect class

The original reported defect was not a single wrong cell. It represented a broader class of PDF table extraction failures:

| Defect | Engineering risk |
|---|---|
| Body capacity values promoted to headers | Incorrect boom columns and unsafe import candidate selection. |
| Side-by-side charts merged without context | Different counterweights or prerequisites could be combined incorrectly. |
| Continuation fragments not merged when compatible | Correct full charts could remain split and incomplete. |
| Duplicate boom headers collapsed | Manufacturer visual configurations could be lost. |
| Number-of-lines row treated as data or sparsely mapped | Reeving/parts-of-line semantics could be lost or assigned to too few columns. |
| Blank cells filled, shifted, or dropped | Unavailable capacities could become fabricated data. |
| Global selection assertions conflated with page-specific golden checks | A valid global best table could fail the regression gate for the wrong reason. |

### 5.2 Concrete `100Tons.pdf` failures

The critical user-supplied defect report focused on pages 17, 18, and 19 of `100Tons.pdf`:

- **Page 17** contains two separate charts with different counterweights (`16.5T` and `23.5T`) and must not be merged.
- **Page 18** contains two side-by-side continuation fragments under the same prerequisite context and should produce a combined 29-column chart.
- **Page 19** already had mostly correct combined continuation behavior, but it needed continued protection against regression and line-count mapping issues.

### 5.3 Broader requirement

The goal was not to memorize `100Tons.pdf`. The goal was to improve the extraction system along general, layout-aware principles:

```text
Use geometry, visual row semantics, chart context, and invariants.
Do not hardcode page numbers, table numbers, manufacturer templates, or one PDF layout.
Preserve exact visual evidence and make derived/envelope forms explicit.
Validate the full available corpus after each change.
```

---

## 6. Scope, Non-Scope, and Branch Boundaries

### 6.1 In scope

This branch layer includes:

- PDF load-chart candidate adjudication improvements;
- visual row semantic summaries;
- exact visual versus envelope/reference labeling;
- context-aware continuation validation;
- counterweight prerequisite context extraction for merge decisions;
- line-requirement row detection and full-column expansion;
- UI import-dialog wording and manual override safeguards;
- full-corpus regression gate;
- `100Tons.pdf` page-specific golden expectations;
- observed baseline report generation;
- production-readiness report generation;
- validation scripts and ZIP evidence generation.

### 6.2 Out of scope

The branch layer intentionally does **not** include:

| Area | Status |
|---|---|
| Genetic Algorithm algorithm redesign | Not changed. |
| GA mutation/crossover/selection/scoring | Not changed. |
| CoordinateReference behavior | Not changed; failures are classified as non-PDF. |
| UI resource fix for `BoolToVisibilityConverter` | Not changed; classified as non-PDF. |
| Database schema / EF migrations | Not changed by this layer. |
| Installer / deployment packaging | Not changed. |
| NuGet dependencies | Not changed. |
| Streamlit UI | No Streamlit impact detected. |
| OCR/scanned-image PDF support | Not added. |
| Manufacturer capacity certification | Not added; imported manufacturer values remain authoritative. |

### 6.3 Branch boundary statement

This branch layer changes how PDF/CSV load-chart data is parsed, represented, validated, reviewed, and regression-gated. It does not change how the GA solves crane placement, and it does not resolve unrelated CoordinateReference/UI test failures.

---

## 7. Architecture Context

### 7.1 Relevant layers

The affected architecture is concentrated in the F4 Load Chart feature and application test project:

```text
CraneOptimizer.UI
  Features/F4_LoadChart
    Services
      LoadChartCsvService.cs
      LoadChartPdfService.cs
    Views
      PdfLoadChartImportDialog.xaml.cs

CraneOptimizer.Application.Tests
  LoadChart
    Golden/100Tons.pages17-19.expected.json
    PdfCorpusRegressionGateTests.cs
    PdfLoadChartLineRequirementPrecisionTests.cs
    PdfLoadChartPrimarySelectionTests.cs
    PdfLoadChartSemanticRowsTests.cs
```

### 7.2 Architectural responsibility boundaries

| Concern | Location | Rationale |
|---|---|---|
| PDF candidate scanning/adjudication | `LoadChartPdfService.cs` | Extraction and candidate logic belong in the F4 PDF service. |
| CSV/parsed-chart semantic row summaries | `LoadChartCsvService.cs` | Parsed chart data is constructed in the CSV/import conversion service. |
| UI messages, manual override, status notes | `PdfLoadChartImportDialog.xaml.cs` | Presentation and review workflow belong in the dialog layer. |
| Golden and corpus regression tests | `CraneOptimizer.Application.Tests/LoadChart` | Regression behavior must be testable and permanent. |
| Observed baseline and readiness reports | PowerShell validation scripts / generated artifacts | These are reproducibility artifacts, not production runtime behavior. |

### 7.3 Architecture preservation

The branch preserves separation of concerns:

- parser/adjudication logic remains in services;
- UI review wording remains in UI code-behind;
- regression criteria remain in tests/golden JSON;
- corpus audit artifacts are generated outside runtime flow;
- no GA optimization internals were moved into F4 import logic;
- no database/persistence layer was affected by the current robustness layer.

---

## 8. Implementation Chronology

This section records the important implementation sequence because it explains why some decisions exist and prevents future contributors from repeating failed validation assumptions.

| Stage | Outcome | Engineering lesson |
|---|---|---|
| Initial extractor audit | Current extractor outputs were captured across nine PDFs. | Need current observed behavior before patching. |
| `100Tons.pdf` defect analysis | Pages 17/18/19 were manually specified with expected grids and line vectors. | Golden truth must be explicit for high-risk defects. |
| Robust extraction patch | Structural behavior improved but regression test initially failed due test-script parsing of blank line-row cells. | Test scripts must safely parse blanks/nulls. |
| Post-patch audit V3 | Page 17 split and Page 18/Page 19 continuation behavior mostly fixed, but line vectors were sparse. | Remaining issue was line-row span expansion, not table structure. |
| Line-row expansion patch | Passed targeted regression for pages 17/18/19. | Sparse line markers must expand across visual column spans. |
| Corpus regression gate | Initial gate failed due overly strict global-selection assertion on `100Tons.pdf`. | Page-specific golden checks should not force global best candidate. |
| Corpus gate assertion fix | Build and corpus gate passed. | Global selected candidate only needs structural validity unless golden truth says otherwise. |
| Full validation/report V1/V2/V3 | Reporting scripts exposed PowerShell collection and TRX/reporting edge cases. | Validation tooling must be robust and deterministic. |
| Full validation/report V4 | Final acceptable status: PDF corpus green, baseline rows correct, full-suite failures classified non-PDF. | PDF work is isolated and auditable; remaining repo failures are outside this scope. |

---

## 9. Core Extraction Semantics Implemented

### 9.1 Exact visual chart preservation

Exact visual import preserves repeated manufacturer columns. Repeated headers are not duplicates to discard; they are separate visual/load-configuration columns.

Example preserved pattern:

```text
17.50, 17.50, 17.50, 17.50
21.80, 21.80, 21.80, 21.80
26.10, 26.10, 26.10, 26.10, 26.10
```

The branch keeps exact visual representation distinct from any conservative envelope/reference form. This matters because optimization may derive conservative lookup behavior, but the import review UI must be able to show the manufacturer's visual chart faithfully.

### 9.2 Semantic visual row typing

The import path now models row intent explicitly. Rows can be classified as:

| Row kind | Meaning |
|---|---|
| `CapacityRadiusRow` | A radius row participating in capacity lookup. |
| `LineRequirementRow` | A `Number of lines` / reeving / parts-of-line metadata row. |
| `FooterMetadataRow` | Footer information, not capacity data. |
| `HeaderRow` | Header labels such as radius/main boom. |
| `NoteRow` | Notes, prerequisites, counterweight, outrigger, or textual remarks. |
| `SeparatorRow` | Blank/separator row. |

This prevents semantic rows from being counted or imported as rated capacities.

### 9.3 Header-band and body-fragment protection

The previous failure mode treated body rows such as `4.5, 52.0, 45.0, 30.0, 49.0` as boom headers. The implemented corrections make this visibly and structurally wrong:

- a header must be compatible with radius/body geometry;
- a row inside the capacity body is not a valid header band;
- small fragments near the bottom or line-row area are demoted or require manual override review;
- UI notes warn when the current selection is not the recommended import candidate.

### 9.4 Context-aware continuation

Continuation merging is allowed only when candidate context is compatible. The branch uses prerequisite context such as counterweight to prevent invalid merges.

| Case | Correct behavior |
|---|---|
| Same context, side-by-side continuation | Generate combined continuation candidate. |
| Different counterweight/prerequisite context | Keep charts separate. |
| Ambiguous or fragmentary candidate | Keep visible for diagnostics, do not recommend automatically. |

### 9.5 Number-of-lines span expansion

PDF charts often place a single line-count number centered under a group of columns. The correct mapping is span-based:

```text
line-count token -> all boom columns whose centers fall within the token's visual span
```

The incorrect mapping was nearest-column only:

```text
line-count token -> one nearest boom column
```

The patch expands line-count values across visual column groups, producing full vectors such as:

```text
Page 18:
6,6,6,6,5,5,5,5,5,5,4,4,4,3,3,3,3,3,3,3,3,3,3,3,3,2,2,2,2
```

---

## 10. `100Tons.pdf` Defect Resolution

The manually verified golden file records the critical expectations for pages 17, 18, and 19.

### 10.1 Page 17

Expected behavior:

- two separate charts;
- left chart has counterweight `16.5T`;
- right chart has counterweight `23.5T`;
- charts must not be merged.

| Chart | Counterweight | Boom columns | Numeric cells | Line vector |
|---|---:|---:|---:|---|
| Left | 16.5T | 14 | 242 | `3,3,3,3,3,3,3,3,3,3,2,2,2,2` |
| Right | 23.5T | 15 | 190 | `12,11,11,11,11,9,9,9,9,8,8,8,8,8,6` |

### 10.2 Page 18

Expected behavior:

- combined continuation candidate exists;
- 29 visual boom columns;
- first boom header begins with 30.50;
- 493 numeric rated-capacity cells;
- full line vector is expanded across all columns.

```text
Page 18 line vector:
6,6,6,6,5,5,5,5,5,5,4,4,4,3,3,3,3,3,3,3,3,3,3,3,3,2,2,2,2
```

### 10.3 Page 19

Expected behavior:

- combined continuation candidate exists;
- 30 visual boom columns;
- 430 numeric rated-capacity cells;
- duplicate boom headers are preserved;
- full line vector is expanded across all columns.

```text
Page 19 line vector:
12,11,11,11,11,9,9,9,9,8,8,8,8,8,6,6,6,6,6,5,5,5,5,5,5,4,4,4,3,3
```

### 10.4 Golden scope limitation

The golden file proves specific page-level facts for `100Tons.pdf`. It does not by itself certify every cell in every other PDF. The corpus baseline for other PDFs is currently observed baseline, not manually verified ground truth.

---

## 11. Full-Corpus Regression Gate

### 11.1 Purpose

The corpus gate prevents a fix for one PDF from silently degrading other crane PDFs. It also creates durable audit artifacts for every run.

### 11.2 PDFs covered

The current gate covers nine PDFs:

- `100Tons-Liebherr.pdf`
- `100Tons.pdf`
- `12_Seiter_Prospekt_SRC550C_eng.pdf`
- `130Tons.pdf`
- `180Tons.pdf`
- `200 t cap.pdf`
- `200Tons.pdf`
- `70Tons-Liebherr.pdf`
- `80Tons.pdf`

### 11.3 Hard checks

The gate validates:

- every expected PDF is present;
- extractor returns page candidates;
- extractor returns table candidates;
- selected candidate exists;
- selected candidate has columns, radius rows, cells, and raw grid;
- import succeeds;
- `100Tons.pdf` page-specific golden expectations are satisfied;
- line-row vectors are fully expanded where golden checks require them;
- corpus failure JSON is empty.

### 11.4 Global selection correction

An early corpus gate incorrectly asserted that the global selected candidate for `100Tons.pdf` had to be Page 19. The current gate correctly treats pages 17/18/19 as page-specific golden checks, while allowing the full-PDF global selected candidate to be another structurally valid and importable chart.

Current observed global selected candidate for `100Tons.pdf`:

```text
Page 25, Table 3, 30 columns, 33 radius rows, 429 numeric cells, combined continuation.
```

This is recorded as observed behavior, not a failure.

---

## 12. Observed Baseline and Production-Readiness Reporting

### 12.1 Observed baseline policy

Observed baselines are snapshots of current extractor behavior. They are useful for detecting future behavior changes, but they are not equivalent to manual golden truth.

Current status:

```text
ObservedBaselineRows: 9
PdfCount: 9
```

### 12.2 Observed corpus baseline

| PDF | Selected | Cols | Radius rows | Cells | Max | Line vector | Combined | Exact visual | Import |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 100Tons-Liebherr.pdf | P11 / T1 | 18 | 31 | 314 | 100 |  | False | True | True |
| 100Tons.pdf | P25 / T3 | 30 | 33 | 429 | 90 | 12;11;11;11;11;9;9;9;9;8;8;8;8;8;6;6;6;6;6;5;5;5;5;5;5;... | True | True | True |
| 12_Seiter_Prospekt_SRC550C_eng.pdf | P7 / T1 | 7 | 25 | 111 | 55 | 10;10;8;8;4;4;3 | False | False | True |
| 130Tons.pdf | P15 / T3 | 29 | 33 | 473 | 130 | 12;10;10;10;8;8;8;6;6;6;6;6;6;5;5;5;4;4;4;3;3;3;3;3;3;2... | True | True | True |
| 180Tons.pdf | P12 / T1 | 13 | 38 | 245 | 180 |  | False | True | True |
| 200 t cap.pdf | P2 / T1 | 15 | 39 | 274 | 200 |  | False | True | True |
| 200Tons.pdf | P4 / T1 | 17 | 42 | 360 | 200 |  | False | True | True |
| 70Tons-Liebherr.pdf | P11 / T1 | 13 | 27 | 191 | 70 |  | False | True | True |
| 80Tons.pdf | P7 / T1 | 6 | 26 | 94 | 80 | 12;9;9;6;5;3 | False | False | True |

### 12.3 Production-readiness status

```text
Status: SUCCESS_WITH_CLASSIFIED_NON_PDF_TEST_FAILURES
FailureMessage: Full suite has classified non-PDF failure(s). PDF corpus gate passed.
```

The report intentionally distinguishes PDF readiness from full-repository readiness:

| Category | Count |
|---|---:|
| PDF corpus failures | 0 |
| PDF-related full-suite failures | 0 |
| Known CoordinateReference failures | 2 |
| Other non-PDF entries | 2 |
| Infrastructure failures | 0 |

### 12.4 Classified full-suite issues

| Test / diagnostic | Outcome | Classification | Message summary |
| --- | --- | --- | --- |
| CraneOptimizer.Application.Tests.RenderPerformanceBenchmark.ExecuteBenchmarkSuite | NotExecuted | OtherNonPdf | Exempt from standard test runs to prevent heavy local-file benchmarks unless explicitly intended. |
| CraneOptimizer.Application.Tests.Diagnostics.CoordinateReferenceTests.Test_62_GAResultPresentationDtoShowsSurveyWhenActive | Failed | KnownUnrelatedCoordinateReference | Assert.Contains() failure: expected survey/easting text; actual output remained in `Local X: 10.00, Local Y: 20.00` text. |
| CraneOptimizer.Application.Tests.RenderPerformanceBenchmark.ExecuteHitTestAndSnapBenchmark | NotExecuted | OtherNonPdf | Exempt from standard test runs to prevent heavy local-file benchmarks unless explicitly intended. |
| CoordinateReference UI/XAML test-host issue inferred from output | OutputDiagnostic | KnownUnrelatedCoordinateReference | Non-PDF UI/XAML test-host diagnostic involving `BoolToVisibilityConverter` / `XamlParseException` / `IFCLoadOptionsDialog`; intentionally not changed by this PDF-focused branch. |

The full-suite non-PDF failures remain real repository issues. They are not fixed by this branch and should be addressed separately.

---

## 13. File-by-File Implementation Ledger

### 13.1 `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCsvService.cs`

**Change type:** parsed chart semantics / visual-row summaries.

**What changed:**

- added `LoadChartVisualRowKind`;
- added `LoadChartVisualRowSummary`;
- added `LoadChartVisualChartSummary`;
- added row classification helpers;
- added numeric capacity versus blank/unavailable cell counting;
- added line-requirement row metrics;
- attached visual summaries and row summaries to parsed chart output.

**Why it changed:**

The extractor needed to distinguish capacity rows from operational metadata rows. Without this distinction, rows such as `Number of lines` could be miscounted as capacity data or omitted from audit semantics.

**Downstream impact:**

- UI can present exact visual chart semantics more accurately;
- tests can assert row-level semantics;
- imported exact visual grids can preserve operational rows without contaminating capacity lookup.

### 13.2 `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs`

**Change type:** PDF candidate adjudication, context-aware continuation, exact visual semantics, diagnostics.

**What changed:**

- `GetBestTable()` now routes through `SelectBestTable(...)` rather than raw score-only ordering;
- primary chart adjudication considers structural completeness and nominal-capacity evidence;
- candidate summaries distinguish visual boom columns and capacity radius rows;
- exact visual chart summaries distinguish capacity rows from line-requirement rows;
- envelope/reference fragments are labeled separately from exact visual combined candidates;
- nearby prerequisite context can be scanned and used for warnings/continuation compatibility;
- counterweight context extraction blocks invalid continuation merges;
- line-row construction uses span expansion across visual columns.

**Why it changed:**

The score-only and fragment-tolerant behavior was not sufficient for complex manufacturer PDFs. The system needed context-aware selection, exact visual preservation, and line-row semantics that match engineering chart meaning.

**Downstream impact:**

- selected PDF candidates are more reliable;
- invalid fragments are less likely to be recommended;
- exact visual duplicate columns remain preserved;
- full corpus gate can validate selected/imported output consistently;
- UI receives better warning and summary text.

### 13.3 `CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs`

**Change type:** UI review text, manual override safeguards, semantic diagnostics display.

**What changed:**

- candidate details include line-requirement precision phrases;
- selection summary text distinguishes exact visual, envelope/reference, and combined continuation candidates;
- bottom status uses safer language about generated capacity lookup points and exact visual semantics;
- manual override warnings are shown for envelope/reference fragments and non-recommended selections;
- selection context lines explain recommendation status and review implications.

**Why it changed:**

The UI must help engineers understand whether they are importing the recommended exact visual chart, a combined continuation, an envelope/reference fragment, or a manual override candidate.

**Downstream impact:**

- users get clearer review prompts;
- import remains possible but riskier selections are explicitly flagged;
- the UI no longer hides important candidate-quality distinctions.

### 13.4 `CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartLineRequirementPrecisionTests.cs`

**Change type:** regression tests.

**Purpose:** protects line-row detection and full vector expansion behavior.

**Maintenance note:** future changes to line-row mapping must keep exact visual column coverage intact.

### 13.5 `CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartPrimarySelectionTests.cs`

**Change type:** regression tests.

**Purpose:** protects primary chart adjudication and prevents malformed fragments from becoming recommendations.

### 13.6 `CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartSemanticRowsTests.cs`

**Change type:** regression tests.

**Purpose:** protects semantic row classification and capacity-versus-line-row separation.

### 13.7 `CraneOptimizer.Application.Tests/LoadChart/PdfCorpusRegressionGateTests.cs`

**Change type:** permanent corpus gate.

**Purpose:** runs the extractor across the full available corpus and applies both structural checks and `100Tons.pdf` page-specific golden expectations.

**Important behavior:** full-PDF global selection is not forced to Page 19. Page 17/18/19 are validated as page-specific golden facts.

### 13.8 `CraneOptimizer.Application.Tests/LoadChart/Golden/100Tons.pages17-19.expected.json`

**Change type:** golden ground-truth artifact.

**Purpose:** records manually verified expectations for the critical `100Tons.pdf` pages 17, 18, and 19 defect.

**Maintenance note:** this file should not be regenerated blindly from observed extractor output. It is a manually verified contract.

---

## 14. Subsystem Impact Assessment

### 14.1 PDF engine / F4 Load Chart import

**Impact:** direct.

The F4 PDF extraction path is the primary affected subsystem. It now carries stronger semantics around exact visual charts, row types, continuation, warnings, and selected-candidate adjudication.

### 14.2 CSV load-chart import

**Impact:** direct but aligned with PDF semantics.

`LoadChartCsvService.cs` now supports visual row summaries and semantic typing, which helps exact visual and line-requirement semantics remain consistent across CSV/text-based import outputs.

### 14.3 WPF F4 import UI

**Impact:** direct.

The import dialog code-behind has clearer candidate status text, manual override classification, and recommendation handling. The XAML itself is not listed as modified in the final diff for this layer, but the code-behind presentation logic is modified.

### 14.4 Genetic Algorithm / optimization engine

**Impact:** indirect, not algorithmic.

The GA benefits from more correct imported load-chart data, especially preserved duplicate columns and line-count metadata. However, this branch does not change:

- population generation;
- mutation;
- crossover;
- objective/fitness functions;
- placement engine behavior;
- capacity resolver logic in the Application GA layer.

### 14.5 Diagnostics and verification logic

**Impact:** direct for validation artifacts, not production GA output schema.

The branch adds a permanent corpus gate and generated audit reports. It does not change the existing GA diagnostics output schema in this layer.

### 14.6 Streamlit UI

**Impact:** none detected.

No Streamlit files or workflows are modified. The affected UI is WPF under `CraneOptimizer.UI`.

### 14.7 Installers and deployment

**Impact:** no production installer impact.

PowerShell scripts were used to patch and validate the branch in the local working tree. They are validation/installation artifacts for this branch workflow, not production installers or deployment manifests.

### 14.8 Dependencies

**Impact:** none detected.

No `.csproj`, package, or NuGet dependency files are modified by this layer. Existing xBIM compatibility warnings remain known warnings and are not introduced by this work.

### 14.9 Database and migrations

**Impact:** none detected.

No EF migrations, model snapshots, or database schema files are modified by the final diff.

### 14.10 Runtime behavior

**Impact:** direct for PDF load-chart import review and selection; indirect for downstream load-chart usage.

When users import PDFs, they will see improved candidate recommendations, clearer warnings, and better exact visual/line-row behavior. Downstream systems receive more accurate imported chart data.

---

## 15. Runtime Behavior and User Workflow Impact

### 15.1 Candidate selection

Users should expect recommended candidates to be more strongly adjudicated. Fragmentary or envelope/reference candidates can still be reviewed, but they are no longer indistinguishable from exact visual primary candidates.

### 15.2 Manual override

Manual override remains possible, but the UI now warns when the selected candidate is not recommended or represents an envelope/reference fragment. This is intentional: expert users retain control, but risky imports are more explicit.

### 15.3 Exact visual charts

Exact visual charts preserve duplicate boom columns and unavailable cells. This is necessary for auditability and manufacturer-chart fidelity.

### 15.4 Capacity lookup

Capacity lookup should use rated-capacity cells only. `Number of lines` rows are metadata and must not become capacity points.

---

## 16. Diagnostics, Outputs, and Reproducibility

### 16.1 Generated artifacts

Final V4 validation generates:

```text
logs/dotnet_build_full_validation.*
logs/dotnet_test_full_solution.*
logs/dotnet_test_pdf_corpus_regression_gate.*
test_results/full_solution/full_solution.trx
test_results/pdf_corpus_gate/pdf_corpus_regression_gate.trx
extractor_audit/*.audit.json
extractor_audit/_corpus_summary.audit.json
extractor_audit/_corpus_summary.audit.csv
extractor_audit/_corpus_failures.audit.json
observed_baseline/pdf_corpus_observed_baseline.json
observed_baseline/pdf_corpus_observed_baseline.csv
production_readiness/classified_test_failures.json
production_readiness/classified_test_failures.csv
production_readiness/production_readiness.json
production_readiness/production_readiness.md
repo_state/git_status_after.txt
repo_state/git_diff_after.patch
```

### 16.2 Command results

| Command | Exit code |
| --- | --- |
| dotnet_build_full_validation | 0 |
| dotnet_test_full_solution | 1 |
| dotnet_test_pdf_corpus_regression_gate | 0 |

### 16.3 Reproduction commands

```powershell
cd C:\Users\Bahyg\Source\Repos\CranePathOptimizer
powershell.exe -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\Install_CranePdfFullValidationAndBaselineReport_V4.ps1"
```

For direct build/test verification:

```powershell
dotnet build CraneOptimizer.sln -c Debug
dotnet test CraneOptimizer.sln -c Debug --logger "trx;LogFileName=full_solution.trx"
dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj `
  -c Debug `
  --filter "FullyQualifiedName~PdfCorpusRegressionGateTests" `
  --logger "trx;LogFileName=pdf_corpus_regression_gate.trx"
```

### 16.4 Reproducibility caveat

The exact PDF corpus is required for the corpus gate. The final report copied all nine PDFs into the validation ZIP. If future contributors run the gate without the corpus files, the gate should fail early and report missing PDFs rather than silently shrinking coverage.

---

## 17. Validation Record

### 17.1 Final V4 status

```text
Status: SUCCESS_WITH_CLASSIFIED_NON_PDF_TEST_FAILURES
BuildExitCode: 0
FullTestExitCode: 1
PdfCorpusGateExitCode: 0
PdfCorpusFailureCount: 0
PdfRelatedFailureCount: 0
```

### 17.2 PDF corpus gate validation

The PDF corpus gate passed:

```text
dotnet_test_pdf_corpus_regression_gate exit code: 0
_corpus_failures.audit.json: []
```

### 17.3 Full solution validation

The full solution test returned exit code `1`, but failures were classified as non-PDF:

- known CoordinateReference assertion failure;
- CoordinateReference/UI XAML diagnostic involving `BoolToVisibilityConverter` / `XamlParseException`;
- skipped/not executed benchmark entries.

### 17.4 Validation conclusion

PDF extraction is green for the current corpus and golden tests. The repository is not fully green until the non-PDF CoordinateReference/UI issues are fixed separately.

---

## 18. Backward Compatibility and Migration Behavior

| Concern | Assessment |
|---|---|
| Existing projects | No database/schema migration in this layer. |
| Existing load charts | Existing imports remain valid; new semantics affect review and diagnostics. |
| Exact visual charts | More information is preserved, not less. |
| Envelope/reference charts | Still available but labeled distinctly. |
| GA optimizer | No direct algorithm or settings changes. |
| Full test suite | Existing non-PDF failures remain isolated. |

---

## 19. Technical Decisions and Tradeoffs

### 19.1 Decision: create a new dossier rather than update older files

**Context:** Existing Markdown documents describe adjacent but distinct implementation layers.

**Decision:** Create a new dossier for the current PDF robustness and corpus gate layer.

**Consequence:** Older documents remain valid historical context. Future contributors can identify which document applies to which branch layer.

### 19.2 Decision: preserve duplicate visual columns

**Context:** Manufacturer PDFs repeat boom lengths for different visual/load configurations.

**Decision:** Exact visual mode preserves repeated columns.

**Consequence:** Import grids are more faithful and auditable, but derived optimization/envelope views must be explicitly labeled.

### 19.3 Decision: treat line rows as metadata

**Context:** `Number of lines` rows are operational semantics, not capacities.

**Decision:** Classify and map line rows separately.

**Consequence:** Capacity counts remain clean, and reeving metadata is preserved.

### 19.4 Decision: use context-aware continuation

**Context:** Some side-by-side charts are continuations; others are separate charts with different counterweights.

**Decision:** Merge only when prerequisite context is compatible.

**Consequence:** False merges are reduced; ambiguous cases may require manual review.

### 19.5 Decision: corpus gate validates page-specific golden facts, not every global selected page

**Context:** `100Tons.pdf` global selected candidate may be a later valid chart, while pages 17/18/19 are defect-specific golden checks.

**Decision:** Treat page 17/18/19 checks as page-specific; require global selected candidate to be structurally valid and importable.

**Consequence:** The gate protects the defect without creating a false failure on valid global selection.

### 19.6 Decision: observed baseline is not golden truth

**Context:** Automatically generated outputs are useful but not manually verified.

**Decision:** Mark corpus baseline rows `ObservedNotManuallyVerified`.

**Consequence:** Future diffs can be detected, but manual review is required before elevating a PDF to golden truth.

---

## 20. Known Limitations and Open Risks

1. **Full solution test is not globally green.**
   Non-PDF CoordinateReference/UI failures remain and should be fixed separately.

2. **Only `100Tons.pdf` pages 17/18/19 have manual golden truth.**
   Other PDFs have observed baselines, not manually verified ground truth.

3. **The corpus is finite.**
   Passing nine PDFs does not prove all crane PDF layouts are solved.

4. **Text/layout PDF assumption remains.**
   This work does not add OCR/scanned-PDF support.

5. **Exact visual and derived envelope semantics must remain separate.**
   Future work must not collapse this distinction in UI or tests.

6. **Nominal capacity inference is evidence, not authority.**
   Filename/model capacity evidence can assist adjudication but must not become a template rule.

---

## 21. Maintainer and Reviewer Checklist

Before merging or building on this branch, verify:

- [ ] `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs` changes are reviewed for generality and absence of PDF-specific hardcoding.
- [ ] `LoadChartCsvService.cs` semantic row changes are reviewed against CSV import workflows.
- [ ] UI warnings in `PdfLoadChartImportDialog.xaml.cs` are reviewed for clarity.
- [ ] `100Tons.pages17-19.expected.json` is treated as manual golden truth, not generated output.
- [ ] `PdfCorpusRegressionGateTests.cs` is committed with the golden file.
- [ ] Observed baseline files are not confused with golden truth.
- [ ] PDF corpus gate passes.
- [ ] Full-solution non-PDF failures are either accepted as out-of-scope or fixed in a separate branch.
- [ ] Local ZIP/log artifacts are not accidentally committed.

---

## 22. Commit and Staging Guidance

### 22.1 Current working tree summary

```text
## feature/pdf-load-chart-metadata-evidence...origin/feature/pdf-load-chart-metadata-evidence
 M CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCsvService.cs
 M CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
 M CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
?? CraneOptimizer.Application.Tests/LoadChart/Golden/
?? CraneOptimizer.Application.Tests/LoadChart/PdfCorpusRegressionGateTests.cs
?? CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartLineRequirementPrecisionTests.cs
?? CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartPrimarySelectionTests.cs
?? CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartSemanticRowsTests.cs
```

Diff stat:

```text
.../F4_LoadChart/Services/LoadChartCsvService.cs   | 234 +++++-
 .../F4_LoadChart/Services/LoadChartPdfService.cs   | 790 +++++++++++++++++++--
 .../Views/PdfLoadChartImportDialog.xaml.cs         | 357 +++++++++-
 3 files changed, 1281 insertions(+), 100 deletions(-)
```

### 22.2 Intended files to stage

```bash
git add CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCsvService.cs
git add CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfService.cs
git add CraneOptimizer.UI/Features/F4_LoadChart/Views/PdfLoadChartImportDialog.xaml.cs
git add CraneOptimizer.Application.Tests/LoadChart/Golden/100Tons.pages17-19.expected.json
git add CraneOptimizer.Application.Tests/LoadChart/PdfCorpusRegressionGateTests.cs
git add CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartLineRequirementPrecisionTests.cs
git add CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartPrimarySelectionTests.cs
git add CraneOptimizer.Application.Tests/LoadChart/PdfLoadChartSemanticRowsTests.cs
git add docs/implementation/pdf-load-chart-extraction-robustness-corpus-regression-dossier.md
```

### 22.3 Do not stage unless intentionally required

Avoid staging generated local artifacts:

```text
CranePdf*.zip
CranePdf*/
Downloads validation logs
local backup folders
```

### 22.4 Suggested commit message

```text
feat(load-chart): harden PDF extraction and add corpus regression gate

- preserve exact visual load-chart semantics and semantic row summaries
- prevent line-requirement rows from being treated as capacity data
- expand number-of-lines spans across visual boom columns
- improve primary candidate adjudication and continuation context checks
- validate 100Tons pages 17-19 with golden expectations
- add full PDF corpus regression gate and observed baseline reporting
- classify non-PDF full-suite failures separately from PDF readiness
```

---

## 23. Appendices

### Appendix A - Corpus baseline details

The observed baseline CSV and JSON are generated by the V4 validation report. They should be used to detect future changes in extractor behavior. They should not be treated as manually verified correctness for all PDFs.

### Appendix B - Final readiness JSON

```json
{
  "Status": "SUCCESS_WITH_CLASSIFIED_NON_PDF_TEST_FAILURES",
  "FailureMessage": "Full suite has classified non-PDF failure(s). PDF corpus gate passed.",
  "GeneratedAt": "2026-06-22T18:55:45.9831153+03:00",
  "RepoRoot": "C:\\Users\\Bahyg\\Source\\Repos\\CranePathOptimizer",
  "LogRoot": "C:\\Users\\Bahyg\\Downloads\\CranePdfFullValidationAndBaselineReportV4_20260622_185423",
  "BuildExitCode": 0,
  "FullTestExitCode": 1,
  "PdfCorpusGateExitCode": 0,
  "PdfCorpusFailureCount": 0,
  "PdfCount": 9,
  "MissingPdfCount": 0,
  "ObservedBaselineRows": 9,
  "ClassifiedFailureCount": 4,
  "PdfRelatedFailureCount": 0,
  "KnownCoordinateReferenceFailureCount": 2,
  "OtherNonPdfFailureCount": 2,
  "InfrastructureFailureCount": 0,
  "Notes": [
    "This V4 report does not change extractor logic.",
    "Observed baselines are not manually verified golden truth except where the existing corpus gate applies 100Tons page-specific golden checks.",
    "Known CoordinateReference/UI-test failures are classified as non-PDF and intentionally not fixed here."
  ]
}
```

### Appendix C - Golden expectation JSON

```json
{
  "pdf": "100Tons.pdf",
  "purpose": "Golden manually verified expectations for the critical pages 17, 18, and 19 load-chart extraction defect.",
  "page17": {
    "mustNotCombineLeftRight": true,
    "left": {
      "counterweightTons": 16.5,
      "boomColumns": 14,
      "numericCapacityCells": 242,
      "numberOfLines": [
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        2,
        2,
        2,
        2
      ]
    },
    "right": {
      "counterweightTons": 23.5,
      "boomColumns": 15,
      "numericCapacityCells": 190,
      "numberOfLines": [
        12,
        11,
        11,
        11,
        11,
        9,
        9,
        9,
        9,
        8,
        8,
        8,
        8,
        8,
        6
      ]
    }
  },
  "page18": {
    "combinedContinuation": true,
    "boomColumns": 29,
    "numericCapacityCells": 493,
    "firstBoomHeader": 30.5,
    "numberOfLines": [
      6,
      6,
      6,
      6,
      5,
      5,
      5,
      5,
      5,
      5,
      4,
      4,
      4,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      3,
      2,
      2,
      2,
      2
    ]
  },
  "page19": {
    "selectedCombinedContinuation": true,
    "boomColumns": 30,
    "numericCapacityCells": 430,
    "numberOfLines": [
      12,
      11,
      11,
      11,
      11,
      9,
      9,
      9,
      9,
      8,
      8,
      8,
      8,
      8,
      6,
      6,
      6,
      6,
      6,
      5,
      5,
      5,
      5,
      5,
      5,
      4,
      4,
      4,
      3,
      3
    ]
  }
}
```

### Appendix D - External documentation references consulted

These references informed the structure and documentation method:

| Reference | URL | Influence |
|---|---|---|
| Google Engineering Practices | https://google.github.io/eng-practices/ | Reviewability, maintainability, long-term code health. |
| Google Developer Documentation Style Guide | https://developers.google.com/style | Clear technical language and developer-facing consistency. |
| Architecture Decision Records | https://adr.github.io/ | Decision/context/consequence framing. |
| AWS ADR best practices | https://aws.amazon.com/blogs/architecture/master-architecture-decision-records-adrs-best-practices-for-effective-decision-making/ | Architecture decision communication and collaboration. |
| Keep a Changelog | https://keepachangelog.com/en/1.1.0/ | Human-readable grouping by meaningful change category. |
| The Turing Way - Reproducible Research | https://book.the-turing-way.org/reproducible-research/reproducible-research/ | Reproducibility, evidence capture, rerunnable workflows. |

### Appendix E - Glossary

| Term | Meaning |
|---|---|
| Exact visual chart | Import representation preserving manufacturer visual columns, including repeated headers and blank cells. |
| Envelope/reference chart | Derived or consolidated capacity view useful for review/optimization, but not the exact visual source table. |
| Visual boom column | A column as it appears in the source chart, even if its numeric boom length repeats. |
| Capacity radius row | A row whose first label is a radius and whose cells represent rated capacities or blanks. |
| Line requirement row | A row such as `Number of lines`, `parts of line`, or `reeving`; operational metadata, not capacity data. |
| Combined continuation | A generated candidate that merges compatible side-by-side fragments into one logical chart. |
| Prerequisite context | Nearby chart text such as counterweight, outriggers, rotation, and boom operating conditions. |
| Golden truth | Manually verified expected output. |
| Observed baseline | Current extractor output captured for change detection but not manually verified. |
| Classified non-PDF failure | A full-suite failure not caused by PDF extraction and intentionally outside this branch layer. |

---

## Final Statement

This branch establishes a robust, auditable PDF load-chart extraction validation layer. The current corpus gate passes, the critical `100Tons.pdf` page 17/18/19 defect is protected by golden expectations, exact visual semantics and line-row metadata are preserved, and observed baselines are generated for all nine available PDFs. The remaining full-suite failures are classified as non-PDF CoordinateReference/UI issues and should be addressed separately.
