using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

public interface IGaResultSnapshotMapper
{
    Task<GaResultSnapshotDto> MapAsync(
        GaExecutionResult execution,
        GaSnapshotMappingContext context,
        CancellationToken cancellationToken = default);
}

public interface IGaResultSnapshotValidator
{
    GaSnapshotValidationResult Validate(GaResultSnapshotDto snapshot);
}

public interface IGaResultSnapshotPathResolver
{
    string ResolveRunDirectory(string optimizationRunId);
    string ResolveSnapshotPath(string optimizationRunId);
}

public interface IGaResultSnapshotStore
{
    Task SaveAsync(GaResultSnapshotDto snapshot, CancellationToken cancellationToken = default);
    Task<GaResultSnapshotDto?> LoadAsync(string snapshotPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default);
    Task<GaResultSnapshotDto?> LoadLatestCompletedAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default);
}

public interface IGaSnapshotClassificationService
{
    GaSnapshotClassificationResult Classify(
        GaProjectActivationContext current,
        GaRunDescriptorDto savedRun);
}

public interface IGaModelIdentityService
{
    Task<GaCurrentModelIdentity> BuildCurrentAsync(
        GaProjectActivationContext project,
        IReadOnlyList<PrecastElement>? currentElements,
        CancellationToken cancellationToken = default);
}

public interface IGaCurrentResultStore
{
    void Set(string projectGuid, string optimizationRunId, GaResultSnapshotDto snapshot);
    bool TryGet(string projectGuid, out GaResultSnapshotDto? snapshot);
    void Remove(string projectGuid);
    void Clear();
}

public interface IGaResultSnapshotProvider
{
    Task<ResolvedGaResultSnapshot> ResolveAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        GaProjectActivationContext context,
        CancellationToken cancellationToken = default);

    Task<ResolvedGaResultSnapshot> ResolveSelectedAsync(
        GaProjectActivationContext context,
        GaRunDescriptorDto selectedRun,
        CancellationToken cancellationToken = default);
}

public interface IGaLegacyCsvSnapshotAdapter
{
    Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        GaProjectActivationContext context,
        CancellationToken cancellationToken = default);

    Task<ResolvedGaResultSnapshot> LoadAsync(
        GaProjectActivationContext context,
        GaRunDescriptorDto selectedRun,
        CancellationToken cancellationToken = default);

    Task<ResolvedGaResultSnapshot> TryLoadLatestAsync(
        string projectRoot,
        string projectGuid,
        CancellationToken cancellationToken = default);
}

public interface IGaSnapshotReportAdapter
{
    IReadOnlyList<LiftingResultSourceRowDto> BuildRows(
        GaResultSnapshotDto snapshot,
        string templateName,
        string itemsIncluded,
        string? selectedPositionId = null);

    IReadOnlyList<CraneSetupReportRowDto> BuildCraneSetups(GaResultSnapshotDto snapshot);
}

public sealed class GaCurrentResultStore : IGaCurrentResultStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, GaResultSnapshotDto> _current = new(StringComparer.Ordinal);

    public void Set(string projectGuid, string optimizationRunId, GaResultSnapshotDto snapshot)
    {
        if (string.IsNullOrWhiteSpace(projectGuid) || string.IsNullOrWhiteSpace(optimizationRunId))
            return;

        lock (_gate)
        {
            _current[projectGuid] = snapshot;
        }
    }

    public bool TryGet(string projectGuid, out GaResultSnapshotDto? snapshot)
    {
        lock (_gate)
        {
            return _current.TryGetValue(projectGuid, out snapshot);
        }
    }

    public void Remove(string projectGuid)
    {
        lock (_gate)
        {
            _current.Remove(projectGuid);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current.Clear();
        }
    }
}

public sealed class GaSnapshotClassificationService : IGaSnapshotClassificationService
{
    public GaSnapshotClassificationResult Classify(
        GaProjectActivationContext current,
        GaRunDescriptorDto savedRun)
    {
        if (savedRun.Classification is "Corrupt" or "Unsupported")
            return new GaSnapshotClassificationResult(savedRun.Classification, savedRun.Warnings);

        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(savedRun.ProjectGuid)
            && !string.Equals(savedRun.ProjectGuid, current.ProjectGuid, StringComparison.Ordinal))
        {
            return new GaSnapshotClassificationResult("Foreign", ["GA result belongs to a different project."]);
        }

        if (string.IsNullOrWhiteSpace(savedRun.ProjectGuid)
            && savedRun.SourceKind is "LegacyCsv" or "PartialLegacy")
        {
            warnings.Add("Legacy run does not contain a stable project GUID. Ownership is inferred from its location inside the active project folder.");
        }

        if (!string.IsNullOrWhiteSpace(current.CurrentModelFingerprint)
            && !string.IsNullOrWhiteSpace(savedRun.ModelFingerprint)
            && string.Equals(savedRun.ModelFingerprint, current.CurrentModelFingerprint, StringComparison.Ordinal))
        {
            return new GaSnapshotClassificationResult("Current", warnings);
        }

        if (!string.IsNullOrWhiteSpace(current.CurrentIfcSha256)
            && !string.IsNullOrWhiteSpace(savedRun.IfcSha256)
            && string.Equals(savedRun.IfcSha256, current.CurrentIfcSha256, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Current model equivalence was established by matching IFC SHA-256 evidence.");
            return new GaSnapshotClassificationResult("CurrentByIfcHash", warnings);
        }

        var hasCurrentEvidence = !string.IsNullOrWhiteSpace(current.CurrentModelFingerprint)
            || !string.IsNullOrWhiteSpace(current.CurrentIfcSha256);
        var hasSavedEvidence = !string.IsNullOrWhiteSpace(savedRun.ModelFingerprint)
            || !string.IsNullOrWhiteSpace(savedRun.IfcSha256);

        if (hasCurrentEvidence && hasSavedEvidence)
        {
            warnings.Add("Historic GA result generated from an earlier project model revision.");
            return new GaSnapshotClassificationResult("Historic", warnings);
        }

        warnings.Add("The project matches, but current model identity is not yet available for verification.");
        return new GaSnapshotClassificationResult("CurrentProjectUnverified", warnings);
    }
}

public sealed class GaModelIdentityService : IGaModelIdentityService
{
    public async Task<GaCurrentModelIdentity> BuildCurrentAsync(
        GaProjectActivationContext project,
        IReadOnlyList<PrecastElement>? currentElements,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(project.IfcPath) && File.Exists(project.IfcPath))
        {
            await using var stream = new FileStream(project.IfcPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            var info = new FileInfo(project.IfcPath);
            return new GaCurrentModelIdentity(
                Convert.ToHexString(hash).ToLowerInvariant(),
                Convert.ToHexString(hash).ToLowerInvariant(),
                Path.GetFileName(project.IfcPath),
                string.IsNullOrWhiteSpace(project.ProjectRoot) ? null : Path.GetRelativePath(project.ProjectRoot, project.IfcPath).Replace('\\', '/'),
                info.Length,
                info.LastWriteTimeUtc,
                project.ModelRevision,
                "ifc-sha256-v1");
        }

        if (currentElements is { Count: > 0 })
        {
            var projection = currentElements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .OrderBy(element => element.ElementID, StringComparer.Ordinal)
                .Select(element => string.Join("|",
                    element.ElementID,
                    element.ElementType,
                    element.RawIfcType,
                    element.ResolvedCategory,
                    element.ElementWT?.ToString("R", CultureInfo.InvariantCulture),
                    $"{element.ElementCOG.X:R},{element.ElementCOG.Y:R},{element.ElementCOG.Z:R}",
                    element.ParentGlobalId,
                    element.MainPartGlobalId,
                    string.Join(";", element.ChildGlobalIds.OrderBy(id => id, StringComparer.Ordinal)),
                    element.ValidationStatus,
                    string.Join(";", element.ValidationWarnings.OrderBy(warning => warning, StringComparer.Ordinal))));
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", projection));
            var hash = SHA256.HashData(bytes);
            return new GaCurrentModelIdentity(
                Convert.ToHexString(hash).ToLowerInvariant(),
                null,
                null,
                null,
                null,
                null,
                project.ModelRevision,
                "element-projection-v1");
        }

        return new GaCurrentModelIdentity(null, null, null, null, null, null, project.ModelRevision, "unavailable");
    }
}

