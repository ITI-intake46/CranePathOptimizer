# GA Run Modes / Benchmark v2 Discovery Report

**Audit date:** 2026-06-07  
**Audited branch:** `enhancemnt_CSV_filesToStartModels`  
**Audited commit:** `e6257ec`  
**Scope:** Discovery only. No optimizer behavior, CSV schema, user-facing mode, or instrumentation was changed.

> The original task text named `phase16-17_HungarianAlgorithms`. The user explicitly selected
> `enhancemnt_CSV_filesToStartModels` because it contains the integrated final code from collaborators.
> All conclusions below therefore apply to `enhancemnt_CSV_filesToStartModels` at `e6257ec`.

## Executive Summary

The codebase is ready for a **small, deliberate CSV instrumentation task**, but the current generated
dataset is not sufficient to select defensible values for Balanced Coverage, Minimum Relocation, and
Crane-Friendly modes.

The engine already has most run-result data needed for coverage and relocation analysis. It also has
the raw data needed for crane stress metrics: radius, required boom length, required capacity, effective
available capacity, crane specification limits, and load-chart rows. The missing work is primarily
exposure, identity, and aggregation rather than new optimization logic.

The largest gaps are project/scope identity, project complexity features, crane specification limits in
diagnostics, per-zone candidate/rejection counters, and explicit final-lift utilization fields. These
should be added before Benchmark_v2 so that mode calibration can be performed offline.

There is an important source/artifact mismatch:

* Current source implements schema-versioned CSVs with `OptimizationRunId` and `StageRunId`.
* The existing generated corpus contains 1,011 older timestamped diagnostic sets and one legacy
  `ga_run_history.csv`; it does not contain the seven newer aggregate CSVs.
* The expected 972-row `ExperimentResults.csv` is not present in the workspace. Only an eight-row
  baseline file exists.

Accordingly, current source contracts are promising, but they need one clean regeneration campaign
before they can be treated as verified Benchmark_v2 inputs.

## Evidence Classification

| Label | Meaning |
|---|---|
| Available in source | A property or writer exists in the audited branch. |
| Verified in generated output | The field/file was observed in an existing CSV artifact. |
| Derivable offline | It can be calculated without changing the optimizer, provided identified source rows are exported. |
| Needs instrumentation | The runtime does not retain or export enough evidence to calculate it safely. |
| Ambiguous | A field exists, but its grain or meaning changes by caller or is not uniquely identified. |

## 1. Crane Database And Specification Availability

### Storage And Selection Flow

The catalog database is:

`CraneOptimizer.UI/bin/Debug/net8.0-windows/Data/crane.db`

Relevant tables and EF entities:

| Database table | EF entity | Purpose |
|---|---|---|
| `Cranes` | `Crane` | Crane model/name/manufacturer/type and vehicle/outrigger dimensions. |
| `CraneSpecifications` | `CraneSpecification` | Rated limits, boom/radius envelope, counterweight, derating metadata, and chart metadata. |
| `TargetPoints` | `TargetPoint` | Discrete load-chart radius, boom-length, and capacity cells. |
| `ProjectCraneSnapshots` | `ProjectCraneSnapshotEntity` | Project-scoped copy of crane/specification values. Present in schema, empty in inspected projects. |
| `ProjectCraneLoadChartSnapshots` | `ProjectCraneLoadChartSnapshotEntity` | Project-scoped load-chart rows. Present in schema, empty in inspected projects. |

`CraneRepository.GetAllWithSpecsAsync` and `GetByIdWithSpecsAsync` eagerly load specifications and
their load charts. `OptimizationInputBuilder.SelectCraneSpecification` selects the first specification
that contains a load chart. `PlacementInput` carries the selected `CraneSpecification` object, but it
does not carry an explicit `CraneSpecificationId` metadata field for diagnostics.

### Catalog Evidence

| Crane | Specification ID | Max capacity | Radius range | Boom range | Chart rows | Vehicle dimensions | Outrigger dimensions |
|---|---:|---:|---|---|---:|---|---|
| STC800 | 1 | 80 t | 3-36 m | 11.8-45 m | 156 | 14.635 x 2.75 x 3.429 m | 6.1 x 7.6 m |
| 180Tons | 2 | 180 t | 2.5-60 m | 13.1-62 m | 456 | 15 x 4 x 3 m | 6 x 6 m |

### Availability Matrix

