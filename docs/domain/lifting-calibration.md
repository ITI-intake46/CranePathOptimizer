# Lifting & Configuration Calibration Guide

This document details the configuration parameters, boom angle limits, outrigger configuration constraints, and safety deratings used to calibrate crane models and resolve capacities conservatively.

---

## 1. Boom Angle Limits

Mobile cranes have physical boom angle operating limits dictated by stability and structure.
- **Luffing & Main Boom Angles:** Crane models (such as Liebherr LTM series) generally operate between **15° and 82°**.
- **Boom Angle Checks:** During GA evaluation, the [CraneConfigurationEngine](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/CraneConfiguration/CraneConfigurationEngine.cs) calculates the required boom angle to cover the lift height and operating radius. If the required angle is outside the crane specification's allowed limits, the lift status is classified as `HeightFail` or `OutOfRadius`.

---

## 2. Outrigger Configuration Constraints

The support base of a crane is safety-critical.
- **Outrigger Base size:** Load charts are mapped to specific outrigger widths (e.g., 7.1m x 6.3m).
- **Outrigger Footprint Check:** The [ConstraintEngine](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs) ensures the full outrigger rectangle is contained within the site's allowed zones and does not overlap forbidden zones or obstacles.

---

## 3. Capacity Resolution Algorithm (ConservativeStep)

Rather than using linear interpolations (which could overestimate capacity between discrete manufacturer-defined steps), the system implements a **ConservativeStep** lookup algorithm in [LoadChartCapacityResolver.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs):

1. **Radius Rounding:** Rounds up the operating radius requirements to the nearest larger radius entry in the manufacturer's load chart.
2. **Configuration Filtering:** Gathers all boom configurations at the selected radius that meet or exceed the required boom length.
3. **Duplicate Column Grouping:** Resolves duplicate boom length entries by using the minimum positive capacity in the duplicate group.
4. **Safety Scaling:** Multiplies the listed capacity by `DeratingRatio` and `SafetyFactor` configured in [GASettings.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs):
   $$\text{availableCapacity} = \text{Capacity} \times \text{DeratingRatio} \times \text{SafetyFactor}$$
5. **Enforcement:** Selects the shortest boom configuration where `availableCapacity >= elementWeight`.
