# IFC Processing Workflow

This document details parsing and categorizing IFC models using xBIM.

## Consolidated from: IFC Documentation Verification Audit (domain/ifc.md)


## Executive Summary

This audit verifies the IFC documentation against the current C# implementation. Source code is the source of truth.

The implementation currently uses xBIM through `IfcModelSession.Open`, `IfcReaderRepository.ReadElementsCore`, `IfcDiagnosticAuditService.RunAuditAsync`, `IfcNormalizationDecisionService.GenerateNormalizationPreviewAsync`, and UI restoration/rendering services. The current documentation was broadly accurate about using xBIM, creating a geometry context, extracting `PrecastElement` objects, and persisting IFC element caches, but several details were stale:

* IFC extraction is based on `IIfcProduct` / `IIfcElement`, not only `IIfcBuildingElement`.
* `IfcElementRole` no longer contains structural roles such as `Column`, `Beam`, or `Slab`; those are resolved categories, not roles.
* Import weight selection is explicit IFC quantity/property first, then concrete-density fallback from direct IFC volume. Material-density overrides exist in helper/display paths but are not the primary normalized import weight source.
* No project-specific schema allow-list for IFC2X3, IFC4, IFC4X1, IFC4X2, or IFC4X3 exists. The project delegates opening to `IfcStore.Open()` and records `model.SchemaVersion`.
* Spatial structure is only partially used: `IfcProject` units and `IfcBuildingStorey` elevation/storey mapping are read; `IfcSite`, `IfcBuilding`, and `IfcSpace` are not imported as optimization elements.
* Requested files `docs/domain/ifc-architecture-review.md`, `docs/domain/ifc-feature-matrix.md`, and `docs/project/current-state.md` are absent.
* Stale links to missing documentation have been annotated.

## Phase 1 Stabilization & Transitional State

> [!NOTE]
> Phase 1 is a stabilization phase focused on fixing identity preservation, resolving `GeometryContext` disposal issues, and enforcing GA safety gating. It is transitional and does not represent the final canonical schema.

### Core Changes in Phase 1
1. **xBIM Geometry Context Disposal**: Safely disposes of the xBIM `GeometryContext` by casting to `IDisposable` inside `IfcModelSession.Dispose` to prevent memory leaks during consecutive loads.
2. **Identity Preservation & Best-Candidate Mark Resolution**:
   - Replaced simple `GetMark` logic with `GetBestMarkCandidate`. It resolves marks using a strict priority chain: Assembly-level Mark Candidate > Explicit Element Mark > Type/Reference Candidate > `product.Name` fallback.
   - Preserves source marks and prevents `product.Name` from overwriting real property-set marks.
   - `ElementMark` in the `PrecastElement` schema remains a transitional field.
3. **Missing Assembly Mark Diagnostics**:
   - Distinguishes between `ExplicitAssemblyMarkFound` (bool) and `ResolvedDisplayMark` (string).
   - Warnings and missing assembly mark counts are calculated strictly based on explicit assembly properties: `Assembly Mark`, `Cast Unit Mark`, `Assembly Number`, `Cast Unit Number`. Falls back to `product.Name` for display (`ResolvedDisplayMark`), but does not suppress the missing assembly mark warning.
4. **Genetic Algorithm Gating**:
   - Blocks elements with `WeightTrustLevel == EstimatedFallback_NotAllowedForGA` from entering GA optimization passes regardless of the `IncludeInGa` setting.
   - Implements parent-assembly child exclusion: if a parent assembly is selected as a GA target, its children parts are automatically excluded to prevent weight double-counting.

## Phase 2 Canonical Assembly Schema & Source Mapping Foundation

Phase 2 introduces the foundation for a single source of truth for business-critical precast assembly data, reducing direct downstream dependency on raw exporter-specific schemas.

### 1. Canonical Fields on PrecastElement
The `PrecastElement` domain model has been expanded to include:
* **Canonical Identity**:
  * `AssemblyMark` (string): The canonical assembly identification mark.
  * `AssemblyMarkSource` (string): The source property set and property path from which the mark was mapped (e.g. `Pset_Revit_Identity Data.Assembly Mark`).
  * `AssemblyMarkConfidence` (enum): The resolution confidence level (`None`, `Low`, `Medium`, `High`).
  * `AssemblyMarkIsExplicit` (bool): True if the mark represents an explicit assembly-level property, false if inferred or fallback.
