# Fitness Function - Adaptive Relocation Weighting

This document details the mathematical model and implementation of the **Adaptive Relocation Weighting** formula used in multi-stage Genetic Algorithm runs.

---

## 1. Overview

Relocation penalties represent the cost of traveling, setting up, and mobilizing cranes at different positions on a construction site. 

If this penalty is kept static and too high, multi-position chromosome setups are mathematically eliminated during selection phases of the Genetic Algorithm, even if they yield higher coverage.

To resolve this penalty death, the [MultiStageGAOrchestrator](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/MultiStageGAOrchestrator.cs) dynamically scales the relocation weight on subsequent stages using the coverage achievements of the preceding stage.

---

## 2. Mathematical Formula

The adaptive relocation weight is computed as follows:

$$\text{adaptiveWeight} = (1.0 - \text{previousCoverage}) \times \text{CoverageWeight} \times \text{AdaptiveRelocationScaleFactor}$$

### Guard Clamping
To prevent the relocation weight from dominating the coverage fitness bonus (which must remain the dominant search signal), a safety guard clamps the weight strictly below the `CoverageWeight`:

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