| Metric/Data | Available? | Source file/table | Property | Unit | Safe to export? | Notes |
|---|---|---|---|---|---|---|
| Crane model identity | Yes | `Cranes`; `Crane.cs` | `Crane.Id`, `Name` | ID/text | Yes | Export both ID and name. |
| Selected specification identity | Runtime only | `CraneSpecifications`; `PlacementInput.CraneSpec` | `CraneSpecification.Id` | ID | Yes | Add explicit diagnostic column; current selection is first chart-bearing spec. |
| Max boom length | Yes | `CraneSpecifications`; `CraneSpecification.cs` | `MaxBoomLength` | m | Yes | Selected through `PlacementInput.CraneSpec`. |
| Min boom length | Yes | Same | `MinBoomLength` | m | Yes | Useful for envelope checks. |
| Max radius | Yes | Same | `MaxRadius` | m | Yes | Do not substitute `GASettings.MaxRadiusForNormalisation`; that is a settings value. |
| Min radius | Yes | Same | `MinRadius` | m | Yes | Useful for chart-envelope interpretation. |
| Max rated capacity | Yes | Same | `MaxCapacity` | metric tons | Yes | Rated maximum is not the capacity at the evaluated chart cell. |
| Discrete chart boom lengths | Yes | `TargetPoints`; `TargetPoint.cs` | `BoomLength` | m | Yes | Multiple rows/cells per boom length. |
| Discrete chart radii | Yes | Same | `Radius` | m | Yes | Resolver uses conservative radius stepping. |
| Chart cell capacity | Yes | Same | `Capacity` | metric tons | Yes | Raw chart capacity before factors. Blank/zero cells have special handling. |
| Counterweight | Yes | `CraneSpecifications` | `CounterWeight` | metric tons | Yes | Catalog values may be zero or incomplete. |
| Jib length | No verified field | Entities/schema inspected | None | - | No | Do not infer from boom length or configuration JSON. |
| Outrigger configuration | Yes | `CraneSpecifications` | `OutriggerConfig` | text/configuration | Yes | Also dimensions exist on `Crane`. |
| Vehicle/base dimensions | Yes | `Cranes`; project snapshot | `VehicleLength/Width/Height`, `OutriggerLength/Width` | m | Yes | Nullable outrigger dimensions must be handled. |
| Crane footprint radius | Derived | `OptimizationInputBuilder` | `CalculateCraneFootprintRadius` | m | Yes | `max(vehicle L/W, outrigger L/W) / 2`; it overwrites the settings value. |
| Element weight | Yes | `PrecastElement` | `ElementWT` | metric tons | Yes | Canonical domain unit is metric tons. |
| Required capacity | Yes | lifting/assignment engines | `RequiredCapacity` | metric tons | Yes | `ElementWT + GASettings.AdditionalWt`. |
| Effective available capacity | Yes | `LoadChartCapacityResolver` | `AvailableCapacity` | metric tons | Yes | `chart capacity * DeratingRatio * SafetyFactor`. |
| Derating ratio | Yes | `GASettings` | `DeratingRatio` | ratio | Yes | Applied inside capacity resolver. |
| Safety factor | Yes | `GASettings` | `SafetyFactor` | ratio | Yes | Applied inside capacity resolver. |

### Safe Crane-Friendly Formulas

For a final evaluated lift:

* `RequiredCapacityTons = ElementWeightTons + AdditionalWtTons`
* `AvailableCapacityTons = ChartCapacityTons * DeratingRatio * SafetyFactor`
* `CapacityUtilization = RequiredCapacityTons / AvailableCapacityTons`
* `CapacityMarginTons = AvailableCapacityTons - RequiredCapacityTons`
* `RadiusUtilization = RadiusM / CraneSpecification.MaxRadius`
* `BoomUtilization = RequiredBoomLengthM / CraneSpecification.MaxBoomLength`

These formulas are safe when denominators are positive and the row represents the final selected chart
evaluation. The current `ga_element_assignments.csv` source writes raw `ElementWT` as `RequiredWeight`;
therefore consumers must add `AdditionalWt`, or the file should expose `RequiredCapacity` explicitly.
The older detailed diagnostic writer already carries `RequiredCapacity`.

Recommended exposure path: add selected crane/specification identity and limits from
`PlacementInput.CraneSpec` to the run/settings snapshot, then add final-lift utilization inputs to the
element-assignment output. No load-chart or optimizer change is required.

## 2. Element And Project-Scope Availability

### Scope Flow

The UI obtains all scene elements, applies `OptimizationScopeEngine.FilterElements`, then applies
normalization and strict GA gating. `OptimizationInputBuilder` filters `IncludeInGa` and optional
`GASettings.TargetElementTypes`. Therefore several distinct counts exist:

1. Raw/cached scene elements.
2. Elements remaining after the user scope filter.
3. Elements passing normalization/GA readiness.
4. Elements entering `PlacementInput.Elements`.

Only the first and fourth are currently carried consistently in the UI input. In GAExperiment,
`TotalElementsCount` is assigned after strict gating, so its meaning differs from the UI caller. This
field must not be treated as an unambiguous raw count in Benchmark_v2.

### Availability Matrix

| Feature | Available? | Source | Current CSV? | Recommended target | Notes |
|---|---|---|---|---|---|
| Total raw/cached elements | Caller-dependent | scene/project cache; `ProjectIfcFiles.ElementCount` | Legacy history only, ambiguously named | `ga_project_features.csv` | Add an explicit `RawElementCount`. |
| Optimization-scope count | Runtime, not retained explicitly | `OptimizationScopeEngine` result | No | `ga_project_features.csv` | Capture immediately after scope filtering. |
| GA-ready count | Yes | final gated list / `PlacementInput.Elements.Count` | `SelectedElements` or `GaReadyElementCount` in current source | Run summary and project features | Rename/define grain precisely. |
| Type/category counts | Yes from elements/database | `ResolvedCategory`, `ElementType`, `Role` | No aggregate columns | `ga_project_features.csv` | Include Wall, Column, Beam, Footing, Assembly, Slab, Other. |
| Include footings decision | Equivalent exists | `TargetElementTypes` and element gating | Settings snapshot stores target types | Settings snapshot | No standalone `IncludeFootings` boolean exists. |
| Target element types | Yes | `GASettings.TargetElementTypes` | Current source settings snapshot | Keep | Semicolon-delimited list. |
| Length/width/height | Model properties exist | `PrecastElement`; `ProjectIfcElementEntity` | No | Project features or element research file | Inspected project rows have direct dimensions null. |
| Bounding-box dimensions | Derivable | bbox min/max; GAExperiment mapping | No | Project features | Define separately from semantic dimensions. |
| Volume | Yes | `ElementVolume` / `VolumeM3` | No | Project features | Present for GA-ready elements in inspected projects. |
| Center of gravity | Yes | `ElementCOG` / `COG_M` | Detailed diagnostics | Keep in detailed/research output | Canonical meters. |
| Weight | Yes | `ElementWT` / `WeightTons` | Detailed and assignment files | Keep | Metric tons. |
| Weight source/trust | Yes | normalization model/project DB | No | Project features; optional element research output | Needed to audit project comparability. |
| Average/P95/max weight | Derivable | final scoped elements | No aggregate | `ga_project_features.csv` | Prefer offline aggregation from a stable scope snapshot. |
| Average/max height | Conditionally derivable | direct or bbox height | No | `ga_project_features.csv` | Must record which height definition is used. |
| Project name | Yes | workspace/project DB/input | Current source run/settings summary; legacy history | Keep | Available in try4/try9. |
| Project ID/GUID | Exists, not propagated | `ProjectInfoEntity.ProjectGuid` | No | All Benchmark_v2 files | Required stable join key. |
| IFC/scenario file | Yes | workspace/input/config | Current source settings/history | Keep | File name alone is not a stable scenario ID. |

