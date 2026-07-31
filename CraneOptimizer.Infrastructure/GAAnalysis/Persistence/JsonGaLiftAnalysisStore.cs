using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CraneOptimizer.Application.GAAnalysis;

namespace CraneOptimizer.Infrastructure.GAAnalysis;

public sealed class JsonGaLiftAnalysisStore : IGaLiftAnalysisStore
{
    public const string AnalysisFileName = "ga_lift_performance_analysis.json";
    public const string ChecksumFileName = "ga_lift_performance_analysis.sha256";
    public const string LatestManifestFileName = "latest_analysis.json";

    private readonly IGaLiftAnalysisPathResolver _pathResolver;
    private readonly IGaLiftAnalysisSerializer _serializer;

    public JsonGaLiftAnalysisStore(
        IGaLiftAnalysisPathResolver pathResolver,
        IGaLiftAnalysisSerializer serializer)
    {
        _pathResolver = pathResolver;
        _serializer = serializer;
    }

    public async Task SaveAsync(GaLiftAnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        Validate(analysis);
        var paths = _pathResolver.Resolve(analysis.ProjectGuid, analysis.OptimizationRunId);
        Directory.CreateDirectory(paths.RunDirectory);

        var existing = await LoadVerifiedAsync(paths.AnalysisPath, paths.ChecksumPath, cancellationToken)
            .ConfigureAwait(false);
        if (existing != null && !string.Equals(existing.RevisionId, analysis.RevisionId, StringComparison.Ordinal))
            await ArchiveAsync(existing, paths, cancellationToken).ConfigureAwait(false);
        else if (existing == null && File.Exists(paths.AnalysisPath))
            await ArchiveCorruptCurrentAsync(paths, cancellationToken).ConfigureAwait(false);

        var previousAnalysisBytes = File.Exists(paths.AnalysisPath)
            ? await File.ReadAllBytesAsync(paths.AnalysisPath, cancellationToken).ConfigureAwait(false)
            : null;
        var previousChecksumBytes = File.Exists(paths.ChecksumPath)
            ? await File.ReadAllBytesAsync(paths.ChecksumPath, cancellationToken).ConfigureAwait(false)
            : null;
        var json = _serializer.Serialize(analysis);
        var hash = Sha256(json);
        try
        {
            await AtomicWriteBytesAsync(paths.AnalysisPath, json, cancellationToken).ConfigureAwait(false);
            await AtomicWriteTextAsync(
                paths.ChecksumPath,
                $"{hash}  {AnalysisFileName}{Environment.NewLine}",
                cancellationToken).ConfigureAwait(false);

            var manifest = new GaLiftAnalysisManifest
            {
                ProjectGuid = analysis.ProjectGuid,
                OptimizationRunId = analysis.OptimizationRunId,
                RevisionId = analysis.RevisionId,
                AnalysisRelativePath = Path.GetRelativePath(paths.ProjectRoot, paths.AnalysisPath).Replace('\\', '/'),
                Sha256 = hash,
                GeneratedAtUtc = analysis.GeneratedAtUtc
            };
            await AtomicWriteBytesAsync(
                paths.LatestManifestPath,
                _serializer.Serialize(manifest),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RestorePreviousCanonicalAsync(
                paths,
                previousAnalysisBytes,
                previousChecksumBytes).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<GaLiftAnalysisResult?> LoadAsync(
        string projectGuid,
        string optimizationRunId,
        CancellationToken cancellationToken = default)
    {
        var paths = _pathResolver.Resolve(projectGuid, optimizationRunId);
        var current = await LoadVerifiedAsync(paths.AnalysisPath, paths.ChecksumPath, cancellationToken)
            .ConfigureAwait(false);
        if (current == null
            && File.Exists(paths.AnalysisPath)
            && !File.Exists(paths.ChecksumPath))
        {
            current = await RecoverMissingChecksumAsync(paths, cancellationToken).ConfigureAwait(false);
        }
        if (current != null)
            return current;

        if (!Directory.Exists(paths.HistoryDirectory))
            return null;

        foreach (var path in Directory.EnumerateFiles(
                     paths.HistoryDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var checksum = Path.ChangeExtension(path, ".sha256");
            var recovered = await LoadVerifiedAsync(path, checksum, cancellationToken).ConfigureAwait(false);
            if (recovered != null
                && recovered.ProjectGuid == projectGuid
                && recovered.OptimizationRunId == optimizationRunId)
            {
                return recovered;
            }
        }

        return null;
    }

    private async Task<GaLiftAnalysisResult?> RecoverMissingChecksumAsync(
        GaLiftAnalysisPaths paths,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(paths.AnalysisPath, cancellationToken)
                .ConfigureAwait(false);
            var analysis = _serializer.DeserializeAnalysis(bytes);
            if (analysis == null)
                return null;
            Validate(analysis);
            await AtomicWriteTextAsync(
                paths.ChecksumPath,
                $"{Sha256(bytes)}  {AnalysisFileName}{Environment.NewLine}",
                cancellationToken).ConfigureAwait(false);
            return analysis;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<GaLiftAnalysisResult?> LoadLatestAsync(
        string projectGuid,
        CancellationToken cancellationToken = default)
    {
        var manifestPath = _pathResolver.Resolve(projectGuid, "manifest").LatestManifestPath;
        if (!File.Exists(manifestPath))
            return null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var manifest = _serializer.DeserializeManifest(bytes);
            if (manifest == null
                || manifest.SchemaVersion != 1
                || !string.Equals(manifest.ProjectGuid, projectGuid, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(manifest.OptimizationRunId)
                || string.IsNullOrWhiteSpace(manifest.RevisionId)
                || string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                return null;
            }

            var analysis = await LoadAsync(projectGuid, manifest.OptimizationRunId, cancellationToken)
                .ConfigureAwait(false);
            if (analysis == null
                || !string.Equals(analysis.RevisionId, manifest.RevisionId, StringComparison.Ordinal))
            {
                return null;
            }

            var paths = _pathResolver.Resolve(projectGuid, manifest.OptimizationRunId);
            var analysisBytes = await File.ReadAllBytesAsync(paths.AnalysisPath, cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(Sha256(analysisBytes), manifest.Sha256, StringComparison.OrdinalIgnoreCase)
                ? analysis
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        string projectGuid,
        string optimizationRunId,
        CancellationToken cancellationToken = default) =>
        await LoadAsync(projectGuid, optimizationRunId, cancellationToken).ConfigureAwait(false) != null;

    private static async Task ArchiveAsync(
        GaLiftAnalysisResult existing,
        GaLiftAnalysisPaths paths,
        CancellationToken cancellationToken)
    {
        ValidateRevisionId(existing.RevisionId);
        Directory.CreateDirectory(paths.HistoryDirectory);
        var historyJson = Path.Combine(paths.HistoryDirectory, $"{existing.RevisionId}.json");
        var historyChecksum = Path.Combine(paths.HistoryDirectory, $"{existing.RevisionId}.sha256");
        if (!File.Exists(historyJson))
        {
            var bytes = await File.ReadAllBytesAsync(paths.AnalysisPath, cancellationToken).ConfigureAwait(false);
            await AtomicWriteBytesAsync(historyJson, bytes, cancellationToken).ConfigureAwait(false);
            await AtomicWriteTextAsync(
                historyChecksum,
                $"{Sha256(bytes)}  {Path.GetFileName(historyJson)}{Environment.NewLine}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<GaLiftAnalysisResult?> LoadVerifiedAsync(
        string analysisPath,
        string checksumPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(analysisPath) || !File.Exists(checksumPath))
            return null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(analysisPath, cancellationToken).ConfigureAwait(false);
            var checksumText = await File.ReadAllTextAsync(checksumPath, cancellationToken).ConfigureAwait(false);
            var expected = checksumText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.Equals(expected, Sha256(bytes), StringComparison.OrdinalIgnoreCase))
                return null;
            var analysis = _serializer.DeserializeAnalysis(bytes);
            if (analysis == null)
                return null;
            Validate(analysis);
            return analysis;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task ArchiveCorruptCurrentAsync(
        GaLiftAnalysisPaths paths,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.HistoryDirectory);
        var suffix = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}_{Guid.NewGuid():N}";
        var historyJson = Path.Combine(paths.HistoryDirectory, $"corrupt_{suffix}.json");
        var historyChecksum = Path.ChangeExtension(historyJson, ".sha256");
        var bytes = await File.ReadAllBytesAsync(paths.AnalysisPath, cancellationToken).ConfigureAwait(false);
        await AtomicWriteBytesAsync(historyJson, bytes, cancellationToken).ConfigureAwait(false);
        await AtomicWriteTextAsync(
            historyChecksum,
            $"{Sha256(bytes)}  {Path.GetFileName(historyJson)}{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);
        if (File.Exists(paths.ChecksumPath))
        {
            var sourceChecksum = await File.ReadAllBytesAsync(paths.ChecksumPath, cancellationToken)
                .ConfigureAwait(false);
            await AtomicWriteBytesAsync(
                Path.Combine(paths.HistoryDirectory, $"corrupt_{suffix}.source.sha256"),
                sourceChecksum,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Validate(GaLiftAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (analysis.SchemaVersion != 1 || analysis.AnalysisType != "ga-lift-performance-analysis")
            throw new InvalidOperationException("Unsupported lift-analysis document.");
        if (string.IsNullOrWhiteSpace(analysis.ProjectGuid)
            || string.IsNullOrWhiteSpace(analysis.OptimizationRunId)
            || string.IsNullOrWhiteSpace(analysis.RevisionId)
            || string.IsNullOrWhiteSpace(analysis.HeadlineCode))
        {
            throw new InvalidOperationException("Lift-analysis identity is incomplete.");
        }
        if (analysis.CurrentCraneAdequacy == CurrentCraneAdequacy.Inadequate
            && (analysis.RightSizingAssessment is RightSizingAssessment.Justified
                or RightSizingAssessment.PotentiallyOversized))
        {
            throw new InvalidOperationException("An inadequate crane cannot be justified or potentially oversized.");
        }
        if (analysis.CurrentCraneAdequacy == CurrentCraneAdequacy.Inconclusive
            && (analysis.RightSizingAssessment is RightSizingAssessment.Justified
                or RightSizingAssessment.PotentiallyOversized))
        {
            throw new InvalidOperationException("Inconclusive adequacy cannot produce a conclusive right-sizing assessment.");
        }
        if (analysis.RightSizingAssessment == RightSizingAssessment.PotentiallyOversized
            && analysis.CurrentCraneAdequacy != CurrentCraneAdequacy.Suitable)
        {
            throw new InvalidOperationException("Potential oversizing requires a suitable current crane.");
        }
        if (analysis.RightSizingAssessment == RightSizingAssessment.Justified
            && analysis.CurrentCraneAdequacy is not (CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline))
        {
            throw new InvalidOperationException("A justified crane requires suitable or borderline adequacy.");
        }
        if (analysis.AlternativeScreeningOutcome == AlternativeScreeningOutcome.LargerOrLongerReachCandidateAvailable
            && (analysis.CurrentCraneAdequacy != CurrentCraneAdequacy.Inadequate
                || !analysis.CandidateResults.Any(candidate =>
                    candidate.IsValidatedAlternative
                    && candidate.RecoversEveryReliableCraneLimitFailure)))
        {
            throw new InvalidOperationException("A larger/longer-reach outcome requires complete validated failure recovery.");
        }
        if ((analysis.AlternativeScreeningOutcome is AlternativeScreeningOutcome.SmallerCandidateAvailable
                or AlternativeScreeningOutcome.BetterCandidateRecommended)
            && (!analysis.CandidateResults.Any(candidate =>
                    candidate.IsValidatedAlternative
                    && candidate.PreservesSuccessfulPlan)
                || analysis.CurrentCraneAdequacy is not (CurrentCraneAdequacy.Suitable or CurrentCraneAdequacy.Borderline)))
        {
            throw new InvalidOperationException("A smaller/better outcome requires adequate current-crane evidence and plan preservation.");
        }
        ValidateRevisionId(analysis.RevisionId);
        GaLiftAnalysisPathResolver.ValidateRunId(analysis.OptimizationRunId);
    }

    private static void ValidateRevisionId(string revisionId)
    {
        if (revisionId.Length != 28
            || !revisionId.StartsWith("rev_", StringComparison.Ordinal)
            || revisionId[4..].Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("Lift-analysis RevisionId is invalid.");
        }
    }

    private static async Task AtomicWriteBytesAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Output directory is unavailable."));
        var tempPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             useAsync: true))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static Task AtomicWriteTextAsync(
        string path,
        string text,
        CancellationToken cancellationToken) =>
        AtomicWriteBytesAsync(path, Encoding.UTF8.GetBytes(text), cancellationToken);

    private static async Task RestorePreviousCanonicalAsync(
        GaLiftAnalysisPaths paths,
        byte[]? previousAnalysisBytes,
        byte[]? previousChecksumBytes)
    {
        if (previousAnalysisBytes == null)
            TryDelete(paths.AnalysisPath);
        else
            await AtomicWriteBytesAsync(
                paths.AnalysisPath,
                previousAnalysisBytes,
                CancellationToken.None).ConfigureAwait(false);

        if (previousChecksumBytes == null)
            TryDelete(paths.ChecksumPath);
        else
            await AtomicWriteBytesAsync(
                paths.ChecksumPath,
                previousChecksumBytes,
                CancellationToken.None).ConfigureAwait(false);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; the committed destination is unaffected.
        }
    }
}
