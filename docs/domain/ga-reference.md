# Genetic Algorithm Optimization Reference Guide

This document provides a comprehensive technical reference for the Genetic Algorithm (GA) settings and hardcoded constants within the crane layout optimization engine.

---

## 1. GASettings Parameter Analysis

This section analyzes the properties of [GASettings.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs). For each parameter, we outline its default value, recommended range, engineering impact, performance impact, and risks.

### Population & Generation Parameters

#### 1. `PopulationSize`
*   **Default:** `100` (init) | **Recommended Range:** `50 - 500`
*   **Engineering Impact:** Controls candidate solution diversity. Higher values explore more areas of the site and reduce premature convergence.
*   **Performance Impact:** Linear scaling $O(P)$. Doubling the size doubles the execution time of each generation.
*   **Risks:** Too small ($<30$) leads to loss of diversity and premature convergence on poor local optima. Too large ($>500$) causes performance lag.

#### 2. `MaxGenerations`
*   **Default:** `200` (init) | **Recommended Range:** `100 - 1000`
*   **Engineering Impact:** Defines the absolute search cutoff.
*   **Performance Impact:** Linear scaling $O(G)$. Sets the maximum possible execution duration.
*   **Risks:** Too low ($<50$) cuts off the search before convergence. Too high ($>1000$) wastes CPU cycles unless early stopping triggers.

#### 3. `EarlyStoppingGenerations` (Stagnation Limit)
*   **Default:** `20` | **Recommended Range:** `10 - 50`
*   **Engineering Impact:** Defines how many generations of stagnation on the best solution are tolerated before terminating.
*   **Performance Impact:** Improves average runtimes significantly by terminating search early once convergence is reached.
*   **Risks:** Too small ($<5$) causes premature termination on flat fitness regions. Too large ($>50$) wastes computations.

#### 4. `ConvergenceThresholdPercent`
*   **Default:** `0.0` (disabled) | **Recommended Range:** `0.0 - 1.0`
*   **Engineering Impact:** Relative fitness improvement threshold.
*   **Performance Impact:** Accelerates termination when progress is negligible.
*   **Risks:** Too large ($>2\%$) triggers premature termination on slow convergence phases.

---

### Genetic Operator Rates & Parameters

#### 5. `CrossoverRate`
*   **Default:** `0.8` | **Recommended Range:** `0.6 - 0.95`
*   **Engineering Impact:** Probability of coordinate exchange between two parents. High values promote exploitation of good setups.
*   **Performance Impact:** Minimal. Swap logic is highly optimized.
*   **Risks:** Too low ($<0.4$) prevents recombination, reducing GA to random search with elitism. Too high ($>0.98$) degrades good solutions too fast.

#### 6. `MutationRate`
*   **Default:** `0.15` (init) | **Recommended Range:** `0.05 - 0.3`
*   **Engineering Impact:** Probability of mutating a chromosome. High values prevent local optimum traps.
*   **Performance Impact:** Minimal. Snapping and clipping are fast.
*   **Risks:** Too low ($<0.02$) leads to stagnation. Too high ($>0.4$) disrupts convergence, turning GA into random walk.

#### 7. `MutationSigma`
*   **Default:** `0.05` | **Recommended Range:** `0.01 - 0.2`
*   **Engineering Impact:** Base perturbation radius. Values $\le1.0$ are treated as a ratio of `MaxRadiusForNormalisation`.
*   **Performance Impact:** None.
*   **Risks:** Too small leads to local entrapment. Too large behaves like random coordinates generator.

#### 8. `MutationExplorationMultiplier`
*   **Default:** `2.0` | **Recommended Range:** `1.0 - 5.0`
*   **Engineering Impact:** Scaling multiplier applied to mutation range to push search boundaries.
*   **Performance Impact:** None.
*   **Risks:** Excessive values skip over local adjustments.

#### 9. `EnableAdaptiveSigma`
*   **Default:** `false` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** Enables generation-dependent cooling schedule for sigma: $\sigma_{\text{gen}} = \sigma \cdot \text{progress}^{\alpha} + \sigma_{\text{floor}}$.
*   **Performance Impact:** None.
*   **Risks:** None, but requires tuning cooling parameters.

