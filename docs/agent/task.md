# Current Task

## Benchmark_v2 Minimal Diagnostic Contract

Implemented:

- Stable optimization, stage, scenario, project-feature, and scope keys.
- Project/scope/crane feature snapshots.
- Stage solution-quality metrics.
- Position and element-assignment crane stress metrics.
- Reachability hotspot provenance and per-zone counters.
- Schema 2.0 CSV integrity checks and staged join-key tests.
- Isolated GAExperiment smoke configuration for `try4 / STC800 / Hungarian`.

Explicitly deferred:

- GA_RunModes and user-facing mode selection.
- Mode thresholds and candidate ranking policy.
- GA parameter tuning or search changes.
- Full benchmark execution.
- Diagnostics-level gating; heavy outputs are documented as research files.

See [Benchmark_v2 diagnostics](../ga/benchmark-v2-diagnostics.md) for the contract.
