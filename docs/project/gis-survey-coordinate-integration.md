# GIS / Survey Coordinate Integration (Phases 1 & 2)

This document details the architecture, design boundaries, and implementation details for the GIS / Survey Coordinate Integration within the **Mobile Crane Path Optimizer** application.

---

## A. Coordinate Space Architecture

The application operates with two strictly separated, independent coordinate spaces: **Local Model Space** and **Survey / GIS Space**. 

```
                                  +---------------------------------------+
                                  |           LOCAL MODEL SPACE           |
                                  | - Units: Meters                       |
                                  | - Origin: IFC / DXF Local Origin      |
                                  | - Used by: SQLite DB, GA Optimizer,   |
                                  |   Helix Viewport, Constraints, Radius |
                                  +---------------------------------------+
                                                      |
                                                      |  LocalToSurvey(x, y, z)
                                                      v
                                  +---------------------------------------+
                                  |          SURVEY / GIS SPACE           |
                                  | - Units: Meters (Easting/Northing)    |
                                  | - Origin: Survey base point / grid    |
                                  | - Used by: UI Coordinate Readouts,    |
                                  |   Element Properties, Reports/Logs    |
                                  +---------------------------------------+
```

### 1. Local Model Space
* **Units**: Meters (automatically scaled and normalized from the source file's units).
* **Origin**: Defined by the $(0,0,0)$ position in the imported IFC or DXF file.
* **Engine/Functional Use**:
  * **Database Persistence**: Centroids (COGs), bounding boxes, obstacle zones, and crane setup coordinates are stored raw in local meters in the SQLite database.
  * **Genetic Algorithm (GA)**: The optimization engine runs exclusively in local coordinates.
  * **Lifting & Physics calculations**: Crane radius checks, load chart interpolations, boom lengths, and clearance checks are calculated using local coordinate values.
  * **Helix 3D Rendering**: Viewport meshes are loaded, transformed, and displayed using their raw local coordinate locations.
  * **Constraint Engine**: Allowed/forbidden zone containment checks (e.g., ray-casting polygon checks) operate in local space.

### 2. Survey / GIS Space
* **Units**: Easting (E), Northing (N), and Orthogonal Elevation (H) in meters (or informational latitude/longitude decimal degrees).
* **Origin**: Defined by `ProjectCoordinateReference` transformation parameters.
* **Functional Use**:
  * **UI Metadata Readout**: Status bar coordinates under the cursor, property grid values for precast elements, and the GA optimal base setup coordinates display converted survey coordinates.
  * **CSV Diagnostics**: Per-element diagnostics logs write survey-translated coordinates alongside local ones for integration with external GIS software.
  * **Future Reports/Exports**: Output documentation displaying crane setup parameters will use these real-world coordinates.

> [!IMPORTANT]
> **The Core Architectural Safety Rule**
> Survey and GIS coordinate coordinates are treated strictly as **presentation metadata and output readouts**. 
> They **do not** transform, shift, rotate, or scale stored IFC element coordinates or DXF zone geometries. They **do not** affect GA candidate placement generation, constraint checking, radius calculations, or viewport rendering. Keeping the GA and spatial engines in local coordinates ensures high floating-point precision, performance, and mathematical stability.

---

## B. Phase 1: SQLite Database Persistence

Prior to Phase 1, project coordinate reference parameters were stored in a standalone file `coordinate_reference.json` in the active project directory, completely bypassing the SQLite database, with VM coordinates defaulting to zero. In Phase 1, coordinate reference persistence was fully integrated into the database.

### Implementation Details
1. **SQLite Integration**: `ProjectCoordinateReferenceEntity` is mapped in EF Core via [CreatedProjectDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs) and configured via [ProjectCoordinateReferenceConfiguration.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectConfigurations/ProjectCoordinateReferenceConfiguration.cs).
2. **Primary Persistence**: The per-project `.craneopt` SQLite database is the primary source of truth. The standalone `coordinate_reference.json` file is maintained only as a backward-compatible fallback for older projects.
3. **Safe Fallback & Migration**: When opening old projects lacking the database table or record, the system gracefully fallbacks to reading `coordinate_reference.json` or instantiates a default disabled reference.
4. **Single-Record Pattern**: Saving or updating uses a secure single-record pattern (Insert-or-Update) in the `ProjectCoordinateReference` table, preventing row duplication.
5. **UI Bindings**: [CoordinateReferenceViewModel.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F18_CoordinateReference/ViewModels/CoordinateReferenceViewModel.cs) is fully bound to the database entity. Local Reference X/Y/Z coordinates are persisted and no longer hardcoded to zero.

### Related Codebase Files
* **Entity**: [ProjectCoordinateReferenceEntity.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject/ProjectCoordinateReferenceEntity.cs)
* **Configuration**: [ProjectCoordinateReferenceConfiguration.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectConfigurations/ProjectCoordinateReferenceConfiguration.cs)
* **Context**: [CreatedProjectDbContext.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContext.cs)
* **Migration**: [20260612183958_AddProjectCoordinateReferenceTable.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260612183958_AddProjectCoordinateReferenceTable.cs)
* **Service**: [ProjectStateService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectStateService.cs)
* **ViewModel**: [CoordinateReferenceViewModel.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F18_CoordinateReference/ViewModels/CoordinateReferenceViewModel.cs)

---

## C. Phase 2: IFC Georeferencing Extraction

Phase 2 automates the extraction of GIS and survey metadata from imported IFC files using the xBIM parsing engine.

### Implementation Details
1. **Extraction Flow**: During IFC model loading, the import pipeline invokes [IfcGeoreferencingExtractor.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcGeoreferencingExtractor.cs) to parse georeferencing coordinates.
2. **IFC4 Preferred Georeferencing**:
   * **`IfcMapConversion`** mapping:
     * `Eastings` $\rightarrow$ `SurveyEasting`
     * `Northings` $\rightarrow$ `SurveyNorthing`
     * `OrthogonalHeight` $\rightarrow$ `SurveyElevation`
     * `Scale` $\rightarrow$ `Scale`
     * `XAxisAbscissa` & `XAxisOrdinate` $\rightarrow$ Calculated `RotationToNorthDegrees` (derived via arctangent rotation of the X-axis vector).
   * **`IfcProjectedCRS`** metadata:
     * CRS Name $\rightarrow$ `CoordinateSystemName`
     * Description $\rightarrow$ `CoordinateSystemDescription`
     * EPSG code (where available) $\rightarrow$ `EpsgCode`
3. **IFC2x3Fallback**:
   * IFC2x3 files lack projected coordinate structures. The extractor fallbacks to reading **`IfcSite.RefLatitude`**, **`IfcSite.RefLongitude`**, and **`IfcSite.RefElevation`**.
   * Latitude and longitude values are parsed from DMS (Degrees, Minutes, Seconds, Microseconds) arrays into decimal degrees.
   * `CoordinateSystemName` is populated as `"WGS84"`.
   * **Warning**: DMS values represent geographic curves and are stored strictly as informational metadata. They **must not** be treated as Easting/Northing coordinate values.
4. **Manual Reference Protection**:
   * If a user has already defined or enabled a manual coordinate reference in the project (i.e., `Source` is `Manual` or `ManualSinglePoint`, or the reference is `IsEnabled`), the IFC import **will not** silently overwrite it. The georeferencing extraction will only auto-populate the reference if it is disabled/empty.
5. **Revit & Tekla Exporters**:
   * IFC4 files exported from Autodesk Revit or Tekla Structures include `IfcMapConversion` structures if "Project Base Point" or "Survey Point" shared coordinate options are set.
   * IFC2x3 exports from these systems typically only expose `IfcSite` latitude/longitude or no coordinates at all. Exporter settings validation is required for specific modeling platforms.

### Related Codebase Files
* **DTO**: [IfcGeoreferencingResult.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/CreatedProject/IfcGeoreferencingResult.cs)
* **Extractor Interface**: [IIfcGeoreferencingExtractor.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Interfaces/IIfcGeoreferencingExtractor.cs)
* **Extractor Implementation**: [IfcGeoreferencingExtractor.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcGeoreferencingExtractor.cs)
* **Reader Repository**: [IIfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Interfaces/IIfcReaderRepository.cs) & [IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)
* **UI ViewModel**: [IfcViewerViewModel.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ViewModels/IfcViewerViewModel.cs)
* **App Entrypoint**: [App.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs)
* **Migration**: [20260612192855_AddCoordinateReferenceMetadataFields.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/20260612192855_AddCoordinateReferenceMetadataFields.cs)
* **Unit Tests**: [IfcGeoreferencingExtractorTests.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC/Georeferencing/IfcGeoreferencingExtractorTests.cs)

---

## D. Explicit Non-Goals & Safety Boundaries

The following operations are **not** implemented in Phase 1 or Phase 2 and are intentionally out of scope to preserve engine integrity:

1. **Pick-Point Reference Mapping**: Users cannot select a 3D vertex in the viewport to define origin offsets.
2. **DXF Georeferencing Extraction**: The DXF import service does not extract coordinates from CAD projection headers; alignment remains a manual transformation matrix process.
3. **DXF/IFC Co-Registration**: There is no automated alignment or validation comparing DXF zones against IFC coordinate references.
4. **Report / Export Formatting**: Final PDF or Excel optimization reports have not been updated to output survey coordinates.
5. **GA transformations**: The Genetic Algorithm is completely insulated from georeferencing translations and operates strictly on local geometries.
6. **Mesh Transformation**: Viewport meshes are never shifted, rotated, or scaled based on survey coordinate transformations.
7. **Reprojection Engine**: The application does not perform mathematical transformations from Latitude/Longitude (geographic coordinates) to Easting/Northing (projected grid coordinates).

---

## E. Testing & Validation

### 1. Test Suite Coverage
A total of **49 coordinate-related unit tests** have been successfully executed and passed:
* **`CoordinateReferenceTests`** (30 passed): Validate the mathematical transformation service (`ProjectCoordinateTransformService`), testing translation offsets, scale, rotation angles, X-axis vector alignments (such as $(1,0)$ and $(0,1)$), and VM state properties.
* **`IfcSessionPersistenceTests`** (10 passed): Validate database CRUD operations, single-record persistence patterns, and JSON fallback compatibility.
* **`IfcGeoreferencingExtractorTests`** (9 passed): Validate the xBIM parsing logic, DMS to decimal degree conversions, extraction of IFC4 map conversion variables, and metadata properties.

### 2. Fixture Limitations
> [!WARNING]
> While the test suite uses mock models to validate extractor calculations, **real exporter validation with Revit and Tekla fixture files remains pending**. Users must verify exported settings on native BIM platforms.

---

## F. Next Phase

### Phase 3 — Coordinate Reference UI: Origin Mapping vs Picked Local Reference Point
The next development phase will focus on user-controlled reference point mappings, allowing users to choose between two alignment methods in the UI:
1. **Local Origin Mapping**: Local coordinates $(0,0,0)$ map directly to the survey Easting/Northing/Elevation.
2. **Picked Local Reference Point**: A user-specified local position $(x_p, y_p, z_p)$ maps to the survey coordinates.

*Note: Phase 3 is not implemented in this documentation-only update.*
