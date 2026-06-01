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
    Weight --> DTO[Output IfcElementData DTOs]
```

The concrete implementation is found in [IfcModelService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcModelService.cs).

---

## 2. Element Classification

The system parses IFC entities and maps them to a domain enum `IfcElementType`:

| IFC Entity Class | Enum Value | Role |
|---|---|---|
| `IIfcColumn` | `IfcElementType.Column` | Vertical load-bearing support |
| `IIfcBeam` | `IfcElementType.Beam` | Horizontal spanning support |
| `IIfcSlab` | `IfcElementType.Slab` | Floor and roof plates |
| `IIfcWall` | `IfcElementType.Wall` | Vertical boundaries |
| `IIfcFooting` | `IfcElementType.Footing` | Structural foundations |
| `IIfcStairFlight` | `IfcElementType.Stair` | Vertical circulation steps |

Any building elements that do not match these interfaces are ignored.

---

## 3. Weight Estimation Calculations

Because IFC models do not always include explicit mass properties, the system calculates weights programmatically:

1. **Volume Extraction (`GetVolume`):**
   * First, it searches the element properties for explicit physical volume declarations (`IIfcQuantityVolume`).
   * If a declared quantity is found, it uses `VolumeValue`.
   * If no explicit volume quantity exists, the system falls back to calculating the volume from the bounding box dimensions:
     $$\text{Volume} = \text{Length} \times \text{Width} \times \text{Height}$$

2. **Weight Computation:**
   Weight is calculated assuming standard precast reinforced concrete density:
   $$\text{Weight (kg)} = \text{Volume (m}^3\text{)} \times 2400 \text{ kg/m}^3$$
   $$\text{Weight (tonnes)} = \frac{\text{Weight (kg)}}{1000}$$

---

## 4. Engineering Assumptions and Limitations

* **Hardcoded Concrete Density:** The density factor of $2400 \text{ kg/m}^3$ is hardcoded inside `IfcModelService.cs`. This does not account for lightweight concrete or steel components.
* **Bounding Box Fallback:** Bounding box volume calculation assumes solid rectangular prisms. This will overestimate the volume and weight of hollow-core slabs, curved elements, and structural beams with I or T-shaped profiles.
* **Coordination Origin:** The coordinates of elements are extracted from local IFC placement structures. Site boundaries must share the same coordinate system.
