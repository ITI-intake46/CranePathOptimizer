# Database Architecture

The application uses **Entity Framework Core** with the **SQLite** provider for persistent storage.

There are two distinct database configurations used in the application:
1.  **Catalog & Project History Database (`CraneDbContext`):** Main database created under the runtime output directory's `Data/crane.db` path on app startup to store catalog items (crane specifications, load charts, historical projects list).
2.  **Project Files (`.craneopt` / `CreatedProjectDbContext`):** Individual SQLite files created at user-specified paths that store site boundaries, obstacles, green zones, crane setups, and structural elements for a specific planning project.

---

## Catalog Schema (`CraneDbContext`)

The catalog database model is configured within [CraneDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs).

```mermaid
erDiagram
    Crane ||--o{ CraneSpecification : has
    CraneSpecification ||--o{ TargetPoint : load_chart
    Project ||--o{ ProjectIfcFile : imports

    Crane {
        int Id PK
        string Brand
        string ModelName
    }
    CraneSpecification {
        int Id PK
        int CraneId FK
        double MaxCapacity
        double MinRadius
        double MaxRadius
        double MaxBoomLength
        double MinBoomLength
        double CounterWeight
        double CraneDerating
        string OutriggerConfig
    }
    TargetPoint {
        int Id PK
        int CraneSpecificationId FK
        double Radius
        double BoomLength
        double Capacity
        int VisualColumnIndex
        string VisualColumnHeader
        int NumberOfLines
        string OperatingConditionLabel
    }
    Project {
        Guid Id PK
        string Name
        string ProjectFolderPath
        double ConcreteDensityKgPerM3
        string Notes
        bool IsFavorite
        bool IsDeleted
        DateTime LastOpenedDate
    }
    ProjectIfcFile {
        int Id PK
        int ProjectId FK
        string FilePath
        string FileName
    }
```

### Project Entity

*   `ConcreteDensityKgPerM3` (`double`, default `2500.0`) stores the per-project concrete density used by IFC volume-based weight fallback.

---

## Project File Schema (`CreatedProjectDbContext`)

The `.craneopt` database model is configured within [CreatedProjectDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs) and represents the serialized state of a specific project scene.

```mermaid
erDiagram
    ProjectInfoEntity ||--o{ ProjectIfcFileEntity : has
    ProjectInfoEntity ||--o{ ProjectIfcElementEntity : contains
    ProjectInfoEntity ||--o{ SiteZoneEntity : has
    ProjectInfoEntity ||--o{ CraneSetupEntity : has
    ProjectInfoEntity ||--o{ ProjectCraneSnapshotEntity : contains
    ProjectCraneSnapshotEntity ||--o{ ProjectCraneLoadChartSnapshotEntity : has

    ProjectInfoEntity {
        int Id PK
        string ProjectGuid
        string Name
        string UnitSystem
        int SchemaVersion
        int CacheVersion
    }
    ProjectIfcFileEntity {
        int Id PK
        int ProjectInfoId FK
        string FileName
        string RelativePath
        string Role
        string OriginalFullPath
        long FileSizeBytes
        DateTime LastWriteUtc
        string SchemaVersion
        string TransformJson
        string MetadataJson
        bool IsActive
        bool IsCacheValid
        int ElementCount
    }
    ProjectIfcElementEntity {
        int Id PK
        int ProjectInfoId FK
        int IfcFileId FK
        string GlobalId
        string Name
        string DisplayName
        string Mark
        string ElementType
        string StoreyName
        string Material
        double LengthM
        double WidthM
        double HeightM
        double VolumeM3
        double WeightTons
        double COG_X
        double COG_Y
        double COG_Z
        double BBoxMinX
        double BBoxMinY
        double BBoxMinZ
        double BBoxMaxX
        double BBoxMaxY
        double BBoxMaxZ
        bool IsLiftable
        string RawIfcType
        string ResolvedCategory
        string Role
        bool IncludeInRender
        bool IncludeInElementTree
        bool IncludeInGa
        string WeightSource
        string WeightTrustLevel
    }
    SiteZoneEntity {
        int Id PK
        int ProjectInfoId FK
        string Name
        string ZoneType
        string VerticesJson
    }
    CraneSetupEntity {
        int Id PK
        int ProjectInfoId FK
        string SceneCraneGuid
        string CraneName
        double PositionX
        double PositionY
        double PositionZ
        double BoomLength
        bool IsValidPlacement
    }
    ProjectCraneSnapshotEntity {
        int Id PK
        int ProjectInfoId FK
        string SourceCraneId
        string CraneName
        string CraneType
        string Manufacturer
        string Model
        double MaxCapacityTons
        double VehicleLengthM
        double VehicleWidthM
        double VehicleHeightM
        double OutriggerLengthM
        double OutriggerWidthM
        double MinRadiusM
        double MaxRadiusM
        double MinBoomLengthM
        double MaxBoomLengthM
        double MaxHookHeightM
        double CounterWeightTons
        double MachineRotation
        double DeratingRatio
        string ConfigurationJson
        string OutriggerConfig
        DateTime SnapshottedAtUtc
    }
    ProjectCraneLoadChartSnapshotEntity {
        int Id PK
        int CraneSnapshotId FK
        double RadiusM
        double BoomLengthM
        double CapacityTons
        int VisualColumnIndex
        string VisualColumnHeader
        int NumberOfLines
        string OperatingConditionLabel
        string Notes
    }
```

