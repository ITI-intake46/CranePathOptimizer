using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class GACsvDiagnosticsService : IGACsvDiagnosticsService
    {
        private readonly ILiftingCalculationEngine _liftingEngine;
        private readonly ICraneConfigurationEngine _configEngine;
        private readonly ILoadChartCapacityResolver _capacityResolver;
        private readonly IProjectCoordinateTransformService? _transformService;
        private readonly IReachabilityMapService? _reachabilityMapService;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
            : this(liftingEngine, configEngine, null, null, null)
        {
        }

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver)
            : this(liftingEngine, configEngine, capacityResolver, null, null)
        {
        }

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            IProjectCoordinateTransformService? transformService)
            : this(liftingEngine, configEngine, null, transformService, null)
        {
        }

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver,
            IProjectCoordinateTransformService? transformService)
            : this(liftingEngine, configEngine, capacityResolver, transformService, null)
        {
        }

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver,
            IProjectCoordinateTransformService? transformService,
            IReachabilityMapService? reachabilityMapService)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
            _transformService = transformService;
            _reachabilityMapService = reachabilityMapService;
        }

        public string GenerateDiagnostics(PlacementInput input, PlacementResult finalResult)
        {
            var diagLevel = input.Settings?.DiagnosticsLevel ?? DiagnosticsLevel.Summary;
            if (diagLevel == DiagnosticsLevel.Off)
                return string.Empty;

            // 1. Resolve output folder
            string? reportsDir = input.DiagnosticsReportsDirectory;
            if (string.IsNullOrWhiteSpace(reportsDir))
            {
                string? projectFolder = input.ProjectFolderPath;
                if (!string.IsNullOrWhiteSpace(projectFolder))
                {
                    reportsDir = Path.Combine(projectFolder, "Reports", "GA");
                }
                else
                {
                    reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Reports", "GA");
                }
            }

            bool batchMode = input.Settings?.BatchModeEnabled ?? false;
            if (!batchMode)
            {
                // Per-run folder policy: <reportsDir>\<ProjectName>\<OptimizationRunId>\
                // Groups all CSVs from one execution into a dedicated subdirectory.
                string projectNameSegment = SanitizePathSegment(input.ProjectName);
                string runIdSegment = SanitizePathSegment(input.Settings?.OptimizationRunId);
                if (!string.IsNullOrEmpty(projectNameSegment) && !string.IsNullOrEmpty(runIdSegment))
                {
                    reportsDir = Path.Combine(reportsDir, projectNameSegment, runIdSegment);
                }
            }

            Directory.CreateDirectory(reportsDir);

            string timestampStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string timeFormatted = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string detailedFileName = batchMode ? "ga_audit_diagnostics.csv" : $"ga_audit_diagnostics_{timestampStr}.csv";
            string detailedPath = Path.Combine(reportsDir, detailedFileName);

            string breakdownFileName = batchMode ? "ga_audit_failure_breakdown.csv" : $"ga_audit_failure_breakdown_{timestampStr}.csv";
            string breakdownPath = Path.Combine(reportsDir, breakdownFileName);

            string historyPath = Path.Combine(reportsDir, "ga_audit_run_history.csv");

            var allElements = input.Elements ?? new List<PrecastElement>();

            // Build detailed rows only when needed (Research diagnostics)
            var detailedRows = new List<ElementDiagnosticRow>();
            if (diagLevel >= DiagnosticsLevel.Research)
            {
                foreach (var element in allElements)
                {
                    if (string.IsNullOrWhiteSpace(element.ElementID)) continue;

                    var candidates = new List<(int Index, ElementLiftResult Result)>();
                    for (int i = 0; i < finalResult.Positions.Count; i++)
                    {
                        var pos = finalResult.Positions[i];
                    var elemResult = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.Ordinal));
                        if (elemResult != null)
                        {
                            candidates.Add((i, elemResult));
                        }
                    }

                    if (candidates.Count == 0) continue;

                    var owner = candidates.FirstOrDefault(c => c.Result.IsFinalAssignedToPoint);

                    ElementLiftResult bestResult;
                    int assignedIndex;
                    bool isLifted;

                    if (owner.Result != null)
                    {
                        bestResult = owner.Result;
                        assignedIndex = owner.Index;
                        isLifted = (bestResult.Status == LiftStatus.Liftable);
                    }
                    else
                    {
                        var bestFailed = candidates.FirstOrDefault(c => string.Equals(c.Result.CandidatePointId, c.Result.BestFailedPointId, StringComparison.OrdinalIgnoreCase));
                        if (bestFailed.Result == null)
                        {
                            bestFailed = candidates.OrderByDescending(c => c.Result.AssignmentScore).First();
                        }
                        bestResult = bestFailed.Result;
                        assignedIndex = bestFailed.Index;
                        isLifted = false;
                    }

                    string failureReason = "Liftable";
                    string failureCategory = "Liftable";
                    string suggestedAction = "";

                    if (!isLifted)
                    {
                        var classification = LiftClassificationPolicy.Classify(
                            bestResult.Status,
                            bestResult.ChartStatus,
                            element.ElementWT,
                            bestResult.BoomLength,
                            input.CraneSpec?.MaxBoomLength ?? 0.0,
                            finalResult.Positions.Count > 0);

                        failureReason = classification.FailureReason;
                        failureCategory = classification.FailureCategory;
                        suggestedAction = classification.SuggestedAction;
                    }

                    detailedRows.Add(new ElementDiagnosticRow
                    {
                        Timestamp = timeFormatted,
                        ProjectName = input.ProjectName ?? "N/A",
                        IfcFileName = input.IfcFileName ?? "N/A",
                        CraneNameOrId = input.CraneSpec?.Crane?.Name ?? (input.CraneSpec != null ? $"Spec ID: {input.CraneSpec.Id}" : "N/A"),
                        ElementId = element.ElementID,
                        ElementName = element.ElementName ?? element.ElementMark ?? "N/A",
                        ElementType = element.ElementType ?? "N/A",
                        StoreyOrElevation = element.ElementCOG.Z.ToString("0.###", Invariant),
                        Weight = element.ElementWT ?? 0.0,
                        CogX = element.ElementCOG.X,
                        CogY = element.ElementCOG.Y,
                        CogZ = element.ElementCOG.Z,
                        AssignedSetupIndex = assignedIndex >= 0 ? (assignedIndex + 1) : -1,
                        AssignedCraneX = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].X : 0.0,
                        AssignedCraneY = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].Y : 0.0,
                        AssignedCraneZ = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].Z : 0.0,
                        BestRadius = bestResult.Radius,
                        RequiredBoomLength = bestResult.BoomLength,
                        RequiredCapacity = bestResult.RequiredCapacity,
                        RawChartCapacity = bestResult.RawChartCapacity,
                        AvailableCapacity = bestResult.AvailableCapacity,
                        AdditionalWt = input.Settings?.AdditionalWt ?? 0.0,
                        DeratingRatio = input.Settings?.DeratingRatio ?? 0.0,
                        SafetyFactor = input.Settings?.SafetyFactor ?? 0.0,
                        WireLength = input.Settings?.WireLength ?? 0.0,
                        LiftStatus = bestResult.Status.ToString(),
                        IsLifted = isLifted,
                        FailureReason = failureReason,
                        FailureCategory = failureCategory,
                        SuggestedAction = suggestedAction,
                        ReachStatus = bestResult.ReachStatus.ToString(),
                        ChartStatus = bestResult.ChartStatus.ToString(),
                        IsExactMatch = bestResult.IsExactMatch,
                        IsSteppedRadius = bestResult.IsSteppedRadius,
                        IsBlankCell = bestResult.IsBlankCell,
                        IsZeroCell = bestResult.IsZeroCell,
                        SafetyMargin = bestResult.SafetyMargin,
                        LookupMode = bestResult.LookupMode,
                        RequiredRadius = bestResult.RequiredRadius,
                        SelectedChartRadius = bestResult.SelectedChartRadius,
                        SelectedChartBoomLength = bestResult.SelectedChartBoomLength,
                        SelectedVisualColumnIndex = bestResult.SelectedVisualColumnIndex,
                        SelectedVisualColumnHeader = bestResult.SelectedVisualColumnHeader,
                        SelectedNumberOfLines = bestResult.SelectedNumberOfLines,
                        SelectedOperatingConditionLabel = bestResult.SelectedOperatingConditionLabel,
                        IsConservativeDuplicateFallback = bestResult.IsConservativeDuplicateFallback,
                        DuplicateAlternativeCount = bestResult.DuplicateAlternativeCount,
                        DuplicateAlternativeSummary = bestResult.DuplicateAlternativeSummary,
                        CraneBaseX = bestResult.CraneBaseX,
                        CraneBaseY = bestResult.CraneBaseY,
                        CraneBaseZ = bestResult.CraneBaseZ,
                        SlewCenterX = bestResult.SlewCenterX,
                        SlewCenterY = bestResult.SlewCenterY,
                        SlewCenterZ = bestResult.SlewCenterZ,
                        BoomHingeX = bestResult.BoomHingeX,
                        BoomHingeY = bestResult.BoomHingeY,
                        BoomHingeZ = bestResult.BoomHingeZ,
                        LoadPointX = bestResult.LoadPointX,
                        LoadPointY = bestResult.LoadPointY,
                        LoadPointZ = bestResult.LoadPointZ,
                        RadiusOriginType = bestResult.RadiusOriginType,
                        BoomOriginType = bestResult.BoomOriginType,
                        CandidateBoomsChecked = bestResult.CandidateBoomsChecked,
                        RejectedCandidates = bestResult.RejectedCandidates,
                        CandidatePointId = bestResult.CandidatePointId,
                        CandidatePointIndex = bestResult.CandidatePointIndex,
                        IsFeasibleFromPoint = bestResult.IsFeasibleFromPoint,
                        IsFinalAssignedToPoint = bestResult.IsFinalAssignedToPoint,
                        AssignedPointId = bestResult.AssignedPointId,
                        AssignedPointIndex = bestResult.AssignedPointIndex,
                        AssignmentReason = bestResult.AssignmentReason,
                        AssignmentScore = bestResult.AssignmentScore,
                        DuplicateAssignmentResolved = bestResult.DuplicateAssignmentResolved,
                        FeasiblePointIds = bestResult.FeasiblePointIds,
                        FinalOwnerPointId = bestResult.FinalOwnerPointId,
                        IsFinalAssigned = bestResult.IsFinalAssigned,
                        BestFailedPointId = bestResult.BestFailedPointId,
                        BestFailureReason = bestResult.BestFailureReason,

                        HasBestFailedCandidate = bestResult.HasBestFailedCandidate,
                        BestFailedLiftStatus = bestResult.BestFailedLiftStatus.ToString(),
                        BestFailedReachStatus = bestResult.BestFailedReachStatus.ToString(),
                        BestFailedChartStatus = bestResult.BestFailedChartStatus.ToString(),
                        BestFailedSafetyMargin = bestResult.BestFailedSafetyMargin,
                        BestFailedRequiredRadius = bestResult.BestFailedRequiredRadius,
                        BestFailedRequiredBoomLength = bestResult.BestFailedRequiredBoomLength
                    });
                }
            }

            // Research-only: detailed diagnostics, assignment matrix, final assignments
            if (diagLevel >= DiagnosticsLevel.Research)
            {
                // 3. Write detailed diagnostics CSV file (UTF-8, properly escaped)
                bool detailedExists = File.Exists(detailedPath);
                using (var writer = new StreamWriter(detailedPath, batchMode, new UTF8Encoding(true)))
                {
                    var originalHeader = new[]
                    {
                        "OptimizationRunId", "StageRunId", "DiagnosticsSchemaVersion",
                        "Timestamp", "ProjectName", "IfcFileName", "CraneNameOrId", "ElementId", "ElementName", "ElementType",
                        "StoreyOrElevation", "Weight", "COG_X", "COG_Y", "COG_Z", "AssignedSetupIndex", "AssignedCraneX",
                        "AssignedCraneY", "AssignedCraneZ", "BestRadius", "RequiredBoomLength", "RequiredCapacity",
                        "RawChartCapacity", "AvailableCapacity", "AdditionalWt", "DeratingRatio", "SafetyFactor", "WireLength",
                        "LiftStatus", "IsLifted", "FailureReason", "FailureCategory", "SuggestedAction",
                        "ReachStatus", "ChartStatus", "IsExactMatch", "IsSteppedRadius", "IsBlankCell", "IsZeroCell", "SafetyMargin",
                        "LookupMode", "RequiredRadius", "SelectedChartRadius", "SelectedChartBoomLength",
                        "SelectedVisualColumnIndex", "SelectedVisualColumnHeader", "SelectedNumberOfLines", "SelectedOperatingConditionLabel", "IsConservativeDuplicateFallback", "DuplicateAlternativeCount", "DuplicateAlternativeSummary",
                        "CraneBasePoint_Local", "CraneBasePoint_Survey", "SlewCenterPoint_Local", "SlewCenterPoint_Survey",
                        "BoomHingePoint_Local", "BoomHingePoint_Survey", "LoadPoint_Local", "LoadPoint_Survey",
                        "RadiusOriginType", "BoomOriginType",
                        "CandidateBoomsChecked", "RejectedCandidates",
                        "CandidatePointId", "CandidatePointIndex", "IsFeasibleFromPoint", "IsFinalAssignedToPoint",
                        "AssignedPointId", "AssignedPointIndex", "AssignmentReason", "AssignmentScore",
                        "DuplicateAssignmentResolved", "FeasiblePointIds", "FinalOwnerPointId", "IsFinalAssigned",
                        "BestFailedPointId", "BestFailureReason",
                        "HasBestFailedCandidate", "BestFailedLiftStatus", "BestFailedReachStatus", "BestFailedChartStatus",
                        "BestFailedSafetyMargin", "BestFailedRequiredRadius", "BestFailedRequiredBoomLength",
                        "Candidate_ModelX", "Candidate_ModelY", "Candidate_ModelZ",
                        "Candidate_SiteE", "Candidate_SiteN", "Candidate_SiteH",
                        "COG_SiteE", "COG_SiteN", "COG_SiteH",
                        "CraneBase_ModelX", "CraneBase_ModelY", "CraneBase_ModelZ",
                        "CraneBase_SiteE", "CraneBase_SiteN", "CraneBase_SiteH",
                        "SlewCenter_ModelX", "SlewCenter_ModelY", "SlewCenter_ModelZ",
                        "SlewCenter_SiteE", "SlewCenter_SiteN", "SlewCenter_SiteH",
                        "BoomHinge_ModelX", "BoomHinge_ModelY", "BoomHinge_ModelZ",
                        "BoomHinge_SiteE", "BoomHinge_SiteN", "BoomHinge_SiteH",
                        "LoadPoint_ModelX", "LoadPoint_ModelY", "LoadPoint_ModelZ",
                        "LoadPoint_SiteE", "LoadPoint_SiteN", "LoadPoint_SiteH",
                        "COG_ModelX", "COG_ModelY", "COG_ModelZ",
                        "COG_SurveyEasting", "COG_SurveyNorthing", "COG_SurveyElevation",
                        "Candidate_SurveyEasting", "Candidate_SurveyNorthing", "Candidate_SurveyElevation",
                        "CraneBase_SurveyEasting", "CraneBase_SurveyNorthing", "CraneBase_SurveyElevation",
                        "SlewCenter_SurveyEasting", "SlewCenter_SurveyNorthing", "SlewCenter_SurveyElevation",
                        "BoomHinge_SurveyEasting", "BoomHinge_SurveyNorthing", "BoomHinge_SurveyElevation",
                        "LoadPoint_SurveyEasting", "LoadPoint_SurveyNorthing", "LoadPoint_SurveyElevation",
                        "SiteReferenceEnabled", "SiteOriginEasting", "SiteOriginNorthing", "SiteOriginElevation", "RotationToNorthDegrees"
                    };
                    if (!batchMode || !detailedExists)
                    {
                        writer.WriteLine(string.Join(",", FormatHeaderWithPrefix(originalHeader)));
                    }

                    foreach (var row in detailedRows)
                    {
                        var isEnabled = _transformService != null && _transformService.IsEnabled;

                        var candPt = new DomainPoint3D(row.AssignedCraneX, row.AssignedCraneY, row.AssignedCraneZ);
                        var candSite = isEnabled && row.AssignedSetupIndex >= 0 ? _transformService!.LocalToSurvey(candPt) : (DomainPoint3D?)null;

                        var cogPt = new DomainPoint3D(row.CogX, row.CogY, row.CogZ);
                        var cogSite = isEnabled ? _transformService!.LocalToSurvey(cogPt) : (DomainPoint3D?)null;

                        var basePt = new DomainPoint3D(row.CraneBaseX, row.CraneBaseY, row.CraneBaseZ);
                        var baseSite = isEnabled ? _transformService!.LocalToSurvey(basePt) : (DomainPoint3D?)null;

                        var slewPt = new DomainPoint3D(row.SlewCenterX, row.SlewCenterY, row.SlewCenterZ);
                        var slewSite = isEnabled ? _transformService!.LocalToSurvey(slewPt) : (DomainPoint3D?)null;

                        var hingePt = new DomainPoint3D(row.BoomHingeX, row.BoomHingeY, row.BoomHingeZ);
                        var hingeSite = isEnabled ? _transformService!.LocalToSurvey(hingePt) : (DomainPoint3D?)null;

                        var loadPt = new DomainPoint3D(row.LoadPointX, row.LoadPointY, row.LoadPointZ);
                        var loadSite = isEnabled ? _transformService!.LocalToSurvey(loadPt) : (DomainPoint3D?)null;

                        var originalRow = new[]
                        {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            "1.0",
                            row.Timestamp,
                            Escape(row.ProjectName),
                            Escape(row.IfcFileName),
                            Escape(row.CraneNameOrId),
                            Escape(row.ElementId),
                            Escape(row.ElementName),
                            Escape(row.ElementType),
                            row.StoreyOrElevation,
                            row.Weight.ToString("0.###", Invariant),
                            row.CogX.ToString("0.###", Invariant),
                            row.CogY.ToString("0.###", Invariant),
                            row.CogZ.ToString("0.###", Invariant),
                            row.AssignedSetupIndex.ToString(),
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneX.ToString("0.###", Invariant) : "N/A",
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneY.ToString("0.###", Invariant) : "N/A",
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneZ.ToString("0.###", Invariant) : "N/A",
                            row.BestRadius.ToString("0.###", Invariant),
                            row.RequiredBoomLength.ToString("0.###", Invariant),
                            row.RequiredCapacity.ToString("0.###", Invariant),
                            row.RawChartCapacity.ToString("0.###", Invariant),
                            row.AvailableCapacity.ToString("0.###", Invariant),
                            row.AdditionalWt.ToString("0.###", Invariant),
                            row.DeratingRatio.ToString("0.###", Invariant),
                            row.SafetyFactor.ToString("0.###", Invariant),
                            row.WireLength.ToString("0.###", Invariant),
                            row.LiftStatus,
                            row.IsLifted ? "true" : "false",
                            Escape(row.FailureReason),
                            Escape(row.FailureCategory),
                            Escape(row.SuggestedAction),
                            row.ReachStatus,
                            row.ChartStatus,
                            row.IsExactMatch ? "true" : "false",
                            row.IsSteppedRadius ? "true" : "false",
                            row.IsBlankCell ? "true" : "false",
                            row.IsZeroCell ? "true" : "false",
                            row.SafetyMargin.ToString("0.###", Invariant),
                            Escape(row.LookupMode),
                            row.RequiredRadius.ToString("0.###", Invariant),
                            row.SelectedChartRadius.ToString("0.###", Invariant),
                            row.SelectedChartBoomLength.ToString("0.###", Invariant),
                            row.SelectedVisualColumnIndex?.ToString(Invariant) ?? string.Empty,
                            Escape(row.SelectedVisualColumnHeader),
                            row.SelectedNumberOfLines?.ToString(Invariant) ?? string.Empty,
                            Escape(row.SelectedOperatingConditionLabel),
                            row.IsConservativeDuplicateFallback ? "true" : "false",
                            row.DuplicateAlternativeCount.ToString(Invariant),
                            Escape(row.DuplicateAlternativeSummary),
                            Escape($"({row.CraneBaseX.ToString("0.###", Invariant)}; {row.CraneBaseY.ToString("0.###", Invariant)}; {row.CraneBaseZ.ToString("0.###", Invariant)})"),
                            Escape(FormatSurveyIfEnabled(row.CraneBaseX, row.CraneBaseY, row.CraneBaseZ)),
                            Escape($"({row.SlewCenterX.ToString("0.###", Invariant)}; {row.SlewCenterY.ToString("0.###", Invariant)}; {row.SlewCenterZ.ToString("0.###", Invariant)})"),
                            Escape(FormatSurveyIfEnabled(row.SlewCenterX, row.SlewCenterY, row.SlewCenterZ)),
                            Escape($"({row.BoomHingeX.ToString("0.###", Invariant)}; {row.BoomHingeY.ToString("0.###", Invariant)}; {row.BoomHingeZ.ToString("0.###", Invariant)})"),
                            Escape(FormatSurveyIfEnabled(row.BoomHingeX, row.BoomHingeY, row.BoomHingeZ)),
                            Escape($"({row.LoadPointX.ToString("0.###", Invariant)}; {row.LoadPointY.ToString("0.###", Invariant)}; {row.LoadPointZ.ToString("0.###", Invariant)})"),
                            Escape(FormatSurveyIfEnabled(row.LoadPointX, row.LoadPointY, row.LoadPointZ)),
                            Escape(row.RadiusOriginType),
                            Escape(row.BoomOriginType),
                            Escape(row.CandidateBoomsChecked),
                            Escape(row.RejectedCandidates),
                            Escape(row.CandidatePointId),
                            row.CandidatePointIndex.ToString(),
                            row.IsFeasibleFromPoint ? "true" : "false",
                            row.IsFinalAssignedToPoint ? "true" : "false",
                            Escape(row.AssignedPointId),
                            row.AssignedPointIndex.ToString(),
                            Escape(row.AssignmentReason),
                            row.AssignmentScore.ToString("0.###", Invariant),
                            row.DuplicateAssignmentResolved ? "true" : "false",
                            Escape(row.FeasiblePointIds),
                            Escape(row.FinalOwnerPointId),
                            row.IsFinalAssigned ? "true" : "false",
                            Escape(row.BestFailedPointId),
                            Escape(row.BestFailureReason),
                            row.HasBestFailedCandidate ? "true" : "false",
                            Escape(row.BestFailedLiftStatus),
                            Escape(row.BestFailedReachStatus),
                            Escape(row.BestFailedChartStatus),
                            row.BestFailedSafetyMargin.ToString("0.###", Invariant),
                            row.BestFailedRequiredRadius.ToString("0.###", Invariant),
                            row.BestFailedRequiredBoomLength.ToString("0.###", Invariant),
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneX.ToString("0.###", Invariant) : "N/A",
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneY.ToString("0.###", Invariant) : "N/A",
                            row.AssignedSetupIndex >= 0 ? row.AssignedCraneZ.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.X.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.Y.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.Z.ToString("0.###", Invariant) : "N/A",
                            cogSite != null ? cogSite.X.ToString("0.###", Invariant) : "N/A",
                            cogSite != null ? cogSite.Y.ToString("0.###", Invariant) : "N/A",
                            cogSite != null ? cogSite.Z.ToString("0.###", Invariant) : "N/A",
                            row.CraneBaseX.ToString("0.###", Invariant),
                            row.CraneBaseY.ToString("0.###", Invariant),
                            row.CraneBaseZ.ToString("0.###", Invariant),
                            baseSite != null ? baseSite.X.ToString("0.###", Invariant) : "N/A",
                            baseSite != null ? baseSite.Y.ToString("0.###", Invariant) : "N/A",
                            baseSite != null ? baseSite.Z.ToString("0.###", Invariant) : "N/A",
                            row.SlewCenterX.ToString("0.###", Invariant),
                            row.SlewCenterY.ToString("0.###", Invariant),
                            row.SlewCenterZ.ToString("0.###", Invariant),
                            slewSite != null ? slewSite.X.ToString("0.###", Invariant) : "N/A",
                            slewSite != null ? slewSite.Y.ToString("0.###", Invariant) : "N/A",
                            slewSite != null ? slewSite.Z.ToString("0.###", Invariant) : "N/A",
                            row.BoomHingeX.ToString("0.###", Invariant),
                            row.BoomHingeY.ToString("0.###", Invariant),
                            row.BoomHingeZ.ToString("0.###", Invariant),
                            hingeSite != null ? hingeSite.X.ToString("0.###", Invariant) : "N/A",
                            hingeSite != null ? hingeSite.Y.ToString("0.###", Invariant) : "N/A",
                            hingeSite != null ? hingeSite.Z.ToString("0.###", Invariant) : "N/A",
                            row.LoadPointX.ToString("0.###", Invariant),
                            row.LoadPointY.ToString("0.###", Invariant),
                            row.LoadPointZ.ToString("0.###", Invariant),
                            loadSite != null ? loadSite.X.ToString("0.###", Invariant) : "N/A",
                            loadSite != null ? loadSite.Y.ToString("0.###", Invariant) : "N/A",
                            loadSite != null ? loadSite.Z.ToString("0.###", Invariant) : "N/A",
                            row.CogX.ToString("0.###", Invariant),
                            row.CogY.ToString("0.###", Invariant),
                            row.CogZ.ToString("0.###", Invariant),
                            cogSite != null ? cogSite.X.ToString("0.###", Invariant) : "N/A",
                            cogSite != null ? cogSite.Y.ToString("0.###", Invariant) : "N/A",
                            cogSite != null ? cogSite.Z.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.X.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.Y.ToString("0.###", Invariant) : "N/A",
                            candSite != null ? candSite.Z.ToString("0.###", Invariant) : "N/A",
                            baseSite != null ? baseSite.X.ToString("0.###", Invariant) : "N/A",
                            baseSite != null ? baseSite.Y.ToString("0.###", Invariant) : "N/A",
                            baseSite != null ? baseSite.Z.ToString("0.###", Invariant) : "N/A",
                            slewSite != null ? slewSite.X.ToString("0.###", Invariant) : "N/A",
                            slewSite != null ? slewSite.Y.ToString("0.###", Invariant) : "N/A",
                            slewSite != null ? slewSite.Z.ToString("0.###", Invariant) : "N/A",
                            hingeSite != null ? hingeSite.X.ToString("0.###", Invariant) : "N/A",
                            hingeSite != null ? hingeSite.Y.ToString("0.###", Invariant) : "N/A",
                            hingeSite != null ? hingeSite.Z.ToString("0.###", Invariant) : "N/A",
                            loadSite != null ? loadSite.X.ToString("0.###", Invariant) : "N/A",
                            loadSite != null ? loadSite.Y.ToString("0.###", Invariant) : "N/A",
                            loadSite != null ? loadSite.Z.ToString("0.###", Invariant) : "N/A",
                            isEnabled.ToString().ToLower(),
                            isEnabled ? _transformService!.CurrentReference.SurveyEasting.ToString("0.###", Invariant) : "N/A",
                            isEnabled ? _transformService!.CurrentReference.SurveyNorthing.ToString("0.###", Invariant) : "N/A",
                            isEnabled ? _transformService!.CurrentReference.SurveyElevation.ToString("0.###", Invariant) : "N/A",
                            isEnabled ? _transformService!.CurrentReference.RotationToNorthDegrees.ToString("0.###", Invariant) : "N/A"
                        };
                        writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
                    }
                }

                // 3A. Write ga_assignment_matrix_*.csv (one row per ElementGlobalId per CandidatePointId)
                string matrixFileName = batchMode ? "ga_audit_assignment_matrix.csv" : $"ga_audit_assignment_matrix_{timestampStr}.csv";
                string matrixPath = Path.Combine(reportsDir, matrixFileName);
                bool matrixExists = File.Exists(matrixPath);

                using (var writer = new StreamWriter(matrixPath, batchMode, new UTF8Encoding(true)))
                {
                    var originalHeader = new[]
                    {
                        "OptimizationRunId", "StageRunId", "DiagnosticsSchemaVersion",
                        "Timestamp", "ProjectName", "IfcFileName", "ElementGlobalId", "CandidatePointId", "CandidatePointIndex",
                        "CraneBasePoint", "SlewCenterPoint", "BoomHingePoint", "LoadPoint", "RequiredRadius", "RequiredBoomLength",
                        "SelectedChartRadius", "SelectedChartBoomLength", "SelectedVisualColumnIndex", "SelectedVisualColumnHeader", "SelectedNumberOfLines",
                        "SelectedOperatingConditionLabel", "IsConservativeDuplicateFallback", "DuplicateAlternativeCount", "DuplicateAlternativeSummary", "AvailableCapacity", "ReachStatus", "ChartStatus",
                        "LiftStatus", "FailureReason", "SafetyMargin", "CandidateBoomsChecked", "RejectedCandidates", "AssignmentScore"
                    };
                    if (!batchMode || !matrixExists)
                    {
                        writer.WriteLine(string.Join(",", FormatHeaderWithPrefix(originalHeader)));
                    }

                    foreach (var element in allElements)
                    {
                        if (string.IsNullOrWhiteSpace(element.ElementID)) continue;

                        for (int i = 0; i < finalResult.Positions.Count; i++)
                        {
                            var pos = finalResult.Positions[i];
                            var er = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.Ordinal));
                            if (er == null) continue;

                            string failReason = "Liftable";
                            if (er.Status != LiftStatus.Liftable)
                            {
                                var classification = LiftClassificationPolicy.Classify(
                                    er.Status, er.ChartStatus, element.ElementWT, er.BoomLength, input.CraneSpec?.MaxBoomLength ?? 0.0, true);
                                failReason = classification.FailureReason;
                            }

                            var originalRow = new[]
                            {
                                input.Settings.OptimizationRunId ?? string.Empty,
                                input.Settings.StageRunId ?? string.Empty,
                                "1.0",
                                timeFormatted,
                                Escape(input.ProjectName ?? "N/A"),
                                Escape(input.IfcFileName ?? "N/A"),
                                Escape(element.ElementID),
                                Escape(er.CandidatePointId),
                                er.CandidatePointIndex.ToString(),
                                Escape($"({er.CraneBaseX.ToString("0.###", Invariant)}; {er.CraneBaseY.ToString("0.###", Invariant)}; {er.CraneBaseZ.ToString("0.###", Invariant)})"),
                                Escape($"({er.SlewCenterX.ToString("0.###", Invariant)}; {er.SlewCenterY.ToString("0.###", Invariant)}; {er.SlewCenterZ.ToString("0.###", Invariant)})"),
                                Escape($"({er.BoomHingeX.ToString("0.###", Invariant)}; {er.BoomHingeY.ToString("0.###", Invariant)}; {er.BoomHingeZ.ToString("0.###", Invariant)})"),
                                Escape($"({er.LoadPointX.ToString("0.###", Invariant)}; {er.LoadPointY.ToString("0.###", Invariant)}; {er.LoadPointZ.ToString("0.###", Invariant)})"),
                                er.Radius.ToString("0.###", Invariant),
                                er.BoomLength.ToString("0.###", Invariant),
                                er.SelectedChartRadius.ToString("0.###", Invariant),
                                er.SelectedChartBoomLength.ToString("0.###", Invariant),
                                er.SelectedVisualColumnIndex?.ToString(Invariant) ?? string.Empty,
                                Escape(er.SelectedVisualColumnHeader),
                                er.SelectedNumberOfLines?.ToString(Invariant) ?? string.Empty,
                                Escape(er.SelectedOperatingConditionLabel),
                                er.IsConservativeDuplicateFallback ? "true" : "false",
                                er.DuplicateAlternativeCount.ToString(Invariant),
                                Escape(er.DuplicateAlternativeSummary),
                                er.AvailableCapacity.ToString("0.###", Invariant),
                                er.ReachStatus.ToString(),
                                er.ChartStatus.ToString(),
                                er.Status.ToString(),
                                Escape(failReason),
                                er.SafetyMargin.ToString("0.###", Invariant),
                                Escape(er.CandidateBoomsChecked),
                                Escape(er.RejectedCandidates),
                                er.AssignmentScore.ToString("0.###", Invariant)
                            };
                            writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
                        }
                    }
                }

                // 3B. Write ga_final_assignments_*.csv (one row per ElementGlobalId)
                string finalAssignmentsFileName = batchMode ? "ga_audit_final_assignments.csv" : $"ga_audit_final_assignments_{timestampStr}.csv";
                string finalAssignmentsPath = Path.Combine(reportsDir, finalAssignmentsFileName);
                bool finalExists = File.Exists(finalAssignmentsPath);

                using (var writer = new StreamWriter(finalAssignmentsPath, batchMode, new UTF8Encoding(true)))
                {
                    var originalHeader = new[]
                    {
                        "OptimizationRunId", "StageRunId", "DiagnosticsSchemaVersion",
                        "Timestamp", "ProjectName", "IfcFileName", "ElementGlobalId", "AssignedPointId", "AssignedPointIndex",
                        "AssignedCraneId", "AssignmentStatus", "AssignmentReason", "AssignmentScore", "IsFinalAssigned",
                        "BestFailedPointId", "BestFailureReason"
                    };
                    if (!batchMode || !finalExists)
                    {
                        writer.WriteLine(string.Join(",", FormatHeaderWithPrefix(originalHeader)));
                    }

                    foreach (var row in detailedRows)
                    {
                        var originalRow = new[]
                        {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            "1.0",
                            row.Timestamp,
                            Escape(row.ProjectName),
                            Escape(row.IfcFileName),
                            Escape(row.ElementId),
                            Escape(row.AssignedPointId),
                            row.AssignedPointIndex.ToString(),
                            Escape(row.CraneNameOrId),
                            row.IsLifted ? "Liftable" : "Unassigned",
                            Escape(row.AssignmentReason),
                            row.AssignmentScore.ToString("0.###", Invariant),
                            row.IsFinalAssigned ? "true" : "false",
                            Escape(row.BestFailedPointId),
                            Escape(row.BestFailureReason)
                        };
                        writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
                    }
                }
            }



            int totalFailed = allElements.Count - finalResult.LiftedElementCount;
            int totalSelected = allElements.Count;

            // 4 / 5. Write failure breakdown CSV file
            bool breakdownExists = File.Exists(breakdownPath);
            using (var writer = new StreamWriter(breakdownPath, batchMode, new UTF8Encoding(true)))
            {
                var originalHeader = new[] { "OptimizationRunId", "StageRunId", "Timestamp", "FailureCategory", "Count", "PercentageOfFailedElements", "PercentageOfSelectedElements", "DiagnosticsSchemaVersion" };
                if (!batchMode || !breakdownExists)
                {
                    writer.WriteLine(string.Join(",", FormatHeaderWithPrefix(originalHeader)));
                }

                if (diagLevel >= DiagnosticsLevel.Research && detailedRows.Count > 0)
                {
                    var categories = new[]
                    {
                        "OutOfRadius", "Overweight", "HeightFail", "BoomLengthExceeded", "NoValidSetupPosition",
                        "ForbiddenZoneBlocked", "NotAssigned", "MissingWeightOrInvalidData", "SpecialCase", "Unknown",
                        "RadiusBeyondLoadChart", "BoomBeyondLoadChart", "NoDiscreteLoadChartEntry", "NoListedCapacityForBoomRadius",
                        "OutsideChartEnvelope", "InvalidBlankCell", "InvalidZeroCell", "MissingLoadChartData"
                    };
                    var failureCounts = categories.ToDictionary(c => c, c => 0);

                    foreach (var row in detailedRows)
                    {
                        if (!row.IsLifted && failureCounts.ContainsKey(row.FailureCategory))
                        {
                            failureCounts[row.FailureCategory]++;
                        }
                    }

                    foreach (var cat in categories)
                    {
                        int count = failureCounts[cat];
                        double pctFailed = totalFailed > 0 ? (double)count / totalFailed * 100.0 : 0.0;
                        double pctSelected = totalSelected > 0 ? (double)count / totalSelected * 100.0 : 0.0;
                        var originalRow = new[] {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            timeFormatted,
                            cat,
                            count.ToString(),
                            pctFailed.ToString("F2", Invariant) + "%",
                            pctSelected.ToString("F2", Invariant) + "%",
                            "1.0"
                        };
                        writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
                    }
                }
                else
                {
                    // Summary mode: compact breakdown from result statistics
                    var stats = finalResult.Statistics;

                    void WriteStat(string category, int count)
                    {
                        double pctFailed = totalFailed > 0 ? (double)count / totalFailed * 100.0 : 0.0;
                        double pctSelected = totalSelected > 0 ? (double)count / totalSelected * 100.0 : 0.0;
                        var originalRow = new[] {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            timeFormatted,
                            category,
                            count.ToString(),
                            pctFailed.ToString("F2", Invariant) + "%",
                            pctSelected.ToString("F2", Invariant) + "%",
                            "1.0"
                        };
                        writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
                    }

                    WriteStat("Liftable", finalResult.LiftedElementCount);
                    WriteStat("OutOfRadius", stats.OutOfRadius);
                    WriteStat("Overweight", stats.Overweight);
                    WriteStat("HeightFail", stats.HeightFail);
                    WriteStat("SpecialCase", stats.SpecialCase);
                    WriteStat("NotCharted", stats.NotCharted);
                }
            }

            // 6. Write/Append to Run History CSV
            double coverage = finalResult.CoverageRatio;
            string healthLabel = "Critical";
            if (coverage >= 0.90) healthLabel = "Excellent";
            else if (coverage >= 0.70) healthLabel = "Good";
            else if (coverage >= 0.40) healthLabel = "Warning";

            bool historyExists = File.Exists(historyPath);
            int stageIndex = input.Settings.EnableMultiStageDiscovery ? input.Settings.MaxCranePositions : 1;

            using (var writer = new StreamWriter(historyPath, true, new UTF8Encoding(true)))
            {
                var originalHeader = new[]
                {
                    "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                    "Timestamp", "ProjectName", "ProjectFolderPath", "IfcFileName", "CraneNameOrId", "TotalTargetElements",
                    "SelectedElements", "LiftedElements", "FailedElements", "CoverageRatio", "HealthLabel",
                    "FinalCraneSetupCount", "GreedyIterations", "PopulationSize", "ConfiguredGenerations",
                    "ActualGenerationLoopCount", "TotalGATimeMs", "ElementAssignmentTimeMs", "CraneCapacityLookupCount",
                    "CraneCapacityCacheHits", "CraneCapacityCacheMisses", "CraneCapacityCacheHitRatio",
                    "MemoryBeforeMB", "MemoryAfterMB", "MemoryDeltaMB", "GCGen0Delta", "GCGen1Delta", "GCGen2Delta",
                    "ProfilingLogPath", "DetailedDiagnosticsCsvPath", "FailureBreakdownCsvPath",
                    "CoordinateReferenceEnabled", "CoordinateSystemName", "EPSGCode", "RotationToNorthDegrees",
                    "LocalReferencePoint", "SurveyReferencePoint",
                    "EnableMultiStageDiscovery", "MultiStageCoverageTarget", "AdaptiveRelocationScaleFactor", "UseHungarianAssignment"
                };
                if (!historyExists)
                {
                    writer.WriteLine(string.Join(",", FormatHeaderWithPrefix(originalHeader)));
                }

                string gaTime = GAProfiler.Enabled ? GAProfiler.GetElapsedTimeMs("GAEngine.Run").ToString("F1", Invariant) : "N/A";
                string assignmentTime = GAProfiler.Enabled ? GAProfiler.GetElapsedTimeMs("ElementAssignmentService.EvaluateAssignment").ToString("F1", Invariant) : "N/A";
                string capacityLookups = "N/A";
                string capacityHits = "N/A";
                string capacityMisses = "N/A";
                string capacityRatio = "N/A";
                string logPath = "N/A";

                if (GAProfiler.Enabled)
                {
                    logPath = GAProfiler.ProfilingLogPath ?? "N/A";
                }

                long lookupHits = GAProfiler.CapacityCacheHits;
                long lookupMisses = GAProfiler.CapacityCacheMisses;
                long totalLookups = lookupHits + lookupMisses;
                double hitRatio = totalLookups > 0 ? (double)lookupHits / totalLookups * 100.0 : 0.0;

                capacityHits = lookupHits.ToString();
                capacityMisses = lookupMisses.ToString();
                capacityRatio = totalLookups > 0 ? hitRatio.ToString("F2", Invariant) + "%" : "0.00%";
                capacityLookups = totalLookups.ToString();

                string memBefore = (GAProfiler.MemoryBeforeBytes / 1024.0 / 1024.0).ToString("F3", Invariant);
                string memAfter = (GAProfiler.MemoryAfterBytes / 1024.0 / 1024.0).ToString("F3", Invariant);
                string memDelta = ((GAProfiler.MemoryAfterBytes - GAProfiler.MemoryBeforeBytes) / 1024.0 / 1024.0).ToString("F3", Invariant);
                string gc0 = (GAProfiler.GcCount0After - GAProfiler.GcCount0Before).ToString();
                string gc1 = (GAProfiler.GcCount1After - GAProfiler.GcCount1Before).ToString();
                string gc2 = (GAProfiler.GcCount2After - GAProfiler.GcCount2Before).ToString();

                // Let's write the history row
                var originalRow = new[]
                {
                    input.Settings.OptimizationRunId ?? string.Empty,
                    input.Settings.StageRunId ?? string.Empty,
                    stageIndex.ToString(),
                    "1.0",
                    timeFormatted,
                    Escape(input.ProjectName ?? "N/A"),
                    Escape(input.ProjectFolderPath ?? "N/A"),
                    Escape(input.IfcFileName ?? "N/A"),
                    Escape(input.CraneSpec?.Crane?.Name ?? (input.CraneSpec != null ? $"Spec ID: {input.CraneSpec.Id}" : "N/A")),
                    input.TotalElementsCount.ToString(),
                    allElements.Count.ToString(),
                    finalResult.LiftedElementCount.ToString(),
                    totalFailed.ToString(),
                    (coverage * 100.0).ToString("F2", Invariant) + "%",
                    healthLabel,
                    finalResult.Positions.Count.ToString(),
                    GAProfiler.GreedySetCoverIterationCount.ToString(),
                    (input.Settings?.PopulationSize ?? 0).ToString(),
                    (input.Settings?.MaxGenerations ?? 0).ToString(),
                    GAProfiler.ActualGenerationLoopCount.ToString(),
                    gaTime,
                    assignmentTime,
                    capacityLookups,
                    capacityHits,
                    capacityMisses,
                    capacityRatio,
                    memBefore,
                    memAfter,
                    memDelta,
                    gc0,
                    gc1,
                    gc2,
                    Escape(logPath),
                    Escape(diagLevel >= DiagnosticsLevel.Research && File.Exists(detailedPath) ? detailedPath : "N/A"),
                    Escape(breakdownPath),
                    (_transformService?.IsEnabled ?? false).ToString(),
                    Escape(_transformService?.CurrentReference?.CoordinateSystemName ?? "N/A"),
                    Escape(_transformService?.CurrentReference?.EPSGCode ?? "N/A"),
                    (_transformService?.CurrentReference?.RotationToNorthDegrees ?? 0.0).ToString("0.###", Invariant),
                    Escape(_transformService != null && _transformService.IsEnabled ? $"({_transformService.CurrentReference.LocalReferenceX.ToString("0.###", Invariant)}; {_transformService.CurrentReference.LocalReferenceY.ToString("0.###", Invariant)}; {_transformService.CurrentReference.LocalReferenceZ.ToString("0.###", Invariant)})" : "N/A"),
                    Escape(_transformService != null && _transformService.IsEnabled ? $"({_transformService.CurrentReference.SurveyEasting.ToString("0.###", Invariant)}; {_transformService.CurrentReference.SurveyNorthing.ToString("0.###", Invariant)}; {_transformService.CurrentReference.SurveyElevation.ToString("0.###", Invariant)})" : "N/A"),
                    (input.Settings?.EnableMultiStageDiscovery ?? true).ToString().ToLower(),
                    (input.Settings?.MultiStageCoverageTarget ?? 0.95).ToString("0.###", Invariant),
                    (input.Settings?.AdaptiveRelocationScaleFactor ?? 0.40).ToString("0.###", Invariant),
                    (input.Settings?.UseHungarianAssignment ?? false).ToString().ToLower()
                };
                writer.WriteLine(string.Join(",", FormatRowWithPrefix(input, true, originalHeader, originalRow, "1.0")));
            }

            // ══════════════════════════════════════════════════════
            // 7 NEW CSV DIAGNOSTIC FILES WRITING (Task 4)
            // ══════════════════════════════════════════════════════
#if LEGACY_BENCHMARK_DIAGNOSTICS

            // 1. ga_run_summary.csv
            string runSummaryPath = Path.Combine(reportsDir, "ga_run_summary.csv");
            bool runSummaryExists = File.Exists(runSummaryPath);
            using (var writer = new StreamWriter(runSummaryPath, true, new UTF8Encoding(true)))
            {
                if (!runSummaryExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,Timestamp,ProjectNameOrId,CraneModel,TotalElementCount,GaReadyElementCount,LiftedElementCount,CoverageRatio,CoveragePercent,FitnessScore,PositionCount,MaxCranePositions,RuntimeMs,TerminationReason,DiagnosticsSchemaVersion");
                }
                
                string runTermReason = "MaxGenerationsReached";
                if (GAProfiler.StoppedByConvergence) runTermReason = "Convergence";
                else if (GAProfiler.ActualGenerationLoopCount < input.Settings.EarlyStoppingGenerations) runTermReason = "EarlyStopping";

                writer.WriteLine(string.Join(",", new[]
                {
                    input.Settings.OptimizationRunId ?? string.Empty,
                    input.Settings.StageRunId ?? string.Empty,
                    timeFormatted,
                    Escape(input.ProjectName ?? "N/A"),
                    Escape(input.CraneSpec?.Crane?.Name ?? "N/A"),
                    input.TotalElementsCount.ToString(),
                    (input.Elements?.Count ?? 0).ToString(),
                    finalResult.LiftedElementCount.ToString(),
                    finalResult.CoverageRatio.ToString("F6", Invariant),
                    (finalResult.CoverageRatio * 100.0).ToString("F4", Invariant),
                    finalResult.FitnessScore.ToString("F4", Invariant),
                    finalResult.Positions.Count.ToString(),
                    input.Settings.MaxCranePositions.ToString(),
                    GAProfiler.GetElapsedTimeMs("GAEngine.Run").ToString("F2", Invariant),
                    runTermReason,
                    "1.0"
                }));
            }

            // 2. ga_settings_snapshot.csv
            string settingsSnapshotPath = Path.Combine(reportsDir, "ga_settings_snapshot.csv");
            bool settingsSnapshotExists = File.Exists(settingsSnapshotPath);
            using (var writer = new StreamWriter(settingsSnapshotPath, true, new UTF8Encoding(true)))
            {
                if (!settingsSnapshotExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,PopulationSize,MaxGenerations,MutationRate,MutationSigma,CrossoverRate,WireLength,AdditionalWt,DeratingRatio,SafetyFactor,RiggingLength,MaxCranePositions,RandomSeed,CoverageWeight,CoverageDominanceMultiplier,RelocationWeight,RadiusPenaltyWeight,RadiusQualityPenaltyWeight,OverweightPenaltyWeight,HeightPenaltyWeight,MaxRadiusForNormalisation,GridStep,EarlyStoppingGenerations,ConvergenceThresholdPercent,RadiusStdDevPenaltyWeight,BalancePenaltyWeight,MinimumRelocationDistance,DuplicatePositionDistance,SimilarPositionPenaltyWeight,RelocationDistancePenaltyWeight,MutationExplorationMultiplier,EnableAdaptiveSigma,SigmaCoolingExponent,MinimumMutationSigma,PositionSamplingAttempts,CraneFootprintRadius,TargetElementTypes,ProfilingLogDirectory,EnableProfiling,SlewOffsetX,SlewOffsetY,SlewOffsetZ,BoomHingeOffsetX,BoomHingeOffsetY,BoomHingeOffsetZ,EnableConcurrentOptimization,HotSpotSeedingRatio,EnableReachabilitySeeding,MultiStageCoverageTarget,EnableMultiStageDiscovery,AdaptiveRelocationScaleFactor,UseHungarianAssignment,ProjectName,IfcFileName,ProjectFolderPath,DiagnosticsReportsDirectory,TotalElementsCount,CraneModelName,RunId");
                }

                writer.WriteLine(string.Join(",", new[]
                {
                    input.Settings.OptimizationRunId ?? string.Empty,
                    input.Settings.StageRunId ?? string.Empty,
                    "1.0",
                    timeFormatted,
                    input.Settings.PopulationSize.ToString(),
                    input.Settings.MaxGenerations.ToString(),
                    input.Settings.MutationRate.ToString("F4", Invariant),
                    input.Settings.MutationSigma.ToString("F4", Invariant),
                    input.Settings.CrossoverRate.ToString("F4", Invariant),
                    input.Settings.WireLength.ToString("F4", Invariant),
                    input.Settings.AdditionalWt.ToString("F4", Invariant),
                    input.Settings.DeratingRatio.ToString("F4", Invariant),
                    input.Settings.SafetyFactor.ToString("F4", Invariant),
                    input.Settings.RiggingLength.ToString("F4", Invariant),
                    input.Settings.MaxCranePositions.ToString(),
                    input.Settings.RandomSeed?.ToString() ?? "null",
                    input.Settings.CoverageWeight.ToString("F4", Invariant),
                    input.Settings.CoverageDominanceMultiplier.ToString("F4", Invariant),
                    input.Settings.RelocationWeight.ToString("F4", Invariant),
                    input.Settings.RadiusPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.RadiusQualityPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.OverweightPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.HeightPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.MaxRadiusForNormalisation.ToString("F4", Invariant),
                    input.Settings.GridStep.ToString("F4", Invariant),
                    input.Settings.EarlyStoppingGenerations.ToString(),
                    input.Settings.ConvergenceThresholdPercent.ToString("F4", Invariant),
                    input.Settings.RadiusStdDevPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.BalancePenaltyWeight.ToString("F4", Invariant),
                    input.Settings.MinimumRelocationDistance.ToString("F4", Invariant),
                    input.Settings.DuplicatePositionDistance.ToString("F4", Invariant),
                    input.Settings.SimilarPositionPenaltyWeight.ToString("F4", Invariant),
                    input.Settings.RelocationDistancePenaltyWeight.ToString("F4", Invariant),
                    input.Settings.MutationExplorationMultiplier.ToString("F4", Invariant),
                    input.Settings.EnableAdaptiveSigma.ToString().ToLower(),
                    input.Settings.SigmaCoolingExponent.ToString("F4", Invariant),
                    input.Settings.MinimumMutationSigma.ToString("F4", Invariant),
                    input.Settings.PositionSamplingAttempts.ToString(),
                    input.Settings.CraneFootprintRadius.ToString("F4", Invariant),
                    Escape(input.Settings.TargetElementTypes != null ? string.Join(";", input.Settings.TargetElementTypes) : "null"),
                    Escape(input.Settings.ProfilingLogDirectory ?? "null"),
                    input.Settings.EnableProfiling.ToString().ToLower(),
                    input.Settings.SlewOffsetX.ToString("F4", Invariant),
                    input.Settings.SlewOffsetY.ToString("F4", Invariant),
                    input.Settings.SlewOffsetZ.ToString("F4", Invariant),
                    input.Settings.BoomHingeOffsetX.ToString("F4", Invariant),
                    input.Settings.BoomHingeOffsetY.ToString("F4", Invariant),
                    input.Settings.BoomHingeOffsetZ.ToString("F4", Invariant),
                    input.Settings.EnableConcurrentOptimization.ToString().ToLower(),
                    input.Settings.HotSpotSeedingRatio.ToString("F4", Invariant),
                    input.Settings.EnableReachabilitySeeding.ToString().ToLower(),
                    input.Settings.MultiStageCoverageTarget.ToString("F4", Invariant),
                    input.Settings.EnableMultiStageDiscovery.ToString().ToLower(),
                    input.Settings.AdaptiveRelocationScaleFactor.ToString("F4", Invariant),
                    input.Settings.UseHungarianAssignment.ToString().ToLower(),
                    Escape(input.ProjectName ?? "N/A"),
                    Escape(input.IfcFileName ?? "N/A"),
                    Escape(input.ProjectFolderPath ?? "N/A"),
                    Escape(input.DiagnosticsReportsDirectory ?? "N/A"),
                    input.TotalElementsCount.ToString(),
                    Escape(input.CraneSpec?.Crane?.Name ?? "N/A"),
                    input.Settings.RunId ?? string.Empty
                }));
            }

            // 3. ga_stage_summary.csv
            string stageSummaryPath = Path.Combine(reportsDir, "ga_stage_summary.csv");
            bool stageSummaryExists = File.Exists(stageSummaryPath);
            using (var writer = new StreamWriter(stageSummaryPath, true, new UTF8Encoding(true)))
            {
                if (!stageSummaryExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,StageIndex,MaxPositions,RelocationWeight,BestCoverageRatio,BestCoveragePercent,BestLiftedCount,BestFitnessScore,RawPositionCount,FinalPositionCount,UsedZoneIds,RuntimeMs,Escalated,TerminationReason,AssignmentEngineUsed,TargetCoverage,PopulationSize,MaxGenerations,EarlyStoppingGenerations,Placeholder1,Placeholder2");
                }

                var lastStage = GAProfiler.StageLogs.LastOrDefault();
                if (lastStage != null)
                {
                    string optRunId = string.IsNullOrWhiteSpace(lastStage.OptimizationRunId) ? (input.Settings.OptimizationRunId ?? string.Empty) : lastStage.OptimizationRunId;
                    string stgRunId = string.IsNullOrWhiteSpace(lastStage.StageRunId) ? (input.Settings.StageRunId ?? string.Empty) : lastStage.StageRunId;
                    writer.WriteLine(string.Join(",", new[]
                    {
                        optRunId,
                        stgRunId,
                        "1.0",
                        timeFormatted,
                        lastStage.StageIndex.ToString(),
                        lastStage.MaxPositions.ToString(),
                        lastStage.RelocationWeight.ToString("F4", Invariant),
                        lastStage.BestCoverageRatio.ToString("F6", Invariant),
                        lastStage.BestCoveragePercent.ToString("F4", Invariant),
                        lastStage.BestLiftedCount.ToString(),
                        lastStage.BestFitnessScore.ToString("F4", Invariant),
                        lastStage.RawPositionCount.ToString(),
                        lastStage.FinalPositionCount.ToString(),
                        Escape(lastStage.UsedZoneIds),
                        lastStage.RuntimeMs.ToString("F2", Invariant),
                        lastStage.Escalated.ToString().ToLower(),
                        Escape(lastStage.TerminationReason),
                        Escape(input.Settings.UseHungarianAssignment ? "Hungarian" : "Greedy"),
                        input.Settings.MultiStageCoverageTarget.ToString("F4", Invariant),
                        input.Settings.PopulationSize.ToString(),
                        input.Settings.MaxGenerations.ToString(),
                        input.Settings.EarlyStoppingGenerations.ToString(),
                        "N/A",
                        "N/A"
                    }));
                }
            }

            // 4. ga_generation_summary.csv
            string genSummaryPath = Path.Combine(reportsDir, "ga_generation_summary.csv");
            bool genSummaryExists = File.Exists(genSummaryPath);
            using (var writer = new StreamWriter(genSummaryPath, true, new UTF8Encoding(true)))
            {
                if (!genSummaryExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,StageIndex,GenerationIndex,BestFitness,AverageFitness,BestCoverageRatio,AverageCoverageRatio,BestPositionCount,Count1Position,Count2Positions,Count3Positions,MutationSuccessRate,InvalidMutationCount,StagnationCounter,Placeholder1,Placeholder2,Placeholder3,Placeholder4");
                }

                int currentStageIndex = input.Settings.EnableMultiStageDiscovery ? input.Settings.MaxCranePositions : 1;
                var currentStageGens = GAProfiler.GenerationLogs.Where(g => g.StageIndex == currentStageIndex);

                foreach (var g in currentStageGens)
                {
                    string optRunId = string.IsNullOrWhiteSpace(g.OptimizationRunId) ? (input.Settings.OptimizationRunId ?? string.Empty) : g.OptimizationRunId;
                    string stgRunId = string.IsNullOrWhiteSpace(g.StageRunId) ? (input.Settings.StageRunId ?? string.Empty) : g.StageRunId;
                    writer.WriteLine(string.Join(",", new[]
                    {
                        optRunId,
                        stgRunId,
                        "1.0",
                        timeFormatted,
                        g.StageIndex.ToString(),
                        g.GenerationIndex.ToString(),
                        g.BestFitness.ToString("F4", Invariant),
                        g.AverageFitness.ToString("F4", Invariant),
                        g.BestCoverageRatio.ToString("F6", Invariant),
                        g.AverageCoverageRatio.ToString("F6", Invariant),
                        g.BestPositionCount.ToString(),
                        g.Count1Position.ToString(),
                        g.Count2Positions.ToString(),
                        g.Count3Positions.ToString(),
                        g.MutationSuccessRate.ToString("F4", Invariant),
                        g.InvalidMutationCount.ToString(),
                        g.StagnationCounter.ToString(),
                        "N/A",
                        "N/A",
                        "N/A",
                        "N/A"
                    }));
                }
            }

            // 5. ga_reachability_hotspots.csv
            var hotSpotsList = new List<ScoredCoordinate>();
            if (_reachabilityMapService != null)
            {
                int topN = Math.Max(100, (int)((input.Settings.PopulationSize) * (input.Settings.MaxCranePositions) * 1.5));
                var hotSpots = _reachabilityMapService.ComputeHotSpots(
                    input.Elements ?? new List<PrecastElement>(),
                    input.Zones ?? new List<GreenAreaZone>(),
                    input.ForbiddenZones ?? new List<GreenAreaZone>(),
                    input.CraneSpec,
                    input.Settings,
                    topN);
                if (hotSpots != null)
                {
                    hotSpotsList.AddRange(hotSpots);
                }
            }

            string hotspotsPath = Path.Combine(reportsDir, "ga_reachability_hotspots.csv");
            bool hotspotsExists = File.Exists(hotspotsPath);
            using (var writer = new StreamWriter(hotspotsPath, true, new UTF8Encoding(true)))
            {
                if (!hotspotsExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,ZoneId,X,Y,ReachableCount,RankWithinZone,GlobalRank,SelectedAsSeed,Z,HotSpotSeedingRatio,EnableReachabilitySeeding,Placeholder1,Placeholder2,Placeholder3");
                }

                if (hotSpotsList.Count > 0)
                {
                    var zoneGroups = hotSpotsList
                        .GroupBy(h => h.ZoneId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(h => h.ReachableCount).ToList()
                        );

                    for (int i = 0; i < hotSpotsList.Count; i++)
                    {
                        var h = hotSpotsList[i];
                        int globalRank = i + 1;
                        int rankWithinZone = zoneGroups.TryGetValue(h.ZoneId, out var list) ? list.IndexOf(h) + 1 : 1;
                        
                        bool selectedAsSeed = finalResult.Positions.Any(pos => {
                            double dist = Math.Sqrt(Math.Pow(pos.X - h.X, 2) + Math.Pow(pos.Y - h.Y, 2));
                            return dist < 1.0; // 1 meter tolerance
                        });

                        writer.WriteLine(string.Join(",", new[]
                        {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            "1.0",
                            timeFormatted,
                            h.ZoneId.ToString(),
                            h.X.ToString("F4", Invariant),
                            h.Y.ToString("F4", Invariant),
                            h.ReachableCount.ToString(),
                            rankWithinZone.ToString(),
                            globalRank.ToString(),
                            selectedAsSeed.ToString().ToLower(),
                            "0.0",
                            input.Settings.HotSpotSeedingRatio.ToString("F4", Invariant),
                            input.Settings.EnableReachabilitySeeding.ToString().ToLower(),
                            "N/A",
                            "N/A",
                            "N/A"
                        }));
                    }
                }
            }

            // 6. ga_positions.csv
            string positionsPath = Path.Combine(reportsDir, "ga_positions.csv");
            bool positionsExists = File.Exists(positionsPath);
            using (var writer = new StreamWriter(positionsPath, true, new UTF8Encoding(true)))
            {
                if (!positionsExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,PositionIndex,X,Y,Z,ZoneId,LiftedCount,UniqueLiftedCount,AssignmentEngineUsed,CoverageContributionRatio,Placeholder1");
                }

                for (int i = 0; i < finalResult.Positions.Count; i++)
                {
                    var pos = finalResult.Positions[i];
                    var containingZone = input.Zones?.FirstOrDefault(z => SpatialMath.PointInPolygon(new DomainPoint3D(pos.X, pos.Y, 0), z.Vertices));
                    Guid zoneId = containingZone?.ZoneId ?? Guid.Empty;

                    int liftedCount = pos.LiftableCount;
                    int uniqueLiftedCount = pos.ElementResults.Count(r => r.IsFinalAssignedToPoint && r.Status == LiftStatus.Liftable);

                    writer.WriteLine(string.Join(",", new[]
                    {
                        input.Settings.OptimizationRunId ?? string.Empty,
                        input.Settings.StageRunId ?? string.Empty,
                        "1.0",
                        timeFormatted,
                        (i + 1).ToString(),
                        pos.X.ToString("F4", Invariant),
                        pos.Y.ToString("F4", Invariant),
                        pos.Z.ToString("F4", Invariant),
                        zoneId.ToString(),
                        liftedCount.ToString(),
                        uniqueLiftedCount.ToString(),
                        Escape(input.Settings.UseHungarianAssignment ? "Hungarian" : "Greedy"),
                        (allElements.Count > 0 ? ((double)uniqueLiftedCount / allElements.Count) : 0.0).ToString("F6", Invariant),
                        "N/A"
                    }));
                }
            }

            // 7. ga_element_assignments.csv
            string assignmentsPath = Path.Combine(reportsDir, "ga_element_assignments.csv");
            bool assignmentsExists = File.Exists(assignmentsPath);
            using (var writer = new StreamWriter(assignmentsPath, true, new UTF8Encoding(true)))
            {
                if (!assignmentsExists)
                {
                    writer.WriteLine("OptimizationRunId,StageRunId,DiagnosticsSchemaVersion,Timestamp,ElementId,ElementType,AssignedPositionIndex,AssignedZoneId,LiftStatus,Radius,RequiredBoomLength,RequiredWeight,ChartCapacity,AvailableCapacity,FailureReason,IsFinalLifted,DeratingRatio,SafetyFactor,AdditionalWt,WireLength,Placeholder1,Placeholder2");
                }

                foreach (var element in allElements)
                {
                    if (string.IsNullOrWhiteSpace(element.ElementID)) continue;

                    // Find if there is a final assigned position
                    int assignedPosIndex = -1;
                    Guid assignedZoneId = Guid.Empty;
                    ElementLiftResult? bestRes = null;

                    for (int i = 0; i < finalResult.Positions.Count; i++)
                    {
                        var pos = finalResult.Positions[i];
                        var er = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.Ordinal));
                        if (er != null && er.IsFinalAssignedToPoint)
                        {
                            assignedPosIndex = i + 1;
                            var containingZone = input.Zones?.FirstOrDefault(z => SpatialMath.PointInPolygon(new DomainPoint3D(pos.X, pos.Y, 0), z.Vertices));
                            assignedZoneId = containingZone?.ZoneId ?? Guid.Empty;
                            bestRes = er;
                            break;
                        }
                    }

                    if (bestRes == null)
                    {
                        // Element not lifted, find best failed evaluation
                        var candidates = new List<ElementLiftResult>();
                        for (int i = 0; i < finalResult.Positions.Count; i++)
                        {
                            var pos = finalResult.Positions[i];
                            var er = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.Ordinal));
                            if (er != null) candidates.Add(er);
                        }

                        if (candidates.Count > 0)
                        {
                            // Order candidates to find best failure
                            bestRes = candidates.FirstOrDefault(c => string.Equals(c.CandidatePointId, c.BestFailedPointId, StringComparison.OrdinalIgnoreCase))
                                         ?? candidates.OrderByDescending(c => c.AssignmentScore).First();
                        }
                    }

                    if (bestRes != null)
                    {
                        string failReason = "Liftable";
                        if (bestRes.Status != LiftStatus.Liftable)
                        {
                            var classification = LiftClassificationPolicy.Classify(
                                bestRes.Status,
                                bestRes.ChartStatus,
                                element.ElementWT,
                                bestRes.BoomLength,
                                input.CraneSpec?.MaxBoomLength ?? 0.0,
                                finalResult.Positions.Count > 0);
                            failReason = classification.FailureReason;
                        }

                        double combinedFactors = input.Settings.DeratingRatio * input.Settings.SafetyFactor;
                        double chartCapacity = combinedFactors > 0 ? (bestRes.AvailableCapacity / combinedFactors) : bestRes.AvailableCapacity;
                        bool isFinalLifted = assignedPosIndex >= 0 && bestRes.Status == LiftStatus.Liftable;

                        writer.WriteLine(string.Join(",", new[]
                        {
                            input.Settings.OptimizationRunId ?? string.Empty,
                            input.Settings.StageRunId ?? string.Empty,
                            "1.0",
                            timeFormatted,
                            Escape(element.ElementID),
                            Escape(element.ElementType ?? "N/A"),
                            assignedPosIndex.ToString(),
                            assignedZoneId.ToString(),
                            bestRes.Status.ToString(),
                            bestRes.Radius.ToString("F4", Invariant),
                            bestRes.BoomLength.ToString("F4", Invariant),
                            (element.ElementWT ?? 0.0).ToString("F4", Invariant),
                            chartCapacity.ToString("F4", Invariant),
                            bestRes.AvailableCapacity.ToString("F4", Invariant),
                            Escape(failReason),
                            isFinalLifted ? "true" : "false",
                            input.Settings.DeratingRatio.ToString("F4", Invariant),
                            input.Settings.SafetyFactor.ToString("F4", Invariant),
                            input.Settings.AdditionalWt.ToString("F4", Invariant),
                            input.Settings.WireLength.ToString("F4", Invariant),
                            "N/A",
                            "N/A"
                        }));
                    }
                }
            }
#endif

            var benchmarkV2Writer = new BenchmarkV2CsvWriter(_reachabilityMapService);
            bool isResearch = diagLevel >= DiagnosticsLevel.Research;
            benchmarkV2Writer.Write(reportsDir, timeFormatted, input, finalResult, isResearch);

            return diagLevel >= DiagnosticsLevel.Research && File.Exists(detailedPath)
                ? detailedPath
                : string.Empty;
        }

        private static bool IsBetterThan(ElementEvaluationResult candidate, ElementEvaluationResult best)
        {
            if (candidate.Status == LiftStatus.Liftable && best.Status != LiftStatus.Liftable)
                return true;
            if (candidate.Status != LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
                return false;
            if (candidate.Status == LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
                return candidate.Radius < best.Radius;

            int rankCand = GetFailureRank(candidate.Status);
            int rankBest = GetFailureRank(best.Status);
            if (rankCand > rankBest)
                return true;
            if (rankCand < rankBest)
                return false;

            return candidate.Radius < best.Radius;
        }

        private static int GetFailureRank(LiftStatus status)
        {
            return LiftClassificationPolicy.GetFailureRank(status);
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool mustQuote = value.Contains(',')
                || value.Contains('"')
                || value.Contains('\r')
                || value.Contains('\n');

            if (!mustQuote)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// Strips characters that are invalid in filesystem path segments so that
        /// ProjectName and OptimizationRunId can be safely embedded in directory names.
        /// Returns an empty string when the input is null or whitespace.
        /// </summary>
        private static string SanitizePathSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString().Trim();
        }

        private static string[] FormatHeaderWithPrefix(string[] originalHeader)
        {
            var toRemove = new HashSet<string> { "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion", "ProjectName", "CraneName", "RandomSeed" };
            var headerList = new List<string>();
            for (int i = 0; i < originalHeader.Length; i++)
            {
                if (!toRemove.Contains(originalHeader[i]))
                {
                    headerList.Add(originalHeader[i]);
                }
            }
            return GABatchDiagnosticsHelper.HeaderPrefix.Concat(headerList).ToArray();
        }

        private static string[] FormatRowWithPrefix(
            PlacementInput input,
            bool isStageFile,
            string[] originalHeader,
            string[] originalRow,
            string schemaVersion = "1.0")
        {
            var toRemove = new HashSet<string> { "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion", "ProjectName", "CraneName", "RandomSeed" };
            var rowList = new List<string>();
            for (int i = 0; i < originalHeader.Length; i++)
            {
                if (!toRemove.Contains(originalHeader[i]))
                {
                    rowList.Add(originalRow[i]);
                }
            }
            var prefixRows = GABatchDiagnosticsHelper.GetRowPrefix(input, isStageFile, schemaVersion).Select(Escape).ToArray();
            return prefixRows.Concat(rowList).ToArray();
        }

        private string FormatSurveyIfEnabled(double x, double y, double z)
        {
            if (_transformService != null && _transformService.IsEnabled)
            {
                var survey = _transformService.LocalToSurvey(new DomainPoint3D(x, y, z));
                return $"({survey.X.ToString("0.###", Invariant)}; {survey.Y.ToString("0.###", Invariant)}; {survey.Z.ToString("0.###", Invariant)})";
            }
            return "N/A";
        }

        private class ElementDiagnosticRow
        {
            public string Timestamp { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string IfcFileName { get; set; } = "";
            public string CraneNameOrId { get; set; } = "";
            public string ElementId { get; set; } = "";
            public string ElementName { get; set; } = "";
            public string ElementType { get; set; } = "";
            public string StoreyOrElevation { get; set; } = "";
            public double Weight { get; set; }
            public double CogX { get; set; }
            public double CogY { get; set; }
            public double CogZ { get; set; }
            public int AssignedSetupIndex { get; set; }
            public double AssignedCraneX { get; set; }
            public double AssignedCraneY { get; set; }
            public double AssignedCraneZ { get; set; }
            public double BestRadius { get; set; }
            public double RequiredBoomLength { get; set; }
            public double RequiredCapacity { get; set; }
            public double RawChartCapacity { get; set; }
            public double AvailableCapacity { get; set; }
            public double AdditionalWt { get; set; }
            public double DeratingRatio { get; set; }
            public double SafetyFactor { get; set; }
            public double WireLength { get; set; }
            public string LiftStatus { get; set; } = "";
            public bool IsLifted { get; set; }
            public string FailureReason { get; set; } = "";
            public string FailureCategory { get; set; } = "";
            public string SuggestedAction { get; set; } = "";
            public string ReachStatus { get; set; } = "";
            public string ChartStatus { get; set; } = "";
            public bool IsExactMatch { get; set; }
            public bool IsSteppedRadius { get; set; }
            public bool IsBlankCell { get; set; }
            public bool IsZeroCell { get; set; }
            public double SafetyMargin { get; set; }
            public string LookupMode { get; set; } = "";
            public double RequiredRadius { get; set; }
            public double SelectedChartRadius { get; set; }
            public double SelectedChartBoomLength { get; set; }
            public int? SelectedVisualColumnIndex { get; set; }
            public string SelectedVisualColumnHeader { get; set; } = "";
            public int? SelectedNumberOfLines { get; set; }
            public string SelectedOperatingConditionLabel { get; set; } = "";
            public bool IsConservativeDuplicateFallback { get; set; }
            public int DuplicateAlternativeCount { get; set; }
            public string DuplicateAlternativeSummary { get; set; } = "";
            public double CraneBaseX { get; set; }
            public double CraneBaseY { get; set; }
            public double CraneBaseZ { get; set; }
            public double SlewCenterX { get; set; }
            public double SlewCenterY { get; set; }
            public double SlewCenterZ { get; set; }
            public double BoomHingeX { get; set; }
            public double BoomHingeY { get; set; }
            public double BoomHingeZ { get; set; }
            public double LoadPointX { get; set; }
            public double LoadPointY { get; set; }
            public double LoadPointZ { get; set; }
            public string RadiusOriginType { get; set; } = "SlewCenter";
            public string BoomOriginType { get; set; } = "BoomHinge";
            public string CandidateBoomsChecked { get; set; } = "";
            public string RejectedCandidates { get; set; } = "";

            // GA Assignment and Uniqueness Fields
            public string CandidatePointId { get; set; } = "";
            public int CandidatePointIndex { get; set; } = -1;
            public bool IsFeasibleFromPoint { get; set; }
            public bool IsFinalAssignedToPoint { get; set; }
            public string AssignedPointId { get; set; } = "";
            public int AssignedPointIndex { get; set; } = -1;
            public string AssignmentReason { get; set; } = "";
            public double AssignmentScore { get; set; }
            public bool DuplicateAssignmentResolved { get; set; }
            public string FeasiblePointIds { get; set; } = "";
            public string FinalOwnerPointId { get; set; } = "";
            public bool IsFinalAssigned { get; set; }
            public string BestFailedPointId { get; set; } = "";
            public string BestFailureReason { get; set; } = "";

            public bool HasBestFailedCandidate { get; set; }
            public string BestFailedLiftStatus { get; set; } = "";
            public string BestFailedReachStatus { get; set; } = "";
            public string BestFailedChartStatus { get; set; } = "";
            public double BestFailedSafetyMargin { get; set; }
            public double BestFailedRequiredRadius { get; set; }
            public double BestFailedRequiredBoomLength { get; set; }
        }
    }
}
