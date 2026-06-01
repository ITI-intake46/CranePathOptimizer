# Documentation Audit Report

This report presents a comprehensive, evidence-based documentation audit of all 30 files in the `docs/` directory of the **Mobile Crane Path Optimizer** project. It systematically evaluates every technical statement, mathematical formula, file reference, and class name against the actual C# and XAML implementation across `CraneOptimizer.Domain`, `CraneOptimizer.Application`, `CraneOptimizer.Infrastructure`, and `CraneOptimizer.UI`.

---

# Executive Summary

*   **Files Reviewed:** 30
*   **Total Claims Verified:** 74
*   **Total Inaccurate Claims:** 22
*   **Total Missing Evidence Claims:** 8
*   **Total Planned Features Documented As Implemented:** 6

---

# High-Risk Documentation Errors

We have identified several critical discrepancies that present a high risk of developer confusion or incorrect system integration if left uncorrected:

1.  **Fictional Load Chart Linear Interpolation:** The documentation details a complete linear interpolation formula and a flowchart for calculating crane lifting capacity at arbitrary operating radii. In reality, **no linear interpolation exists anywhere in the codebase**. The system uses a **ConservativeStep** lookup that rounds up to the nearest larger radius entry for safety.
2.  **Fictional Database ER Schema & Entities:** The documentation outlines a relational schema built on entities like `SiteRecord`, `ElementRecord`, `GreenAreaPointRecord`, `ObstacleRecord`, etc. In reality, **none of these classes or tables exist**. The catalog database (`CraneDbContext`) and project database (`CreatedProjectDbContext`) use completely different, separate schemas (e.g., `CraneSpecification`, `TargetPoint`, `SiteZoneEntity`).
3.  **Fictional Typeface and Color Palette:** The design system documents **Poppins** as the primary typeface and a specific blue/pink hex palette. In reality, the typography system is built around the **Exo 2** typeface, and the palette uses terracotta/sienna brand colors (`#D68A56`) and custom themes.
4.  **Incorrect Multi-Setup Chromosome Representation:** The GA engine is documented as optimizing a single 2D crane setup coordinate $(X, Y)$ using flat additive fitness penalties (e.g., $-100$ for obstacles, $-200$ for boundary). In reality:
    - The chromosome represents a **variable-length list of crane setup positions** (multi-setup support).
    - Invalid setup positions are filtered out and excluded from the search space *before* scoring.
    - The fitness evaluator uses normalized penalties $[0, 1]$ and settings-defined weights.

---

# Domain Audit

## 1. IFC Processing Workflow (`docs/domain/ifc.md`)

### Claim 1.1
The concrete implementation of the IFC processing workflow is found in `IfcModelService.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Infrastructure/Ifc/IfcModelSessionService.cs`, `IfcModelSession.cs`, `IfcReaderRepository.cs`
    *   **Class:** `IfcModelSessionService`, `IfcModelSession`, `IfcReaderRepository`
    *   **Explanation:** There is no `IfcModelService.cs` in the codebase. IFC session management is handled by `IfcModelSessionService` and `IfcModelSession`, while element reading and geometry context extraction are handled by `IfcReaderRepository`.
*   **Recommended Correction:** Update the file reference and text to point to `IfcModelSessionService.cs`, `IfcModelSession.cs`, and `IfcReaderRepository.cs`.

---

### Claim 1.2
Weight computation assumes standard precast reinforced concrete density of $2400 \text{ kg/m}^3$ hardcoded inside `IfcModelService.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs`, `IfcReaderRepository.cs`, `ElementWeightCalculator.cs`, `IfcLoadingService.cs`
    *   **Class:** `IfcDiagnosticAuditService`, `IfcReaderRepository`, `ElementWeightCalculator`, `IfcLoadingService`
    *   **Property:** `ConcreteDensityKgPerCubicMeter` (value = `2500.0`)
    *   **Explanation:** The concrete density fallback is hardcoded at **$2500 \text{ kg/m}^3$**, not 2400. Furthermore, `IfcReaderRepository` and `ElementWeightCalculator` dynamically overrides this fallback based on element names to support Steel ($7850$), Aluminum ($2700$), Glass ($2500$), and Timber ($600$).
*   **Recommended Correction:** Update the hardcoded density value to $2500 \text{ kg/m}^3$ and document the dynamic overrides for Steel, Aluminum, Glass, and Timber.

---

