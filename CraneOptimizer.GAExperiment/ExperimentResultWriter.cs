using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.GAExperiment
{
    public class ExperimentResultWriter : IExperimentResultWriter
    {
        private static readonly SemaphoreSlim WriteLock = new(1, 1);

        private static readonly string[] Headers = new[]
        {
            "RunId",
            "Timestamp",
            "ProjectFile",
            "ProjectScale",
            "ElementCount",
            "CraneModel",
            "PopulationSize",
            "MaxGenerations",
            "MutationRate",
            "MutationSigma",
            "CrossoverRate",
            "MaxCranePositions",
            "MinimumRelocationDistance",
            "GridStep",
            "RandomSeed",
            "Coverage",
            "CoveredElements",
            "TotalElements",
            "Fitness",
            "ExecutionTimeMs",
            "GenerationReached",
            "UniqueCranePositions",
            "AverageRadius",
            "MaximumRadius",
            "RelocationDistance",
            "LiftFailures",
            "OutOfRadius",
            "Overweight",
            "HeightFail",
            "SpecialCase",
            "NotCharted",
            "FailureDominantReason",
            "FailureDominantPercent",
            "BenchmarkVersion"
        };

        public async Task WriteResultAsync(ExperimentRunResult result, string csvPath)
        {
            if (result == null || string.IsNullOrWhiteSpace(csvPath)) return;
            result.RefreshDerivedFields();

            try
            {
                await WriteLock.WaitAsync();

                int maxRetries = 10;
                int delayMs = 100;
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        bool isNewFile = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;

                        var directory = Path.GetDirectoryName(csvPath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        using (var stream = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (var writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            if (isNewFile)
                            {
                                await writer.WriteLineAsync(string.Join(",", Headers));
                            }

                            string dataLine = string.Join(",", new[]
                            {
                                EscapeCsvField(result.RunId),
                                EscapeCsvField(result.Timestamp),
                                EscapeCsvField(result.ProjectFile),
                                EscapeCsvField(result.ProjectScale),
                                result.ElementCount.ToString(CultureInfo.InvariantCulture),
                                EscapeCsvField(result.CraneModel),
                                result.PopulationSize.ToString(CultureInfo.InvariantCulture),
                                result.MaxGenerations.ToString(CultureInfo.InvariantCulture),
                                result.MutationRate.ToString(CultureInfo.InvariantCulture),
                                result.MutationSigma.ToString(CultureInfo.InvariantCulture),
                                result.CrossoverRate.ToString(CultureInfo.InvariantCulture),
                                result.MaxCranePositions.ToString(CultureInfo.InvariantCulture),
                                result.MinimumRelocationDistance.ToString(CultureInfo.InvariantCulture),
                                result.GridStep.ToString(CultureInfo.InvariantCulture),
                                result.RandomSeed.ToString(CultureInfo.InvariantCulture),
                                result.Coverage.ToString(CultureInfo.InvariantCulture),
                                result.CoveredElements.ToString(CultureInfo.InvariantCulture),
                                result.TotalElements.ToString(CultureInfo.InvariantCulture),
                                result.Fitness.ToString(CultureInfo.InvariantCulture),
                                result.ExecutionTimeMs.ToString(CultureInfo.InvariantCulture),
                                result.GenerationReached.ToString(CultureInfo.InvariantCulture),
                                result.UniqueCranePositions.ToString(CultureInfo.InvariantCulture),
                                result.AverageRadius.ToString(CultureInfo.InvariantCulture),
                                result.MaximumRadius.ToString(CultureInfo.InvariantCulture),
                                result.RelocationDistance.ToString(CultureInfo.InvariantCulture),
                                result.LiftFailures.ToString(CultureInfo.InvariantCulture),
                                result.OutOfRadius.ToString(CultureInfo.InvariantCulture),
                                result.Overweight.ToString(CultureInfo.InvariantCulture),
                                result.HeightFail.ToString(CultureInfo.InvariantCulture),
                                result.SpecialCase.ToString(CultureInfo.InvariantCulture),
                                result.NotCharted.ToString(CultureInfo.InvariantCulture),
                                EscapeCsvField(result.FailureDominantReason),
                                result.FailureDominantPercent.ToString("F1", CultureInfo.InvariantCulture),
                                EscapeCsvField(result.BenchmarkVersion)
                            });

                            await writer.WriteLineAsync(dataLine);
                            await writer.FlushAsync();
                        }

                        // Success, exit the retry loop
                        break;
                    }
                    catch (IOException ex) when (retry < maxRetries - 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[Warning] File lock encountered on {csvPath}. Retrying in {delayMs}ms ({retry + 1}/{maxRetries}). Error: {ex.Message}");
                        Console.ResetColor();
                        await Task.Delay(delayMs);
                        delayMs *= 2; // exponential backoff
                    }
                }
            }
            finally
            {
                WriteLock.Release();
            }
        }

        public async Task<HashSet<string>> ReadExistingRunIdsAsync(string csvPath)
        {
            var runIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(csvPath)) return runIds;

            try
            {
                await WriteLock.WaitAsync();

                using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? header = await reader.ReadLineAsync();
                if (header == null) return runIds;

                while (await reader.ReadLineAsync() is string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        runIds.Add(parts[0].Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to read existing RunIds from {csvPath}: {ex.Message}");
            }
            finally
            {
                WriteLock.Release();
            }

            return runIds;
        }

        public async Task WriteSummaryAsync(IEnumerable<ExperimentRunResult> results, string summaryPath)
        {
            if (results == null || string.IsNullOrWhiteSpace(summaryPath)) return;

            try
            {
                await WriteLock.WaitAsync();

                var directory = Path.GetDirectoryName(summaryPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var list = results.ToList();
                var summaryHeaders = new[]
                {
                    "BenchmarkVersion",
                    "CraneModel",
                    "BestCoverage",
                    "BestFitness",
                    "AverageCoverage",
                    "AverageRuntimeMs",
                    "BestParameterCombination"
                };

                using var stream = new FileStream(summaryPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, Encoding.UTF8);

                await writer.WriteLineAsync(string.Join(",", summaryHeaders));

                var groups = list.GroupBy(r => new { 
                    BenchmarkVersion = string.IsNullOrWhiteSpace(r.BenchmarkVersion) ? "Benchmark_v1" : r.BenchmarkVersion, 
                    r.CraneModel 
                });

                foreach (var group in groups)
                {
                    var benchmarkVersion = group.Key.BenchmarkVersion;
                    var craneModel = group.Key.CraneModel;
                    var bestCoverage = group.Max(r => r.Coverage);
                    var bestFitness = group.Max(r => r.Fitness);
                    var averageCoverage = group.Average(r => r.Coverage);
                    var averageRuntime = group.Average(r => r.ExecutionTimeMs);

                    // Highest fitness, tie broken by shortest runtime
                    var bestRun = group
                        .OrderByDescending(r => r.Fitness)
                        .ThenBy(r => r.ExecutionTimeMs)
                        .First();

                    string bestParams = $"Pop={bestRun.PopulationSize};Gen={bestRun.MaxGenerations};Mut={bestRun.MutationRate:F3};Sigma={bestRun.MutationSigma:F3};Cross={bestRun.CrossoverRate:F3};Pos={bestRun.MaxCranePositions};Reloc={bestRun.MinimumRelocationDistance:F1};Grid={bestRun.GridStep:F2};Seed={bestRun.RandomSeed}";

                    string summaryLine = string.Join(",", new[]
                    {
                        EscapeCsvField(benchmarkVersion),
                        EscapeCsvField(craneModel),
                        bestCoverage.ToString("F4", CultureInfo.InvariantCulture),
                        bestFitness.ToString("F4", CultureInfo.InvariantCulture),
                        averageCoverage.ToString("F4", CultureInfo.InvariantCulture),
                        averageRuntime.ToString("F1", CultureInfo.InvariantCulture),
                        EscapeCsvField(bestParams)
                    });

                    await writer.WriteLineAsync(summaryLine);
                }

                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to generate ExperimentSummary: {ex.Message}");
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool containsSpecialChar = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
            if (containsSpecialChar)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