#### 10. `SigmaCoolingExponent`
*   **Default:** `1.5` | **Recommended Range:** `1.0 - 3.0`
*   **Engineering Impact:** Controls decay curve shape of adaptive mutation sigma.
*   **Performance Impact:** None.
*   **Risks:** Too high ($>3.0$) decays the search range too early.

#### 11. `MinimumMutationSigma`
*   **Default:** `0.5` | **Recommended Range:** `0.1 - 2.0` (meters)
*   **Engineering Impact:** Sets lower bound floor for adaptive sigma to prevent complete search freeze.
*   **Performance Impact:** None.
*   **Risks:** Too small prevents local adjustments near the end of execution.

#### 12. `PositionSamplingAttempts`
*   **Default:** `80` | **Recommended Range:** `20 - 150`
*   **Engineering Impact:** Maximum attempts to sample a valid, collision-free coordinate.
*   **Performance Impact:** High in dense sites. More attempts increase collision check iterations.
*   **Risks:** Too low ($<10$) fails to find valid spots in highly congested layouts. Too high ($>200$) stalls execution when site is over-constrained.

---

### Mechanical, Physical & Safety Parameters

#### 13. `MaxCranePositions`
*   **Default:** `3` | **Recommended Range:** `1 - 10`
*   **Engineering Impact:** Hard limit on the maximum number of relocation setups per solution.
*   **Performance Impact:** High. Chromosome length scales evaluation arrays.
*   **Risks:** Setting this lower than physically required makes solutions infeasible (coverage cannot reach 100%).

#### 14. `CraneFootprintRadius`
*   **Default:** Set by input builder | **Recommended Range:** Derived from Crane dimensions
*   **Engineering Impact:** Defines outrigger/body clearance. Footprint must fit fully within allowed zones.
*   **Performance Impact:** None.
*   **Risks:** If set too small, outriggers may collide with physical site obstacles.

#### 15. `MinCraneSpacing` (MinimumRelocationDistance)
*   **Default:** `10.0` (meters) | **Recommended Range:** `5 - 30`
*   **Engineering Impact:** Minimum distance to treat setups as separate locations.
*   **Performance Impact:** None.
*   **Risks:** Setting too small ($<3\text{m}$) leads to redundant setups.

#### 16. `SafetyFactor`
*   **Default:** `0.95` | **Recommended Range:** `0.75 - 1.0`
*   **Engineering Impact:** Safe limit applied to chart capacity. Capacity is scaled by `DeratingRatio * SafetyFactor`.
*   **Performance Impact:** None.
*   **Risks:** Too close to 1.0 reduces safety margins. Too low ($<0.7$) makes crane placements conservative and expensive.

#### 17. `DeratingRatio`
*   **Default:** `0.9` | **Recommended Range:** `0.7 - 1.0`
*   **Engineering Impact:** Capacity derating.
*   **Performance Impact:** None.
*   **Risks:** Overriding without analysis risk over-constraining the load.

#### 18. `WireLength`
*   **Default:** `2.0` (meters) | **Recommended Range:** `1.0 - 10.0`
*   **Engineering Impact:** Added rigging wire length. Increases required lift heights.
*   **Performance Impact:** None.
*   **Risks:** Under-estimation leads to crane hook collisions.

#### 19. `AdditionalWt`
*   **Default:** `0.0` (tons) | **Recommended Range:** `0.0 - 5.0`
*   **Engineering Impact:** Adds rigging block/slings mass to the target weights.
*   **Performance Impact:** None.
*   **Risks:** Under-estimation leads to crane overloading.

---

### Offset Parameters

#### 20. `SlewOffsetX` / `SlewOffsetY` / `SlewOffsetZ`
*   **Default:** `0.0` | **Recommended Range:** Crane model dependent
*   **Engineering Impact:** Slew center mechanical offsets from crane base coordinate.
*   **Performance Impact:** None.
*   **Risks:** Mismatch results in incorrect operating radius calculations.