### Inspected Project Evidence

| Project | Cached rows | IncludeInGa | Strict GA-ready evidence | Category observations |
|---|---:|---:|---:|---|
| try4 | 392 | 196 | 196 | 71 Columns, 41 Footings, 75 Walls, 9 Beams; 75 Assemblies are grouping-only. |
| try9 | 7,637 | 3,967 | 3,940 | 2,100 Beams, 1,738 trusted Slabs, 102 Columns; 27 suspicious-trust liftables excluded. |

Direct `LengthM`, `WidthM`, and `HeightM` were null in both inspected project databases. Volume and COG
were populated for relevant rows. Benchmark_v2 should either improve direct dimension persistence or
explicitly define its height metrics from bounding-box extents.

## 3. Allowed Zones And Site Complexity

`GreenAreaZone` carries `ZoneId`, vertices, area, name, and type. `SiteZoneEntity` persists the same
concept, including `VerticesJson` and `Area`. Polygon area is calculated by `ZoneGeometryService` using
the absolute signed 2D area.

| Zone/Site metric | Available? | Source | Current CSV? | Instrumentation needed? | Notes |
|---|---|---|---|---|---|
| Allowed zone IDs | Yes | `GreenAreaZone.ZoneId` | Hotspots/positions/stage source | No | Use stable GUIDs. |
| Polygon vertices | Yes at runtime/database | `GreenAreaZone.Vertices`; `SiteZones.VerticesJson` | No | Only if reproducibility file is needed | Avoid repeating vertices per run. |
| Per-zone area | Yes | `Area` or geometry calculation | No | Add project feature aggregate | Unit is m2 under canonical coordinates. |
| Total allowed area | Conditionally safe | sum/geometry union | No | Definition required | Simple sum double-counts overlaps; union area requires geometry union. |
| Allowed zone count | Yes | input zones | No run aggregate | Add project features | Count only valid allowed zones. |
| Min/max zone area | Derivable | zone areas | No | Add project features | Cheap. |
| Allowed area per element | Derivable | area and scoped count | No | Add project features | Denominator must be explicit. |
| Candidate points per zone | Not retained | grid generation | No | Yes | Instrument before de-duplication/filtering. |
| Valid points per zone | Not retained as counters | `ReachabilityMapService` local values | No | Yes | Current `TotalCandidatePoints` actually uses valid-point count. |
| Rejected points per zone | No | constraint checks | No | Yes | Needs counters. |
| Rejection reasons | No aggregate | `ConstraintEngine` boolean path | No | Yes | At minimum: outside allowed footprint, forbidden overlap, invalid polygon, duplicate/separation. |
| Selected seeds per zone | Not reliably available | initializer provenance not retained | Hotspot source infers proximity to final positions | Yes | Final-position proximity is not true seed provenance. |
| Top reachability per zone | Yes from scored results | `ScoredCoordinate` | Hotspot source | No, after regeneration | Current generated corpus lacks hotspot files. |
| Reachability mean/stddev | Derivable only if all scored valid points retained/exported | reachability scoring | No | Usually yes | Top-N hotspots are biased and insufficient for population statistics. |
| Zone coverage skew | Partially derivable | assignments/positions by zone | No aggregate | Add derived summary | Define whether skew is lifted elements, positions, or reachable candidates. |
| Zone-specific liftability | Not directly | full candidate-element feasibility | No | Yes or offline spatial join | Add candidate zone to matrix or an element-zone feasibility summary. |
| Zone starvation | Not diagnosable reliably | current counters insufficient | No | Yes | Cannot distinguish no candidates, all rejected, low reachability, or initializer omission. |

Observed allowed-zone areas:

* try4: approximately 1,699.84 m2 and 549.56 m2.
* try9: approximately 1,904.43 m2, 725.36 m2, and 1,355.88 m2; one forbidden zone is approximately
  21.41 m2.

`ReachabilityMapService` currently exposes only total/zero-coverage/hotspot counts and maximum/average
hotspot coverage. It does not expose the per-zone pipeline counters required to prove whether a small
zone is starved.

## 4. Current Diagnostic CSV Audit

### Generated Corpus

Inspected directory:

`CraneOptimizer.GAExperiment/bin/Debug/net8.0-windows/Diagnostics`

| Artifact family | Files | Total bytes |
|---|---:|---:|
| `ga_diagnostics_*.csv` | 1,011 | 1,745,535,145 |
| `ga_assignment_matrix_*.csv` | 1,011 | 636,109,776 |
| `ga_final_assignments_*.csv` | 1,011 | 274,781,476 |
| `ga_failure_breakdown_*.csv` | 1,011 | 692,190 |
| `ga_run_history.csv` | 1 | 671,918 |
| **Total** | **4,045** | **2,657,790,505** |

