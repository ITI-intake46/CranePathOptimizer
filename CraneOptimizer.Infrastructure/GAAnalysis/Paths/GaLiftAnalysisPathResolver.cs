using System.IO;
using System.Text.RegularExpressions;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Services;

namespace CraneOptimizer.Infrastructure.GAAnalysis;

/// <summary>
/// Resolves analysis paths without creating directories or otherwise mutating the project.
/// </summary>
public sealed class GaLiftAnalysisPathResolver : IGaLiftAnalysisPathResolver
{
    private static readonly Regex RunIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedDeviceNames =
        new(
        [
            "CON", "PRN", "AUX", "NUL",
            "CLOCK$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly IActiveProjectPathProvider _activeProjectPathProvider;

    public GaLiftAnalysisPathResolver(IActiveProjectPathProvider activeProjectPathProvider)
    {
        _activeProjectPathProvider = activeProjectPathProvider;
    }

    public GaLiftAnalysisPaths Resolve(string projectGuid, string optimizationRunId)
    {
        if (string.IsNullOrWhiteSpace(projectGuid))
            throw new ArgumentException("ProjectGuid is required.", nameof(projectGuid));
        ValidateRunId(optimizationRunId);

        var activeRoot = _activeProjectPathProvider.GetActiveProjectPath();
        if (string.IsNullOrWhiteSpace(activeRoot))
            throw new InvalidOperationException("No active project folder is available.");

        var projectRoot = Path.GetFullPath(activeRoot);
        var analysisRoot = ContainedCombine(projectRoot, "Reports", "Analysis");
        var runDirectory = ContainedCombine(analysisRoot, optimizationRunId);
        return new GaLiftAnalysisPaths(
            projectRoot,
            runDirectory,
            ContainedCombine(runDirectory, JsonGaLiftAnalysisStore.AnalysisFileName),
            ContainedCombine(runDirectory, JsonGaLiftAnalysisStore.ChecksumFileName),
            ContainedCombine(analysisRoot, JsonGaLiftAnalysisStore.LatestManifestFileName),
            ContainedCombine(runDirectory, "History"));
    }

    public static void ValidateRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || !RunIdPattern.IsMatch(value)
            || value is "." or ".."
            || value.EndsWith('.') || value.EndsWith(' '))
        {
            throw new ArgumentException("OptimizationRunId is not a valid path segment.", nameof(value));
        }

        var baseName = value.Split('.', 2)[0];
        if (ReservedDeviceNames.Contains(baseName))
            throw new ArgumentException("OptimizationRunId uses a reserved Windows device name.", nameof(value));
    }

    private static string ContainedCombine(string root, params string[] segments)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine([root, .. segments]));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved analysis path escapes the active project folder.");
        return candidate;
    }
}
