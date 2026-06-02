# Coding Standards: Data Transfer Objects (DTO) Rules

To maintain separation of concerns and prevent persistence models from leaking into transient operations, the system enforces rules on using Data Transfer Objects (DTOs) and dedicated entity mappings.

---

## 1. Domain Entities vs. Project Serialization Entities

*   **Catalog Database Entities (Domain):** Represent catalog data in the primary SQLite database (`crane.db`). They reside in [Entities/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Entities) (e.g. `Crane.cs`, `CraneSpecification.cs`, `TargetPoint.cs`, `Project.cs`).
*   **Project Serialization Entities (`*Entity`):** Reside under [CreatedProject/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject) and map exactly to SQLite tables in project `.craneopt` files. These structures separate the catalog schema from active project scene states:
    *   `ProjectInfoEntity`
    *   `ProjectIfcFileEntity`
    *   `ProjectIfcElementEntity`
    *   `ProjectModelFileEntity`
    *   `SiteZoneEntity`
    *   `CraneSetupEntity`

---

## 2. IFC Processing Abstractions

The `IIfcReaderRepository` processes xBIM models and returns domain elements mapped to the [PrecastElement.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Schemas/PrecastElement.cs) domain schema.

*   **Rule:** The IFC parsing service does not write to the database itself, nor does it create database entities directly.
*   **Mapping:** The ViewModels receive these `PrecastElement` domain schemas. When saving the project, the `ProjectService` maps them to `ProjectIfcElementEntity` records to save them inside the `.craneopt` file.

### Implementation Reference

File:
[IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)

Class:
`IfcReaderRepository`

Method:
`ReadElements()`