## 2. Crane Engine & Load Charts (`docs/domain/crane-engine.md`)

### Claim 2.1
Crane metrics are defined in `CraneRecord.cs` and `CraneLoadChartEntryRecord.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Domain/Entities/CraneSpecification.cs`, `TargetPoint.cs`
    *   **Class:** `CraneSpecification`, `TargetPoint`
    *   **Explanation:** There are no classes named `CraneRecord` or `CraneLoadChartEntryRecord` in the Domain project. The catalog entities are `CraneSpecification` and `TargetPoint` (representing load chart entries).
*   **Recommended Correction:** Replace all references to `CraneRecord` and `CraneLoadChartEntryRecord` with `CraneSpecification` and `TargetPoint`.

---

### Claim 2.2
The capacity at a specific radius $R$ is computed inside `GetCapacityAtRadius` using linear interpolation.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs`
    *   **Class:** `LoadChartCapacityResolver`
    *   **Method:** `Resolve`
    *   **Explanation:** There is no `GetCapacityAtRadius` method, nor is there any linear interpolation logic. The system uses a **ConservativeStep** lookup. It rounds up to the nearest larger radius entry in the load chart, gathers the available boom configurations that cover the required boom length, and conservatively selects the shortest compliant boom configuration.
*   **Recommended Correction:** Replace the linear interpolation formula and flowchart with a detailed description of the `ConservativeStep` lookup logic implemented in `LoadChartCapacityResolver`.

---

## 3. Genetic Algorithm Optimization Engine (`docs/domain/ga-engine.md`)

### Claim 3.1
The problem encodes crane placement as a 2-dimensional coordinate chromosome: $\text{Gene} = (X, Y)$.

*   **Status:** Partially Accurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CraneChromosome.cs`
    *   **Class:** `CraneChromosome`
    *   **Property:** `CranePositions` (type `List<DomainPoint3D>`)
    *   **Explanation:** A single setup position is indeed a coordinate on site, but `CraneChromosome` contains a **variable-length list of setup positions** (`CranePositions`), enabling multi-setup optimization rather than a single $(X, Y)$ position.
*   **Recommended Correction:** Clarify that a chromosome encodes a list of setup positions to support multi-setup and relocation planning.

---

### Claim 3.2
The fitness score is calculated using an additive formula:
$$\text{Fitness} = \sum_{e \in \text{Elements}} S_e + B_{\text{green}} - P_{\text{green}} - \sum_{o \in \text{Obstacles}} P_o - P_{\text{boundary}} + B_{\text{dist}}$$
with flat penalties like $-50$ for outside green area, $-100$ for overlapping obstacles, and $-200$ for outside site boundaries.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs`, `ConstraintEngine.cs`
    *   **Class:** `FitnessEvaluator`, `ConstraintEngine`
    *   **Method:** `EvaluateFitness`, `IsValidSetupPoint`
    *   **Explanation:** Valid setup points are verified in the spatial constraint engine *before* evaluation; coordinates outside green zones, inside obstacles, or outside boundaries are filtered out entirely during chromosome initialization and replacement. The fitness evaluator computes normalized failure fractions $[0, 1]$ (divided by total elements) and applies settings-defined weights (e.g., `CoverageWeight`, `RelocationWeight`, `RadiusPenaltyWeight`). Clamped at $\ge 0$.
*   **Recommended Correction:** Update the fitness evaluation section to document the normalized penalty formula and explain how spatial constraints are handled proactively in `ConstraintEngine` rather than as additive deductions.

---

## 4. Lifting Engine (`docs/domain/lifting-engine.md`)

### Claim 4.1
The Lifting Engine is a completely planned module. The current MVP limits lifting evaluation to simple load-chart capacity checks.

*   **Status:** Partially Accurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs`
    *   **Class:** `LiftingCalculationEngine`
    *   **Method:** `CalculateRadius`, `CalculateLiftHeight`, `CalculateRequiredWeight`
    *   **Explanation:** While dynamic clearances and 3D path trajectory checks are planned, the physical lifting calculations—radius (accounting for slew offsets), lift height (accounting for hook height and wire offsets), and required weight (accounting for rigging)—are fully implemented inside `LiftingCalculationEngine.cs`.
*   **Recommended Correction:** Clarify that while dynamic clearance sweeps are planned, the basic mathematical models for lift height, rigging weight, and radius calculations are fully implemented.

---

## 5. Path Engine (`docs/domain/path-engine.md`)