The latest inspected timestamped set was `20260605_211621`. CSV parsing found valid header/row field
counts and decimal-dot numeric values. It is a legacy schema without the newer run/stage/schema keys.

### Readiness Matrix

“Source” means the current branch writer exists. “Generated” means an artifact was observed.

| File | Exists in source/generated? | Valid schema? | Join keys | Balanced | Min relocation | Crane-friendly | Main missing fields | Recommendation |
|---|---|---|---|---|---|---|---|---|
| `ga_run_summary.csv` | Yes / No | Source test contract only | Opt + Stage, no StageIndex | Strong | Strong | Weak | ProjectId, spec ID/limits, StageIndex | Extend minimally. |
| `ga_run_history.csv` | Yes / Yes legacy | Generated rows valid; legacy header | None in generated; current source adds keys/schema | Strong | Strong | Weak | Stable project/spec IDs and stress aggregates | Keep as operational history; do not make it the only benchmark fact table. |
| `ga_settings_snapshot.csv` | Yes / No | Source test contract only | Opt + Stage, no StageIndex | Configuration | Configuration | Configuration | ProjectId, CraneSpecId, explicit scope counts | Extend. |
| `ga_stage_summary.csv` | Yes / No | Source test contract only | Opt + Stage + StageIndex | Strong | Strong | Weak | Previous-stage gain and stress aggregates | Keep one row per stage. |
| `ga_generation_summary.csv` | Yes / No | Source test contract only | Opt + Stage + StageIndex | GA behavior | Indirect | No | Worst fitness, real mutation counters, crossover count, diversity | Research-only extension. |
| `ga_reachability_hotspots.csv` | Yes / No | Source test contract only | Opt + Stage; no StageIndex | Useful | Useful | Useful | StageIndex, raw/valid/rejected counters, true seed provenance | Extend for research; add separate per-zone summary. |
| `ga_positions.csv` | Yes / No | Source test contract only | Opt + Stage; no StageIndex | Strong | Strong | Indirect | StageIndex, relocation-to-previous, route semantics | Extend slightly or derive offline. |
| `ga_element_assignments.csv` | Yes / No | Source test contract only | Opt + Stage; no StageIndex | Strong | Strong | Strong after fixes | RequiredCapacity, spec limits, utilization, element metadata | Primary extension target. |
| `ga_failure_breakdown_*.csv` | Yes / Yes legacy | Latest generated rows valid | New source has keys/schema; generated does not | Strong | Weak | Useful | Project/spec identity | Keep; join after regeneration. |
| `ga_diagnostics_*.csv` | Yes / Yes legacy | Latest generated 128-column rows valid | New source has keys/schema; generated does not | Strong | Strong | Strong | Stable IDs/spec limits; very wide | Research-only detailed output. |
| `ga_assignment_matrix_*.csv` | Yes / Yes legacy | Latest generated rows valid | New source has keys/schema; generated does not | Strong | Strong | Strong | Candidate ZoneId and rejection pipeline context | Research-only due size. |
| `ga_final_assignments_*.csv` | Yes / Yes legacy | Latest generated rows valid | New source has keys/schema; generated does not | Strong | Strong | Strong | Stable IDs/spec limits/utilization fields | Keep or converge with element assignments. |

### Key Contract Findings

* Current source writes decimal values with `InvariantCulture` in the inspected writers.
* The focused test `GACloseOutFixTests.TestCsvDiagnostics_SchemasAndHeaderColumnCountsMatch` asserts at
  least seven CSV files, equal header/row field counts, matching run IDs, and schema `1.0`.
* The focused test command did not complete within 120 seconds and was stopped after its remaining
  process became idle. Therefore this audit does not claim a fresh passing test run.
* Multi-stage execution calls the placement engine once per stage. The stage writer emits the current
  last profiler stage, so each stage call can append one stage row.
* Generation output filters profiler logs by current `StageIndex`, preventing earlier stages from being
  appended again solely because profiler state is shared.
* `StageIndex` is encoded as the stage's `MaxCranePositions`. That is valid for the current 1-3 discovery
  loop but should be documented as a stage-position index, not a universal stage ordinal.
* The diagnostics service recalculates reachability hotspots after the run. This is potentially
  expensive and should be gated in normal runs.

## 5. Solution Quality Metric Feasibility

### Coverage And Relocation

| Metric | Compute now? | Source | Formula | New field? | Target |
|---|---|---|---|---|---|
| CoverageRatio | Yes | run/stage/result | lifted / scoped elements | No | Run/stage summary |
| CoveragePercent | Yes | same | ratio * 100 | No | Derived or summary |
| LiftedElementCount | Yes | result/run/stage | count final lifted | No | Run/stage summary |
| FailedElementCount | Yes | result/run | scoped - lifted | No | Run summary |
| PositionCount | Yes | result/positions | final positions count | No | Run/stage summary |
| CoverageGainFromPreviousStage | Yes after current CSV regeneration | stage summary | current coverage - previous stage coverage | No | Derived solution metrics |
| CoverageGainPerExtraPosition | Yes | stage summary | coverage gain / position-count gain | No | Derived solution metrics |
| TotalRelocationDistance | Yes | ordered positions | sum distance between adjacent position indices | No | Derived solution metrics |
| AverageRelocationDistance | Yes | positions | total / max(position count - 1, 1) | No | Derived solution metrics |
| UsedZoneCount | Yes | positions/stage IDs | distinct nonempty zone IDs | No | Derived solution metrics |
| UsedZoneIds | Yes | stage/positions | distinct IDs | No | Stage summary |

