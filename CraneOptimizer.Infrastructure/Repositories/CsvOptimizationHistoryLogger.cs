using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Infrastructure.Repositories;

public sealed class CsvOptimizationHistoryLogger : IOptimizationHistoryLogger, IDisposable
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task LogRunAsync(OptimizationRunEntry entry, string projectFolderPath)
    {
        if (entry == null || string.IsNullOrWhiteSpace(projectFolderPath))
        {
            return;
        }

        try
        {
            await _writeLock.WaitAsync();

            string filePath = Path.Combine(projectFolderPath, "OptimizationHistory.csv");
            bool isNewFile = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

            // Open stream with UTF-8 encoding and append mode
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            if (isNewFile)
            {
                string header = string.Join(",", new[]
                {
                    "Timestamp",
                    "ProjectName",
                    "IfcFileName",
                    "CraneModel",
                    "PopulationSize",
                    "MaxGenerations",
                    "MutationRate",
                    "CrossoverRate",
                    "EliteCount",
                    "TournamentSize",
                    "ConvergenceThreshold",
                    "EarlyStopGenerations",
                    "WireLength",
                    "AdditionalWt",
                    "SafetyFactor",
                    "DeratingRatio",
                    "MaxCranePositions",
                    "MinimumRelocationDistance",
                    "MutationSigma",
                    "GridStep",
                    "CandidatePointsGenerated",
                    "ValidPointsGenerated",
                    "FinalSolutionsCount",
                    "BestFitness",
                    "CoveragePercent",
                    "ExecutionTimeMs",
                    "TotalElements",
                    "LiftableElements",
                    "UnliftableElements",
                    "BestPointX",
                    "BestPointY",
                    "BestPointZ",
                    "ElementsLiftedByBestPoint",
                    "CoveragePercentBestPoint",
                    "AverageRadius",
                    "MaxRadius",
                    "GenerationReached",
                    "StoppedByConvergence",
                    "StoppedByMaxGenerations"
                });
                await writer.WriteLineAsync(header);
            }

            string dataLine = string.Join(",", new[]
            {
                EscapeCsvField(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                EscapeCsvField(entry.ProjectName),
                EscapeCsvField(entry.IfcFileName),
                EscapeCsvField(entry.CraneModel),
                entry.PopulationSize.ToString(CultureInfo.InvariantCulture),
                entry.MaxGenerations.ToString(CultureInfo.InvariantCulture),
                entry.MutationRate.ToString(CultureInfo.InvariantCulture),
                entry.CrossoverRate.ToString(CultureInfo.InvariantCulture),
                entry.EliteCount.ToString(CultureInfo.InvariantCulture),
                entry.TournamentSize.ToString(CultureInfo.InvariantCulture),
                entry.ConvergenceThreshold.ToString(CultureInfo.InvariantCulture),
                entry.EarlyStopGenerations.ToString(CultureInfo.InvariantCulture),
                entry.WireLength.ToString(CultureInfo.InvariantCulture),
                entry.AdditionalWt.ToString(CultureInfo.InvariantCulture),
                entry.SafetyFactor.ToString(CultureInfo.InvariantCulture),
                entry.DeratingRatio.ToString(CultureInfo.InvariantCulture),
                entry.MaxCranePositions.ToString(CultureInfo.InvariantCulture),
                entry.MinimumRelocationDistance.ToString(CultureInfo.InvariantCulture),
                entry.MutationSigma.ToString(CultureInfo.InvariantCulture),
                entry.GridStep.ToString(CultureInfo.InvariantCulture),
                entry.CandidatePointsGenerated.ToString(CultureInfo.InvariantCulture),
                entry.ValidPointsGenerated.ToString(CultureInfo.InvariantCulture),
                entry.FinalSolutionsCount.ToString(CultureInfo.InvariantCulture),
                entry.BestFitness.ToString(CultureInfo.InvariantCulture),
                entry.CoveragePercent.ToString(CultureInfo.InvariantCulture),
                entry.ExecutionTimeMs.ToString(CultureInfo.InvariantCulture),
                entry.TotalElements.ToString(CultureInfo.InvariantCulture),
                entry.LiftableElements.ToString(CultureInfo.InvariantCulture),
                entry.UnliftableElements.ToString(CultureInfo.InvariantCulture),
                entry.BestPointX.ToString(CultureInfo.InvariantCulture),
                entry.BestPointY.ToString(CultureInfo.InvariantCulture),
                entry.BestPointZ.ToString(CultureInfo.InvariantCulture),
                entry.ElementsLiftedByBestPoint.ToString(CultureInfo.InvariantCulture),
                entry.CoveragePercentBestPoint.ToString(CultureInfo.InvariantCulture),
                entry.AverageRadius.ToString(CultureInfo.InvariantCulture),
                entry.MaxRadius.ToString(CultureInfo.InvariantCulture),
                entry.GenerationReached.ToString(CultureInfo.InvariantCulture),
                entry.StoppedByConvergence ? "1" : "0",
                entry.StoppedByMaxGenerations ? "1" : "0"
            });

            await writer.WriteLineAsync(dataLine);
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            // Failsafe catch block to guarantee a failed history write never crashes the UI thread.
            System.Diagnostics.Debug.WriteLine($"[History Logger Error] Failed to write run log: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
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

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