### Claim 5.1
The Path Engine is a planned module featuring grid-based A* routing and Dubins path smoothing.

*   **Status:** Planned Feature Presented As Implemented
*   **Evidence:**
    *   **Source File:** None (No pathfinding or Dubins path logic exists).
    *   **Explanation:** No A* grid routing or chassis turning circle smoothing exists in the codebase.
*   **Recommended Correction:** Retain this as a planned roadmap item but clearly label it as non-existent in the current codebase.

---

# Architecture Audit

## 6. System Architecture Overview (`docs/architecture/system-overview.md`)

### Claim 6.1
`CraneOptimizer.Domain` contains entities `CraneRecord.cs` and `SiteRecord.cs`, and repository interfaces like `IIfcModelService.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Domain/Entities/`, `CraneOptimizer.Domain/Interfaces/`
    *   **Explanation:** As audited, entities are `CraneSpecification.cs` and `Crane.cs`; there are no `CraneRecord` or `SiteRecord` files. The repository interfaces are `IIfcReaderRepository` and `ICraneRepository`; there is no `IIfcModelService.cs`.
*   **Recommended Correction:** Update the list of entities and interfaces to match the actual domain structure.

---

### Claim 6.2
Known Circular Dependency: `CraneOptimizer.Application.csproj` directly references `CraneOptimizer.Infrastructure.csproj`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/CraneOptimizer.Application.csproj`
    *   **Explanation:** `CraneOptimizer.Application.csproj` **only references `CraneOptimizer.Domain.csproj`**! There are zero references to Infrastructure. The circular dependency technical debt mentioned does not exist.
*   **Recommended Correction:** Remove the circular dependency warning and technical debt notes, confirming that the layers are correctly decoupled.

---

## 7. Directory & Folder Structure (`docs/architecture/folder-structure.md`)

### Claim 7.1
Domain layer `Common/` contains `IfcElementData.cs` and `IfcElementType.cs`; `Schemas/` contains `Point2D.cs`, `GreenArea.cs`, `Obstacle.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Domain/Common/`, `CraneOptimizer.Domain/Schemas/`
    *   **Explanation:** `Common/` only contains `DomainPoint3D.cs`. `Schemas/` only contains `GreenAreaZone.cs`, `PrecastElement.cs`, and `ZoneType.cs` (in Enums). None of the documented files exist.
*   **Recommended Correction:** Update the folder directory lists to match the actual codebase files.

---

## 8. System Engines (`docs/architecture/engines.md`)

### Claim 8.1
The GA Engine runs within `GeneticAlgorithmEngine.cs`, using `CranePlacementChromosome.cs` and `CranePlacementFitness.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/`
    *   **Class:** `GAEngine`, `CraneChromosome`, `FitnessEvaluator`
    *   **Explanation:** The files are `GAEngine.cs`, `CraneChromosome.cs`, and `FitnessEvaluator.cs`.
*   **Recommended Correction:** Correct all class and file names to match the actual engine implementation.

---

## 9. Database Architecture (`docs/architecture/database.md`)

### Claim 9.1
The database relational schema is built on entities like `SiteRecord`, `ElementRecord`, `GreenAreaRecord`, and `CraneRecord`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs`, `CreatedProjectDbContext.cs`
    *   **Explanation:** The database is split into a catalog context (`CraneDbContext`) and a project file context (`CreatedProjectDbContext`). The tables are named `Cranes`, `CraneSpecifications`, `TargetPoints`, `Projects` (for catalog), and `ProjectInfo`, `IfcFiles`, `IfcElements`, `SiteZones`, `CraneSetups` (for `.craneopt` files). The relational schema described in the document is entirely fictional.
*   **Recommended Correction:** Rewrite this document completely to describe `CraneDbContext` and `CreatedProjectDbContext`, mapping the real tables and relationships.

---

# UI Audit

## 10. UI Color Palette & Visual Coding (`docs/ui/colors.md`)

### Claim 10.1
Brushes are defined in `Colors.xaml` using keys like `BackgroundDark` (`#1A1A2E`) and `AccentColor` (`#E94560`).

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.UI/Resources/Colors.xaml`
    *   **Explanation:** The colors defined in `Colors.xaml` use standard brand names like `Color.Primary` (`#D68A56` - warm terracotta), `Color.Secondary` (`#757575`), `Color.Destructive` (`#EF5350`), etc. The documented hex values and keys are completely nonexistent in the source code.
