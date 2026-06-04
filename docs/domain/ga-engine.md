# Domain: Genetic Algorithm (GA) Optimization Engine

The optimization engine is responsible for finding the most effective sequence of coordinate positions to set up a mobile crane to maximize precast erection coverage while minimizing costs and satisfying safety regulations.

For a detailed walkthrough of the complete execution workflow, please refer to the [ga-pipeline.md](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ga-pipeline.md) document.

---

## 1. Chromosome Representation

A candidate relocation sequence is represented by the [CraneChromosome.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CraneChromosome.cs) class:
*   **Gene Sequence:** A chromosome contains a list of [DomainPoint3D](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Common/DomainPoint3D.cs) points representing the sequential coordinates of the crane setups:
    $$\text{Chromosome} = \langle \mathbf{P}_1, \mathbf{P}_2, \dots, \mathbf{P}_n \rangle$$
    where each setup $\mathbf{P}_i = (X_i, Y_i, Z_i)$ represents a distinct location.
*   **Variable Length:** The number of setups $n$ is dynamic and bounded by `settings.MaxCranePositions` (default 3, though configurable up to 4 or more).
*   **Sanitization:** The engine sanitizes final chromosomes during mutation, crossover, and replacement stages to filter out duplicate, invalid, or out-of-boundary points.

---

## 2. Spatial Constraints & Footprints

Constraints are evaluated at different stages to ensure valid search spaces:
1.  **Allowed Zone Inclusion:** Evaluated in `ConstraintEngine.IsValidSetupPoint()`. The entire crane footprint (circle of radius `CraneFootprintRadius` around the setup point) must lie completely inside the `Allowed` zone polygon.
2.  **Forbidden Zone Overlap:** Checked using ray-casting and segment distance algorithms in [SpatialMath.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/SpatialMath.cs). The crane footprint must not intersect any forbidden zone polygon or obstacle.
3.  **Proximity Constraints:** Crane setups must be separated by at least `settings.MinimumRelocationDistance` to be considered unique relocations. Proximity violations trigger similarity penalties.
4.  **Zone Boundary Projection:** If a perturbed point (during mutation) or a heuristic centroid (during initialization in [GAInitializer.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L295-L301)) falls outside the allowed zones, it is projected onto the nearest boundary of the closest allowed zone using `ProjectPointToZoneBoundary` before checking for validity.

---

## 3. Fitness Function

The fitness of a solution is calculated in [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L254-L265) using a multi-objective weighted deduction formula:

$$\text{Fitness} = \max\left(0, W_{\text{cov}} \cdot R_{\text{cov}} - \text{Penalty}_{\text{rel}} - W_{\text{rad}} \cdot P_{\text{rad}} - W_{\text{ow}} \cdot P_{\text{ow}} - W_{\text{h}} \cdot P_{\text{h}} - W_{\text{sc}} \cdot P_{\text{sc}} - W_{\text{rq}} \cdot P_{\text{rq}} - P_{\text{bal}} - P_{\text{sd}} - W_{\text{sim}} \cdot P_{\text{sim}} - W_{\text{dist}} \cdot P_{\text{dist}}\right)$$

### Components & Deductions

*   **Coverage Ratio ($R_{\text{cov}}$):** Number of successfully liftable elements divided by total elements. A coverage ratio of 0 immediately results in a total fitness of 0.
*   **Step-wise Relocation Penalty ($\text{Penalty}_{\text{rel}}$):** In [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L160-L162), this is calculated as `settings.RelocationWeight * (craneCount - 1)` for crane counts greater than 1 (0 penalty for a single setup). Note: This is an un-normalized step-wise deduction, whereas the diagnostic CSV logs the normalized fraction `(double)craneCount / MaxCranePositions` for `RelocationPenalty`.
*   **Out of Radius Fraction ($P_{\text{rad}}$):** The proportion of elements that are out of reach of all crane setups in the solution.
*   **Overweight Fraction ($P_{\text{ow}}$):** The proportion of elements that exceed the crane's load-chart lifting limits at their respective radii.
*   **Height Failure Fraction ($P_{\text{h}}$):** The proportion of elements that exceed hook height limits.
*   **Special Case Fraction ($P_{\text{sc}}$):** The proportion of elements marked as unchartable or invalid.
*   **Radius Quality ($P_{\text{rq}}$):** Encourages solutions to use shorter working radii: average radius of liftable elements divided by `MaxRadiusForNormalisation`.
*   **Balance Penalty ($P_{\text{bal}}$):** Weighted variance of liftable elements distributed across setups to favor balanced work distribution.
*   **Radius Std Dev Penalty ($P_{\text{sd}}$):** Penalizes variations in lifting radii to ensure consistent operations.
*   **Setup Proximity Penalty ($P_{\text{sim}}$):** Normalised proximity penalty for setups placed closer than `MinimumRelocationDistance`.
*   **Relocation Distance Penalty ($P_{\text{dist}}$):** Normalized total coordinate distance between sequential crane setups.

---

## 4. Genetic Operators

*   **Selection:** Implemented in [TournamentSelection.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs). Executes tournament selection with size $k = 3$. Wins are decided by fitness, then coverage, then setup count.
*   **Crossover:** Implemented in [CrossoverOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs). Performs uniform coordinate crossover per index with a 50% swap rate. Setups closer than the duplicate threshold ($2 \times \text{CraneFootprintRadius}$) are skipped rather than merged, which dynamically alters chromosome length.
*   **Mutation:** Implemented in [MutationOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L29-L112). Applies one of three mutations based on a hardcoded **80/10/10 probability split**:
    1.  **Coordinate Perturbation (80%):** Perturbs coordinates using Gaussian noise. Sigma is calculated from `MutationSigma` and can adaptively decrease over generations if `EnableAdaptiveSigma` is active. If the perturbed coordinate falls out of boundary, it is projected onto the closest zone boundary.
    2.  **Setup Addition (10%):** Spawns a new coordinate inside permissible zones, capped by `MaxCranePositions`. This increases chromosome length.
    3.  **Setup Removal (10%):** Deletes a random coordinate setup, maintaining a minimum of 1 setup. This decreases chromosome length.

---

## 5. Parallel Execution & Profiling

*   **Parallel Fitness Evaluation:** Implemented in [GAEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs). The engine utilizes PLINQ `AsParallel()` and thread-safe random managers to run candidate fitness scoring concurrently across CPU cores.
*   **Profiling Traces:** Profiling metrics (such as cache hit ratios, allocation deltas, and CPU generation timings) are gathered in real-time by the [GAProfiler.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/GAProfiler.cs) utility.

---

## 6. Termination & Convergence

The optimization loop terminates when:
1.  **Max Generations** is reached (configured via `GASettings.MaxGenerations`).
2.  **Stagnation Limit** is met: No improvement in the best fitness score for a sliding window of `EarlyStoppingGenerations` (default 20).
3.  **Relative Progress Convergence** is reached: Relative improvements in fitness drop below `ConvergenceThresholdPercent`. If `ConvergenceThresholdPercent == 0` (the default value), this progress convergence check is bypassed, meaning early stopping is solely determined by a complete lack of fitness improvement over the stagnation window.

---

## 7. Default UI/Dialog Execution Parameters

When running from the UI dialog (e.g., in [CraneOptimizerDialogWorkflow.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs#L90-L96)), the default parameters are:
*   **Population Size:** 30
*   **Max Generations:** 30
*   **Mutation Rate:** 0.1
*   **Crossover Rate:** 0.8
*   **Random Seed:** 42 (allows deterministic runs)

