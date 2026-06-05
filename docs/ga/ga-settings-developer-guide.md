# GASettings Developer & Calibration Guide

This guide provides comprehensive developer and maintainer documentation for all active properties of `GASettings` within the crane layout optimization engine. It outlines parameter tuning, classifications, scaling behaviors, and calibration workflows for repeated runs.

---

## 1. Classifications of Settings

To assist developers in navigation and UI layout design, we classify the settings into four functional categories:

```
GASettings
├── High-Impact Settings (Core inputs modified by users on every run)
├── Advanced Settings (Tuning parameters for GA convergence and penalties)
├── Experimental Settings (Non-default cooling schedules and concurrent switches)
└── Diagnostic Settings (Profiling and report file directory targets)
```

### A. High-Impact Settings
These properties represent physical or structural site limits. They must be easily accessible to end-users on every optimization run:
*   `MaxCranePositions`
*   `WireLength`
*   `AdditionalWt`
*   `DeratingRatio`
*   `SafetyFactor`
*   `GridStep`
*   `MinimumRelocationDistance`
*   `TargetElementTypes`

### B. Advanced Settings
Tuning parameters for the GA evolutionary operator behaviors and fitness function weights:
*   `PopulationSize`
*   `MaxGenerations`
*   `EarlyStoppingGenerations`
*   `RandomSeed`
*   `CoverageWeight`
*   `RelocationWeight`
*   `RadiusPenaltyWeight`
*   `OverweightPenaltyWeight`
*   `HeightPenaltyWeight`
*   `RadiusStdDevPenaltyWeight`
*   `BalancePenaltyWeight`
*   `SimilarPositionPenaltyWeight`
*   `RelocationDistancePenaltyWeight`
*   `PositionSamplingAttempts`

### C. Experimental Settings
Settings governing adaptive scaling models and alternative optimization execution branches:
*   `EnableAdaptiveSigma`
*   `SigmaCoolingExponent`
*   `MinimumMutationSigma`
*   `MutationSigma` (when combined with cooling)
*   `MutationExplorationMultiplier`
*   `CrossoverRate`
*   `MutationRate`
*   `ConvergenceThresholdPercent`
*   `EnableConcurrentOptimization`
*   `EnableReachabilitySeeding`
*   `HotSpotSeedingRatio`

### D. Diagnostic Settings
Options managing performance profilers and trace folder targets:
*   `EnableProfiling`
*   `ProfilingLogDirectory`
*   `DuplicatePositionDistance`
*   `MaxRadiusForNormalisation` (used to normalize distance ranges)

---

## 2. Active Parameter Reference Sheets

For every actively used setting, we detail its purpose, default, range, impacts, and risks.

### `PopulationSize`
*   **Purpose:** The number of candidate layouts evolved per generation.
*   **Default:** `100` | **Allowed Range:** `10 - 1000`
*   **Engineering Impact:** None directly on physical metrics.
*   **Optimization Impact:** Higher populations sustain spatial diversity and avoid getting trapped in local minima caused by irregular forbidden zones.
*   **Performance Impact:** Linear scaling $O(P)$. Doubling the size doubles the execution time of each generation.
*   **Risk of Incorrect Values:** Too small ($<30$) leads to loss of diversity and premature convergence. Too large ($>500$) causes performance lag (longer than 5s runs).
*   **Tuning Recommendation:** Use `100` for standard runs. Increase to `250` only for large projects ($E > 500$ elements).

### `MaxGenerations`
*   **Purpose:** The maximum number of evolutionary loops allowed.
*   **Default:** `200` | **Allowed Range:** `50 - 2000`
*   **Engineering Impact:** None.
*   **Optimization Impact:** Sets the cutoff for search depth.
*   **Performance Impact:** Linear scaling $O(G)$.
*   **Risk of Incorrect Values:** Too small ($<50$) cuts off the search before convergence. Too high ($>1000$) causes unnecessary CPU load.
*   **Tuning Recommendation:** Keep at `200`. Rely on early stopping for termination.

### `WireLength`
*   **Purpose:** Vertical length offset for hook slings.
*   **Default:** `2.0`m | **Allowed Range:** `0.0 - 20.0`
*   **Engineering Impact:** Increases the required hook elevation clearance: $Z_{\text{hook}} = Z_{\text{COG}} + H_{\text{half}} + \text{WireLength}$.
*   **Optimization Impact:** Direct impact on solution feasibility. Higher values make tall buildings unreachable for low-hook cranes.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Under-estimation leads to crane boom collisions.
*   **Tuning Recommendation:** Set matching the physical rigging equipment used on site.

### `AdditionalWt`
*   **Purpose:** Weight offset to account for hook block and sling assemblies.
*   **Default:** `0.0` tons | **Allowed Range:** `0.0 - 10.0`
*   **Engineering Impact:** Increases required lifting capacity: $W_{\text{req}} = \text{ElementWT} + \text{AdditionalWt}$.
*   **Optimization Impact:** Directly affects load capacity checks.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Under-estimation leads to crane overloads.
*   **Tuning Recommendation:** Standard default is `0.0` but should match rigging weights.