Relocation semantics require care:

* GAExperiment reports a 3D adjacent-position polyline.
* The fitness engine uses a 2D adjacent-position polyline normalized by radius and position count.
* Position index is chromosome/setup order, not a proven construction travel route.

### Crane-Friendly Metrics

| Metric | Compute now? | Source/formula | New field? | Target |
|---|---|---|---|---|
| Average/max/P95/stddev radius | Yes after assignment regeneration | aggregate final lifted `Radius` | No | Derived solution metrics |
| Average/max/P95 required boom | Yes | aggregate final lifted `RequiredBoomLength` | No | Derived solution metrics |
| Average/max/P95 capacity utilization | Not safely from current assignment column alone | `(RequiredWeight + AdditionalWt) / AvailableCapacity` | Add explicit `RequiredCapacity` recommended | Element assignments + derived metrics |
| Min/average/P95 capacity margin | Same condition | `AvailableCapacity - RequiredCapacity` | Add explicit `RequiredCapacity` recommended | Same |
| Radius utilization | Crane limit not exported | `Radius / CraneSpec.MaxRadius` | Yes | Element assignments |
| Boom utilization | Crane limit not exported | `RequiredBoomLength / CraneSpec.MaxBoomLength` | Yes | Element assignments |
| High-utilization flag | Derivable after threshold is defined | utilization >= mode threshold | No raw flag needed | Offline/model layer |
| Long-radius flag | Derivable | radius utilization >= threshold | No raw flag needed | Offline/model layer |
| Long-boom flag | Derivable | boom utilization >= threshold | No raw flag needed | Offline/model layer |

Threshold booleans should not be persisted yet. Persist the continuous inputs and derive flags after mode
thresholds are selected. Also consider P05 capacity margin in research analysis: P95 margin describes
large margins, while the lower tail is more relevant to stress risk.

### GA Behavior

| Metric | Compute now? | Source | New field? | Target |
|---|---|---|---|---|
| BestFitness | Yes | generation profiler | No | Generation summary |
| AverageFitness | Yes | generation profiler | No | Generation summary |
| WorstFitness | No | population available in loop, not logged | Yes | Research generation summary |
| BestCoverageRatio | Yes | generation profiler | No | Generation summary |
| AverageCoverageRatio | Yes | generation profiler | No | Generation summary |
| Count1/2/3Positions | Yes | generation profiler | No | Generation summary |
| MutationSuccessRate | Column exists, value is hardcoded zero | `GAEngine` | Real counter required | Research generation summary |
| InvalidMutationCount | Column exists, value is hardcoded zero | `GAEngine` | Real counter required | Research generation summary |
| CrossoverCount | No | operators not aggregated | Yes | Research generation summary |
| MutationCount | No | operators not aggregated | Yes | Research generation summary |
| StagnationCounter | Yes | generation profiler | No | Generation summary |
| PopulationDiversityMetric | No | not calculated | Yes, only if a hypothesis needs it | Research generation summary |

## 6. Project Scale Classification

GA-ready element count is a necessary size feature but is not sufficient for mode selection. Two projects
with the same element count can differ greatly in spatial feasibility, weight distribution, zone
fragmentation, valid setup density, and crane reachability.

Recommended non-final feature groups:

* **Scope size:** raw, scoped, GA-ready, and category counts.
* **Load difficulty:** average/P95/max weight and height; weight-source/trust proportions.
* **Site opportunity:** valid allowed-zone count, total/union area, min/max area, area per element.
* **Search-space density:** candidate count, valid count, rejection ratio, valid points per element.
* **Reachability:** maximum, mean, standard deviation, zero-coverage ratio, and per-zone skew.
* **Observed solution:** positions and zones used. Keep this out of the pre-run scale label because it is
  outcome-dependent.

A future `ProjectComplexityScore` is feasible, but no formula should be fixed until Benchmark_v2 contains
enough diverse projects and the feature definitions above are stable.

### Proposed `ga_project_features.csv`

Recommended grain: one row per **project + optimization scope + crane specification + grid/footprint
configuration**. Reachability features depend on crane and grid settings, so this must not be described
as a pure one-row-per-project table.

| Column group | Proposed columns | Availability |
|---|---|---|
| Identity | `ProjectFeatureSnapshotId`, `ProjectId`, `ProjectName`, `ScenarioId`, `IfcFileName`, optional IFC hash | Project GUID needs propagation; other values exist. |
| Scope identity | `ScopeDefinitionHash`, `TargetElementTypes`, selected-ID count, excluded-ID count | Partly available; hash not implemented. |
| Counts | `RawElementCount`, `OptimizationScopeElementCount`, `GaReadyElementCount` | Values exist at different pipeline points; need retention. |
| Categories | `WallCount`, `ColumnCount`, `BeamCount`, `FootingCount`, `AssemblyCount`, `SlabCount`, `OtherCount` | Derivable from scoped elements. |
| Load profile | `AverageWeightTons`, `P95WeightTons`, `MaxWeightTons`, trust/source counts | Derivable; not exported. |
| Geometry profile | `AverageHeightM`, `MaxHeightM`, `HeightDefinition`, optional volume statistics | Needs explicit dimension definition. |
| Crane/search identity | `CraneId`, `CraneSpecificationId`, `GridStepM`, `CraneFootprintRadiusM` | Available in runtime; IDs not exported. |
| Zones | `AllowedZoneCount`, `TotalAllowedAreaM2`, `AreaDefinition`, `MinAllowedZoneAreaM2`, `MaxAllowedZoneAreaM2`, `AllowedAreaPerGaReadyElementM2` | Cheap except overlap-safe union area. |
| Grid pipeline | `CandidateGridPointCount`, `ValidGridPointCount`, `RejectedGridPointCount`, `RejectedGridPointRatio`, rejection reason counts | Needs instrumentation. |
| Reachability | `ReachabilityMax`, `ReachabilityMean`, `ReachabilityStdDev`, `ZeroReachabilityRatio`, `ZoneReachabilitySkew` | Needs full valid-point aggregates. |

