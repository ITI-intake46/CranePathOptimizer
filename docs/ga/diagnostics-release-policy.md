# GA Diagnostics — Release Policy

## Purpose

Control which GA diagnostics artifacts are produced in each build configuration.  
Prevents developer-only / research-level CSV output from leaking into a Release build.

## Diagnostics Level

Three levels defined in `CraneOptimizer.Application/Optimization/GeneticAlgorithms/Settings/DiagnosticsLevel.cs`:

| Level | Value | Behavior |
|---|---|---|
| `Off` | 0 | No CSV files are written. The `GenerateDiagnostics()` call is skipped entirely in `CranePlacementEngine.cs`. |
| `Summary` | 1 | **(default)** Only compact production-safe files are written. Research-level files are suppressed. |
| `Research` | 2 | All files are written (Summary + Research). Used during development and experimentation. |

## Default

`GASettings.DiagnosticsLevel` defaults to `DiagnosticsLevel.Summary` — safe for Release.

## File Classification

### Summary-level files (always written when level >= Summary)

| File | Description |
|---|---|
| `ga_run_summary.csv` | Per-run summary |
| `ga_settings_snapshot.csv` | Snapshot of GA settings |
| `ga_stage_summary.csv` | Per-stage summary |
| `ga_positions.csv` | Final crane positions |
| `ga_element_assignments.csv` | Element-to-crane assignments |
| `ga_failure_breakdown_*.csv` | Compact failure breakdown from `PlacementResult.Statistics` |
| `ga_run_history.csv` | Appended run history |

### Research-level files (written only when level == Research)

| File | Description |
|---|---|
| `ga_diagnostics_*.csv` | Per-element full diagnostic row (coordinate transforms, survey data) |
| `ga_assignment_matrix_*.csv` | Element × CandidatePoint matrix |
| `ga_final_assignments_*.csv` | Final assignment decisions |
| `ga_generation_summary.csv` | Per-generation GA state |
| `ga_reachability_hotspots.csv` | HotSpot analysis |
| `ga_project_features.csv` | Project feature snapshot |
| `ga_solution_quality_metrics.csv` | Quality metrics |
| `ga_zone_reachability_summary.csv` | Zone-level reachability |

## Profiler

- `GAProfiler.Enabled` defaults to `false`.
- Profiling is activated only when both `GASettings.EnableProfiling == true` **and** `DiagnosticsLevel >= DiagnosticsLevel.Research`.
- See `CranePlacementEngine.cs:67`.

## Gate Chain (Code Flow)

```
CranePlacementEngine.Run()
  ├─ GAProfiler.Enabled = (EnableProfiling && DiagnosticsLevel >= Research)
  ├─ if (DiagnosticsLevel != Off) → GenerateDiagnostics()
  │    ├─ GACsvDiagnosticsService:
  │    │   ├─ Diagnostics files gated by if (diagLevel >= Research)
  │    │   ├─ Failure breakdown: Research mode uses detailedRows,
  │    │   │   Summary mode uses finalResult.Statistics
  │    │   └─ BenchmarkV2CsvWriter.Write(includeResearchFiles)
  │    └─ BenchmarkV2CsvWriter:
  │        └─ Research files gated by includeResearchFiles
  └─ returns reportPath or string.Empty
```

## Developer Workflow

During development, set `DiagnosticsLevel = DiagnosticsLevel.Research` in
`GASettings` to enable full diagnostics and profiling.

Before committing / releasing, ensure `DiagnosticsLevel` is `Summary` or `Off`.

## .gitignore

The following patterns in `.gitignore` exclude generated GA artifacts from source control:

```
# GA Diagnostics
**/Reports/GA/
**/*ga_diagnostics_*.csv
**/*ga_assignment_matrix_*.csv
**/*ga_final_assignments_*.csv
**/*ga_failure_breakdown_*.csv
**/*ga_run_history.csv

# GA Profiler
**/profiling_log_*.txt
**/GAProfiler_*.log

# GAExperiment generated output
**/CraneOptimizer.GAExperiment/**/output/
**/CraneOptimizer.GAExperiment/**/Results/
**/CraneOptimizer.GAExperiment/**/Reports/

# Analytics artifacts
**/Analytics/**/*.csv
**/Analytics/**/*.json
```
