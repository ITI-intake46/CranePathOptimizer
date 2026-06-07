# Genetic Algorithm Configuration Settings

This document details the configuration parameters and scaling properties defined in [GASettings.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs).

---

## 1. New Properties (Phase 16 & 17)

The following properties were introduced to support multi-stage discovery and Hungarian global assignment:

### `EnableMultiStageDiscovery`
- **Type:** `bool`
- **Default Value:** `true`
- **Description:** Enables the multi-stage escalation loop. When `false`, the system runs a single GA optimization pass using the base setting for `MaxCranePositions`.

### `MultiStageCoverageTarget`
- **Type:** `double`
- **Default Value:** `0.95` (95%)
- **Description:** The target coverage threshold (0.0 to 1.0) above which the multi-stage loop terminates early. If Stage 1 achieves $\ge 95\%$ coverage, Stage 2 and 3 are skipped.

### `AdaptiveRelocationScaleFactor`
- **Type:** `double`
- **Default Value:** `0.40`
- **Description:** Scaling multiplier used in the adaptive weight formula to calculate the relocation penalty for multi-position setups.

### `UseHungarianAssignment`
- **Type:** `bool`
- **Default Value:** `false`
- **Description:** Enforces the use of the globally optimal bipartite matching assignment engine (`HungarianAssignmentService`) instead of the legacy greedy solver (`ElementAssignmentService`). Defaults to `false` to preserve reproducibility of baseline benchmarks.

---

## 2. Cloning Settings

### `Clone()`
- **Return Type:** [GASettings](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs)
- **Description:** Performs a shallow copy of the setting instance using `MemberwiseClone()`.
- **Rationale:** During the multi-stage loop, the orchestrator needs to modify settings (like `MaxCranePositions` and `RelocationWeight`) independently for each stage run. Using a shallow copy prevents side effects on the user-selected base settings.

```csharp
public GASettings Clone()
{
    return (GASettings)this.MemberwiseClone();
}
```
