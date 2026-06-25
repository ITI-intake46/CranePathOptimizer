# System Limitations & Assumptions

This document lists the assumptions, simplifications, and architectural limitations of the Mobile Crane Layout Optimization system, derived from the current C# source code.

---

## 1. Geometric & Structural Simplifications

### A. 2D Operating Radius reach Calculations
*   **Limitation:** The horizontal operating radius calculation (used to look up crane capacities in load charts) is strictly 2D.
*   **Code Reference:** Implemented in [LiftingCalculationEngine.cs Line 22](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs#L22):
    $$R = \sqrt{(X_{\text{slew}} - X_{\text{COG}})^2 + (Y_{\text{slew}} - Y_{\text{COG}})^2}$$
*   **Assumption:** The crane and target elements are assumed to lie on a flat plane for horizontal distance checks. The slope or terrain curvature between the crane base and target elements is ignored when querying radial capacities. Vertical Z limits are validated independently in hook height checks.

### B. Axis-Aligned Bounding Box Approximations
*   **Limitation:** Zone boundary checks and coordinate snapping approximations use Axis-Aligned Bounding Boxes (AABB).
*   **Code Reference:** Implemented in `SpatialMath.BoundingBox` and `GAInitializer.GenerateRandomPointInsideZone`.
*   **Assumption:** Sites and zones are projected onto axis-aligned coordinate structures. Highly diagonal or skewed site boundaries may result in high rejection rates during coordinate sampling passes since bounds checks do not account for polygon rotation.

---

## 2. Physics & Environmental Constraints

### C. Ignored Wind & Weather Effects
*   **Limitation:** The optimization engine does not calculate wind forces or dynamic load adjustments.
*   **Assumption:** Environmental conditions are assumed to be static and ideal. The crane is assumed to operate within safe wind velocity thresholds. Dynamic structural derating due to wind shear is not accounted for in load capacity checks.

### D. Static Rigging Assumptions
*   **Limitation:** Rigging configuration calculations use a fixed, user-defined vertical sling height and accessory weight, rather than dynamic, element-specific rigging setups.
*   **Code References:** `GASettings.WireLength` (default 2.0m) and `GASettings.AdditionalWt` (default 0.0 tons).
*   **Assumption:** All precast elements are assumed to be rigged using slings and hooks of identical vertical height and weight. Dynamic rigging variables (such as rigging angles, spreader beam dimensions, and component self-weight variation) are not simulated.

---

## 3. Data & Material Assumptions

### E. Material Density Fallbacks & GA Safety Gating
*   **Limitation:** When an IFC element lacks explicit weight or volume metadata, the extraction pipeline falls back to hardcoded density coefficients to estimate weight.
*   **Code Reference:** Implemented in [IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs).
*   **Assumption/Safety Gating:** Concrete fallback weight estimation assumes a density of exactly $2500 \text{ kg/m}^3$. Other heuristic fallbacks include:
    *   Steel: $7850 \text{ kg/m}^3$
    *   Aluminum: $2700 \text{ kg/m}^3$
    *   Glass: $2500 \text{ kg/m}^3$
    *   Timber: $600 \text{ kg/m}^3$
*   **Safety Rule:** Any elements resolved through fallback estimation are marked with `WeightTrustLevel = EstimatedFallback_NotAllowedForGA`. These elements are **strictly blocked** from entering GA layout optimization passes, regardless of their `IncludeInGa` property. There is no user override for this gate in Phase 1.

### E2. Parent-Assembly Child Exclusion (Double-Counting Prevention)
*   **Limitation:** Parent assemblies and child parts must not both be sent to GA optimization if the parent is the liftable unit.
*   **Code Reference:** Implemented in [CranePlacementService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Services/CranePlacementService.cs).
*   **Gating Rule:** If a parent assembly (`Role == LiftAssembly` or `LiftableElement`) is present in the optimization set with a trusted weight, its children components/parts are automatically excluded from the active GA target list to prevent weight/coordinate double-counting.

### E3. Canonical Identity Mapping Limitations
*   **Limitation:** Relying on IFC property sets for assembly marks can be unreliable because property names, configurations, and structures vary wildly between export tools (Tekla vs. Revit vs. standard IFC) and custom user templates.
*   **Upcoming Direct JSON Exports:** In future phases, the system will support Tekla/Revit native add-ins exporting canonical JSON business data. In this model, the IFC file will only be responsible for 3D mesh geometry and GlobalId mappings, while the JSON file provides the authoritative, high-confidence canonical business properties, bypassing the heuristic IFC property set mapping chain.

---

## 4. Optimization Engine Limits

### F. Single Crane Specification Limit
*   **Limitation:** The GA engine optimizes placement layouts for multiple setups of a **single crane specification** at a time.
*   **Code Reference:** Implemented in [PlacementInput.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/DTOs/PlacementInput.cs) where a single `CraneSpecification` is passed to the engine.
*   **Assumption:** The site is assumed to operate a homogeneous fleet of identical crane models. The solver cannot mix different crane specifications (e.g., placing one HammerHead tower crane and one LuffingJib crane in the same optimization pass).

### G. 2D Obstacle/Forbidden Zone Containment
*   **Limitation:** Forbidden zones and site obstacles are treated as 2D polygonal footprints on the ground.
*   **Code Reference:** Implemented in `ConstraintEngine.IsValidSetupPoint` using `SpatialMath.PointInPolygon`.
*   **Assumption:** Obstacles are assumed to project infinitely upwards. Overhead clearance constraints (such as slewing over a low-rise building or under power lines) are not supported.

---

## 5. GIS / Survey Coordinate Integration Limitations

### H. Readout-Only Presentation Mapping
*   **Limitation:** Survey and GIS coordinates are purely presentation-layer metadata and outputs. 
*   **Code Reference:** Mapped via `IProjectCoordinateTransformService.LocalToSurvey` only in the UI and logging layers.
*   **Assumption:** Stored geometry (precast elements, COGs, bounding boxes, crane setups, DXF zones) remains unchanged in local meters relative to the IFC model's local origin $(0, 0, 0)$. Adjusting georeferencing settings does not shift, scale, or transform physical meshes, nor does it affect genetic algorithm optimization, radius capacity lookups, boom vertical heights, or constraint containment calculations.

### I. Identity Placement and Skipped Viewport Interaction for IFC Origin Mapping
*   **Limitation:** Selecting `"IFC Origin Mapping"` uses a zero-offset identity transform $(0,0,0)$ and skips viewport interaction.
*   **Code Reference:** Implemented in `IfcViewerViewModel.LoadIfcAsync` and `CanMoveIfc`.
*   **Assumption:** The IFC model is placed exactly where it is drawn relative to its local origin in its source BIM authoring tool. The user is not prompted to select a placement/insertion point, nor can they manually move the model while this mapping is active. Local $(0,0,0)$ maps directly to the `IfcMapConversion` Easting, Northing, and Elevation coordinates.

### J. Informational-Only Geographic Coordinates (IFC2x3)
*   **Limitation:** Geographic curve coordinates (Latitude / Longitude decimal degrees) extracted from IFC2x3 `IfcSite` are informational only.
*   **Code Reference:** Extracted by `IfcGeoreferencingExtractor` but excluded from active Easting/Northing origin mappings.
*   **Assumption:** The application does not perform projection reprojections or CRS conversion (e.g. from GPS decimal degrees to planar projection Easting/Northing).plan coordinate grids are assumed to be grid-based projected coordinates.

### K. Excluded Manual Picked Reference Points
*   **Limitation:** Custom local anchor mapping (viewport vertex click mapping to a specific survey coordinate) is not supported.
*   **Assumption:** Manual picked coordinate reference mode is not implemented. Reference Mode ComboBox exposes only "Local Only" and "IFC Origin Mapping".

### L. Excluded Export Reports
*   **Limitation:** PDF, Excel, and standard UI reports do not output survey coordinates.
*   **Assumption:** Export integration remains out of scope for the current GIS epic phases.
