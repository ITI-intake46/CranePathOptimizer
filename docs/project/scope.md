# Project Scope: MVP & Exclusions

## Current MVP Scope

The Minimum Viable Product (MVP) of the **Mobile Crane Path Optimizer** focuses on single-crane layout planning and spatial placement optimization using genetic algorithms. The features included in the current implementation are:

### 1. BIM & IFC Model Processing
* **Model Loading:** Ingestion of Industry Foundation Classes (`.ifc`) files using the xBIM library.
* **Element Extraction:** Automatic parsing of standard building elements including `IIfcColumn`, `IIfcBeam`, `IIfcSlab`, `IIfcWall`, `IIfcFooting`, and `IIfcStairFlight`.
* **Geometry Processing:** Extraction of local and global coordinates, bounding boxes (length, width, height), and volumes from 3D shape geometries.
* **Weight Calculation:** Estimation of structural weights using physical volumes and a hardcoded concrete density of $2400 \text{ kg/m}^3$.

### 2. Site Constraint Modeling
* **Green Areas:** Defining 2D polygonal areas on site where it is permissible to set up the crane.
* **Obstacles:** Defining 2D polygonal obstacle boundaries with associated heights to represent existing structures or forbidden regions.
* **Site Boundary:** Specifying the outer limits of the construction site to ensure the crane is not positioned off-property.
* **Point-in-Polygon Checks:** Ray-casting algorithm implementation to check if crane coordinates lie inside boundaries, green zones, or obstacles.

### 3. Crane Database & Specifications
* **Crane Records Management:** Storing structural crane specifications including boom length, jib length, max capacity, min/max operating radii, base height, speeds, and costs.
* **Manufacturer Load Charts:** Supporting radius-to-capacity tables (`CraneLoadChartEntryRecord`) with linear interpolation for real-time capacity checks at any arbitrary distance.

### 4. Genetic Algorithm Placement Optimization
* **GA Configuration:** Customizing population size, mutation rate/strength, elitism count, tournament size, and convergence parameters.
* **Placement Optimization:** Executing a genetic algorithm that encodes crane location $(X, Y)$ and evaluates it based on:
  * Reachability of all precast elements.
  * Lifting capacity check at target radius.
  * Penalty for placing outside site boundaries or green areas.
  * Penalty for colliding with obstacles.
  * Bonus for placing inside green areas and minimizing average distance to elements.

### 5. 3D Spatial Visualization
* **DirectX Viewport:** Using `HelixToolkit.Wpf.SharpDX` for hardware-accelerated 3D graphics.
* **Mesh Generation:** Rendering building elements colored by type (e.g., columns in blue, beams in green, slabs in gray).
* **Site Overlays:** Visualizing green areas as translucent green sheets, obstacles as translucent red blocks, site boundary as a yellow line, and crane capacity as a cyan circle centered on the optimal crane marker.

### 6. SQLite-Based Project Persistence
* **Project Files:** Storing the entire project (elements, site boundary, obstacles, green areas, crane settings, optimization parameters) into a single SQLite database file using the `.craneopt` extension.

---

## Out of Scope (Exclusions)

The following features are explicitly excluded from the current scope of the application:

* **Dynamic Crane Travel Path Optimization:** Planning or simulating the physical movement path of the mobile crane across the site from one setup point to another (Path Engine).
* **3D Lift Sequence and Clearances:** Simulating the full 3D lifting trajectory of an element from its delivery trailer to its final placement, including collision checks with existing building components (Lifting Engine).
* **Multi-Crane Optimization:** Optimizing the placement of multiple cranes working in tandem or sharing overlapping working zones.
* **Interactive Geometry Modification:** Editing the dimensions or shapes of imported IFC elements directly inside the application.
* **Hoist Schedule & Time Estimation:** Simulating the time durations of slewing, hoisting, and trolley movements to calculate erection schedules (speeds are stored in the database but are not active in optimization logic).
