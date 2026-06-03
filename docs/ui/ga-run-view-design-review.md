# F8_GA Run View UI Design Review - Final Calibration Layout

This document details the final approved UI design review and layout structure for the F8_GA Run View. All parameters reside inside the **Advanced Settings SideWindow**, with ordering organized strictly by GA calibration importance and expected tuning frequency during solver experiments.

---

## 1. Advanced Settings SideWindow Layout

The SideWindow presents settings in a single scrollable view structured into six collapsible/logical sections ordered from top to bottom based on GA calibration workflow priority:

```
Advanced Settings SideWindow
    ├── 1. Core Calibration (Primary solver & genetic search controls)
    ├── 2. Fitness Strategy (Objective weight coefficients)
    ├── 3. Layout Constraints (Spatial placement constraints)
    ├── 4. Engineering Model (Rigging & safety margins)
    ├── 5. Advanced GA (Hyperparameters & search modes)
    └── 6. Diagnostics (Offsets, logging, & normalization)
```

---

## 2. Final Approved Ordering Inventory

### A. Core Calibration
The primary controls used to tune search speed, convergence rate, and coordinate diversity between runs:
*   **`PopulationSize`** (Number of candidate layouts evolved per generation)
*   **`MaxGenerations`** (The absolute evolutionary generation limit)
*   **`MutationRate`** (Probability of mutating an individual's crane positions)
*   **`MutationSigma`** (Standard deviation for Gaussian coordinate drift mutation)
*   **`MaxCranePositions`** (Max setups allowed in a single chromosome)
*   **`EarlyStoppingGenerations`** (Generations of flat best-fitness tolerated before stopping)
*   **`ConvergenceThresholdPercent`** (Relative fitness improvement % required to count as progress)

---

### B. Fitness Strategy
Weight coefficients adjusted to balance optimization objectives:
*   **`CoverageWeight`** (Multiplier for unique liftable element ratio)
*   **`RelocationWeight`** (Multiplier for setup relocation count penalty)
*   **`RadiusPenaltyWeight`** (Multiplier for out-of-reach element penalty)
*   **`RadiusQualityPenaltyWeight`** (Multiplier for average operating radius ratio)
*   **`BalancePenaltyWeight`** (Multiplier for lift count variance among setups)
*   **`RadiusStdDevPenaltyWeight`** (Multiplier for operating radius standard deviation)
*   **`SimilarPositionPenaltyWeight`** (Multiplier for setups placed too close to one another)
*   **`RelocationDistancePenaltyWeight`** (Multiplier for total movement distance between sequential setups)

---

### C. Layout Constraints
Physical layout spacing and inclusion boundaries:
*   **`MinimumRelocationDistance`** (Minimum distance to treat setups as separate locations)
*   **`GridStep`** (Snapping step for crane coordinates in meters)
*   **`TargetElementTypes`** (Allow-list for element categories in optimization)

---

### D. Engineering Model
Physical rigging constants and capacity limits (configured once per project rather than adjusted between calibration runs):
*   **`WireLength`** (Rigging rope height clearance offset)
*   **`AdditionalWt`** (Rigging block/shackle weight offset)
*   **`SafetyFactor`** (User capacity safety margin applied to load charts)
*   **`DeratingRatio`** (Structural capacity derating applied to load charts)

---

### E. Advanced GA
Hyperparameters, sampling limits, and alternative optimization loops tuned during developer experiments:
*   **`RandomSeed`** (Deterministic run validation seed)
*   **`CrossoverRate`** (Crossover probability between parent pairs)
*   **`MutationExplorationMultiplier`** (Multiplier applied to Gaussian mutations)
*   **`EnableAdaptiveSigma`** (Toggles generation-dependent cooling schedules)
*   **`SigmaCoolingExponent`** (Decay exponent for adaptive sigma cooling)
*   **`MinimumMutationSigma`** (Cooling sigma floor in meters)
*   **`PositionSamplingAttempts`** (Rejection-sampling limit for valid coordinate placement)
*   **`EnableConcurrentOptimization`** (Toggles concurrent multi-setup GA vs greedy set-cover iterations)

---

### F. Diagnostics
Offsets, normalizers, and logging variables positioned at the bottom of the SideWindow:
*   **Offsets:** `SlewOffsetX/Y/Z`, `BoomHingeOffsetX/Y/Z`
*   **Normalization:** `MaxRadiusForNormalisation`, `CraneFootprintRadius`, `DuplicatePositionDistance`
*   **Profiling:** `EnableProfiling`, `ProfilingLogDirectory`, `RiggingLength` (Legacy alias, hidden)
