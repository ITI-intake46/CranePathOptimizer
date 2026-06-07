# Mobile Crane Path Optimizer - Project Roadmap

This document outlines the implementation phases of the Mobile Crane Path Optimizer application.

---

## Roadmap Phases

### Phase 14 - Caching and Performance Optimization
*   **Status:** ✅ Complete
*   **Notes:** Implemented pre-indexed load charts and static element caches.

### Phase 15 - Reachability Intelligence & Search Space Pruning
*   **Status:** ✅ Complete
*   **Description:** Built pre-computation service to map allowed zone grid reachability before starting GA.
*   **Implementation Notes:**
    - Created [ReachabilityMapService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/ReachabilityMapService.cs) implementing [IReachabilityMapService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/IReachabilityMapService.cs).
    - Added settings properties to [GASettings.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs).
    - Seeding is split between 30% reachability hot spots and 70% random inside [GAInitializer.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs).
    - UI warning is shown in [CraneOptimizerDialog.xaml.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml.cs) when coverage is low (<20%).

### Phase 16 - Dynamic Position Count Discovery & Adaptive Relocation Weighting
*   **Status:** ✅ Complete
*   **Description:** Implemented a multi-stage Genetic Algorithm orchestration loop to dynamically discover the optimal number of crane setups (1, 2, or 3 positions) and apply adaptive relocation weights.
*   **Implementation Notes:**
    - Created [IMultiStageGAOrchestrator.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/IMultiStageGAOrchestrator.cs) and [MultiStageGAOrchestrator.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/MultiStageGAOrchestrator.cs).
    - Integrates into [CranePlacementService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Services/CranePlacementService.cs) to scale setups dynamically when target coverage is not reached.
    - Fully verified with **312 tests passing** successfully under branch `phase16-17_HungarianAlgorithms`.

### Phase 17 - Bipartite Matching Assignment Engine (Hungarian Solver)
*   **Status:** ✅ Complete
*   **Description:** Built the Hungarian Assignment Solver using successive relocation min-cost flow to globally optimize element assignment across crane positions.
*   **Implementation Notes:**
    - Created [HungarianAssignmentService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/HungarianAssignmentService.cs) implementing [IElementAssignmentService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/IElementAssignmentService.cs).
    - Solves bipartite assignment globally under capacity limits and load balancing in $O(N \cdot M^2)$ time ($M \le 3$), executing in **38ms** (well under the 50ms budget) on 3,940 elements (try9).
    - Registered dynamically in DI in [App.xaml.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs) switchable based on the `UseHungarianAssignment` flag in `GASettings`.
    - Fully validated with **312 tests passing** successfully under branch `phase16-17_HungarianAlgorithms`.

---

## Historical Performance Assessments & Research

## Consolidated from: Executive Summary (project/ga-performance-assessment.md)

The GA feels slow because the hot path repeatedly evaluates every element against every crane setup for every solution, generation, child population, and greedy set-cover iteration. The dominant cost is not selection, crossover, mutation, or final coverage accounting; it is assignment evaluation plus load-chart resolution inside `ElementAssignmentService`.

Measured synthetic benchmark evidence from `GoldenMasterTests.RunBenchmarkScenarios` confirms this pattern. With `PopulationSize = 15`, `MaxGenerations = 10`, `MaxCranePositions = 4`, and four greedy set-cover iterations, `ElementAssignmentService.EvaluateAssignment` consumed most measured GA time:

| Scenario | Selected Elements | Total Run | GA Run | Assignment | Fitness | Gen 0 GC |
|---|---:|---:|---:|---:|---:|---:|
| Small | 10 | 321.690 ms | 192.516 ms | 93.037 ms | 7.549 ms | 3 |
| Medium | 50 | 397.850 ms | 371.606 ms | 326.051 ms | 11.943 ms | 18 |
| Large | 200 | 911.397 ms | 840.085 ms | 787.596 ms | 22.932 ms | 62 |

The measured 200-element scenario spent about 86% of total run time and about 94% of GA run time in assignment evaluation. Fitness scoring was small by comparison. Constraint validation is visible during initialization, mutation, and sanitization, but it is not the dominant cost in the benchmark case.

