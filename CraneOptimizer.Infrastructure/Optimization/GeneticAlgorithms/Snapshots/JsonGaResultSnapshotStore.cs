using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Services;

namespace CraneOptimizer.Infrastructure.Optimization.GeneticAlgorithms.Snapshots;

public sealed class GaResultSnapshotPathResolver : IGaResultSnapshotPathResolver
{
    private readonly IProjectOutputPathService _outputPathService;

    public GaResultSnapshotPathResolver(IProjectOutputPathService outputPathService)
    {
        _outputPathService = outputPathService;
    }

    public string ResolveRunDirectory(string optimizationRunId)
    {
        if (!IsSafeRunId(optimizationRunId))
            throw new ArgumentException("OptimizationRunId is not a valid path segment.", nameof(optimizationRunId));

        return _outputPathService.GetGaRunReportsDirectory(optimizationRunId);
    }

    public string ResolveSnapshotPath(string optimizationRunId) =>
        Path.Combine(ResolveRunDirectory(optimizationRunId), JsonGaResultSnapshotStore.SnapshotFileName);

    internal static bool IsSafeRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;
        return value.IndexOfAny(['/', '\\', ':']) < 0;
    }
}

public sealed class JsonGaResultSnapshotStore : IGaResultSnapshotStore
{
    public const string SnapshotFileName = "ga_result_snapshot.json";
    public const string ChecksumFileName = "ga_result_snapshot.sha256";
    public const string ManifestFileName = "ga_result_manifest.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly IGaResultSnapshotPathResolver _pathResolver;
    private readonly IGaResultSnapshotValidator _validator;

    public JsonGaResultSnapshotStore(
        IGaResultSnapshotPathResolver pathResolver,
        IGaResultSnapshotValidator validator)
    {
        _pathResolver = pathResolver;
        _validator = validator;
    }

