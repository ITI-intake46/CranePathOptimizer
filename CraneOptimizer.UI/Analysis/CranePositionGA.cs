using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Single-objective GA for crane position optimisation.
    /// Inspired by NSGA-II but simplified to one objective (minimise Lift Score).
    ///
    /// Design variables: index into the pre-sampled valid candidate list.
    /// A chromosome is (candidateIndex, liftScore).
    /// </summary>
    public sealed class CranePositionGA
    {
        private readonly Random _rng;

        public CranePositionGA(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // ── Public entry point ─────────────────────────────────────────────

        /// <summary>
        /// Runs the GA synchronously (call from Task.Run for async UI).
        /// </summary>
        /// <param name="candidates">Valid crane positions from <see cref="AllowedZoneSampler"/>.</param>
        /// <param name="elements">IFC elements to include in fitness evaluation.</param>
        /// <param name="crane">Crane instance whose position will be optimised.</param>
        /// <param name="chartPoints">Load-chart data points for the crane's assigned chart.</param>
        /// <param name="settings">Algorithm + penalty parameters.</param>
        /// <param name="progress">Optional progress reporter (called once per generation).</param>
        /// <param name="ct">Cancellation token.</param>
        public OptimizerResult Run(
            List<Point>                     candidates,
            List<IfcElementModel>           elements,
            CraneInstance                   crane,
            List<CraneLoadChartPointEntity> chartPoints,
            OptimizerSettings               settings,
            IProgress<OptimizerProgress>    progress = null,
            CancellationToken               ct       = default)
        {
            if (candidates == null || candidates.Count == 0)
                return FailResult("No valid crane positions found in Allowed zones.", settings, elements);

            int  n          = candidates.Count;
            int  popSize    = Math.Min(settings.PopulationSize, n);
            int  maxGen     = settings.MaxGenerations;
            int  maxPenalty = settings.MaxPenalty(elements.Count);

            // ── Initial population: random indices ─────────────────────────────
            var population = new List<Chromosome>(popSize);
            var shuffled   = Enumerable.Range(0, n).OrderBy(_ => _rng.Next()).Take(popSize).ToList();
            foreach (int idx in shuffled)
            {
                var (score, nL, nNL, nUR) = Evaluate(idx, candidates, elements, crane, chartPoints, settings);
                population.Add(new Chromosome(idx, score, nL, nNL, nUR));
            }

            population = population.OrderBy(c => c.Score).ToList();

            double   bestScore         = population[0].Score;
            var      genBestScores     = new List<double>(maxGen) { bestScore };
            int      stagnantGens      = 0;
            int      generation        = 0;
            string   stopReason        = "Max generations reached";

            // ── GA loop ────────────────────────────────────────────────────────
            for (generation = 1; generation <= maxGen; generation++)
            {
                ct.ThrowIfCancellationRequested();

                // Selection: top 50%
                int parentCount = Math.Max(2, popSize / 2);
                var parents     = population.Take(parentCount).ToList();

                // Crossover + mutation → fill the rest of the population
                var offspring = new List<Chromosome>(popSize - parentCount);
                while (offspring.Count < popSize - parentCount)
                {
                    var p1  = parents[_rng.Next(parentCount)];
                    var p2  = parents[_rng.Next(parentCount)];
                    double alpha = _rng.NextDouble();

                    // Blend indices in real-valued UV space [0, n-1]
                    double c1Idx = alpha       * p1.Idx + (1 - alpha) * p2.Idx;
                    double c2Idx = (1 - alpha) * p1.Idx + alpha       * p2.Idx;

                    // Mutation
                    if (_rng.NextDouble() < settings.MutationRate)
                        c1Idx += GaussianNoise() * settings.MutationSigma * n;
                    if (_rng.NextDouble() < settings.MutationRate)
                        c2Idx += GaussianNoise() * settings.MutationSigma * n;

                    int idx1 = Clamp((int)Math.Round(c1Idx), 0, n - 1);
                    int idx2 = Clamp((int)Math.Round(c2Idx), 0, n - 1);

                    foreach (int idx in new[] { idx1, idx2 })
                    {
                        if (offspring.Count >= popSize - parentCount) break;
                        var (score, nL, nNL, nUR) = Evaluate(idx, candidates, elements, crane, chartPoints, settings);
                        offspring.Add(new Chromosome(idx, score, nL, nNL, nUR));
                    }
                }

                // Combine, sort, keep best popSize
                population = parents
                    .Concat(offspring)
                    .OrderBy(c => c.Score)
                    .Take(popSize)
                    .ToList();

                double genBest = population[0].Score;
                genBestScores.Add(genBest);

                // Convergence check — only active when ConvergenceThresholdPct > 0.
                // Set threshold = 0 in the UI to always run all MaxGenerations.
                if (settings.ConvergenceThresholdPct > 0 && generation >= 5)
                {
                    double prevBest = genBestScores[genBestScores.Count - 6]; // 5 gens ago
                    double improvement = prevBest > 0
                        ? 100.0 * Math.Abs(prevBest - genBest) / prevBest
                        : 0;

                    if (improvement < settings.ConvergenceThresholdPct)
                    {
                        stagnantGens++;
                        if (stagnantGens >= 5)
                        {
                            stopReason = $"Converged at generation {generation} (improvement < {settings.ConvergenceThresholdPct:F2}%)";
                            break;
                        }
                    }
                    else
                    {
                        stagnantGens = 0;
                    }
                }

                bestScore = genBest;

                // Report progress
                progress?.Report(new OptimizerProgress
                {
                    Generation       = generation,
                    MaxGenerations   = maxGen,
                    BestScore        = genBest,
                    CountLiftable    = population[0].NLiftable,
                    CountNonLiftable = population[0].NNonLiftable,
                    CountUnreachable = population[0].NUnreachable
                });
            }

            // ── Build final result ─────────────────────────────────────────────
            var best = population[0];
            var bestPos = candidates[best.Idx];

            // Re-run analysis at best position to get per-element results
            var finalResults = LiftAnalysisEngine.AnalyzeAll(
                elements, bestPos.X, bestPos.Y, crane, chartPoints, settings.MinClearanceM);

            LiftScoreCalculator.Calculate(finalResults,
                settings.PenaltyNonLiftable, settings.PenaltyUnreachable,
                out int fL, out int fNL, out int fUR);

            return new OptimizerResult
            {
                BestX               = bestPos.X,
                BestY               = bestPos.Y,
                BestScore           = best.Score,
                CountLiftable       = fL,
                CountNonLiftable    = fNL,
                CountUnreachable    = fUR,
                GenerationsRun      = generation,
                StopReason          = stopReason,
                ElementResults      = finalResults,
                GenerationBestScores = genBestScores
            };
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private (double score, int nL, int nNL, int nUR) Evaluate(
            int                             idx,
            List<Point>                     candidates,
            List<IfcElementModel>           elements,
            CraneInstance                   crane,
            List<CraneLoadChartPointEntity> chartPoints,
            OptimizerSettings               settings)
        {
            var pos     = candidates[idx];
            var results = LiftAnalysisEngine.AnalyzeAll(
                elements, pos.X, pos.Y, crane, chartPoints, settings.MinClearanceM);
            double score = LiftScoreCalculator.Calculate(
                results,
                settings.PenaltyNonLiftable,
                settings.PenaltyUnreachable,
                out int nL, out int nNL, out int nUR);
            return (score, nL, nNL, nUR);
        }

        private double GaussianNoise()
        {
            // Box–Muller transform
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        private static int Clamp(int v, int lo, int hi) =>
            v < lo ? lo : v > hi ? hi : v;

        private static OptimizerResult FailResult(
            string reason, OptimizerSettings s, List<IfcElementModel> elements)
        {
            return new OptimizerResult
            {
                BestX            = 0,
                BestY            = 0,
                BestScore        = s.MaxPenalty(elements.Count),
                CountUnreachable = elements.Count,
                GenerationsRun   = 0,
                StopReason       = reason,
                ElementResults   = new List<ElementLiftResult>(),
                GenerationBestScores = new List<double>()
            };
        }

        // ── Chromosome ─────────────────────────────────────────────────────────
        private sealed class Chromosome
        {
            public int    Idx          { get; }
            public double Score        { get; }
            public int    NLiftable    { get; }
            public int    NNonLiftable { get; }
            public int    NUnreachable { get; }

            public Chromosome(int idx, double score, int nL, int nNL, int nUR)
            {
                Idx          = idx;
                Score        = score;
                NLiftable    = nL;
                NNonLiftable = nNL;
                NUnreachable = nUR;
            }
        }
    }
}