Realistic improvement potential is high. The largest ROI comes from reducing repeated pair evaluations and load-chart scans, then parallelizing independent solution or element evaluations after thread-safety issues are resolved. A conservative target is 3x-8x from parallel fitness/assignment evaluation on modern CPUs, with additional 2x-5x possible from caching/indexing load chart and geometric pair calculations depending on chart size and repeated setup coordinates. Combined improvements can be substantial, but only if diagnostics allocation and thread-safety are addressed first.

This report does not optimize or refactor code. It documents current performance characteristics, measured behavior, and recommended optimization order.

# GA Workflow Analysis

The current GA run is orchestrated through `CranePlacementEngine.RunOptimization`, which wraps the GA in a greedy set-cover loop. The outer loop runs while elements remain uncovered and stops after four selected setup points, 95% coverage, or insufficient incremental improvement.

Workflow per set-cover iteration:

1. `CranePlacementEngine` replaces `input.Elements` with the remaining uncovered elements.
2. `GAEngine.Run` initializes a population through `GAInitializer.InitializePopulation`.
3. `GAEngine.Run` evaluates every current population individual.
4. For every generation, it sorts to find the current best, selects parents, creates children, sanitizes children, optionally mutates children, evaluates all children, and applies replacement.
5. `CranePlacementEngine.MapResult` re-evaluates final chromosome positions through `ElementAssignmentService.EvaluatePosition` to build per-position lift results.
6. The best point for that set-cover iteration is selected by new liftable coverage.
7. `CoverageEngine.FilterRemainingElements` creates the next remaining-element list.
8. After the loop, `CoverageEngine` deduplicates and recalculates coverage, `AssignmentPolicy.ResolveFinalAssignments` resolves unique final element ownership, and `GACsvDiagnosticsService.GenerateDiagnostics` writes reports.

The main per-solution evaluation path is:

1. `GAEngine.Run` calls `ElementAssignmentService.EvaluateAssignment`.
2. `EvaluateAssignment` rebuilds an assignment lookup dictionary when manual assignments exist.
3. For each element, it either evaluates one user-assigned crane position or calls `EvaluateBestPosition`.
4. `EvaluateBestPosition` loops over every crane setup in the chromosome.
5. `ComputeEvaluationResult` calculates radius, lift height, boom length, required capacity, and calls `LoadChartCapacityResolver.Resolve`.
6. `LoadChartCapacityResolver.Resolve` scans and filters the crane load chart with LINQ, groups duplicate boom lengths, builds diagnostic strings, and returns a detailed result object.
7. `ElementAssignmentService` creates one retained `ElementEvaluationResult` per element, then summarizes assignments into dictionaries of element IDs.
8. `FitnessEvaluator.EvaluateFitness` scans the retained evaluation results and computes coverage and penalties.

The important distinction is that `EvaluateBestPosition` may compute several candidate position evaluations but retains only the best per element. The temporary candidate objects and resolver results still allocate and execute all calculations.

# CPU Hotspots

## Critical Hotspots

`ElementAssignmentService.EvaluateAssignment` is the critical hotspot. It is called once per existing population individual and once per child individual in every generation. In the 200-element benchmark it was called 785 times and consumed 787.596 ms. This service multiplies by element count and crane setup count, so it becomes more expensive as projects grow.

`LoadChartCapacityResolver.Resolve` is the deepest repeated calculation inside assignment evaluation. It performs repeated LINQ scans over `craneSpec.LoadChart`, including `Where(...).ToList()`, `Min`, another `Where(...).OrderBy(...).ToList()`, `GroupBy`, per-group `OrderBy(...).ThenBy(...).ToList()`, per-group positive filtering, and diagnostic string construction. It is called for every element/setup candidate pair. The existing `CraneConfigurationEngine.CraneCapacity` cache was not used in the benchmark path; profiler output reported zero capacity cache hits and zero misses.

`GAEngine.GenerationLoop` is the aggregate critical loop. It contains two sequential evaluation phases per generation: existing population first, then children after genetic operations. The measured 200-element benchmark spent 834.339 ms in this loop.

