# ADR-005: Use SQLite for Project File Format (`.craneopt`)

## Status
Accepted

---

## Context
The application needs to persist all project-related settings—imported elements, site boundary points, green setup areas, obstacles, crane database configs, and optimization configurations—to a user-selected file path. Typically, desktop applications use file formats like JSON or XML. However, since the database schema matches our planning context, we need to decide on an optimal project file serialization format.

---

## Decision
We chose to save and load project files as individual **SQLite database files** using the **`.craneopt`** file extension.

---

## Consequences

### Positive
* **Consistent Data Logic:** We reuse the existing [CraneDbContext](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs) architecture. No need to write custom JSON parser mapping logic.
* **Transactional Reliability:** SQLite guarantees transactional operations (ACID). If a save fails mid-operation, the file is not corrupted.
* **Efficient Loading:** We utilize Entity Framework Core eager loading (`Include()`) to pull the complete site tree in a single query.
* **Extensibility:** As we introduce new features (e.g. Lifting and Path sequences), we can easily update the project file schema by executing migrations on the file.

### Negative
* **Primary Key Management:** Saving requires resetting all database primary keys (`Id = 0`) before adding them to the target SQLite file. If not handled, EF Core attempts to preserve existing IDs, resulting in key constraint violations.
* **Binary Format:** The project file is a binary SQLite database, which is not human-readable or diffable in text-based version control tools.
