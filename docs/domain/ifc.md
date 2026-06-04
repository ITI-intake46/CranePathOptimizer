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

The user-selected placement offset is applied when model coordinates are saved or restored.

The offset affects:

* Mesh centroids
* Bounding boxes
* Placement coordinates

after transformation through:

```text
XbimShapeInstance.Transformation
```

This ensures consistent positioning between IFC coordinates and optimizer project coordinates.