## High Hotspots

`CranePlacementEngine` greedy set-cover wrapper can multiply the entire GA run by up to four. In the measured scenarios, all benchmark cases used four greedy iterations. This outer loop makes otherwise small GA settings much more expensive.

`ConstraintEngine.IsValidSetupPoint` is high during initialization, mutation, and child sanitization. It validates zones and normalizes vertices on each point validation. In the 50-element benchmark it was called 1,869 times. With complex polygons or many zones, this can become a major CPU cost even though it was not dominant in the synthetic rectangular-zone benchmark.

`GAInitializer.ClusterElements` is high during initialization for larger element sets. It performs multiple full-list `Min`/`Max` passes and an `OrderBy` over elements for every heuristic chromosome. Since 30% of the initial population is heuristic, initialization cost scales with `0.3 * populationSize * elements * log(elements)`.

`ReplacementStrategy.Replace` sorts old population and merged non-elites every generation. It is much smaller than assignment for current settings, but it uses repeated LINQ sorting and `Sum` tie-breakers over crane positions.

## Medium Hotspots

`FitnessEvaluator.EvaluateFitness` scans results, builds a `HashSet<string>` of unique liftable IDs, performs a second pass for radius standard deviation, and calls constraint penalty helpers. It was only 22.932 ms in the 200-element benchmark, but it scales linearly with element count and is called as often as assignment.

`TournamentSelection.SelectParents` copies the full population to a pool for every parent selection through `population.Individuals.ToList()`, then removes tournament candidates. With current population sizes this is minor. With large populations, it becomes unnecessary repeated list allocation and copying.

`CranePlacementEngine.MapResult` re-evaluates each final setup against all elements after the GA run. This is not repeated per generation, so it is medium or low in most runs, but it performs the same lift calculations again.

`AssignmentPolicy.ResolveFinalAssignments` loops by distinct element ID and searches each final position's result list using `FirstOrDefault`. This is acceptable for a few final positions, but its shape is `O(elements * positions * elementResultsPerPosition)` and can become noticeable for thousands of elements.

## Low Hotspots

`CrossoverOperator.Crossover` is cheap. It copies references to existing immutable-style `DomainPoint3D` values and does not perform geometry or chart work.

`MutationOperator.Mutate` is usually cheap relative to assignment, but it can call constraint validation repeatedly. Its cost rises with `PositionSamplingAttempts`, polygon complexity, and failed mutation attempts.

`CoverageEngine.RecalculateCoverage`, `DeduplicateFinalPositions`, and `FilterRemainingElements` were negligible in measured runs.

# Memory Hotspots

The largest allocation source is per-evaluation object creation inside assignment and load-chart resolution. Every element/setup candidate pair creates a `LoadChartResolverResult`; every selected best element creates an `ElementEvaluationResult`; every solution evaluation creates an `AssignmentResult` containing a list and dictionaries. These objects are short-lived during population and child evaluation and drive Gen 0 garbage collection.

Measured GC growth supports this. Gen 0 collections rose from 3 in the 10-element benchmark to 18 in the 50-element benchmark and 62 in the 200-element benchmark. Managed memory deltas were approximate because profiling intentionally uses `GC.GetTotalMemory(false)` without forcing GC, but the collection counts clearly show high temporary allocation pressure.

Specific allocation sources:

