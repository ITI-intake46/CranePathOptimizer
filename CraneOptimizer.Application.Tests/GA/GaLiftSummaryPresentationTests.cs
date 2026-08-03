using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaLiftSummaryPresentationTests
{
    [Fact]
    public void Saved_capacity_metrics_are_presented_without_recalculation()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        analysis = CopyWithMetrics(analysis, new GaLiftMetricSummary
        {
            TotalLiftCount = 4,
            SuccessfulLiftCount = 3,
            FailedLiftCount = 1,
            CoverageRatio = 0.75,
            CapacityUtilizationMaximum = 0.994,
            CapacityUtilizationAverage = 0.625,
            MinimumCapacityMarginTon = 0.06,
            NearLimitLiftCount = 2
        });

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("99.4%", summary.HighestCapacityUseText);
        Assert.Equal("62.5%", summary.AverageCapacityUseText);
        Assert.Equal("0.06 t", summary.SmallestCapacityReserveText);
        Assert.Equal("2", summary.NearCapacityLimitText);
        Assert.Equal(
            "Completed lifts operating at or above the configured capacity-use advisory threshold.",
            summary.NearCapacityLimitTooltip);
    }

    [Fact]
    public void Average_capacity_fallback_excludes_missing_and_invalid_completed_values()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        GaLiftAnalysisItem[] lifts =
        [
            Completed(0.4, 20),
            Completed(0.8, 20),
            Completed(null, 20),
            Completed(0.2, 0),
            Failed("Overweight")
        ];
        analysis = CopyWithMetricsAndLifts(
            analysis,
            new GaLiftMetricSummary
            {
                TotalLiftCount = 5,
                SuccessfulLiftCount = 4,
                FailedLiftCount = 1,
                CoverageRatio = 0.8,
                NearLimitLiftCount = 0
            },
            lifts);

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("60.0%", summary.AverageCapacityUseText);
        Assert.Equal("80.0%", summary.HighestCapacityUseText);
    }

    [Fact]
    public void Safety_factor_and_derating_are_not_applied_to_saved_capacity_metrics()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        analysis = CopyWithMetrics(analysis, new GaLiftMetricSummary
        {
            TotalLiftCount = 4,
            SuccessfulLiftCount = 3,
            FailedLiftCount = 1,
            CoverageRatio = 0.75,
            CapacityUtilizationMaximum = 0.5,
            CapacityUtilizationAverage = 0.4,
            MinimumCapacityMarginTon = 1,
            NearLimitLiftCount = 0
        });

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("50.0%", summary.HighestCapacityUseText);
        Assert.Equal("40.0%", summary.AverageCapacityUseText);
        Assert.Equal("0.95", summary.SafetyFactorText);
        Assert.Equal("0.90", summary.DeratingRatioText);
    }

    [Theory]
    [InlineData("Overweight", "capacity", "1")]
    [InlineData("InsufficientCapacity", "capacity", "1")]
    [InlineData("OutOfRadius", "radius", "1")]
    [InlineData("RadiusEnvelopeFailure", "radius", "1")]
    [InlineData("HeightFail", "boom", "1")]
    [InlineData("BoomLengthExceeded", "boom", "1")]
    [InlineData("NotCharted", "chart", "1")]
    [InlineData("OutsideChartEnvelope", "chart", "1")]
    [InlineData("InvalidBlankCell", "chart", "1")]
    [InlineData("MissingWeightOrInvalidData", "input", "1")]
    [InlineData("NoValidSetupPosition", "input", "1")]
    [InlineData("SpecialCase", "other", "1")]
    public void Canonical_unresolved_outcomes_map_to_one_user_facing_category(
        string code,
        string category,
        string expected)
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis([code]));

        Assert.Equal(expected, FailureCount(summary, category));
        Assert.Equal("1", summary.UnresolvedLiftsText);
        Assert.Null(summary.InternalConsistencyWarningCode);
    }

    [Fact]
    public void Failure_totals_reconcile_and_each_unresolved_outcome_is_counted_once()
    {
        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(
            CreateAnalysis(
            [
                "Overweight",
                "OutOfRadius",
                "HeightFail",
                "OutsideChartEnvelope",
                "MissingWeightOrInvalidData",
                "SpecialCase"
            ]));

        int mapped =
            Parse(summary.CapacityFailuresText) +
            Parse(summary.RadiusFailuresText) +
            Parse(summary.BoomHeightFailuresText) +
            Parse(summary.LoadChartFailuresText) +
            Parse(summary.InputDataIssuesText) +
            Parse(summary.OtherUnresolvedIssuesText);
        Assert.Equal(Parse(summary.UnresolvedLiftsText), mapped);
        Assert.Equal(6, mapped);
        Assert.Null(summary.InternalConsistencyWarningCode);
    }

    [Fact]
    public void Canonical_failure_category_wins_over_conflicting_secondary_evidence()
    {
        GaLiftAnalysisResult source = CreateAnalysis(["Overweight"]);
        GaLiftAnalysisItem canonicalOutcome = new()
        {
            LiftUnitId = "hidden-lift",
            IsLifted = false,
            FailureCategory = "Overweight",
            LiftStatus = "OutOfRadius",
            FailureReason = "HeightFail"
        };
        GaLiftAnalysisResult analysis = CopyWithMetricsAndLifts(
            source,
            source.Metrics,
            source.LiftResults.Where(item => item.IsLifted).Append(canonicalOutcome).ToArray());

        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("1", summary.CapacityFailuresText);
        Assert.Equal("0", summary.RadiusFailuresText);
        Assert.Equal("0", summary.BoomHeightFailuresText);
    }

    [Fact]
    public void Missing_outcome_records_are_reconciled_as_other_and_raise_a_coded_warning()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        analysis = CopyWithMetrics(analysis, new GaLiftMetricSummary
        {
            TotalLiftCount = 5,
            SuccessfulLiftCount = 3,
            FailedLiftCount = 2,
            CoverageRatio = 0.6
        });

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("1", summary.CapacityFailuresText);
        Assert.Equal("1", summary.OtherUnresolvedIssuesText);
        Assert.Equal("LIFT_SUMMARY_FAILURE_TOTAL_MISMATCH", summary.InternalConsistencyWarningCode);
        Assert.Contains("require review", summary.SummaryStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zero_failure_categories_are_hidden_only_after_authoritative_mapping()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["OutOfRadius"]));

        Assert.False(summary.HasCapacityFailures);
        Assert.True(summary.HasRadiusFailures);
        Assert.False(summary.HasBoomHeightFailures);
        Assert.False(summary.HasLoadChartFailures);
    }

    [Fact]
    public void Input_only_failures_do_not_call_the_crane_inadequate()
    {
        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(
            CreateAnalysis(["MissingWeightOrInvalidData", "NoValidSetupPosition"]));

        Assert.Contains("cannot be confirmed", summary.SummaryStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cannot complete", summary.SummaryStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inadequate", summary.ExecutiveSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before changing crane size", summary.PrimaryRecommendationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mixed_failures_produce_a_mixed_headline_and_ordered_recommendation()
    {
        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(
            CreateAnalysis(["Overweight", "MissingWeightOrInvalidData"]));

        Assert.Contains("while others require corrected input", summary.SummaryStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Correct incomplete lift information first", summary.PrimaryRecommendationText);
    }

    [Fact]
    public void Missing_chart_evidence_does_not_recommend_a_larger_crane()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["InvalidBlankCell"]));

        Assert.Contains("cannot be confirmed", summary.SummaryStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correct the selected crane load chart", summary.PrimaryRecommendationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("larger crane", summary.PrimaryRecommendationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dominant_failure_uses_the_largest_authoritative_category()
    {
        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(
            CreateAnalysis(["OutOfRadius", "OutOfRadius", "OutOfRadius", "Overweight"]));

        Assert.Equal("Dominant Failure: Outside Working Radius", summary.DominantFailureTitleText);
        Assert.Contains("Most unresolved lifts", summary.DominantFailureExplanationText, StringComparison.Ordinal);
    }

    [Fact]
    public void Engineering_inputs_come_from_the_saved_analysis_context()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["Overweight"]));

        Assert.Equal("0.95", summary.SafetyFactorText);
        Assert.Equal("0.90", summary.DeratingRatioText);
        Assert.Equal("2.0 m", summary.WireLengthText);
        Assert.Equal("0.35 t", summary.LiftingAccessoriesWeightText);
        Assert.Equal("Grouped Lifts", summary.OperatingModeText);
    }

    [Fact]
    public void Historical_accessory_weight_uses_saved_aggregate_rigging_when_total_is_absent()
    {
        GaLiftAnalysisResult source = CreateAnalysis(["Overweight"]);
        GaLiftAnalysisResult analysis = CopyWithRunContext(
            source,
            new GaAnalysisRunContext
            {
                ProjectName = source.RunContext.ProjectName,
                CraneName = source.RunContext.CraneName,
                OptimizerCompletedAtUtc = source.RunContext.OptimizerCompletedAtUtc,
                SafetyFactor = source.RunContext.SafetyFactor,
                DeratingRatio = source.RunContext.DeratingRatio,
                WireRopeLengthM = source.RunContext.WireRopeLengthM,
                HookRiggingWeightTon = 0.2,
                TotalLiftingAccessoriesWeightTon = 0,
                OperatingMode = source.RunContext.OperatingMode
            });

        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("0.20 t", summary.LiftingAccessoriesWeightText);
    }

    [Fact]
    public void Completed_run_uses_the_final_success_status_wording()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis([]));

        Assert.Equal(
            "The current crane completed all evaluated lifts.",
            summary.SummaryStatusText);
    }

    [Fact]
    public void Search_and_placement_values_come_from_the_saved_analysis()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["Overweight"]));

        Assert.Equal("150", summary.CandidatePointsGeneratedText);
        Assert.Equal("110", summary.ValidCandidatePointsText);
        Assert.Equal("40", summary.RejectedCandidatePointsText);
        Assert.Equal("3", summary.SelectedCranePositionsText);
        Assert.Equal("12.3 sec", summary.OptimizationTimeText);
        Assert.True(summary.HasOptimizationTime);
    }

    [Fact]
    public void Missing_optional_search_values_are_unavailable_not_false_zero()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        analysis = CopyWithPerformance(analysis, new GaPerformanceAnalysisSummary
        {
            FinalSetupPositionCount = 0
        });

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("Unavailable", summary.CandidatePointsGeneratedText);
        Assert.Equal("Unavailable", summary.ValidCandidatePointsText);
        Assert.Equal("Unavailable", summary.RejectedCandidatePointsText);
        Assert.False(summary.HasOptimizationTime);
    }

    [Fact]
    public void Crane_display_name_removes_workflow_words_and_repairs_tons_spacing()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"], craneName: "100Tons solved");

        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Equal("100 Tons", summary.CraneName);
    }

    [Fact]
    public void Input_evidence_suppresses_candidate_claims_until_data_is_corrected()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["MissingWeightOrInvalidData"], candidateCount: 4));

        Assert.Empty(summary.AlternativeRows);
        Assert.Contains("cannot be evaluated reliably", summary.AlternativesMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No validated replacement", summary.AlternativesMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Copy_summary_contains_user_metrics_and_no_technical_or_identity_values()
    {
        GaLiftAnalysisResult analysis = CreateAnalysis(["Overweight"]);
        GaLiftSummaryPresentationModel summary = new GaLiftSummaryPresentationMapper().Map(analysis);

        Assert.Contains("Highest capacity use:", summary.CopyText, StringComparison.Ordinal);
        Assert.Contains("Average capacity use:", summary.CopyText, StringComparison.Ordinal);
        Assert.Contains("Safety factor: 0.95", summary.CopyText, StringComparison.Ordinal);
        Assert.Contains("Wire length: 2.0 m", summary.CopyText, StringComparison.Ordinal);
        Assert.Contains(
            "Lifting accessories weight: 0.35 t",
            summary.CopyText,
            StringComparison.Ordinal);
        Assert.Contains("Candidate points generated: 150", summary.CopyText, StringComparison.Ordinal);
        Assert.DoesNotContain("P95", summary.CopyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("percentile", summary.CopyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", summary.CopyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(analysis.OptimizationRunId, summary.CopyText, StringComparison.Ordinal);
        Assert.DoesNotContain("lift-", summary.CopyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Geometry_and_approved_safety_language_are_plain_and_complete()
    {
        GaLiftSummaryPresentationModel summary =
            new GaLiftSummaryPresentationMapper().Map(CreateAnalysis(["Overweight"]));

        Assert.Contains(
            summary.Limitations,
            text => text.Contains("same crane-geometry assumptions", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            summary.Limitations,
            text => text.Contains("RunSettingsSharedOffsets", StringComparison.Ordinal));
        Assert.Contains(
            summary.Limitations,
            text => text.Contains("existing crane setup positions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            summary.Limitations,
            text => text.Contains("ground bearing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            summary.Limitations,
            text => text.Contains("does not automatically approve", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("This summary is an application-generated advisory assessment", summary.SafetyNotice);
        Assert.Contains("qualified engineering review", summary.SafetyNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Async_mapping_builds_the_immutable_model_on_a_worker_task()
    {
        IGaLiftSummaryPresentationMapper mapper = new GaLiftSummaryPresentationMapper();

        GaLiftSummaryPresentationModel summary =
            await mapper.MapAsync(CreateAnalysis(["Overweight"]));

        Assert.Equal("1", summary.CapacityFailuresText);
        Assert.IsType<GaLiftSummaryPresentationModel>(summary);
    }

    private static string FailureCount(GaLiftSummaryPresentationModel summary, string category) =>
        category switch
        {
            "capacity" => summary.CapacityFailuresText,
            "radius" => summary.RadiusFailuresText,
            "boom" => summary.BoomHeightFailuresText,
            "chart" => summary.LoadChartFailuresText,
            "input" => summary.InputDataIssuesText,
            _ => summary.OtherUnresolvedIssuesText
        };

    private static int Parse(string value) =>
        int.Parse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture);

    private static GaLiftAnalysisResult CreateAnalysis(
        IReadOnlyList<string> failureCodes,
        string craneName = "Crane A",
        int candidateCount = 1)
    {
        GaLiftAnalysisItem[] completed =
        [
            Completed(0.50, 20),
            Completed(0.70, 20),
            Completed(0.90, 20)
        ];
        GaLiftAnalysisItem[] failed = failureCodes
            .Select(Failed)
            .ToArray();
        int total = completed.Length + failed.Length;
        return new GaLiftAnalysisResult
        {
            ProjectGuid = "project-secret",
            OptimizationRunId = "12345678-run-secret",
            GeneratedAtUtc = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
            RunContext = new GaAnalysisRunContext
            {
                ProjectName = "Project Alpha",
                CraneName = craneName,
                OptimizerCompletedAtUtc = new DateTimeOffset(2026, 7, 27, 11, 0, 0, TimeSpan.Zero),
                SafetyFactor = 0.95,
                DeratingRatio = 0.90,
                WireRopeLengthM = 2,
                HookRiggingWeightTon = 0.2,
                TotalLiftingAccessoriesWeightTon = 0.35,
                OperatingMode = "GroupedLifts"
            },
            AlternativeScreeningOutcome = AlternativeScreeningOutcome.NoValidatedAlternative,
            Confidence = GaLiftAnalysisConfidence.High,
            Thresholds = new GaLiftAnalysisThresholds { BorderlineUtilization = 0.90 },
            Metrics = new GaLiftMetricSummary
            {
                TotalLiftCount = total,
                SuccessfulLiftCount = completed.Length,
                FailedLiftCount = failed.Length,
                CoverageRatio = completed.Length / (double)total,
                CapacityUtilizationMaximum = 0.90,
                CapacityUtilizationAverage = 0.70,
                MinimumCapacityMarginTon = 1.25,
                NearLimitLiftCount = 1
            },
            Performance = new GaPerformanceAnalysisSummary
            {
                CandidatePointsGenerated = 150,
                ValidCandidatePoints = 110,
                RejectedCandidatePoints = 40,
                FinalSetupPositionCount = 3,
                RunDurationMilliseconds = 12_345
            },
            CandidateResults = Enumerable.Range(1, candidateCount)
                .Select(index => new GaCandidateScreeningResult
                {
                    CraneName = $"Candidate {index}",
                    NominalCapacityTon = 80 + index,
                    PreservesSuccessfulPlan = true,
                    RecoveredFailedLiftCount = failed.Length,
                    IsValidatedAlternative = index == 1
                })
                .ToArray(),
            LiftResults = completed.Concat(failed).ToArray(),
            GeometrySource = "RunSettingsSharedOffsets"
        };
    }

    private static GaLiftAnalysisItem Completed(double? utilization, double available) => new()
    {
        LiftUnitId = $"lift-completed-{Guid.NewGuid():N}",
        IsLifted = true,
        LiftStatus = "Liftable",
        CapacityUtilization = utilization,
        AvailableCapacityTon = available,
        RequiredCapacityTon = utilization.HasValue ? utilization.Value * available : null,
        SafetyMarginTon = utilization.HasValue ? available - (utilization.Value * available) : null
    };

    private static GaLiftAnalysisItem Failed(string code) => new()
    {
        LiftUnitId = $"lift-failed-{Guid.NewGuid():N}",
        IsLifted = false,
        LiftStatus = code,
        FailureCategory = code,
        FailureReason = code,
        ChartStatus = code == "OutsideChartEnvelope" ? "OutsideChartEnvelope" : null
    };

    private static GaLiftAnalysisResult CopyWithMetrics(
        GaLiftAnalysisResult source,
        GaLiftMetricSummary metrics) =>
        new()
        {
            ProjectGuid = source.ProjectGuid,
            OptimizationRunId = source.OptimizationRunId,
            GeneratedAtUtc = source.GeneratedAtUtc,
            RunContext = source.RunContext,
            AlternativeScreeningOutcome = source.AlternativeScreeningOutcome,
            Confidence = source.Confidence,
            Thresholds = source.Thresholds,
            Metrics = metrics,
            Performance = source.Performance,
            CandidateResults = source.CandidateResults,
            LiftResults = source.LiftResults,
            GeometrySource = source.GeometrySource
        };

    private static GaLiftAnalysisResult CopyWithMetricsAndLifts(
        GaLiftAnalysisResult source,
        GaLiftMetricSummary metrics,
        IReadOnlyList<GaLiftAnalysisItem> lifts) =>
        new()
        {
            ProjectGuid = source.ProjectGuid,
            OptimizationRunId = source.OptimizationRunId,
            GeneratedAtUtc = source.GeneratedAtUtc,
            RunContext = source.RunContext,
            AlternativeScreeningOutcome = source.AlternativeScreeningOutcome,
            Confidence = source.Confidence,
            Thresholds = source.Thresholds,
            Metrics = metrics,
            Performance = source.Performance,
            CandidateResults = source.CandidateResults,
            LiftResults = lifts,
            GeometrySource = source.GeometrySource
        };

    private static GaLiftAnalysisResult CopyWithPerformance(
        GaLiftAnalysisResult source,
        GaPerformanceAnalysisSummary performance) =>
        new()
        {
            ProjectGuid = source.ProjectGuid,
            OptimizationRunId = source.OptimizationRunId,
            GeneratedAtUtc = source.GeneratedAtUtc,
            RunContext = source.RunContext,
            AlternativeScreeningOutcome = source.AlternativeScreeningOutcome,
            Confidence = source.Confidence,
            Thresholds = source.Thresholds,
            Metrics = source.Metrics,
            Performance = performance,
            CandidateResults = source.CandidateResults,
            LiftResults = source.LiftResults,
            GeometrySource = source.GeometrySource
        };

    private static GaLiftAnalysisResult CopyWithRunContext(
        GaLiftAnalysisResult source,
        GaAnalysisRunContext runContext) =>
        new()
        {
            ProjectGuid = source.ProjectGuid,
            OptimizationRunId = source.OptimizationRunId,
            GeneratedAtUtc = source.GeneratedAtUtc,
            RunContext = runContext,
            AlternativeScreeningOutcome = source.AlternativeScreeningOutcome,
            Confidence = source.Confidence,
            Thresholds = source.Thresholds,
            Metrics = source.Metrics,
            Performance = source.Performance,
            CandidateResults = source.CandidateResults,
            LiftResults = source.LiftResults,
            GeometrySource = source.GeometrySource
        };
}