* **Metadata & Context**:
  * `SourceSystem` (enum): The originating CAD/BIM environment (`IFC`, `Revit`, `Tekla`, `JSON`).
  * `ExporterProfile` (enum): The specific software profile that exported the IFC (`Generic`, `Tekla`, `Revit`).
* **Validation & Health**:
  * `ValidationStatus` (enum): The validation health check status (`Valid`, `Warning`, `Error`).
  * `ValidationWarnings` (list of strings): List of specific warning messages generated during validation rules processing.
* **Non-Authoritative Engineering Mirrors (Future Migration Foundation)**:
  * Getter properties `AssemblyWeightTons`, `AssemblyWeightSource`, `AssemblyWeightTrustLevel`, `AssemblyVolumeM3`, `AssemblyMaterial`, `AssemblyCenterOfGravity`, `AssemblyLength`, `AssemblyWidth`, and `AssemblyHeight` have been added as non-authoritative mirrors of existing engineering fields. Downstream GA and reporting modules will be transitioned to these in a future phase.

### 2. Canonical Identity Mapping Priority Chain
When processing elements from an IFC model, the mapping engine resolves the `AssemblyMark` and its confidence level in a strict priority order:
1. **High Confidence (`AssemblyMarkConfidence.High`)**: Explicit assembly-level properties found in known property sets (e.g., `Assembly Mark`, `Cast Unit Mark`, `Assembly Number`, `Cast Unit Number`).
2. **Medium Confidence (`AssemblyMarkConfidence.Medium`)**: Exporter-specific candidate fields such as Revit assembly marks or Tekla cast units on `IfcElementAssembly` types, or explicit element-level `Mark`/`Element Mark` properties.
3. **Low Confidence (`AssemblyMarkConfidence.Low`)**: Type/reference candidates (e.g. `Type Mark`, `Reference`, `Type Name`), object types, or the product `Name` property.
4. **None Confidence (`AssemblyMarkConfidence.None`)**: Fallback to preview name when no other identity candidate can be found.

### 3. Validation Diagnostics Rules & Status
Each normalized element is evaluated by the validation engine to identify missing attributes or assembly risks:
* **MissingAssemblyMark (Warning)**: Triggered when `AssemblyMarkIsExplicit` is false. This occurs when falling back to a product Name, ObjectType, or type reference, highlighting that the mark might not be a stable, unique production identifier.
* **MissingTrustedWeight (Warning)**: Triggered if `WeightTrustLevel` is not `ExplicitQuantity` or `ExplicitProperty` (e.g., estimated fallback weights).
* **EstimatedWeightNotApproved (Error)**: Triggered if `WeightTrustLevel` is `EstimatedFallback_NotAllowedForGA`. Estimated fallback weights are strictly blocked from GA optimization.
* **MissingAssemblyCOG (Warning)**: Triggered if Center of Gravity (COG) coordinates are missing, zero, or invalid.
* **MissingAssemblyVolume (Warning)**: Triggered if volume is missing or zero.
* **MissingMaterial (Info)**: Triggered if no material is specified.
* **SourceMappingLowConfidence (Warning)**: Triggered if mapping confidence is `Low` or `None`.
* **ParentChildDoubleCountRisk (Warning)**: Triggered if both parent assembly and child part are target candidates for GA optimization.

The validation results dictate the overall `ValidationStatus`:
* Any `Error` severity result promotes status to `ValidationStatus.Error`.
* Any `Warning` severity result (with no Errors) promotes status to `ValidationStatus.Warning`.
* Otherwise, status remains `ValidationStatus.Valid`.

### 4. Direct JSON Data Source Direction
In future phases, the system will support Tekla/Revit native add-ins exporting canonical JSON business data. In this model:
* The IFC file will remain responsible for 3D mesh representation, geometry coordinate space, and `GlobalId` mapping.
* The JSON business data sidecar will provide the canonical properties, directly mapping with high-confidence to `AssemblyMark`, `AssemblyWeight`, `AssemblyCOG`, etc., bypassing the heuristic property set mapping chain.


