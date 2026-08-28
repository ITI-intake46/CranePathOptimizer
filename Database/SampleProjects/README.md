# Sample Projects & Acceptance Fixtures — CraneOptimizer
## Requirement R-06 Acceptance Fixture Package

---

This directory contains the official, deterministic sample artifacts and pre-seeded database assets required for evaluation, testing, and supervisor acceptance review.

### Sample Artifact Inventory

| Artifact File | Category / Purpose | Description |
| :--- | :--- | :--- |
| **`Sample_Crane_Catalog.db`** | SQLite Database | Pre-seeded crane catalog containing Sany STC800 and Liebherr LTM 1200-5.1. |
| **`Sample_Precast_Building.ifc`** | IFC Building Model | Valid IFC4 building file containing precast beams, columns, and slabs. |
| **`Sample_Sany_STC800_LoadChart.csv`** | Crane Load Chart | Manufacturer load chart CSV for Sany STC800 mobile crane. |
| **`Sample_Normalized_Elements.json`** | Normalized BIM Data | Parsed and unit-normalized precast element dataset ready for GA input. |
| **`Sample_GA_Placement_Result.json`** | Optimization Output | Deterministic multi-stage GA optimization result snapshot. |
| **`Sample_Project.craneopt`** | CraneOpt Saved Project | Full application saved project workspace state file. |
| **`Sample_Lifting_Report.pdf`** | Generated PDF Report | Formatted PDF lifting feasibility report. |
| **`Sample_Lifting_Report.csv`** | Generated Data Export | Tabular CSV export of element lifting capacities and safety margins. |
| **`Sample_Site_Layout.dxf`** | DXF Drawing | CAD DXF site layout boundary drawing. |
| **`README.md`** | Documentation | Inventory and usage instructions for reviewers. |

### Quick Start for Reviewers

1. Launch CraneOptimizer (`CraneOptimizer.UI.exe`).
2. Select **Open Existing Project** and browse to `Sample_Project.craneopt`.
3. To test catalog loading, import `Sample_Crane_Catalog.db` or load chart `Sample_Sany_STC800_LoadChart.csv`.
4. To test IFC parsing, open `Sample_Precast_Building.ifc`.
