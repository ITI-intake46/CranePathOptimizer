# Fitness Function - Adaptive Relocation Weighting

This document details the mathematical model and implementation of the **Adaptive Relocation Weighting** formula used in multi-stage Genetic Algorithm runs.

---

## 1. Overview

Relocation penalties represent the cost of traveling, setting up, and mobilizing cranes at different positions on a construction site. 

If this penalty is kept static and too high, multi-position chromosome setups are mathematically eliminated during selection phases of the Genetic Algorithm, even if they yield higher coverage.

To resolve this penalty death, the [MultiStageGAOrchestrator](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/MultiStageGAOrchestrator.cs) dynamically scales the relocation weight on subsequent stages using the coverage achievements of the preceding stage.

---

## 2. Mathematical Formula

The overall fitness value of a solution is computed using the following weighted penalty deduction formula:

$$\text{fitness} = \text{CoverageWeight} \times \text{CoverageDominanceMultiplier} \times \text{coverageRatio} - \text{Penalties}$$

Where the penalties are:

$$\text{Penalties} = \text{relocationPenaltyVal} + \text{RadiusPenaltyWeight} \times \text{outOfRadiusFraction} + \dots$$

> [!NOTE]
> **Coverage Scaling:** The coverage term is scaled by `CoverageDominanceMultiplier` (default `100.0`). This ensures that coverage improvements (the dominant signal) always override other penalties (like relocation, std-dev, and balance penalties) when coverage increases.

### Coverage vs Fitness Score Separation

| Metric | Scope | Range | Description |
|---|---|---|---|
| `CoverageRatio` | User-facing | 0.0 – 1.0 | Fraction of elements lifted (e.g. 0.75 = 75%) |
| `CoveragePercent` | User-facing | 0 – 100 | `CoverageRatio × 100` (e.g. 75%) |
| `FitnessScore` | Internal GA | ≥ 0 (may be >> 1) | Raw GA score used for selection pressure. Scaled by `CoverageWeight × CoverageDominanceMultiplier`, and may be much larger than 1.0. Not comparable across different settings or runs. |

**UI Rule:** The result panel must display Coverage % (CoveragePercent) and lifted element count. Raw `FitnessScore` may be shown in diagnostic sections but must be labeled **GA Score** to prevent confusion with coverage percentage.

### Adaptive Relocation Weighting
The adaptive relocation weight is computed as follows:

$$\text{adaptiveWeight} = (1.0 - \text{previousCoverage}) \times \text{CoverageWeight} \times \text{AdaptiveRelocationScaleFactor}$$

### Guard Clamping
To prevent the relocation weight from dominating the coverage fitness bonus, a safety guard clamps the weight strictly below the `CoverageWeight`:

$$\text{adaptiveWeight} = \min(\text{adaptiveWeight}, \text{CoverageWeight} \times 0.99)$$

---

## 3. Worked Examples

### Example 1: Stage 2 Weight with Moderate Coverage in Stage 1
- **Settings:**
  - `CoverageWeight = 10.0`
  - `AdaptiveRelocationScaleFactor = 0.40`
- **Previous Stage Coverage:** `0.70` (70% elements lifted)
- **Calculation:**
  $$\text{adaptiveWeight} = (1.0 - 0.70) \times 10.0 \times 0.40 = 0.30 \times 4.0 = 1.20$$
- **Outcome:** The relocation weight is set to `1.20` for Stage 2. This represents a lightweight penalty allowing 2-position solutions with $>82\%$ coverage to easily beat a 1-position solution with 70% coverage.

### Example 2: Stage 3 Weight with High Coverage in Stage 2
- **Settings:**
  - `CoverageWeight = 10.0`
  - `AdaptiveRelocationScaleFactor = 0.40`
- **Previous Stage Coverage:** `0.92` (92% elements lifted)
- **Calculation:**
  $$\text{adaptiveWeight} = (1.0 - 0.92) \times 10.0 \times 0.40 = 0.08 \times 4.0 = 0.32$$
- **Outcome:** The relocation weight is set to `0.32` for Stage 3. Because the coverage gap is small, a very small relocation penalty is used to allow 3-position setups to compete only if they achieve near-perfect coverage.

### Example 3: Guard Clamp Triggering
- **Settings:**
  - `CoverageWeight = 10.0`
  - `AdaptiveRelocationScaleFactor = 1.50` (Extreme setup)
- **Previous Stage Coverage:** `0.10` (10% elements lifted)
- **Calculation:**
  $$\text{adaptiveWeight} = (1.0 - 0.10) \times 10.0 \times 1.50 = 0.90 \times 15.0 = 13.50$$
- **Guard Clamping Check:**
  Since $13.50 \ge 10.0$, the guard condition is met:
  $$\text{adaptiveWeight} = 10.0 \times 0.99 = 9.90$$
- **Outcome:** The relocation weight is clamped to `9.90` to guarantee that coverage remains the dominant signal.

---

## 4. Benchmark_v2 Exposure

The diagnostics extension does not change this fitness function. It records coverage separately from raw fitness and exposes stage gain, position count, relocation distance, runtime, and crane-stress aggregates in `ga_solution_quality_metrics.csv`.

Balanced Coverage and Minimum Relocation are future analysis labels, not implemented runtime modes. No thresholds or alternate fitness weights were introduced. See [Benchmark_v2 diagnostics](../ga/benchmark-v2-diagnostics.md).
