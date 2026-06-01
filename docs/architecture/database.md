# Database Architecture

The application uses **Entity Framework Core** with the **SQLite** provider for persistent storage.

There are two distinct database configurations used in the application:
1. **Primary Workspace Database:** Main database created at `%APPDATA%/CraneOptimizer/craneoptimizer.db` on app startup to store catalog items (cranes, saved sites).
2. **Project Files (`.craneopt`):** Individual SQLite files created at user-specified paths that store site boundaries, obstacles, green areas, crane setups, and structural elements for a specific project.

---

## Entity Relationship (ER) Schema

The database model is configured within the [AppDbContext](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/AppDbContext.cs) class.

```mermaid
erDiagram
    SiteRecord ||--o{ ElementRecord : contains
    SiteRecord ||--o{ GreenAreaRecord : has
    SiteRecord ||--o{ SiteBoundaryPointRecord : bounded_by
    SiteRecord ||--o{ ObstacleRecord : has
    GreenAreaRecord ||--o{ GreenAreaPointRecord : polygon_points
    ObstacleRecord ||--o{ ObstaclePointRecord : polygon_points
    CraneRecord ||--o{ CraneLoadChartEntryRecord : limits

    SiteRecord {
        int Id PK
        string ProjectName
        string Description
        string IfcFilePath
    }
    ElementRecord {
        int Id PK
        int SiteRecordId FK
        string GlobalId
        string Name
        string ElementType
        double X
        double Y
        double Z
        double Length
        double Width
        double Height
        double Weight
        double Volume
        int SortOrder
    }
    GreenAreaRecord {
        int Id PK
        int SiteRecordId FK
        string Name
    }
    GreenAreaPointRecord {
        int Id PK
        int GreenAreaRecordId FK
        double X
        double Y
        int SortOrder
    }
    SiteBoundaryPointRecord {
        int Id PK
        int SiteRecordId FK
        double X
        double Y
        int SortOrder
    }
    ObstacleRecord {
        int Id PK
        int SiteRecordId FK
        string Name
        double Height
    }
    ObstaclePointRecord {
        int Id PK
        int ObstacleRecordId FK
        double X
        double Y
        int SortOrder
    }
    CraneRecord {
        int Id PK
        string ModelName
        double MaxCapacity
        double MinRadius
        double MaxRadius
        double MaxTipLoad
        double BoomLength
        double JibLength
        double CounterweightRadius
        double MaxHoistHeight
        double HoistSpeed
        double SlewSpeed
        double TrolleySpeed
        double TipLoadAtMaxRadius
        double OwnershipCost
        double BaseHeight
    }
    CraneLoadChartEntryRecord {
        int Id PK
        int CraneRecordId FK
        double Radius
        double Capacity
    }
```

---

## Migration History

Database changes are tracked via EF Core migrations located in the [Migrations](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations) folder:

### 1. `InitialCreate`
Creates the main entity tables:
* `CraneRecords` & `SiteRecords`
* `ElementRecords` (bound to `SiteRecords`)
* `GreenAreaRecords` & `GreenAreaPointRecords`
* `SiteBoundaryPointRecords`
* `ObstacleRecords` & `ObstaclePointRecords`

### 2. `AddCraneLoadChart`
* Adds the `CraneLoadChartEntryRecords` table.
* Adds the `BaseHeight` column to `CraneRecords`.

---

## Cascade Delete Rules

All relationships are configured with **Cascade Delete** via EF Core Fluent API:
* Deleting a `SiteRecord` deletes all associated elements, site boundary points, green areas, and obstacles.
* Deleting a `GreenAreaRecord` deletes all its child points (`GreenAreaPointRecord`).
* Deleting a `ObstacleRecord` deletes all its child points (`ObstaclePointRecord`).
* Deleting a `CraneRecord` deletes all its load chart entries (`CraneLoadChartEntryRecord`).
