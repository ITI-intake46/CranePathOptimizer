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

## F. Phase 3: Coordinate Reference Modes UI

Phase 3 implements user-controlled coordinate reference modes in the user interface. The UI exposes only two selectable modes:
1. **Local Only** (`IsEnabled = false`): The application uses raw local coordinates only. Local coordinates default to local values and are isolated.
2. **IFC Origin Mapping** (`IsEnabled = true`, `Source = "IfcMapConversion"`):
   * **Behavior**: Uses the IFC local origin directly. It does **NOT** require the user to pick a point in the viewport or perform manual calibration.
   * **Mesh Isolation**: The model stays exactly in local coordinates. It is loaded where it is drawn relative to its own local origin (e.g. if drawn 10m from local X and 15m from local Y in Revit/Tekla, it appears at (10, 15, 0) locally). The model meshes are not moved, rotated, scaled, or transformed.
   * **Origin Mapping**: Local (0,0,0) maps directly to the `IfcMapConversion`-extracted survey Easting, Northing, and Elevation. `LocalReferenceX/Y/Z` are fixed at `0.0`.
   * **Readout translation**: Only the coordinate readouts in the UI are converted from local coordinates to survey coordinates (Easting, Northing, Elevation) dynamically using rotation and scale parameters.

### Mode Availability and Rules
* **IFC Origin Mapping Availability**: This mode is selectable if active IFC georeferencing is loaded in the current import session, or if a saved project already contains an IFC-derived `ProjectCoordinateReference` in the SQLite database. If neither is available, "IFC Origin Mapping" is disabled, and an informational warning message is shown.
* **IFC2x3 Restriction**: IFC Origin Mapping only activates from valid, projected `IfcMapConversion` data (Easting/Northing/Elevation). IFC2x3 `IfcSite` latitude/longitude coordinates are strictly informational and do not enable active E/N mapping.
* **Manual Mode Protection**: A third mode, **Manual Mapping (Preserved)**, is not selectable in the mode ComboBox in Phase 3, but is recognized and preserved if loaded from legacy databases or future extensions. If the project contains a manual reference (`Source = "Manual"` or `"ManualSinglePoint"`), the UI displays it as active, protecting existing manual configurations from silent overwrite.

### Safety & Engine Insulation
* **Engine Isolation**: The Genetic Algorithm (GA) solver, viewport rendering, 3D mesh geometry, collision/constraint detection, crane radius checks, load chart checks, and precast element local coordinates remain strictly local and unchanged. All calculations, bounds, and radius checks remain local and unchanged.
* **No Database Migrations**: Active coordinate mode is inferred safely at run-time from `IsEnabled` and `Source` properties, avoiding database schema changes.

---

## G. Future / Pending Phases

### Picked Local Reference Point (Future Mode 3)
* **Description**: A manual alignment method where a user selects or inputs a local coordinate point $(x_p, y_p, z_p)$ to map to a specific survey reference point $(E_s, N_s, H_s)$, with optional rotation/scale.
* **Status**: **Pending/Future**. Viewport hit-testing, pick-point UI, two-point calibration, and manual local anchor selection are NOT implemented in Phase 3.

---

## H. Phase 3.1: Move Coordinate Reference Mode Choice into IFC Load Options

Phase 3.1 optimizes the user experience by moving the choice of coordinate reference mode directly into the **IFC Load Options** dialog before importing.

### 1. IFC Load Options Dialog Integration
* **Asynchronous Pre-scan**: When a user selects an IFC file for import, the application initiates a lightweight background pre-scan using `IIfcGeoreferencingExtractor.ExtractFromFile(filePath)`. This pre-scan opens the IFC model without building full 3D geometry contexts, taking a fraction of a second.
* **Non-Blocking UI**: The pre-scan task runs on a background thread. While scanning, the dialog displays a progress message `"Scanning georeferencing..."` and keeps the UI responsive. Once the task finishes, the UI dispatcher updates the dialog state:
  * If a valid `IfcMapConversion` is found, the `"IFC Origin Mapping"` option is enabled and a success status is shown.
  * If no georeference metadata is found, or if only informational IFC2x3 latitude/longitude is found, the `"IFC Origin Mapping"` option is disabled, defaulting the choice to `"Local Only"`, and an appropriate warning message is displayed.
* **Selection Result**: The selected `CoordinateReferenceMode` is stored in the `IfcLoadOptionsDto` and passed into the import pipeline.