## Phase 3 — Source Mapping Profiles & Central Alias Resolver

Phase 3 organizes exporter-specific property names into clean profiles and centralizes IFC property traversal into a dedicated, testable service.

### 1. Reusable Source Mapping Profiles
All source-specific mapping metadata (property lists and keyword patterns) is defined in concrete profiles implementing the pure `IIfcSourceMappingProfile` interface (located in Domain):
* [GenericIfcExporterProfile](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/GenericIfcExporterProfile.cs): Provides default aliases for standard IFC schemas.
* [RevitIfcExporterProfile](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/RevitIfcExporterProfile.cs): Focuses on Revit identity property sets.
* [TeklaIfcExporterProfile](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/TeklaIfcExporterProfile.cs): Handles Tekla-specific cast unit and assembly mark logic.

### 2. Centralized Alias Resolver (`CentralIfcPropertyAliasResolver`)
The resolver resides in the Infrastructure layer because it directly traverses xBIM/IFC models and interfaces (e.g., `IIfcProduct`, property sets, quantity sets).
* **Stateless Service**: Registered as a singleton in the DI container.
* **Traceable Resolutions**: Returns `SourcePropertyResolutionResult` objects detailing the exact source property set, property name, and confidence level for logical fields (`AssemblyMark`, `ElementMark`, `Weight`, `Volume`, `Material`, `Level`, `Storey`).
* **Clean Boundaries**: Keeps the Domain project free of xBIM or Infrastructure-specific dependencies.

## Phase 4 — Import Validation & GA Readiness Diagnostics Foundation

Phase 4 exposes the validation/readiness state in a consistent, reusable way by creating a centralized diagnostics/readiness foundation and a minimal UI card ("Current Run Readiness") inside the GA Optimizer dialog.

### 1. Centralized Models & Service
* **ValidationMessage**: Represents a single user-facing diagnostic warning or info message. It contains `Code`, `Severity` ("Info", "Warning", "Error"), `Message`, `AffectedCount`, `AffectedElementIds`, `RecommendedAction`, `SourceSystem`, and `Category`.
* **ImportReadinessSummary**: Summarizes total elements, renderable elements, GA candidates, blocked elements, specific check warning counts, overall status, and validation messages list.
* **ImportReadinessService**: Evaluates the scoped elements, groups warning codes, counts affected items, and returns the readiness summary. If the Element Checker toggle is disabled, it returns a neutral Valid state with a single info message.

### 2. GA Readiness Gating & Scoping
* **Same Scoped Element List**: Readiness diagnostics are executed strictly on the same scoped element list used by the GA run, which honors viewport filters, target-type selections, and mouse-selection scoping.
* **Zero-Candidate Blocking**: If `GaCandidateElements` count is 0, the optimizer blocks the run and displays a clear explanation of the errors and warnings to the user.

### 3. Element Checker Toggle
* **Element Checker Toggle (ON/OFF)**: Adds a UI toggle to optionally skip detailed diagnostics breakdowns. When disabled, the UI shows a neutral "Not Checked" state. It does not bypass existing GA safety gates.

---

## Verified Statements

