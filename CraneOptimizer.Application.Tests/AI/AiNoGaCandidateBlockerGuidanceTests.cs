using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Workflow;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiNoGaCandidateBlockerGuidanceTests
    {
        [Fact]
        public void WorkflowReadiness_PrioritizesNoGaCandidateBlockerWithoutSuggestingBypass()
        {
            var service = new WorkflowReadinessService();
            var context = BuildBlockedContext();

            var snapshot = service.Evaluate(context, new UiInteractionContextService());
            var cards = service.BuildStructuredCards(snapshot);

            Assert.Contains(snapshot.Items, item => item.Name.Contains("GaCannotStartNoCandidates"));
            Assert.Contains(cards, card => card.Finding.Contains("GaCannotStartNoCandidates"));
            Assert.False(cards.Any(card => card.Recommendation.Contains("force", StringComparison.OrdinalIgnoreCase)));
            Assert.False(cards.Any(card => card.Recommendation.Contains("use estimated weights", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(cards, card => card.NextClick.Contains("trusted weight-source", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EvidenceTextBuilder_BuildsBimChecklistFromNoCandidateEvidence()
        {
            var text = AiWorkflowEvidenceTextBuilder.BuildBimExporterChecklist(BuildBlockedContext());

            Assert.Contains("explicit", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("mass", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("weight", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assembly", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not change optimizer eligibility", text, StringComparison.OrdinalIgnoreCase);
        }



        [Fact]
        public void EvidenceTextBuilder_BuildsOperationalNextStepInsteadOfDocumentationSummary()
        {
            var text = AiWorkflowEvidenceTextBuilder.BuildNoGaCandidateAssistantAnswer(
                BuildBlockedContext(),
                "What should I do next?",
                "IFC MODEL → Export Element List / trusted weight-source review",
                "CRANES");

            Assert.Contains("Do **not** run the optimizer yet", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("zero trusted GA-ready grouped lift units", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IFC MODEL", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Export Element List", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("XAML", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("detailed documentation", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not bypass", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EvidenceTextBuilder_StrictPromptSuppressesGenericInventoryForBlockerQuestions()
        {
            var text = AiWorkflowEvidenceTextBuilder.BuildStrictNoGaCandidatePrompt(
                BuildBlockedContext(),
                "What should I do next?",
                "IFC MODEL → Export Element List / trusted weight-source review",
                "CRANES");

            Assert.Contains("CURRENT WORKFLOW BLOCKER", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("answer only because", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not mention XAML inventory", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not suggest bypassing GA", text, StringComparison.OrdinalIgnoreCase);
        }



        [Fact]
        public void StrictNoGaRouting_DoesNotHijackIfcMetadataQuestions()
        {
            var context = BuildBlockedContext();

            Assert.False(AiWorkflowEvidenceTextBuilder.IsNoGaCandidateWorkflowQuestion("Is it possible to know building type from IFC MODEL? The building usage for example?"));
            Assert.False(AiWorkflowEvidenceTextBuilder.ShouldUseStrictNoGaCandidateRouting(context, "Is it possible to know building type from IFC MODEL? The building usage for example?"));

            var answer = AiWorkflowEvidenceTextBuilder.BuildIfcMetadataAssistantAnswer(
                context,
                "Is it possible to know building type from IFC MODEL? The building usage for example?",
                "IFC MODEL");

            Assert.Contains("building type/usage is not exposed", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("should not infer", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Pset_BuildingCommon", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Separate optimization caveat", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("current deterministic workflow blocker", answer, StringComparison.OrdinalIgnoreCase);
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
        public void CompanionPanel_ExposesBlockerAndCopyActions()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);

            var companionXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var companionVm = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "ViewModels", "AiCompanionViewModel.cs"));
            var ifcContributor = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.UI", "AIIntegration", "Contributors", "IfcAiContextContributor.cs"));
            var gaContributor = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.UI", "AIIntegration", "Contributors", "GaAiContextContributor.cs"));

            Assert.Contains("Blocker", companionXaml);
            Assert.Contains("CopyBimExporterChecklist", companionVm);
            Assert.Contains("CopyIssueSummary", companionVm);
            Assert.Contains("GaCannotStartNoCandidates", ifcContributor);
            Assert.Contains("GaPreRunNoCandidates", gaContributor);
        }

        private static AiWorkflowContextSnapshot BuildBlockedContext()
        {
            return new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Blocked IFC Test",
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
                        Stage = "GA blocked — no trusted lift units",
                        Summary = "IFC is loaded but no trusted GA-ready lift units exist.",
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "IFC is loaded; zones=1; cranes=1; 0 trusted GA-ready grouped lift units.",
                                Recommendation = "Do not bypass the optimizer gate. Review/export explicit BIM mass or weight quantities.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            },
                            new AiContextIssueSnapshot
                            {
                                Code = "EstimatedWeightBlockedFromGA",
                                Severity = "Error",
                                Area = "Weight",
                                Evidence = "Estimated fallback weight is used and strictly blocked from GA.",
                                Recommendation = "Provide explicit quantities so elements are not blocked from GA.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            }
                        }
                    }
                }
            };
        }

        private static string? FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            return null;
        }
    }
}
