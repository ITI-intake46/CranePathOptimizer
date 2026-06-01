# Coding Standards: Data Transfer Objects (DTO) Rules

To maintain separation of concerns and prevent persistence models from leaking into transient operations, the system enforces rules on using Data Transfer Objects (DTOs).

---

## 1. Entity vs. DTO Separation

* **Entities (`*Record`):** Represent rows in the primary application database. They contain system IDs (primary keys) and database relation metadata (foreign keys). They should only exist within repositories, contexts, services, and viewmodels that directly handle database state.
* **DTOs & Schemas (`*Data`, `*Schema`):** Stateless structures used to transmit data across boundary layers (e.g. IFC extraction $\rightarrow$ UI, UI $\rightarrow$ Project File). They must contain no database annotations, no primary keys, and no database configuration fields.

---

## 2. Specific Serialization Schemas

When serializing a project to a `.craneopt` SQLite file, we must use the schema objects defined in [CraneOptimizer.Domain.CreatedProject](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject):
* **ProjectFileSchema:** Serves as the blueprint for serializing the complete project context.
* **CraneDataSchema & LoadChartEntrySchema:** Encapsulate crane definitions without database IDs.
* **SiteDataSchema, ElementDataSchema, GreenAreaDataSchema, PointDataSchema, ObstacleDataSchema:** Maps spatial site boundaries without tracking entities.

During saving, the [ProjectService](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Services/ProjectService.cs) strips primary keys from records (setting `Id = 0`) and mapping them, letting the target database auto-generate fresh primary keys.

---

## 3. IFC Processing DTOs

The `IIfcModelService` must return an [IfcModelResult](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common/IfcModelResult.cs) containing a list of [IfcElementData](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common/IfcElementData.cs). 

* **Rule:** The IFC parsing service does not write to the database itself, nor does it create database entities directly.
* **Mapping:** The ViewModels receive these `IfcElementData` DTOs and convert them into `ElementRecord` entities only when the user commits to saving the imported project.
