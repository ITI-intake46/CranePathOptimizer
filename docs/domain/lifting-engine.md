# Domain: Lifting Engine

The **Lifting Engine** is responsible for verifying structural lifting envelopes, operating radii, and required lifting capacities for precast building elements at any given crane setup location.

---

## 1. Operating Metrics & Physics-Based Formulas

The calculations are implemented in [LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs) and [CraneConfigurationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs).

### A. Operating Radius ($R$)
Calculates the horizontal distance between the crane center of rotation (slew center) and the element center of gravity (COG). 

> [!WARNING]
> **2D Reach Restriction:** Horizontal operating radius calculations are strictly 2D. The vertical coordinate $Z$ is ignored when calculating the reach distance for load chart validations.

$$R = \sqrt{(X_{\text{slew}} - X_{\text{COG}})^2 + (Y_{\text{slew}} - Y_{\text{COG}})^2}$$

Where:
*   $X_{\text{slew}} = X_{\text{crane}} + \text{settings.SlewOffsetX}$
*   $Y_{\text{slew}} = Y_{\text{crane}} + \text{settings.SlewOffsetY}$
*   $X_{\text{COG}}, Y_{\text{COG}}$ represent the element center of gravity.

Implemented in:
*   [LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs#L29-L43) (`CalculateRadius` methods)

---

### B. Hoist Lift Height ($H$)
Calculates the vertical clearance required from the crane base elevation to the highest point of the suspended load (hook point), accounting for the rigging wire/sling length:

$$H = \max\left(0, Z_{\text{COG}} + H_{\text{half}} - Z_{\text{crane}}\right) + \text{settings.WireLength}$$

Where:
*   $Z_{\text{COG}}$ is the elevation of the element's COG in project meters.
*   $H_{\text{half}} = \frac{\text{ElementHeight}}{2}$ is the half-height of the element.
*   $Z_{\text{crane}}$ is the crane setup elevation.
*   `settings.WireLength` is the length of the wire rope/rigging gear (default 2.0m).

Implemented in:
*   [LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs#L45-L54) (`CalculateLiftHeight` method)

---

### C. Required Lifting Weight ($W_{\text{req}}$)
Calculates the total weight suspended on the hook, accounting for both the element weight and auxiliary rigging additions (slings, shackles, hook block):

$$W_{\text{req}} = \text{ElementWT} + \text{settings.AdditionalWt}$$

Where:
*   `ElementWT` is the precast element's weight in metric tons.
*   `settings.AdditionalWt` is the rigging weight adjustment (default 0.0 tons).

Implemented in:
*   [LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs#L56-L59) (`CalculateRequiredWeight` method)

---

### D. Required Boom Length ($B$)
Calculates the minimum boom/jib length required to reach the suspended element hook point. This accounts for structural boom hinge offsets:

$$B = \sqrt{(R_{\text{hinge}})^2 + (H_{\text{hinge}})^2}$$

Where:
*   $R_{\text{hinge}} = \sqrt{(X_{\text{hinge}} - X_{\text{COG}})^2 + (Y_{\text{hinge}} - Y_{\text{COG}})^2}$ is the horizontal distance from the boom foot hinge to the element COG.
*   $X_{\text{hinge}} = X_{\text{crane}} + \text{settings.BoomHingeOffsetX}$
*   $Y_{\text{hinge}} = Y_{\text{crane}} + \text{settings.BoomHingeOffsetY}$
*   $H_{\text{hinge}} = \max\left(0, Z_{\text{COG}} + H_{\text{half}} + \text{settings.WireLength} - \left(Z_{\text{crane}} + \text{settings.BoomHingeOffsetZ}\right)\right)$ is the non-negative vertical distance from the boom foot hinge to the hook point.

Implemented in:
*   [CraneConfigurationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs#L42-L62) (`CalculateRequiredBoomLength` methods)

---

## 2. Load Chart Resolver Integration

The lifting engine does not calculate crane capacities in isolation. It coordinates with the load chart solver component:
*   **Resolution Method:** [LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs#L61-L70) (`DetermineLiftStatus`) delegates capacity checks to [LoadChartCapacityResolver.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs#L14-L62) (implementing [ILoadChartCapacityResolver.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/ILoadChartCapacityResolver.cs)).
*   **Capacity Resolution Workflow:** The solver receives the computed operating radius $R$, required boom length $B$, required capacity $W_{\text{req}}$, and the crane specification. It queries the `ConservativeStep` load chart using a pre-sorted indexing mechanism to find the best configuration that meets or exceeds $R$ and $B$, scales the capacity, and determines the final `LiftStatus`.

---

## 3. Planned Features (Future Design Roadmap)

Subsequent updates are planned to introduce 3D dynamic clearance and sequence tracking:

### Sequence-Based Obstacle Modeling [Planned Feature]
As elements are erected, they will be dynamically added as active 3D collision obstacles for subsequent lifts based on erection schedules.

### Boom & Jib 3D Collision Clearance [Planned Feature]
Vector-based intersection checks of the physical crane boom and jib structures against already installed elements.

### Hoist Rope Trajectory Traversal [Planned Feature]
Tracing cables to ensure rigging ropes do not catch or scrape against adjacent structural components.

