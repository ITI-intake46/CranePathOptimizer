# Project Vision: Mobile Crane Path Optimizer for Precast Buildings

## Purpose
The **Mobile Crane Path Optimizer for Precast Buildings** is a specialized engineering and decision-support tool designed for construction planners, site managers, and structural engineers. The primary purpose of this system is to automate and optimize the spatial placement of heavy mobile cranes on construction sites for erecting precast concrete buildings.

By leveraging building data directly from Building Information Modeling (BIM) files, specifically the Industry Foundation Classes (IFC) format, the system bridge the gap between design and construction logistics. It solves the mathematically complex problem of finding the safest, most efficient crane erection positions to reach and lift precast elements (columns, beams, slabs, walls) while respecting site constraints, load capacities, and obstacle clearances.

---

## Core Problems Solved

1. **Manual and Error-Prone Planning:** Traditionally, site planners determine crane placement using 2D drawings, paper load charts, and manual measurements. This process is time-consuming and prone to human calculation errors, which can result in costly on-site delays or structural failures.
2. **Complex Spatial Interactions:** Precast construction requires lifting elements with high weight and varying dimensions over existing structures, green zones, and site boundaries. Finding a single crane position that satisfies all load charts and clearances is a multi-objective optimization problem that is highly challenging to solve manually.
3. **Safety and Capacity Verification:** Verification of whether a crane can safely lift a specific piece at a certain radius requires cross-referencing manufacturer load charts. The system automates this check, ensuring no lift exceeds safe operating limits.

---

## High-Level Goals

* **Automated BIM Ingestion:** Directly parse IFC models to automatically extract precast element dimensions, locations, volumes, and weights, eliminating manual data entry.
* **Intelligent Placement Optimization:** Utilize advanced metaheuristics (Genetic Algorithms) to compute mathematically optimal crane coordinates on site.
* **3D Interactive Visualization:** Provide an interactive 3D scene (using DirectX-accelerated viewports) for engineers to inspect the building model, site obstacles, green areas, crane radius boundaries, and placement results.
* **Safety First Design:** Incorporate physical crane capacity charts, minimum/maximum radii, obstacle heights, and boundary constraints directly into the fitness calculation of the optimization engine.
* **Reliable Persistence:** Store project data and optimization runs in an efficient, self-contained file format to support collaborative planning.
* **Minimum Setup, Maximum Coverage:** Determine the smallest possible number of crane positioning points that enable the lifting of the largest number of precast elements. The optimization process minimizes crane relocations, balances lifting demands across positions, avoids excessive stress on crane safety limits, and automatically excludes prohibited parking locations such as green areas and other restricted site zones.