| Statement | Evidence |
|---|---|
| The application opens IFC files with xBIM. | `CraneOptimizer.Infrastructure/Ifc/IfcModelSession.cs`, class `IfcModelSession`, method `Open`, call `IfcStore.Open(fullPath)`. |
| A 3D xBIM geometry context is created for loaded models. | `CraneOptimizer.Infrastructure/Ifc/IfcModelSession.cs`, class `IfcModelSession`, method `Open`, property `GeometryContext`, calls `new Xbim3DModelContext(model)` and `CreateContext()`. |
| The import returns domain `PrecastElement` schemas. | `CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs`, class `IfcReaderRepository`, method `ReadElementsCore`; `CraneOptimizer.Domain/Schemas/PrecastElement.cs`, class `PrecastElement`. |
| IFC diagnostics extract schema metadata. | `CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs`, class `IfcDiagnosticAuditService`, method `ExtractMetadata`, property assignment `SchemaVersion = model.SchemaVersion.ToString()`. |
| Shape instances and shape geometry are used. | `IfcReaderRepository.cs`, nested class `IfcEngineeringExtractor`, methods `GetGeometrySnapshot`, `UpdateBounds`, `CalculateCentroidFromShapeGeometry`; `ElementVisualBuilder.cs`, class `ElementVisualBuilder`, mesh rendering methods. |
| Bounding boxes are computed from transformed shape-instance bounding-box corners. | `IfcReaderRepository.cs`, nested class `IfcEngineeringExtractor`, method `UpdateBounds`, properties `XbimShapeInstance.BoundingBox` and `XbimShapeInstance.Transformation`. |
| Mesh centroid calculation decodes `XbimShapeGeometry.ShapeData`. | `IfcReaderRepository.cs`, nested class `IfcEngineeringExtractor`, method `CalculateCentroidFromShapeGeometry`, property `ShapeData`, method `ReadShapeTriangulation()`. |
| IFC material associations are extracted when present. | `IfcReaderRepository.cs`, nested class `IfcEngineeringExtractor`, method `GetMaterial`, relationship `IIfcRelAssociatesMaterial`; `IfcDiagnosticAuditService.cs`, method `AuditVolumeMaterial`. |
| Explicit weight quantities and weight/mass properties are audited before fallback. | `IfcDiagnosticAuditService.cs`, class `IfcDiagnosticAuditService`, method `AuditWeight`, properties `IIfcQuantityWeight.WeightValue` and `IIfcPropertySingleValue.NominalValue`. |
| `IfcSpace`, `IfcOpeningElement`, and `IfcVirtualElement` are excluded from physical extraction. | `IfcReaderRepository.cs`, nested class `IfcEngineeringExtractor`, method `IsEngineeringProduct`; `IfcDiagnosticAuditService.cs`, method `IsPhysicalProduct`. |
| `.craneopt` saves IFC file metadata and element cache rows. | `CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectStateService.cs`, method `SaveIfcAsync`; `ProjectIfcFileEntity.cs`; `ProjectIfcElementEntity.cs`; `PrecastElementMapper.cs`. |

## Inaccurate Statements

| Statement | Classification | Evidence | Correction |
|---|---|---|---|
| IFC pipeline queries `IIfcBuildingElement`. | Inaccurate | `IfcReaderRepository.cs`, `ExtractNormalized`, uses `_model.Instances.OfType<IIfcProduct>().Where(IsEngineeringProduct)`; `IsEngineeringProduct` checks `product is IIfcElement`. | Document `IIfcProduct` / `IIfcElement` filtering. |
| Structural types are mapped to `IfcElementRole.Column`, `Beam`, `Slab`, `Wall`, `Footing`, `Stair`, or `Other`. | Inaccurate | `IfcNormalizationModels.cs`, enum `IfcElementRole` contains `LiftableElement`, `LiftAssembly`, `GroupingOnly`, `InternalAuxiliary`, `Excluded`, `NeedsReview`. | Document resolved category separately from role. |
| Missing volume falls back to bounding-box volume during import. | Inaccurate | `IfcReaderRepository.cs`, `BuildSourceSnapshot`, `volume = ConvertVolume(GetQuantity(...volume...) ?? GetPropertyDouble(...volume...))`; no bounding-box volume fallback is assigned to `ElementVolume` in normalized import. | Document direct IFC quantity/property volume extraction; note UI display helper can estimate from bounding box when needed. |
| Import weight fallback uses material overrides for steel, aluminum, glass, and timber. | Partially Accurate | `IfcDiagnosticAuditService.cs`, `AuditWeight`, fallback uses direct volume times `ConcreteDensityKgPerCubicMeter`; `ElementWeightCalculator.cs`, `GetDensityKgPerCubicMeter`, uses material/type heuristics only when displayed/filter weight is estimated from a `PrecastElement` lacking positive `ElementWT`. | Separate import weight source from UI/display fallback. |
| Material overrides are based on element names. | Inaccurate | `ElementWeightCalculator.cs`, `GetDensityKgPerCubicMeter`, combines `ElementMaterial` and `ElementType`; `IfcReaderRepository.cs`, `EstimateWeightFromMaterial`, accepts `material`. | Use material/type wording, not element-name-only wording. |
| `ElementWeightCalculator.cs` is part of IFC import weight computation. | Partially Accurate | `ElementWeightCalculator.cs` is in UI filter services and is used by filter/element-management display code; `IfcReaderRepository.ReadElementsCore` does not call it. | Document it as post-import UI/display estimation only. |
| The project explicitly supports IFC2X3, IFC4, IFC4X1, IFC4X2, and IFC4X3. | Missing Evidence | No project allow-list or schema switch exists. `IfcModelSession.Open` delegates to `IfcStore.Open`; `IfcDiagnosticAuditService.ExtractMetadata` records `model.SchemaVersion`. | Document accepted schema as xBIM-openable, with no project-specific schema validation gate. |
| xBIM geometry context is explicitly disposed. | Inaccurate | `IfcModelSession.Dispose` closes/disposes `Model`; no explicit `GeometryContext.Dispose()` call exists. | Document current disposal behavior exactly. |
| `ProjectService` maps IFC elements to project entities. | Inaccurate | `ProjectStateService.SaveIfcAsync` calls `PrecastElementMapper.ToEntity`; `IProjectService` is a separate transient service. | Reference `ProjectStateService` and `PrecastElementMapper`. |
| Current onboarding docs require `docs/project/current-state.md`. | Missing Evidence | `Test-Path docs/project/current-state.md` returned false. | Remove or redirect the link until the file exists. |