### 2. Import Pipeline & Metadata Review Flow
* **Apply Selected Mode**: During model placement, `IfcViewerViewModel` applies the georeferencing metadata:
  * If the user selected `"IFC Origin Mapping"`, the reference is saved with `IsEnabled = true` and `Source = "IfcMapConversion"`.
  * If the user selected `"Local Only"`, but the IFC contained valid georeferencing, the metadata is still extracted and applied to `ProjectCoordinateReference` but with `IsEnabled = false`.
* **Inactive Review Status**: If `"Local Only"` was selected for a georeferenced file, the Coordinate Reference tab remains inactive (`IsEnabled = false`) and displays `"IFC Georeferencing exists but is inactive (Local Only)."`, allowing the user to review the survey metadata without activating it. The metadata source displays `"Local Only (IFC Georeferencing Inactive)"`.

### 3. Coordinate Reference Tab UI Adjustments
* **Primary Review/Status Focus**: The Coordinate Reference tab is now primarily for review and status display.
* **ComboBox Label Cleanup**: The ComboBox displays clean user-friendly labels:
  * `"Local Only"`
  * `"IFC Origin Mapping"`
  No raw enum, object, or type names appear in the UI.
* **Legacy Mode Preservation**: If a legacy manual coordinate mapping is active in the database, the dropdown ComboBox is hidden, and a read-only label `"Manual Mapping (Preserved)"` is shown to prevent accidental overwrite.

---

## I. Phase 3.2: Clean Dropdown Mode Labels
* **UI ComboBox Clean Labels**: Updated the Reference Mode ComboBox display style and converters to guarantee that only user-friendly labels appear in the dropdown (specifically `"Local Only"` and `"IFC Origin Mapping"`).
* **Prevention of Raw Type Names**: Removed any instances of raw class or enum names (`CraneOptimizer.UI.Feature...`) from appearing in the user interface.

---

## J. Phase 3.3: Automatic IFC Origin Placement and Manual Move Prevention
* **No Placement Pick-point Prompts**: Selecting `"IFC Origin Mapping"` during IFC import completely bypasses placement mode. It does not prompt the user to choose a model landing or insertion point in the viewport, nor does it register a click listener on the interaction service.
* **Identity Placement Offset**: Uses a zero-offset identity transformation $(0, 0, 0)$ for placement coordinates. The model is positioned exactly where it was drawn in its source BIM authoring tool (e.g. Autodesk Revit, Tekla Structures).
* **Disabled Move IFC command**: While an active IFC Origin Mapping reference is loaded in the project (`IsEnabled = true` and `Source = "IfcMapConversion"`), the manual "Move IFC" operation is disabled in the UI to prevent model drift and coordinate misalignment.

---

## K. Phase 4: UI Readouts Review, Quality Improvement, and Consistency Audit
* **Standardized Coordinate Terminology**: Applied standard, uniform terminology throughout the application interface to distinguish local coordinate spaces from real-world survey coordinates:
  * Local coordinate labels: `Local X`, `Local Y`, `Local Z` (meters, formatted as `{value:F3} m` or similar).
  * Survey coordinate labels: `Easting`, `Northing`, `Elevation` (meters, formatted as `{value:F3} m`).
* **Active/Inactive State Consistency**:
  * If coordinate mapping is active (`IsEnabled = true`), the readouts display the calculated `Easting`, `Northing`, and `Elevation`.
  * If mapping is inactive/Local Only (`IsEnabled = false`), local properties continue to display local point values normally, while the survey readout clearly shows: `"Survey: Not active / Local Only"`.
* **WPF Interface Updates**:
  * **Coordinate Reference Tab**: Replaced "REFERENCE CHECK" preview labels and tooltips with the standard terms. Added read-only fields for informational `Latitude` / `Longitude` metadata fields and increased the metadata layout widths to prevent clipping.
  * **Status Bar / Cursor Readouts**: Updated the live cursor coordinate readout to display `Local X`, `Local Y`, and `[Default Local Z: 0.00m]`. When active, it displays the mapped `Easting` and `Northing`. Z coordinate is explicitly labeled as default/approximate to avoid misrepresenting cursor 2D position as accurate 3D snapped elevation.
  * **Precast Element Property Grid**: Formatted both local coordinates and active survey coordinates. Displays `"Survey: Not active / Local Only"` when survey mapping is inactive.
  * **GA Optimizer Results Panel**: Standardized the coordinate details grid and optimal base position display cards to clearly show both local and converted survey coordinate readouts without altering the optimization solver logic.