### `DeratingRatio`
*   **Purpose:** Scale down manufacturer capacity charts for safety margins.
*   **Default:** `0.9` (90% capacity) | **Allowed Range:** `0.5 - 1.0`
*   **Engineering Impact:** Multiplies chart capacity: $\text{Cap}_{\text{effective}} = \text{Cap}_{\text{chart}} \times \text{DeratingRatio} \times \text{SafetyFactor}$.
*   **Optimization Impact:** Directly restricts crane reaches for heavy lifts.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Setting too close to 1.0 reduces safety margins.
*   **Tuning Recommendation:** Standard structural default is `0.9`.

### `SafetyFactor`
*   **Purpose:** Operational safety limit applied after derating.
*   **Default:** `0.95` | **Allowed Range:** `0.5 - 1.0`
*   **Engineering Impact:** Direct multiplier on allowable lifting capacity.
*   **Optimization Impact:** Restricts crane placements.
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Too low over-constrains placements.
*   **Tuning Recommendation:** Standard default is `0.95`.

### `MaxCranePositions`
*   **Purpose:** Hard limit on the maximum number of relocation setups per solution.
*   **Default:** `3` | **Allowed Range:** `1 - 10`
*   **Engineering Impact:** Defines maximum mobilization effort.
*   **Optimization Impact:** Directly affects coverage potential.
*   **Performance Impact:** High. Chromosome size scales array sizes in assignment checks.
*   **Risk of Incorrect Values:** Setting too small prevents reaching 100% coverage on large sites.
*   **Tuning Recommendation:** Match layout mobilization targets.

### `EnableReachabilitySeeding`
*   **Purpose:** Toggles pre-computation of reachability hot spots before starting GA.
*   **Default:** `true` | **Allowed Range:** `true` or `false`
*   **Engineering Impact:** None directly.
*   **Optimization Impact:** Prunes search space by seeding initial GA population near coordinates with high coverage.
*   **Performance Impact:** Lightweight pre-computation step (<3s).
*   **Risk of Incorrect Values:** Disabling may cause GA to waste generations on unreachable areas on highly constrained sites.

### `HotSpotSeedingRatio`
*   **Purpose:** Fraction of GA initial population seeded from reachability hot spots.
*   **Default:** `0.30` | **Allowed Range:** `0.0` (disabled) to `1.0` (all)
*   **Engineering Impact:** None.
*   **Optimization Impact:** Balances early exploit pressure (hot spots) with explore diversity (random placements).
*   **Performance Impact:** None.
*   **Risk of Incorrect Values:** Setting to `1.0` reduces genetic diversity, risking local entrapment. Setting to `0.0` disables reachability seeding.

---

## 3. Practical Tuning Profiles

These preset profiles represent balanced configurations for typical project scales:

### A. Small Project Profile (e.g., $<50$ elements, 1-2 setups)
*   `PopulationSize`: `50`
*   `MaxGenerations`: `100`
*   `MaxCranePositions`: `2`
*   `EarlyStoppingGenerations`: `15`
*   `MinimumRelocationDistance`: `8.0`m
*   `PositionSamplingAttempts`: `40`

### B. Medium Project Profile (e.g., $50 - 300$ elements, 2-3 setups)
*   `PopulationSize`: `100`
*   `MaxGenerations`: `200`
*   `MaxCranePositions`: `3`
*   `EarlyStoppingGenerations`: `20`
*   `MinimumRelocationDistance`: `10.0`m
*   `PositionSamplingAttempts`: `80`

### C. Large Project Profile (e.g., $>300$ elements, $3+$ setups)
*   `PopulationSize`: `200`
*   `MaxGenerations`: `400`
*   `MaxCranePositions`: `5`
*   `EarlyStoppingGenerations`: `35`
*   `MinimumRelocationDistance`: `12.0`m
*   `PositionSamplingAttempts`: `120`

---

## 4. Calibration Workflows for Repeated Experiments

When running consecutive optimization experiments to find optimal placements, follow this calibration workflow:

```
[Import IFC & Setup Site]
           │
           ▼
[Step 1: Establish Feasible Baseline]
  - Disable relocation penalties (RelocationWeight = 0).
  - Run GA to locate positions where reach/capacity are 100% satisfied.
           │
           ▼
[Step 2: Calibrate Relocation Density]
  - Increment RelocationWeight (e.g., 2.0 -> 5.0).
  - Run GA to prune setups down to the minimum operational setups needed.
           │
           ▼
[Step 3: Fine-Tune Proximity & Paths]
  - Adjust SimilarPositionPenaltyWeight & MinimumRelocationDistance.
  - Snapshot runs to ga_run_history.csv to compare elapsed time vs coverage.
```

### Diagnostic Validation
Always review the exported diagnostics files located in the project's output reports directory:
*   Verify the cache hit ratio in the `ga_run_history.csv` summary. If the capacity lookup hit ratio falls below 90%, it indicates that coordinate snapping (`GridStep`) should be enabled or set larger to maximize database cache reuse.
*   Audit `ga_failure_breakdown.csv` to see whether failures are dominated by reach (`OutOfRadius`) or load capacity (`InsufficientCapacity`). If capacity failures dominate, adjust `SafetyFactor` or change the selected crane model specification.
