# Genetic Algorithm Design Decisions

This document details the architectural decisions and trade-offs behind selecting and structuring the Genetic Algorithm (GA) framework for crane placement and layout optimization.

---

## 1. Rationale for Selecting Genetic Algorithms

In crane placement optimization, the search space is highly non-linear, discontinuous, and constrained:
*   **Permissible Areas:** Crane setups must lie fully within irregular, polygonal "Allowed" zones.
*   **Obstacles:** Setups must completely avoid "Forbidden" zones (building footprints, utilities, power lines). This creates "holes" or voids in the coordinate space.
*   **Discrete Catalogs:** The choice of crane model/specification is discrete (e.g., jib length, counterweight configurations).
*   **Multi-setup Sequences:** A single solution consists of a variable number of relocations (1 to $N$ setups), where the coordination between setups is critical.

### Alternatives Evaluated

| Method | Suitability | Rationale for Rejection / Adoption |
| :--- | :--- | :--- |
| **Brute-Force Grid Search** | Poor | Scalability is $O(G^k)$ where $G$ is grid resolution and $k$ is setup count. Becomes computationally infeasible for large sites with $k > 2$. |
| **Linear / Mixed-Integer Programming (MIP)** | Poor | The load chart lookup is highly non-linear, non-monotonic, and discrete. Site boundaries are non-convex polygons. MIP cannot model these constraints without severe, unsafe approximations. |
| **Particle Swarm Optimization (PSO)** | Moderate | High performance in continuous spaces. However, the presence of discrete crane models and non-convex forbidden zones causes particles to get stuck in local minima or drift outside boundaries frequently, requiring expensive boundary correction schemes. |
| **Ant Colony Optimization (ACO)** | Moderate | Excellent for discrete path routing, but maps poorly to continuous 2D/3D coordinate selections for stationary crane setups. |
| **Genetic Algorithm (GA)** | **Excellent** | **Adopted.** Handles mixed-integer/discrete-continuous variables (crane configurations + coordinates) seamlessly. Tolerates discontinuous, highly non-convex search spaces. Population-based diversity prevents premature convergence on isolated local optima. |

---

## 2. Key Genetic Operator Design Decisions

### A. Chromosome Representation
*   **Decision:** Variable-length sequence of coordinates, represented by [CraneChromosome.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/CraneChromosome.cs).
*   **Rationale:** The chromosome maps directly to the physical relocation sequence of a mobile crane:
    $$\text{Chromosome} = \langle (X_1, Y_1, Z_1), (X_2, Y_2, Z_2), \dots, (X_n, Y_n, Z_n) \rangle$$
    This matches the problem description where a crane relocates to multiple setups to cover precast erections.

### B. Selection Strategy
*   **Decision:** Tournament selection with tournament size $k = 3$, implemented in [TournamentSelection.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs).
*   **Rationale:** Maintains strong selection pressure to drive convergence while preserving diversity. Ties are resolved by evaluating coverage ratio first, and then favoring solutions with fewer crane setups (minimizing relocation costs).

### C. Crossover Strategy
*   **Decision:** Uniform coordinate crossover per index, implemented in [CrossoverOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs).
*   **Rationale:** Standard single-point crossover can be highly disruptive to spatial layouts. Uniform crossover swaps discrete crane setups (genes) as complete units, preserving successful setup locations. Proximity checks prevent offspring from retaining near-duplicate setups (setups closer than $2 \times \text{CraneFootprintRadius}$).

### D. Mutation Strategy
*   **Decision:** Three-way probability split (80% Perturbation / 10% Addition / 10% Removal), implemented in [MutationOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs).
*   **Rationale:**
    *   **Position Perturbation (80%):** Applies Gaussian noise to adjust coordinates slightly. Snaps to grid if `GridStep > 0`. If `EnableAdaptiveSigma` is active, the mutation scale cools down over generations to fine-tune placements.
    *   **Setup Addition (10%):** Adds a new setup coordinate inside allowed zones. Allows the solver to explore whether adding a setup improves overall coverage.
    *   **Setup Removal (10%):** Deletes a setup coordinate. Prevents solutions from retaining redundant, low-contribution setups.

### E. Fitness Evaluation Strategy
*   **Decision:** Multi-objective weighted deduction formula with fast-path optimization overloads, implemented in [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs) and [ElementAssignmentService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs).
*   **Rationale:**
    *   **Safety-first constraints:** Physical reach, height, and load limits are treated as severe, soft-weighted penalties in the fitness formula. This guides the population toward valid configurations without disconnecting the search space.
    *   **Hot-path compilation:** To achieve the required 2-5 second runtime, the fitness evaluation uses a fast-path overload during the generation loop. This bypasses structural diagnostics reporting and utilizes pre-computed element COGs/weights (`ElementStaticCache`) and pre-sorted load charts (`LoadChartIndex` with binary search lookup). Full diagnostics and coordinate survey translations are computed only once for the final best solution.
