using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Infrastructure.Optimization.GeneticAlgorithms.Snapshots;

public sealed class LegacyCsvGaSnapshotAdapter : IGaLegacyCsvSnapshotAdapter
{
    private static readonly string[] AssignmentNames =
    [
        "ga_audit_element_assignments.csv",
        "ga_element_assignments.csv",
        "ga_final_assignments.csv"
    ];

    private static readonly string[] PositionNames =
    [
        "ga_audit_positions.csv",
        "ga_positions.csv"
    ];

    private static readonly string[] SummaryNames =
    [
        "ga_audit_run_summary.csv",
        "ga_run_summary.csv",
        "ga_audit_run_history.csv",
        "ga_run_history.csv"
    ];

    private static readonly string[] SettingsNames =
    [
        "ga_audit_settings_snapshot.csv",
        "ga_settings_snapshot.csv"
    ];

    private static readonly string[] StageNames =
    [
        "ga_audit_stage_summary.csv",
        "ga_stage_summary.csv"
    ];

    private static readonly string[] MeaningfulNames = AssignmentNames
        .Concat(PositionNames)
        .Concat(SummaryNames)
        .Concat(SettingsNames)
        .Concat(StageNames)
        .ToArray();

    public async Task<IReadOnlyList<GaRunDescriptorDto>> ListRunsAsync(
        GaProjectActivationContext context,
        CancellationToken cancellationToken = default)
    {
        var candidates = DiscoverCandidateFolders(context.ProjectRoot).ToList();
        var descriptors = new List<GaRunDescriptorDto>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = await BuildDescriptorAsync(context, candidate, cancellationToken).ConfigureAwait(false);
            if (descriptor != null)
                descriptors.Add(descriptor);
        }

