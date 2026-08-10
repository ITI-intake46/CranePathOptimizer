using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Workflow;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiWorkflowReadinessContextTests
    {
        [Fact]
        public void WorkflowReadiness_IncludesScreenContextIssuesAsCards()
        {
            var service = new WorkflowReadinessService();
            var uiContext = new UiInteractionContextService();
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "AI Context Test",
                ElementCount = 10,
                ZoneCount = 1,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                OptimizationResult = new AiOptimizationResultSnapshot
                {
                    HasResult = true,
                    CoverageRatio = 0.9,
                    LiftedElementCount = 9,
                    TotalElementCount = 10,
                    PositionCount = 1
                },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "Element review",
                        Stage = "Review",
                        Summary = "Selected element identity needs review.",
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "SelectedElementIdentityLowConfidence",
                                Severity = "Warning",
                                Area = "Identity",
                                Evidence = "Assembly mark confidence is Low.",
                                Recommendation = "Review lift mark source before reporting.",
                                NextClick = "IFC MODEL → Element Management",
                                AffectedCount = 1
                            }
                        }
                    }
                }
            };

            var snapshot = service.Evaluate(context, uiContext);
            var cards = service.BuildStructuredCards(snapshot);

            Assert.Contains(snapshot.Items, item => item.Name.Contains("SelectedElementIdentityLowConfidence"));
            Assert.Contains(cards, card => card.Finding.Contains("SelectedElementIdentityLowConfidence"));
        }

        [Fact]
        public void WorkflowReadiness_AddsReviewRequiredAiReportNarrativeWhenResultExists()
        {
            var service = new WorkflowReadinessService();
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Report Context Test",
                ElementCount = 2,
                ZoneCount = 1,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                OptimizationResult = new AiOptimizationResultSnapshot
                {
                    HasResult = true,
                    CoverageRatio = 1.0,
                    LiftedElementCount = 2,
                    TotalElementCount = 2,
                    PositionCount = 1
                },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "Reports",
                        Stage = "Ready for reviewed report",
                        Summary = "Report can be drafted from deterministic evidence.",
                        Facts = new Dictionary<string, string> { ["HasOptimizationResult"] = "true" }
                    }
                }
            };

            var snapshot = service.Evaluate(context, new UiInteractionContextService());

            Assert.Contains(snapshot.Items, item => item.Name == "AI-assisted report narrative" && item.Severity == "Warning");
        }

        [Fact]
        public void WorkflowReadiness_PrioritizesCorePrerequisiteBeforeDownstreamGaDataBlocker()
        {
            var service = new WorkflowReadinessService();
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "IFC loaded setup missing",
                ElementCount = 2483,
                ZoneCount = 0,
                CraneCount = 0,
                HasLoadChartEvidence = false,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "GA blocked",
                        Summary = "No trusted GA-ready lift units are available.",
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "Trusted GA-ready lift units: 0.",
                                Recommendation = "Review trusted weight-source evidence.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 2125
                            }
                        }
                    }
                }
            };

            var snapshot = service.Evaluate(context, new UiInteractionContextService());
            var cards = service.BuildStructuredCards(snapshot);

            Assert.NotEmpty(cards);
            Assert.Equal("Placement zones", cards[0].Finding);
            Assert.Contains("Allowed Zone", cards[0].NextClick);
            Assert.Contains(cards, card => card.Finding.Contains("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase));
        }
        [Fact]
        public void WorkflowReadiness_ResultSnapshotWithoutSelectionIdentity_DoesNotClaimSelectedPercentage()
        {
            var service = new WorkflowReadinessService();
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Result Provenance",
                ElementCount = 357,
                ZoneCount = 2,
                AllowedZoneCount = 2,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                LoadChartEvidenceCount = 1,
                OptimizationResult = new AiOptimizationResultSnapshot
                {
                    HasResult = true,
                    CoverageRatio = 0.1933,
                    LiftedElementCount = 69,
                    TotalElementCount = 357,
                    PositionCount = 2
                }
            };

            var snapshot = service.Evaluate(context, new UiInteractionContextService());

            Assert.Equal("Result review", snapshot.CurrentStage);
            Assert.DoesNotContain("selected result", snapshot.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("19.33", snapshot.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Lift Summary", snapshot.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(snapshot.Items, item =>
                item.Name == "Result review"
                && item.NextClick.Contains("Lift Summary", StringComparison.OrdinalIgnoreCase)
                && item.Evidence.Contains("does not expose", StringComparison.OrdinalIgnoreCase));
        }

    }
}