## 7. Benchmark v2 Dataset Structure

### `ga_project_features.csv`

**Purpose:** Stable pre-run project, scope, crane, and search-space features.  
**Grain:** One row per project/scope/crane/grid feature snapshot.  
**Keys:** `ProjectFeatureSnapshotId`, `ProjectId`, `CraneSpecificationId`, `ScopeDefinitionHash`.  
**Supports:** All modes and project-scale classification.  
**Generation:** Static features are derivable; grid/reachability counters need instrumentation.

### `ga_solution_quality_metrics.csv`

**Purpose:** Compact comparable outcome metrics without loading element or matrix files.  
**Grain:** One row per stage, with a final-stage/best-result marker.  
**Keys:** `OptimizationRunId`, `StageRunId`, `StageIndex`, `ProjectFeatureSnapshotId`.  
**Required columns:** coverage/lift/failure counts, position count, coverage gain, relocation distances and
definition, used-zone count, radius/boom/capacity distributions, utilization distributions, capacity
margins, runtime, fitness, termination reason, assignment engine, and `IsBestStage`.  
**Supports:** Balanced, Minimum Relocation, Crane-Friendly.  
**Generation:** Mostly post-processable from stage, position, and final assignment files once missing
crane/scope fields are exposed.

### `ga_mode_experiment_plan.csv`

**Purpose:** Immutable design table for planned Benchmark_v2 runs and resume validation.  
**Grain:** One row per planned optimization run/configuration/seed.  
**Keys:** `PlanRunId`, `BenchmarkVersion`, `ScenarioId`, `ProjectFeatureSnapshotId`.  
**Required columns:** project/scenario, crane/specification, seed, all varied GA settings, assignment
engine, requested mode label, diagnostics level, planned status, config hash.  
**Supports:** Reproducibility and balanced experimental design for every mode.  
**Generation:** Batch-runner extension; no optimizer instrumentation required.

### `ga_mode_experiment_results.csv`

**Purpose:** Completion/status and final selected outcome for each planned run.  
**Grain:** One row per `PlanRunId`, not per element or generation.  
**Keys:** `PlanRunId`, `OptimizationRunId`, `BestStageRunId`, `ProjectFeatureSnapshotId`.  
**Required columns:** start/end timestamps, status/error, best/final stage, core quality metrics,
execution time, observed schema versions, artifact paths/hashes, and benchmark version.  
**Supports:** Model fitting, mode comparison, failed-run accounting, and resumability.  
**Generation:** Batch runner plus derived solution-quality join.

Element-, position-, generation-, and assignment-matrix files should remain normalized research
artifacts rather than being copied into the run-level result table.

## 8. Performance And Storage Risk

| Metric/File | Runtime cost | Storage cost | Normal user run? | Research only? | Recommendation |
|---|---|---|---|---|---|
| Run/stage/settings summaries | Low | Low | Yes | No | Always emit. |
| Project static counts/weight aggregates | Low | Low | Yes | No | Compute once per feature snapshot. |
| P95 final assignment metrics | Low offline; moderate in-process sorting | Low aggregate | Summary result only | Raw rows optional | Prefer post-processing from final assignments. |
| Position/relocation summary | Low | Low | Yes | No | Derive from ordered positions. |
| Full element assignments | Moderate | O(elements) | Optional compact form | Yes for full detail | Keep for benchmark runs. |
| Detailed diagnostics | Moderate/high | O(elements x wide columns) | No by default | Yes | Existing corpus shows high cost. |
| Assignment matrix | High | O(elements x positions) | No | Yes | Main storage growth risk. |
| Reachability scoring | High | O(valid grid points x elements x chart lookup) | Only when optimizer requires it | Yes for extra export | Do not recompute solely for ordinary diagnostics. |
| Per-zone grid rejection counters | Low if counted during existing filter | Low | Yes summary | Detailed reasons research-only | Counters are cheap; rejected-point rows are not. |
| Population diversity | Potentially high | Low aggregate | No | Only with a research hypothesis | Defer. |

Recommended control: a developer-facing diagnostics level such as `Off`, `Summary`, and `Research`,
with heavy-file sub-options only if needed. This is an instrumentation control, not a GA user mode.
Normal user runs should emit compact run/settings/stage/project/solution summaries. Research runs may
add detailed diagnostics, matrix, generation, hotspot, and full assignment artifacts.

### Observed And Estimated Storage Per Run

Observed legacy four-file totals:

* 196 elements, one position: approximately 0.245 MB.
* 196 elements, two positions: approximately 0.319 MB.
* 3,940 elements, one position: approximately 4.87 MB.
* 3,940 elements, two positions: approximately 5.89 MB.

The following estimates extrapolate observed row widths and matrix growth. They exclude profiler logs
and assume the same four legacy detailed files; newer summaries add little, while a full new element
assignment file may add roughly 0.1-0.8 MB depending on element count.