| Component | Allocation Pattern | Impact |
|---|---|---|
| `LoadChartCapacityResolver.Resolve` | `Where(...).ToList()`, `OrderBy(...).ToList()`, `GroupBy`, per-group lists, `checkedBooms`, `rejections`, `string.Join` | Critical temporary allocations per element/setup pair |
| `ElementAssignmentService.ComputeEvaluationResult` | Full diagnostic `ElementEvaluationResult` object with many fields and strings | Critical when repeated across population and generations |
| `AssignmentResult` | New list plus two dictionaries per solution evaluation | High repeated allocation |
| `FitnessEvaluator.EvaluateFitness` | New `FitnessBreakdown` and `HashSet<string>` per solution evaluation | Medium allocation |
| `UpdateAssignmentSummary` | Creates `List<string>` per crane index in dictionaries | Medium allocation |
| `TournamentSelection.PickRandomCandidates` | Copies full population to a new list for every selected parent | Medium at larger populations |
| `ReplacementStrategy.Replace` | Multiple LINQ ordered lists and merged lists | Medium per generation |
| `GAInitializer.ClusterElements` | Ordered element list, cluster lists, `Skip`/`Take` lists | Medium at initialization |
| `CranePlacementEngine.MapResult` | Per-position result lists and mapped `ElementLiftResult` objects | High once per set-cover iteration, not per generation |
| `GACsvDiagnosticsService.GenerateDiagnostics` | Large detailed row lists and strings | Medium post-run; not part of optimization search loop |

Deep copying is limited but still present. `CraneSolution.DeepCopy` copies the chromosome position list when preserving the best solution and when crossover is skipped. It does not deep-copy `DomainPoint3D` instances, so it is not the main memory driver.

# Complexity Analysis

Definitions:

| Symbol | Meaning |
|---|---|
| `E` | selected elements in current optimization pass |
| `P` | population size |
| `G` | actual generation count |
| `K` | average crane setup positions per chromosome, bounded by `MaxCranePositions` |
| `C` | load chart target point count |
| `Z` | allowed plus forbidden zone count |
| `V` | average polygon vertex count |
| `S` | greedy set-cover iterations, max 4 in current code |
| `A` | position sampling attempts |

Main search complexity is approximately `O(S * G * P * E * K * C log C)` with the current load-chart resolver shape. The `C log C` part comes from repeated ordering and grouping of candidate load-chart entries. If chart scans are considered fixed-size, the practical scaling with elements is close to linear: `O(S * G * P * E * K)`.

The current generation performs repeated evaluation of the existing population and children. The measured hit counts show 785 assignment/fitness evaluations for a 15-population, 10-generation, four-iteration benchmark because early stopping and child counts vary, but the effective unit of work is still solution evaluations times elements times setup count.

Initialization complexity is `O(P * K * A * Z * V + 0.3P * E log E)`. The first term is random valid point sampling and constraint checks; the second term is heuristic Morton-order clustering.

Constraint validation complexity per setup point is `O(Z * V)` plus repeated zone validation and normalization. For complex site boundaries this can become more expensive than the simple rectangle benchmark suggests.

Memory growth during active optimization is mostly short-lived and approximately `O(E)` per evaluated solution plus LINQ temporaries per element/setup/load-chart lookup. Retained population memory is small: `O(P * K)`. Final result memory is `O(finalPositions * E)` because each final position stores element results.

Estimated scaling using the measured 200-element benchmark as a baseline is shown below. These estimates assume the same benchmark settings, same chart size, similar early-stopping behavior, four greedy iterations, and no change in hardware or build mode. Real projects with larger load charts or complex polygons may be slower.

| Elements | Estimated Total Time | Estimated Assignment Time | Relative Memory/GC Pressure | Notes |
|---:|---:|---:|---:|---|
| 100 | 0.45-0.55 s | 0.35-0.40 s | Moderate | Linear interpolation from 50/200-element runs |
| 500 | 2.0-2.5 s | 1.9-2.0 s | High | Still plausible under current small population/generation settings |
| 1000 | 4.0-5.0 s | 3.8-4.0 s | Very high | Approaches or exceeds target responsiveness depending on chart size |
| 5000 | 20-30 s | 19-25 s | Extreme | Likely unacceptable without caching, allocation reduction, and parallelism |

For production defaults mentioned in docs, `PopulationSize = 100` and `MaxGenerations = 500`, runtime can be far higher than the benchmark. Compared with `P = 15`, `G = 10`, a full `P = 100`, `G = 500` run has up to about 333x more population-generation evaluation work before early stopping. Early stopping can reduce this, but worst-case scaling explains why the GA can feel slow on large projects.

# Bottleneck Ranking

