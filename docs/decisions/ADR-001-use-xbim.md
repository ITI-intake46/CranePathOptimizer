# ADR-001: Use xBIM for IFC Building Model Parsing

## Status
Accepted

---

## Context
The application needs to parse Industry Foundation Classes (IFC) building models to extract precast concrete elements (columns, beams, slabs, etc.) along with their spatial dimensions, coordinates, volumes, and structural placements. Since developing a custom IFC parser is extremely complex due to the size and density of the IFC schema, we need to select an existing open-source C# BIM parsing framework.

---

## Decision
We chose the **xBIM (eXtensible Building Information Modelling)** toolkit, specifically:
* `xBim.Essentials` and `Xbim.Ifc` (to open IFC files and read metadata, properties, quantities, and schema models).
* `xBim.Geometry`, `xBim.Geometry.Engine.Interop`, and `Xbim.ModelGeometry.Scene` (to create geometry contexts, read shape instances, compute transformed bounds, and access shape geometry).

---

## Consequences

### Positive
* **Schema Delegation:** The project delegates schema opening to `IfcStore.Open()` and records the resulting `model.SchemaVersion`. There is no project-specific IFC schema allow-list or validation matrix.
* **Geometry Resolution:** The interop geometry engine allows us to query coordinates and bounding box dimensions from complex shapes.
* **Performance:** Extracted geometry coordinates are resolved quickly.

### Negative
* **Native Dependency:** `xBim.Geometry.Engine.Interop` depends on native C++ compiler libraries. This restricts runtime execution platforms to Windows (x86/x64).
* **Memory Management:** IFC sessions load heavy `IfcStore` and `Xbim3DModelContext` objects. The current `IfcModelSession.Dispose()` implementation closes and disposes the `IfcStore`; the geometry context is owned by the session but is not explicitly disposed in current code.