---

## Migration History

Database changes are tracked via EF Core migrations located in [Migrations/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations):

### Catalog migrations:
*   `20260224012129_InitialCreate` - Sets up standard catalog schema.
*   `20260427211132_AddProjectTables` - Connects imported project history metadata.
*   `20260428183336_AddCSVToCeaneSpecification` - Adds CSV mapping support for specs.
*   `20260519000100_NormalizeUnitSystemStorage` - Standardizes metrics conversions.
*   `20260521194249_AddProjectFolderpathtoProject` - Integrates directory paths.
*   `20260528181000_AddFullFidelityLoadChartMetadata` - Adds `VisualColumnIndex` and `VisualColumnHeader` to `TargetPoints` for exact-visual column matching.
*   `20260603120000_AddProjectConcreteDensity` - Integrates per-project concrete density adjustments in `Projects`.
*   `20260608165000_AddLoadChartNumberOfLinesMetadata` - Adds `NumberOfLines` metadata to `TargetPoints`.
*   `20260609011000_AddLoadChartOperatingConditionLabel` - Adds `OperatingConditionLabel` to `TargetPoints`.

### Project `.craneopt` migrations:
*   `20260521202008_InitialCreatedProjectDb` - Sets up baseline schema for project elements.
*   `20260523163420_AddProjectStateTables` - Adds scene snapshots and optimization run tables.
*   `20260525231817_AddLightweightElementMetadata` - Integrates element properties and visibility flags.
*   `20260528181100_AddFullFidelityLoadChartSnapshotMetadata` - Adds `VisualColumnIndex` and `VisualColumnHeader` columns to `ProjectCraneLoadChartSnapshots` for exact-visual matching.
*   `20260530180000_AddProjectOptionalFields` - Adds optional fields (such as viewport states) to the project state.
*   `20260608165100_AddLoadChartNumberOfLinesSnapshotMetadata` - Adds `NumberOfLines` to `ProjectCraneLoadChartSnapshots`.
*   `20260609011100_AddLoadChartSnapshotOperatingConditionLabel` - Adds `OperatingConditionLabel` to `ProjectCraneLoadChartSnapshots`.

---

## Cascade Delete Rules

All relationships are configured with **Cascade Delete** via EF Core Fluent API:
*   Deleting a `Crane` deletes all its child `Specifications`.
*   Deleting a `CraneSpecification` deletes all its child `TargetPoints`.
*   Deleting a `Project` deletes all associated imported `ProjectIfcFiles`.
*   Deleting a `ProjectInfoEntity` in `.craneopt` deletes all child elements, snapshots, zones, and setups.

### Implementation Reference

File:
[CraneDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CraneDbContext.cs) and [CreatedProjectDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs)
