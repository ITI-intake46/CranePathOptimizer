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