| Rank | Severity | Component | Reason |
|---:|---|---|---|
| 1 | Critical | `ElementAssignmentService.EvaluateAssignment` | Dominates measured runtime; multiplies by `S * G * P * E * K` |
| 2 | Critical | `LoadChartCapacityResolver.Resolve` | Repeated chart scans, sorting, grouping, list creation, and diagnostic string creation per element/setup pair |
| 3 | Critical | Sequential population and child evaluation in `GAEngine.Run` | Independent chromosome evaluations are single-threaded; leaves CPU cores idle |
| 4 | High | Greedy set-cover wrapper in `CranePlacementEngine.RunOptimization` | Can run the full GA up to four times |
| 5 | High | Per-evaluation allocation of diagnostics-heavy result objects | Causes frequent Gen 0 GC and memory churn |
| 6 | High | Constraint validation during sampling, sanitization, and mutation | Revalidates and normalizes zones repeatedly; grows with polygon complexity |
| 7 | Medium | Fitness scoring | Linear scans and `HashSet` allocation per solution; small measured cost but called frequently |
| 8 | Medium | Selection/replacement LINQ allocations and sorts | Minor at current population sizes, grows with larger populations |
| 9 | Medium | Final assignment resolution and diagnostics | Not search-loop dominant, but expensive after large runs |
| 10 | Low | Crossover and basic mutation math | Small compared with assignment and chart resolution |

# Cache Opportunities

Existing caches:

| Cache | Current Status | Finding |
|---|---|---|
| `CraneConfigurationEngine._capacityCache` | Thread-safe `ConcurrentDictionary` keyed by chart identity, radius, boom length, derating, safety factor | Not used by the primary assignment path; benchmark showed 0 hits and 0 misses |
| `GAProfiler` accumulators | Thread-safe timing counters | Diagnostic only; useful for measurement, not optimization |

Missing or underused cache opportunities:

| Opportunity | Current Repetition | Expected ROI |
|---|---|---|
| Load chart radius/boom index | `Resolve` repeatedly filters and sorts `LoadChart` | Critical; pre-indexing chart radii and boom groups would reduce per lookup cost |
| Capacity resolver result cache | Same rounded radius/boom/weight/settings combinations can recur due to grid snapping and repeated chromosomes | High if grid snapping or duplicate positions are common |
| Element static properties | Required weight and element half-height are recomputed repeatedly | Medium; cheap individually, huge call count |
| Setup-element geometry | Radius, boom length, and lift height are recomputed for identical setup/element/settings pairs | Critical when setup points repeat through elitism, crossover, grid snapping, and set-cover iterations |
| Zone validation/normalization | `IsValidSetupPoint` validates and normalizes polygons on every point check | High for complex zones |
| Zone bounding boxes/elevations/centroids | Recomputed in initializer helpers through `Average` and `BoundingBox` | Medium |
| Assignment lookup | Rebuilt per solution evaluation when manual assignment exists | Medium if assignments are populated |
| Final result element lookup | Rebuilt in each `MapResult` call | Low to medium |

Caching cautions:

1. Cache keys need stable coordinate rounding if positions are floating-point. Exact double keys will miss frequently unless grid snapping is active.
2. Load chart caches must include crane specification identity and safety/derating settings.
3. Geometry caches must include setup point, element ID, COG, offsets, wire length, and grid/snap assumptions.
4. Mutable inputs are a risk. If `CraneSpecification.LoadChart`, zone vertices, or element COG/weight can change during a run, caches must be run-scoped or invalidated.
5. Diagnostic strings should not be part of hot-path cache values unless diagnostics are explicitly needed.

# Parallelization Opportunities

Do not parallelize until thread-safety issues are resolved and verified. The following readiness assessment is based on current code.

Thread-safe or mostly thread-safe operations:

| Operation | Assessment |
|---|---|
| `FitnessEvaluator.EvaluateFitness` | Mostly thread-safe if each call receives an independent `AssignmentResult`; uses local state plus injected constraint engine |
| `ElementAssignmentService.EvaluateAssignment` | Mostly thread-safe by local state if dependencies are thread-safe and inputs are read-only |
| `LoadChartCapacityResolver.Resolve` | Stateless and thread-safe if `CraneSpecification.LoadChart` is not mutated during evaluation |
| `LiftingCalculationEngine` methods | Stateless calculations; thread-safe if config dependency is safe |
| `CraneConfigurationEngine._capacityCache` | Uses `ConcurrentDictionary`; cache operations are thread-safe |
| `CoverageEngine` methods | Local state but mutates `PlacementResult`; safe only per independent result |

Not thread-safe or risky operations:

| Operation | Risk |
|---|---|
| `GARandomManager.Instance` | Shared `Random` is not thread-safe; selection, crossover, mutation, and initialization cannot be parallelized safely as-is |
| `GAEngine.Run` population mutation | Writes `solution.Fitness`, `solution.CoverageRatio`, creates/replaces population, mutates child chromosomes |
| `GAProfiler` metadata fields | Accumulators are concurrent, but metadata and enabled/reset state are global static mutable state |
| `ConstraintEngine` geometry service | Depends on `IZoneGeometryService` implementation; current holder/global service pattern should be verified before concurrent calls |
| `CranePlacementEngine.RunOptimization` | Mutates `input.Elements`, clears shared config cache, writes reports/logs, and updates final result |
| `GACsvDiagnosticsService.GenerateDiagnostics` | File-system writes and timestamped output; not a search-loop parallel target |

Best parallelization candidates after thread-safety cleanup:

| Candidate Loop | Why It Is Suitable |
|---|---|
| Population evaluation loop in `GAEngine.Run` | Each chromosome fitness evaluation is independent after initialization |
| Child evaluation loop in `GAEngine.Run` | Each child assignment/fitness evaluation is independent after mutation/sanitization |
| Element loop inside `ElementAssignmentService.EvaluateAssignment` | Each element evaluation is independent before summary aggregation |
| Crane setup candidate loop inside `EvaluateBestPosition` | Per-position candidate calculations are independent, but small `K` may not justify overhead |
| Final `MapResult` position evaluation | Positions can be evaluated independently after final solution is selected |

Recommended parallelization order is population/child evaluation first. It has the largest granularity and lowest coordination overhead. Element-level parallelism should be secondary because it creates many small tasks and needs careful aggregation to avoid locking away the benefit.

# Quick Wins

These are analysis recommendations only; no code has been changed.

1. Use existing `GAProfiler` output as the baseline for every optimization experiment. Keep `PopulationSize`, `MaxGenerations`, `RandomSeed`, chart size, and element count fixed.
2. Add finer profiler scopes around `LoadChartCapacityResolver.Resolve` and `ElementAssignmentService.ComputeEvaluationResult` before changing behavior. Current profiling proves assignment is hot but does not separate geometry math from chart resolution.
3. Stop building diagnostic strings in the hot path unless diagnostics are requested. `CandidateBoomsChecked` and `RejectedCandidates` are useful for reports, but expensive during every chromosome evaluation.
4. Pre-index load chart entries by stepped radius and sorted boom groups once per run or crane spec.
5. Cache validated/normalized zone polygons for the run instead of validating them on every setup point check.
6. Avoid rebuilding manual assignment lookup per chromosome evaluation; build it once per run if assignments are present.
7. Reduce LINQ allocations in tournament selection and replacement after the main chart/assignment bottleneck is addressed.
8. Keep final diagnostics generation separate from optimization timing when measuring search-loop performance.

# High-Risk Optimizations

1. Parallelizing the GA loop before replacing shared `Random` usage is high risk. `System.Random` is not thread-safe and could corrupt determinism or produce invalid random streams.
2. Broad caching with mutable domain objects is high risk. `CraneSpecification`, `TargetPoint`, zones, and elements may be mutable; stale caches could produce unsafe lift results.
3. Rounding floating-point geometry cache keys too aggressively is high risk. It can merge distinct crane positions and change capacity or radius outcomes.
4. Removing diagnostic fields from `ElementEvaluationResult` without preserving report behavior is high risk. CSV diagnostics and UI reports rely on those fields.
5. Replacing ConservativeStep lookup semantics is high risk. Load chart lookup is safety-critical and must not become interpolated or optimistic.
6. Changing greedy set-cover behavior is high risk. It affects final setup count and coverage behavior, not just speed.
7. Reusing mutable `AssignmentResult` or `ElementEvaluationResult` objects from pools is high risk unless reset logic is exhaustive.

