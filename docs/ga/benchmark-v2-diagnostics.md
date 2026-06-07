# Benchmark_v2 Minimal Diagnostics Contract

Status: implemented on `enhancemnt_CSV_filesToStartModels`.

This contract exposes the minimum project, crane, stage, reachability, assignment, and solution-quality data needed for later Benchmark_v2 analysis. It does not implement GA_RunModes, user-facing modes, mode thresholds, or parameter tuning.

## Stable Join Keys

| Key | Meaning |
|---|---|
| `OptimizationRunId` | One user or headless optimization execution. Stable across all stages in that execution. |
| `StageRunId` | One optimization stage. Unique within an optimization execution. |
| `StageIndex` | One-based stage ordinal. Current multi-stage orchestration uses 1, 2, and 3 for its successive stages; a single-stage run uses 1. It is not a mode identifier. |
| `ScenarioId` | Deterministic `SCN-` hash of stable project, IFC, scope, crane/specification, grid, target-type, and requested-position inputs. It never uses a timestamp. |
| `ProjectFeatureSnapshotId` | Deterministic `PFS-` hash for project/scope/crane/grid/footprint features. One row is written per optimization run and snapshot. |
| `ScopeDefinitionHash` | Deterministic `SCP-` hash of the optimization-scope element identities. |

`DiagnosticsSchemaVersion` is `2.0` because the compact schemas materially changed. Existing timestamped legacy/research files retain their existing format.

## Compact Files

| File | Grain and purpose |
|---|---|
| `ga_run_summary.csv` | One row per stage result; top-level project, crane, coverage, runtime, and termination identity. |
| `ga_settings_snapshot.csv` | One row per stage; complete active GA, fitness, safety, crane, scope, and identity settings. |
| `ga_stage_summary.csv` | One row per stage; escalation analysis, stage gain, used zones, assignment engine, and runtime. |
| `ga_generation_summary.csv` | One row per generation when profiler generation logs exist. |
| `ga_project_features.csv` | One row per optimization run plus feature snapshot; project size, scope, load, geometry, crane, zones, and reachability profile. |
| `ga_solution_quality_metrics.csv` | One row per stage result; coverage, relocation, crane stress, failures, runtime, and `IsBestStage`. |
| `ga_positions.csv` | One row per final crane position. |
| `ga_element_assignments.csv` | One row per GA-ready element with its final or best failed evaluation. |
| `ga_reachability_hotspots.csv` | One row per retained hotspot when reachability ran. |
| `ga_zone_reachability_summary.csv` | One row per allowed zone when reachability ran. |

All compact rows carry direct project/scenario and crane/specification identity where the row is stage-grained. Empty row families still create a header-only file.

## Engineering Formulas

For an element assignment:

```text
RequiredCapacity = ElementWeight + AdditionalWt
ChartCapacity = AvailableCapacity / (DeratingRatio * SafetyFactor)
CapacityUtilization = RequiredCapacity / AvailableCapacity
CapacityMarginTons = AvailableCapacity - RequiredCapacity
RadiusUtilization = Radius / CraneMaxRadius
BoomUtilization = RequiredBoomLength / CraneMaxBoomLength
```

Utilization is `N/A` when its denominator is not positive. Numeric CSV output uses `CultureInfo.InvariantCulture`.

Relocation is the 3D polyline distance between adjacent final positions ordered by `PositionIndex`. Coverage ratio remains in `[0, 1]`; coverage percent is ratio multiplied by 100.

## Reachability Counters

Candidate points are unique grid coordinates that fall inside the first matching allowed polygon. Valid points also pass setup-point constraints. Rejected points are candidates that fail those constraints. Per-zone rows include candidate, valid, rejected, zero-reachability, max/mean/standard-deviation, and selected-seed counts.

The optimizer's existing reachability pass is instrumented; diagnostics do not trigger a second expensive pass.

## Heavy Output Guidance

There is not yet a public `Summary`/`Research` diagnostics-level setting. Existing detailed files remain emitted to preserve behavior:

- `ga_diagnostics_<timestamp>.csv`
- `ga_assignment_matrix_<timestamp>.csv`
- `ga_final_assignments_<timestamp>.csv`
- `ga_failure_breakdown_<timestamp>.csv`
- profiler logs and run history

Treat these as research/heavy outputs. Benchmark aggregation should start from the compact files. A future diagnostics-level setting may gate them, but this task deliberately did not change emission defaults.

## Known Unavailable Fields

- `UnreachableCount` is `N/A`: the current lift-status model has no independent unreachable category.
- Rejection reason splits are `N/A`: current setup validation returns a combined validity result. `RejectedOtherCount` carries the total rejected count.
- Allowed area uses `SumOfAllowedZoneAreas_NoOverlapUnion`; overlapping polygons are not unioned.
- Height uses `ElementHeight` when available and bounding-box height as fallback.
- No IFC file hash is calculated because no cheap existing hash is available.

## Later Analysis

- Balanced Coverage: coverage, stage gain, fitness, position contribution, and used zones.
- Minimum Relocation: position count and adjacent-position relocation metrics.
- Crane-Friendly: radius, boom, capacity utilization, margins, and failure counts.
- Research Benchmark: generation summaries, hotspots, zone starvation counters, and existing heavy files.

These are analysis inputs only. GA_RunModes are not implemented and no thresholds have been selected.
