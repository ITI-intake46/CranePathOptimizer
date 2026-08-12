# CraneOpt

An engineering desktop application for optimizing mobile crane setup locations for precast building erection using IFC-based BIM data and constraint-aware Genetic Algorithm optimization.

---

## Table of Contents

- [Overview](#overview)
- [Problem Statement](#problem-statement)
- [Solution](#solution)
- [Key Capabilities](#key-capabilities)
- [Engineering Workflow](#engineering-workflow)
- [Input Data](#input-data)
- [Crane Feasibility Model](#crane-feasibility-model)
- [Genetic Algorithm Optimization](#genetic-algorithm-optimization)
- [Fitness Function](#fitness-function)
- [Constraint Handling](#constraint-handling)
- [IFC Processing Architecture](#ifc-processing-architecture)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)
- [Database and Project Files](#database-and-project-files)
- [Performance Engineering](#performance-engineering)
- [User Configuration](#user-configuration)
- [Installation](#installation)
- [Build and Run](#build-and-run)
- [Testing](#testing)
- [Limitations and Assumptions](#limitations-and-assumptions)
- [Roadmap / Future Work](#roadmap--future-work)
- [Development Guidelines](#development-guidelines)
- [Documentation](#documentation)
- [License](#license)
- [Project Status](#project-status)

---

## Overview

CraneOpt is a Windows desktop application for crane planning engineers and construction professionals working on precast concrete building erection projects. It addresses the problem of determining where a mobile crane should be positioned on a construction site and how many setup locations are required to lift the maximum possible number of precast structural elements while respecting site constraints, crane load-chart capacities, and safety requirements.

The application ingests a structural BIM model in IFC format, extracts the precast elements with their geometry and weight data, allows the user to define the site layout (allowed zones, obstacles), selects and configures a crane with its load chart, and then runs a Genetic Algorithm to find an optimized set of crane setup positions. Results are displayed in a 3D viewport with per-element lift feasibility coloring and can be exported to PDF or CSV reports.

CraneOpt is designed to make systematic crane planning practical for projects of realistic scale: buildings with hundreds to thousands of precast elements across multi-story structures.

---

## Problem Statement

Positioning a mobile crane for precast building erection is a multi-constraint engineering problem.

A crane can only reach elements within its maximum operating radius. Capacity at any given radius is governed by the manufacturer's load chart, a two-dimensional table of (radius, boom length) pairs mapping to allowable lifting weights. As the radius increases, the allowable capacity decreases non-linearly. Hook height adds a third dimension: the crane must have sufficient boom length to elevate the hook above the element's top surface plus rigging clearance.

On a real construction site, the crane cannot be placed anywhere. Portions of the site are occupied by existing structures, access roads, temporary facilities, or property boundaries. Allowed areas are often irregular polygons. The crane itself has a footprint (outrigger spread) that must be fully contained within an allowed area and must not overlap any obstacle.

A large precast building may have several hundred to several thousand individual elements distributed across a plan area that no single crane position can cover. Multiple crane setup locations are necessary, but each relocation incurs cost, time, and risk. The goal is to find the minimum number of relocations that still allows the maximum fraction of elements to be lifted.

Solving this manually is time-consuming and error-prone. Exhaustive search is computationally intractable for realistic element counts and site geometries. A nearest-point or greedy heuristic typically produces suboptimal results because it does not consider the interaction between multiple setup positions, capacity degradation with radius, or the non-linear load-chart surface.

---

## Solution

CraneOpt automates crane setup planning using a pipeline that connects BIM geometry, engineering constraints, and stochastic optimization:

```
IFC file on disk
  |
  v  IfcModelSession
     Opens model with xBIM, builds geometry context
  |
  v  IfcReaderRepository
     Extracts elements: type, material, COG, bounding box, volume, weight
  |
  v  IfcNormalizationDecisionService
     Classifies each element: role and weight trust level
  |
  v  Element Management UI
     User reviews elements, toggles inclusion flags
  |
  v  Site constraints definition
     User draws or imports allowed zones and forbidden zones
  |
  v  Crane catalog
     User selects a crane specification with load chart
  |
  v  OptimizationScopeEngine
     Assembles the active optimization target set
  |
  v  ReachabilityMapService
     Pre-computes element reachability hot spots across the allowed grid
  |
  v  MultiStageGAOrchestrator
     Runs GA stages: 1-position, 2-position, up to MaxCranePositions
     Each stage:
       GAInitializer    - seeds population
       GAEngine         - evolves population across MaxGenerations
         LiftingCalculationEngine   - computes radius, lift height, required weight
         LoadChartCapacityResolver  - resolves capacity using conservative step lookup
         ElementAssignmentService   - assigns each element to the best feasible position
         FitnessEvaluator           - scores each candidate solution
         TournamentSelection -> CrossoverOperator -> MutationOperator -> ReplacementStrategy
  |
  v  Best solution selected across all stages
  |
  v  3D viewport - elements colored by lift status per position
  |
  v  Reports - PDF / CSV export of lift feasibility results
```

---

## Key Capabilities

The following capabilities are implemented in the current codebase.

**BIM / IFC Import**

- Import of `.ifc` files (IFC2x3 and IFC4) using the xBIM library
- Automatic element type resolution (columns, beams, slabs, walls, footings, stairs, assemblies, proxies)
- Role classification: liftable element, lift assembly, auxiliary, excluded, or review-required
- Geometry extraction: transformed bounding box, center of gravity (COG), volume
- Weight extraction: explicit IFC property/quantity values preferred; volume-derived estimation with material density as fallback for concrete, steel, aluminum, timber, and glass
- Weight trust-level tracking: Explicit, EstimatedVolumeDerived, EstimatedFallback (blocked from GA)
- Assembly hierarchy handling: parent assemblies de-duplicated from their child parts to prevent weight double-counting
- IFC georeferencing extraction: projected CRS (`IfcProjectedCRS`) and map conversion (`IfcMapConversion`) for IFC4; informational latitude/longitude from `IfcSite` for IFC2x3

**Site Constraint Modeling**

- Allowed zone definition as 2D polygonal footprints (zones where the crane may set up)
- Forbidden zone definition as 2D polygonal footprints (obstacles, exclusion areas)
- DXF file import for zone geometry
- Crane footprint circle containment check: the crane's outrigger footprint must fit entirely within at least one allowed zone and must not overlap any forbidden zone
- Minimum separation distance enforcement between crane setup positions

**Crane Catalog and Load Chart**

- Crane catalog database: make, model, max capacity, min/max radius, min/max boom length
- Load chart storage: radius x boom length capacity table per crane specification
- PDF load chart import with preview/review workflow (PdfPig and PDFtoImage)
- Load chart CSV import
- Load chart operating setup selection: line-count and operating condition constraints forwarded to the capacity resolver at optimization time
- Conservative step lookup: actual radius and boom length are mapped to the next valid chart entry (no extrapolation or unsafe interpolation)
- Capacity derating ratio and safety factor applied to all load chart lookups

**Genetic Algorithm Optimization**

- Multi-stage orchestration: GA runs sequentially for 1, 2, N crane positions; stages stop early when a coverage target is reached
- Variable-length chromosome: each individual encodes a list of (X, Y, Z) crane setup coordinates
- Population initialization: hot-spot seeded from reachability pre-computation plus random inside allowed zones
- Tournament selection (size 3) with deterministic tie-breaking
- Index-aligned crossover with duplicate detection
- Gaussian position perturbation mutation (80%) + setup addition (10%) + setup removal (10%)
- Optional adaptive sigma cooling schedule for mutation strength
- Grid snapping of generated and mutated positions
- Boundary projection: mutated positions outside allowed zones are projected to the nearest zone boundary
- Early stopping on fitness stagnation
- Parallel fitness evaluation within each generation using `Parallel.ForEach`
- Optional Hungarian bipartite matching assignment (configurable via `UseHungarianAssignment`)
- Post-generation solution sanitization: invalid or near-duplicate positions removed after every crossover and mutation

**Coverage and Feasibility Analysis**

- Per-element lift feasibility evaluation: Liftable, OutOfRadius, Overweight, HeightFail, NotCharted, SpecialCase
- Unique element coverage counting (duplicate coverage across positions detected and penalized)
- Per-position liftable element assignment tracking
- Reachability diagnostics

**Project Persistence**

- Project saved as a `.craneopt` file (SQLite database)
- Stores: project metadata, IFC file references, extracted elements, site zones, crane snapshots, load chart snapshots, optimization run records, coordinate reference data, viewport state
- Separate application catalog database (`crane.db`) stores the crane and load chart catalog

**3D Visualization**

- WPF 3D viewport powered by HelixToolkit.Wpf
- Element meshes built from xBIM geometry with in-place material override for status coloring
- Geometry mesh caching (keyed by shape hash and session) to avoid re-tessellation
- WPF `Freezable` objects frozen after construction for thread safety and rendering performance
- Zone overlays: allowed zones (translucent green), forbidden zones (translucent red)
- Crane radius circle overlay, selected setup point marker
- Object snap and measurement tools in the viewport

**Diagnostics and Reporting**

- PDF reports (QuestPDF)
- CSV diagnostics output at configurable levels: Off, Summary, Research
- GA profiling: high-resolution timing across engine phases and generation-level logs
- IFC audit and normalization diagnostic reports
- Optimization history CSV logger

---

## Engineering Workflow

### 1. Create or Open a Project

The user creates a new project or opens an existing `.craneopt` file. A project is the persistent context that holds all data for a single crane planning task.

### 2. Import an IFC Model

The user selects an `.ifc` file and chooses the coordinate reference mode (Local Only or IFC Origin Mapping). The system opens the file with xBIM, builds the geometry context (`Xbim3DModelContext`), extracts all physical elements, resolves their types and roles, extracts geometry (COG, bounding box, volume), and attempts to read weights from IFC property sets. Elements with missing or unreliable weight data are flagged and blocked from optimization.

Elements are displayed in the 3D viewport as a shaded mesh model.

### 3. Review Building Elements

The element management panel displays every extracted element with its IFC type, resolved role, weight, weight source, volume, dimensions, and eligibility status. The user can toggle individual elements in or out of the optimization scope. Elements with `WeightTrustLevel = EstimatedFallback_NotAllowedForGA` cannot be included in optimization regardless of the toggle state.

### 4. Define Allowed Zones

The user draws or imports (from DXF) polygonal zones representing areas of the site where the crane is permitted to set up. All GA candidate positions must be fully contained within at least one allowed zone, accounting for the crane's footprint radius.

### 5. Define Forbidden Zones

The user draws or imports polygonal zones representing obstacles, existing structures, access roads, or any areas where the crane cannot operate. These are treated as 2D footprint exclusions projecting infinitely upward.

### 6. Select and Configure the Crane

The user selects a crane make and specification from the catalog. The specification includes max/min radius, max/min boom length, maximum capacity, and outrigger configuration. The associated load chart (radius x boom to capacity table) is loaded.

### 7. Configure the Load Chart

The user optionally imports a load chart from a manufacturer PDF or CSV file, reviews the extracted table in a preview dialog, and saves it to the crane specification. An operating condition constraint (line count, condition label) can be applied to restrict which chart columns the optimizer uses.

### 8. Configure Optimization Parameters

The user adjusts GA settings: population size, maximum generations, mutation rate, crossover rate, maximum crane setup positions, and the set of fitness weights. Engineering parameters including safety factor, derating ratio, wire length, and additional rigging weight are also configured here.

### 9. Run Optimization

The user initiates the optimization. The system runs on a background thread. The multi-stage orchestrator executes one GA stage per position count (1, 2, up to MaxCranePositions). Within each stage, the engine evolves the population and reports progress. Optimization can be cancelled at any time.

### 10. Review Results

On completion, the 3D viewport colors each element by its lift status for each crane position:

- Green: Liftable
- Yellow: OutOfRadius
- Red: Overweight
- Orange: HeightFail
- Gray: NotCharted or excluded

The results panel shows the selected crane setup coordinates, coverage ratio, per-position element counts, and the fitness breakdown.

### 11. Export Reports

The user can export a PDF summary report or a CSV diagnostics file containing the full element-level lift feasibility results.

### 12. Save the Project

The project is saved to the `.craneopt` file, capturing the current element set, zone geometry, crane configuration, optimization settings, and the best optimization result.

---

## Input Data

### Building Model

CraneOpt accepts `.ifc` files exported from structural BIM authoring tools including Tekla Structures, Autodesk Revit (via standard IFC export), and any IFC2x3 or IFC4 compliant exporter. The system detects the exporter profile automatically (Tekla, Revit, or Generic) and applies profile-specific property name aliases to improve weight and identity extraction.

For each element, the system attempts to extract:

| Field | Source |
|---|---|
| GlobalId (ElementID) | IFC GlobalId |
| Element type | IFC entity type + property set classification |
| Storey / level | IfcBuildingStorey containment |
| Material | IFC material associations |
| Dimensions (L, W, H) | Property sets or geometry bounding box |
| Volume | IFC base quantities (NetVolume, GrossVolume) or geometry |
| Weight | IFC property sets (Mass, Weight, or equivalent aliases) |
| Center of Gravity | xBIM geometry centroid |
| Bounding box | xBIM shape instance AABB |
| Assembly mark | IFC property sets (multi-source with fallback chain) |

**Weight trust levels.** The extraction pipeline assigns each element one of three trust levels:

- `Explicit` — weight was read directly from an IFC property or quantity. This element is eligible for GA optimization.
- `EstimatedVolumeDerived` — weight was computed from IFC-supplied volume and a material density heuristic (concrete: 2500 kg/m3, steel: 7850 kg/m3, aluminum: 2700 kg/m3, timber: 600 kg/m3, glass: 2500 kg/m3). This element is eligible for GA optimization.
- `EstimatedFallback_NotAllowedForGA` — weight could not be reliably estimated. This element is **blocked from GA optimization** and cannot be overridden by the user. It is still displayed in the element list and 3D viewport.

When extraction warnings occur (missing weight, ambiguous identity, property name conflicts), they are recorded in the element's warning collections and surfaced in the element management panel.

### Site Constraints

| Constraint | Representation |
|---|---|
| Allowed zones | Named 2D polygons in local model coordinates (meters) |
| Forbidden zones | Named 2D polygons (obstacles, no-go areas) |
| Crane footprint | Circle of radius `CraneFootprintRadius` centered on the setup point |

Allowed and forbidden zones can be defined by drawing directly in the viewport or by importing from a DXF file. Zone vertices are stored in local model coordinates (meters, relative to the IFC model's local origin).

A crane setup position is only valid if its footprint circle is fully inside at least one allowed zone and does not overlap any forbidden zone. This check is performed by `ConstraintEngine.IsValidSetupPoint` using point-in-polygon and circle-polygon intersection tests.

### Crane Data

The crane catalog stores:

| Field | Description |
|---|---|
| Make / Model | Crane manufacturer and model name |
| MaxCapacity | Maximum rated capacity (metric tons) |
| MinRadius / MaxRadius | Minimum and maximum operating radius (meters) |
| MinBoomLength / MaxBoomLength | Minimum and maximum boom length (meters) |
| LoadChart | Collection of (Radius, BoomLength, Capacity) entries |
| DeratingRatio | Optional capacity multiplier applied to all chart values (0 to 1) |
| OutriggerConfig | Outrigger configuration descriptor |

The load chart defines crane capacity as a function of both operating radius and boom length. This two-dimensional lookup is required because boom length determines both the reach geometry (hook height) and the rated capacity (longer booms reduce allowable loads).

---

## Crane Feasibility Model

For each (crane position, precast element) pair, the following calculations are performed by `LiftingCalculationEngine` and `LoadChartCapacityResolver`.

### Horizontal Reach

The operating radius is the 2D horizontal distance from the crane's slew center to the element's center of gravity:

```
R = sqrt((slewX - COG_X)^2 + (slewY - COG_Y)^2)
```

where `slewX = cranePosition.X + SlewOffsetX` and `slewY = cranePosition.Y + SlewOffsetY`. The slew offset accounts for the horizontal distance between the crane base reference point and the actual slew center. This is a strictly 2D calculation; terrain slope is not modeled.

### Lift Height

The required hook height is the vertical distance from the crane base elevation to the top of the element, plus rigging allowance:

```
HookHeight = max(0, elementCOG_Z + elementHeight/2 - cranePosition_Z) + WireLength
```

`WireLength` (default 2.0 m) represents the combined vertical height of the rigging assembly (slings, hooks, spreader bar).

### Required Lifting Weight

The total load applied to the hook includes the element weight and any additional rigging weight:

```
RequiredWeight = ElementWT + AdditionalWt
```

`AdditionalWt` (default 0.0 t) accounts for hook block, shackles, and accessories.

### Load Chart Capacity Resolution

The `LoadChartCapacityResolver` uses a **Conservative Step** lookup:

1. If the required radius exceeds the chart's maximum radius, the element is classified `OutOfRadius`.
2. If the required hook height exceeds the chart's maximum boom length, the element is classified `HeightFail`.
3. The resolver selects the smallest chart radius that is greater than or equal to the required radius. It never interpolates down to a larger-capacity row.
4. Within that chart radius, it selects the smallest boom length that is greater than or equal to the required hook height. Again conservative.
5. When multiple entries share the same boom length (for example repeated visual columns from a PDF import), the policy `ConservativeMinimumAcrossDuplicateVisualColumns` applies the minimum capacity from the group.
6. The resolved capacity is then adjusted: `EffectiveCapacity = ChartCapacity x DeratingRatio x SafetyFactor`.
7. If `EffectiveCapacity >= RequiredWeight`, the element is `Liftable`. Otherwise it is `Overweight`.
8. If the load chart contains no entry covering the computed radius and boom length combination, the element is classified `NotCharted` or `SpecialCase`.

This approach guarantees that the optimizer never uses optimistic capacities derived from interpolation. An element is considered liftable only when a chart entry explicitly covers its reach requirements at the derated capacity.

---

## Genetic Algorithm Optimization

### Why a Genetic Algorithm

The crane setup placement problem is a combinatorial spatial optimization problem. The search space is continuous (any point within the allowed zones), multi-objective (maximize coverage, minimize relocations), and non-convex (feasible regions are arbitrary polygons, the load chart surface is non-smooth). Classical mathematical programming methods are not directly applicable at this scale. A Genetic Algorithm is well-suited because it explores the search space without requiring gradients, handles arbitrary constraint geometries natively, and scales to realistic problem sizes.

### Chromosome Representation

Each individual is a `CraneSolution` containing a `CraneChromosome`. The chromosome encodes a variable-length list of `DomainPoint3D` crane setup positions. The list length can range from 1 to `MaxCranePositions` (default 3). This variable-length representation allows the GA to simultaneously optimize both the number and the locations of crane setups.

### Multi-Stage Orchestration

The `MultiStageGAOrchestrator` runs separate GA stages for 1, 2, up to `MaxCranePositions` maximum positions in sequence. At the end of each stage, if the best solution's coverage ratio exceeds `MultiStageCoverageTarget` (default 0.95), the orchestration stops early. This prevents adding unnecessary relocations when fewer positions already achieve sufficient coverage. The relocation penalty weight is scaled adaptively between stages.

### Population Initialization

The `GAInitializer` produces the initial population using two strategies:

When reachability seeding is enabled (`EnableReachabilitySeeding = true`):
- A configurable fraction (`HotSpotSeedingRatio`, default 30%) of the population is seeded from positions with high precomputed reachability scores.
- The remaining fraction is seeded from random positions inside the allowed zones.

Without reachability seeding (legacy mode):
- 30% of individuals are seeded near centroids of element clusters (heuristic).
- 70% are seeded from purely random positions inside the allowed zones.

All seed positions are snapped to a grid if `GridStep > 0`.

### Selection

Tournament selection with tournament size 3 is used. Three individuals are sampled from the population without replacement; the winner is the individual with the highest fitness score. Ties are broken deterministically by coverage ratio first, then by preferring fewer crane positions.

### Crossover

The `CrossoverOperator` creates a child by iterating position-by-position over the longer parent's chromosome. At each index, if both parents have a position at that index, one is selected uniformly at random; otherwise the available parent's position is inherited. Positions that are too close to already-inherited positions (within `2 x CraneFootprintRadius`) are skipped to prevent near-duplicate entries in the child.

### Mutation

The `MutationOperator` applies one of three operations per mutation event:

- **Position perturbation** (80% probability): Selects one position at random and adds Gaussian noise with standard deviation `sigma`. If the perturbed point is outside the allowed zones, it is projected to the nearest zone boundary.
- **Setup addition** (10% probability): Generates a new valid position and adds it to the chromosome, subject to the `MaxCranePositions` limit.
- **Setup removal** (10% probability): Removes one randomly selected position, subject to a minimum of one remaining position.

The mutation sigma can optionally follow an adaptive cooling schedule (`EnableAdaptiveSigma`): sigma decays from a high initial value toward a floor (`MinimumMutationSigma`) as the generation count increases.

### Solution Sanitization

After every crossover and every mutation, `GAEngine.SanitizeSolution` filters the chromosome through `ConstraintEngine.FilterDistinctValidPoints`. This removes any positions that are invalid (outside allowed zones, inside forbidden zones), violate the footprint constraint, or are too close to other positions in the same chromosome (within `DuplicatePositionDistance`). If all positions are removed, a fallback position is injected to keep the individual evaluable.

### Termination

The GA terminates when `MaxGenerations` is reached, or when `EarlyStoppingGenerations` consecutive generations pass without sufficient fitness improvement (controlled by `ConvergenceThresholdPercent`).

---

## Fitness Function

The fitness function is implemented in `FitnessEvaluator.EvaluateFitness`. The objective is to maximize element coverage while penalizing avoidable relocations, unreachable elements, capacity failures, height failures, and inefficient use of working radius.

**Hard constraint.** Any solution that cannot lift a single element is assigned fitness = 0 immediately.

**Fitness formula.** All terms except coverage are penalized (subtracted). The final score is clamped at zero.

```
fitness =
    CoverageWeight x CoverageDominanceMultiplier x coverageRatio
  - RelocationWeight x (craneCount - 1)
  - RadiusPenaltyWeight x outOfRadiusFraction
  - OverweightPenaltyWeight x overweightFraction
  - HeightPenaltyWeight x heightFailFraction
  - SpecialCasePenaltyWeight x specialCaseFraction
  - RadiusQualityPenaltyWeight x avgWorkingRadiusQuality
  - BalancePenaltyWeight x liftableVariance
  - RadiusStdDevPenaltyWeight x normalizedRadiusStdDev
  - SimilarPositionPenaltyWeight x similarityPenalty
  - RelocationDistancePenaltyWeight x normalizedDistance
```

The coverage term is the dominant signal. `CoverageWeight x CoverageDominanceMultiplier` is designed to be substantially larger than any combination of penalty terms so that a solution lifting more elements always outranks one lifting fewer, regardless of other characteristics.

Failure fractions are normalized by total element count to be project-size-independent:

- `outOfRadiusFraction = outOfRadiusCount / totalElements`
- `overweightFraction = overweightCount / totalElements`
- `heightFailFraction = heightFailCount / totalElements`

The balance penalty penalizes unequal distribution of liftable elements across crane positions. Computed as normalized variance of per-position liftable counts, using only confirmed `Liftable` assignments.

The similar position penalty penalizes solutions where two or more crane positions are close enough to each other to be operationally indistinct.

If multiple crane positions can lift the same element, only one count is credited toward `coverageRatio`. Redundant coverage is penalized implicitly through balance and radius quality terms.

---

## Constraint Handling

Constraints are enforced at two levels.

**Hard placement constraints** (a position is rejected outright):

| Constraint | Enforcement point |
|---|---|
| Candidate not inside any allowed zone | `ConstraintEngine.IsValidSetupPoint` |
| Candidate footprint overlaps a forbidden zone | `ConstraintEngine.IsValidSetupPoint` |
| Two positions within `DuplicatePositionDistance` | `FilterDistinctValidPoints` |
| Position count exceeds `MaxCranePositions` | Mutation operator (setup addition) |

Hard constraints are checked after every crossover, after every mutation, and before fitness evaluation. Invalid positions are removed and replaced with fallbacks rather than being penalized.

**Soft fitness penalties** (a feasible position is penalized):

| Violation | Penalty term |
|---|---|
| Element out of radius | RadiusPenaltyWeight x outOfRadiusFraction |
| Element overweight at resolved radius | OverweightPenaltyWeight x overweightFraction |
| Required hook height exceeds boom limit | HeightPenaltyWeight x heightFailFraction |
| No chart data for radius/boom combination | SpecialCasePenaltyWeight x specialCaseFraction |
| Multiple relocations | RelocationWeight x (count minus 1) |
| Near-duplicate setup positions | SimilarPositionPenaltyWeight |
| High total relocation travel distance | RelocationDistancePenaltyWeight |
| Unequal lifting load across cranes | BalancePenaltyWeight |
| Large or variable working radii | RadiusQualityPenaltyWeight + RadiusStdDevPenaltyWeight |

---

## IFC Processing Architecture

CraneOpt uses the xBIM Toolkit for all IFC file operations.

**Why xBIM.** xBIM is an open-source .NET library specifically designed for IFC file processing. It provides a native C# API for parsing IFC schema entities, geometry computation via a native C++ engine (`Xbim.Geometry.Engine64.dll`) accessed through a managed interop layer, support for both IFC2x3 and IFC4, and in-memory model querying without requiring a full BIM authoring tool installation.

**Windows/native dependency.** `Xbim.Geometry.Engine64.dll` is a native x64 Windows DLL. The Infrastructure and UI projects target `net8.0-windows` and require x64 build configuration. Cross-platform deployment is not supported.

**Processing pipeline:**

`IfcModelSession.Open()` performs the complete model setup:

1. Opens the `.ifc` file using `IfcStore.Open()`, tracking memory before and after.
2. Creates the xBIM geometry context (`Xbim3DModelContext`) to build triangulated mesh representations for all shape instances.
3. Records open duration, context creation duration, and shape instance count for diagnostics.

`IfcReaderRepository` performs element extraction from the open session:

1. Iterates over all `IIfcProduct` entities in the model.
2. Applies the active `IIfcExporterProfile` (Tekla, Revit, or Generic) to resolve software-specific property name aliases.
3. Extracts: IFC type, GlobalId, name, mark, storey, material, dimensions, volume, weight, COG from shape geometry, and bounding box.
4. Records extraction warnings for missing or ambiguous fields.

`IfcNormalizationDecisionService` classifies each extracted element:

- Assigns `IfcElementRole` (LiftableElement, LiftAssembly, Auxiliary, Excluded, ReviewRequired).
- Assigns `IfcWeightTrustLevel` (Explicit, EstimatedVolumeDerived, EstimatedFallback_NotAllowedForGA).
- Assigns `IncludeInGa` flag.
- Records the normalization decision reason for audit trail.

`IfcGeoreferencingExtractor` reads coordinate reference system data:

- For IFC4 files: extracts `IfcProjectedCRS` name and `IfcMapConversion` (Eastings, Northings, OrthogonalHeight, scale, rotation).
- For IFC2x3 files: reads `IfcSite` latitude/longitude as informational decimal degrees.

The extracted georeferencing data is persisted in the `.craneopt` project database and used only for display-layer coordinate readout formatting. All geometry, optimization, and constraint calculations remain in the IFC model's local coordinate space (meters from local origin).

---

## Architecture

CraneOpt follows a Clean Architecture (Onion Architecture) structure with strict dependency direction: outer layers may depend on inner layers, but inner layers may never reference outer layers.

```
CraneOptimizer.Domain          (innermost, no external dependencies)
        ^
CraneOptimizer.Application     (references Domain)
        ^
CraneOptimizer.Infrastructure  (references Domain and Application;
                                 implements repositories, EF Core DbContexts,
                                 xBIM geometry processing, PDF reports)
        ^
CraneOptimizer.UI              (references all layers; WPF entry point, DI composition root)

CraneOptimizer.AI              (references Domain; AI companion layer; referenced by UI)
```

Additionally:

- `CraneOptimizer.GAExperiment` — standalone console runner for offline GA parameter calibration; references Domain, Application, Infrastructure.
- `CraneOptimizer.Application.Tests` — test project; references all layers.

### Domain

`CraneOptimizer.Domain` contains:

- Core entities: `Crane`, `CraneSpecification`, `TargetPoint` (load chart entry), `Project`, `ProjectIfcFile`
- Schemas: `PrecastElement`, `GreenAreaZone`, associated enums
- Domain interfaces: `ICraneRepository`, `IIfcReaderRepository`, `IProjectRepository`, `IZoneGeometryService`, `IIfcNormalizationDecisionService`, `IIfcDiagnosticAuditService`, and others
- Project persistence schemas (CreatedProject subtree): entity classes for the `.craneopt` database
- Common types: `DomainPoint3D`, unit system definitions
- Diagnostics types: audit result structures

The Domain layer has no NuGet package references. It does not know about EF Core, xBIM, WPF, or any infrastructure concern.

### Application

`CraneOptimizer.Application` contains:

- The complete GA implementation: `GAEngine`, `GAInitializer`, `FitnessEvaluator`, `ElementAssignmentService`, `LiftingCalculationEngine`, `LoadChartCapacityResolver`, `ConstraintEngine`, `CoverageEngine`, `CraneConfigurationEngine`, `CranePlacementEngine`
- GA operators: `TournamentSelection`, `CrossoverOperator`, `MutationOperator`, `ReplacementStrategy`
- GA services: `MultiStageGAOrchestrator`, `LoadChartIndex`, `ElementStaticCache`, `ReachabilityMapService`, `HungarianAssignmentService`
- `GASettings` with all configurable GA and engineering parameters
- Report builders and services (application-layer report assembly)
- GA analysis and diagnostics services
- Application-level interfaces for all optimization components

### Infrastructure

`CraneOptimizer.Infrastructure` contains:

- `CraneDbContext` (EF Core) for the application catalog database
- `CreatedProjectDbContext` (EF Core) for the per-project `.craneopt` database
- EF Core migrations for both databases
- Repository implementations: `CraneRepository`, `IfcReaderRepository`, `ProjectRepository`
- IFC processing: `IfcModelSession`, `IfcModelSessionService`, `IfcGeoreferencingExtractor`
- Normalization and audit: `IfcNormalizationDecisionService`, `IfcDiagnosticAuditService`, `CentralIfcPropertyAliasResolver`
- Report exporters: PDF (QuestPDF), CSV

Infrastructure targets `net8.0-windows` and the native xBIM geometry engine is copied to the output directory as part of the build.

### UI

`CraneOptimizer.UI` contains:

- `App.xaml.cs` as the DI composition root; registers all services, repositories, ViewModels, and configures EF Core
- WPF Views (XAML) and ViewModels (C#) organized under `Features/` by functional area
- Features: IFC viewer (F2), crane setup (F3), load chart import (F4), element management (F5), zones (F7), GA optimizer (F8), project management (F10), DXF import (F11), reports (F12), coordinate reference (F18), object snap (F15)
- `Shell/` with main window and main ViewModel
- `Core/` with MVVM base classes, `RelayCommand`, converters, `SceneRenderingManager`
- `Resources/` with WPF resource dictionaries and the design system (colors, brushes, styles, typography, themes)
- 3D rendering: `ElementVisualBuilder`, `ElementMaterialProvider`, `SceneRenderingManager`, zone geometry overlays, selected point markers

---

## Project Structure

```
CraneOpt/
|-- CraneOptimizer.Domain/                 # Domain entities, schemas, interfaces
|   |-- Common/                            # DomainPoint3D
|   |-- CreatedProject/                    # .craneopt database entity classes
|   |-- Diagnostics/                       # Audit and normalization diagnostics types
|   |-- Entities/                          # Crane, CraneSpecification, TargetPoint, Project
|   |-- Interfaces/                        # Repository and service contracts
|   |-- Schemas/                           # PrecastElement, GreenAreaZone, enums
|   |-- Services/                          # ZoneGeometryService
|   +-- Units/                             # Unit system and conversion
|
|-- CraneOptimizer.Application/            # Optimization engines and application services
|   |-- Optimization/GeneticAlgorithms/
|   |   |-- Engines/                       # GAEngine, FitnessEvaluator, ConstraintEngine, etc.
|   |   |-- Operators/                     # Selection, Crossover, Mutation, Replacement
|   |   |-- Services/                      # LoadChartCapacityResolver, MultiStageGAOrchestrator, etc.
|   |   |-- Models/                        # CraneSolution, CraneChromosome, FitnessBreakdown, etc.
|   |   |-- Settings/                      # GASettings
|   |   +-- Interfaces/                    # All optimization component contracts
|   |-- Optimization/Utilities/            # GAProfiler, SpatialMath
|   |-- Reports/                           # Report builders and services
|   +-- Services/                          # Identity resolver, coordinate formatting, etc.
|
|-- CraneOptimizer.Infrastructure/         # EF Core, xBIM geometry, repositories, reports
|   |-- Contexts/                          # CraneDbContext, CreatedProjectDbContext
|   |-- Ifc/                               # IfcModelSession, IfcModelSessionService
|   |-- Migrations/                        # EF Core migration history
|   |-- Repositories/                      # IfcReaderRepository, CraneRepository, etc.
|   +-- Reports/                           # PDF (QuestPDF), CSV, user-facing exporters
|
|-- CraneOptimizer.UI/                     # WPF application (entry point)
|   |-- App.xaml / App.xaml.cs             # DI composition root
|   |-- Shell/                             # MainWindow, MainViewModel
|   |-- Features/                          # Feature areas F2 through F18
|   |-- Core/                              # MVVM infrastructure, SceneRenderingManager
|   +-- Resources/                         # Design system, themes, icons
|
|-- CraneOptimizer.AI/                     # AI companion module (UI-integrated assistant)
|
|-- CraneOptimizer.GAExperiment/           # Offline GA calibration console runner
|
|-- CraneOptimizer.Application.Tests/      # Unit and integration tests (xUnit)
|   |-- GA/                                # GA engine, operators, fitness, constraint tests
|   |-- LoadChart/                         # Capacity resolver, PDF import tests
|   |-- IFC/                               # Normalization, filtering, georeferencing tests
|   +-- ...
|
+-- docs/                                  # Developer documentation
    |-- architecture/                      # System overview, rendering, database docs
    |-- decisions/                         # Architecture Decision Records ADR-001 to ADR-005
    |-- domain/                            # Crane engine, lifting engine, IFC, units docs
    |-- ga/                                # GA design, settings, fitness, diagnostics docs
    |-- coding-standards/                  # Clean code, MVVM, DI, naming, DTO rules
    +-- project/                           # Scope, limitations, roadmap, vision
```

---

## Technology Stack

| Category | Technology | Notes |
|---|---|---|
| Language | C# 12 | Nullable reference types and implicit usings enabled |
| Runtime | .NET 8 | `net8.0` (Domain, Application) and `net8.0-windows` (Infrastructure, UI) |
| UI Framework | WPF (Windows Presentation Foundation) | `net8.0-windows` |
| Architecture | Clean Architecture + MVVM | Onion dependency direction strictly enforced |
| BIM / IFC | xBIM Toolkit 6.0.587 (Essentials, Ifc) + 5.1.820 (Geometry, ModelGeometry.Scene) | IFC2x3 and IFC4 |
| IFC Geometry Engine | Xbim.Geometry.Engine64.dll | Native x64 Windows DLL |
| 3D Rendering | HelixToolkit.Wpf 3.1.2 | WPF-based 3D viewport |
| Database (catalog) | SQLite via EF Core 9.0.14 | `crane.db` — cranes, load charts, project index |
| Database (project) | SQLite via EF Core 9.0.14 | `.craneopt` — per-project data |
| ORM | Entity Framework Core 9.0.14 | Code-first migrations |
| Dependency Injection | Microsoft.Extensions.DependencyInjection 9.0.14 | Composition root in App.xaml.cs |
| PDF Report Generation | QuestPDF 2026.6.0 | Lift result PDF reports |
| PDF Load Chart Import | PdfPig 0.1.14 | Text and table extraction from PDF load charts |
| PDF to Image | PDFtoImage 5.2.0 | Page rendering for load chart visual preview |
| DXF Import | netDxf 2023.11.10 | Zone geometry import from DXF files |
| Configuration | Microsoft.Extensions.Configuration 9.0.13 | appsettings.json |
| Testing | xUnit 2.9.2 | Unit and integration tests |
| Test Coverage | coverlet.collector 6.0.2 | Coverage instrumentation |

---

## Database and Project Files

### Application Catalog Database

The application catalog database (`crane.db`) is a SQLite file located at `<OutputDirectory>/Data/crane.db`. It is managed by `CraneDbContext` and persists:

- **Cranes** — crane make and model records
- **CraneSpecifications** — technical specifications (capacity, radii, boom limits, derating, outrigger config, load chart storage mode)
- **TargetPoints** — individual load chart entries (radius, boom length, capacity, visual column metadata)
- **Projects** — project index entries (name, folder path, created/modified dates, favorite flag, soft-delete flag)
- **ProjectIfcFiles** — IFC file references associated with each project

The catalog database is shared across all projects and is initialized automatically at first run.

### `.craneopt` Project Files

Each project is stored as a single SQLite file with the `.craneopt` extension, located in the project's folder on disk. This database is managed by `CreatedProjectDbContext` and contains everything needed to fully restore a project session:

| Table | Content |
|---|---|
| ProjectInfo | Project name, description, creation date, concrete density setting |
| IfcFiles | IFC file path and file name records for the project |
| IfcElements | Extracted precast elements with all engineering fields |
| SiteZones | Allowed zones and forbidden zones (vertices, type, DXF source metadata) |
| CraneSetups | Crane positions (selected/saved setup points) |
| CraneSnapshots | Snapshot of the crane specification used in the project |
| CraneLoadChartSnapshots | Snapshot of the load chart associated with the crane snapshot |
| OptimizationRuns | Records of optimization run metadata |
| ViewportStates | Last-known camera and viewport state |
| CoordinateReferences | Survey coordinate reference mapping (mode, offsets, CRS metadata) |
| ModelFiles | Supplementary model file references |

The `.craneopt` format is self-contained. Moving the file to another machine (with the same `.ifc` file available at the referenced path) fully restores the project state.

---

## Performance Engineering

The following performance strategies are implemented in the current codebase.

**Load chart pre-indexing.** Before the GA generation loop begins, `LoadChartIndex` builds a pre-sorted, pre-indexed representation of the crane's load chart keyed by radius and boom length. This eliminates repeated LINQ filtering on the raw load chart collection during the hot path of per-element capacity resolution.

**Element static cache.** Before the GA loop, `ElementStaticCache` pre-computes per-element derived values (required weight, half-height, COG coordinates). These are stored as value-type structs in a dictionary keyed by ElementId. This avoids repeated null-coalescing, arithmetic, and property access during fitness evaluation.

**Parallel fitness evaluation.** Within each generation, population fitness evaluation is parallelized using `Parallel.ForEach`. Each individual's `EvaluateSolution` call (element assignment + fitness scoring) runs concurrently.

**Reachability pre-computation.** Before the GA starts, `ReachabilityMapService` optionally scores all candidate grid positions in the allowed zones by how many elements they can reach (considering radius only). The top-N positions become hot spots used to bias population initialization toward geometrically promising locations.

**3D rendering optimizations:**

- Geometry mesh caching: xBIM shape instances are tessellated once and cached by shape hash and session ID; subsequent renders reuse cached meshes.
- WPF Freezable freezing: all `MeshGeometry3D`, `SolidColorBrush`, and `MaterialGroup` objects are frozen after construction, eliminating thread-affinity overhead and enabling cross-thread access without marshalling.
- In-place material override: element status colors are applied by mutating the `Material` property of existing `GeometryModel3D` instances rather than replacing geometry or spawning overlay meshes.

**Diagnostics cold-path separation.** The `GAProfiler` and CSV diagnostics generation are gated behind `DiagnosticsLevel` and `EnableProfiling` flags. In the default off configuration, no profiling overhead is incurred during optimization.

---

## User Configuration

### Optimization

| Parameter | Default | Description |
|---|---|---|
| `PopulationSize` | — | Number of individuals per generation |
| `MaxGenerations` | — | Maximum number of generations per stage |
| `MutationRate` | — | Probability of applying mutation to a child |
| `CrossoverRate` | 0.8 | Probability of applying crossover rather than direct copy |
| `MaxCranePositions` | 3 | Maximum crane setups in a chromosome |
| `EarlyStoppingGenerations` | 20 | Generations of stagnation before termination |
| `ConvergenceThresholdPercent` | 0 | Minimum improvement % to count as progress (0 = disabled) |
| `RandomSeed` | 42 | Fixed seed for reproducible runs; null for fully random |
| `EnableMultiStageDiscovery` | true | Run GA stages from 1 to MaxCranePositions |
| `MultiStageCoverageTarget` | 0.95 | Early stop threshold for multi-stage |

### Engineering

| Parameter | Default | Unit | Description |
|---|---|---|---|
| `WireLength` | 2.0 | m | Vertical rigging height added to hook elevation |
| `AdditionalWt` | 0.0 | t | Additional rigging accessory weight |
| `DeratingRatio` | 0.9 | — | Load chart capacity multiplier (0 to 1) |
| `SafetyFactor` | 0.95 | — | Additional safety multiplier after derating |
| `MaxRadiusForNormalisation` | 50 | m | Radius scale for penalty normalization |
| `MinimumRelocationDistance` | 10.0 | m | Minimum distance between two setup points |
| `DuplicatePositionDistance` | 3.0 | m | Distance below which two positions are treated as identical |
| `GridStep` | 0 | m | Grid snapping step (0 = free placement) |

### Fitness Weights

| Parameter | Default | Description |
|---|---|---|
| `CoverageWeight` | 10 | Weight on coverage ratio |
| `CoverageDominanceMultiplier` | 100 | Multiplier ensuring coverage dominates all penalties |
| `RelocationWeight` | 5 | Penalty per additional crane setup beyond the first |
| `RadiusPenaltyWeight` | 3 | Penalty on fraction of out-of-radius elements |
| `RadiusQualityPenaltyWeight` | 1.0 | Penalty on average working radius magnitude |
| `OverweightPenaltyWeight` | 10 | Penalty on fraction of overweight elements |
| `HeightPenaltyWeight` | 5 | Penalty on fraction of height-fail elements |
| `BalancePenaltyWeight` | 2.0 | Penalty on imbalanced element distribution across positions |
| `RadiusStdDevPenaltyWeight` | 2.0 | Penalty on variation in working radii |
| `SimilarPositionPenaltyWeight` | 8.0 | Penalty on near-duplicate setup positions |
| `RelocationDistancePenaltyWeight` | 1.5 | Penalty on total travel distance between setup positions |

---

## Installation

### System Requirements

- **Operating System**: Windows 10 or Windows 11, 64-bit. Windows is required because `Xbim.Geometry.Engine64.dll` is a native x64 Windows DLL.
- **Architecture**: x64 only. AnyCPU is supported only if resolved to x64 at runtime.
- **.NET Runtime**: .NET 8 Runtime (Windows).

### For Users

1. Obtain the build output directory (no installer is currently provided).
2. Ensure `Xbim.Geometry.Engine64.dll` is present alongside the executable (the build process copies it automatically).
3. Ensure `Data/crane.db` exists or will be created on first run (the application creates it automatically).
4. Run `CraneOptimizer.UI.exe`.

### For Developers

1. Install the .NET 8 SDK.
2. Install Visual Studio 2022 (17.8 or later) with the `.NET desktop development` workload.
3. Clone the repository.
4. Restore NuGet packages.
5. Build the solution targeting `x64`. The build scripts automatically copy `Xbim.Geometry.Engine64.dll` from the xBIM NuGet package to the output directory.
6. Set `CraneOptimizer.UI` as the startup project.

---

## Build and Run

```powershell
# Restore all NuGet packages
dotnet restore CraneOptimizer.sln

# Build the solution for x64 (required for xBIM native engine)
dotnet build CraneOptimizer.sln --configuration Release -p:Platform=x64

# Run the unit test suite
dotnet test CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj

# Run the application
dotnet run --project CraneOptimizer.UI/CraneOptimizer.UI.csproj -p:Platform=x64
```

The test project targets `net8.0-windows` and must be run on Windows. `Xbim.Geometry.Engine64.dll` is also copied to the test output directory by a build target.

---

## Testing

Tests are in `CraneOptimizer.Application.Tests` using **xUnit 2.9.2** with **coverlet** for coverage instrumentation.

| Area | Test files |
|---|---|
| GA operators | OperatorsTests.cs, AdaptiveSigmaTests.cs, EmptyChromosomeRegressionTests.cs |
| GA fitness and assignment | AssignmentPolicyTests.cs, GaLiftAnalysisRulesTests.cs, OptimizationLiftStatusResolverTests.cs |
| Load chart capacity resolver | LoadChartCapacityResolverTests.cs, LoadChartCapacityUnitTests.cs |
| Load chart PDF import | LoadChartPdfMetadataServiceTests.cs, PdfCorpusRegressionGateTests.cs, PdfLoadChartMultiTableSegmentationTests.cs |
| Constraint engine | ConstraintEngineTests.cs, ZoneGeometryTests.cs |
| Coverage engine | CoverageEngineTests.cs |
| Multi-stage orchestration | MultiStageGAOrchestratorTests.cs, MultiStageCustomComparisons.cs |
| Hungarian assignment | HungarianAssignmentServiceTests.cs |
| Reachability service | ReachabilityMapServiceTests.cs |
| GA result persistence | GaResultSnapshotPersistenceTests.cs |
| IFC normalization and filtering | Files under IFC/Audit/, IFC/Filtering/, IFC/Georeferencing/ |
| IFC identity | IfcIdentitySsotTests.cs, IfcIdentityCacheTests.cs |
| Diagnostics | DiagnosticsSeparationTests.cs, BenchmarkV2DiagnosticsContractTests.cs |
| UI viewmodel workflows | CraneOptimizerDialogViewModelTests.cs, CraneOptimizerDialogWorkflowTransparencyTests.cs |
| GA presets | GAModePresetFactoryTests.cs |

---

## Limitations and Assumptions

### Geometric Simplifications

**2D horizontal radius.** The operating radius used for all load chart lookups is a strictly 2D horizontal distance. Terrain slope between the crane and the element is ignored for radial capacity calculations.

**2D zone footprints.** Allowed and forbidden zones are 2D polygons on a ground plane. Obstacles project infinitely upward. Overhead slewing clearance (above a low-rise structure, or under power lines) is not modeled.

**Axis-aligned bounding box sampling.** Random position generation inside zones samples from the axis-aligned bounding box of the zone polygon, then rejects points outside the actual polygon. Highly skewed or narrow polygons may require many sampling attempts.

### Physical and Environmental Constraints

**Wind and weather not modeled.** The optimization engine does not calculate wind-induced load derating or dynamic load effects.

**Static rigging configuration.** All elements are assumed to use identical rigging geometry: a fixed vertical sling height (`WireLength`) and a fixed accessory weight (`AdditionalWt`). Element-specific rigging setups, spreader beam lengths, rigging angles, or multi-point lift configurations are not simulated.

### Data Assumptions

**Weight trust gating.** Elements with `WeightTrustLevel = EstimatedFallback_NotAllowedForGA` are hard-blocked from GA optimization. There is no user override for this gate.

**IFC property set variability.** Property names for weight, mark, and assembly identity vary significantly across IFC exporter tools and versions. Unusual custom configurations may require additional alias registration.

### Optimization Engine

**Single crane specification per run.** The GA optimizes positions for multiple setups of one crane specification. Mixed-fleet optimization (different crane models in one run) is not supported.

**Heuristic, not exact.** The Genetic Algorithm is a stochastic heuristic. It does not guarantee finding the globally optimal solution. Results are high-quality approximations. Different random seeds may produce different results.

### Coordinate System

**Local coordinate space only for optimization.** Georeferencing (survey coordinates, Easting/Northing) is a presentation-only feature. All geometry, optimization, radius calculations, zone containment checks, and database storage remain in the IFC model's local coordinate space.

**No reprojection engine.** Geographic curve coordinates (latitude/longitude from IFC2x3 IfcSite) are informational only. Projection conversion to grid Easting/Northing is not implemented.

### Reports and Storage

**No automatic log rotation.** Diagnostics CSVs, GA profiling logs, and PDF/CSV exports are retained indefinitely. The application does not enforce size limits or automatic cleanup.

**No project context, no output.** All export paths are resolved relative to the active project folder. Running export operations without a loaded project is blocked.

---

## Roadmap / Future Work

The following items are identified as future work in the project documentation. None of these are currently implemented.

- **[Planned]** Crane relocation path planning: computing the physical movement route between setup positions (Path Engine)
- **[Planned]** 3D lift trajectory simulation: simulating the full 3D arc of an element from pick-up to placement with collision checking
- **[Planned]** Multi-crane heterogeneous optimization: optimizing positions for multiple different crane models in a single run
- **[Planned]** Native BIM add-in JSON data export: Tekla/Revit add-ins exporting canonical JSON business data; IFC file would carry geometry only
- **[Planned]** Reprojection engine: conversion from geographic coordinates to projected grid coordinates
- **[Planned]** Survey coordinate export in reports: PDF and Excel output with Easting/Northing values
- **[Planned]** Hoist schedule and time estimation: estimating erection schedules from stored slewing and hoisting speeds
- **[Planned]** Manual coordinate reference: interactive viewport selection of reference points

---

## Development Guidelines

### Architecture Rules

- **Domain isolation**: The Domain layer must have zero references to EF Core, xBIM geometry, WPF, or any infrastructure package.
- **Dependency direction**: Application may reference Domain only. Infrastructure may reference Domain and Application. UI references all layers. No reverse dependencies.
- **Repository pattern**: All database access must go through repository interfaces defined in Domain and implemented in Infrastructure.
- **No business logic in ViewModels**: ViewModels coordinate UI state and delegate to Application-layer services. Optimization logic, weight calculations, and constraint evaluation must remain in the Application layer.
- **No business logic in code-behind**: XAML code-behind should contain only WPF event forwarding and UI-specific initialization.

### MVVM

- All ViewModels inherit from `ViewModelBase` in `CraneOptimizer.UI.Core`.
- Use `RelayCommand` for command binding.
- Long-running operations (IFC load, optimization) must run on background tasks with `CancellationToken` support and progress reporting.

### Dependency Injection

- All services, repositories, engines, and ViewModels are registered in `App.xaml.cs`.
- Constructor injection only. No service locator pattern.
- Resolve engines and services through interfaces, not concrete types.

### Testing

- New optimization logic should have unit tests before merging.
- Load chart resolver changes require tests against the existing corpus of test cases in `LoadChartCapacityResolverTests.cs`.
- GA operator changes require tests in `OperatorsTests.cs` or dedicated regression files.

---

## Documentation

The `docs/` directory contains developer documentation organized as follows:

| Area | Path |
|---|---|
| System architecture overview | docs/architecture/system-overview.md |
| Rendering strategy | docs/architecture/rendering-strategy.md |
| Database design | docs/architecture/database.md |
| Database migrations | docs/architecture/database-migrations.md |
| Fitness function design | docs/architecture/fitness-function.md |
| Optimization engines | docs/architecture/engines.md |
| Assignment engine | docs/architecture/assignment-engine.md |
| GA engine design (detailed) | docs/ga/ga-engine-design.md |
| GA settings guide | docs/ga/ga-settings-guide.md |
| Fitness weights | docs/ga/fitness-weights.md |
| GA diagnostics policy | docs/ga/diagnostics-release-policy.md |
| GA result persistence | docs/ga/ga-result-persistence.md |
| GA mode presets | docs/ga/ga-mode-presets-v1.md |
| Crane and lifting engine | docs/domain/crane-engine.md, docs/domain/lifting-engine.md |
| Load chart PDF import | docs/domain/pdf-load-chart-import.md |
| Unit system | docs/domain/units.md |
| GIS coordinate integration | docs/project/gis-survey-coordinate-integration.md |
| Limitations and assumptions | docs/project/limitations.md |
| Project scope and exclusions | docs/project/scope.md |
| Roadmap | docs/project/roadmap.md |
| Coding standards | docs/coding-standards/ |
| Architecture Decision Records | docs/decisions/ADR-001 through ADR-005 |

ADRs document key technology decisions: ADR-001 (use of xBIM), ADR-002 (use of SQLite), ADR-003 (MVVM architecture), ADR-004 (Genetic Algorithm), ADR-005 (.craneopt project file format).

---

## License

License information has not yet been specified for this repository.

---

## Project Status

CraneOpt is a graduation project under active development. The core optimization pipeline, IFC processing, load chart management, 3D visualization, and project persistence are implemented and functional. The codebase has a substantial test suite covering the optimization engines, load chart resolution, and IFC processing.

The application is not production-released and does not have a published installer. Features marked [Planned] in the roadmap are not implemented.
