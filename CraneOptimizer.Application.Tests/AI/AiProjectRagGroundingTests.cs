using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge.Rag;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiProjectRagGroundingTests
    {
        [Fact]
        public void WorkflowProjector_CreatesCitationAddressableDocumentsFromLiveSnapshot()
        {
            var projector = new AiWorkflowKnowledgeProjector();

            var documents = projector.Project(BuildBlockedContext());

            Assert.Contains(documents, document => document.SourceType == "ProjectSnapshot");
            Assert.Contains(documents, document => document.SourceType == "OptimizationSnapshot");
            Assert.Contains(documents, document => document.SourceType == "ScreenSnapshot");
            Assert.Contains(documents, document => document.SourceType == "WorkflowIssue"
                                                   && document.Content.Contains("GaCannotStartNoCandidates", StringComparison.Ordinal));
            Assert.All(documents, document => Assert.Equal(12, document.ModelRevision));
            Assert.All(documents, document => Assert.Contains("project-bahy", document.Id, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void HybridRetriever_RanksDeterministicBlockerEvidenceForOptimizationQuestion()
        {
            var projector = new AiWorkflowKnowledgeProjector();
            var retriever = new AiHybridKnowledgeRetriever(new AiDeterministicHashEmbeddingService());
            var documents = projector.Project(BuildBlockedContext());

            var results = retriever.Retrieve(
                documents,
                "Why is optimization blocked and what should I fix next?",
                5);

            Assert.NotEmpty(results);
            Assert.Equal("E1", results[0].Id);
            Assert.Contains(results, citation => citation.SourceType == "WorkflowIssue"
                                                 && citation.Snippet.Contains("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(results, citation => citation.Snippet.Contains("trusted GA-ready", StringComparison.OrdinalIgnoreCase));
            Assert.All(results, citation => Assert.InRange(citation.Score, 0.15, 1.0));
        }

        [Fact]
        public void GroundingService_BuildsBoundedPromptWithEvidenceIdsAndModelIdentity()
        {
            var service = CreateService();

            var packet = service.Ground(
                BuildBlockedContext(),
                "Why is the optimizer blocked?",
                4);

            Assert.True(packet.HasEvidence);
            Assert.InRange(packet.Citations.Count, 1, 4);
            Assert.Equal(12, packet.ModelRevision);
            Assert.Equal("model:test-bahy:12", packet.ModelFingerprint);
            Assert.Contains("PROJECT RAG GROUNDING PACKET", packet.PromptContext, StringComparison.Ordinal);
            Assert.Contains("[E1]", packet.PromptContext, StringComparison.Ordinal);
            Assert.Contains("Citation contract", packet.PromptContext, StringComparison.Ordinal);
            Assert.Contains("Never recommend bypassing", packet.PromptContext, StringComparison.Ordinal);
        }

        [Fact]
        public void GroundingService_AppendsAuditableCitationTraceToDeterministicAnswer()
        {
            var service = CreateService();
            var packet = service.Ground(BuildBlockedContext(), "What should I do next?", 3);

            var answer = service.AppendCitationTrace(
                "Do not run GA yet. Review trusted weight evidence first.",
                packet);

            Assert.Contains("## Grounding evidence", answer, StringComparison.Ordinal);
            Assert.Contains("[E1]", answer, StringComparison.Ordinal);
            Assert.Contains("model revision 12", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("deterministic project evidence takes precedence", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GroundingService_DoesNotDuplicateTraceWhenAnswerAlreadyUsesCitation()
        {
            var service = CreateService();
            var packet = service.Ground(BuildBlockedContext(), "Why is GA blocked?", 3);
            var answer = "GA is blocked because no trusted lift units exist [E1].";

            var result = service.AppendCitationTrace(answer, packet);

            Assert.Equal(answer, result);
        }

        [Fact]
        public void GroundingService_ReturnsEmptyPacketWithoutLiveContext()
        {
            var service = CreateService();

            var packet = service.Ground(null, "Why is optimization blocked?");

            Assert.False(packet.HasEvidence);
            Assert.Empty(packet.Citations);
            Assert.Equal("No relevant live project evidence retrieved", packet.CoverageLabel);
        }

        [Fact]
        public void UiContracts_ExposeGroundingEvidenceInDockAndFullCopilot()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);

            var companion = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var fullCopilot = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));
            var advisor = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "ViewModels", "AiAdvisorViewModel.cs"));

            Assert.Contains("GroundingEvidenceLabel", companion, StringComparison.Ordinal);
            Assert.Contains("GroundingCitations", companion, StringComparison.Ordinal);
            Assert.Contains("RAG Grounding", fullCopilot, StringComparison.Ordinal);
            Assert.Contains("PROJECT RAG GROUNDING PACKET", advisor, StringComparison.Ordinal);
        }

        private static AiProjectGroundingService CreateService()
        {
            return new AiProjectGroundingService(
                new AiWorkflowKnowledgeProjector(),
                new AiHybridKnowledgeRetriever(new AiDeterministicHashEmbeddingService()));
        }

        private static AiWorkflowContextSnapshot BuildBlockedContext()
        {
            return new AiWorkflowContextSnapshot
            {
                ProjectKey = "project:bahy",
                ProjectDisplayName = "Bahy",
                HasProject = true,
                CurrentProjectName = "Bahy",
                ModelDisplayName = "Bahy IFC",
                ModelRevision = 12,
                ModelFingerprint = "model:test-bahy:12",
                ElementCount = 362,
                ElementTableTotalCount = 337,
                ElementTableVisibleCount = 337,
                ZoneCount = 5,
                AllowedZoneCount = 3,
                ForbiddenZoneCount = 2,
                CraneCount = 0,
                LoadChartEvidenceCount = 0,
                HasLoadChartEvidence = false,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "GA blocked — no trusted lift units",
                        Summary = "IFC elements are loaded, but GA has zero trusted GA-ready grouped lift-unit candidates.",
                        Metrics = new[]
                        {
                            new AiContextMetricSnapshot { Name = "Trusted GA-ready lift units", Value = "0" },
                            new AiContextMetricSnapshot { Name = "Raw candidate-like elements", Value = "362" }
                        },
                        Facts = new Dictionary<string, string>
                        {
                            ["TrustedGroupedLiftUnits"] = "0",
                            ["EstimatedWeightBlockedCount"] = "362",
                            ["TopResolvedCategories"] = "Column (205), Slab (60), Beam (34)"
                        },
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "IFC is loaded; zones=5; cranes=0; 0 trusted GA-ready grouped lift units.",
                                Recommendation = "Do not bypass the optimizer gate. Review explicit BIM mass or weight quantities and select a mobile crane.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362,
                                Confidence = "High"
                            },
                            new AiContextIssueSnapshot
                            {
                                Code = "CraneMissing",
                                Severity = "Error",
                                Area = "Crane setup",
                                Evidence = "No mobile crane is selected.",
                                Recommendation = "Select a mobile crane and verify its active load chart.",
                                NextClick = "CRANES → MOBILE CRANE",
                                Confidence = "High"
                            }
                        },
                        ActionHints = new[]
                        {
                            new AiContextActionHintSnapshot
                            {
                                Label = "Open crane setup",
                                ActionType = "NavigateTab",
                                Target = "CRANES",
                                Reason = "A crane is required before manual inspection and optimization.",
                                Risk = "Low — navigation only."
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