    public async Task SaveAsync(GaResultSnapshotDto snapshot, CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(snapshot);
        if (!validation.IsValid)
            throw new InvalidOperationException("GA result snapshot validation failed: " + string.Join("; ", validation.Errors));

        var runDir = _pathResolver.ResolveRunDirectory(snapshot.Run.OptimizationRunId);
        Directory.CreateDirectory(runDir);

        var finalPath = Path.Combine(runDir, SnapshotFileName);
        var tempPath = finalPath + ".tmp";
        var checksumPath = Path.Combine(runDir, ChecksumFileName);
        var manifestPath = Path.Combine(runDir, ManifestFileName);

        if (File.Exists(finalPath))
        {
            var existing = await LoadAsync(finalPath, cancellationToken).ConfigureAwait(false);
            if (existing != null && existing.Run.OptimizationRunId != snapshot.Run.OptimizationRunId)
                throw new InvalidOperationException("Refusing to overwrite a GA snapshot from a different run.");
        }

        TryDelete(tempPath);

        await using (var stream = new FileStream(
                         tempPath,
                         FileMode.CreateNew,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 64 * 1024,
                         useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var roundTrip = await LoadJsonOnlyAsync(tempPath, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Serialized GA result snapshot could not be read back.");
        var roundTripValidation = _validator.Validate(roundTrip);
        if (!roundTripValidation.IsValid)
            throw new InvalidOperationException("Serialized GA result snapshot validation failed: " + string.Join("; ", roundTripValidation.Errors));

        var bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
        var hash = ComputeSha256(bytes);

        if (File.Exists(finalPath))
            File.Replace(tempPath, finalPath, null);
        else
            File.Move(tempPath, finalPath);

        await AtomicWriteTextAsync(
            checksumPath,
            $"{hash}  {SnapshotFileName}{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);

        var manifest = BuildManifest(snapshot, new FileInfo(finalPath).Length);
        await AtomicWriteJsonAsync(manifestPath, manifest, cancellationToken).ConfigureAwait(false);
        TryDelete(tempPath);
    }

    public async Task<GaResultSnapshotDto?> LoadAsync(string snapshotPath, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadJsonOnlyAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        if (snapshot == null)
            return null;

        var checksumPath = Path.Combine(Path.GetDirectoryName(snapshotPath) ?? string.Empty, ChecksumFileName);
        if (File.Exists(checksumPath))
        {
            var checksumText = await File.ReadAllTextAsync(checksumPath, cancellationToken).ConfigureAwait(false);
            var expected = checksumText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var actual = ComputeSha256(await File.ReadAllBytesAsync(snapshotPath, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        var validation = _validator.Validate(snapshot);
        return validation.IsValid ? snapshot : null;
    }

    public async Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default)
    {
        var gaRoot = Path.Combine(projectRoot, "Reports", "GA");
        if (!Directory.Exists(gaRoot))
            return Array.Empty<GaRunDescriptorDto>();

        var descriptors = new List<GaRunDescriptorDto>();
        foreach (var snapshotPath in Directory.EnumerateFiles(gaRoot, SnapshotFileName, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runDir = Path.GetDirectoryName(snapshotPath) ?? string.Empty;
            var manifest = await LoadOrRebuildManifestAsync(snapshotPath, runDir, cancellationToken).ConfigureAwait(false);
            if (manifest == null)
            {
                descriptors.Add(new GaRunDescriptorDto
                {
                    SnapshotPath = snapshotPath,
                    Classification = "Corrupt",
                    SourceKind = "ProductionJson",
                    Warnings = ["GA result snapshot or manifest could not be loaded."]
                });
                continue;
            }

            var classification = Classify(manifest.ProjectGuid, manifest.ModelFingerprint, projectGuid, modelFingerprint);
            descriptors.Add(new GaRunDescriptorDto
            {
                OptimizationRunId = manifest.OptimizationRunId,
                CompletedAtUtc = manifest.CompletedAtUtc,
                Status = manifest.Status,
                ProjectGuid = manifest.ProjectGuid,
                ModelFingerprint = manifest.ModelFingerprint,
                IfcSha256 = manifest.IfcSha256,
                CoverageRatio = manifest.CoverageRatio,
                LiftedLiftUnitCount = manifest.LiftedLiftUnitCount,
                TotalTargetLiftUnitCount = manifest.TotalTargetLiftUnitCount,
                SetupCount = manifest.PositionCount,
                CraneName = manifest.CraneName,
                EffectiveLiftMode = manifest.EffectiveLiftMode,
                Classification = classification,
                SnapshotPath = snapshotPath,
                Capabilities = manifest.Capabilities,
                SourceKind = manifest.Capabilities.SourceKind
            });
        }

        return descriptors
            .OrderByDescending(descriptor => descriptor.CompletedAtUtc ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<GaResultSnapshotDto?> LoadLatestCompletedAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default)
    {
        var descriptors = await ListRunsAsync(projectRoot, projectGuid, modelFingerprint, cancellationToken).ConfigureAwait(false);
        foreach (var descriptor in descriptors
                     .Where(descriptor => descriptor.Status == "Completed"
                         && descriptor.Classification is "Current" or "CurrentByIfcHash" or "CurrentProjectUnverified" or "Historic")
                     .OrderBy(descriptor => descriptor.Classification switch
                     {
                         "Current" => 0,
                         "CurrentByIfcHash" => 1,
                         "CurrentProjectUnverified" => 2,
                         "Historic" => 3,
                         _ => 9
                     })
                     .ThenByDescending(descriptor => descriptor.CompletedAtUtc ?? DateTimeOffset.MinValue))
        {
            var snapshot = await LoadAsync(descriptor.SnapshotPath, cancellationToken).ConfigureAwait(false);
            if (snapshot != null && snapshot.Run.Status == "Completed")
                return snapshot;
        }

        return null;
    }

    private async Task<GaResultManifestDto?> LoadOrRebuildManifestAsync(
        string snapshotPath,
        string runDir,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDir, ManifestFileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                await using var manifestStream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, useAsync: true);
                var manifest = await JsonSerializer.DeserializeAsync<GaResultManifestDto>(manifestStream, Options, cancellationToken).ConfigureAwait(false);
                if (manifest != null && manifest.SchemaVersion == 1)
                    return manifest;
            }
            catch
            {
                // Rebuild below.
            }
        }

        var snapshot = await LoadAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        if (snapshot == null)
            return null;

        var rebuilt = BuildManifest(snapshot, new FileInfo(snapshotPath).Length);
        await AtomicWriteJsonAsync(manifestPath, rebuilt, cancellationToken).ConfigureAwait(false);
        return rebuilt;
    }

    private static async Task<GaResultSnapshotDto?> LoadJsonOnlyAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(snapshotPath))
            return null;

        try
        {
            await using var stream = new FileStream(snapshotPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
            return await JsonSerializer.DeserializeAsync<GaResultSnapshotDto>(stream, Options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static GaResultManifestDto BuildManifest(GaResultSnapshotDto snapshot, long sizeBytes) => new()
    {
        SchemaVersion = 1,
        OptimizationRunId = snapshot.Run.OptimizationRunId,
        ProjectGuid = snapshot.Project.ProjectGuid,
        ModelFingerprint = snapshot.Model.ModelFingerprint,
        IfcSha256 = snapshot.Model.IfcSha256,
        Status = snapshot.Run.Status,
        CompletedAtUtc = snapshot.Run.CompletedAtUtc,
        CraneName = snapshot.Crane.CraneName,
        EffectiveLiftMode = snapshot.InputScope.EffectiveLiftMode,
        TotalTargetLiftUnitCount = snapshot.Summary.TotalTargetLiftUnitCount,
        LiftedLiftUnitCount = snapshot.Summary.LiftedLiftUnitCount,
        CoverageRatio = snapshot.Summary.CoverageRatio,
        PositionCount = snapshot.Summary.PositionCount,
        SnapshotSizeBytes = sizeBytes,
        Capabilities = snapshot.Capabilities
    };

    private static string Classify(
        string snapshotProjectGuid,
        string snapshotModelFingerprint,
        string projectGuid,
        string? modelFingerprint)
    {
        if (!string.Equals(snapshotProjectGuid, projectGuid, StringComparison.Ordinal))
            return "Foreign";
        if (!string.IsNullOrWhiteSpace(modelFingerprint)
            && !string.Equals(snapshotModelFingerprint, modelFingerprint, StringComparison.Ordinal))
            return "Historic";
        return "Current";
    }

    private static async Task AtomicWriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        TryDelete(tempPath);
        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        if (File.Exists(path))
            File.Replace(tempPath, path, null);
        else
            File.Move(tempPath, path);
    }

    private static async Task AtomicWriteTextAsync(string path, string text, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        TryDelete(tempPath);
        await File.WriteAllTextAsync(tempPath, text, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        if (File.Exists(path))
            File.Replace(tempPath, path, null);
        else
            File.Move(tempPath, path);
    }

    private static string ComputeSha256(byte[] bytes) =>
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
            // Temporary cleanup is best-effort.
        }
    }
}
