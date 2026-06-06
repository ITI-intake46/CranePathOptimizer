# Directory & Folder Structure

This document outlines the organization of code and resources in the **Mobile Crane Path Optimizer** repository.

---

## High-Level Solution Structure

The project solution file `CraneOptimizer.sln` includes 5 projects:

```
CraneOptimizer/
├── CraneOptimizer.Domain/            # Layer 1: Core domain entities, schemas, interfaces
├── CraneOptimizer.Application/       # Layer 2: Optimization engines, application services
├── CraneOptimizer.Infrastructure/    # Layer 3: DB context mappings, repositories, xBIM parsers
├── CraneOptimizer.UI/                 # Layer 4: WPF views, viewmodels, design system, 3D viewport
└── CraneOptimizer.Application.Tests/ # Unit and integration tests for engines & services
```

---

## Detailed Directory Tree

### 1. Domain Layer (`CraneOptimizer.Domain/`)
*   [Common/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common) — Core shared point representations (`DomainPoint3D.cs`).
*   [CreatedProject/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject) — Schema representations for `.craneopt` databases (e.g., `ProjectInfoEntity.cs`, `ProjectCoordinateReferenceEntity.cs`, `ProjectSettingEntity.cs`) organized under subfolders: `CraneFiles/`, `IfcFiles/`, `OptimizationFiles/`, `ProjectState/`.
*   [Diagnostics/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Diagnostics) — Hierarchical audit contexts (`IfcAuditResult.cs`, `IfcNormalizationModels.cs`, `RenderPerformanceStats.cs`).
*   [Entities/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Entities) — Catalog database entities (`Crane.cs`, `CraneSpecification.cs`, `TargetPoint.cs`, `Project.cs`, `ProjectIfcFile.cs`).
*   [Interfaces/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Interfaces) — Contracts for repositories and services (`ICraneRepository.cs`, `IIfcReaderRepository.cs`, `IIfcDiagnosticAuditService.cs`).
*   [Schemas/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Schemas) — Core geometry and spatial representations (`GreenAreaZone.cs`, `PrecastElement.cs`) and `Enums/` (`ZoneType.cs`).
*   [Services/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services) — Domain geometry calculators (`ZoneGeometryService.cs`).
*   [Units/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Units) — Unit conversion systems (`UnitSystem.cs`, `UnitConversions.cs`).

### 2. Application Layer (`CraneOptimizer.Application/`)
*   [Optimization/GeneticAlgorithms/Engines/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines) — Implements the 7 core optimization engines.
*   [Optimization/GeneticAlgorithms/Operators/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators) — Crossover, mutation, selection, and replacement genetic operators.
*   [Optimization/GeneticAlgorithms/Services/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services) — Pre-built indexes, load chart capacity resolving, and diagnostic logic.
*   [Optimization/GeneticAlgorithms/Models/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models) — Domain modeling for populations, solutions, chromosomes, and evaluations.
*   [Optimization/GeneticAlgorithms/Settings/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings) — Configuration classes for GA parameters ([GASettings.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs)).
*   [Optimization/GeneticAlgorithms/DTOs/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/DTOs) — Diagnostic and optimization input/output payload structures.
*   [Optimization/GeneticAlgorithms/Interfaces/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces) — Extensibility and testing decoupled boundaries.
*   [Optimization/Utilities/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities) — Performance profile tracing ([GAProfiler.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/GAProfiler.cs)) and geometry math ([SpatialMath.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/SpatialMath.cs)).
*   [Services/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Services) — Coordinates reader formatters and coordinate translation services.

### 3. Infrastructure Layer (`CraneOptimizer.Infrastructure/`)
*   [Contexts/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts) — DbContext classes: `CraneDbContext.cs` and project subfolder `CreatedProject/` containing `CreatedProjectDbContext.cs` and factories.
*   [Ifc/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc) — Concrete implementations of xBIM geometry and session controls (`IfcModelSessionService.cs`, `IfcModelSession.cs`).
*   [Migrations/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations) — Entity Framework schema upgrade snapshots for both databases.
*   [Repositories/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories) — Repository implementations (`CraneRepository.cs`, `IfcReaderRepository.cs`, `ProjectRepository.cs`, `IfcDiagnosticAuditService.cs`, `IfcNormalizationDecisionService.cs`).

### 4. UI Layer (`CraneOptimizer.UI/`)
*   [Core/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core) — Base classes for MVVM pattern, commands, hit-testing, and converters (`ViewModelBase.cs`, `RelayCommand.cs`, `Converters/`).
*   [Data/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Data) — View model DataTemplate bindings (`ViewDataTemplates.xaml`).
*   [Features/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features) — Subfolders grouping View (XAML) and ViewModel (C#) pairs for specific pages (e.g., `F2_IfcViewer/`, `F3_CraneSetup/`, `F5_ElementManagement/`, `F8_GAOptimizer/`, `F10_ProjectManagement/`, `F18_CoordinateReference/`).
*   [Resources/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources) — WPF dictionaries: `Colors.xaml`, `Brushes.xaml`, `Styles.xaml`, `Typography.xaml`, `ThemeService.cs`, and subdirectories (`DesignSystem/`, `Fonts/`, `Icons/`, `Shell/`, `Theme/`).
*   [Shell/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Shell) — The main window housing (`MainWindow.xaml`, `MainViewModel.cs`).

### 5. Tests Layer (`CraneOptimizer.Application.Tests/`)
*   [Crane/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/Crane), [Diagnostics/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/Diagnostics), [GA/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/GA), [IFC/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC) — Unit and integration tests.
