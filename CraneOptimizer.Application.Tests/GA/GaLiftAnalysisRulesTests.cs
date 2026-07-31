using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaLiftAnalysisRulesTests
{
    [Fact]
    public void R7_percentile_matches_the_documented_interpolation_method()
    {
        Assert.Equal(4.8, GaLiftAnalysisRules.PercentileR7([1, 2, 3, 4, 5], 0.95)!.Value, 10);
    }

    [Fact]
    public async Task Borderline_crane_can_never_be_classified_as_potentially_oversized()
    {
        var snapshot = BuildSuccessfulSnapshot(
            capacityUtilization: 0.40,
            radiusUtilization: 0.90,
            boomUtilization: 0.50);
        var service = new GaLiftAnalysisService(new FixedScreeningService(AlternativeScreeningOutcome.NotRun));

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = false
        });

        Assert.Equal(CurrentCraneAdequacy.Borderline, result.CurrentCraneAdequacy);
        Assert.Equal(RightSizingAssessment.Justified, result.RightSizingAssessment);
    }

    [Fact]
    public async Task Conservative_low_utilization_profile_flags_potential_oversizing_independently()
    {
        var snapshot = BuildSuccessfulSnapshot(
            capacityUtilization: 0.40,
            radiusUtilization: 0.50,
            boomUtilization: 0.50);
        var service = new GaLiftAnalysisService(new FixedScreeningService(AlternativeScreeningOutcome.NoValidatedAlternative));

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = true
        });

        Assert.Equal(CurrentCraneAdequacy.Suitable, result.CurrentCraneAdequacy);
        Assert.Equal(RightSizingAssessment.PotentiallyOversized, result.RightSizingAssessment);
        Assert.Equal(AlternativeScreeningOutcome.NoValidatedAlternative, result.AlternativeScreeningOutcome);
    }

    [Fact]
    public async Task Smaller_candidate_outcome_is_rejected_when_current_crane_is_inadequate()
    {
        var snapshot = BuildSuccessfulSnapshot(0.50, 0.50, 0.50);
        snapshot = WithReliableFailure(snapshot);
        var service = new GaLiftAnalysisService(new FixedScreeningService(AlternativeScreeningOutcome.SmallerCandidateAvailable));

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = true
        });

        Assert.Equal(CurrentCraneAdequacy.Inadequate, result.CurrentCraneAdequacy);
        Assert.Equal(AlternativeScreeningOutcome.Inconclusive, result.AlternativeScreeningOutcome);
    }

    [Fact]
    public async Task Complete_analysis_persists_controlling_lift_margin_setups_relocation_and_provenance()
    {
        var snapshot = BuildSuccessfulSnapshot(0.80, 0.60, 0.50);
        var service = new GaLiftAnalysisService(
            new FixedScreeningService(AlternativeScreeningOutcome.NotRun));

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = false
        });

        Assert.Equal("lift-1", result.Metrics.ControllingLiftUnitId);
        Assert.Equal("Capacity", result.Metrics.ControllingCriterion);
        Assert.Equal(5, result.Metrics.MinimumCapacityMarginTon);
        Assert.Equal(2, result.SetupSummaries.Count);
        Assert.Equal(5, result.Relocation.TotalRelocationDistanceM, 10);
        Assert.Equal(1, result.Relocation.RelocationCount);
        Assert.Equal("Project", result.RunContext.ProjectName);
        Assert.Equal("Current crane", result.RunContext.CraneName);
        Assert.Equal(5, result.LiftResults.Count);
        var controllingLift = Assert.Single(result.LiftResults, lift => lift.LiftUnitId == "lift-1");
        Assert.True(controllingLift.IsLifted);
        Assert.Equal("eval-1", controllingLift.EvidenceEvaluationId);
        Assert.Equal(0.80, controllingLift.CapacityUtilization);
        Assert.Equal(0.60, controllingLift.RadiusUtilization);
        Assert.Equal(0.50, controllingLift.BoomUtilization);
        Assert.Equal("KEEP_CURRENT_CRANE", result.ExecutiveSummary.OverallStatus);
        Assert.Equal("Current", result.ExecutiveSummary.CurrencyAtGeneration);
        Assert.Equal("HyndmanFanR7", result.Thresholds.PercentileMethod);
        Assert.Equal(0.80, result.Metrics.CapacityUtilizationMedian);
        Assert.Equal(0.80, result.Metrics.CapacityUtilizationAverage);
        Assert.Equal("position-1", result.LiftPlan.MostProductivePositionId);
        Assert.NotEmpty(result.Recommendations);
        Assert.Equal(1, result.Performance.StageCount);
        Assert.Equal("InconclusiveMissingConvergenceEvidence", result.Performance.RunInterpretation);
        Assert.Equal(GaLiftAnalysisConfidence.High, result.Confidence);
    }

    [Theory]
    [InlineData("Off")]
    [InlineData("Summary")]
    public async Task Analysis_does_not_depend_on_raw_diagnostics_at_any_normal_level(
        string diagnosticsLevel)
    {
        var snapshot = BuildSuccessfulSnapshot(0.60, 0.50, 0.50, diagnosticsLevel);
        var service = new GaLiftAnalysisService(
            new FixedScreeningService(AlternativeScreeningOutcome.NotRun));

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = false
        });

        Assert.Equal(diagnosticsLevel, result.RunContext.DiagnosticsLevel);
        Assert.Empty(result.TechnicalEvidence);
        Assert.Equal(5, result.LiftResults.Count);
    }

    [Fact]
    public async Task Smaller_candidate_must_replay_every_final_assignment_through_the_canonical_chart_engine()
    {
        var snapshot = BuildSuccessfulSnapshot(0.60, 0.40, 0.40);
        var screening = new GaAlternativeScreeningService(
            new CraneConfigurationEngine(new LoadChartCapacityResolver()));
        var candidate = CreateCandidate(capacity: 85, chartCapacity: 20);

        var result = await screening.ScreenAsync(
            snapshot,
            CurrentCraneAdequacy.Suitable,
            [candidate]);

        Assert.Equal(AlternativeScreeningOutcome.SmallerCandidateAvailable, result.Outcome);
        Assert.True(Assert.Single(result.Candidates).PreservesSuccessfulPlan);
    }

    [Fact]
    public async Task Candidate_with_missing_chart_evidence_is_rejected_without_blocking_screening()
    {
        var snapshot = BuildSuccessfulSnapshot(0.60, 0.40, 0.40);
        var screening = new GaAlternativeScreeningService(
            new CraneConfigurationEngine(new LoadChartCapacityResolver()));
        var candidate = CreateCandidate(capacity: 85, chartCapacity: null);

        var result = await screening.ScreenAsync(
            snapshot,
            CurrentCraneAdequacy.Suitable,
            [candidate]);

        Assert.Equal(AlternativeScreeningOutcome.NoValidatedAlternative, result.Outcome);
        var rejected = Assert.Single(result.Candidates);
        Assert.False(rejected.IsValidatedAlternative);
        Assert.Contains(
            "Complete positive load-chart evidence is unavailable.",
            rejected.RejectionReasons);
    }

    [Fact]
    public void Currency_service_reports_the_exact_changed_engineering_component()
    {
        var snapshot = BuildSuccessfulSnapshot(0.60, 0.40, 0.40);
        var saved = new GaLiftAnalysisResult
        {
            ProjectGuid = snapshot.Project.ProjectGuid,
            OptimizationRunId = snapshot.Run.OptimizationRunId,
            SourceSnapshotFingerprint = snapshot.Model.OptimizationInputFingerprint,
            SourceFingerprintComponents = snapshot.Model.OptimizationInputFingerprintComponents
        };
        var changedComponents = new Dictionary<string, string>(
            snapshot.Model.OptimizationInputFingerprintComponents,
            StringComparer.Ordinal)
        {
            ["CapacityFactors"] = "changed"
        };
        var current = new GaResultSnapshotDto
        {
            Project = snapshot.Project,
            Run = snapshot.Run,
            Model = new GaModelSnapshotDto
            {
                OptimizationInputFingerprint = "changed-aggregate",
                OptimizationInputFingerprintComponents = changedComponents
            }
        };

        var currentContext = new GaAnalysisCurrentContextProvider().Capture(current);
        var freshness = new GaAnalysisCurrencyService().Compare(saved, currentContext);

        Assert.False(freshness.IsCurrent);
        Assert.Equal(["CapacityFactors"], freshness.ChangedComponents);
    }

    private static GaResultSnapshotDto BuildSuccessfulSnapshot(
        double capacityUtilization,
        double radiusUtilization,
        double boomUtilization,
        string diagnosticsLevel = "Off")
    {
        var evaluations = Enumerable.Range(1, 5)
            .Select(index => new GaElementEvaluationSnapshotDto
            {
                EvaluationId = $"eval-{index}",
                LiftUnitId = $"lift-{index}",
                CandidatePointId = "position-1",
                LiftStatus = "Liftable",
                IsFeasibleFromPoint = true,
                IsFinalAssignedToPoint = true,
                RequiredRadiusM = radiusUtilization * 100,
                RequiredBoomLengthM = boomUtilization * 100,
                RequiredCapacityTon = 10,
                CapacityUtilizationPercent = capacityUtilization * 100,
                SafetyMarginTon = 10 - index,
                CraneBaseLocal = new GaPointSnapshotDto { X = 0, Y = 0, Z = 0 },
                LoadPointLocal = new GaPointSnapshotDto { X = 10, Y = 0, Z = 10 }
            })
            .ToArray();
        return new GaResultSnapshotDto
        {
            Project = new GaProjectSnapshotDto { ProjectGuid = "project", ProjectName = "Project" },
            Run = new GaRunIdentitySnapshotDto
            {
                OptimizationRunId = "run",
                SnapshotSchemaVersion = 3,
                Status = "Completed",
                CompletedAtUtc = DateTimeOffset.UtcNow
            },
            Stages =
            [
                new GaStageSummarySnapshotDto
                {
                    StageRunId = "stage-1",
                    StageIndex = 1,
                    WasSelectedAsFinal = true
                }
            ],
            Model = new GaModelSnapshotDto
            {
                ModelFingerprint = "model",
                OptimizationInputFingerprint = "input",
                OptimizationInputFingerprintVersion = 2,
                OptimizationInputFingerprintComponents = GaOptimizationInputFingerprint.RequiredComponentNames
                    .ToDictionary(name => name, _ => "hash", StringComparer.Ordinal)
            },
            EffectiveFinalSettings = new GaSettingsSnapshotDto
            {
                DeratingRatio = 1,
                SafetyFactor = 1,
                DiagnosticsLevel = diagnosticsLevel
            },
            Crane = new GaCraneSnapshotDto
            {
                CraneSpecificationId = 1,
                CraneName = "Current crane",
                MaxCapacityTon = 100,
                MaxRadiusM = 100,
                MaxBoomLengthM = 100
            },
            Summary = new GaRunSummarySnapshotDto
            {
                TotalTargetLiftUnitCount = 5,
                LiftedLiftUnitCount = 5,
                CoverageRatio = 1
            },
            Capabilities = GaSnapshotCapabilitiesDto.Production(false, false, false),
            LiftUnits = evaluations.Select(evaluation => new CanonicalLiftUnitSnapshotDto
            {
                LiftUnitId = evaluation.LiftUnitId,
                LiftWeightTon = 10,
                HeightM = 10
            }).ToArray(),
            Positions =
            [
                new GaPositionSnapshotDto
                {
                    PositionId = "position-1",
                    SetupIndex = 1,
                    SetupReference = "Setup 1",
                    FinalAssignedCount = 3,
                    LocalPosition = new GaPointSnapshotDto { X = 0, Y = 0, Z = 0 },
                    Evaluations = evaluations.Take(3).ToArray()
                },
                new GaPositionSnapshotDto
                {
                    PositionId = "position-2",
                    SetupIndex = 2,
                    SetupReference = "Setup 2",
                    FinalAssignedCount = 2,
                    LocalPosition = new GaPointSnapshotDto { X = 3, Y = 4, Z = 0 },
                    Evaluations = evaluations.Skip(3).ToArray()
                }
            ],
            FinalOutcomes = evaluations.Select(evaluation => new GaFinalOutcomeSnapshotDto
            {
                LiftUnitId = evaluation.LiftUnitId,
                IsLifted = true,
                FinalEvaluationId = evaluation.EvaluationId,
                FinalOwnerPositionId = evaluation.LiftUnitId is "lift-1" or "lift-2" or "lift-3"
                    ? "position-1"
                    : "position-2",
                FinalLiftStatus = "Liftable"
            }).ToArray()
        };
    }

    private static GaCraneCandidate CreateCandidate(double capacity, double? chartCapacity) =>
        new(
            new GaCraneCandidateSpecification
            {
                Id = 2,
                CraneName = "Candidate crane",
                MaxCapacity = capacity,
                MinRadius = 1,
                MaxRadius = 100,
                MinBoomLength = 5,
                MaxBoomLength = 100,
                LoadChart = chartCapacity.HasValue
                    ?
                    [
                        new GaCraneCandidateLoadChartPoint
                        {
                            Radius = 10,
                            BoomLength = 20,
                            Capacity = chartCapacity.Value
                        }
                    ]
                    : Array.Empty<GaCraneCandidateLoadChartPoint>()
            },
            "Candidate crane");

    private static GaResultSnapshotDto WithReliableFailure(GaResultSnapshotDto source)
    {
        var outcomes = source.FinalOutcomes.Take(4).Concat(
        [
            new GaFinalOutcomeSnapshotDto
            {
                LiftUnitId = "lift-5",
                IsLifted = false,
                BestFailedEvaluationId = "eval-5",
                FinalLiftStatus = "OutOfRadius",
                FailureCategory = "OutOfRadius",
                FailureReason = "Required radius exceeds the crane limit"
            }
        ]).ToArray();
        return new GaResultSnapshotDto
        {
            Project = source.Project,
            Run = source.Run,
            Model = source.Model,
            Crane = source.Crane,
            Capabilities = source.Capabilities,
            Positions = source.Positions,
            FinalOutcomes = outcomes,
            Summary = new GaRunSummarySnapshotDto
            {
                TotalTargetLiftUnitCount = 5,
                LiftedLiftUnitCount = 4,
                UnliftedLiftUnitCount = 1,
                CoverageRatio = 0.8
            }
        };
    }

    private sealed class FixedScreeningService : IGaAlternativeScreeningService
    {
        private readonly AlternativeScreeningOutcome _outcome;

        public FixedScreeningService(AlternativeScreeningOutcome outcome)
        {
            _outcome = outcome;
        }

        public Task<GaAlternativeScreeningResult> ScreenAsync(
            GaResultSnapshotDto snapshot,
            CurrentCraneAdequacy adequacy,
            IReadOnlyList<GaCraneCandidate> candidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GaAlternativeScreeningResult { Outcome = _outcome });
    }
}
