# Domain: Genetic Algorithm (GA) Optimization Engine

The optimization engine is responsible for finding the most effective sequence of coordinate positions to set up a mobile crane to maximize precast erection coverage while minimizing costs and satisfying safety regulations.

---

## 1. Chromosome Representation

A candidate relocation sequence is represented by the [CraneChromosome.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CraneChromosome.cs) class:
*   **Gene Sequence:** A chromosome contains a list of [DomainPoint3D](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common/DomainPoint3D.cs) points representing the sequential coordinates of the crane setups:
    $$\text{Chromosome} = \langle \mathbf{P}_1, \mathbf{P}_2, \dots, \mathbf{P}_n \rangle$$
    where each setup $\mathbf{P}_i = (X_i, Y_i, Z_i)$ represents a distinct location.
*   **Variable Length:** The number of setups $n$ is dynamic and bounded by `settings.MaxCranePositions` (default 3).
*   **Sanitization:** The engine sanitizes final chromosomes to filter out coordinate points that do not contribute to coverage, are outside allowed zones, or duplicate other positions.

---

## 2. Spatial Constraints & Footprints

Instead of using hard rejections which create disconnected search spaces, constraints are evaluated at different stages:
1.  **Allowed Zone Inclusion:** Evaluated in `ConstraintEngine.IsValidSetupPoint()`. The entire crane footprint (circle of radius `CraneFootprintRadius` around the setup point) must lie completely inside the `Allowed` zone polygon.
2.  **Forbidden Zone Overlap:** Checked using ray-casting and segment distance algorithms in [SpatialMath.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/SpatialMath.cs). The crane footprint must not intersect any forbidden zone polygon or obstacle.
3.  **Proximity Constraints:** Crane setups must be separated by at least `settings.MinimumRelocationDistance` to be considered unique relocations. Proximity violations trigger similarity penalties.

---

## 3. Fitness Function

The fitness of a solution is calculated in [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs) using a multi-objective weighted deduction formula:

$$\text{Fitness} = \max\left(0, W_{\text{cov}} \cdot R_{\text{cov}} - W_{\text{rel}} \cdot P_{\text{rel}} - W_{\text{rad}} \cdot P_{\text{rad}} - W_{\text{ow}} \cdot P_{\text{ow}} - W_{\text{h}} \cdot P_{\text{h}} - W_{\text{sc}} \cdot P_{\text{sc}} - W_{\text{rq}} \cdot P_{\text{rq}} - W_{\text{bal}} \cdot P_{\text{bal}} - W_{\text{sd}} \cdot P_{\text{sd}} - W_{\text{sim}} \cdot P_{\text{sim}} - W_{\text{dist}} \cdot P_{\text{dist}}\right)$$

### Components & Deductions

| Term | Weight Parameter | Description |
| :--- | :--- | :--- |
| $R_{\text{cov}}$ | `CoverageWeight` ($10$) | **Coverage Ratio:** Number of successfully liftable elements divided by total elements. A coverage ratio of 0 immediately results in a total fitness of 0. |
| $P_{\text{rel}}$ | `RelocationWeight` ($5$) | **Relocation Penalty:** Scaled by setup count: $\frac{n - 1}{\text{MaxCranePositions}}$. Penalizes relocation setup changes. |
| $P_{\text{rad}}$ | `RadiusPenaltyWeight` ($3$) | **Out of Radius Fraction:** The proportion of elements that are out of reach of all crane setups in the solution. |
| $P_{\text{ow}}$ | `OverweightPenaltyWeight` ($10$) | **Overweight Fraction:** The proportion of elements that exceed the crane's load-chart lifting limits at their respective radii. |
| $P_{\text{h}}$ | `HeightPenaltyWeight` ($5$) | **Height Failure Fraction:** The proportion of elements that exceed hook height limits. |
| $P_{\text{sc}}$ | `SpecialCasePenaltyWeight` ($1$) | **Special Case Fraction:** The proportion of elements marked as unchartable or invalid. |
| $P_{\text{rq}}$ | `RadiusQualityPenaltyWeight` ($1.0$) | **Radius Quality:** Encourages solutions to use shorter working radii: average radius of liftable elements divided by `MaxRadiusForNormalisation`. |
| $P_{\text{bal}}$ | `BalancePenaltyWeight` ($2.0$) | **Balance Variance:** Normalized variance of liftable elements distributed across setups to favor balanced work distribution. |
| $P_{\text{sd}}$ | `RadiusStdDevPenaltyWeight` ($2.0$) | **Radius Standard Deviation:** Penalizes large variations in lifting radii to ensure lifting operations remain consistent. |
| $P_{\text{sim}}$ | `SimilarPositionPenaltyWeight` ($8.0$) | **Setup Proximity Penalty:** Normalised proximity penalty for setups placed closer than `MinimumRelocationDistance`. |
| $P_{\text{dist}}$ | `RelocationDistancePenaltyWeight` ($1.5$) | **Relocation Distance Travel:** Normalized total coordinate distance between sequential crane setups. |

---

## 4. Genetic Operators

*   **Selection:** Implemented in [TournamentSelection.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs). Executes tournament selection with size $k = 3$. Wins are decided by fitness, then coverage, then setup count.
*   **Crossover:** Implemented in [CrossoverOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs). Performs uniform coordinate crossover per index with a 50% swap rate. Setups closer than the duplicate threshold ($2 \times \text{CraneFootprintRadius}$) are merged to prevent coordinate redundancy.
*   **Mutation:** Implemented in [MutationOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs). Applies one of three mutations based on a hardcoded **80/10/10 probability split**:
    1.  **Coordinate Perturbation (80%):** Perturbs coordinates using Gaussian noise. Sigma is calculated from `MutationSigma` and can adaptively decrease over generations if `EnableAdaptiveSigma` is active.
    2.  **Setup Addition (10%):** Spawns a new coordinate inside permissible zones, capped by `MaxCranePositions`.
    3.  **Setup Removal (10%):** Deletes a random coordinate setup, maintaining a minimum of 1 setup.

---

## 5. Parallel Execution & Profiling

*   **Parallel Fitness Evaluation:** Implemented in [GAEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs). The engine utilizes PLINQ `AsParallel()` and thread-safe random managers to run candidate fitness scoring concurrently across CPU cores.
*   **Profiling Traces:** Profiling metrics (such as cache hit ratios, allocation deltas, and CPU generation timings) are gathered in real-time by the [GAProfiler.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/GAProfiler.cs) utility.

---

## 6. Termination & Convergence

The optimization loop terminates when:
1.  **Max Generations** is reached (configured via `GASettings.MaxGenerations`).
2.  **Stagnation Limit** is met: No improvement in the best fitness score for a sliding window of `EarlyStoppingGenerations` (default 20).
3.  **Relative Progress Convergence** is reached: Relative improvements in fitness drop below `ConvergenceThresholdPercent`.
