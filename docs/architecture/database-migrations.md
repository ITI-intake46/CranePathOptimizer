# Database Schema & EF Core Migrations

This document details the EF Core database schema migration lifecycle, version checks, and schema validation checks for SQLite project databases (`.craneopt`).

---

## 1. Schema Lifecycle & EF Core

The database models are configured in [CraneDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs) and [CreatedProjectDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs).
- **Migration Generation:** Changes are generated using EF Core commands in the infrastructure layer:
  ```bash
  dotnet ef migrations add [MigrationName] --project CraneOptimizer.Infrastructure --startup-project CraneOptimizer.UI
  ```
- **Automatic Migration Application:** On app startup, the catalog database `crane.db` is migrated automatically. For project files, migrations are executed upon file open:
  ```csharp
  dbContext.Database.Migrate();
  ```

---

## 2. `.craneopt` Schema Versioning

Project databases are SQLite files with a `.craneopt` extension:
- **Version Tracking:** The `ProjectInfoEntity` contains schema version trackers:
  ```csharp
  public int SchemaVersion { get; set; }
  public int CacheVersion { get; set; }
  ```
- **Schema Validation Check:** Upon opening a project file, the system queries the schema version. If it detects a mismatch, it triggers database migration or prompts the user if the file was created in a newer version.
