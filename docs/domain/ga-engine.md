# Domain: Genetic Algorithm (GA) Optimization Engine

The optimization engine is responsible for finding the most effective $(X, Y)$ coordinate to position a mobile crane.

---

## 1. Chromosome Representation

The problem encodes crane placement as a 2-dimensional coordinate chromosome:
$$\text{Gene} = (X, Y)$$

* **Initialization:** A chromosome is initialized inside a randomly selected green area polygon. If the random point lies outside the polygon, the algorithm uses a loop to generate fresh coordinates until point-in-polygon containment passes.
* **Cloning:** Support deep copying coordinates for selection and crossover stages.

---

## 2. Detailed Fitness Scoring

The fitness score evaluates placement quality based on the following criteria:

$$\text{Fitness} = \sum_{e \in \text{Elements}} S_e + B_{\text{green}} - P_{\text{green}} - \sum_{o \in \text{Obstacles}} P_o - P_{\text{boundary}} + B_{\text{dist}}$$

### Scoring Components:

1. **Element Erection Score ($S_e$):**
   For each precast building element, calculate the horizontal distance $D_e$ from the crane:
   * **Reachable and Liftable:** If $D_e \in [\text{MinRadius}, \text{MaxRadius}]$ and $\text{Capacity}(D_e) \ge \text{Weight}_e$:
     $$S_e = +10.0$$
   * **Reachable but Too Heavy:** If $D_e \in [\text{MinRadius}, \text{MaxRadius}]$ but $\text{Capacity}(D_e) < \text{Weight}_e$:
     $$S_e = +2.0$$
   * **Unreachable:** If $D_e \notin [\text{MinRadius}, \text{MaxRadius}]$:
     $$S_e = -5.0$$

2. **Green Area Setup Zones:**
   * **Inside Green Area ($B_{\text{green}}$):** $+20.0$ bonus.
   * **Outside Green Area ($P_{\text{green}}$):** $-50.0$ penalty.

3. **Obstacle Clearances ($P_o$):**
   * **Overlapping an Obstacle:** $-100.0$ penalty per obstacle.

4. **Site Boundaries ($P_{\text{boundary}}$):**
   * **Outside Site Boundary:** $-200.0$ penalty.

5. **Distance Efficiency Bonus ($B_{\text{dist}}$):**
   Rewards placements centered relative to the building:
   $$B_{\text{dist}} = \max\left(0, 50.0 - \text{AvgDistance}\right)$$
   Where $\text{AvgDistance}$ is the average horizontal distance to all building elements.

---

## 3. Genetic Operators

* **Selection:** Tournament selection (default size = 3).
* **Crossover (Blend Crossover BLX-0):**
  $$X_{\text{child}} = X_{\text{parent1}} + \beta \times (X_{\text{parent2}} - X_{\text{parent1}})$$
  $$Y_{\text{child}} = Y_{\text{parent1}} + \beta \times (Y_{\text{parent2}} - Y_{\text{parent1}})$$
  Where $\beta$ is a random value selected uniformly from $[0, 1]$.
* **Mutation (Gaussian Mutation):**
  Adds a random value from a Gaussian (normal) distribution:
  $$X_{\text{new}} = X + \mathcal{N}(0, 1) \times \text{Strength}$$
  $$Y_{\text{new}} = Y + \mathcal{N}(0, 1) \times \text{Strength}$$
  If the mutated point is outside all green areas, it clamps the point to the bounding box limits of the primary green area.

---

## 4. Termination & Convergence

The optimization loop continues until:
1. **Max Generations reached** (default 500).
2. **Convergence criteria met:** The difference between the maximum and minimum best fitness values over a sliding window (default 50 generations) drops below a threshold (default 0.01):
   $$\max(\text{History}_{50}) - \min(\text{History}_{50}) < 0.01$$
