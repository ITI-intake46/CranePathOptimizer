# Project Scope: MVP & Exclusions

## Current MVP Scope

The Minimum Viable Product (MVP) of the **Mobile Crane Path Optimizer** focuses on single-crane, multi-setup layout planning and spatial placement optimization using genetic algorithms. The system determines the minimum number of crane setup locations required to safely lift the maximum possible number of precast elements while satisfying site constraints, crane capacity limitations, and permissible parking zones. The features included in the current implementation are:

### 1. BIM & IFC Model Processing
*   **Model Loading:** Ingestion of Industry Foundation Classes (`.ifc`) files using the xBIM library.
*   **Element Extraction:** Automatic parsing of standard building elements including columns, beams, slabs, walls, footings, and stairs.
*   **Geometry Processing:** Extraction of local and global coordinates, bounding boxes (length, width, height), and volumes from 3D shape geometries.
*   **Weight Calculation:** Estimation of structural weights using physical volumes and a default concrete density of $2500 \text{ kg/m}^3$ (with overrides for steel $7850$, aluminum $2700$, glass $2500$, timber $600$ based on element names).

### 2. Site Constraint Modeling
*   **Green Areas:** Defining 2D polygonal areas on site where it is permissible to set up the crane.
*   **Obstacles:** Defining 2D polygonal obstacle boundaries with associated heights to represent forbidden regions.
*   **Site Boundary:** Specifying the outer limits of the construction site to ensure the crane is not positioned off-property.
*   **Circle Footprint Checks:** `ConstraintEngine` checks if the crane's footprint circle is fully within allowed zones and does not overlap obstacles.

### 3. Crane Database & Specifications
*   **Crane Specifications Management:** Storing structural crane specifications including boom length, max capacity, min/max operating radii, base height, and speeds in database structures (`CraneSpecification`, `TargetPoint`).
*   **Manufacturer Load Charts:** Supporting radius-to-capacity tables using a **ConservativeStep** lookup mechanism for real-time capacity checks at any arbitrary distance.

### 4. Genetic Algorithm Placement Optimization
*   **GA Configuration:** Customizing population size, mutation rate/strength, elitism count, tournament size, and convergence parameters.
*   **Multi-Setup Placement Optimization:** Executing a genetic algorithm that identifies the minimum number of crane setup locations required to maximize coverage of precast elements across the construction site.
*   **Chromosome Representation:** Each candidate solution encodes a variable-length list of crane setup points represented by coordinates $(X,Y,Z)$.
*   **Fitness Evaluation:** Candidates are scored using normalized failure fractions and settings-defined weights.

### 5. 3D Spatial Visualization
*   **DirectX Viewport:** Using `HelixToolkit.Wpf.SharpDX` for hardware-accelerated 3D graphics.
*   **Mesh Generation:** Rendering building elements using default terracotta brand colors.
*   **Site Overlays:** Visualizing green areas as translucent green sheets, obstacles as translucent red blocks, site boundary as a yellow line, and crane capacity as a cyan circle.

### 6. SQLite-Based Project Persistence
*   **Project Files:** Storing the entire project (elements, site boundary, obstacles, green areas, crane settings, optimization parameters) into a single SQLite database file using the `.craneopt` extension.

---

## Out of Scope (Exclusions)

The following features are explicitly excluded from the current scope of the application:

*   **Crane Relocation Path Planning:** While the system determines multiple crane setup locations, it does not compute or optimize the travel route between these locations. Physical movement planning remains part of the future Path Engine.
*   **Dynamic Crane Travel Path Optimization:** Planning or simulating the physical movement path of the mobile crane across the site from one setup point to another (Path Engine).
*   **3D Lift Sequence and Clearances:** Simulating the full 3D lifting trajectory of an element from its delivery trailer to its final placement, including collision checks with existing building components (Lifting Engine).
*   **Multi-Crane Optimization:** Optimizing the placement of multiple cranes working in tandem or sharing overlapping working zones.
*   **Interactive Geometry Modification:** Editing the dimensions or shapes of imported IFC elements directly inside the application.
*   **Hoist Schedule & Time Estimation:** Simulating the time durations of slewing, hoisting, and trolley movements to calculate erection schedules (speeds are stored in the database but are not active in optimization logic).
