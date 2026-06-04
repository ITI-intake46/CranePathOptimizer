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

### E. Material Density Fallbacks
*   **Limitation:** When an IFC element lacks explicit weight or volume metadata, the extraction pipeline falls back to hardcoded density coefficients to estimate weight.
*   **Code Reference:** Implemented in [IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs).
*   **Assumption:** Concrete fallback weight estimation assumes a density of exactly $2500 \text{ kg/m}^3$. Other heuristic fallbacks include:
    *   Steel: $7850 \text{ kg/m}^3$
    *   Aluminum: $2700 \text{ kg/m}^3$
    *   Glass: $2500 \text{ kg/m}^3$
    *   Timber: $600 \text{ kg/m}^3$
*   Elements with untrusted weights are marked as `EstimatedFallback_NotAllowedForGA` and excluded from the optimization target set.

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