| Elements | 1 position | 2 positions | 3 positions |
|---:|---:|---:|---:|
| 200 | ~0.25 MB | ~0.33 MB | ~0.39 MB |
| 1,000 | ~1.24 MB | ~1.50 MB | ~1.76 MB |
| 4,000 | ~4.94 MB | ~5.98 MB | ~7.02 MB |

The existing 1,011-run diagnostic corpus is already approximately 2.66 GB, demonstrating that heavy
diagnostics should not be the default for normal user runs.

## 9. Existing Benchmark Data Inventory

### Benchmark Configuration

`CraneOptimizer.GAExperiment/GAExperimentConfig.json` defines:

* Projects: try4 and try9.
* Cranes: STC800 and 180Tons.
* Population sizes: 30, 60, 100.
* Generations: 30, 60, 100.
* Mutation rates: 0.05, 0.10, 0.20.
* Mutation sigma: 0.03, 0.06, 0.12.
* Seeds: 1, 42, 99.
* One crossover rate, position limit, relocation distance, and grid step.

This is `2 x 2 x 3 x 3 x 3 x 3 x 3 = 972` planned runs.

### Inventory

| Dataset | Location | Format/rows | Projects | Cranes | Useful columns | Gaps |
|---|---|---|---|---|---|---|
| Expected Benchmark_v1 results | `CraneOptimizer.GAExperiment/ExperimentResults.csv` | Missing | Intended try4/try9 | 2 | Current writer supports 34 columns | Cannot audit or join the claimed 972 rows. |
| Analytics copy | `Analytics/datasets/ExperimentResults.csv` | Missing | Intended try4/try9 | 2 | Scripts require exactly 972 unique RunIds | Reports/scripts reference data not present. |
| Baseline results | `CraneOptimizer.GAExperiment/baselines/ExperimentResults.csv` | CSV, 8 rows | Four project files by filename only | 2 | GA settings, coverage, runtime, radius, relocation, failures | Old 29-column schema; not Benchmark_v1 factorial data. |
| Legacy run history | generated diagnostics directory | CSV, 1,015 data rows | 502 try4, 501 try9, 6 try8, 6 Try1 | 508 STC800, 507 180Tons | Coverage, counts, runtime, profiler paths | No RunId or full factorial settings; cannot identify the exact 972 safely. |
| Timestamped diagnostics | same directory | 1,011 sets | Primarily try4/try9 | 2 | Rich element/assignment evidence | Legacy schemas and no stable run join keys. |
| Project databases | external configured project paths | SQLite `.craneopt` | try4, try9; try8/Try1 also configured elsewhere | N/A | Elements, zones, normalization metadata | Snapshot/run tables are empty in try4/try9. |
| Catalog database | UI debug data folder | SQLite | N/A | 2 | Crane/spec/chart data | Only two crane models. |

Current source `ExperimentResultWriter` supports:

`RunId`, timestamp, project file/scale/element count, crane, GA settings, seed, coverage and counts,
fitness, execution time, reached generation, positions, average/maximum radius, relocation distance,
failure counts/dominance, and benchmark version.

The older eight-row baseline lacks project scale, element count, failure dominance, and benchmark
version. Existing legacy diagnostics cannot be joined safely to it or to the missing 972 rows because
the generated diagnostic headers lack `RunId`/optimization IDs and timestamps are not a guaranteed key.

The batch runner exists in `CraneOptimizer.GAExperiment/Program.cs` and supports configuration matrices,
seeds, deterministic run IDs, resume behavior, and multiple cranes. Project diversity is insufficient
for calibration:

* try8 provides a small 31-element example but only six history rows.
* try4 is medium at 196 GA-ready elements.
* Try1 provides a larger 392-element example but only six history rows.
* try9 is very large at 3,940 strict GA-ready elements.

More projects are needed within each scale and with different zone fragmentation, load profiles, and
reachability patterns. Element count alone would otherwise become a proxy for project identity.

## 10. Final Recommendation

### Are Current CSVs Sufficient?

No. They are sufficient for preliminary coverage/runtime/position analysis and some radius analysis,
but not for defensible GA mode calibration. The missing stable identities, crane limits, project
features, zone pipeline counters, and capacity-utilization inputs would force assumptions.

### Minimum Instrumentation Before Benchmark_v2

1. Propagate stable `ProjectId`, `ScenarioId`, `CraneId`, and `CraneSpecificationId`.
2. Record explicit raw, scoped, and GA-ready element counts.
3. Add a project/scope/crane feature snapshot with category, weight, zone, and grid/reachability
   aggregates.
4. Expose `MaxRadius`, `MinRadius`, `MaxBoomLength`, `MaxCapacity`, and derived footprint radius.
5. Add explicit final-lift `RequiredCapacity`, capacity margin, and the raw values needed for radius,
   boom, and capacity utilization.
6. Count candidate, valid, and rejected grid points per zone with compact rejection counters.
7. Add `StageIndex` consistently to stage-grained position, hotspot, and assignment files.
8. Generate an immutable experiment-plan file and link every completed run to `PlanRunId`.
9. Gate heavy diagnostics behind a developer diagnostics level.
10. Regenerate a clean, schema-consistent Benchmark_v2 corpus and validate all joins before analysis.

### Fields Derivable Offline

Once the minimum raw fields above exist, these should be calculated outside the optimizer:

* Coverage percent, failure count, and coverage gain.
* Coverage gain per added position.
* Total/average relocation distance under a documented distance definition.
* Used-zone count.
* Average/max/P95/stddev radius and boom length.
* Average/max/P95 capacity utilization and capacity margins.
* Radius/boom utilization and threshold flags.
* Category/weight distributions when element-scope rows or a feature snapshot are available.
* Compact solution-quality and mode-result tables.

