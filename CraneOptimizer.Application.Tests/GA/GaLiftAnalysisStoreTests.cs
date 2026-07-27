using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Infrastructure.GAAnalysis;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaLiftAnalysisStoreTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("COM1.audit")]
    [InlineData("CLOCK$")]
    [InlineData("run:stream")]
    [InlineData("run/escape")]
    [InlineData("run.")]
    public void Resolver_rejects_unsafe_run_ids_without_creating_directories(string runId)
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));

        Assert.ThrowsAny<ArgumentException>(() => resolver.Resolve("project-1", runId));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Resolver_accepts_128_char_run_id_and_keeps_it_beneath_analysis_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));
        var runId = $"r{new string('a', 127)}";

        var paths = resolver.Resolve("project-1", runId);

        var analysisRoot = Path.GetFullPath(Path.Combine(root, "Reports", "Analysis"))
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(analysisRoot, paths.RunDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Resolver_rejects_run_ids_longer_than_128_characters()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve("project-1", $"r{new string('a', 128)}"));
    }

    [Fact]
    public async Task Store_writes_checksum_manifest_and_archives_prior_revision_atomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));
        var store = new JsonGaLiftAnalysisStore(resolver, new SystemTextJsonGaLiftAnalysisSerializer());
        var first = CreateAnalysis("rev_111111111111111111111111", "First");
        var second = CreateAnalysis("rev_222222222222222222222222", "Second");

        // Path resolution is deliberately side-effect free.
        var paths = resolver.Resolve(first.ProjectGuid, first.OptimizationRunId);
        Assert.False(Directory.Exists(paths.RunDirectory));

        await store.SaveAsync(first);
        await store.SaveAsync(second);
        var loaded = await store.LoadAsync(second.ProjectGuid, second.OptimizationRunId);

        Assert.NotNull(loaded);
        Assert.Equal(second.RevisionId, loaded.RevisionId);
        Assert.True(File.Exists(paths.AnalysisPath));
        Assert.True(File.Exists(paths.ChecksumPath));
        Assert.True(File.Exists(paths.LatestManifestPath));
        Assert.EndsWith(
            Path.Combine("Reports", "Analysis", "latest_analysis.json"),
            paths.LatestManifestPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("Reports", "Analysis", "run-1", "ga_lift_performance_analysis.json"),
            paths.AnalysisPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(paths.HistoryDirectory, $"{first.RevisionId}.json")));
        Assert.True(File.Exists(Path.Combine(paths.HistoryDirectory, $"{first.RevisionId}.sha256")));
        Assert.True(await store.ExistsAsync(second.ProjectGuid, second.OptimizationRunId));
        Assert.Equal(second.RevisionId, (await store.LoadLatestAsync(second.ProjectGuid))?.RevisionId);
        var json = await File.ReadAllTextAsync(paths.AnalysisPath);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains(
            "\"analysisType\": \"ga-lift-performance-analysis\"",
            json,
            StringComparison.Ordinal);
        Assert.Contains("\"currentCraneAdequacy\": \"SUITABLE\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Store_preserves_corrupt_current_document_before_regeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));
        var store = new JsonGaLiftAnalysisStore(resolver, new SystemTextJsonGaLiftAnalysisSerializer());
        var analysis = CreateAnalysis("rev_333333333333333333333333", "Recovered");
        var paths = resolver.Resolve(analysis.ProjectGuid, analysis.OptimizationRunId);
        Directory.CreateDirectory(paths.RunDirectory);
        await File.WriteAllTextAsync(paths.AnalysisPath, "{ invalid json");
        await File.WriteAllTextAsync(paths.ChecksumPath, "invalid");

        await store.SaveAsync(analysis);

        Assert.Single(Directory.EnumerateFiles(paths.HistoryDirectory, "corrupt_*.json"));
        Assert.NotNull(await store.LoadAsync(analysis.ProjectGuid, analysis.OptimizationRunId));
    }

    [Fact]
    public async Task Store_recovers_a_valid_canonical_document_with_a_missing_checksum()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));
        var serializer = new SystemTextJsonGaLiftAnalysisSerializer();
        var store = new JsonGaLiftAnalysisStore(resolver, serializer);
        var analysis = CreateAnalysis("rev_444444444444444444444444", "Recover checksum");
        var paths = resolver.Resolve(analysis.ProjectGuid, analysis.OptimizationRunId);
        Directory.CreateDirectory(paths.RunDirectory);
        await File.WriteAllBytesAsync(paths.AnalysisPath, serializer.Serialize(analysis));

        var loaded = await store.LoadAsync(analysis.ProjectGuid, analysis.OptimizationRunId);

        Assert.Equal(analysis.RevisionId, loaded?.RevisionId);
        Assert.True(File.Exists(paths.ChecksumPath));
    }

    [Fact]
    public async Task Failed_manifest_commit_restores_the_previous_canonical_analysis()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        var resolver = new GaLiftAnalysisPathResolver(new FixedProjectPathProvider(root));
        var serializer = new SystemTextJsonGaLiftAnalysisSerializer();
        var store = new JsonGaLiftAnalysisStore(resolver, serializer);
        var first = CreateAnalysis("rev_555555555555555555555555", "Previous");
        var second = CreateAnalysis("rev_666666666666666666666666", "Uncommitted");
        await store.SaveAsync(first);
        var failingStore = new JsonGaLiftAnalysisStore(
            resolver,
            new FailingManifestSerializer(serializer));

        await Assert.ThrowsAsync<IOException>(() => failingStore.SaveAsync(second));

        var restored = await store.LoadAsync(first.ProjectGuid, first.OptimizationRunId);
        var latest = await store.LoadLatestAsync(first.ProjectGuid);
        Assert.Equal(first.RevisionId, restored?.RevisionId);
        Assert.Equal(first.RevisionId, latest?.RevisionId);
    }

    private static GaLiftAnalysisResult CreateAnalysis(string revisionId, string headline) => new()
    {
        RevisionId = revisionId,
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        ProjectGuid = "project-1",
        OptimizationRunId = "run-1",
        SourceSnapshotSchemaVersion = 3,
        SourceSnapshotFingerprint = new string('a', 64),
        CurrentCraneAdequacy = CurrentCraneAdequacy.Suitable,
        RightSizingAssessment = RightSizingAssessment.Justified,
        AlternativeScreeningOutcome = AlternativeScreeningOutcome.NotRun,
        HeadlineCode = "KEEP_CURRENT_CRANE",
        Headline = headline,
        HeadlineQualifier = "Test evidence"
    };

    private sealed class FixedProjectPathProvider : IActiveProjectPathProvider
    {
        private readonly string _root;

        public FixedProjectPathProvider(string root)
        {
            _root = root;
        }

        public string? GetActiveProjectPath() => _root;
    }

    private sealed class FailingManifestSerializer : IGaLiftAnalysisSerializer
    {
        private readonly IGaLiftAnalysisSerializer _inner;

        public FailingManifestSerializer(IGaLiftAnalysisSerializer inner)
        {
            _inner = inner;
        }

        public byte[] Serialize(GaLiftAnalysisResult analysis) => _inner.Serialize(analysis);

        public byte[] Serialize(GaLiftAnalysisManifest manifest) =>
            throw new IOException("Simulated manifest failure.");

        public GaLiftAnalysisResult? DeserializeAnalysis(ReadOnlySpan<byte> json) =>
            _inner.DeserializeAnalysis(json);

        public GaLiftAnalysisManifest? DeserializeManifest(ReadOnlySpan<byte> json) =>
            _inner.DeserializeManifest(json);
    }
}