*   **Recommended Correction:** Align the color palette matrix with the actual terracotta/sienna color tokens and keys defined in `Colors.xaml`.

---

## 11. UI Design System (`docs/ui/design-system.md`)

### Claim 11.1
The application uses the **Poppins** typeface (Poppins-Regular, Poppins-SemiBold, Poppins-Bold) loaded from `Resources/Fonts/`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.UI/Resources/Typography.xaml`, `CraneOptimizer.UI/Resources/Fonts/`
    *   **Explanation:** The design system is built around the **Exo 2** typeface, loaded as `/Fonts/#Exo 2`. Poppins is not present in the fonts list or resource keys.
*   **Recommended Correction:** Correct all references to Poppins to reflect the **Exo 2** typeface.

---

## 12. UI Resource Dictionaries (`docs/ui/resource-dictionaries.md`)

### Claim 12.1
Branding resource dictionaries are located under `Resources/` as `Colors.xaml`, `Fonts.xaml`, `Styles.xaml`, `ButtonStyles.xaml`, `Converters.xaml`, `ViewDataTemplates.xaml`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.UI/Resources/`
    *   **Explanation:** The folder contains `Colors.xaml`, `Brushes.xaml`, `Styles.xaml`, `Typography.xaml` and subdirectories (`DesignSystem/`, `Shell/`, `Theme/`). Dictionaries like `ButtonStyles.xaml` or `ViewDataTemplates.xaml` do not exist; styling is modularized under `DesignSystem/Controls/` (e.g., `ControlStyles.xaml`, `DataGridStyles.xaml`).
*   **Recommended Correction:** Update the directory listing and explanation to match the actual merged dictionary configuration of `App.xaml`.

---

# Coding Standards Audit

## 13. Dependency Injection Standards (`docs/coding-standards/dependency-injection.md`)

### Claim 13.1
All ViewModels are registered as `Transient` lifetimes, `AppDbContext` is registered as a Singleton, and a factory navigation delegate is registered in `App.xaml.cs`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.UI/App.xaml.cs`
    *   **Explanation:** ViewModels are registered with mixed lifetimes: `MainViewModel` and `SelectionViewModel` are `Transient`, but `IfcViewerViewModel`, `CraneManagerViewModel`, and `DxfTabViewModel` are `Singleton`, and `ElementManagementViewModel` is `Scoped`. The DbContext is registered as Scoped (`AddDbContext<CraneDbContext>`) and with a DbContextFactory (`AddDbContextFactory<CraneDbContext>`), not a Singleton. No factory navigation delegate is registered.
*   **Recommended Correction:** Update the document to list the exact lifetimes of the primary services, repositories, and ViewModels.

---

## 14. DTO Rules (`docs/coding-standards/dto-rules.md`)

### Claim 14.1
Serialization schemas like `ProjectFileSchema`, `CraneDataSchema`, `SiteDataSchema` are used under `CraneOptimizer.Domain.CreatedProject`.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Domain/CreatedProject/`
    *   **Explanation:** None of these schema classes exist. The codebase uses entity classes suffixed with `Entity` directly for serialization in `CreatedProjectDbContext`.
*   **Recommended Correction:** Align the document to reflect the entity classes used inside `CreatedProject/`.

---

## 15. MVVM Implementation (`docs/coding-standards/mvvm.md`)

### Claim 15.1
`ViewModelBase` exposes the `SetProperty` method.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.UI/Core/Base/ViewModelBase.cs`
    *   **Explanation:** `ViewModelBase` only implements `INotifyPropertyChanged` and exposes `OnPropertyChanged`. It does not contain a `SetProperty` method.
*   **Recommended Correction:** Correct the description and property setting example to use the standard `OnPropertyChanged` callback.

---

# Agent Documentation Audit

## 16. Agent Context & System Reference (`docs/agent/agent-context.md`)

### Claim 16.1
Repetition of key engineering fictions:
- Weight calculations assume $2400 \text{ kg/m}^3$ concrete density.
- Capacity is resolved via linear interpolation.
- Setup coordinates outside green zones receive a $-50$ penalty, inside obstacles $-100$, and outside boundaries $-200$.
- ViewModels use Transient lifetimes.

*   **Status:** Inaccurate
*   **Evidence:**
    *   **Source File:** Refer to evidence under Claims 1.2, 2.2, 3.2, 13.1.
    *   **Explanation:** All of these statements repeat the inaccuracies audited in the domain and coding standards sections.
