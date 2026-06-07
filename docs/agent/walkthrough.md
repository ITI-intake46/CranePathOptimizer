# Agent Walkthrough

## Current Benchmark_v2 Diagnostics Flow

1. UI or GAExperiment builds `PlacementInput` with project identity, raw/scoped/GA-ready counts, feature-scope elements, crane specification, and settings.
2. `BenchmarkDiagnosticsIdentity.Ensure` derives deterministic scenario, scope, and feature-snapshot keys when callers did not supply them.
3. Multi-stage orchestration preserves `OptimizationRunId`, creates a unique `StageRunId`, and assigns a one-based `StageIndex`.
4. Existing reachability calculation records compact global and per-zone counters plus actual selected hotspot seeds.
5. Existing optimization and assignment behavior runs unchanged.
6. `GACsvDiagnosticsService` preserves the legacy detailed files and delegates compact schema 2.0 output to `BenchmarkV2CsvWriter`.
7. Contract tests validate CSV shape, unique headers, stable joins, stage identity, formulas, invariant culture, and best-stage selection.

The schema reference and unavailable-field notes are in [Benchmark_v2 diagnostics](../ga/benchmark-v2-diagnostics.md).

GA_RunModes and mode thresholds are not implemented.
