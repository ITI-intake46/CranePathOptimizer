# Pathfinding & Relocation Engine (Travel Routes)

This document outlines the travel route planning, spatial grid mapping, and turning radius kinematic constraints modeled for crane relocation movements across construction sites.

---

## 1. Spatial Grid Mapping

The site layout is discretized into traversable grid cells for path planning:
- **TraversableGridCell Struct:**
  ```csharp
  public class TraversableGridCell
  {
      public double X { get; set; }
      public double Y { get; set; }
      public bool IsTraversable { get; set; }
      public double MaxBearingCapacity { get; set; } // Soil bearing support metric
      public double GroundSlope { get; set; }
  }
  ```
- **Obstacle Pruning:** Cells overlapping obstacle polygons or forbidden zones are marked non-traversable.
- **Slope & Bearing Enforcement:** Cells with ground slopes exceeding maximum travel limits or soil bearing capacities below the crane's weight are pruned.

---

## 2. A* Pathfinding

The pathfinding system uses the A* search algorithm:
1. **Grid Generation:** Subdivides the site boundary into grid cells matching the configured resolution.
2. **Shortest Path Search:** Searches from the start coordinate to the target solver setup position.
3. **A* Cost Function:** Minimizes travel distance and slope changes:
   $$f(n) = g(n) + h(n)$$
   where $g(n)$ is the actual travel cost from the start to cell $n$, and $h(n)$ is the Euclidean distance heuristic to the target.

---

## 3. Kinematic Constraints & Dubins Curves

Heavy mobile cranes require wide turning clearances and cannot change headings instantly:
- **Dubins Curves:** The travel paths are smoothed using Dubins paths (combinations of straight lines and circular arcs of maximum curvature) to match the crane's physical chassis turning radius.
- **Clearance Sweep:** Sweeps the crane width along the Dubins path to verify zero collision with site obstacles.