        return descriptors
            .OrderByDescending(item => item.CompletedAtUtc ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<ResolvedGaResultSnapshot> LoadAsync(
        GaProjectActivationContext context,
        GaRunDescriptorDto selectedRun,
        CancellationToken cancellationToken = default)
    {
        var runDir = Directory.Exists(selectedRun.SnapshotPath)
            ? selectedRun.SnapshotPath
            : Path.GetDirectoryName(selectedRun.SnapshotPath) ?? selectedRun.SnapshotPath;
        if (string.IsNullOrWhiteSpace(runDir) || !Directory.Exists(runDir))
        {
            return new ResolvedGaResultSnapshot
            {
                SourceKind = "PartialLegacy",
                Classification = "CurrentProjectUnverified",
                Warnings = ["Legacy GA result folder is missing."],
                SelectedRun = selectedRun
            };
        }

        var files = FindRunFiles(runDir);
        var warnings = new List<string>
        {
            "This is a legacy GA result. Some setup-specific and assignment-review details were not saved."
        };

        var positionRows = await ReadRowsAsync(files.Position, warnings, cancellationToken).ConfigureAwait(false);
        var assignmentRows = await ReadRowsAsync(files.Assignment, warnings, cancellationToken).ConfigureAwait(false);
        var summaryRows = await ReadRowsAsync(files.Summary, warnings, cancellationToken).ConfigureAwait(false);

        if (files.Assignment == null)
            warnings.Add("Legacy assignment CSV is missing; detailed final assignment rows are unavailable.");

        var runId = FirstNonEmpty(
            selectedRun.OptimizationRunId,
            FirstValue(assignmentRows, "OptimizationRunId", "RunId"),
            FirstValue(summaryRows, "OptimizationRunId", "RunId"),
            StableLegacyId(context.ProjectRoot, runDir, selectedRun.CompletedAtUtc));

        var positions = BuildPositions(positionRows, assignmentRows).ToList();
        var liftUnits = BuildLiftUnits(assignmentRows).ToList();
        var evaluations = BuildEvaluations(runId, positions, assignmentRows, liftUnits).ToList();
        AttachEvaluations(positions, evaluations);
        var outcomes = BuildOutcomes(liftUnits, evaluations).ToList();

        var total = Math.Max(
            ParseInt(FirstValue(summaryRows, "TotalTargetLiftUnitCount", "TotalTargetElements", "TotalElements")),
            outcomes.Count);
        var lifted = Math.Max(
            ParseInt(FirstValue(summaryRows, "LiftedLiftUnitCount", "LiftedElements", "Lifted")),
            outcomes.Count(item => item.IsLifted));
        if (total == 0)
            total = outcomes.Count;
        var coverage = ParseDouble(FirstValue(summaryRows, "CoverageRatio", "Coverage", "CoveragePercent"));
        if (coverage > 1)
            coverage /= 100.0;
        if (coverage <= 0 && total > 0)
            coverage = (double)lifted / total;

        var hasAssignments = outcomes.Count > 0;
        var hasCapacity = evaluations.Any(item => item.RequiredCapacityTon.HasValue || item.AvailableCapacityTon.HasValue);
        var hasFailures = outcomes.Any(item => !item.IsLifted && !string.IsNullOrWhiteSpace(item.FailureReason));
        var sourceKind = hasAssignments ? "LegacyCsv" : "PartialLegacy";
        var capabilities = new GaSnapshotCapabilitiesDto
        {
            HasFinalAssignments = hasAssignments,
            HasPositionEvaluationMatrix = false,
            HasFailureEvidence = hasFailures,
            HasLoadChartProvenance = evaluations.Any(item => item.RawChartCapacityTon.HasValue || item.SelectedChartRadiusM.HasValue),
            HasAssemblyMetadata = liftUnits.Any(item => item.IsAssemblyGroup),
            HasStageSummaries = files.Stage != null,
            SupportsSelectedPointFeasibility = false,
            SupportsAssignmentReview = false,
            SupportsAllFeasibleDebug = false,
            SourceKind = sourceKind
        };

        var snapshot = new GaResultSnapshotDto
        {
            SchemaVersion = 0,
            Run = new GaRunIdentitySnapshotDto
            {
                OptimizationRunId = runId,
                Status = "Completed",
                CompletedAtUtc = selectedRun.CompletedAtUtc ?? LatestWriteUtc(files.AllFiles)
            },
            Project = new GaProjectSnapshotDto
            {
                ProjectGuid = context.ProjectGuid,
                ProjectName = context.ProjectName,
                ProjectRelativeSnapshotPath = Path.GetRelativePath(context.ProjectRoot, runDir).Replace('\\', '/')
            },
            Model = new GaModelSnapshotDto(),
            RequestedSettings = BuildLegacySettings(files.Settings),
            EffectiveFinalSettings = BuildLegacySettings(files.Settings),
            Crane = new GaCraneSnapshotDto
            {
                CraneName = FirstNonEmpty(selectedRun.CraneName, FirstValue(summaryRows, "CraneName", "CraneNameOrId"))
            },
            InputScope = new GaInputScopeSnapshotDto
            {
                EffectiveLiftMode = FirstNonEmpty(selectedRun.EffectiveLiftMode, FirstValue(summaryRows, "EffectiveLiftMode", "LiftMode"), "Unknown"),
                SubmittedLiftUnitCount = total,
                SubmittedLiftUnitIds = liftUnits.Select(item => item.LiftUnitId).ToList()
            },
            Summary = new GaRunSummarySnapshotDto
            {
                TotalTargetLiftUnitCount = total,
                LiftedLiftUnitCount = lifted,
                UnliftedLiftUnitCount = Math.Max(0, total - lifted),
                PositionCount = positions.Count,
                CoverageRatio = coverage,
                Liftable = lifted
            },
            LiftUnits = liftUnits,
            Positions = positions,
            FinalOutcomes = outcomes,
            Capabilities = capabilities,
            Diagnostics = new GaDiagnosticsReferencesSnapshotDto
            {
                ProjectRelativeFiles = files.AllFiles
                    .Select(path => Path.GetRelativePath(context.ProjectRoot, path).Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList()
            }
        };

        return new ResolvedGaResultSnapshot
        {
            Snapshot = snapshot,
            SourceKind = sourceKind,
            Classification = selectedRun.Classification,
            Capabilities = capabilities,
            Warnings = selectedRun.Warnings.Concat(warnings).Distinct(StringComparer.Ordinal).ToList(),
            SelectedRun = selectedRun
        };
    }

    public async Task<ResolvedGaResultSnapshot> TryLoadLatestAsync(
        string projectRoot,
        string projectGuid,
        CancellationToken cancellationToken = default)
    {
        var context = new GaProjectActivationContext(projectRoot, projectGuid, string.Empty, null, null, null, null);
        var descriptors = await ListRunsAsync(context, cancellationToken).ConfigureAwait(false);
        var selected = descriptors.FirstOrDefault(item => item.Status == "Completed");
        if (selected == null)
        {
            return new ResolvedGaResultSnapshot
            {
                SourceKind = "None",
                Classification = "NoResult",
                Warnings = ["No GA result snapshot or legacy CSV diagnostics were found."]
            };
        }

        return await LoadAsync(context, selected, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> DiscoverCandidateFolders(string projectRoot)
    {
        var gaRoot = Path.Combine(projectRoot, "Reports", "GA");
        if (!Directory.Exists(gaRoot))
            yield break;

        foreach (var directory in Directory.EnumerateDirectories(gaRoot, "*", SearchOption.AllDirectories).Prepend(gaRoot))
        {
            if (Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Any(name => MeaningfulNames.Contains(name ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
            {
                yield return directory;
            }
        }
    }

    private static async Task<GaRunDescriptorDto?> BuildDescriptorAsync(
        GaProjectActivationContext context,
        string runDir,
        CancellationToken cancellationToken)
    {
        var files = FindRunFiles(runDir);
        var warnings = new List<string>
        {
            "Legacy run does not contain a stable project GUID. Ownership is inferred from its location inside the active project folder."
        };

        var assignmentRows = await ReadRowsAsync(files.Assignment, warnings, cancellationToken).ConfigureAwait(false);
        var summaryRows = await ReadRowsAsync(files.Summary, warnings, cancellationToken).ConfigureAwait(false);
        var positionRows = await ReadRowsAsync(files.Position, warnings, cancellationToken).ConfigureAwait(false);
        var completedAt = LatestWriteUtc(files.AllFiles);
        var runId = FirstNonEmpty(
            FirstValue(assignmentRows, "OptimizationRunId", "RunId"),
            FirstValue(summaryRows, "OptimizationRunId", "RunId"),
            TryGuidFolderName(runDir),
            StableLegacyId(context.ProjectRoot, runDir, completedAt));

        var total = Math.Max(
            ParseInt(FirstValue(summaryRows, "TotalTargetLiftUnitCount", "TotalTargetElements", "TotalElements")),
            CountDistinct(assignmentRows, "LiftUnitId", "ElementId", "ElementID"));
        var lifted = Math.Max(
            ParseInt(FirstValue(summaryRows, "LiftedLiftUnitCount", "LiftedElements", "Lifted")),
            CountLifted(assignmentRows));
        var coverage = ParseDouble(FirstValue(summaryRows, "CoverageRatio", "Coverage", "CoveragePercent"));
        if (coverage > 1)
            coverage /= 100.0;
        if (coverage <= 0 && total > 0)
            coverage = (double)lifted / total;

        var capabilities = new GaSnapshotCapabilitiesDto
        {
            HasFinalAssignments = assignmentRows.Count > 0,
            HasPositionEvaluationMatrix = false,
            HasFailureEvidence = assignmentRows.Any(row => !string.IsNullOrWhiteSpace(Value(row, "FailureReason", "FailureCategory", "BestFailureReason"))),
            HasLoadChartProvenance = assignmentRows.Any(row => !string.IsNullOrWhiteSpace(Value(row, "RawChartCapacity", "SelectedChartRadius", "SelectedChartBoomLength"))),
            SupportsSelectedPointFeasibility = false,
            SupportsAssignmentReview = false,
            SupportsAllFeasibleDebug = false,
            SourceKind = assignmentRows.Count > 0 ? "LegacyCsv" : "PartialLegacy"
        };

        return new GaRunDescriptorDto
        {
            OptimizationRunId = runId,
            CompletedAtUtc = completedAt,
            Status = "Completed",
            CoverageRatio = coverage,
            LiftedLiftUnitCount = lifted,
            TotalTargetLiftUnitCount = total,
            SetupCount = Math.Max(positionRows.Count, CountDistinct(assignmentRows, "SetupIndex", "AssignedSetupIndex", "SetupNo")),
            CraneName = FirstNonEmpty(FirstValue(summaryRows, "CraneName", "CraneNameOrId")),
            EffectiveLiftMode = FirstNonEmpty(FirstValue(summaryRows, "EffectiveLiftMode", "LiftMode"), "Unknown"),
            Classification = "CurrentProjectUnverified",
            SnapshotPath = runDir,
            Capabilities = capabilities,
            SourceKind = capabilities.SourceKind,
            Warnings = warnings
        };
    }

    private static IReadOnlyList<GaPositionSnapshotDto> BuildPositions(
        IReadOnlyList<IReadOnlyDictionary<string, string>> positionRows,
        IReadOnlyList<IReadOnlyDictionary<string, string>> assignmentRows)
    {
        var positions = positionRows.Select((row, index) =>
        {
            var setupIndex = ParseInt(Value(row, "SetupIndex", "SetupNo", "PositionIndex"));
            if (setupIndex <= 0)
                setupIndex = index + 1;
            var positionId = FirstNonEmpty(Value(row, "PositionId", "CandidatePointId", "PointId"), $"legacy-setup-{setupIndex}");
            return new GaPositionSnapshotDto
            {
                PositionId = positionId,
                SetupIndex = setupIndex,
                LocalPosition = new GaPointSnapshotDto
                {
                    X = ParseDouble(Value(row, "X", "CraneX", "PositionX")),
                    Y = ParseDouble(Value(row, "Y", "CraneY", "PositionY")),
                    Z = ParseDouble(Value(row, "Z", "CraneZ", "PositionZ")),
                    Frame = "ProjectLocalMeters",
                    Unit = "m"
                },
                FinalAssignedCount = ParseInt(Value(row, "FinalAssignedCount", "LiftedItemsCount", "AssignedCount")),
                FinalAssignedLiftUnitIds = Array.Empty<string>(),
                Evaluations = Array.Empty<GaElementEvaluationSnapshotDto>()
            };
        }).ToList();

        if (positions.Count == 0)
        {
            positions = assignmentRows
                .Select(row => ParseInt(Value(row, "SetupIndex", "AssignedSetupIndex", "SetupNo")))
                .Where(index => index > 0)
                .Distinct()
                .OrderBy(index => index)
                .Select(index => new GaPositionSnapshotDto
                {
                    PositionId = $"legacy-setup-{index}",
                    SetupIndex = index,
                    LocalPosition = new GaPointSnapshotDto { Frame = "ProjectLocalMeters", Unit = "m" }
                })
                .ToList();
        }

        if (positions.Count == 0)
        {
            positions.Add(new GaPositionSnapshotDto
            {
                PositionId = "legacy-setup-1",
                SetupIndex = 1,
                LocalPosition = new GaPointSnapshotDto { Frame = "ProjectLocalMeters", Unit = "m" }
            });
        }

        return positions;
    }

    private static IEnumerable<CanonicalLiftUnitSnapshotDto> BuildLiftUnits(IReadOnlyList<IReadOnlyDictionary<string, string>> assignmentRows)
    {
        foreach (var group in assignmentRows
                     .Select(row => new { Row = row, Id = FirstNonEmpty(Value(row, "LiftUnitId", "ElementId", "ElementID", "SourceElementGlobalId")) })
                     .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                     .GroupBy(item => item.Id, StringComparer.Ordinal))
        {
            var row = group.First().Row;
            yield return new CanonicalLiftUnitSnapshotDto
            {
                LiftUnitId = group.Key,
                SourceElementGlobalId = FirstNonEmpty(Value(row, "SourceElementGlobalId", "GlobalId", "ElementGlobalId")),
                ElementName = FirstNonEmpty(Value(row, "ElementName", "Name")),
                ElementType = FirstNonEmpty(Value(row, "ElementType", "Type")),
                ElementMark = FirstNonEmpty(Value(row, "ElementMark", "Mark")),
                CanonicalMark = FirstNonEmpty(Value(row, "CanonicalMark", "ResolvedDisplayMark")),
                AssemblyMark = FirstNonEmpty(Value(row, "AssemblyMark")),
                ParentAssemblyGlobalId = FirstNonEmpty(Value(row, "ParentAssemblyGlobalId")),
                MainPartGlobalId = FirstNonEmpty(Value(row, "MainPartGlobalId")),
                AssemblyMemberIds = SplitIds(Value(row, "AssemblyMemberIds", "AssemblyMemberIdsJoined")),
                IsAssemblyGroup = !string.IsNullOrWhiteSpace(Value(row, "AssemblyMemberIds", "AssemblyMemberIdsJoined")),
                LiftUnitKind = !string.IsNullOrWhiteSpace(Value(row, "AssemblyMemberIds", "AssemblyMemberIdsJoined")) ? "GroupedAssembly" : "IndividualElement",
                RoleAtRunTime = "Legacy",
                LiftWeightTon = ParseNullableDouble(Value(row, "ElementWeight", "Weight", "LiftWeightTon")),
                WasSubmittedToGa = true
            };
        }
    }

    private static IEnumerable<GaElementEvaluationSnapshotDto> BuildEvaluations(
        string runId,
        IReadOnlyList<GaPositionSnapshotDto> positions,
        IReadOnlyList<IReadOnlyDictionary<string, string>> assignmentRows,
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> liftUnits)
    {
        var liftUnitLookup = liftUnits.ToDictionary(item => item.LiftUnitId, StringComparer.Ordinal);
        foreach (var row in assignmentRows)
        {
            var liftUnitId = FirstNonEmpty(Value(row, "LiftUnitId", "ElementId", "ElementID", "SourceElementGlobalId"));
            if (string.IsNullOrWhiteSpace(liftUnitId))
                continue;

            var setupIndex = ParseInt(Value(row, "SetupIndex", "AssignedSetupIndex", "SetupNo"));
            var position = positions.FirstOrDefault(item => item.SetupIndex == setupIndex) ?? positions.First();
            var status = NormalizeStatus(FirstNonEmpty(Value(row, "LiftStatus", "Status"), "NotCharted"));
            var isLifted = IsLiftedStatus(status, row);
            var required = ParseNullableDouble(Value(row, "RequiredCapacity", "RequiredCapacityTon"));
            var available = ParseNullableDouble(Value(row, "AvailableCapacity", "AvailableCapacityTon"));
            var safety = ParseNullableDouble(Value(row, "SafetyMargin", "CapacityMargin"));
            safety ??= available.HasValue && required.HasValue ? available.Value - required.Value : null;
            var unitWeight = liftUnitLookup.TryGetValue(liftUnitId, out var unit) ? unit.LiftWeightTon : null;

            yield return new GaElementEvaluationSnapshotDto
            {
                EvaluationId = "legacy_" + StableHash($"{runId}|{position.PositionId}|{liftUnitId}")[..16],
                LiftUnitId = liftUnitId,
                CandidatePointId = position.PositionId,
                CandidatePointIndex = position.SetupIndex - 1,
                LiftStatus = status,
                ReachStatus = FirstNonEmpty(Value(row, "ReachStatus"), status),
                ChartStatus = FirstNonEmpty(Value(row, "ChartStatus"), status),
                RadiusM = ParseNullableDouble(Value(row, "Radius", "BestRadius")),
                RequiredRadiusM = ParseNullableDouble(Value(row, "RequiredRadius")),
                SelectedChartRadiusM = ParseNullableDouble(Value(row, "SelectedChartRadius")),
                BoomLengthM = ParseNullableDouble(Value(row, "BoomLength")),
                RequiredBoomLengthM = ParseNullableDouble(Value(row, "RequiredBoomLength")),
                SelectedChartBoomLengthM = ParseNullableDouble(Value(row, "SelectedChartBoomLength")),
                ElementWeightTon = ParseNullableDouble(Value(row, "ElementWeight", "Weight")) ?? unitWeight,
                HookRiggingWeightTon = ParseNullableDouble(Value(row, "HookRiggingWeight", "AdditionalWeight")),
                RequiredCapacityTon = required,
                RawChartCapacityTon = ParseNullableDouble(Value(row, "RawChartCapacity")),
                AvailableCapacityTon = available,
                SafetyMarginTon = safety,
                CapacityUtilizationPercent = ParseNullableDouble(Value(row, "CapacityUtilizationPercent", "Utilization")),
                LookupMode = FirstNonEmpty(Value(row, "LookupMode")),
                IsFeasibleFromPoint = isLifted,
                IsFinalAssigned = isLifted,
                IsFinalAssignedToPoint = isLifted,
                AssignedPointId = isLifted ? position.PositionId : null,
                AssignedPointIndex = isLifted ? position.SetupIndex - 1 : null,
                FinalOwnerPointId = isLifted ? position.PositionId : null,
                FailureReason = FirstNonEmpty(Value(row, "FailureReason", "BestFailureReason")),
                FailureCategory = FirstNonEmpty(Value(row, "FailureCategory")),
                SuggestedAction = FirstNonEmpty(Value(row, "SuggestedAction")),
                HasBestFailedCandidate = !isLifted,
                BestFailedPointId = !isLifted ? position.PositionId : null,
                BestFailedLiftStatus = !isLifted ? status : null,
                BestFailedReachStatus = !isLifted ? FirstNonEmpty(Value(row, "ReachStatus"), status) : null,
                BestFailedChartStatus = !isLifted ? FirstNonEmpty(Value(row, "ChartStatus"), status) : null,
                BestFailedSafetyMarginTon = !isLifted ? safety : null,
                BestFailedRequiredRadiusM = !isLifted ? ParseNullableDouble(Value(row, "RequiredRadius", "Radius", "BestRadius")) : null,
                BestFailedRequiredBoomLengthM = !isLifted ? ParseNullableDouble(Value(row, "RequiredBoomLength", "BoomLength")) : null
            };
        }
    }

    private static void AttachEvaluations(
        IList<GaPositionSnapshotDto> positions,
        IReadOnlyList<GaElementEvaluationSnapshotDto> evaluations)
    {
        for (var i = 0; i < positions.Count; i++)
        {
            var position = positions[i];
            var rows = evaluations.Where(item => item.CandidatePointId == position.PositionId).ToList();
            var assignedIds = rows.Where(item => item.IsFinalAssignedToPoint).Select(item => item.LiftUnitId).Distinct(StringComparer.Ordinal).ToList();
            positions[i] = new GaPositionSnapshotDto
            {
                PositionId = position.PositionId,
                SetupIndex = position.SetupIndex,
                OriginalCandidatePointIndex = position.OriginalCandidatePointIndex,
                LocalPosition = position.LocalPosition,
                SurveyPosition = position.SurveyPosition,
                ZoneId = position.ZoneId,
                ZoneName = position.ZoneName,
                NewLiftableCount = position.NewLiftableCount,
                FinalAssignedCount = assignedIds.Count > 0 ? assignedIds.Count : position.FinalAssignedCount,
                FinalAssignedLiftUnitIds = assignedIds,
                Evaluations = rows
            };
        }
    }

    private static IEnumerable<GaFinalOutcomeSnapshotDto> BuildOutcomes(
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> liftUnits,
        IReadOnlyList<GaElementEvaluationSnapshotDto> evaluations)
    {
        foreach (var unit in liftUnits)
        {
            var rows = evaluations.Where(item => item.LiftUnitId == unit.LiftUnitId).ToList();
            var final = rows.FirstOrDefault(item => item.IsFinalAssignedToPoint);
            var bestFailed = rows.FirstOrDefault(item => item.HasBestFailedCandidate) ?? rows.FirstOrDefault();
            yield return new GaFinalOutcomeSnapshotDto
            {
                LiftUnitId = unit.LiftUnitId,
                IsLifted = final != null,
                FinalEvaluationId = final?.EvaluationId,
                FinalOwnerPositionId = final?.FinalOwnerPointId,
                BestFailedEvaluationId = final == null ? bestFailed?.EvaluationId : null,
                FinalLiftStatus = final?.LiftStatus ?? bestFailed?.LiftStatus ?? "NotCharted",
                FailureReason = final == null ? bestFailed?.FailureReason : null,
                FailureCategory = final == null ? bestFailed?.FailureCategory : null,
                SuggestedAction = final == null ? bestFailed?.SuggestedAction : null
            };
        }
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ReadRowsAsync(
        string? path,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Array.Empty<IReadOnlyDictionary<string, string>>();

        try
        {
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return ParseCsv(text)
                .Where(row => row.Count > 0)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            warnings.Add($"Legacy CSV could not be parsed: {Path.GetFileName(path)}.");
            return Array.Empty<IReadOnlyDictionary<string, string>>();
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, string>> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '\ufeff')
            text = text[1..];

        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        if (records.Count == 0)
            yield break;

        var headers = records[0].Select(header => header.Trim()).ToArray();
        foreach (var row in records.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    values[headers[i]] = row[i].Trim();
            }
            yield return values;
        }
    }

    private static RunFiles FindRunFiles(string runDir)
    {
        var files = Directory.EnumerateFiles(runDir, "*.csv", SearchOption.TopDirectoryOnly).ToList();
        return new RunFiles(
            Find(files, AssignmentNames),
            Find(files, PositionNames),
            Find(files, SummaryNames),
            Find(files, SettingsNames),
            Find(files, StageNames),
            files);
    }

    private static string? Find(IEnumerable<string> files, IEnumerable<string> names) =>
        files.FirstOrDefault(path => names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));

    private static DateTimeOffset? LatestWriteUtc(IEnumerable<string> files)
    {
        var latest = files.Where(File.Exists).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max();
        return latest == default ? null : new DateTimeOffset(latest, TimeSpan.Zero);
    }

    private static string? FirstValue(IReadOnlyList<IReadOnlyDictionary<string, string>> rows, params string[] names) =>
        rows.Select(row => Value(row, names)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string Value(IReadOnlyDictionary<string, string> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }
        return string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0d;

    private static double? ParseNullableDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static IReadOnlyList<string> SplitIds(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeStatus(string status) =>
        status.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase) switch
        {
            "Liftable" or "Lifted" or "Success" => "Liftable",
            "OutOfRadius" => "OutOfRadius",
            "Overweight" => "Overweight",
            "HeightFail" => "HeightFail",
            "SpecialCase" => "SpecialCase",
            _ => "NotCharted"
        };

    private static bool IsLiftedStatus(string status, IReadOnlyDictionary<string, string> row)
    {
        var liftedText = Value(row, "IsLifted", "IsFinalAssigned", "IsFinalAssignedToPoint");
        if (liftedText.Equals("true", StringComparison.OrdinalIgnoreCase)
            || liftedText.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || liftedText == "1")
            return true;

        return status == "Liftable";
    }

    private static int CountDistinct(IReadOnlyList<IReadOnlyDictionary<string, string>> rows, params string[] names) =>
        rows.Select(row => Value(row, names))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();

    private static int CountLifted(IReadOnlyList<IReadOnlyDictionary<string, string>> rows) =>
        rows.Count(row => IsLiftedStatus(NormalizeStatus(FirstNonEmpty(Value(row, "LiftStatus", "Status"), "NotCharted")), row));

    private static string? TryGuidFolderName(string runDir)
    {
        var name = Path.GetFileName(runDir);
        return Guid.TryParse(name, out _) ? name : null;
    }

    private static string StableLegacyId(string projectRoot, string runDir, DateTimeOffset? completedAt) =>
        "legacy-" + StableHash($"{Path.GetRelativePath(projectRoot, runDir)}|{completedAt:O}")[..16];

    private static string StableHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static GaSettingsSnapshotDto BuildLegacySettings(string? settingsPath) => new()
    {
        OptimizationRunId = settingsPath == null ? string.Empty : Path.GetFileName(Path.GetDirectoryName(settingsPath)) ?? string.Empty
    };

    private sealed record RunFiles(
        string? Assignment,
        string? Position,
        string? Summary,
        string? Settings,
        string? Stage,
        IReadOnlyList<string> AllFiles);
}