#### 21. `BoomHingeOffsetX` / `BoomHingeOffsetY` / `BoomHingeOffsetZ`
*   **Default:** `0.0` | **Recommended Range:** Crane model dependent
*   **Engineering Impact:** Boom hinge offsets from base coordinate for hoist boom length checks.
*   **Performance Impact:** None.
*   **Risks:** Direct impact on calculated boom length.

---

### Parallel & Optimization Strategy Parameters

#### 22. `EnableConcurrentOptimization`
*   **Default:** `true` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** When true, runs simultaneous multi-setup GA. When false, runs greedy set-cover iterations.
*   **Performance Impact:** Greedy set-cover iteration runs multiple small GAs sequentially. Concurrent optimization runs a single GA but with longer chromosomes.
*   **Risks:** Greedy set-cover fails to account for coordinated setups, yielding suboptimal global solutions.

#### 23. `EnableProfiling`
*   **Default:** `true` | **Recommended Range:** `true` or `false`
*   **Engineering Impact:** Toggles performance profile tracking via `GAProfiler`.
*   **Performance Impact:** Negligible overhead (ticks accumulation).
*   **Risks:** Disabling removes detailed diagnostic metrics.

#### 24. `RandomSeed`
*   **Default:** `42` | **Recommended Range:** `null` (random) or any integer (deterministic)
*   **Engineering Impact:** Guarantees repeatable optimizations.
*   **Performance Impact:** None.
*   **Risks:** Keeping static prevents discovering alternative placements for identical inputs.

---

## 2. Hardcoded Values Audit & Inventory

This section audits all hardcoded constants identified in the optimization layer. They are classified into **Critical Optimization Parameters**, **Engineering Constants**, and **Magic Numbers** with recommendations.

### A. Critical Optimization Parameters (Should Be Configurable)

These parameters affect solver quality and constraint validations directly. They should be exposed in `GASettings`.

#### 1. Heuristic Seeding Split Ratio (`0.30` / `70%`)
*   **Location:** [GAInitializer.cs Line 52](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L52)
*   **Current State:** `0.30` ratio dictates that 30% of initial populations are seeded using Morton Z-order centroids, and 70% randomly.
*   **Engineering Impact:** Governs initialization quality and diversity. Highly structured sites benefit from heuristic seeds; simple sites benefit from random exploration.
*   **Recommendation:** Move to a new property `GASettings.HeuristicSeedingRatio` (default `0.30`).

