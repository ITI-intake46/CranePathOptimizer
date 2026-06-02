# Domain: IFC Processing Workflow

The IFC Processing Workflow extracts structural geometry from BIM models to define optimization targets.

---

## 1. Process Flow

```mermaid
graph LR
    BIM[IFC File] --> Open[xBIM IfcStore.Open]
    Open --> Context[Create 3D Geometry Context]
    Context --> Audit[Run diagnostic audit]
    Audit --> Normalize[Build normalization preview]
    Normalize --> Filter[Query IIfcProduct / IIfcElement]
    Filter --> Parse[Extract identity, geometry, quantities & material]
    Parse --> Weight[Resolve explicit or fallback weight]
    Weight --> DTO[Output PrecastElement Schemas]
```

The concrete implementation is found in [IfcModelSession.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcModelSession.cs), [IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs), [IfcDiagnosticAuditService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs), and [IfcNormalizationDecisionService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcNormalizationDecisionService.cs).

### Implementation Reference

File:
[IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)

Class:
`IfcReaderRepository`

Method:
`ReadElementsCore()` and nested `IfcEngineeringExtractor.ExtractNormalized()`

---

## 2. Element Classification

The system parses physical IFC products and maps them to two different concepts:

| Concept | Implementation | Purpose |
|---|---|---|
| Resolved category | `IfcDiagnosticAuditService.ResolveCandidateCategory()` | Produces UI/filter categories such as `Column`, `Beam`, `Slab`, `Wall`, `Roof`, `Stair`, `Ramp`, `Footing`, `Pile`, `Plate`, `Member`, `Assembly`, `Proxy`, or `Unknown`. |
| Normalization role | `IfcElementRole` | Controls behavior: `LiftableElement`, `LiftAssembly`, `GroupingOnly`, `InternalAuxiliary`, `Excluded`, or `NeedsReview`. |

The extraction candidate set is `_model.Instances.OfType<IIfcProduct>().Where(IsEngineeringProduct)`. `IsEngineeringProduct()` requires an `IIfcElement` with `GlobalId` and `Name`, and excludes `IIfcOpeningElement`, `IIfcSpace`, and `IIfcVirtualElement`.

### Implementation Reference

File:
[IfcNormalizationModels.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Diagnostics/IfcNormalizationModels.cs)

Enum:
`IfcElementRole`

File:
[IfcDiagnosticAuditService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs)

Method:
`ResolveCandidateCategory()`

---

## 3. Weight Estimation Calculations

Because IFC models do not always include explicit mass properties, the import and UI display layers use separate fallback paths.

1. **Import weight resolution (`IfcDiagnosticAuditService.AuditWeight`):**
   * First, it searches `IIfcElementQuantity` sets for `IIfcQuantityWeight` names containing `weight` or `mass`.
   * If no quantity weight exists, it searches `IIfcPropertySet` values with names containing `weight` or `mass`.
   * If no explicit weight is found, it looks for direct volume via `IIfcQuantityVolume` or numeric volume properties and applies the concrete fallback density:
     $$\text{Weight (tonnes)} = \frac{\text{Volume (m}^3\text{)} \times 2500}{1000}$$

2. **Volume extraction (`IfcEngineeringExtractor.BuildSourceSnapshot`):**
   * The importer reads `netvolume` / `volume` from quantities or numeric properties.
   * The normalized import path does not assign a bounding-box volume fallback to `ElementVolume`.

3. **UI/display estimated weight (`ElementWeightCalculator`):**
   * When a `PrecastElement` lacks a positive `ElementWT`, the UI filter and element-management layer can estimate weight from `ElementVolume` or bounding-box dimensions.
   * That display fallback uses `ElementMaterial` and `ElementType` heuristics:
     * **Steel:** $7850 \text{ kg/m}^3$ (`STEEL`, `REBAR`, `REINFORC`)
     * **Aluminum:** $2700 \text{ kg/m}^3$ (`ALUMIN`)
     * **Glass:** $2500 \text{ kg/m}^3$ (`GLASS`)
     * **Timber:** $600 \text{ kg/m}^3$ (`TIMBER`, `WOOD`)
     * **Concrete / Default Fallback:** $2500 \text{ kg/m}^3$

### Implementation Reference

File:
[IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)

Class:
`IfcReaderRepository`

Method:
`ReadElementsCore()` and nested `IfcEngineeringExtractor.BuildSourceSnapshot()`

File:
[IfcDiagnosticAuditService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs)

Method:
`AuditWeight()` and `AuditVolumeMaterial()`

File:
[ElementWeightCalculator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F16_Filter/Services/ElementWeightCalculator.cs)

Method:
`GetWeightTons()` and `GetDensityKgPerCubicMeter()`

---

## 4. Engineering Assumptions and Limitations

*   **Schema Handling:** There is no project-specific allow-list for IFC2X3, IFC4, IFC4X1, IFC4X2, or IFC4X3. The project delegates file opening to `IfcStore.Open()` and records `model.SchemaVersion` in audit metadata.
*   **Concrete Density Fallback:** The concrete fallback density is $2500 \text{ kg/m}^3$ in `IfcDiagnosticAuditService`, `IfcReaderRepository`, `ElementWeightCalculator`, and `IfcLoadingService`.
*   **Material / Type Heuristics:** The active display/filter fallback in `ElementWeightCalculator` uses substring matching on `ElementMaterial` and `ElementType`. It is not the primary normalized import weight source.
*   **Bounding Box Fallback:** Bounding-box volume is used by the UI display/filter fallback only when explicit element volume is missing. It assumes a solid rectangular prism and can overestimate hollow, curved, I-shaped, or T-shaped elements.
*   **Spatial Structure:** `IfcProject` is read for units, and `IfcBuildingStorey` is read for model ground elevation and filter storey labels. `IfcSite`, `IfcBuilding`, and `IfcSpace` are not imported as optimization elements; `IfcSpace` is explicitly excluded.
*   **Coordinate Origin:** Shape-instance bounding boxes and mesh centroids are transformed using `XbimShapeInstance.Transformation`. The UI then applies a user-selected placement offset when the IFC model is placed or restored.