### Defer For Now

* Persisted threshold booleans such as `IsLongRadiusLift`.
* A fixed `ProjectComplexityScore`.
* Population diversity metrics without a specific research hypothesis.
* Full rejected-grid-point row exports.
* Duplicating polygon vertices in every run.
* A user-facing Research Benchmark GA mode; use a developer diagnostics control instead.
* Jib-specific metrics until a verified jib model exists.

### Recommended Next Task

**Task title:** `Benchmark_v2 Minimal Diagnostic Contract and Stable Join Keys`

**Prompt scope:** Implement only the minimum identity, project-feature, crane-limit, final-lift
utilization-input, per-zone counter, and diagnostics-level changes listed above. Add schema tests and
generate a small two-project/two-crane smoke corpus. Do not implement GA_RunModes or choose mode
thresholds in that task.

## Exact Sources Inspected

### Documentation

* `docs/agent/start-here.md`
* `docs/agent/agent-context.md`
* `docs/project/limitations.md`
* `docs/ga/ga-settings-guide.md`
* `docs/architecture/database.md`
* `docs/domain/crane-engine.md`
* `docs/domain/units.md`
* `docs/architecture/assignment-engine.md`
* `docs/project/roadmap.md`

### Crane, Element, Zone, And Database Source

* `CraneOptimizer.Domain/Entities/Crane.cs`
* `CraneOptimizer.Domain/Entities/CraneSpecification.cs`
* `CraneOptimizer.Domain/Entities/TargetPoint.cs`
* `CraneOptimizer.Domain/Schemas/PrecastElement.cs`
* `CraneOptimizer.Domain/Diagnostics/IfcNormalizationModels.cs`
* `CraneOptimizer.Domain/CreatedProject/ProjectInfoEntity.cs`
* Project entity classes for IFC elements, crane snapshots, load-chart snapshots, zones, and runs
* `CraneOptimizer.Infrastructure/Persistence/CraneDbContext.cs`
* `CraneOptimizer.Infrastructure/Persistence/Repositories/CraneRepository.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationScopeEngine.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/ReachabilityMapService.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/LiftingCalculation/LiftingCalculationEngine.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/HungarianAssignmentService.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs`
* `CraneOptimizer.UI/Features/F8_GAOptimizer/Services/CranePlacementService.cs`

### Diagnostics, Orchestration, And Benchmark Source

* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/GACsvDiagnosticsService.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/GADiagnosticsReaderService.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/MultiStageGAOrchestrator.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs`
* `CraneOptimizer.Application/Optimization/Utilities/GAProfiler.cs`
* `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/GASettings.cs`
* `CraneOptimizer.Application.Tests/GA/GACloseOutFixTests.cs`
* `CraneOptimizer.Application.Tests/GA/MultiStageGAOrchestratorTests.cs`
* `CraneOptimizer.GAExperiment/Program.cs`
* `CraneOptimizer.GAExperiment/GAExperimentConfig.cs`
* `CraneOptimizer.GAExperiment/GAExperimentConfig.json`
* `CraneOptimizer.GAExperiment/ExperimentRunResult.cs`
* `CraneOptimizer.GAExperiment/ExperimentResultWriter.cs`
* `Analytics/scripts/dataset_loader.py`
* `Analytics/scripts/assignment_dataset_builder.py`
* `Analytics/scripts/ga_run_mode_discovery.py`
* `Analytics/scripts/solution_mode_analysis.py`
* `Analytics/scripts/dataset_capability_analysis.py`

### Databases And Tables

* Catalog `crane.db`: `Cranes`, `CraneSpecifications`, `TargetPoints`
* try4 and try9 project databases: project information/file tables, `ProjectIfcElements`, `SiteZones`,
  `ProjectCraneSnapshots`, `ProjectCraneLoadChartSnapshots`, and `ProjectOptimizationRuns`

### CSV Artifacts

* Existing generated `ga_run_history.csv`
* Latest generated `ga_diagnostics_20260605_211621.csv`
* Latest generated `ga_failure_breakdown_20260605_211621.csv`
* Latest generated `ga_assignment_matrix_20260605_211621.csv`
* Latest generated `ga_final_assignments_20260605_211621.csv`
* All generated diagnostic families for count and storage totals
* `CraneOptimizer.GAExperiment/baselines/ExperimentResults.csv`
* Expected but missing `CraneOptimizer.GAExperiment/ExperimentResults.csv`
* Expected but missing `Analytics/datasets/ExperimentResults.csv`

## Implementation Addendum: Benchmark_v2 Minimal Diagnostics

The recommended minimum instrumentation is now implemented on `enhancemnt_CSV_filesToStartModels` with compact diagnostics schema `2.0`.

Implemented:

- Stable optimization, stage, deterministic scenario, scope, and project-feature snapshot keys.
- Direct project and crane/specification identity on compact stage-grained rows.
- `ga_project_features.csv`, `ga_solution_quality_metrics.csv`, and per-zone reachability summaries.
- Final assignment capacity, margin, radius, and boom utilization inputs.
- Schema, join-key, staged-best-result, formula, and invariant-culture tests.
- A one-run `try4 / STC800 / Hungarian` smoke corpus with reachability counters.

Not implemented:

- GA_RunModes or user-facing mode selection.
- Mode thresholds, ranking policy, or GA tuning.
- Full Benchmark_v2 execution.
- Summary/research emission gating; existing heavy files remain and are documented as research outputs.

The current contract and known unavailable fields are documented in `docs/ga/benchmark-v2-diagnostics.md`.
