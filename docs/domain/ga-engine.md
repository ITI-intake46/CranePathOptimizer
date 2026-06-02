# Domain: Genetic Algorithm (GA) Optimization Engine

The optimization engine is responsible for finding the most effective set of $(X, Y)$ coordinates to position a mobile crane across multiple setups to maximize precast erection coverage.

---

## 1. Chromosome Representation

The problem encodes a complete multi-setup relocation solution as a **multi-position chromosome**:

$$\text{Chromosome} = \langle (X_1, Y_1, Z_1), (X_2, Y_2, Z_2), \dots, (X_n, Y_n, Z_n) \rangle$$

Where:
*   $n$ represents a variable number of crane setup positions per candidate solution (bounded by `settings.MaxCranePositions`).
*   **Initialization:** Active setup positions are initialized inside randomly chosen permissible zones.
*   **Validity Filtering:** Candidates are validated in real-time. Setup points are subjected to strict circle containment and forbidden zone overlap checks. Invalid setup points are filtered out of the chromosome before scoring.

### Implementation Reference

File:
[CraneChromosome.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CraneChromosome.cs)

Class:
`CraneChromosome`

Property:
`CranePositions` (type `List<DomainPoint3D>`)

---

## 2. Spatial Constraints & Footprints

Instead of applying soft fitness penalties for obstacle overlaps or off-site setups, the engine enforces these checks as **hard constraints** via [ConstraintEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs) during chromosome generation and mutation sweeps:

1.  **Allowed Zone Circle Containment:** The crane footprint (defined by `settings.CraneFootprintRadius`) must be **fully contained** inside an allowed `GreenAreaZone` polygon:
    $$\text{CircleInsidePolygon}(\text{SetupPoint}, \text{FootprintRadius}, \text{AllowedZone}) = \text{True}$$
2.  **Forbidden Zone Overlap Checking:** The crane footprint must **not overlap** any forbidden zone or obstacle polygon:
    $$\text{CircleOverlapsPolygon}(\text{SetupPoint}, \text{FootprintRadius}, \text{ForbiddenZone}) = \text{False}$$
3.  **Setup Separation Constraints:** Relocated positions must be separated by at least `settings.MinimumRelocationDistance` to prevent duplicate or redundant crane positions.

### Implementation Reference

File:
[ConstraintEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs)

Class:
`ConstraintEngine`

Method:
`IsValidSetupPoint()` and `IsCircleInsidePolygon()`

---

## 3. Detailed Fitness Scoring

Candidate solutions that have valid setup points are evaluated using a normalized, settings-weighted deduction formula. This ensures that the fitness scale remains independent of the total number of elements or site footprint size.

### Fitness Formula

$$\text{Fitness} = \max\left(0, W_{\text{cov}} \times R_{\text{cov}} - W_{\text{rel}} \times P_{\text{rel}} - W_{\text{rad}} \times P_{\text{rad}} - W_{\text{ow}} \times P_{\text{ow}} - W_{\text{h}} \times P_{\text{h}} - W_{\text{sc}} \times P_{\text{sc}} - W_{\text{rq}} \times P_{\text{rq}} - P_{\text{bal}} - P_{\text{sd}} - W_{\text{sim}} \times P_{\text{sim}} - W_{\text{dist}} \times P_{\text{dist}}\right)$$

Where:
*   **Coverage Ratio ($R_{\text{cov}}$):** Unique liftable elements divided by total elements. If $R_{\text{cov}} = 0$, fitness is 0.
*   **Relocation Penalty ($P_{\text{rel}}$):** Crane setups count divided by `MaxCranePositions`.
*   **Failure Fractions:** Out-of-radius fraction ($P_{\text{rad}}$), overweight fraction ($P_{\text{ow}}$), height fail fraction ($P_{\text{h}}$), and uncharted special cases ($P_{\text{sc}}$) normalized by total elements.
*   **Working Radius Quality ($P_{\text{rq}}$):** Average working radius of liftable elements divided by `MaxRadiusForNormalisation`.
*   **Setup Balance Variance ($P_{\text{bal}}$):** Normalized variance of liftable elements assigned per crane position.
*   **Lifting Standard Deviation ($P_{\text{sd}}$):** Spread of working radii divided by `MaxRadiusForNormalisation`.
*   **Similarity Penalty ($P_{\text{sim}}$):** Deductions for setups placed closer than `MinimumRelocationDistance`.
*   **Mobilization Travel Distance ($P_{\text{dist}}$):** Total relocation travel distance normalized by site bounds.

### Implementation Reference

File:
[FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs)

Class:
`FitnessEvaluator`

Method:
`EvaluateFitness()`

---

## 4. Genetic Operators

*   **Selection:** Tournament selection (default size = 3).
*   **Crossover:** [CrossoverOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs) performs index-matching setup crossovers.
*   **Mutation:** [MutationOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs) applies Gaussian perturbations to setup coordinates.

---

## 5. Termination & Convergence

The optimization loop continues until:
1.  **Max Generations reached** (default 500).
2.  **Convergence criteria met:** The difference between the maximum and minimum best fitness values over a sliding window (default 50 generations) drops below a threshold (default 0.01).
