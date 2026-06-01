# Domain: Lifting Engine

The **Lifting Engine** is responsible for verifying structural hoist constraints, operating radii, and required lifting capacities for precast building elements.

---

## 1. Operating Metrics & Calculations

The system computes operating metrics for every element using settings-defined rigging parameters and crane mechanical offsets:

### Operating Radius ($R$)
Calculates the horizontal distance between the crane center of rotation (accounting for structural slew offsets) and the center of gravity (COG) of the building element:

$$R = \sqrt{(X_{\text{slew}} - X_{\text{element}})^2 + (Y_{\text{slew}} - Y_{\text{element}})^2}$$

Where:
*   $X_{\text{slew}} = X_{\text{crane}} + \text{settings.SlewOffsetX}$
*   $Y_{\text{slew}} = Y_{\text{crane}} + \text{settings.SlewOffsetY}$

### Hoist Lift Height ($H$)
Calculates the vertical clearance required, accounting for the element's height and the length of the rigging slings:

$$H = \max\left(0, Z_{\text{element}} + \frac{\text{ElementHeight}}{2} - Z_{\text{crane}}\right) + \text{settings.WireLength}$$

### Required Lifting Weight ($W_{\text{req}}$)
Calculates the total load on the hook, accounting for both the structural element weight and the weight of the rigging hook block/slings:

$$W_{\text{req}} = \text{ElementWT} + \text{settings.AdditionalWt}$$

### Implementation Reference

File:
[LiftingCalculationEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs)

Class:
`LiftingCalculationEngine`

Method:
`CalculateRadius()`, `CalculateLiftHeight()`, and `CalculateRequiredWeight()`

---

## 2. Planned Features (Future Design Roadmap)

While the current engine verifies liftability using static radius, height, and capacity checks, subsequent updates are planned to introduce dynamic spatial constraints:

### Sequence-Based Obstacle Modeling
*   As precast elements are erected (in the order defined by `SortOrder`), they will be dynamically added as active 3D collision obstacles for subsequent lifts.

### Boom & Jib 3D Collision Clearance
*   Calculates vector intersections of the physical crane boom and jib structures against already installed elements during slewing or trolley movements.

### hoist Rope Trajectory Traversal
*   Traces hoist ropes to ensure the rigging cables do not scrape or catch against structural components during lifts.