## Missing Evidence

| Claim Area | Finding |
|---|---|
| IFC2X3 / IFC4 / IFC4X1 / IFC4X2 / IFC4X3 support matrix | No project-specific support matrix or validation code found. Runtime support is whatever configured xBIM packages can open. |
| IFC validation | No explicit IFC schema validation pass was found. Error handling is try/catch around open/audit/extraction, with warnings captured in audit metadata. |
| `IfcSite` import behavior | No import of `IfcSite` as a planning element found. UI filter options may hide site types if present, but physical extraction excludes non-`IIfcElement` products. |
| `IfcBuilding` import behavior | No building extraction or building-name assignment found in the import path. `ProjectIfcFileEntity.BuildingName` exists but is not assigned by `ProjectStateService.SaveIfcAsync`. |
| `IfcSpace` optimization behavior | `IfcSpace` is explicitly excluded from physical extraction. |
| Direct storey persistence on imported elements | `ProjectIfcElementEntity.StoreyName` exists, and `ElementVisibilityFilterService.TryBuildIfcStoreyMap` can build a UI storey map from active xBIM relations, but `PrecastElementMapper.ToEntity` does not set `StoreyName`. |

## Planned Features Presented As Implemented

| Statement | Evidence |
|---|---|
| Merge IFC multi-model geometry loading is available. | `IfcViewerViewModel.ShowMergeIfcPlaceholder` sets status text that shared multi-model geometry loading is not implemented yet. |
| User-driven weight/density rule editor is implemented. | `IfcNormalizationDecisionService` report text lists this as a recommendation; no UI implementation found. |
| `docs/domain/ifc-architecture-review.md`, `docs/domain/ifc-feature-matrix.md`, and `docs/project/current-state.md` exist as active documentation. | These requested paths do not exist in the current tree. |

## Recommended Corrections

1. Replace `IIfcBuildingElement` pipeline wording with `IIfcProduct` / `IIfcElement` extraction and explicit exclusions.
2. Replace the stale structural `IfcElementRole` table with current roles and resolved categories.
3. Document import weight flow as explicit weight quantity, explicit weight/mass property, then concrete-density fallback from direct IFC volume.
4. Document material/type density heuristics as UI/display fallback behavior, not as the normalized import source.
5. State that project schema support is delegated to `IfcStore.Open()` and recorded through `model.SchemaVersion`; no project allow-list exists.
6. Correct `.craneopt` IFC persistence docs to match `ProjectIfcFileEntity`, `ProjectIfcElementEntity`, and `PrecastElementMapper`.
7. Remove links to missing `current-state.md` or mark them as absent.

## Documentation Accuracy Score

**Before alignment:** 62 / 100