# Recommended Optimization Roadmap

1. Measurement phase: add profiler scopes for resolver, geometry, candidate evaluation, diagnostics string creation, and assignment summary. Capture benchmarks at 100, 500, 1000, and 5000 elements with fixed seeds and representative load charts.
2. Load chart phase: pre-index load chart radii and boom groups, then measure resolver time and allocations. Preserve ConservativeStep behavior exactly.
3. Diagnostics separation phase: split search-loop minimal evaluation from final diagnostic hydration so population scoring does not allocate large diagnostic objects and strings for every candidate.
4. Geometry cache phase: cache run-scoped element static data, setup-element radius/boom calculations, and normalized zones. Use conservative cache keys and verify with golden-master tests.
5. Assignment phase: avoid repeated assignment lookup construction and reduce temporary objects in `EvaluateBestPosition`.
6. Parallel readiness phase: replace shared random access with thread-local or deterministic per-operation random streams, verify dependency thread-safety, and isolate profiler metadata.
7. Parallel execution phase: parallelize population and child evaluation loops first. Benchmark speedup and GC behavior before considering element-level parallelism.
8. Cleanup phase: address lower-priority LINQ allocations in selection, replacement, final assignment resolution, and diagnostics writing.

Expected ROI by phase:

| Phase | Expected Improvement | Confidence |
|---|---:|---|
| Load chart indexing/caching | 2x-5x on chart-heavy runs | High |
| Diagnostics separation/allocation reduction | 1.5x-3x plus fewer Gen 0 GCs | High |
| Geometry and assignment caching | 1.5x-4x depending on grid/repeated points | Medium |
| Population-level parallelism | 3x-8x on modern CPUs | High after thread-safety fixes |
| Selection/replacement LINQ cleanup | 5%-15% at large populations | Medium |

Highest ROI sequence: measure resolver detail, index load charts, reduce diagnostic allocations, then parallelize population scoring after random/profiler/thread-safety issues are solved.

---

## Consolidated from: Phase 15 Research Findings & GA Validation Report (project/phase-findings.md)

## Executive Summary

Phase 15 implements **Reachability Intelligence & Search Space Pruning** to resolve the search space bottleneck in the Genetic Algorithm (GA) crane path optimizer.

---

## 1. Empirical Justification (Why Phase 15 is Needed)

Based on the completed `Benchmark_v1` parametric sweep (consisting of 972 GA optimization runs):

- **91.65%** of all lift failures for the `STC800` crane model are classified as `OutOfRadius` (meaning the crane physically cannot reach the precast elements from any coordinate within the allowed green zones).
- Despite exhaustive GA tuning, the `STC800` crane achieves a maximum coverage of only **21.73%** on the large `try9` project.
- GA parameter sensitivity analysis proves that the parameters `PopulationSize`, `MaxGenerations`, and `MutationRate` combined explain less than **3%** of the coverage variance.
- **Conclusion:** The allowed zones and candidate coordinate pool (the search space itself) represent the bottleneck. The GA is spending major computational cycles mutating coordinates that are geometrically incapable of reaching elements. Pre-calculating reachability before the GA runs and seeding the initial population with these "hot spots" resolves this bottleneck.

---

## 2. Expected Improvement Hypothesis

- **Centroid-Based Seeding:** Seeding chromosomes at coordinates that represent reachability centroids/hot spots is expected to improve the initial generation's average fitness by **15% to 30%** for highly constrained cranes.
- **Search Space Pruning:** By focusing early generations on areas with non-zero coverage, the GA can allocate genetic mutation and crossover operations to refine local offsets instead of searching for reachable coordinates from scratch.
- **Diversity Maintenance:** Preserving a 70% random coordinate distribution ensures that global search capability is not lost while exploiting the pre-computed reachability hot spots in the remaining 30%.
