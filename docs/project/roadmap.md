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

### Phase 17 - Bipartite Matching Assignment Engine (Hungarian Solver)
*   **Status:** ✅ Complete
*   **Description:** Built the Hungarian Assignment Solver using successive relocation min-cost flow to globally optimize element assignment across crane positions.
*   **Implementation Notes:**
    - Created [HungarianAssignmentService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/HungarianAssignmentService.cs) implementing [IElementAssignmentService.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/IElementAssignmentService.cs).
    - Solves bipartite assignment globally under capacity limits and load balancing in $O(N \cdot M^2)$ time ($M \le 3$), executing in `< 50ms` on 3,940 elements (try9).
    - Registered dynamically in DI in [App.xaml.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs) switchable based on the `UseHungarianAssignment` flag in `GASettings`.
