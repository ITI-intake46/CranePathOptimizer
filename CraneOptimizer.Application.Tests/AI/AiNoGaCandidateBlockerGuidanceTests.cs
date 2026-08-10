using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Workflow;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiNoGaCandidateBlockerGuidanceTests
    {
        [Fact]
        public void WorkflowReadiness_TreatsNoCandidateEvidenceAsAdvisoryNotOptimizerBlocker()
        {
            var service = new WorkflowReadinessService();
            var context = BuildAdvisoryContext();

            var snapshot = service.Evaluate(context, new UiInteractionContextService());
            var cards = service.BuildStructuredCards(snapshot);
            var state = AiWorkflowStateClassifier.Classify(context, snapshot);

            Assert.Equal(AiWorkflowStateKind.OptimizerVerificationRequired, state.Kind);
            Assert.True(state.CanProceedToOptimizerCheck);
            Assert.False(state.CanRunOptimizerNow);
            Assert.Equal(0, snapshot.MissingCriticalCount);
            Assert.Contains(snapshot.Items, item => item.Name.Contains("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
                                                   && !item.IsCritical);
            Assert.Contains(cards, card => card.Finding.Contains("Optimizer built-in readiness", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(cards, card => card.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase)
                                                  && card.Finding.Contains("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EvidenceTextBuilder_DoesNotDeclareGaBlockedFromAdvisoryCandidateSignal()
        {
            var text = AiWorkflowEvidenceTextBuilder.BuildNoGaCandidateAssistantAnswer(
                BuildAdvisoryContext(),
                "Can I run the optimizer now?",
                "ANALYSIS → Run Optimizer",
                "ANALYSIS");

            Assert.Contains("cannot certify", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Current Run Readiness", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("advisory", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Do **not** run the optimizer yet", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("optimization is blocked because", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("do not bypass", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void StrictPrompt_PreservesOptimizerAuthorityBoundary()
        {
            var text = AiWorkflowEvidenceTextBuilder.BuildStrictNoGaCandidatePrompt(
                BuildAdvisoryContext(),
                "Why does AI say zero candidates?",
                "ANALYSIS → Run Optimizer",
                "ANALYSIS");

            Assert.Contains("AUTHORITY", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Current Run Readiness", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("authoritative", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CURRENT WORKFLOW BLOCKER", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompletedResult_ConflictsWithZeroCandidateSignalWithoutBlockingCurrentRunCheck()
        {
            var context = BuildAdvisoryContext();
            context = new AiWorkflowContextSnapshot
            {
                HasProject = context.HasProject,
                CurrentProjectName = context.CurrentProjectName,
                ElementCount = context.ElementCount,
                ZoneCount = context.ZoneCount,
                CraneCount = context.CraneCount,
                HasLoadChartEvidence = context.HasLoadChartEvidence,
                ScreenContexts = context.ScreenContexts,
                OptimizationResult = new AiOptimizationResultSnapshot
                {
                    HasResult = true,
                    TotalElementCount = 357,
                    LiftedElementCount = 14,
                    CoverageRatio = 14d / 357d,
                    PositionCount = 1
                }
            };

            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var state = AiWorkflowStateClassifier.Classify(context);

            Assert.True(assessment.CandidateEvidenceConflict);
            Assert.Equal(357, assessment.HistoricalEvaluatedLiftUnits);
            Assert.Equal(AiWorkflowStateKind.ResultReview, state.Kind);
            Assert.True(state.CanProceedToOptimizerCheck);
            Assert.False(state.CanRunOptimizerNow);
            Assert.Contains("conflict", state.EvidencePrefix, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void StrictNoGaRouting_DoesNotHijackIfcMetadataQuestions()
        {
            var context = BuildAdvisoryContext();

            Assert.False(AiWorkflowEvidenceTextBuilder.IsNoGaCandidateWorkflowQuestion("Is it possible to know building type from IFC MODEL? The building usage for example?"));
            Assert.False(AiWorkflowEvidenceTextBuilder.ShouldUseStrictNoGaCandidateRouting(context, "Is it possible to know building type from IFC MODEL? The building usage for example?"));

            var answer = AiWorkflowEvidenceTextBuilder.BuildIfcMetadataAssistantAnswer(
                context,
                "Is it possible to know building type from IFC MODEL? The building usage for example?",
                "IFC MODEL");

            Assert.Contains("building type/usage is not exposed", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("should not infer", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Pset_BuildingCommon", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoIfcState_DoesNotClaimIfcIsLoadedOrRecommendElementExport()
        {
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Empty",
                ElementCount = 0,
                ZoneCount = 3,
                CraneCount = 0,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false }
            };

            var state = AiWorkflowStateClassifier.Classify(context);
            var answer = AiWorkflowEvidenceTextBuilder.BuildNoGaCandidateAssistantAnswer(context, "What should I do next?", "", "CRANES");

            Assert.Equal(AiWorkflowStateKind.NoIfcModel, state.Kind);
            Assert.Contains("IFC MODEL → Load IFC", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IFC is loaded", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Export Element List", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HistoricalResultCapacity_DoesNotProveCurrentLoadChartWhenCraneContextSaysNo()
        {
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Historical chart test",
                ElementCount = 100,
                ZoneCount = 1,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                OptimizationResult = new AiOptimizationResultSnapshot
                {
                    HasResult = true,
                    TotalElementCount = 100,
                    LiftedElementCount = 10,
                    CoverageRatio = 0.1,
                    MaxCapacity = 4.2,
                    MaxRadius = 30
                },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "Crane and load chart",
                        Stage = "Current crane",
                        Summary = "No current load chart selected.",
                        Facts = new Dictionary<string, string>
                        {
                            ["SelectedHasSpecification"] = "False",
                            ["SelectedCurrentRadius"] = "0",
                            ["SelectedCurrentCapacity"] = "0"
                        }
                    }
                }
            };

            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var state = AiWorkflowStateClassifier.Classify(context);

            Assert.False(assessment.HasCurrentLoadChartEvidence);
            Assert.Equal(AiWorkflowStateKind.LoadChartMissing, state.Kind);
            Assert.False(state.CanProceedToOptimizerCheck);
        }

        private static AiWorkflowContextSnapshot BuildAdvisoryContext()
        {
            return new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Strict isolation test",
                ElementCount = 362,
                ZoneCount = 1,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "Import/readiness review",
                        Summary = "Host import/readiness evidence reports zero candidate units.",
                        Facts = new Dictionary<string, string>
                        {
                            [AiWorkflowFactKeys.TrustedGaLiftUnits] = "0",
                            [AiWorkflowFactKeys.TrustedGroupedLiftUnits] = "0"
                        },
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "IFC is loaded; zones=1; cranes=1; host import/readiness signal=0.",
                                Recommendation = "Review trusted weight-source evidence.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            },
                            new AiContextIssueSnapshot
                            {
                                Code = "EstimatedWeightBlockedFromGA",
                                Severity = "Error",
                                Area = "Weight",
                                Evidence = "Estimated fallback weight exists in import/readiness evidence.",
                                Recommendation = "Review explicit quantities if the optimizer itself confirms a data blocker.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            }
                        }
                    }
                }
            };
        }
    }
}
