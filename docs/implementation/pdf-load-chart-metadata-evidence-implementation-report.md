# CranePathOptimizer Branch Implementation Report

## `feature/pdf-load-chart-metadata-evidence` — PDF Load Chart Metadata Extraction, Exact Visual Chart Preservation, and Professional Evidence Presentation

**Repository:** `ITI-intake46/CranePathOptimizer`
**Recommended branch name:** `feature/pdf-load-chart-metadata-evidence`
**Recommended repository path:** `docs/implementation/pdf-load-chart-metadata-evidence-implementation-report.md`
**Document type:** Branch implementation report / architecture impact record / validation record / maintainer handoff
**Primary subsystem:** `CraneOptimizer.UI/Features/F4_LoadChart`
**Primary implementation theme:** robust PDF crane load-chart import with metadata prefill, exact visual load-chart preservation, auditable source evidence, and professional WPF presentation
**Date:** 2026-06-20

---

## Table of Contents

1. [Purpose and Audience](#1-purpose-and-audience)
2. [Relationship to Existing Documentation](#2-relationship-to-existing-documentation)
3. [Executive Summary](#3-executive-summary)
4. [Problem Statement](#4-problem-statement)
5. [Scope, Non-Scope, and Branch Boundaries](#5-scope-non-scope-and-branch-boundaries)
6. [Architecture Preservation Assessment](#6-architecture-preservation-assessment)
7. [Feature Isolation Assessment](#7-feature-isolation-assessment)
8. [End-to-End User Flow](#8-end-to-end-user-flow)
9. [Implemented Behavior](#9-implemented-behavior)
10. [File-by-File Implementation Ledger](#10-file-by-file-implementation-ledger)
11. [Subsystem Impact Assessment](#11-subsystem-impact-assessment)
12. [Validation and Reproducibility Record](#12-validation-and-reproducibility-record)
13. [Regression-Prevention Strategy](#13-regression-prevention-strategy)
14. [Runtime Review Observations](#14-runtime-review-observations)
15. [Backward Compatibility and Migration Behavior](#15-backward-compatibility-and-migration-behavior)
16. [Known Limitations and Future Considerations](#16-known-limitations-and-future-considerations)
17. [Reviewer Checklist](#17-reviewer-checklist)
18. [Commit, Staging, and Pull-Request Guidance](#18-commit-staging-and-pull-request-guidance)
19. [Appendices](#19-appendices)

---

## 1. Purpose and Audience

This implementation report documents the branch work that hardens the **Import Crane Load Chart** experience for text-based manufacturer PDF load charts. The branch extends the F4 load-chart import user flow so that extracted load-chart tables are accompanied by reviewable crane metadata, source confidence, exact visual chart preservation, and professional evidence presentation.

The report is intended for:

- senior engineers reviewing branch readiness;
- maintainers responsible for the F4 PDF load-chart importer;
- WPF/UI engineers maintaining the import and dimension-review dialogs;
- engineers extending PDF metadata extraction logic;
- GA and optimization engineers who need to know whether downstream optimizer behavior changed;
- QA engineers reproducing validation;
- future contributors who must understand why evidence is shown compactly and why detailed evidence is opened on demand.

This is intentionally not a lightweight release note. It records intent, boundaries, files, tests, risks, and maintenance consequences so that the branch can be reviewed and maintained without requiring external context.

---

## 2. Relationship to Existing Documentation

This report should be added as a **new branch-specific implementation report** rather than replacing the existing Markdown documents.

The existing documents cover adjacent but distinct scopes:

| Existing topic | Relationship to this branch | Recommended action |
|---|---|---|
| PDF table-candidate robustness | Documents earlier F4 work on table reading order, continuation candidates, and source-preview behavior. | Keep as historical/upstream context. |
| Load-chart provenance through GA diagnostics | Documents downstream propagation of selected load-chart provenance through optimizer diagnostics. | Keep as downstream/diagnostics context. |
| F8 load-chart operating setup selector | Documents runtime setup selection and inclusive line-count eligibility. | Keep as downstream optimizer setup context. |

This branch is related to all three topics, but it has its own distinct purpose: **PDF metadata extraction, metadata prefill, outrigger evidence robustness, exact visual chart preservation, and professional metadata evidence presentation inside the F4 import user flow**.

Keeping this as a separate report avoids mixing unrelated branch histories and prevents contradictory future maintenance guidance.

---

## 3. Executive Summary

### 3.1 Branch Outcome

This branch upgrades the PDF Load Chart Import user flow from table-only extraction toward a complete, reviewable import experience:

1. **Exact PDF visual load chart preservation**
   - Exact visual chart grids are preserved in the import user flow.
   - Repeated manufacturer boom columns remain visible rather than being collapsed into a lossy one-column-per-header model.
   - Duplicate same-radius/same-boom visual columns remain available for conservative downstream capacity lookup.

2. **Crane metadata extraction and prefill**
   - The importer detects model, manufacturer, counterweight, transport dimensions, and outrigger dimensions where supported by PDF evidence.
   - Metadata is prefilled into editable fields so the user can review and correct values before saving.

3. **Manufacturer support-span robustness**
   - The metadata extractor handles difficult Liebherr and SANY layouts, including compact dimension diagrams, C/B labels, support-base measurements, and technical parameter tables.
   - Important support-span evidence is preserved through clean, user-facing summaries.

4. **Outrigger span repair**
   - The implementation handles Liebherr support diagrams where correct detection requires recognizing:
     - transverse support width from compact diagram values such as `5500 / 6230 / 8300`;
     - longitudinal front-to-rear support base from values such as `5034 / 3865 / 8899`.
   - For the key Liebherr 200 t example, the visible values are:
     - outrigger pad-to-pad span: `8.3 m`;
     - front-to-rear support span: `8.899 m`.

5. **Professional metadata evidence UI**
   - The Import Crane Load Chart left sidebar shows a compact `PDF metadata` status card.
   - The narrow sidebar does not display raw evidence text.
   - The Crane Dimensions dialog uses a compact summary with short field-level evidence hints.
   - Detailed evidence is available through a dedicated `PDF Metadata Details` dialog.
   - Copy functionality is placed in the details dialog rather than in the cramped sidebar.

6. **Validation preserved**
   - Metadata service tests pass `35/35`.
   - Visual boom header parsing tests pass `14/14`.
   - Broad LoadChart-filtered tests pass `134/134`.
   - Build completes with `0` errors.

### 3.2 Branch Value

The branch improves robustness and auditability without hardcoding a single page, table, crane PDF, or layout. The implementation is evidence-driven, confidence-aware, and review-oriented. It also preserves the user's ability to inspect and edit metadata before saving.

### 3.3 Primary Risk Addressed

Manufacturer PDF load charts often encode important crane metadata in visually dense diagrams, repeated-column load charts, multilingual dimension pages, and compact technical tables. This branch reduces the risk of importing an incomplete or misleading crane configuration by extracting metadata with source evidence and by making the evidence visible only when the user requests it.

---

## 4. Problem Statement

### 4.1 Original Technical Problem

The PDF import window could extract load-chart capacity tables well, but metadata extraction and metadata evidence presentation needed to match the quality of the table extraction. The importer needed to handle:

- selected load-chart page and selected table context;
- counterweight extraction;
- vehicle transport dimensions;
- outrigger support spans;
- manufacturer-specific layout patterns;
- exact visual table preservation;
- duplicate boom headers;
- compact and professional evidence presentation.

### 4.2 Original User-Experience Problem

Before the final presentation design, evidence could appear too densely in the UI:

- the left `PDF Metadata Evidence` card was too narrow and visually cramped;
- the card title could wrap awkwardly;
- raw evidence text could appear in the main import path;
- long `PDF source/confidence` lines in the Crane Dimensions dialog were dense and truncated;
- extraction artifacts could appear in user-visible evidence;
- evidence was technically useful but not presented with production-level polish.

### 4.3 Required Product Behavior

The importer needs to present evidence in a way that is:

- minimal in the primary import path;
- detailed only when requested;
- professional and readable;
- audit-friendly;
- not misleading;
- not dependent on hardcoded file/page assumptions;
- consistent with the existing WPF dark theme;
- respectful of the user's focus while reviewing and saving a crane.

The adopted UX principle is:

```text
Show essential confidence/status in the main user flow.
Show detailed evidence only on demand.
Keep raw extraction noise out of the normal path.
```

---

## 5. Scope, Non-Scope, and Branch Boundaries

### 5.1 In Scope

This branch includes:

- F4 PDF load-chart metadata extraction;
- selected-page and selected-table evidence priority;
- exact visual chart preservation interaction;
- repeated boom-column preservation;
- Liebherr compact support diagram support-span extraction;
- SANY dimension and counterweight extraction cases;
- evidence source/confidence capture;
- professional user-facing metadata summaries;
- on-demand metadata details dialogs;
- WPF layout refinements for the Import Crane Load Chart window;
- WPF layout refinements for the Crane Dimensions dialog;
- focused validation through LoadChart-related tests.

### 5.2 Out of Scope

This branch intentionally does **not** include:

| Area | Status |
|---|---|
| Genetic Algorithm algorithm redesign | Not changed. |
| GA scoring, mutation, crossover, selection | Not changed. |
| GA capacity resolver semantics | Not changed by this branch. |
| Streamlit UI | Not affected; no Streamlit files are part of this implementation. |
| Installer or deployment packaging | Not affected. |
| Database schema or migrations | Not affected. |
| IFC/DXF/model import | Not affected. |
| Persistent user settings | Not added for evidence-panel open/closed state. |
| Manufacturer capacity calculation | Not changed; imported manufacturer capacities remain authoritative. |
| OCR/scanned-image PDF support | Not added; the import path remains text/layout based. |
| Certification or engineering approval logic | Not added; user review remains required. |

### 5.3 Branch Boundary

This branch changes the import-time and review-time experience for PDF load charts. It does not certify crane setup values, replace manufacturer engineering documents, or alter the optimizer's core algorithms.

---

## 6. Architecture Preservation Assessment

### 6.1 Architecture Reference

The senior-provided architecture diagram establishes the expected separation:

```text
PL  ->  BLL  ->  DAL

Entities + contexts  ->  Repository  ->  Interface
ViewModel / MVC DTO / API  ->  Services  ->  Interface
View  <->  Controller
```

The key expectations are:

- presentation logic remains in views/controllers/viewmodels;
- business behavior belongs in services;
- data access belongs behind repositories/interfaces/DAL;
- layers should not be bypassed unnecessarily;
- feature-specific changes should remain isolated.

### 6.2 How the Branch Preserves the Architecture

The branch preserves the intended architecture as follows:

| Concern | Architectural layer | Current implementation behavior |
|---|---|---|
| PDF metadata detection heuristics | Service layer / feature service | Kept in `LoadChartPdfMetadataService.cs`. |
| PDF capacity table extraction | Existing F4 PDF service path | Existing import pipeline remains the source of table extraction. |
| User review/edit of values | Presentation layer | Kept in WPF dialogs. |
| Evidence presentation | Presentation layer | XAML/code-behind show summaries and detail dialogs. |
| Save Crane validation | Existing import user flow | Not bypassed. |
| Persistence/DAL | Infrastructure/repository/database layer | Not changed by this branch. |
| GA capacity lookup | Application/GA layer | Not modified by this branch. |

### 6.3 Architectural Nuance

The project currently places some F4 PDF import services under:

```text
CraneOptimizer.UI/Features/F4_LoadChart/Services
```

That folder is not a pure external BLL assembly. It is, however, the existing project convention for F4 feature services. This branch respects that convention by keeping extraction logic in feature service code rather than embedding parsing rules directly into XAML or WPF event handlers.

### 6.4 Architecture-Preservation Conclusion

The branch preserves the project’s practical architecture:

- no DAL/repository concerns were moved into views;
- no PDF parsing rules were embedded in XAML;
- no GA logic was moved into the PDF import UI;
- no installer or persistence side effects were introduced;
- validation remained focused on the affected F4 LoadChart behavior.

---

## 7. Feature Isolation Assessment

### 7.1 Isolation Preserved

Feature isolation was preserved because the implementation targets a narrow F4 LoadChart surface:

```text
CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfMetadataService.cs
CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml
CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml.cs
CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneDimensionsDialog.xaml
CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneDimensionsDialog.xaml.cs
CraneOptimizer.Application.Tests/LoadChart/LoadChartPdfMetadataServiceTests.cs
CraneOptimizer.Application.Tests/LoadChart/LoadChartVisualBoomHeaderParsingTests.cs
```

The implementation does not broaden scope into GA internals, installers, database migrations, DAL, Streamlit, IFC, DXF, or unrelated runtime features.

### 7.2 Why These Files Changed

| File | Why it changed |
|---|---|
| `LoadChartPdfMetadataService.cs` | Houses metadata extraction heuristics, evidence scoring, dimension parsing, and support-span extraction. |
| `LoadChartImportDialog.xaml` | Defines the Import Crane Load Chart layout, left sidebar, metadata card, and load-chart preview presentation. |
| `LoadChartImportDialog.xaml.cs` | Applies detected metadata to fields, builds user-facing evidence summaries, opens evidence details, and manages copy actions. |
| `CraneDimensionsDialog.xaml` | Defines the dimensions review/edit dialog and compact metadata notice. |
| `CraneDimensionsDialog.xaml.cs` | Formats compact evidence badges and detailed metadata evidence for the dimensions review dialog. |
| `LoadChartPdfMetadataServiceTests.cs` | Locks metadata extraction, support-span detection, and evidence behavior. |
| `LoadChartVisualBoomHeaderParsingTests.cs` | Protects exact visual boom-column behavior. |

### 7.3 Isolation Controls

The implementation is isolated by:

- limiting functional changes to F4 LoadChart import/review files;
- keeping extraction behavior inside service code;
- keeping presentation behavior inside WPF view and code-behind files;
- preserving existing save/import paths;
- maintaining targeted regression tests for metadata extraction and exact visual header behavior;
- avoiding repository-wide behavioral changes.

---

## 8. End-to-End User Flow

```text
User opens Import Crane Load Chart
        ↓
User loads a text-based manufacturer PDF
        ↓
PDF import service extracts table candidates and exact visual chart cells
        ↓
Metadata service scans PDF text/layout and selected-page evidence
        ↓
Model/manufacturer/counterweight/dimensions/outriggers are detected with source/confidence
        ↓
Import form is prefilled with reviewable values
        ↓
User can open Crane Dimensions and review/edit dimensions
        ↓
Compact evidence summaries show confidence without cramming raw text into the main UI
        ↓
User can open PDF Metadata Details for audit evidence and copy details if needed
        ↓
User saves crane
        ↓
Existing load-chart save/catalog behavior continues
```

---

## 9. Implemented Behavior

### 9.1 Exact Visual Load Chart Preservation

The import user flow preserves exact visual manufacturer chart columns, including repeated boom columns. This matters when the same numeric boom length appears multiple times with different line counts, operating conditions, or manufacturer-specific visual structure.

The UI communicates this through the `Exact PDF Visual Load Chart` section. The reference 2D envelope remains available, but exact visual chart preservation is treated as the authoritative import view when visual columns are present.

### 9.2 Metadata Prefill

The metadata extractor can prefill:

| Field | Example |
|---|---|
| Model | `LTM 1200-5.1` |
| Manufacturer | `Liebherr` |
| Counterweight | `72 t` |
| Transport length | `15.282 m` |
| Transport height | `3.95 m` |
| Travel width | `3.0 m` |
| Outrigger pad-to-pad span | `8.3 m` |
| Outrigger front-to-rear span | `8.899 m` |

All prefilled values remain user-reviewable before saving.

### 9.3 Liebherr Support-Span Detection

The metadata extractor handles support-span evidence such as:

```text
Transverse support width: 5500 / 6230 / 8300
Front-to-rear support base: 5034 / 3865 / 8899
```

The key user-facing result is:

```text
Outrigger W 8.3 m
Outrigger B 8.899 m
```

The implementation treats these as evidence-derived values, not as a hardcoded PDF-specific exception.

### 9.4 SANY Metadata Extraction

The branch preserves and improves extraction behavior for SANY crane PDFs where model, counterweight, dimensions, and support spans appear in technical parameter pages rather than Liebherr-style dimension diagrams.

Observed examples include:

| Crane | Representative metadata |
|---|---|
| `SRC550C` | counterweight `4.5 t`, transport length `14.3 m`, travel width `3.3 m`, support spans `7.2 m / 7.2 m`. |
| `STC800` | counterweight `4 t`, transport length `14.1 m`, travel width `2.75 m`, support spans `7.6 m / 6.1 m`. |
| `STC1000C` | counterweight `32.5 t`, support span `7.6 m`. |
| `STC1300C` | counterweight `40 t`, support span `7.6 m`. |

### 9.5 Evidence Display Model

The evidence UI follows progressive disclosure:

| UI area | Evidence behavior |
|---|---|
| Import Crane Load Chart sidebar | Compact `PDF metadata` card with confidence summary and `View details`. |
| Crane Dimensions dialog | Compact metadata summary and short field-level evidence hints. |
| Full details dialog | Structured detected values, evidence lines, review notes, and copy action. |

The primary UI remains minimal. Detailed evidence is available only when the user requests it.

### 9.6 Import Sidebar Refinement

The Import Crane Load Chart left sidebar now uses a compact metadata card:

```text
PDF metadata
High confidence - 8 fields auto-filled.
[View details]
```

Design decisions:

- no raw evidence text in the narrow sidebar;
- no `Copy summary` action in the narrow sidebar;
- copy functionality remains in the details dialog;
- bottom scroll spacing is added so the final card can scroll fully into view;
- the sidebar keeps one normal scroll area rather than adding nested scroll controls.

### 9.7 Crane Dimensions Dialog Refinement

The Crane Dimensions dialog presents:

```text
PDF metadata applied
High confidence - 8 fields auto-filled from PDF evidence.
[View details]
```

Field-level evidence is concise, for example:

```text
PDF evidence - Page 39 - dimensions diagram
High confidence - Page 39 - transverse support diagram - values 5500 / 6230 / 8300
High confidence - Page 39 - front-to-rear support base - values 3865 / 5034 / 8899
```

This design keeps normal review/edit user flow clean while preserving auditability.

### 9.8 Evidence Text Normalization

User-facing evidence is normalized to avoid mojibake-style artifacts and dense extraction noise in normal UI. Raw PDF snippets are not shown in the primary import path. Details are structured around detected values, source pages, confidence, and decisive numeric tokens.

---

## 10. File-by-File Implementation Ledger

### 10.1 `CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartPdfMetadataService.cs`

| Category | Detail |
|---|---|
| Change type | PDF metadata extraction service logic. |
| Why changed | Needed stronger model, counterweight, dimension, and outrigger evidence extraction from manufacturer PDFs. |
| What changed | Adds or refines selected-page evidence, Liebherr compact support-span parsing, compressed C/B support-base detection, longitudinal support-base extraction, SANY dimension/counterweight handling, and confidence/evidence source generation. |
| Architecture role | Feature service / business-style parsing logic within the existing F4 convention. |
| Downstream impact | Better import-field prefill and more reliable crane dimension evidence. |
| Risk | Moderate due to manufacturer PDF variability; mitigated by focused regression tests and exact visual header tests. |

### 10.2 `CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml`

| Category | Detail |
|---|---|
| Change type | WPF import dialog layout. |
| Why changed | Needed compact professional evidence presentation in the Import Crane Load Chart sidebar. |
| What changed | Adds a compact `PDF metadata` card, single `View details` action, hidden evidence storage text element, and bottom scroll spacing for the left sidebar. |
| Architecture role | Presentation only; no PDF parsing logic. |
| Downstream impact | Improves metadata review without changing import computation. |
| Risk | Low; layout-only behavior validated by runtime review. |

### 10.3 `CraneOptimizer.UI/Features/F4_LoadChart/Views/LoadChartImportDialog.xaml.cs`

| Category | Detail |
|---|---|
| Change type | Import dialog presentation orchestration. |
| Why changed | Needed to build clean metadata summaries, open details dialog, and keep copy actions in the appropriate place. |
| What changed | Adds structured metadata details generation, compact summary text, evidence normalization, details-dialog behavior, and copy-details behavior. |
| Architecture role | UI orchestration and presentation formatting; does not own extraction logic. |
| Downstream impact | User can review metadata evidence without leaving the import user flow. |
| Risk | Low to moderate; detail text quality should continue to be refined as more manufacturer PDFs are added. |

### 10.4 `CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneDimensionsDialog.xaml`

| Category | Detail |
|---|---|
| Change type | WPF dimensions review layout. |
| Why changed | Needed to replace dense source/confidence lines with a cleaner summary and controlled detail access. |
| What changed | Adds compact metadata summary card and `View details` action. |
| Architecture role | Presentation only. |
| Downstream impact | Improves review/edit clarity for transport and outrigger dimensions. |
| Risk | Low; runtime review shows the dialog is clear and acceptable. |

### 10.5 `CraneOptimizer.UI/Features/F4_LoadChart/Views/CraneDimensionsDialog.xaml.cs`

| Category | Detail |
|---|---|
| Change type | Evidence hint formatting and details dialog behavior. |
| Why changed | Needed concise evidence hints and structured on-demand details. |
| What changed | Converts dense source/confidence text into short labels and opens a structured evidence details dialog. |
| Architecture role | UI formatting/orchestration. |
| Downstream impact | Users can audit values without crowding the normal edit form. |
| Risk | Low; no extraction logic lives here. |

### 10.6 `CraneOptimizer.Application.Tests/LoadChart/LoadChartPdfMetadataServiceTests.cs`

| Category | Detail |
|---|---|
| Change type | Regression tests for metadata extraction. |
| Why changed | Needed to lock support-span repair, manufacturer metadata extraction, and evidence confidence behavior. |
| What changed | Adds or updates tests for real-world SANY/Liebherr metadata cases and explicit support-base evidence. |
| Architecture role | Test coverage. |
| Downstream impact | Prevents future metadata changes from breaking known manufacturer cases. |

### 10.7 `CraneOptimizer.Application.Tests/LoadChart/LoadChartVisualBoomHeaderParsingTests.cs`

| Category | Detail |
|---|---|
| Change type | Regression tests for exact visual boom header behavior. |
| Why changed | Needed to preserve exact PDF visual chart behavior while metadata logic changed. |
| What changed | Maintains coverage for repeated boom columns and visual-header preservation. |
| Architecture role | Test coverage. |
| Downstream impact | Ensures metadata improvements do not collapse or reorder preserved visual columns. |

---

## 11. Subsystem Impact Assessment

### 11.1 F4 PDF Load Chart Import

**Impact:** Direct.

The branch improves PDF metadata extraction, exact visual chart preservation interaction, and user-facing evidence review. This is the primary modified subsystem.

### 11.2 PDF Metadata Extraction Service

**Impact:** Direct.

The metadata service now handles more manufacturer layouts and support-span evidence patterns. It remains the correct location for metadata parsing logic under the current project organization.

### 11.3 WPF Import Dialog

**Impact:** Direct.

The import dialog now presents metadata evidence through a compact status card and an on-demand details dialog. The left sidebar remains a single-scroll panel with no nested metadata-card scroll area.

### 11.4 WPF Crane Dimensions Dialog

**Impact:** Direct but isolated.

The dialog now displays compact metadata notice and short source/evidence hints. Values remain editable.

### 11.5 Genetic Algorithm

**Impact:** Indirect only.

The GA continues to consume saved crane load charts. This branch can improve the quality and auditability of imported crane data, but it does not alter mutation, crossover, scoring, feasibility evaluation, path planning, assignment, placement, or capacity resolver semantics.

### 11.6 Engine and Optimization Pipeline

**Impact:** None direct.

No engine-level algorithm or optimizer pipeline behavior is changed. Any downstream benefit comes from higher-quality imported crane metadata.

### 11.7 Diagnostics and Verification Logic

**Impact:** Test-level and UI-level only.

The branch strengthens LoadChart-related regression tests and improves metadata evidence shown in the WPF UI. It does not introduce a new GA diagnostics output contract.

### 11.8 Installers and Deployment

**Impact:** None.

No installer, deployment manifest, packaging file, or NuGet dependency file is changed.

### 11.9 Database and Persistence

**Impact:** None documented for this branch.

No database migration or persistence contract change is included. The Save Crane user flow remains the existing persistence boundary.

### 11.10 Streamlit UI

**Impact:** None.

No Streamlit files are part of this implementation.

### 11.11 CSV Import

**Impact:** None direct.

The branch focuses on PDF metadata/evidence behavior. CSV load-chart import behavior is not modified.

### 11.12 Outputs Pipeline

**Impact:** None direct.

The branch does not change external CSV/report outputs. It changes WPF import and review UI behavior.

---

## 12. Validation and Reproducibility Record

### 12.1 Verified Validation Commands

The branch was validated with the following command categories:

```text
dotnet restore
dotnet build --configuration Debug --no-restore
dotnet test --configuration Debug --no-build --filter "FullyQualifiedName~LoadChartPdfMetadataServiceTests"
dotnet test --configuration Debug --no-build --filter "FullyQualifiedName~LoadChartVisualBoomHeaderParsingTests"
dotnet test --configuration Debug --no-build --filter "FullyQualifiedName~LoadChart"
```

### 12.2 Observed Validation Summary

| Validation target | Observed result |
|---|---:|
| Restore | Passed |
| Build | Passed |
| Build errors | `0` |
| Metadata service tests | `35/35` passed |
| Visual boom header parsing tests | `14/14` passed |
| Broad LoadChart-filtered tests | `134/134` passed |

### 12.3 Recommended Pre-PR Validation

Before opening the pull request, run:

```text
cd "C:\Users\Bahyg\Source\Repos\CranePathOptimizer"
dotnet restore
dotnet build --configuration Debug --no-restore
dotnet test --configuration Debug --no-build --filter "FullyQualifiedName~LoadChart"
```

If repository policy requires full-solution validation, additionally run:

```text
dotnet test --configuration Debug --no-build
```

### 12.4 Warnings

The build can report existing warnings. The branch readiness criterion is:

```text
Build completes successfully.
Build errors are 0.
LoadChart regression tests pass.
```

Warnings should be reviewed separately if repository policy requires a warning-clean merge.

---

## 13. Regression-Prevention Strategy

### 13.1 Protected Behaviors

The branch protects the following behaviors:

| Risk | Protection |
|---|---|
| Metadata extraction regresses for known PDFs | `LoadChartPdfMetadataServiceTests` |
| Exact visual boom columns collapse | `LoadChartVisualBoomHeaderParsingTests` |
| Liebherr `8.899 m` support base regresses | Metadata regression coverage and runtime review |
| Import sidebar becomes cramped again | Runtime UI review |
| Crane Dimensions returns to dense raw text | Runtime UI review and code review |
| Extraction logic moves into UI layout | Architecture review |
| Unrelated subsystems are modified | File-level review and scoped staging |

### 13.2 Reviewer Regression Checklist

Reviewers should specifically verify:

- exact visual chart columns are preserved;
- `LTM 1200-5.1` metadata remains correct;
- `W 8.3 m` and `B 8.899 m` remain visible for the key Liebherr case;
- no raw evidence wall appears in the import sidebar;
- no long dense source/confidence paragraphs appear under every Crane Dimensions field;
- `View details` opens structured metadata evidence;
- `Copy details` remains in the details dialog;
- no unrelated GA, DAL, installer, or Streamlit files are included.

---

## 14. Runtime Review Observations

### 14.1 Import Crane Load Chart Window

Observed runtime behavior:

- exact visual load chart is displayed;
- repeated boom columns are preserved;
- metadata fields prefill correctly;
- left sidebar card is compact;
- sidebar uses one normal scroll region;
- metadata card shows only `PDF metadata`, confidence summary, and `View details`;
- copy action is not placed in the narrow sidebar.

### 14.2 PDF Metadata Details Dialog

Observed runtime behavior:

- details dialog opens from `View details`;
- dialog shows detected values;
- decisive support-span evidence is visible:
  - `5500 / 6230 / 8300`;
  - `3865 / 5034 / 8899`;
- `Copy details` is available in the dialog;
- obvious mojibake-style artifacts are not visible in the main UI.

### 14.3 Crane Dimensions Dialog

Observed runtime behavior:

- compact `PDF metadata applied` summary appears;
- field-level hints are concise;
- outrigger width shows `8.3`;
- front-to-rear span shows `8.899`;
- `View details` remains available;
- dialog presentation is accepted as professional and complete.

### 14.4 Remaining Presentation Note

The full details dialog may still include sanitized source-language text in review notes where the source material is multilingual. This is not shown in the primary user flow. If product policy later requires fully English-only detailed evidence, the next refinement should normalize review notes into concise English evidence statements while preserving decisive numeric tokens.

---

## 15. Backward Compatibility and Migration Behavior

### 15.1 Existing PDF Import

Existing text-based PDF import remains compatible. The branch improves metadata prefill and evidence presentation without removing the user's ability to review and correct values.

### 15.2 Existing CSV Import

CSV import behavior is not directly changed.

### 15.3 Existing Crane Catalog/Save Behavior

No persistence migration is introduced. The Save Crane user flow remains in place.

### 15.4 Existing Optimizer Behavior

Optimizer algorithms and capacity lookup semantics are not changed. The optimizer can benefit from better imported crane data, but this branch does not alter the GA.

### 15.5 Existing UI Flows

The import and dimension-review flows remain familiar:

```text
Load PDF
Review table
Review metadata
Edit dimensions if needed
Save Crane
```

The evidence presentation improves the review experience without changing the core user flow.

---

## 16. Known Limitations and Future Considerations

### 16.1 OCR and Scanned PDFs

The implementation remains text/layout based. Scanned image-only PDFs are outside the branch scope.

### 16.2 Manufacturer Layout Variability

The implementation improves known SANY and Liebherr cases but does not guarantee every manufacturer document layout. New manufacturer PDFs should be added to regression coverage as they are discovered.

### 16.3 Detailed Evidence Language Normalization

The compact UI is professional and English-focused. Detailed evidence can still expose sanitized source-language context for audit. A future refinement can convert detailed review notes into fully English-only evidence summaries.

### 16.4 Full-Solution Test Coverage

The latest verified test set is LoadChart-focused. A full solution-wide test run is recommended before merge if required by repository policy.

### 16.5 Documentation Maintenance

Future changes to metadata extraction semantics, source-confidence display, or exact visual chart preservation should update this report or add a successor implementation report.

---

## 17. Reviewer Checklist

### 17.1 Code Review

- [ ] Confirm extraction logic remains in service code, not XAML.
- [ ] Confirm UI code-behind only formats/presents evidence and does not own PDF parsing.
- [ ] Confirm exact visual chart preservation remains intact.
- [ ] Confirm support-span values are not hardcoded to a single PDF page.
- [ ] Confirm no unrelated GA, DAL, installer, or Streamlit files are included.
- [ ] Confirm no local build artifacts or temporary files are staged.

### 17.2 Runtime Review

- [ ] Load a Liebherr PDF with repeated visual boom columns.
- [ ] Confirm exact visual chart columns are preserved.
- [ ] Confirm metadata fields prefill.
- [ ] Confirm the sidebar metadata card is compact and fully visible when scrolled.
- [ ] Open `View details`.
- [ ] Confirm decisive numeric tokens are visible in the details dialog.
- [ ] Open Crane Dimensions.
- [ ] Confirm `W 8.3 m` and `B 8.899 m`.
- [ ] Confirm Crane Dimensions evidence is concise and professional.

### 17.3 Validation Review

- [ ] `dotnet restore` passes.
- [ ] `dotnet build --configuration Debug --no-restore` passes.
- [ ] Metadata tests pass `35/35`.
- [ ] Visual boom header tests pass `14/14`.
- [ ] Broad LoadChart-filtered tests pass.
- [ ] Full solution tests pass if required by project policy.

---

## 18. Commit, Staging, and Pull-Request Guidance

### 18.1 Recommended Branch Name

```text
feature/pdf-load-chart-metadata-evidence
```

Alternative:

```text
feature/f4-pdf-metadata-evidence-ui
```

### 18.2 Recommended Commit Message

```text
Improve PDF load chart metadata evidence and review UI
```

### 18.3 Suggested Commit Body

```text
- Improve PDF metadata extraction for crane model, manufacturer, counterweight,
  transport dimensions, and outrigger spans.
- Preserve exact visual load-chart columns, including repeated manufacturer
  boom columns.
- Improve support-span detection for compact Liebherr/SANY layouts.
- Present PDF metadata evidence through compact review UI and on-demand details.
- Keep the change isolated to F4 LoadChart import/review surfaces and focused
  LoadChart regression tests.
```

### 18.4 Recommended PR Title

```text
Improve PDF load chart metadata evidence and review UI
```

### 18.5 Recommended PR Body

```markdown
## Summary

This PR improves the F4 PDF Load Chart Import user flow by adding stronger
metadata extraction, exact visual load-chart preservation, and a cleaner
professional evidence-review UI.

## What changed

- Improved PDF metadata extraction for model, manufacturer, counterweight,
  transport dimensions, and outrigger dimensions.
- Preserved exact visual load-chart columns, including repeated manufacturer
  boom columns.
- Improved outrigger span detection for compact Liebherr/SANY dimension layouts.
- Added compact PDF metadata summaries in the import and dimensions dialogs.
- Moved detailed evidence into an on-demand PDF Metadata Details dialog.
- Kept copy actions in the details dialog instead of the narrow sidebar.
- Added/maintained focused LoadChart regression coverage.

## Feature isolation

The change is isolated to the F4 LoadChart import/review path and focused
LoadChart tests.

Primary affected areas:

- `CraneOptimizer.UI/Features/F4_LoadChart/Services`
- `CraneOptimizer.UI/Features/F4_LoadChart/Views`
- `CraneOptimizer.Application.Tests/LoadChart`

The implementation does not modify GA algorithms, Streamlit UI, installers,
database migrations, IFC/DXF import, or persistence contracts.

## Architecture and design-pattern preservation

The senior architecture pattern is preserved:

- Presentation behavior remains in WPF views and code-behind.
- Metadata extraction remains in the F4 load-chart service layer.
- Save Crane validation and persistence flow remain unchanged.
- GA capacity lookup and optimizer internals are not modified.
- No data-access concerns are moved into the UI.

The branch follows the existing project convention where F4 feature services
live under the UI feature folder while still keeping parsing logic out of XAML
and UI event handlers.

## Validation

Observed validation:

- `dotnet restore`: passed
- `dotnet build --configuration Debug --no-restore`: passed
- `LoadChartPdfMetadataServiceTests`: 35/35 passed
- `LoadChartVisualBoomHeaderParsingTests`: 14/14 passed
- Broad LoadChart-filtered tests: 134/134 passed
- Build errors: 0
```

### 18.6 Recommended Staging Discipline

Stage only intended tracked source and documentation files. Do not stage local local-only artifacts, screenshots, build outputs, or machine-specific files.

Recommended staging pattern:

```text
git status --short

git add `
  .\CraneOptimizer.UI\Features\F4_LoadChart\Services\LoadChartPdfMetadataService.cs `
  .\CraneOptimizer.UI\Features\F4_LoadChart\Views\LoadChartImportDialog.xaml `
  .\CraneOptimizer.UI\Features\F4_LoadChart\Views\LoadChartImportDialog.xaml.cs `
  .\CraneOptimizer.UI\Features\F4_LoadChart\Views\CraneDimensionsDialog.xaml `
  .\CraneOptimizer.UI\Features\F4_LoadChart\Views\CraneDimensionsDialog.xaml.cs `
  .\CraneOptimizer.Application.Tests\LoadChart\LoadChartPdfMetadataServiceTests.cs `
  .\CraneOptimizer.Application.Tests\LoadChart\LoadChartVisualBoomHeaderParsingTests.cs `
  .\docs\implementation\pdf-load-chart-metadata-evidence-implementation-report.md

git diff --cached --stat
git diff --cached --check
```

---

## 19. Appendices

### Appendix A — Architecture Mapping

| Architecture element | Branch mapping |
|---|---|
| View | `LoadChartImportDialog.xaml`, `CraneDimensionsDialog.xaml` |
| Controller / UI orchestration | `LoadChartImportDialog.xaml.cs`, `CraneDimensionsDialog.xaml.cs` |
| Services | `LoadChartPdfMetadataService.cs` and existing F4 load-chart services |
| Interfaces / repositories | Not changed |
| Entities / contexts | Not changed |
| DAL | Not changed |
| GA/optimizer engine | Not changed |

### Appendix B — Key Terms

| Term | Meaning |
|---|---|
| Exact visual load chart | A preserved table grid matching the PDF visual columns, including repeated headers. |
| Reference 2D envelope | A simplified radius/boom lookup view retained for reference. |
| Metadata evidence | Source/confidence information explaining why a metadata field was prefilled. |
| Pad-to-pad span | Transverse outrigger support width. |
| Front-to-rear span | Longitudinal outrigger support base. |
| Progressive disclosure | Showing only essential information in the primary UI and revealing details on demand. |

### Appendix C — Recommended Manual Smoke Test

1. Open the application.
2. Open the Import Crane Load Chart user flow.
3. Load a Liebherr PDF with repeated visual boom columns.
4. Confirm the exact visual chart appears.
5. Confirm model/manufacturer/counterweight metadata is prefilled.
6. Scroll the left sidebar and confirm the compact `PDF metadata` card is fully visible.
7. Click `View details`.
8. Confirm detected values and decisive support-span tokens are visible.
9. Open Crane Dimensions.
10. Confirm transport dimensions and outriggers are correct.
11. Confirm evidence hints are concise and not dense raw text.
12. Save the crane only after reviewing values.

### Appendix D — Maintenance Rules

Future contributors should preserve the following rules:

- Do not collapse repeated visual boom columns unless the user explicitly chooses a simplified reference view.
- Do not place raw PDF evidence text in the narrow sidebar.
- Do not move PDF parsing logic into XAML.
- Do not use page-specific or file-specific constants as the primary extraction strategy.
- Keep metadata values user-reviewable before saving.
- Add new regression cases when a new manufacturer layout is supported.
- Keep evidence concise in normal UI and detailed only on demand.