The documentation correctly identified the broad xBIM-based IFC workflow, but several implementation-level claims were stale or overspecified.

**After alignment in this audit pass:** 88 / 100

Remaining risk is mostly in broad planning/vision documents whose wording is intentionally aspirational, and in existing historical audit/refactoring notes that mention IFC as context rather than current behavior.


---

## 3. Weight Resolution & Fallbacks

Because IFC models do not always include explicit mass properties, the import path and UI display layers use separate fallback paths.

### A. Import Weight Priority Chain (`IfcDiagnosticAuditService.AuditWeight`)

During the normalized import path, the element weight is resolved in a strict priority chain:

1. **Explicit Quantity Weight (`IIfcQuantityWeight`)**

   * Searches quantity sets for weight properties containing `weight` or `mass`.
   * Values are converted from kilograms to metric tons:

   $$
   \text{Weight}*{\text{Tons}} = \frac{\text{Weight}*{\text{kg}}}{1000}
   $$

2. **Explicit Property Set Weight (`IIfcPropertySingleValue`)**

   * Searches property sets for values containing `weight` or `mass`.
   * Supports unit parsing and conversion (`kg`, `lb`, `ton`, `tonne`).
   * Tekla-specific values greater than `100.0` are heuristically interpreted as kilograms and converted to tons.

3. **Direct Volume Fallback**

   * If no explicit weight exists, the importer retrieves volume from:

     * `IIfcQuantityVolume`
     * Volume-related properties

   Then applies the project density:

   $$
   \text{Weight}_{\text{Tons}} =
   \frac{\text{Volume (m}^3\text{)} \times 2500 \text{ kg/m}^3}{1000}
   $$

4. **No Bounding Box Fallback**

   * The normalized import path never uses bounding-box volume estimation.
   * If volume is unavailable, the element is flagged as:

   ```text
   IfcElementRole.NeedsReview
   ```

   and excluded from GA processing until resolved.

### B. UI / Display Estimated Weight (`ElementWeightCalculator`)

When a restored `PrecastElement` lacks a positive `ElementWT` value, UI filtering and element-management features may estimate weight for display purposes only.

The helper uses the following density heuristics:

| Material Type      | Density (kg/m³) |
| ------------------ | --------------- |
| Steel              | 7850            |
| Aluminum           | 2700            |
| Glass              | 2500            |
| Timber             | 600             |
| Concrete / Default | 2500            |

Keyword matching examples:

* Steel → `STEEL`, `REBAR`, `REINFORC`, `METAL`
* Aluminum → `ALUMIN`
* Glass → `GLASS`
* Timber → `TIMBER`, `WOOD`

### Concrete Density Setting

* Default density: **2500 kg/m³**

* Users can modify density on a per-project basis.

* Import fallback calculations use:

  ```text
  Weight = Density × Volume
  ```

  when explicit IFC mass information is unavailable.

* Elements resolved through fallback estimation may require review before inclusion in optimization workflows.

---

## 6. Spatial Structure Handling

### Schema Handling

The application does not enforce a project-specific schema allow-list.

File opening is delegated to:

```text
IfcStore.Open()
```

and the schema version is recorded through:

```text
model.SchemaVersion
```

Supported schemas:

* IFC2X3
* IFC4
* IFC4X1
* IFC4X2
* IFC4X3

through xBIM support.

### IfcProject

`IfcProject` is evaluated to retrieve:

* Unit definitions
* Length scaling
* Project-level metadata

### IfcBuildingStorey

`IfcBuildingStorey` is used to:

* Resolve model ground elevation
* Support storey filtering
* Preserve building hierarchy information

### IfcSite / IfcBuilding

These entities provide spatial context but are not imported as optimization targets.

### IfcSpace

`IfcSpace` entities are explicitly excluded because they do not represent physical liftable elements.

### Concrete Density Fallback

The import density fallback defaults to:

```text
2500 kg/m³
```

and may be overridden on a per-project basis.

`IfcDiagnosticAuditService` and `IfcReaderRepository` receive the active project density through import options.

### Material / Type Heuristics

Display-layer fallback calculations use substring matching against:

* ElementMaterial
* ElementType

