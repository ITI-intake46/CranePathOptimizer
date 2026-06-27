# Assignment Diagnostics Reference

The implemented assignment engines and runtime routing are documented in [architecture/assignment-engine.md](../architecture/assignment-engine.md).

Benchmark_v2 records `UseHungarianAssignment` and `AssignmentEngineUsed` on relevant compact rows. Element assignments also expose weight, required capacity, chart/available capacity, margins, radius utilization, and boom utilization using the formulas in [Benchmark_v2 diagnostics](benchmark-v2-diagnostics.md).

These fields support later Crane-Friendly analysis. No crane-friendly threshold or GA_RunMode is implemented.
