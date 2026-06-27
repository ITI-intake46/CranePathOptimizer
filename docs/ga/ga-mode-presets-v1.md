# Genetic Algorithm Mode Presets V1 — Calibration & Implementation Trail

This document details the design, calibration, and implementation trail of the GA optimization mode presets introduced in Phase 6.

---

## 1. Why These Modes Exist

In the Mobile Crane Path Optimizer project, the genetic algorithm (GA) searches for the optimal sequence of crane setup positions and the corresponding lift assignments for precast elements. Previously, the user had to manually adjust over 30 individual genetic algorithm tuning parameters (population size, max generations, mutation/crossover rates, and a complex matrix of fitness function weights). 

To make the system accessible to non-expert users while preserving the ability for experts to tune the parameters manually, three user-friendly preset modes were introduced:
1. **Balanced Coverage:** A general-purpose preset optimized for maximum element coverage within a reasonable execution time.
2. **Minimum Relocation:** A preset designed to minimize site mobilization costs by restricting the optimization to a single crane setup position.
3. **Crane-Friendly:** A preset that allows for more setup positions to minimize crane stress by ensuring elements are lifted at shorter radii.

---

## 2. The Calibration Experiment

The presets were calibrated using a rigorous cross-crane sweep on the `auditmodel` project.

### Experiment Details:
* **Target Project:** `auditmodel`
* **Target Cranes:**
  * `STC800` (80-ton mobile crane; lower capacity, shorter reach)
  * `STC1300C` (125-ton mobile crane; higher capacity, longer reach)
* **Parameter Matrix:** 9 distinct parameter sets (B01–B03, R01–R03, C01–C03) targeting different GA configurations.
* **Seeds Evaluated:** 3 seeds per configuration (`1`, `42`, `99`) to ensure statistical stability.
* **Total Runs:** 54 clean, independent runs (27 per crane).

---

## 3. Calibrated Configurations

From the 54-run matrix, three clear winning parameter sets were identified:

### 1. Balanced Coverage &rarr; **B02**
* **Calibrated Setup Count:** Exactly 2 setup positions.
* **Tuning Values:** Population Size = `60`, Max Generations = `80`, Max Crane Positions = `2`.
* **Performance:**
  * `STC800` Coverage: **64.44%** (runtime: ~8.5s)
  * `STC1300C` Coverage: **63.42%** (runtime: ~9.3s)
* **Design Rationale:** Provides the best compromise between runtime and coverage, yielding consistent results across both cranes.

### 2. Minimum Relocation &rarr; **R01**
* **Calibrated Setup Count:** Exactly 1 setup position.
* **Tuning Values:** Population Size = `30`, Max Generations = `40`, Max Crane Positions = `1`.
* **Performance:**
  * `STC800` Coverage: **53.33%** (runtime: ~1.7s)
  * `STC1300C` Coverage: **54.36%** (runtime: ~4.1s)
* **Design Rationale:** Guarantees zero crane relocations on site. Because relocation is restricted at the structural search level via `MaxCranePositions = 1`, the relocation weight remains at the calibrated baseline of `5.0`.

### 3. Crane-Friendly &rarr; **C02**
* **Calibrated Setup Count:** Up to 3 setup positions.
* **Tuning Values:** Population Size = `60`, Max Generations = `80`, Max Crane Positions = `3`.
* **Performance:**
  * `STC800` Coverage: **70.26%** (runtime: ~32.5s)
  * `STC1300C` Coverage: **62.39%** (runtime: ~5.5s)
* **Design Rationale:** Encourages the algorithm to find shorter working radii per lift (mean radius of **14.57m** on STC800 vs **15.03m** in other modes), lowering overall load stress on the boom.

---

## 4. Implemented Preset Mapping

The parameters below are applied by `GAModePresetFactory.cs`. Any parameters not listed here are treated as physical/safety constraints (e.g. `SafetyFactor`, `GridStep`, `LoadChartConstraints`) and are **always preserved** from the user's manual UI inputs.

| Parameter / Weight | Base Default | Balanced (B02) | Min Relocation (R01) | Crane-Friendly (C02) | Source (CSV Column) |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **PopulationSize** | `30` (UI fallback) | **60** | **30** | **60** | `PopulationSize` |
| **MaxGenerations** | `30` (UI fallback) | **80** | **40** | **80** | `MaxGenerations` |
| **MaxCranePositions** | `3` | **2** | **1** | **3** | `MaxCranePositions` |
| **MutationSigma** | `0.05` | **0.005** | **0.005** | **0.005** | `MutationSigma` |
| **MutationRate** | `0.05` | **0.10** | **0.10** | **0.10** | `MutationRate` |
| **CrossoverRate** | `0.70` | **0.80** | **0.80** | **0.80** | `CrossoverRate` |
| **UseHungarianAssignment** | `false` | **true** | **true** | **true** | `UseHungarianAssignment` |
| **EnableMultiStageDiscovery** | `false` | **true** | **true** | **true** | `EnableMultiStageDiscovery` |
| **RelocationWeight** | `5.0` | **5.0** | **5.0** | **5.0** | `RelocationWeight` |
| **RadiusPenaltyWeight** | `3.0` | **3.0** | **3.0** | **3.0** | `RadiusPenaltyWeight` |
| **OverweightPenaltyWeight** | `10.0` | **10.0** | **10.0** | **10.0** | `OverweightPenaltyWeight` |
| **HeightPenaltyWeight** | `5.0` | **5.0** | **5.0** | **5.0** | `HeightPenaltyWeight` |

---

## 5. Limitations & Caveats

* **Over-parameterization on Large Cranes:** 
  The calibration data shows that when optimization is run with large capacity cranes (like the `STC1300C`), allowing up to 3 crane setup positions (`C02`) actually introduces search-space overhead, which occasionally results in *lower* coverage than the 2-position setup (`B02`) due to genetic drift.
* **Rigid Preset Overrides:** 
  Selecting a preset currently overrides the GA tuning fields globally. If a user wants to slightly tweak a preset (e.g. increase population size to `100` for a Balanced run), they must switch to the `None / Custom` mode and manually input all parameters.

---

## 6. How to Validate or Recalibrate

If structural constraints, cranes, or the fitness function design change in the future, the presets should be validated or recalibrated:

1. **Rerun Calibration Batch:**
   Execute the `CraneOptimizer.GAExperiment` sweep using:
   ```powershell
   dotnet run --project CraneOptimizer.GAExperiment --mode calibrate --crane STC800
   ```
2. **Audit Output Folder:**
   Analyze the generated `ga_audit_settings_snapshot.csv` and `ga_audit_run_summary.csv` inside:
   `DiagnosticsFinal\auditmodel\mode_calibration_<yyyyMMdd_HHmmss>\`
3. **Compare Candidates:**
   Identify the winning configurations that achieve the best balance of coverage, setup count, and runtime for each of the three modes.
4. **Update Factory:**
   If the winning values deviate from the V1 presets, update the constants in `GAModePresetFactory.cs` and run the unit tests:
   ```powershell
   dotnet test --filter "GAModePresetFactoryTests"
   ```