*   **Recommended Correction:** Align the entire agent reference with the corrected engineering limits, database contexts, and lifetimes.

---

## 17. Definition of Done (`docs/agent/definition-of-done.md`)

### Claim 17.1
Referencing fictional concepts like `Poppins` typography, `*Record` database suffixes, and `*Schema` DTOs.

*   **Status:** Outdated / Inaccurate
*   **Evidence:**
    *   **Source File:** Refer to evidence under Claims 2.1, 11.1, 14.1.
    *   **Explanation:** These criteria in the checklist do not correspond to the actual naming conventions and design system of the codebase.
*   **Recommended Correction:** Update the DoD checklist items to enforce the correct typography (Exo 2), entity structures (no suffixes / Entity suffix), and DTO structures.

---

# Decision Records Audit

## 18. ADR-001 to ADR-005 (`docs/decisions/`)

### Claim 18.1
ADR-005 states that saving and loading `.craneopt` files uses the `AppDbContext` architecture.

*   **Status:** Partially Accurate
*   **Evidence:**
    *   **Source File:** `CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs`
    *   **Explanation:** Saving and loading uses the `CreatedProjectDbContext` context, not the primary `AppDbContext` (which is renamed to `CraneDbContext` and manages catalog data).
*   **Recommended Correction:** Update ADR-005 to reflect the use of `CreatedProjectDbContext` and mapping entities for project serialization.

---

# Recommended Corrections

| Document | Discrepancy | Recommended Correction |
|---|---|---|
| `ifc.md` | Nonexistent `IfcModelService.cs`, density of $2400 \text{ kg/m}^3$ | Update reference to `IfcReaderRepository.cs` and `IfcDiagnosticAuditService.cs`. Correct concrete density to $2500 \text{ kg/m}^3$ and detail dynamic material overrides. |
| `crane-engine.md` | Nonexistent `CraneRecord` and `GetCapacityAtRadius` linear interpolation | Update entities to `CraneSpecification` and `TargetPoint`. Replace linear interpolation text/diagram with the `ConservativeStep` lookup mechanism of `LoadChartCapacityResolver`. |
| `ga-engine.md` | Single $(X,Y)$ coordinate chromosome and flat additive penalties | Update to reflect the variable-length list chromosome representation (`CraneChromosome`). Replace flat penalties with normalized failure fractions and settings-defined weights. |
| `system-overview.md` | Direct compilation dependency (technical debt warning) | Confirm that Application only references Domain, and remove references to the circular dependency. |
| `database.md` | Fictional ER diagram with `SiteRecord`, `ElementRecord`, etc. | Completely rewrite to map the tables in `CraneDbContext` (catalog) and `CreatedProjectDbContext` (project files). |
| `colors.md` | Fictional hex codes and brushes | Align the color system with the brand terracotta sienna `#D68A56` and keys defined in `Colors.xaml` and `Brushes.xaml`. |
| `design-system.md` | Fictional Poppins font family | Update typeface reference to **Exo 2**. |
| `dependency-injection.md`| Fictional viewmodel transient lifetimes and database singleton | Detail the mixed lifetimes (Singleton, Scoped, Transient) of viewmodels and the scoped/factory registration of `CraneDbContext`. |
| `mvvm.md` | Fictional `SetProperty` method | Update property setters to use the standard `OnPropertyChanged` pattern. |

---

# Documentation Alignment Roadmap

Following the completion and review of this system audit report, the following steps are proposed for the next phase (upon separate approval):

1.  **Phase 2A — Domain Realignment:** Correct `ifc.md`, `crane-engine.md`, and `ga-engine.md` to reflect the actual $2500 \text{ kg/m}^3$ density fallback, `ConservativeStep` load chart resolution, and normalized fitness score metrics.
2.  **Phase 2B — Architecture & Database Realignment:** Completely rewrite `database.md` and `folder-structure.md` to describe `CraneDbContext` and `CreatedProjectDbContext`, correcting fictional entity suffixes.
3.  **Phase 2C — UI & Typography Alignment:** Align `design-system.md` and `colors.md` with the **Exo 2** font and brand terracotta/sienna color tokens.
4.  **Phase 2D — Standards & Agent Onboarding:** Align `dependency-injection.md`, `mvvm.md`, `agent-context.md`, and the DoD checklists.
