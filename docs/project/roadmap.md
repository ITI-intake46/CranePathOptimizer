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
