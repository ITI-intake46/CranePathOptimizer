# Domain: IFC Processing Workflow

The IFC Processing Workflow extracts structural geometry from BIM models to define optimization targets.

---

## 1. Process Flow

```mermaid
graph LR
    BIM[IFC File] --> Open[xBIM IfcStore.Open]
    Open --> Context[Create 3D Geometry Context]
    Context --> Filter[Query IIfcBuildingElement]
    Filter --> Parse[Extract coordinates, bbox & volume]
    Parse --> Weight[Calculate Weight via density]
    Weight --> DTO[Output PrecastElement Schemas]
```

The concrete implementation is found in [IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs) and [IfcModelSessionService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcModelSessionService.cs).

### Implementation Reference

File:
[IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)

Class:
`IfcReaderRepository`

Method:
`ReadElements()`

---

## 2. Element Classification

The system parses IFC entities and maps them to a domain role `IfcElementRole`:

| IFC Entity Class | Enum Value | Role |
|---|---|---|
| `IIfcColumn` | `IfcElementRole.Column` | Vertical load-bearing support |
| `IIfcBeam` | `IfcElementRole.Beam` | Horizontal spanning support |
| `IIfcSlab` | `IfcElementRole.Slab` | Floor and roof plates |
| `IIfcWall` | `IfcElementRole.Wall` | Vertical boundaries |
| `IIfcFooting` | `IfcElementRole.Footing` | Structural foundations |
| `IIfcStairFlight` | `IfcElementRole.Stair` | Vertical circulation steps |

Any building elements that do not match these interfaces are processed as `IfcElementRole.Other` or ignored depending on the normalization policies.

### Implementation Reference

File:
[IfcNormalizationModels.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Diagnostics/IfcNormalizationModels.cs)

Enum:
`IfcElementRole`

---

## 3. Weight Estimation Calculations

Because IFC models do not always include explicit mass properties, the system calculates weights programmatically:

1. **Volume Extraction (`GetVolume`):**
   * First, it searches the element properties for explicit physical volume declarations (`IIfcQuantityVolume`).
   * If a declared quantity is found, it uses `VolumeValue`.
   * If no explicit volume quantity exists, the system falls back to calculating the volume from the bounding box dimensions:
     $$\text{Volume} = \text{Length} \times \text{Width} \times \text{Height}$$

2. **Weight Computation:**
   Weight is calculated assuming dynamic densities based on element naming patterns or standard concrete density fallback:
   * **Steel:** $7850 \text{ kg/m}^3$ (triggered by "STEEL", "REBAR", or "METAL" in the element name)
   * **Aluminum:** $2700 \text{ kg/m}^3$ (triggered by "ALUMIN" in the element name)
   * **Glass:** $2500 \text{ kg/m}^3$ (triggered by "GLASS" in the element name)
   * **Timber:** $600 \text{ kg/m}^3$ (triggered by "TIMBER" or "WOOD" in the element name)
   * **Concrete / Default Fallback:** $2500 \text{ kg/m}^3$
   
   $$\text{Weight (kg)} = \text{Volume (m}^3\text{)} \times \text{Density (kg/m}^3\text{)}$$
   $$\text{Weight (tonnes)} = \frac{\text{Weight (kg)}}{1000}$$

### Implementation Reference

File:
[IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)

Class:
`IfcReaderRepository`

Method:
`EstimateWeightFromMaterialName()` and `ExtractVolume()`

---

## 4. Engineering Assumptions and Limitations

*   **Concrete Density Fallback:** The default density factor of $2500 \text{ kg/m}^3$ is utilized inside `IfcReaderRepository.cs`, `IfcDiagnosticAuditService.cs`, and `ElementWeightCalculator.cs`.
*   **Material Name Heuristics:** The system uses substring matching on element and material names to override concrete density with steel, wood, glass, or aluminum. If naming conventions differ in an exported IFC model, it will revert to the default concrete density.
*   **Bounding Box Fallback:** Bounding box volume calculation assumes solid rectangular prisms. This will overestimate the volume and weight of hollow-core slabs, curved elements, and structural beams with I or T-shaped profiles.
*   **Coordination Origin:** The coordinates of elements are extracted from local IFC placement structures. Site boundaries must share the same coordinate system unless a transformation is configured.
