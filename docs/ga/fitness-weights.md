# Genetic Algorithm Fitness Weights Tuning Guide

The optimization engine evaluates candidate crane placement layouts using a weighted penalty deduction formula. This document catalogs every fitness weight, explains its purpose, and describes the system sensitivity impact of tuning it.

---

## 1. Summary of Fitness Weights

The weights are defined in [GASettings.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs) and applied in [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs).

| Weight Property | Default Value | Category | Target Objective |
| :--- | :--- | :--- | :--- |
| `CoverageWeight` | `10.0` | Primary Signal | Maximize number of liftable elements. |
| `OverweightPenaltyWeight` | `10.0` | Safety / Constraint | Avoid assigning elements that exceed load chart capacities. |
| `RelocationWeight` | `5.0` | Operational Cost | Minimize the number of separate crane setups (relocations). |
| `HeightPenaltyWeight` | `5.0` | Safety / Constraint | Avoid setups that violate vertical hook clearance limits. |
| `RadiusPenaltyWeight` | `3.0` | Safety / Constraint | Avoid placing elements out of horizontal crane radius limits. |
| `BalancePenaltyWeight` | `2.0` | Logistics / Sequence | Distribute lifts evenly among active crane setups. |
| `RadiusStdDevPenaltyWeight` | `2.0` | Mechanical Safety | Maintain consistent lifting radii across setups. |
| `SimilarPositionPenaltyWeight` | `8.0` | Layout Constraint | Prevent setups from being placed too close together. |
| `RelocationDistancePenaltyWeight`| `1.5` | Logistics / Time | Minimize the travel distance of crane relocations. |
| `RadiusQualityPenaltyWeight` | `1.0` | Mechanical Safety | Encourage using shorter operating radii. |
| `SpecialCasePenaltyWeight` | `1.0` | System Safety | Penalize unchartable configuration requests (hardcoded). |

---

## 2. Detailed Tuning & Sensitivity Impact Analysis

### 1. `CoverageWeight` (Default: 10.0)
*   **Purpose:** Promotes the core objective of the optimization—maximizing precast erection coverage.
*   **Implementation Note:** In the fitness function, the coverage ratio term is scaled by `100.0` times `CoverageWeight` to ensure that coverage improvements always override all penalties.
*   **Increasing the weight:** Forces the solver to prioritize lifting every single element, even if it requires adding extra relocation setups or moving the crane over long distances.
*   **Decreasing the weight:** The solver will accept lower coverage if it means saving on relocation setups or mobilization costs.

### 2. `OverweightPenaltyWeight` (Default: 10.0)
*   **Purpose:** Enforces load chart capacity safety. Penalizes solutions that assign elements heavier than the crane's derated capacity.
*   **Increasing the weight:** Strongly discourages capacity violations. The solver will select larger crane models or add setups closer to heavy elements to ensure safe lifts.
*   **Decreasing the weight:** May allow capacity-violating setups to survive in early generations, which can sometimes help the solver bypass local minima traps, but risks generating invalid final solutions if left too low.

### 3. `RelocationWeight` (Default: 5.0)
*   **Purpose:** Penalizes solutions that use a high number of crane setups. Reflects mobilization costs and sequence time.
*   **Increasing the weight:** Forces the solver to search for compact layouts (e.g., covering all elements with 1 or 2 setups instead of 3).
*   **Decreasing the weight:** Allows the solver to freely add crane setups to maximize coverage.

### 4. `HeightPenaltyWeight` (Default: 5.0)
*   **Purpose:** Penalizes layouts where elements exceed hook height limits or require longer boom lengths than physically supported.
*   **Increasing the weight:** Restricts crane placements to areas where boom elevations can safely clear structure margins.
*   **Decreasing the weight:** Might result in placements where boom rigging lines collide with or fail to reach elements at higher elevations.

### 5. `RadiusPenaltyWeight` (Default: 3.0)
*   **Purpose:** Penalizes layouts where target elements fall outside the crane's minimum or maximum operating radii.
*   **Increasing the weight:** Forces setups to be placed within the exact horizontal reach envelope of all elements.
*   **Decreasing the weight:** Can result in crane placements where elements are out of reach.

### 6. `BalancePenaltyWeight` (Default: 2.0)
*   **Purpose:** Evaluates the variance of lift distributions among active crane setups. Favoring balanced distributions helps optimize crane utilization and logistics.
*   **Increasing the weight:** Encourages the solver to partition the site evenly (e.g., if there are 2 setups, each handles roughly 50% of the lifts).
*   **Decreasing the weight:** Allows highly unbalanced solutions to survive (e.g., one setup handles 95% of lifts, and a second setup handles only 5%).

### 7. `RadiusStdDevPenaltyWeight` (Default: 2.0)
*   **Purpose:** Penalizes high variation in lifting radii. Consistent operating radii make lifting operations safer and faster.
*   **Increasing the weight:** Encourages setups where all assigned elements are roughly the same distance away.
*   **Decreasing the weight:** Allows setups to mix very close and very far lifts, which increases operational variability.

### 8. `SimilarPositionPenaltyWeight` (Default: 8.0)
*   **Purpose:** Applies a penalty to solutions that place crane setups too close to each other.
*   **Increasing the weight:** Enforces distinct relocation setups. Prevents the solver from wasting setups on minor position adjustments.
*   **Decreasing the weight:** May cause the solver to place multiple crane setups within a few meters of each other, resulting in redundant relocations.

### 9. `RelocationDistancePenaltyWeight` (Default: 1.5)
*   **Purpose:** Penalizes total travel distance between sequential crane setup positions.
*   **Increasing the weight:** Promotes layouts where the crane moves along a short path (e.g., setups aligned in a line along one side of the structure).
*   **Decreasing the weight:** Allows setups to be scattered across opposite corners of the site, regardless of travel distance.

### 10. `RadiusQualityPenaltyWeight` (Default: 1.0)
*   **Purpose:** Encourages using shorter working radii (closer to the crane center) where capacities and safety margins are higher.
*   **Increasing the weight:** Pulls setups as close as possible to the centers of gravity of their target elements.
*   **Decreasing the weight:** Accepts lifts near the maximum radius limits of the crane.

---

## 3. Recommended Tuning Strategy

When optimizing a new project site, follow this tuning sequence:
1.  **Phase 1 (Feasibility):** Keep `CoverageWeight` ($10$) and `OverweightPenaltyWeight` ($10$) high. Ensure the solver can find a baseline layout that reaches all targets.
2.  **Phase 2 (Cost Reduction):** Increase `RelocationWeight` ($5 \rightarrow 8$) if you want to force the solver to reduce the number of crane setups.
3.  **Phase 3 (Refinement):** If setups are placed too close to each other, increase `SimilarPositionPenaltyWeight` to enforce separation. If crane relocations are scattered, increase `RelocationDistancePenaltyWeight` to pull them into a linear path.
