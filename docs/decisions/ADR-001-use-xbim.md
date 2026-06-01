# ADR-001: Use xBIM for IFC Building Model Parsing

## Status
Accepted

---

## Context
The application needs to parse Industry Foundation Classes (IFC) building models to extract precast concrete elements (columns, beams, slabs, etc.) along with their spatial dimensions, coordinates, volumes, and structural placements. Since developing a custom IFC parser is extremely complex due to the size and density of the IFC schema, we need to select an existing open-source C# BIM parsing framework.

---

## Decision
We chose the **xBIM (eXtensible Building Information Modelling)** toolkit, specifically:
* `xBim.Essentials` (to read metadata, properties, and IFC schema models).
* `xBim.Geometry` & `xBim.Geometry.Engine.Interop` (to compute element transformations, world coordinates, and shape bounding boxes).

---

## Consequences

### Positive
* **Comprehensive Standard Support:** xBIM natively supports IFC2x3 and IFC4 schemas.
* **Geometry Resolution:** The interop geometry engine allows us to query coordinates and bounding box dimensions from complex shapes.
* **Performance:** Extracted geometry coordinates are resolved quickly.

### Negative
* **Native Dependency:** `xBim.Geometry.Engine.Interop` depends on native C++ compiler libraries. This restricts runtime execution platforms to Windows (x86/x64).
* **Memory Management:** The geometric engine loads heavy contexts that must be explicitly disposed (`Xbim3DModelContext` and `IfcStore` objects) to prevent memory leaks in WPF.
