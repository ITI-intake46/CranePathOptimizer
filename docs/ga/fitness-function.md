# Fitness Diagnostics Reference

The implemented fitness behavior is documented in [architecture/fitness-function.md](../architecture/fitness-function.md).

Benchmark_v2 records `FitnessScore`, `CoverageDominanceMultiplier`, coverage ratio/percent, stage coverage gain, and relocation metrics without changing the fitness formula. Raw fitness is an internal GA score and must not be interpreted as coverage.

Future Balanced Coverage and Minimum Relocation analysis may compare these recorded fields. No GA_RunModes, mode weights, or thresholds are implemented.

See [Benchmark_v2 diagnostics](benchmark-v2-diagnostics.md) for schema details.
