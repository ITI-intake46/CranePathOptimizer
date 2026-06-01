# Directory & Folder Structure

This document outlines the organization of code and resources in the **Mobile Crane Path Optimizer** repository.

---

## High-Level Solution Structure

The project solution file `CraneOptimizer.sln` includes 5 projects corresponding to the Onion Architecture layers:

```
CraneOptimizer/
├── CraneOptimizer.Domain/            # Layer 1: Core domain models, repository interfaces
├── CraneOptimizer.Application/       # Layer 2: Optimization engines, application services
├── CraneOptimizer.Infrastructure/    # Layer 3: Database mappings, repositories, xBIM parsers
├── CraneOptimizer.UI/                 # Layer 4: WPF views, viewmodels, themes, 3D viewport
└── CraneOptimizer.Application.Tests/ # Unit and integration tests for engine & services
```

---

## Detailed Directory Tree

### 1. Domain Layer (`CraneOptimizer.Domain/`)
* [Common/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common) — Shared domain data classes (e.g. `IfcElementData.cs`, `IfcElementType.cs`).
* [CreatedProject/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject) — Schema representations for saving files.
* [Diagnostics/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Diagnostics) — Hierarchical diagnostic contexts and logging entry formats.
* [Entities/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Entities) — Database entities (e.g., `CraneRecord.cs`, `SiteRecord.cs`, `ElementRecord.cs`).
* [Interfaces/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Interfaces) — Contracts for repositories and services (e.g., `ICraneRepository.cs`, `IIfcModelService.cs`).
* [Schemas/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Schemas) — In-memory geometry validation classes (`Point2D.cs`, `GreenArea.cs`, `Obstacle.cs`, `SiteBoundary.cs`) featuring ray-casting containment tests.
* [Services/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services) — Log formatting and basic system service implementations (`DiagnosticService.cs`).
* [Units/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Units) — Conversion helpers (`UnitConversions.cs`) to handle standard unit transformations.

### 2. Application Layer (`CraneOptimizer.Application/`)
* [Optimization/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization) — The Genetic Algorithm engine loop, configuration models, chromosome structures, and fitness scoring.
* [Services/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Services) — Orchestration services and database adapters (`CraneService.cs`, `ProjectService.cs`).

### 3. Infrastructure Layer (`CraneOptimizer.Infrastructure/`)
* [Contexts/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts) — EF Core context class `AppDbContext.cs` targeting SQLite connections.
* [Ifc/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc) — Concrete implementations of xBIM geometry and attribute parsing routines.
* [Migrations/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations) — Entity Framework schema upgrade snapshots.
* [Repositories/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories) — DbContext CRUD actions.

### 4. UI Layer (`CraneOptimizer.UI/`)
* [Core/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core) — Base files for MVVM pattern, command handlers, and navigation services (`ViewModelBase.cs`, `RelayCommand.cs`, `NavigationService.cs`).
* [Data/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Data) — View model xaml registration mappings.
* [Features/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features) — Subfolders grouping the View (XAML) and ViewModel (C#) pairs for specific pages:
  * `Dashboard/`, `ProjectSetup/`, `SiteManagement/`, `CraneManagement/`, `Optimization/`, `Results/`, `Settings/`
* [Resources/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources) — WPF dictionaries defining branding colors, typefaces (Poppins), and custom element control templates.
* [Shared/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Shared) — XAML value converters.
* [Shell/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Shell) — The main window frame housing the application sidebar and content view placeholder (`MainWindow.xaml`).

### 5. Tests Layer (`CraneOptimizer.Application.Tests/`)
* [Crane/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/Crane), [Diagnostics/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/Diagnostics), [GA/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/GA), [IFC/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC), [ProjectPersistence/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/ProjectPersistence) — Unit test categories mapped to matching application/infrastructure layers.
* [TestInfrastructure/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/TestInfrastructure) — Temporary in-memory databases and factory configurations.
