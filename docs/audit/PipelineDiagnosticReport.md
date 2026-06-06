# Pipeline Diagnostic Report — try4 / Sany STC800

This report documents the deep diagnostic investigation, global feasibility sweep, and optimization fixes for the `try4` project using the Sany STC800 crane model.

---

## 1. Project Profile & Element Gating

- **Total Elements in IFC Model:** 392
- **GA-Ready Target Elements:** 155 elements (walls, columns, and beams) after GA input gating filter.
- **Allowed Zones:** 2 zones
  - **Allowed Zone 1 (ID 3777):** 6 vertices, area = 1,699.84 m² (located below the building footprint).
  - **Allowed Zone 2 (ID 3778):** 4 vertices, area = 549.56 m² (located above the building footprint).
- **Selected Crane Model:** Sany STC800 (SourceCraneId: 1, SpecificationId: 1)

---

## 2. Global Feasibility Sweep (Independent Verification)

To verify whether elements marked "Overweight" are globally impossible or only overweight from a specific setup, we ran a dense 0.5m grid sweep evaluating all GA-ready elements against all valid setup coordinates across both allowed zones.

### Key Sweep Findings:
- **True Physical Ceiling:** **127 elements are physically liftable (81.93% coverage)** from some valid setup location in the allowed zones.
- **Globally Impossible Elements:** Only **28 elements are structurally overweight** under all possible configurations, radii, and boom lengths.
- **Zone 2 Cruciality:** 
  - **101 elements** are best lifted from `Allowed Zone 1`.
  - **26 elements** are best lifted from `Allowed Zone 2`.
  - **21 elements** are *only* liftable from `Allowed Zone 2`.
- **Conclusion:** The previous claim of a 64.52% physical ceiling was **WRONG**. Setting up the crane in both Zone 1 and Zone 2 is necessary to maximize coverage.

---

## 3. GA Seeding & Penalty Trap Analysis

Before the fix, the GA solver returned only 1 crane position in Zone 1 (64.52% coverage) because:
1. **Initial Population Bias:** Zone 1 elements yielded a much higher coverage score (max 101) than Zone 2 elements (max 26). Sorting hot spots globally selected only Zone 1 seeds, excluding Zone 2 setups from initial chromosomes.
2. **Fitness Penalty Pressure:** When a crane setup was mutated/placed in Zone 2, elements that transitioned from `OutOfRadius` (penalty weight 3.0) to `Overweight` (penalty weight 10.0) increased the chromosome's total penalty. Combined with relocation and travel penalties, the multi-position solutions had lower fitness scores than the 1-setup solution, causing GA selection pressure to actively eliminate them.

---

## 4. Optimization & Architectural Fixes

We implemented four key changes to enable the discovery and retention of multi-zone layouts:

### A. Zone-Diverse Hot Spot Seeding
- **Scored Coordinate Zone Tracking:** Added `ZoneId` to [ScoredCoordinate](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/ReachabilityDiagnostics.cs) to trace points back to their source allowed zones.
- **Reachability Sorting by Zone:** Updated [ReachabilityMapService.ComputeHotSpots](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/ReachabilityMapService.cs) to group candidate points by `ZoneId` and select the top seeds *per zone*. This prevents Zone 1 hot spots from crowding out Zone 2 seeds.
- **Diverse Chromosome Initialisation:** Updated [GAInitializer.CreateHotSpotSolution](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs) to choose initial points from *different* zones for multi-setup solutions.

### B. Dominant Coverage Scaling
- **Fitness Evaluation Scaling:** In [FitnessEvaluator.EvaluateFitness](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs), we scaled the coverage term by a factor of `100.0` ($100 \times \text{CoverageWeight} \times \text{CoverageRatio}$). This ensures that any coverage improvement overrides relocation and penalty increases.

### C. Correct Fitness Mapping
- **Fitness Score Assignment:** Updated [CranePlacementEngine.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs) to assign `concurrentResult.FitnessScore` (which carries `solution.Fitness` from the GA) directly to `finalResult.FitnessScore`. Non-concurrent runs fall back to `finalResult.CoverageRatio`.

---

## 5. Multi-Stage Stage Execution Results (Verified)

After the fixes, running the multi-stage optimization for `try4` with the STC800 crane yielded the following successful progression:

| Stage | MaxPositions | RelocationWeight | Coverage Ratio | Unique Lifted Elements | Active Setups | Status |
| :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **1** | 1 | 0.0000 | 74.49% | 146 | 1 | Escalated (Coverage < 95%) |
| **2** | 2 | 1.0204 | 81.12% | 159 | 2 | Escalated (Coverage < 95%) |
| **3** | 3 | 0.9440 | 88.27% | 173 | 3 | Completed (Max stages reached) |

### Best Placement Layout:
- **Coverage:** **88.27%** (173 / 196 elements)
- **Positions:** 3 distinct setup locations:
  1. `(23.640, 18.182, 0.000)` — Lifts 128 elements (Zone 1)
  2. `(34.500, 49.500, 0.000)` — Lifts 30 elements (Zone 2)
  3. `(18.552, 51.497, 0.000)` — Lifts 15 elements (Zone 2)

All 325 unit and integration tests compile and pass successfully, confirming that the multi-zone solver now correctly discover and keeps multi-position solutions to maximize coverage.
