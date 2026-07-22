using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Agents.Readiness;
using CraneOptimizer.AI.Agents.Runtime;
using CraneOptimizer.AI.Agents.Tools;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge.Rag;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiProjectReadinessAgentTests
    {
        [Theory]
        [InlineData("Why is optimization blocked and what should I do next?")]
        [InlineData("Run a project readiness preflight.")]
        [InlineData("Can I run the optimizer now?")]
        [InlineData("Diagnose the critical issues in this workflow.")]
        public void CanHandle_ReadinessAndBlockerGoals(string question)
        {
            var service = CreateService(out _);

            Assert.True(service.CanHandle(question));
        }

        [Theory]
        [InlineData("How many BIM objects and zones are currently loaded?")]
        [InlineData("How many slabs are in the model?")]
        [InlineData("Write a friendly greeting.")]
        public void CanHandle_DoesNotHijackNarrowDataOrGeneralQuestions(string question)
        {
            var service = CreateService(out _);

            Assert.False(service.CanHandle(question));
        }

        [Fact]
        public void Registry_ExposesUniqueTypedReadOnlyTools()
        {
            var registry = CreateRegistry();

            Assert.Equal(7, registry.Definitions.Count);
            Assert.Equal(registry.Definitions.Count,
                registry.Definitions.Select(definition => definition.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(registry.Definitions, definition => Assert.Equal(AiToolAccessMode.ReadOnly, definition.AccessMode));
            Assert.Contains(registry.Definitions, definition => definition.Name == "get_project_overview");
            Assert.Contains(registry.Definitions, definition => definition.Name == "get_ga_readiness");
            Assert.Contains(registry.Definitions, definition => definition.Name == "search_project_evidence");
        }

        [Fact]
        public async Task RunAsync_ExecutesGovernedPlanAndExplainsTheBlocker()
        {
            var service = CreateService(out _);

            var run = await service.RunAsync(
                BuildBlockedContext(),
                "Why is optimization blocked, which evidence proves it, and what should I do next?",
                "HOME",
                "CRANES → MOBILE CRANE");

            Assert.Equal(AiAgentRunStatus.Succeeded, run.Status);
            Assert.InRange(run.Plan.Count, 6, 7);
            Assert.Equal(run.Plan.Count, run.Trace.Count);
            Assert.All(run.Trace, entry => Assert.Equal(AiAgentStepStatus.Succeeded, entry.Status));
            Assert.Contains("zero trusted GA-ready", run.Conclusion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("get_bim_readiness", run.VisibleAnswer, StringComparison.Ordinal);
            Assert.Contains("get_ga_readiness", run.VisibleAnswer, StringComparison.Ordinal);
            Assert.Contains("No IFC, crane, zone, GA, report, or project state was modified", run.VisibleAnswer, StringComparison.Ordinal);
            Assert.Contains(run.RecommendedActions, action => action.Contains("CRANES", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(run.RecommendedActions, action => action.Contains("trusted", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RunAsync_PreservesModelIdentityAndReturnsRagCitations()
        {
            var service = CreateService(out _);
            var context = BuildBlockedContext();

            var run = await service.RunAsync(context, "Assess project readiness and blockers.", "AI COPILOT", string.Empty);

            Assert.Equal(context.ModelRevision, run.ModelRevision);
            Assert.Equal(context.ModelFingerprint, run.ModelFingerprint);
            Assert.NotEmpty(run.Citations);
            Assert.Contains(run.Citations, citation => citation.Snippet.Contains("trusted GA-ready", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("[E1]", run.VisibleAnswer, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RunAsync_RecordsBoundedAuditSnapshot()
        {
            var service = CreateService(out var auditStore);

            var run = await service.RunAsync(BuildBlockedContext(), "What should I do next?", "HOME", string.Empty);
            var recent = auditStore.GetRecent();

            Assert.Single(recent);
            Assert.Equal(run.RunId, recent[0].RunId);
            Assert.Equal("Project Readiness Agent", recent[0].AgentName);
            Assert.True(recent[0].CompletedAt >= recent[0].StartedAt);
        }

        [Fact]
        public void ApprovalPolicy_BlocksMutatingToolsUntilExplicitApproval()
        {
            var policy = new DefaultAiAgentApprovalPolicy();
            var decision = policy.Evaluate(new AiToolDefinition
            {
                Name = "apply_ga_settings",
                Description = "Mutates GA settings.",
                AccessMode = AiToolAccessMode.Mutating,
                PermissionLevel = "ProjectWrite"
            });

            Assert.False(decision.CanExecute);
            Assert.True(decision.RequiresApproval);
            Assert.Contains("explicit user approval", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UiSurfaces_ExposeAgentRunActionAndAuditTrace()
        {
            var root = FindRepositoryRoot();
            var companion = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var advisor = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));
            var viewModel = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "ViewModels", "AiAdvisorViewModel.cs"));

            Assert.Contains("AgentExecutionTrace", companion, StringComparison.Ordinal);
            Assert.Contains("Agent Run", advisor, StringComparison.Ordinal);
            Assert.Contains("Run Agent", viewModel, StringComparison.Ordinal);
            Assert.Contains("no model required", viewModel, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RunAsync_NoProjectProducesSafeFirstAction()
        {
            var service = CreateService(out _);
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = false,
                ProjectKey = "global",
                ProjectDisplayName = "Global",
                ModelRevision = 0,
                ModelFingerprint = "model:none",
                OptimizationResult = new AiOptimizationResultSnapshot()
            };

            var run = await service.RunAsync(context, "Is the project ready?", "HOME", string.Empty);

            Assert.Contains("no active project", run.Conclusion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(run.RecommendedActions, action => action.Contains("Open or create a project", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(run.Trace, entry => entry.Status == AiAgentStepStatus.ApprovalRequired);
        }

        private static ProjectReadinessAgentService CreateService(out InMemoryAiAgentAuditStore auditStore)
        {
            auditStore = new InMemoryAiAgentAuditStore();
            return new ProjectReadinessAgentService(
                CreateRegistry(),
                new DefaultAiAgentApprovalPolicy(),
                auditStore,
                new WorkflowReadinessService());
        }

        private static AiToolRegistry CreateRegistry()
        {
            var grounding = new AiProjectGroundingService(
                new AiWorkflowKnowledgeProjector(),
                new AiHybridKnowledgeRetriever(new AiDeterministicHashEmbeddingService()));

            IAiProjectTool[] tools =
            {
                new GetProjectOverviewTool(),
                new GetBimReadinessTool(),
                new GetZoneSummaryTool(),
                new GetCraneSetupTool(),
                new GetGaReadinessTool(),
                new GetCriticalIssuesTool(),
                new SearchProjectEvidenceTool(grounding)
            };

            return new AiToolRegistry(tools);
        }

        private static string FindRepositoryRoot()
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

            throw new InvalidOperationException("Could not locate CraneOptimizer.sln.");
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
                ModelRevision = 7,
                ModelFingerprint = "model:rev:7:project:bahy:elements:362",
                ElementCount = 362,
                ElementTableTotalCount = 337,
                ElementTableVisibleCount = 337,
                ZoneCount = 5,
                AllowedZoneCount = 3,
                ForbiddenZoneCount = 2,
                AllowedZoneArea = 1271.049,
                ForbiddenZoneArea = 523.729,
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
                        Facts = new Dictionary<string, string>
                        {
                            [AiWorkflowFactKeys.ReadinessElementCount] = "361",
                            [AiWorkflowFactKeys.ReferenceGeometryExcludedCount] = "1",
                            [AiWorkflowFactKeys.TrustedGroupedLiftUnits] = "0",
                            [AiWorkflowFactKeys.EstimatedWeightBlockedCount] = "327",
                            [AiWorkflowFactKeys.MissingTrustedWeightCount] = "327"
                        },
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "IFC is loaded; zones=5; cranes=0; 0 trusted GA-ready grouped lift units.",
                                Recommendation = "Do not bypass the optimizer gate. Review explicit BIM mass or weight quantities.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 327,
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
                        }
                    }
                }
            };
        }
    }
}
