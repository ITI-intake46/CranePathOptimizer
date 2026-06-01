# Future Roadmap

This document outlines the planned future features and architecture enhancements for the **Mobile Crane Path Optimizer** beyond the current MVP scope.

---

## Short-Term Roadmap (1-3 Months)

### 1. Lifting Sequencer & Dynamic Safety Calculations
* **Erection Sequencing:** Utilize the `SortOrder` property on `ElementRecord` to evaluate crane capacity step-by-step as the building is erected. 
* **Dynamic Obstacle Updates:** Treat already-erected building elements as dynamic 3D obstacles for subsequent lifts, ensuring boom-to-structure and line-to-structure collision avoidance.

### 2. Economic & Schedule Optimization
* **Erection Time Simulations:** Integrate crane velocities (`HoistSpeed`, `SlewSpeed`, `TrolleySpeed`) to calculate travel time for each lift.
* **Cost-Benefit Analysis:** Use `OwnershipCost` (rental rate/hour) and estimated erection duration to evaluate placement configurations not only on reachability but also on cost-efficiency.

### 3. Reporting and Export Capabilities
* **PDF Report Generation:** Generate PDF reports containing diagnostic logs, optimal coordinates, load chart compliance graphs, and 3D viewport snapshots.
* **CSV Exporting:** Support exporting element lists and optimized coordinates to CSV format for site surveyors.

---

## Medium-Term Roadmap (3-6 Months)

### 4. Path Engine (Crane Travel Optimization)
* **Travel Grid Mapping:** Subdivide the construction site boundary into a cell grid, classifying cells as traversable (green zones, flat areas) or untraversable (obstacles, uneven terrain, erected elements).
* **Pathfinding Algorithms:** Implement A* or Dijkstra's algorithm to plan the mobile crane's travel path between setup zones, considering the crane's width and turning radius (e.g., using a hybrid A* or kinematic path solver).

### 5. Lifting Trajectory Engine
* **3D Collision Checking:** Build full 3D collision detection for the hook, rigging, and load during hoisting.
* **Dynamic Clearances:** Calculate minimum clearance distances between the crane boom, counterweight, and surrounding structures during rotation.

### 6. Multi-Crane Optimization
* **Cooperative Placement:** Extend the Genetic Algorithm to place $N$ cranes simultaneously, maximizing coverage while avoiding collisions between overlapping booms.
* **Shared Loading Zones:** Coordinate pickup points (delivery truck routes) shared between multiple cranes.

---

## Long-Term Roadmap (6+ Months)

### 7. Cloud Integration & Web Dashboard
* **Cloud Solving:** Offload intensive genetic algorithm optimizations to cloud workers for faster convergence or large-scale multi-crane setups.
* **BIM Collaborate Integration:** Sync optimization results directly back to Autodesk Construction Cloud or other common data environments (CDE).

### 8. Augmented Reality (AR) On-Site Verification
* **AR Visualization:** Export optimal placement coordinates and working radius spheres to mobile AR devices, allowing site supervisors to visualize crane positions directly on the physical site.
