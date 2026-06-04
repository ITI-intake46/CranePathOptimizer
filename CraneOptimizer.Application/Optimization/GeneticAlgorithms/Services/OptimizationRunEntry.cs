using System;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

public sealed class OptimizationRunEntry
{
    // --- Project context ---
    public DateTime Timestamp           { get; init; }
    public string   ProjectName         { get; init; } = string.Empty;
    public string   IfcFileName         { get; init; } = string.Empty;
    public string   CraneModel          { get; init; } = string.Empty;

    // --- GA settings snapshot ---
    public int      PopulationSize           { get; init; }
    public int      MaxGenerations           { get; init; }
    public double   MutationRate             { get; init; }
    public double   CrossoverRate            { get; init; }
    public int      EliteCount               { get; init; }   // computed: (int)(EliteFraction * PopulationSize)
    public int      TournamentSize           { get; init; }
    public double   ConvergenceThreshold     { get; init; }
    public int      EarlyStopGenerations     { get; init; }
    public double   WireLength               { get; init; }
    public double   AdditionalWt             { get; init; }
    public double   SafetyFactor             { get; init; }
    public double   DeratingRatio            { get; init; }
    public int      MaxCranePositions        { get; init; }
    public double   MinimumRelocationDistance { get; init; }
    public double   MutationSigma            { get; init; }
    public double   GridStep                 { get; init; }

    // --- Run output ---
    public int      CandidatePointsGenerated { get; init; }
    public int      ValidPointsGenerated     { get; init; }
    public int      FinalSolutionsCount      { get; init; }
    public double   BestFitness              { get; init; }
    public double   CoveragePercent          { get; init; }
    public long     ExecutionTimeMs          { get; init; }

    // --- Element statistics ---
    public int      TotalElements      { get; init; }
    public int      LiftableElements   { get; init; }
    public int      UnliftableElements { get; init; }

    // --- Best solution geometry ---
    public double   BestPointX                  { get; init; }
    public double   BestPointY                  { get; init; }
    public double   BestPointZ                  { get; init; }
    public int      ElementsLiftedByBestPoint   { get; init; }
    public double   CoveragePercentBestPoint    { get; init; }
    public double   AverageRadius               { get; init; }
    public double   MaxRadius                   { get; init; }

    // --- Termination indicators ---
    public int      GenerationReached           { get; init; }
    public bool     StoppedByConvergence        { get; init; }
    public bool     StoppedByMaxGenerations     { get; init; }
}
