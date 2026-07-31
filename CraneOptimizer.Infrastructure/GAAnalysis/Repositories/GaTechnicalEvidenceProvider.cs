using System.IO;
using System.Security.Cryptography;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Services;

namespace CraneOptimizer.Infrastructure.GAAnalysis;

/// <summary>
/// Optionally inventories existing GA diagnostic files. It never generates diagnostics
/// and never changes the canonical snapshot used for the engineering conclusions.
/// </summary>
public sealed class GaTechnicalEvidenceProvider : IGaTechnicalEvidenceProvider
{
    private readonly IActiveProjectPathProvider _activeProjectPathProvider;

    public GaTechnicalEvidenceProvider(IActiveProjectPathProvider activeProjectPathProvider)
    {
        _activeProjectPathProvider = activeProjectPathProvider;
    }

    public async Task<IReadOnlyList<GaTechnicalEvidenceFile>> CollectAsync(
        GaResultSnapshotDto snapshot,
        CancellationToken cancellationToken = default)
    {
        if (snapshot.Diagnostics.ProjectRelativeFiles.Count == 0)
            return Array.Empty<GaTechnicalEvidenceFile>();

        var activeRoot = _activeProjectPathProvider.GetActiveProjectPath();
        if (string.IsNullOrWhiteSpace(activeRoot))
            return snapshot.Diagnostics.ProjectRelativeFiles
                .Select(path => Missing(path, "No active project folder is available."))
                .ToArray();

        var root = Path.GetFullPath(activeRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var results = new List<GaTechnicalEvidenceFile>();
        foreach (var relativePath in snapshot.Diagnostics.ProjectRelativeFiles
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            }
            catch
            {
                results.Add(Missing(relativePath, "The evidence path is invalid."));
                continue;
            }

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(Missing(relativePath, "The evidence path escapes the active project folder."));
                continue;
            }
            if (!File.Exists(fullPath))
            {
                results.Add(Missing(relativePath, "The referenced evidence file is missing."));
                continue;
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            results.Add(new GaTechnicalEvidenceFile
            {
                ProjectRelativePath = relativePath.Replace('\\', '/'),
                Exists = true,
                SizeBytes = stream.Length,
                Sha256 = Convert.ToHexString(hash).ToLowerInvariant()
            });
        }
        return results;
    }

    private static GaTechnicalEvidenceFile Missing(string path, string warning) => new()
    {
        ProjectRelativePath = path.Replace('\\', '/'),
        Exists = false,
        Warning = warning
    };
}
