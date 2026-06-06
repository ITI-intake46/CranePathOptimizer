# Coding Standards: Naming Conventions

Consistency in naming makes the codebase easy to read and navigate. Follow these naming rules across all projects in the solution.

---

## 1. Code Elements

| Element | Case Style | Example | Notes |
|---|---|---|---|
| **Classes / Structs** | PascalCase | `GeneticAlgorithmEngine` | Nouns describing responsibilities |
| **Interfaces** | PascalCase | `ICraneRepository` | Must begin with a capital `I` |
| **Public Properties** | PascalCase | `MaxCapacity` | |
| **Public Methods** | PascalCase | `RunOptimization` | Verb or verb-noun phrase |
| **Private Fields** | _camelCase | `_craneService` | Must prefix with underscore |
| **Local Variables** | camelCase | `craneRecord` | |
| **Enums / Values** | PascalCase | `IfcElementType` / `Column` | |

---

## 2. File and Architecture Artifacts

* **Views:** All WPF views must be named with the suffix `View` (e.g., `ResultsView.xaml`).
* **ViewModels:** All viewmodels must inherit from `ViewModelBase` and end with the suffix `ViewModel` (e.g., `ResultsViewModel.cs`).
* **Database Records (Entities):** All entities representing rows in SQLite tables must be suffixed with `Record` (e.g., `CraneRecord.cs`, `ElementRecord.cs`).
* **Schema Files:** Data Transfer Objects (DTOs) used for project serialization must end with `Schema` (e.g., `ProjectFileSchema.cs`, `CraneDataSchema.cs`).
* **Repository Abstractions:** Interfaces must be suffixed with `Repository` (e.g., `ICraneRepository.cs`).
* **Services:** Service classes must end with `Service` (e.g., `CraneService.cs`).

---

## 3. Asynchronous Method Naming

Any method that runs asynchronously and returns a `Task` or `Task<T>` must end with the **Async** suffix:

* **Correct:** `Task SaveProjectAsync(...)`
* **Incorrect:** `Task SaveProject(...)`

Exceptions are made for event handler delegates or interface overrides that are forced by external framework signatures (e.g., WPF commands, though inside those commands we call `Async` methods).