#### 2. Crossover Footprint Multiplier (`2.0`)
*   **Location:** [CrossoverOperator.cs Line 36](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs#L36)
*   **Current State:** Restricts crossover duplicate points using `2.0 * CraneFootprintRadius` distance.
*   **Engineering Impact:** Directly prevents offspring from retaining overlapping setups.
*   **Recommendation:** Move to `GASettings.CrossoverOverlapDistanceMultiplier` (default `2.0`).

#### 3. Crossover Footprint Fallback (`3.0`)
*   **Location:** [CrossoverOperator.cs Line 37](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs#L37)
*   **Current State:** Fallback distance limit of `3.0` meters if footprint is 0.
*   **Engineering Impact:** Governs recombination spacing when footprint data is missing.
*   **Recommendation:** Move to `GASettings.CrossoverOverlapDistanceFallback` (default `3.0`).

#### 4. Mutation Operator Probabilities (`0.80` / `0.90` / `0.10`)
*   **Location:** [MutationOperator.cs Lines 44, 90](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs#L44)
*   **Current State:** Hardcoded 80% perturbation drift, 10% addition (remainder under 90%), and 10% deletion mutation rates.
*   **Engineering Impact:** Controls layout change exploration. High addition rates increase setups; high deletion rates prune them.
*   **Recommendation:** Move to `GASettings.MutationDriftProbability` (default `0.80`), `GASettings.MutationAddProbability` (default `0.10`), and `GASettings.MutationRemoveProbability` (default `0.10`).

#### 5. Elitism Selection Preservation Rate (`0.10`)
*   **Location:** [ReplacementStrategy.cs Line 23](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/ReplacementStrategy.cs#L23)
*   **Current State:** Hardcoded `0.10` (10%) preservation of elite parents during population replacement.
*   **Engineering Impact:** High elitism preserves high-fitness solutions but leads to premature convergence. Low rates risk losing optimal placements.
*   **Recommendation:** Move to `GASettings.ElitismRate` (default `0.10`).

#### 6. Greedy Set-Cover Limits (`maxPoints = 4`, `targetCoverage = 0.95`, `minImprovement = 0.05`)
*   **Location:** [CranePlacementEngine.cs Lines 75-77](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs#L75-77)
*   **Current State:** Defines greedy set-cover cutoffs.
*   **Engineering Impact:** Governs loop iterations when concurrent engine is disabled.
*   **Recommendation:** Move to `GASettings.GreedyMaxSetups`, `GASettings.GreedyTargetCoverage`, and `GASettings.GreedyMinImprovement`.

---

### B. Engineering Constants (Acceptable as Hardcoded)

These are numerical tolerances or constants derived from mathematics or physical units. They are acceptable as constants.

#### 7. Floating-Point Comparison Epsilon (`1e-12` / `1e-9` / `1e-6`)
*   **Locations:**
    *   `1e-12` in [SpatialMath.PointInPolygon](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/Utilities/SpatialMath.cs#L55) (division protection)
    *   `1e-9` in [ZoneGeometryService.SegmentsIntersect2D](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services/ZoneGeometryService.cs) (collinearity checks)
    *   `1e-6` in [ZoneGeometryService.ValidateTriangulation](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Domain/Services/ZoneGeometryService.cs) (degenerate triangles)
*   **Rationale:** Standard floating-point tolerance offsets. Configurable parameters would introduce unnecessary complexity.
*   **Recommendation:** Remain hardcoded as `private const` or `readonly` constants.

#### 8. Snapping Snug Epsilon Tolerances (`0.001` / `0.005`)
*   **Locations:**
    *   `0.001` point equality tolerance in `ZoneGeometryService`.
    *   `0.005` area mismatch tolerance in `ZoneGeometryService`.
*   **Rationale:** Mesh boundary precision metrics.
*   **Recommendation:** Remain hardcoded.

#### 9. Morton Coordinate Scale Limits (`1023` / 10-bit range)
*   **Location:** [GAInitializer.cs Lines 222-223](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs#L222-223)
*   **Current State:** Snaps bounding boxes to $[0, 1023]$ range to perform Z-order spatial partitioning.
*   **Rationale:** Integer normalization range required for 20-bit Morton coordinate bit shifting.
*   **Recommendation:** Remain hardcoded but document constraints inside the class.

---

### C. Magic Numbers (Require Review & Justification)

These are values embedded directly in code without explanation or constants mapping.

#### 10. Selection Competitors Count (`3`)
*   **Location:** [TournamentSelection.cs Line 23](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs#L23)
*   **Current State:** Private constant `TournamentSize = 3` determines selection competitors.
*   **Engineering Impact:** Controls tournament pressure.
*   **Recommendation:** Move to `GASettings.TournamentSize` (default `3`).

#### 11. Special Case Penalty Weight (`1.0`)
*   **Location:** [FitnessEvaluator.cs Line 76](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs#L76)
*   **Current State:** Deducts 1.0 weight penalty for SpecialCase lift failures.
*   **Rationale:** Hardcoded deduction that behaves as an arbitrary scaling factor.
*   **Recommendation:** Move to `GASettings.SpecialCasePenaltyWeight` (default `1.0`).

#### 12. Normalization Detection Check Tolerance (`0.0001`)
*   **Location:** [OptimizationInputBuilder.cs Line 87](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs#L87)
*   **Current State:** Checks if `settings.DeratingRatio` is close to the default `0.9` (within `0.0001` epsilon) to auto-apply crane-specific specifications.
*   **Rationale:** Epsilon check used to detect whether settings retain their uninitialized defaults.
*   **Recommendation:** Define as a named constant `DefaultDetectionEpsilon` within the builder.