public sealed class GaResultSnapshotProvider : IGaResultSnapshotProvider
{
    private readonly IGaCurrentResultStore _currentStore;
    private readonly IGaResultSnapshotStore _snapshotStore;
    private readonly IGaLegacyCsvSnapshotAdapter _legacyCsvAdapter;
    private readonly IGaSnapshotClassificationService _classificationService;

    public GaResultSnapshotProvider(
        IGaCurrentResultStore currentStore,
        IGaResultSnapshotStore snapshotStore,
        IGaLegacyCsvSnapshotAdapter legacyCsvAdapter,
        IGaSnapshotClassificationService classificationService)
    {
        _currentStore = currentStore;
        _snapshotStore = snapshotStore;
        _legacyCsvAdapter = legacyCsvAdapter;
        _classificationService = classificationService;
    }

    public async Task<ResolvedGaResultSnapshot> ResolveAsync(
        string projectRoot,
        string projectGuid,
        string? modelFingerprint,
        CancellationToken cancellationToken = default)
    {
        var context = new GaProjectActivationContext(
            projectRoot,
            projectGuid,
            string.Empty,
            null,
            modelFingerprint,
            null,
            null);

        if (_currentStore.TryGet(projectGuid, out var current)
            && current != null
            && current.Project.ProjectGuid == projectGuid
            && (string.IsNullOrWhiteSpace(modelFingerprint) || current.Model.ModelFingerprint == modelFingerprint))
        {
            var currentDescriptor = ToDescriptor(current, "Current", string.Empty);
            return new ResolvedGaResultSnapshot
            {
                Snapshot = current,
                SourceKind = "CurrentSession",
                Classification = "Current",
                Capabilities = current.Capabilities,
                SelectedRun = currentDescriptor
            };
        }

        var descriptors = await ListRunsAsync(context, cancellationToken).ConfigureAwait(false);
        var loadWarnings = new List<string>();
        foreach (var descriptor in SelectUsableRuns(descriptors))
        {
            var resolved = await ResolveSelectedAsync(context, descriptor, cancellationToken).ConfigureAwait(false);
            if (resolved.Snapshot != null)
                return resolved;

            if (resolved.Warnings.Count > 0)
                loadWarnings.AddRange(resolved.Warnings);
        }

        return new ResolvedGaResultSnapshot
        {
            SourceKind = "None",
            Classification = "NoResult",
            Warnings = loadWarnings.Count > 0
                ? loadWarnings.Distinct(StringComparer.Ordinal).ToList()
                : ["No GA result snapshot is available for this project."]
        };
    }

