# ADR-004: Use Genetic Algorithms for Crane Placement

## Status
Accepted

---

## Context
Finding the optimal setup coordinate $(X, Y)$ for a mobile crane on a construction site is a complex optimization problem. The solution space is highly non-linear, discontinuous (due to obstacles and site boundaries), and multi-objective (maximizing reached elements, minimizing distance, and respecting load capacity restrictions). Traditional analytical optimization methods cannot solve this problem efficiently, so we must select a suitable heuristic search algorithm.

---

## Decision
We chose to implement a custom **Genetic Algorithm (GA)** engine.

---

## Consequences

### Positive
* **Constraint Flexibilities:** GA allows us to easily incorporate non-smooth fitness scoring (such as discrete step penalties for obstacle overlaps and boundaries).
* **Global Search Capabilities:** By keeping a population of multiple chromosomes (default 100), GA explores multiple areas of the site simultaneously, reducing the likelihood of getting stuck in local optima.
* **Rapid Convergence:** The C# implementation runs in 2-5 seconds, making it suitable for real-time interactive tuning in the WPF viewport.
* **Simplicity:** The algorithm is easy to configure and test (crossover, mutation, and tournament selection).

### Negative
* **Heuristic Nature:** GA does not guarantee finding the mathematically absolute global optimum; it returns the best-fit candidate found within the generation budget.
* **Parameter Sensitivity:** Optimization performance is highly dependent on parameters (mutation rates, elitism count, tournament size). If poorly configured, it can converge prematurely.
