using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Workflow;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiIntentOrchestratorTests
    {
        [Theory]
        [InlineData("What is a crane?", AiIntentOrchestrator.AiIntentFamily.DomainExplanation)]
        [InlineData("What is critical about crane logistics?", AiIntentOrchestrator.AiIntentFamily.DomainExplanation)]
        [InlineData("Why is this program important?", AiIntentOrchestrator.AiIntentFamily.AppCapabilityExplanation)]
        [InlineData("What is needed to be delivered?", AiIntentOrchestrator.AiIntentFamily.DeliveryRequirements)]
        [InlineData("How to ensure accuracy of results?", AiIntentOrchestrator.AiIntentFamily.EngineeringQualityAssurance)]
        [InlineData("How to implement this in real life?", AiIntentOrchestrator.AiIntentFamily.RealWorldImplementation)]
        [InlineData("Which local model is best?", AiIntentOrchestrator.AiIntentFamily.ModelOpsRecommendation)]
        public void OpenEndedQuestionsRouteToLlmInsteadOfCannedDeterministicAnswers(
            string question,
            AiIntentOrchestrator.AiIntentFamily expectedIntent)
        {
            var context = BuildContext();

            var handled = AiIntentOrchestrator.TryBuildDeterministicAnswer(
                context,
                BuildReadiness(),
                question,
                "AI COPILOT",
                "llama3.2:latest",
                "Fastest benchmark response: llama3.2:latest (3,200 ms).",
                "Fastest responding model: llama3.2:latest (3,200 ms).",
                "CRANES → MOBILE CRANE",
                out var answer,
                out var source,
                out var decision);

            Assert.False(handled);
            Assert.Equal(expectedIntent, decision.Intent);
            Assert.True(decision.NeedsOllama);
            Assert.Empty(answer);
            Assert.Empty(source);
        }

        [Theory]
        [InlineData("Are there 50 or 116 slabs?")]
        [InlineData("Which slab count should I use for lift planning?")]
        [InlineData("Compare the slab and beam counts in both scopes.")]
        [InlineData("Why does the Filter show 999 slabs while BIM contains 116?")]
        [InlineData("The Filter says 999 slabs.")]
        [InlineData("Which slab count is correct?")]
        public void BimCategoryCountBoundaryNeverRequestsModelProvider(string question)
        {
            var decision = AiIntentOrchestrator.Classify(BuildContext(), question, "IFC MODEL");

            Assert.Equal(AiIntentOrchestrator.AiIntentFamily.ProjectFact, decision.Intent);
            Assert.False(decision.NeedsOllama);
            Assert.Equal("current_project_bim", decision.Scope);
        }

        [Fact]
        public void PromptContractForOpenEndedQuestionForbidsMemorizedQaAndDocumentationSummaries()
        {
            var context = BuildContext();

            var contract = AiIntentOrchestrator.BuildPromptContract(context, "What is a crane?", "HOME", "CRANES → MOBILE CRANE");

            Assert.Contains("Intent family: DomainExplanation", contract);
            Assert.Contains("Suppress broad application documentation: True", contract);
            Assert.Contains("Start by answering the visible user question directly", contract);
            Assert.Contains("Do not use or imitate a memorized Q&A answer", contract);
            Assert.Contains("DomainExplanation: answer the domain concept first", contract);
        }


        [Fact]
        public void DeliveryAndAccuracyQuestionsDoNotUseStrictBlockerRouting()
        {
            var context = BuildContext();

            Assert.False(AiIntentOrchestrator.ShouldUseStrictWorkflowBlockerRouting(context, "What is needed to be delivered?", "AI COPILOT"));
            Assert.False(AiIntentOrchestrator.ShouldUseStrictWorkflowBlockerRouting(context, "How to ensure accuracy of results?", "AI COPILOT"));
        }

        [Fact]
        public void DomainEvidencePacketPreventsDocumentationAndBlockerHijack()
        {
            var packet = AiIntentOrchestrator.BuildEvidenceRoutingPacket(
                BuildContext(),
                BuildReadiness(),
                "What is a crane?",
                "HOME",
                "CRANES → MOBILE CRANE");

            Assert.Contains("General crane/construction domain knowledge is primary", packet);
            Assert.Contains("do not let project blockers hijack", packet, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Local documentation is not an answer source", packet);
        }

        [Fact]
        public void QualityGateFlagsDocumentationSummaryFailures()
        {
            var decision = AiIntentOrchestrator.Classify(BuildContext(), "How to ensure accuracy of results?", "AI COPILOT");

            var needsRepair = AiIntentOrchestrator.NeedsResponseRepair(
                decision,
                "This appears to be a documentation for a crane placement optimization software. Here's a summary of the main sections.",
                out var reason);

            Assert.True(needsRepair);
            Assert.Contains("documentation summary", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ModelOpsEvidenceIsFactualNotARecommendationBody()
        {
            var evidence = AiIntentOrchestrator.BuildModelOpsEvidence(
                "llama3.2:latest",
                "Fastest benchmark response: llama3.2:latest (3,200 ms).",
                "Fastest responding model: llama3.2:latest (3,200 ms).");

            Assert.Contains("Selected model: llama3.2:latest", evidence);
            Assert.Contains("Benchmark summary", evidence);
            Assert.Contains("Do not claim a model is best unless benchmark", evidence);
            Assert.DoesNotContain("Use a **fast, small local model**", evidence, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GreenAreaQuestionUsesDeterministicZoneCounts()
        {
            var context = BuildContext();

            var handled = AiIntentOrchestrator.TryBuildDeterministicAnswer(
                context,
                BuildReadiness(),
                "How many green areas have we drawn?",
                "ZONES",
                null,
                null,
                null,
                "ZONES → Allowed Zone",
                out var answer,
                out var source,
                out var decision);

            Assert.True(handled);
            Assert.Equal(AiIntentOrchestrator.AiIntentFamily.ProjectFact, decision.Intent);
            Assert.False(decision.NeedsOllama);
            Assert.Contains("2 green/allowed", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Forbidden zones", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("project fact", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BuildingAreaQuestionUsesSnapshotMetricAndCaveat()
        {
            var context = BuildContext();

            var handled = AiIntentOrchestrator.TryBuildDeterministicAnswer(
                context,
                BuildReadiness(),
                "What is the building's area?",
                "IFC MODEL",
                null,
                null,
                null,
                null,
                out var answer,
                out _,
                out var decision);

            Assert.True(handled);
            Assert.Equal(AiIntentOrchestrator.AiIntentFamily.ProjectFact, decision.Intent);
            Assert.False(decision.NeedsOllama);
            Assert.Contains("1,145.44", answer);
            Assert.Contains("m²", answer);
            Assert.Contains("not a certified survey area", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UnitQuestionUsesVisibleProjectUnitProfile()
        {
            var context = BuildContext();

            var handled = AiIntentOrchestrator.TryBuildDeterministicAnswer(
                context,
                BuildReadiness(),
                "What are the units used?",
                "HOME",
                null,
                null,
                null,
                null,
                out var answer,
                out _,
                out var decision);

            Assert.True(handled);
            Assert.Equal(AiIntentOrchestrator.AiIntentFamily.ProjectFact, decision.Intent);
            Assert.Contains("metric unit profile", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("m²", answer);
            Assert.Contains("kg/m³", answer);
            Assert.DoesNotContain("lbf", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GroundedFallbackForRealWorldImplementationStartsWithFieldWorkflow()
        {
            var handled = AiIntentOrchestrator.TryBuildGroundedFallbackAnswer(
                BuildContext(),
                BuildReadiness(),
                "How to implement this in real life?",
                "AI COPILOT",
                "llama3.2:latest",
                null,
                null,
                "CRANES → MOBILE CRANE",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("engineering workflow first", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("site survey", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("manufacturer load charts", answer, StringComparison.OrdinalIgnoreCase);
        }

        private static AiWorkflowContextSnapshot BuildContext()
        {
            return new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Bahy",
                ProjectDisplayName = "Bahy",
                ModelDisplayName = "Bahy",
                ModelRevision = 7,
                ModelFingerprint = "test-intent-context",
                ElementCount = 362,
                ElementTableVisibleCount = 331,
                ElementTableTotalCount = 337,
                ZoneCount = 3,
                AllowedZoneCount = 2,
                ForbiddenZoneCount = 1,
                AllowedZoneArea = 250.0,
                ForbiddenZoneArea = 40.0,
                CraneCount = 0,
                LoadChartEvidenceCount = 0,
                HasLoadChartEvidence = false,
                BuildingArea = 1145.44,
                BuildingAreaUnit = "m²",
                BuildingHeight = 14.60,
                BuildingLengthUnit = "m",
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "Review",
                        Summary = "Model loaded with no trusted GA-ready grouped lift units.",
                        Facts = new Dictionary<string, string>
                        {
                            ["TopResolvedCategories"] = "Column (205), Slab (60), Beam (34)",
                            ["TotalElements"] = "362"
                        }
                    }
                }
            };
        }

        private static WorkflowReadinessSnapshot BuildReadiness()
        {
            return new WorkflowReadinessSnapshot
            {
                CurrentStage = "Crane setup",
                CompleteCount = 3,
                TotalCount = 16,
                MissingCriticalCount = 5,
                CompletionRatio = 3d / 16d,
                Summary = "Crane setup is blocked until a mobile crane and load-chart evidence are added.",
                Items = Array.Empty<WorkflowReadinessItem>()
            };
        }
    }
}