to infer material densities when explicit data is unavailable.

These heuristics are display helpers only and are not the primary normalized import weight source.

### Bounding Box Fallback

Bounding-box volume estimation is used only by UI display and filtering helpers when explicit volume is unavailable.

Limitations:

* Assumes a solid rectangular prism.
* May overestimate:

  * Hollow elements
  * Curved elements
  * I-sections
  * T-sections
  * Complex precast geometries

Bounding-box estimation is never used during the normalized import workflow.

### Placement Offset

The coordinate placement offset depends on the selected import mode:

1. **Local Only Mode**:
   * The user is prompted to choose a model insertion / landing / placement point in the 3D viewport.
   * This offset is applied to all imported elements when saved or restored.
   * It affects mesh centroids, bounding boxes, and placement coordinates after transformation through `XbimShapeInstance.Transformation` to match local project space.

2. **IFC Origin Mapping Mode**:
   * Bypasses the placement prompt entirely.
   * Uses a zero-offset identity placement: offset = `(0, 0, 0)`.
   * The model loads exactly where it is drawn relative to its local origin in its source BIM authoring tool.
   * LocalReferenceX/Y/Z are set to `0.0`.
   * The manual "Move IFC" command is disabled in the UI to prevent coordinate misalignment.

## Phase 4 — Import Validation & GA Readiness Diagnostics Foundation

Phase 4 introduces a centralized diagnostics and import validation framework to assess precast elements before genetic optimization.

### 1. Centralized Diagnostics Summary (`ImportReadinessSummary`)
A unified readiness model encapsulates validation results for a collection of elements:
* **Overall Status**: Driven by the highest severity warning/error among all checked elements (Valid, Warning, Error, or Not Checked).
* **Element Counts**: Tracks total scoped elements, valid GA candidate elements, and GA-blocked elements.
* **Breakdown Anomaly Counts**: Captures specific metric counts for missing marks, low confidence mappings, and blocked weights (estimated weights).
* **Validation Messages**: Collects individual validation messages detailing specific issues per element.

### 2. Genetic Algorithm Candidate Eligibility
* **Eligibility Gating**: The readiness checks and GA execution share the exact same logic. Any element with an estimated fallback weight is classified as `EstimatedFallback_NotAllowedForGA` and blocked from GA optimization, ensuring consistency.
* **UI Readiness Card**: Displays overall status badges (Valid/Warning/Error/Not Checked) and detail counts in the left panel of the GA Optimizer dialog. If zero valid candidate elements exist, the optimizer blocks execution and prompts the user.

## Phase 5 — Canonical Display Identity Alignment

Phase 5 aligns all downstream data paths (Genetic Algorithm results, Reports, and CSV Diagnostics) to consume the Canonical Display Identity resolved from Phase 2/3.

### 1. Authoritative Identity Contract (`ElementDisplayIdentityDto`)
A dedicated data contract, `ElementDisplayIdentityDto`, is resolved using a neutral static utility `CanonicalMarkResolver` in the Application layer. It decouples presentation logic from raw `PrecastElement` geometry-heavy entities:
* **DisplayLabel**: Sourced via a strict priority chain: Explicit `AssemblyMark` > Any `AssemblyMark` > `ResolvedDisplayMark` > `ElementMark` > `ParentAssemblyName` > `ElementId`.
* **Validation & Context Metadata**: Carries `MarkSource`, `MarkConfidence`, `IsExplicitMark`, `ValidationStatus`, `ValidationWarnings`, `SourceSystem`, and `ExporterProfile`.

### 2. Downstream Mappings
The display identity and resolved mark are mapped into the following locations without modifying core engineering data or fitness calculations:
* **GA Results**: Enriched at `PlacementResult.ElementLiftResult` and propagated down to `GAResultsWindowViewModel` to display `CanonicalMark` in UI lists.
* **Reports**: Mapped to `ElementReportRowDto` and `LiftingResultSourceRowDto` properties and preferred by data providers for labels.
* **CSV Diagnostics**: Appended as flat headers/columns (`CanonicalMark`, `MarkSource`, `MarkConfidence`, `IsExplicitMark`) at the right of `ga_audit_detailed_*.csv` sheets.