    public async Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        GaProjectActivationContext context,
        CancellationToken cancellationToken = default)
    {
        var production = await _snapshotStore.ListRunsAsync(
            context.ProjectRoot,
            context.ProjectGuid,
            context.CurrentModelFingerprint,
            cancellationToken).ConfigureAwait(false);
        var legacy = await _legacyCsvAdapter.ListRunsAsync(context, cancellationToken).ConfigureAwait(false);

        return production
            .Concat(legacy)
            .Select(descriptor => Reclassify(context, descriptor))
            .GroupBy(DedupKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(SourcePriority)
                .ThenByDescending(item => item.CompletedAtUtc ?? DateTimeOffset.MinValue)
                .First())
            .OrderBy(ClassificationPriority)
            .ThenByDescending(item => item.CompletedAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(SourcePriority)
            .ToList();
    }

    public async Task<ResolvedGaResultSnapshot> ResolveSelectedAsync(
        GaProjectActivationContext context,
        GaRunDescriptorDto selectedRun,
        CancellationToken cancellationToken = default)
    {
        var descriptor = Reclassify(context, selectedRun);
        if (!IsUsableCompleted(descriptor))
        {
            return new ResolvedGaResultSnapshot
            {
                SourceKind = descriptor.SourceKind,
                Classification = descriptor.Classification,
                Capabilities = descriptor.Capabilities,
                Warnings = descriptor.Warnings.Count > 0
                    ? descriptor.Warnings
                    : [$"The selected GA run cannot be opened as a normal result because it is {descriptor.Classification}."],
                SelectedRun = descriptor
            };
        }

        if (descriptor.SourceKind is "LegacyCsv" or "PartialLegacy")
            return await _legacyCsvAdapter.LoadAsync(context, descriptor, cancellationToken).ConfigureAwait(false);

        var snapshot = await _snapshotStore.LoadAsync(descriptor.SnapshotPath, cancellationToken).ConfigureAwait(false);
        if (snapshot == null)
        {
            return new ResolvedGaResultSnapshot
            {
                SourceKind = descriptor.SourceKind,
                Classification = "Corrupt",
                Capabilities = descriptor.Capabilities,
                Warnings = ["The selected GA result could not be loaded because its snapshot or checksum is invalid."],
                SelectedRun = descriptor
            };
        }

        var finalDescriptor = Reclassify(context, ToDescriptor(snapshot, descriptor.Classification, descriptor.SnapshotPath));
        return new ResolvedGaResultSnapshot
        {
            Snapshot = snapshot,
            SourceKind = snapshot.Capabilities.SourceKind,
            Classification = finalDescriptor.Classification,
            Capabilities = snapshot.Capabilities,
            Warnings = finalDescriptor.Warnings,
            SelectedRun = finalDescriptor
        };
    }

    private static GaRunDescriptorDto ToDescriptor(
        GaResultSnapshotDto snapshot,
        string classification,
        string path) => new()
    {
        OptimizationRunId = snapshot.Run.OptimizationRunId,
        CompletedAtUtc = snapshot.Run.CompletedAtUtc,
        Status = snapshot.Run.Status,
        ProjectGuid = snapshot.Project.ProjectGuid,
        ModelFingerprint = snapshot.Model.ModelFingerprint,
        IfcSha256 = snapshot.Model.IfcSha256,
        CoverageRatio = snapshot.Summary.CoverageRatio,
        LiftedLiftUnitCount = snapshot.Summary.LiftedLiftUnitCount,
        TotalTargetLiftUnitCount = snapshot.Summary.TotalTargetLiftUnitCount,
        SetupCount = snapshot.Summary.PositionCount,
        CraneName = snapshot.Crane.CraneName,
        EffectiveLiftMode = snapshot.InputScope.EffectiveLiftMode,
        Classification = classification,
        SnapshotPath = path,
        Capabilities = snapshot.Capabilities,
        SourceKind = snapshot.Capabilities.SourceKind
    };

    private GaRunDescriptorDto Reclassify(GaProjectActivationContext context, GaRunDescriptorDto descriptor)
    {
        var classified = _classificationService.Classify(context, descriptor);
        return new GaRunDescriptorDto
        {
            OptimizationRunId = descriptor.OptimizationRunId,
            CompletedAtUtc = descriptor.CompletedAtUtc,
            Status = descriptor.Status,
            ProjectGuid = descriptor.ProjectGuid,
            ModelFingerprint = descriptor.ModelFingerprint,
            IfcSha256 = descriptor.IfcSha256,
            CoverageRatio = descriptor.CoverageRatio,
            LiftedLiftUnitCount = descriptor.LiftedLiftUnitCount,
            TotalTargetLiftUnitCount = descriptor.TotalTargetLiftUnitCount,
            SetupCount = descriptor.SetupCount,
            CraneName = descriptor.CraneName,
            EffectiveLiftMode = descriptor.EffectiveLiftMode,
            Classification = classified.Classification,
            SnapshotPath = descriptor.SnapshotPath,
            Capabilities = descriptor.Capabilities,
            SourceKind = descriptor.SourceKind,
            Warnings = descriptor.Warnings.Concat(classified.Warnings).Distinct(StringComparer.Ordinal).ToList()
        };
    }

    private static GaRunDescriptorDto? SelectBestRun(IEnumerable<GaRunDescriptorDto> descriptors) =>
        SelectUsableRuns(descriptors).FirstOrDefault();

    private static IEnumerable<GaRunDescriptorDto> SelectUsableRuns(IEnumerable<GaRunDescriptorDto> descriptors) =>
        descriptors
            .Where(IsUsableCompleted)
            .OrderBy(ClassificationPriority)
            .ThenByDescending(item => item.CompletedAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(SourcePriority);

    private static bool IsUsableCompleted(GaRunDescriptorDto descriptor) =>
        string.Equals(descriptor.Status, "Completed", StringComparison.OrdinalIgnoreCase)
        && descriptor.Classification is "Current" or "CurrentByIfcHash" or "CurrentProjectUnverified" or "Historic"
        && descriptor.SourceKind is "ProductionJson" or "LegacyCsv" or "PartialLegacy";

    private static string DedupKey(GaRunDescriptorDto descriptor) =>
        !string.IsNullOrWhiteSpace(descriptor.OptimizationRunId)
            ? "run:" + descriptor.OptimizationRunId
            : "path:" + descriptor.SnapshotPath;

    private static int SourcePriority(GaRunDescriptorDto descriptor) =>
        descriptor.SourceKind == "ProductionJson" ? 0 : 1;

    private static int ClassificationPriority(GaRunDescriptorDto descriptor) =>
        descriptor.Classification switch
        {
            "Current" => 0,
            "CurrentByIfcHash" => 1,
            "CurrentProjectUnverified" => 2,
            "Historic" => 3,
            _ => 9
        };
}

public sealed class GaResultSnapshotMapper : IGaResultSnapshotMapper
{
    public Task<GaResultSnapshotDto> MapAsync(
        GaExecutionResult execution,
        GaSnapshotMappingContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var input = execution.OriginalInput;
        var best = execution.BestResult;
        var requested = CloneSettings(execution.RequestedSettings);
        var effective = CloneSettings(execution.EffectiveFinalSettings);
        var projectGuid = string.IsNullOrWhiteSpace(context.ProjectGuid)
            ? input.ProjectId ?? string.Empty
            : context.ProjectGuid;
        if (input.SubmittedCanonicalLiftUnits.Count == 0
            || string.IsNullOrWhiteSpace(input.CanonicalSnapshotId)
            || string.IsNullOrWhiteSpace(input.CanonicalPayloadHash))
        {
            throw new InvalidOperationException("GA execution input is missing the pre-run canonical lift-unit snapshot.");
        }

        var liftUnits = CanonicalLiftUnitSnapshotBuilder
            .DeepCloneCanonicalUnits(input.SubmittedCanonicalLiftUnits)
            .ToList();
        CanonicalLiftUnitSnapshotBuilder.ValidateSnapshotParity(input.SubmittedCanonicalLiftUnits, liftUnits, "GA result snapshot");
        var positions = BuildPositions(execution.OptimizationRunId, best, input, effective).ToList();
        var outcomes = BuildFinalOutcomes(liftUnits, positions, input, best).ToList();
        var modelFingerprint = input.CanonicalPayloadHash;
        var loadChartFingerprint = ComputeLoadChartFingerprint(input.CraneSpec?.LoadChart);
        var optimizationInputFingerprint = GaOptimizationInputFingerprint.Build(input, effective);
        var evaluationCount = positions.Sum(position => position.Evaluations.Count);
        var hasReachability = best.ReachabilityDiagnostics != null;
        var hasSurvey = false;

        var snapshot = new GaResultSnapshotDto
        {
            SchemaVersion = 3,
            CanonicalSnapshotId = input.CanonicalSnapshotId,
            CanonicalPayloadHash = input.CanonicalPayloadHash,
            ApplicationVersion = context.ApplicationVersion,
            GitCommitHash = context.GitCommitHash,
            Run = new GaRunIdentitySnapshotDto
            {
                OptimizationRunId = execution.OptimizationRunId,
                FinalStageRunId = execution.FinalStageRunId,
                FinalStageIndex = execution.FinalStageIndex,
                Status = "Completed",
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                DurationMilliseconds = execution.DurationMilliseconds,
                TerminationReason = execution.TerminationReason,
                ApplicationVersion = context.ApplicationVersion,
                GitCommitHash = context.GitCommitHash,
                SnapshotSchemaVersion = 3,
                DiagnosticsSchemaVersion = "1.0"
            },
            Project = new GaProjectSnapshotDto
            {
                ProjectGuid = projectGuid,
                ProjectName = context.ProjectName,
                ProjectDatabaseId = context.ProjectDatabaseId,
                ProjectRelativeSnapshotPath = $"Reports/GA/{execution.OptimizationRunId}/ga_result_snapshot.json"
            },
            Model = new GaModelSnapshotDto
            {
                CanonicalSnapshotId = input.CanonicalSnapshotId,
                CanonicalPayloadHash = input.CanonicalPayloadHash,
                ModelFingerprint = modelFingerprint,
                OptimizationInputFingerprint = optimizationInputFingerprint.Aggregate,
                OptimizationInputFingerprintVersion = optimizationInputFingerprint.Version,
                OptimizationInputFingerprintComponents = optimizationInputFingerprint.Components,
                IfcFileName = input.IfcFileName,
                IfcProjectRelativePath = BuildProjectRelativeIfcPath(context.ProjectRoot, context.IfcFilePath),
                IfcSha256 = TryHashFile(context.IfcFilePath, cancellationToken),
                IfcFileSizeBytes = TryFileSize(context.IfcFilePath),
                IfcLastWriteUtc = TryLastWrite(context.IfcFilePath),
                ScenarioId = input.ScenarioId,
                ProjectFeatureSnapshotId = input.ProjectFeatureSnapshotId,
                ScopeDefinitionHash = input.ScopeDefinitionHash
            },
            Coordinates = new GaCoordinateReferenceSnapshotDto(),
            RequestedSettings = GaSettingsSnapshotDto.FromSettings(requested),
            EffectiveFinalSettings = GaSettingsSnapshotDto.FromSettings(effective),
            Stages = execution.Stages.Select(stage => ToStageSummary(stage)).ToList(),
            Crane = BuildCrane(input.CraneSpec, effective),
            LiftingAccessories = BuildLiftingAccessories(input.CraneSpec, effective),
            LoadChart = BuildLoadChart(input.CraneSpec, loadChartFingerprint, effective),
            InputScope = BuildInputScope(input, effective),
            Summary = new GaRunSummarySnapshotDto
            {
                TotalTargetLiftUnitCount = best.TotalElementCount,
                LiftedLiftUnitCount = best.LiftedElementCount,
                UnliftedLiftUnitCount = Math.Max(0, best.TotalElementCount - best.LiftedElementCount),
                RawGaEligibleElementCount = best.RawGaEligibleElementCount,
                GroupedAssemblyCount = best.GroupedAssemblyCount,
                UngroupedElementCount = best.UngroupedElementCount,
                PositionCount = best.Positions.Count,
                FitnessScore = best.FitnessScore,
                CoverageRatio = best.CoverageRatio,
                Liftable = best.Statistics.Liftable,
                OutOfRadius = best.Statistics.OutOfRadius,
                Overweight = best.Statistics.Overweight,
                HeightFail = best.Statistics.HeightFail,
                SpecialCase = best.Statistics.SpecialCase,
                NotCharted = best.Statistics.NotCharted
            },
            Performance = BuildPerformance(execution, effective),
            Reachability = BuildReachability(best),
            LiftUnits = liftUnits,
            Positions = positions,
            FinalOutcomes = outcomes,
            Capabilities = GaSnapshotCapabilitiesDto.Production(hasPerformance: true, hasReachability, hasSurvey),
            Diagnostics = new GaDiagnosticsReferencesSnapshotDto
            {
                ProjectRelativeFiles = BuildDiagnosticReferences(input)
            },
            Integrity = new GaIntegritySnapshotDto
            {
                LiftUnitCount = liftUnits.Count,
                PositionCount = positions.Count,
                EvaluationCount = evaluationCount,
                FinalOutcomeCount = outcomes.Count,
                ValidatedAtUtc = DateTimeOffset.UtcNow
            }
        };

        return Task.FromResult(snapshot);
    }

    private static IEnumerable<GaPositionSnapshotDto> BuildPositions(
        string optimizationRunId,
        PlacementResult result,
        PlacementInput input,
        GASettings settings)
    {
        var liftUnitWeights = input.Elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ElementWT ?? group.First().WeightTons, StringComparer.Ordinal);

        for (var index = 0; index < result.Positions.Count; index++)
        {
            var position = result.Positions[index];
            var positionId = $"Point_{index}";
            var evaluations = position.ElementResults
                .Select(resultRow => ToEvaluation(optimizationRunId, positionId, resultRow, liftUnitWeights, settings))
                .ToList();
            var finalAssigned = evaluations
                .Where(evaluation => evaluation.IsFinalAssignedToPoint && evaluation.LiftStatus == LiftStatus.Liftable.ToString())
                .Select(evaluation => evaluation.LiftUnitId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            yield return new GaPositionSnapshotDto
            {
                PositionId = positionId,
                SetupIndex = index + 1,
                SetupReference = $"Point_{index + 1}",
                OriginalCandidatePointIndex = index,
                LocalPosition = ToPoint(position.X, position.Y, position.Z),
                NewLiftableCount = position.NewLiftableCount,
                FinalAssignedCount = finalAssigned.Count,
                FinalAssignedLiftUnitIds = finalAssigned,
                Evaluations = evaluations
            };
        }
    }

    private static GaElementEvaluationSnapshotDto ToEvaluation(
        string optimizationRunId,
        string positionId,
        ElementLiftResult row,
        IReadOnlyDictionary<string, double?> liftUnitWeights,
        GASettings settings)
    {
        var elementWeight = row.Weight ?? (liftUnitWeights.TryGetValue(row.ElementId, out var weight) ? weight : null);
        var hookWeight = settings.AdditionalWt;
        var utilization = row.AvailableCapacity > 0 && row.RequiredCapacity > 0
            ? row.RequiredCapacity / row.AvailableCapacity * 100.0
            : (double?)null;
        var finalOwner = string.IsNullOrWhiteSpace(row.FinalOwnerPointId) ? null : row.FinalOwnerPointId;
        var candidatePointId = string.IsNullOrWhiteSpace(row.CandidatePointId) ? positionId : row.CandidatePointId;
        var evaluationId = StableEvaluationId(optimizationRunId, positionId, row.ElementId);
        var classification = row.Status == LiftStatus.Liftable
            ? ("Liftable", "Liftable", string.Empty)
            : ClassifyFailure(row);

        return new GaElementEvaluationSnapshotDto
        {
            EvaluationId = evaluationId,
            LiftUnitId = row.ElementId,
            CandidatePointId = candidatePointId,
            CandidatePointIndex = row.CandidatePointIndex,
            LiftStatus = row.Status.ToString(),
            ReachStatus = row.ReachStatus.ToString(),
            ChartStatus = row.ChartStatus.ToString(),
            RadiusM = NullIfUnavailable(row.Radius),
            RequiredRadiusM = NullIfUnavailable(row.RequiredRadius),
            SelectedChartRadiusM = NullIfUnavailable(row.SelectedChartRadius),
            BoomLengthM = NullIfUnavailable(row.BoomLength),
            RequiredBoomLengthM = NullIfUnavailable(row.BoomLength),
            SelectedChartBoomLengthM = NullIfUnavailable(row.SelectedChartBoomLength),
            ElementWeightTon = elementWeight,
            HookRiggingWeightTon = hookWeight,
            RequiredCapacityTon = NullIfUnavailable(row.RequiredCapacity),
            RawChartCapacityTon = NullIfUnavailable(row.RawChartCapacity),
            AvailableCapacityTon = NullIfUnavailable(row.AvailableCapacity),
            SafetyMarginTon = row.AvailableCapacity > 0 || row.RequiredCapacity > 0 ? row.SafetyMargin : null,
            CapacityUtilizationPercent = utilization,
            DeratingRatio = settings.DeratingRatio,
            SafetyFactor = settings.SafetyFactor,
            CombinedCapacityFactor = settings.DeratingRatio * settings.SafetyFactor,
            LookupMode = row.LookupMode,
            IsExactMatch = row.IsExactMatch,
            IsSteppedRadius = row.IsSteppedRadius,
            IsBlankCell = row.IsBlankCell,
            IsZeroCell = row.IsZeroCell,
            SelectedVisualColumnIndex = row.SelectedVisualColumnIndex,
            SelectedVisualColumnHeader = row.SelectedVisualColumnHeader,
            SelectedNumberOfLines = row.SelectedNumberOfLines,
            SelectedOperatingConditionLabel = row.SelectedOperatingConditionLabel,
            IsConservativeDuplicateFallback = row.IsConservativeDuplicateFallback,
            DuplicateAlternativeCount = row.DuplicateAlternativeCount,
            DuplicateAlternativeSummary = row.DuplicateAlternativeSummary,
            CandidateBoomsChecked = row.CandidateBoomsChecked,
            RejectedCandidates = row.RejectedCandidates,
            CraneBaseLocal = ToPoint(row.CraneBaseX, row.CraneBaseY, row.CraneBaseZ),
            SlewCenterLocal = ToPoint(row.SlewCenterX, row.SlewCenterY, row.SlewCenterZ),
            BoomHingeLocal = ToPoint(row.BoomHingeX, row.BoomHingeY, row.BoomHingeZ),
            LoadPointLocal = ToPoint(row.LoadPointX, row.LoadPointY, row.LoadPointZ),
            RadiusOriginType = row.RadiusOriginType,
            BoomOriginType = row.BoomOriginType,
            IsFeasibleFromPoint = row.IsFeasibleFromPoint,
            IsFinalAssigned = row.IsFinalAssigned,
            IsFinalAssignedToPoint = row.IsFinalAssignedToPoint,
            AssignedPointId = string.IsNullOrWhiteSpace(row.AssignedPointId) ? null : row.AssignedPointId,
            AssignedPointIndex = row.AssignedPointIndex >= 0 ? row.AssignedPointIndex : null,
            FinalOwnerPointId = finalOwner,
            AssignmentReason = row.AssignmentReason,
            AssignmentScore = row.AssignmentScore,
            DuplicateAssignmentResolved = row.DuplicateAssignmentResolved,
            FeasiblePointIds = SplitIds(row.FeasiblePointIds),
            HasBestFailedCandidate = row.HasBestFailedCandidate,
            BestFailedPointId = string.IsNullOrWhiteSpace(row.BestFailedPointId) ? null : row.BestFailedPointId,
            BestFailedLiftStatus = row.HasBestFailedCandidate ? row.BestFailedLiftStatus.ToString() : null,
            BestFailedReachStatus = row.HasBestFailedCandidate ? row.BestFailedReachStatus.ToString() : null,
            BestFailedChartStatus = row.HasBestFailedCandidate ? row.BestFailedChartStatus.ToString() : null,
            BestFailedSafetyMarginTon = row.HasBestFailedCandidate ? row.BestFailedSafetyMargin : null,
            BestFailedRequiredRadiusM = row.HasBestFailedCandidate ? row.BestFailedRequiredRadius : null,
            BestFailedRequiredBoomLengthM = row.HasBestFailedCandidate ? row.BestFailedRequiredBoomLength : null,
            BestFailureReason = string.IsNullOrWhiteSpace(row.BestFailureReason) ? null : row.BestFailureReason,
            FailureReason = classification.Item1,
            FailureCategory = classification.Item2,
            SuggestedAction = classification.Item3
        };
    }

    private static IEnumerable<GaFinalOutcomeSnapshotDto> BuildFinalOutcomes(
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> liftUnits,
        IReadOnlyList<GaPositionSnapshotDto> positions,
        PlacementInput input,
        PlacementResult result)
    {
        var evaluations = positions
            .SelectMany(position => position.Evaluations.Select(evaluation => (position, evaluation)))
            .ToList();
        var elementLookup = input.Elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var liftUnit in liftUnits.OrderBy(unit => unit.LiftUnitId, StringComparer.Ordinal))
        {
            var finalOwner = evaluations.FirstOrDefault(item =>
                item.evaluation.LiftUnitId == liftUnit.LiftUnitId
                && item.evaluation.IsFinalAssignedToPoint
                && item.evaluation.LiftStatus == LiftStatus.Liftable.ToString());

            if (finalOwner.evaluation != null)
            {
                yield return new GaFinalOutcomeSnapshotDto
                {
                    LiftUnitId = liftUnit.LiftUnitId,
                    IsLifted = true,
                    FinalEvaluationId = finalOwner.evaluation.EvaluationId,
                    FinalOwnerPositionId = finalOwner.position.PositionId,
                    FinalLiftStatus = finalOwner.evaluation.LiftStatus,
                    FailureReason = "Liftable",
                    FailureCategory = "Liftable"
                };
                continue;
            }

            var candidates = evaluations
                .Where(item => item.evaluation.LiftUnitId == liftUnit.LiftUnitId)
                .Select(item => item.evaluation)
                .ToList();
            var bestFailed = candidates.FirstOrDefault(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.BestFailedPointId)
                    && string.Equals(candidate.CandidatePointId, candidate.BestFailedPointId, StringComparison.OrdinalIgnoreCase))
                ?? candidates.OrderByDescending(candidate => candidate.AssignmentScore ?? double.MinValue).FirstOrDefault();
            var elementWeight = liftUnit.LiftWeightTon;
            var classification = bestFailed == null
                ? ("No saved evaluation", "Unavailable", "Review GA result snapshot.")
                : ClassifyFailure(bestFailed, elementWeight, input.CraneSpec?.MaxBoomLength ?? 0);

            yield return new GaFinalOutcomeSnapshotDto
            {
                LiftUnitId = liftUnit.LiftUnitId,
                IsLifted = false,
                BestFailedEvaluationId = bestFailed?.EvaluationId,
                FinalLiftStatus = bestFailed?.LiftStatus ?? "Unassigned",
                FailureReason = classification.Item1,
                FailureCategory = classification.Item2,
                SuggestedAction = classification.Item3
            };
        }
    }

    private static GaStageSummarySnapshotDto ToStageSummary(GaStageExecutionResult stage) => new()
    {
        OptimizationRunId = stage.OptimizationRunId,
        StageRunId = stage.StageRunId,
        StageIndex = stage.StageIndex,
        WasSelectedAsFinal = stage.WasSelectedAsFinal,
        StartedAtUtc = stage.StartedAtUtc,
        CompletedAtUtc = stage.CompletedAtUtc,
        DurationMilliseconds = stage.DurationMilliseconds,
        TerminationReason = stage.TerminationReason,
        MaxCranePositions = stage.EffectiveSettings.MaxCranePositions,
        RelocationWeight = stage.EffectiveSettings.RelocationWeight,
        UseHungarianAssignment = stage.EffectiveSettings.UseHungarianAssignment,
        EnableGroupedLifts = stage.EffectiveSettings.EnableGroupedLifts,
        LoadChartNumberOfLinesConstraint = stage.EffectiveSettings.LoadChartNumberOfLinesConstraint,
        LoadChartOperatingConditionConstraint = stage.EffectiveSettings.LoadChartOperatingConditionConstraint,
        RandomSeed = stage.EffectiveSettings.RandomSeed,
        PopulationSize = stage.EffectiveSettings.PopulationSize,
        MaxGenerations = stage.EffectiveSettings.MaxGenerations,
        EarlyStoppingGenerations = stage.EffectiveSettings.EarlyStoppingGenerations,
        FitnessScore = stage.Result.FitnessScore,
        CoverageRatio = stage.Result.CoverageRatio,
        LiftedLiftUnitCount = stage.Result.LiftedElementCount,
        TotalTargetLiftUnitCount = stage.Result.TotalElementCount,
        PositionCount = stage.Result.Positions.Count
    };

    private static (double? Length, double? Width, double? Height, string Source, string Method) ResolveGaDimensions(PrecastElement element)
    {
        if (element.ElementLength is > 0 || element.ElementWidth is > 0 || element.ElementHeight is > 0)
        {
            return (
                NullIfUnavailable(element.ElementLength ?? 0),
                NullIfUnavailable(element.ElementWidth ?? 0),
                NullIfUnavailable(element.ElementHeight ?? 0),
                "Explicit IFC dimensions",
                "ElementLength/ElementWidth/ElementHeight");
        }

        if (element.BBoxLength is > 0 || element.BBoxWidth is > 0 || element.BBoxHeight is > 0)
        {
            return (
                NullIfUnavailable(element.BBoxLength ?? 0),
                NullIfUnavailable(element.BBoxWidth ?? 0),
                NullIfUnavailable(element.BBoxHeight ?? 0),
                "Persisted bounding box",
                "BBoxLength/BBoxWidth/BBoxHeight");
        }

        if (element.BoundingBox_M.X > 0 || element.BoundingBox_M.Y > 0 || element.BoundingBox_M.Z > 0)
        {
            return (
                NullIfUnavailable(element.BoundingBox_M.X),
                NullIfUnavailable(element.BoundingBox_M.Y),
                NullIfUnavailable(element.BoundingBox_M.Z),
                "Transformed bounding box",
                "BoundingBox_M axes");
        }

        return (null, null, null, "Unavailable in this saved run", "Unavailable in this saved run");
    }

    private static double? ResolveGaVolume(PrecastElement element, double? length, double? width, double? height)
    {
        if (element.VolumeM3 is > 0)
            return element.VolumeM3;
        if (element.ElementVolume is > 0)
            return element.ElementVolume;
        if (length is > 0 && width is > 0 && height is > 0)
            return length.Value * width.Value * height.Value;
        return null;
    }

    private static GaCraneSnapshotDto BuildCrane(Domain.Entities.CraneSpecification? spec, GASettings settings)
    {
        var loadChartReference = BuildLoadChartReference(spec);
        return new GaCraneSnapshotDto
        {
            CraneId = spec?.CraneId,
            CraneSpecificationId = spec?.Id,
            CraneName = spec?.Crane?.Name ?? string.Empty,
            Manufacturer = spec?.Crane?.Manufacturer,
            ModelOrSpecification = spec?.LoadChartSourceDescription ?? spec?.Id.ToString(CultureInfo.InvariantCulture),
            ModelName = spec?.Crane?.Name,
            SpecificationName = spec?.Id.ToString(CultureInfo.InvariantCulture),
            CraneType = spec?.Crane?.CraneType.ToString(),
            MinRadiusM = spec?.MinRadius,
            MaxRadiusM = spec?.MaxRadius,
            MinBoomLengthM = spec?.MinBoomLength,
            MaxBoomLengthM = spec?.MaxBoomLength,
            MaxCapacityTon = spec?.MaxCapacity,
            VehicleLengthM = spec?.Crane?.VehicleLength,
            VehicleWidthM = spec?.Crane?.VehicleWidth,
            VehicleHeightM = spec?.Crane?.VehicleHeight,
            OutriggerLengthM = spec?.Crane?.OutriggerLength,
            OutriggerWidthM = spec?.Crane?.OutriggerWidth,
            OutriggerConfiguration = spec?.OutriggerConfig,
            CounterWeightTon = spec?.CounterWeight,
            WireLengthM = null,
            HoistWireLengthM = 0.0,
            HookRiggingAdditionalWeightTon = settings.AdditionalWt,
            DeratingRatio = settings.DeratingRatio,
            SafetyFactor = settings.SafetyFactor,
            CombinedCapacityFactor = settings.DeratingRatio * settings.SafetyFactor,
            LoadChartNameOrReference = loadChartReference,
            LoadChartSource = BuildLoadChartSource(spec)
        };
    }

    private static GaLiftingAccessoriesSnapshotDto BuildLiftingAccessories(
        Domain.Entities.CraneSpecification? spec,
        GASettings settings)
    {
        var totalBelowHookWeight = Math.Max(0, settings.AdditionalWt);
        return new GaLiftingAccessoriesSnapshotDto
        {
            WireRopeLengthM = settings.WireLength,
            RiggingWireRopeLengthM = settings.WireLength,
            HookRiggingWeightTon = totalBelowHookWeight,
            TotalBelowHookWeightTon = totalBelowHookWeight,
            TotalLiftingAccessoriesWeightTon = totalBelowHookWeight,
            NumberOfFallsOrLines = settings.LoadChartNumberOfLinesConstraint,
            RiggingConfigurationName = string.IsNullOrWhiteSpace(settings.LoadChartOperatingConditionConstraint)
                ? "NotProvided"
                : settings.LoadChartOperatingConditionConstraint,
            Completeness = totalBelowHookWeight > 0 ? "AggregateOnly" : "NotProvided",
            Items = Array.Empty<GaLiftingAccessoryItemSnapshotDto>()
        };
    }

    private static GaLoadChartSnapshotDto BuildLoadChart(
        Domain.Entities.CraneSpecification? spec,
        string fingerprint,
        GASettings settings)
    {
        var points = spec?.LoadChart?.ToList() ?? new List<Domain.Entities.TargetPoint>();
        return new GaLoadChartSnapshotDto
        {
            CraneSpecificationId = spec?.Id,
            ChartNameOrSourceDescription = BuildLoadChartReference(spec),
            SourceFileName = BuildLoadChartSource(spec),
            LoadChartFingerprint = fingerprint,
            TargetPointCount = points.Count,
            PositiveUsableCapacityCount = points.Count(point => point.Capacity > 0),
            DistinctVisualColumnCount = points.Select(point => point.VisualColumnIndex?.ToString(CultureInfo.InvariantCulture) ?? point.VisualColumnHeader ?? $"{point.Radius}:{point.BoomLength}").Distinct(StringComparer.Ordinal).Count(),
            LookupPolicy = "ConservativeStep",
            RequestedNumberOfLinesConstraint = settings.LoadChartNumberOfLinesConstraint,
            RequestedOperatingConditionConstraint = settings.LoadChartOperatingConditionConstraint,
            ResolvedOperatingSetupDescription = settings.LoadChartOperatingConditionConstraint,
            DuplicateColumnResolutionPolicy = spec?.LoadChartCapacityPolicy ?? "ConservativeMinimumAcrossDuplicateVisualColumns"
        };
    }

    private static string? BuildLoadChartReference(Domain.Entities.CraneSpecification? spec)
    {
        if (!string.IsNullOrWhiteSpace(spec?.LoadChartSourceDescription))
            return spec.LoadChartSourceDescription;

        if (spec == null)
            return null;

        var craneName = string.IsNullOrWhiteSpace(spec.Crane?.Name) ? "Crane" : spec.Crane.Name;
        return $"Specification {spec.Id} load chart ({craneName})";
    }

    private static string? BuildLoadChartSource(Domain.Entities.CraneSpecification? spec)
    {
        if (!string.IsNullOrWhiteSpace(spec?.LoadChartSourceDescription))
            return spec.LoadChartSourceDescription;

        return spec?.LoadChart?.Count > 0
            ? "Crane specification load chart"
            : null;
    }

    private static GaInputScopeSnapshotDto BuildInputScope(PlacementInput input, GASettings settings) => new()
    {
        RawSceneElementCount = input.RawElementCount,
        RawIncludeInGaCount = input.FeatureScopeElements.Count(element => element.IncludeInGa),
        OptimizationScopeElementCount = input.OptimizationScopeElementCount,
        ReferenceGeometryExcludedCount = Math.Max(0, input.RawElementCount - input.OptimizationScopeElementCount),
        SubmittedLiftUnitCount = input.Elements.Count,
        RawGaEligibleElementCount = input.RawGaEligibleElementCount,
        GroupedAssemblyCount = input.GroupedAssemblyCount,
        UngroupedElementCount = input.UngroupedElementCount,
        RequestedMaxCranePositions = input.RequestedMaxCranePositions,
        AllowedZoneCount = input.Zones?.Count ?? 0,
        ForbiddenZoneCount = input.ForbiddenZones?.Count ?? 0,
        CandidateSetupPointCount = null,
        ValidCandidatePointCount = null,
        RejectedCandidatePointCount = null,
        EnableGroupedLiftsRequested = settings.EnableGroupedLifts,
        EffectiveLiftMode = settings.EnableGroupedLifts ? "GroupedAssemblies" : "Individual",
        TargetElementTypes = settings.TargetElementTypes?.ToArray() ?? Array.Empty<string>(),
        SubmittedLiftUnitIds = input.Elements.Select(element => element.ElementID).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
        FeatureScopeElementIds = input.FeatureScopeElements.Select(element => element.ElementID).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(1000).ToList()
    };

    private static GaPerformanceSnapshotDto BuildPerformance(GaExecutionResult execution, GASettings settings) => new()
    {
        ConfiguredGenerations = settings.MaxGenerations,
        TotalRuntimeMilliseconds = execution.DurationMilliseconds
    };

    private static GaReachabilitySnapshotDto? BuildReachability(PlacementResult result)
    {
        var diagnostics = result.ReachabilityDiagnostics;
        if (diagnostics == null)
            return null;

        return new GaReachabilitySnapshotDto
        {
            TotalCandidatePoints = diagnostics.TotalCandidatePoints,
            ValidCandidatePoints = diagnostics.ValidCandidatePoints,
            RejectedCandidatePoints = diagnostics.RejectedCandidatePoints,
            ZeroCoveragePoints = diagnostics.ZeroCoveragePoints,
            HotSpotCount = diagnostics.HotSpotCount,
            MaximumHotSpotCoverage = (int)Math.Round(diagnostics.MaxHotSpotCoverage, MidpointRounding.AwayFromZero),
            AverageHotSpotCoverage = diagnostics.AverageHotSpotCoverage,
            ReachabilityMean = diagnostics.ReachabilityMean,
            ReachabilityStandardDeviation = diagnostics.ReachabilityStdDev
        };
    }

    private static IReadOnlyList<string> BuildDiagnosticReferences(PlacementInput input)
    {
        var root = input.ProjectFolderPath;
        var dir = input.DiagnosticsReportsDirectory;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(dir))
            return Array.Empty<string>();

        var runDir = Path.Combine(dir, SanitizeSegment(input.Settings.OptimizationRunId));
        if (!Directory.Exists(runDir))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(runDir, "*.csv", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string ComputeModelFingerprint(IEnumerable<PrecastElement> elements)
    {
        var projection = elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .OrderBy(element => element.ElementID, StringComparer.Ordinal)
            .Select(element => string.Join("|",
                element.ElementID,
                element.ElementType,
                element.RawIfcType,
                element.ResolvedCategory,
                element.ElementWT?.ToString("R", CultureInfo.InvariantCulture),
                PointKey(element.ElementCOG),
                element.ParentGlobalId,
                element.MainPartGlobalId,
                string.Join(";", element.ChildGlobalIds.OrderBy(id => id, StringComparer.Ordinal)),
                element.ValidationStatus,
                string.Join(";", element.ValidationWarnings.OrderBy(warning => warning, StringComparer.Ordinal))));
        return HashText(string.Join("\n", projection));
    }

    private static string ComputeLoadChartFingerprint(IEnumerable<Domain.Entities.TargetPoint>? points)
    {
        var projection = (points ?? Array.Empty<Domain.Entities.TargetPoint>())
            .OrderBy(point => point.Radius)
            .ThenBy(point => point.BoomLength)
            .ThenBy(point => point.VisualColumnIndex ?? int.MaxValue)
            .ThenBy(point => point.VisualColumnHeader, StringComparer.Ordinal)
            .Select(point => string.Join("|",
                point.Radius.ToString("R", CultureInfo.InvariantCulture),
                point.BoomLength.ToString("R", CultureInfo.InvariantCulture),
                point.Capacity.ToString("R", CultureInfo.InvariantCulture),
                point.VisualColumnIndex,
                point.VisualColumnHeader,
                point.NumberOfLines,
                point.OperatingConditionLabel));
        return HashText(string.Join("\n", projection));
    }

    private static string StableEvaluationId(string runId, string positionId, string liftUnitId) =>
        "eval_" + HashText($"{runId}|{positionId}|{liftUnitId}")[..16];

    private static string HashText(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? TryHashFile(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static long? TryFileSize(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? new FileInfo(path).Length : null;

    private static DateTimeOffset? TryLastWrite(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;

    private static string? BuildProjectRelativeIfcPath(string? projectRoot, string? ifcFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(ifcFilePath))
            return null;

        return Path.GetRelativePath(projectRoot, ifcFilePath).Replace('\\', '/');
    }

    private static (string, string, string) ClassifyFailure(ElementLiftResult row)
    {
        var classification = LiftClassificationPolicy.Classify(
            row.Status,
            row.ChartStatus,
            row.Weight,
            row.BoomLength,
            row.SelectedChartBoomLength,
            row.CandidatePointIndex >= 0);
        return (classification.FailureReason, classification.FailureCategory, classification.SuggestedAction);
    }

    private static (string, string, string) ClassifyFailure(GaElementEvaluationSnapshotDto row, double? elementWeight, double maxBoomLength)
    {
        _ = Enum.TryParse(row.LiftStatus, out LiftStatus liftStatus);
        _ = Enum.TryParse(row.ChartStatus, out ChartStatus chartStatus);
        var classification = LiftClassificationPolicy.Classify(
            liftStatus,
            chartStatus,
            elementWeight,
            row.BoomLengthM ?? 0,
            maxBoomLength,
            true);
        return (classification.FailureReason, classification.FailureCategory, classification.SuggestedAction);
    }

    private static IReadOnlyList<string> SplitIds(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static GaPointSnapshotDto ToPoint(DomainPoint3D point) => ToPoint(point.X, point.Y, point.Z);

    private static GaPointSnapshotDto ToPoint(double x, double y, double z) => new()
    {
        X = x,
        Y = y,
        Z = z,
        Frame = "ProjectLocalMeters",
        Unit = "m"
    };

    private static string PointKey(DomainPoint3D point) =>
        $"{point.X:R},{point.Y:R},{point.Z:R}";

    private static double? NullIfUnavailable(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) < double.Epsilon ? null : value;

    private static bool IsZeroPoint(DomainPoint3D point) =>
        Math.Abs(point.X) < double.Epsilon
        && Math.Abs(point.Y) < double.Epsilon
        && Math.Abs(point.Z) < double.Epsilon;

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars).Trim();
    }

    private static GASettings CloneSettings(GASettings settings)
    {
        var clone = settings.Clone();
        if (settings.TargetElementTypes != null)
            clone.TargetElementTypes = settings.TargetElementTypes.ToArray();
        return clone;
    }
}

public sealed class GaResultSnapshotValidator : IGaResultSnapshotValidator
{
    public GaSnapshotValidationResult Validate(GaResultSnapshotDto snapshot)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (snapshot.SchemaVersion is not (1 or 2 or 3))
            errors.Add("Unsupported SchemaVersion.");
        if (snapshot.SnapshotType != "CraneOptimizer.GaResult")
            errors.Add("Invalid SnapshotType.");
        if (string.IsNullOrWhiteSpace(snapshot.Run.OptimizationRunId))
            errors.Add("Run.OptimizationRunId is required.");
        if (string.IsNullOrWhiteSpace(snapshot.Project.ProjectGuid))
            errors.Add("Project.ProjectGuid is required.");
        if (snapshot.Run.Status == "Completed" && snapshot.Run.CompletedAtUtc == null)
            errors.Add("Completed snapshots require Run.CompletedAtUtc.");

        var total = snapshot.Summary.TotalTargetLiftUnitCount;
        var lifted = snapshot.Summary.LiftedLiftUnitCount;
        if (total < 0 || lifted < 0 || lifted > total)
            errors.Add("Invalid lifted/total counts.");
        if (snapshot.Summary.UnliftedLiftUnitCount != total - lifted)
            errors.Add("Unlifted count does not equal total minus lifted.");
        if (snapshot.Summary.CoverageRatio is < 0 or > 1)
            errors.Add("CoverageRatio must be between 0 and 1.");
        var expectedCoverage = total == 0 ? 0 : (double)lifted / total;
        if (Math.Abs(snapshot.Summary.CoverageRatio - expectedCoverage) > 0.005)
            errors.Add("CoverageRatio does not match lifted/total target count.");

        RequireUnique(snapshot.LiftUnits.Select(unit => unit.LiftUnitId), "LiftUnitId", errors);
        RequireUnique(snapshot.Positions.Select(position => position.PositionId), "PositionId", errors);
        RequireUnique(snapshot.Positions.SelectMany(position => position.Evaluations).Select(evaluation => evaluation.EvaluationId), "EvaluationId", errors);
        RequireUnique(snapshot.FinalOutcomes.Select(outcome => outcome.LiftUnitId), "FinalOutcome.LiftUnitId", errors);

        var liftUnitIds = snapshot.LiftUnits.Select(unit => unit.LiftUnitId).ToHashSet(StringComparer.Ordinal);
        var positionIds = snapshot.Positions.Select(position => position.PositionId).ToHashSet(StringComparer.Ordinal);
        var evaluationIds = snapshot.Positions.SelectMany(position => position.Evaluations).Select(evaluation => evaluation.EvaluationId).ToHashSet(StringComparer.Ordinal);
        if (snapshot.FinalOutcomes.Count != total)
            errors.Add("FinalOutcomes.Count must equal TotalTargetLiftUnitCount.");

        foreach (var outcome in snapshot.FinalOutcomes)
        {
            if (!liftUnitIds.Contains(outcome.LiftUnitId))
                errors.Add($"FinalOutcome references unknown LiftUnitId '{outcome.LiftUnitId}'.");
            if (!string.IsNullOrWhiteSpace(outcome.FinalEvaluationId) && !evaluationIds.Contains(outcome.FinalEvaluationId))
                errors.Add($"FinalEvaluationId '{outcome.FinalEvaluationId}' does not exist.");
            if (!string.IsNullOrWhiteSpace(outcome.BestFailedEvaluationId) && !evaluationIds.Contains(outcome.BestFailedEvaluationId))
                errors.Add($"BestFailedEvaluationId '{outcome.BestFailedEvaluationId}' does not exist.");
            if (!string.IsNullOrWhiteSpace(outcome.FinalOwnerPositionId) && !positionIds.Contains(outcome.FinalOwnerPositionId))
                errors.Add($"FinalOwnerPositionId '{outcome.FinalOwnerPositionId}' does not exist.");
            if (outcome.IsLifted && (string.IsNullOrWhiteSpace(outcome.FinalEvaluationId) || string.IsNullOrWhiteSpace(outcome.FinalOwnerPositionId)))
                errors.Add($"Lifted outcome '{outcome.LiftUnitId}' requires final evaluation and owner position.");
            if (!outcome.IsLifted && !string.IsNullOrWhiteSpace(outcome.FinalOwnerPositionId))
                errors.Add($"Unlifted outcome '{outcome.LiftUnitId}' must not have final owner.");
        }

        var finalAssignments = snapshot.Positions
            .SelectMany(position => position.Evaluations)
            .Where(evaluation => evaluation.IsFinalAssignedToPoint)
            .GroupBy(evaluation => evaluation.LiftUnitId, StringComparer.Ordinal)
            .ToList();
        if (finalAssignments.Any(group => group.Count() > 1))
            errors.Add("At least one lift unit has more than one final owner.");

        var liftedOutcomes = snapshot.FinalOutcomes.Count(outcome => outcome.IsLifted);
        if (liftedOutcomes != lifted)
            errors.Add("Lifted FinalOutcomes count does not match summary.");
        var assignedCount = snapshot.Positions.Sum(position => position.FinalAssignedCount);
        if (assignedCount != lifted)
            errors.Add("Sum of Position.FinalAssignedCount does not match lifted count.");

        foreach (var position in snapshot.Positions)
        {
            foreach (var id in position.FinalAssignedLiftUnitIds)
            {
                if (!snapshot.FinalOutcomes.Any(outcome => outcome.LiftUnitId == id && outcome.IsLifted))
                    errors.Add($"Position '{position.PositionId}' references non-lifted final lift unit '{id}'.");
            }
        }

        foreach (var evaluation in snapshot.Positions.SelectMany(position => position.Evaluations))
        {
            ValidateFinite(evaluation, errors);
            ValidateCapacityFormula(evaluation, warnings);
            ValidatePoint(evaluation.CraneBaseLocal, errors);
            ValidatePoint(evaluation.SlewCenterLocal, errors);
            ValidatePoint(evaluation.BoomHingeLocal, errors);
            ValidatePoint(evaluation.LoadPointLocal, errors);
        }

        foreach (var point in snapshot.Positions.Select(position => position.LocalPosition))
            ValidatePoint(point, errors);

        if (string.IsNullOrWhiteSpace(snapshot.InputScope.EffectiveLiftMode))
            errors.Add("EffectiveLiftMode is required.");
        if (snapshot.LiftUnits.Any(unit => string.IsNullOrWhiteSpace(unit.LiftUnitKind)))
            errors.Add("Every LiftUnit requires LiftUnitKind.");
        if (snapshot.SchemaVersion >= 2)
        {
            foreach (var unit in snapshot.LiftUnits)
            {
                if (unit.LengthM is not > 0 || unit.WidthM is not > 0 || unit.HeightM is not > 0)
                    errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' requires positive LengthM, WidthM, and HeightM.");
                if (string.IsNullOrWhiteSpace(unit.ElementName))
                    errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' requires ElementName.");
                if (string.IsNullOrWhiteSpace(unit.ElementType) || string.Equals(unit.ElementType, "Unknown", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' requires resolved ElementType.");
                if (string.IsNullOrWhiteSpace(unit.MaterialType) || string.Equals(unit.MaterialType, "Unknown", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' requires MaterialType.");
                if (string.IsNullOrWhiteSpace(unit.SharedDataHash))
                    errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' requires SharedDataHash.");
                else
                {
                    var expectedHash = CanonicalLiftUnitSnapshotBuilder.ComputeSharedDataHash(unit);
                    if (!string.Equals(unit.SharedDataHash, expectedHash, StringComparison.Ordinal))
                        errors.Add($"Schema v2 LiftUnit '{unit.LiftUnitId}' SharedDataHash does not match its shared fields.");
                }
            }
        }

        if (snapshot.SchemaVersion >= 3)
        {
            if (snapshot.Run.SnapshotSchemaVersion != snapshot.SchemaVersion)
                errors.Add("Run.SnapshotSchemaVersion must match SchemaVersion.");
            if (snapshot.Model.OptimizationInputFingerprintVersion != GaOptimizationInputFingerprint.CurrentVersion)
                errors.Add($"Schema v3 requires OptimizationInputFingerprintVersion {GaOptimizationInputFingerprint.CurrentVersion}.");
            if (string.IsNullOrWhiteSpace(snapshot.Model.OptimizationInputFingerprint))
                errors.Add("Schema v3 requires OptimizationInputFingerprint.");
            foreach (var componentName in GaOptimizationInputFingerprint.RequiredComponentNames)
            {
                if (!snapshot.Model.OptimizationInputFingerprintComponents.TryGetValue(componentName, out var value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"Schema v3 requires OptimizationInputFingerprintComponents.{componentName}.");
                }
            }
        }

        return new GaSnapshotValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private static void RequireUnique(IEnumerable<string> values, string label, ICollection<string> errors)
    {
        var duplicates = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
            errors.Add($"{label} values must be unique. Duplicate: {duplicates[0]}");
    }

    private static void ValidateFinite(GaElementEvaluationSnapshotDto evaluation, ICollection<string> errors)
    {
        foreach (var property in typeof(GaElementEvaluationSnapshotDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(double?) || property.GetValue(evaluation) is not double value)
                continue;
            if (double.IsNaN(value) || double.IsInfinity(value))
                errors.Add($"{evaluation.EvaluationId}.{property.Name} is not finite.");
            if ((property.Name is nameof(GaElementEvaluationSnapshotDto.RequiredCapacityTon) or nameof(GaElementEvaluationSnapshotDto.AvailableCapacityTon)) && value < 0)
                errors.Add($"{evaluation.EvaluationId}.{property.Name} must be non-negative.");
        }
    }

    private static void ValidateCapacityFormula(GaElementEvaluationSnapshotDto evaluation, ICollection<string> warnings)
    {
        const double tolerance = 0.02;
        if (evaluation.ElementWeightTon.HasValue
            && evaluation.HookRiggingWeightTon.HasValue
            && evaluation.RequiredCapacityTon.HasValue
            && Math.Abs(evaluation.RequiredCapacityTon.Value - (evaluation.ElementWeightTon.Value + evaluation.HookRiggingWeightTon.Value)) > tolerance)
        {
            warnings.Add($"{evaluation.EvaluationId} required capacity differs from element + hook/rigging weight.");
        }

        if (evaluation.AvailableCapacityTon.HasValue
            && evaluation.RequiredCapacityTon.HasValue
            && evaluation.SafetyMarginTon.HasValue
            && Math.Abs(evaluation.SafetyMarginTon.Value - (evaluation.AvailableCapacityTon.Value - evaluation.RequiredCapacityTon.Value)) > tolerance)
        {
            warnings.Add($"{evaluation.EvaluationId} safety margin differs from available - required capacity.");
        }

        if (evaluation.AvailableCapacityTon is > 0
            && evaluation.RequiredCapacityTon is > 0
            && evaluation.CapacityUtilizationPercent.HasValue)
        {
            var expected = evaluation.RequiredCapacityTon.Value / evaluation.AvailableCapacityTon.Value * 100.0;
            if (Math.Abs(evaluation.CapacityUtilizationPercent.Value - expected) > 0.2)
                warnings.Add($"{evaluation.EvaluationId} utilization differs from required / available capacity.");
        }
    }

    private static void ValidatePoint(GaPointSnapshotDto? point, ICollection<string> errors)
    {
        if (point == null)
            return;
        if (double.IsNaN(point.X) || double.IsNaN(point.Y) || double.IsNaN(point.Z)
            || double.IsInfinity(point.X) || double.IsInfinity(point.Y) || double.IsInfinity(point.Z))
        {
            errors.Add("Point contains NaN or Infinity.");
        }
        if (string.IsNullOrWhiteSpace(point.Frame) || string.IsNullOrWhiteSpace(point.Unit))
            errors.Add("Point frame and unit are required.");
    }
}
