# Phase 15 Research Findings & GA Validation Report

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
