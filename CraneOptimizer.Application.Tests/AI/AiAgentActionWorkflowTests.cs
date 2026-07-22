using CraneOptimizer.AI.Agents.Actions;
using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Agents.Runtime;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge.Rag;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiAgentActionWorkflowTests
    {
        [Fact]
        public void Registry_ExposesSixTypedNavigationTools()
        {
            var registry = CreateRegistry(new CapturingUiContext());

            Assert.Equal(6, registry.Definitions.Count);
            Assert.All(registry.Definitions, definition => Assert.Equal(AiToolAccessMode.Navigation, definition.AccessMode));
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_crane_setup");
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_element_export");
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_ga_readiness");
        }

        [Fact]
        public void BuildProposals_MapsReadinessFindingsToExecutableActions()
        {
            var service = CreateService(new CapturingUiContext(), out _);
            var proposals = service.BuildProposals(BuildBlockedRun());

            Assert.Contains(proposals, proposal => proposal.ToolName == "open_crane_setup");
            Assert.Contains(proposals, proposal => proposal.ToolName == "open_load_chart_import");
            Assert.Contains(proposals, proposal => proposal.ToolName == "open_element_export");
            Assert.Contains(proposals, proposal => proposal.ToolName == "open_ga_readiness");
            Assert.All(proposals, proposal => Assert.Equal(7, proposal.ModelRevision));
            Assert.All(proposals, proposal => Assert.NotEmpty(proposal.EvidenceIds));
        }

        [Fact]
        public async Task ExecuteAsync_UserInvokedNavigationRaisesUiActionAndRecordsAudit()
        {
            var ui = new CapturingUiContext();
            var service = CreateService(ui, out var auditStore);
            var run = BuildBlockedRun();
            var proposal = service.BuildProposals(run).First(item => item.ToolName == "open_crane_setup");

            var execution = await service.ExecuteAsync(proposal, run, userApproved: true, initiatedBy: "Test user");

            Assert.Equal(AiAgentActionExecutionStatus.Succeeded, execution.Status);
            Assert.Equal("CRANES", ui.LastTarget);
            Assert.Equal("NavigateTab", ui.LastActionType);
            Assert.True(execution.UserInitiated);
            Assert.True(execution.ExplicitApproval);
            Assert.Equal(run.RunId, execution.RunId);
            Assert.Equal(run.ModelFingerprint, execution.ModelFingerprint);
            Assert.Single(auditStore.GetRecent());
            Assert.Equal(execution.AuditId, auditStore.GetRecent()[0].AuditId);
        }

        [Fact]
        public async Task ExecuteAsync_NavigationWithoutUserInvocationIsBlocked()
        {
            var ui = new CapturingUiContext();
            var service = CreateService(ui, out var auditStore);
            var run = BuildBlockedRun();
            var proposal = service.BuildProposals(run).First(item => item.ToolName == "open_ga_readiness");

            var execution = await service.ExecuteAsync(proposal, run, userApproved: false, initiatedBy: string.Empty);

            Assert.Equal(AiAgentActionExecutionStatus.ApprovalRequired, execution.Status);
            Assert.Null(ui.LastTarget);
            Assert.Contains("user selects the action card", execution.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Single(auditStore.GetRecent());
        }

        [Fact]
        public async Task ExecuteAsync_MutatingActionIsBlockedUntilExplicitApproval()
        {
            var ui = new CapturingUiContext();
            var mutatingTool = new FakeMutatingActionTool();
            var registry = new AiAgentActionToolRegistry(CreateTools(ui).Append(mutatingTool));
            var auditStore = new InMemoryAiAgentActionAuditStore();
            var service = new AiAgentActionService(registry, new DefaultAiAgentApprovalPolicy(), auditStore);
            var run = BuildBlockedRun();
            var proposal = new AiAgentActionProposal
            {
                RunId = run.RunId,
                ToolName = mutatingTool.Definition.Name,
                Title = mutatingTool.Definition.Title,
                Description = mutatingTool.Definition.Description,
                ActionType = mutatingTool.Definition.ActionType,
                Target = mutatingTool.Definition.Target,
                ButtonLabel = mutatingTool.Definition.ButtonLabel,
                AccessMode = mutatingTool.Definition.AccessMode,
                PermissionLevel = mutatingTool.Definition.PermissionLevel,
                ModelRevision = run.ModelRevision,
                ModelFingerprint = run.ModelFingerprint,
                EvidenceIds = new[] { "E1" }
            };

            var blocked = await service.ExecuteAsync(proposal, run, userApproved: false);
            var approved = await service.ExecuteAsync(proposal, run, userApproved: true);

            Assert.Equal(AiAgentActionExecutionStatus.ApprovalRequired, blocked.Status);
            Assert.Equal(AiAgentActionExecutionStatus.Succeeded, approved.Status);
            Assert.Equal(1, mutatingTool.ExecutionCount);
            Assert.Equal(2, auditStore.GetRecent().Count);
        }

        [Fact]
        public void Reject_RecordsExplicitUserRejectionWithoutExecution()
        {
            var service = CreateService(new CapturingUiContext(), out var auditStore);
            var run = BuildBlockedRun();
            var proposal = service.BuildProposals(run).First();

            var execution = service.Reject(proposal, run, "Test user");

            Assert.Equal(AiAgentActionExecutionStatus.Rejected, execution.Status);
            Assert.Contains("rejected", execution.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Single(auditStore.GetRecent());
        }

        [Fact]
        public void UiSurfaces_ExposeExecutableActionCardsAndAuditStatus()
        {
            var root = FindRepositoryRoot();
            var companion = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var advisor = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));
            var viewModel = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "ViewModels", "AiAdvisorViewModel.cs"));

            Assert.Contains("AgentActionProposals", companion, StringComparison.Ordinal);
            Assert.Contains("ExecuteAgentActionCommand", companion, StringComparison.Ordinal);
            Assert.Contains("AgentActionAuditLabel", advisor, StringComparison.Ordinal);
            Assert.Contains("Recommended actions", viewModel, StringComparison.Ordinal);
            Assert.Contains("RejectAgentActionCommand", viewModel, StringComparison.Ordinal);
        }

        [Fact]
        public void CapturedAgentContext_UsesExplicitNoOpEventAccessors()
        {
            var root = FindRepositoryRoot();
            var source = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Agents", "Readiness", "ProjectReadinessAgentService.cs"));

            Assert.Contains("ContextChanged { add { } remove { } }", source, StringComparison.Ordinal);
            Assert.Contains("ActionRequested { add { } remove { } }", source, StringComparison.Ordinal);
        }

        private static AiAgentActionService CreateService(
            CapturingUiContext ui,
            out InMemoryAiAgentActionAuditStore auditStore)
        {
            auditStore = new InMemoryAiAgentActionAuditStore();
            return new AiAgentActionService(
                CreateRegistry(ui),
                new DefaultAiAgentApprovalPolicy(),
                auditStore);
        }

        private static AiAgentActionToolRegistry CreateRegistry(CapturingUiContext ui)
            => new(CreateTools(ui));

        private static IAiAgentActionTool[] CreateTools(CapturingUiContext ui) =>
            new IAiAgentActionTool[]
            {
                new OpenCraneSetupActionTool(ui),
                new OpenLoadChartImportActionTool(ui),
                new OpenElementExportActionTool(ui),
                new OpenElementManagementActionTool(ui),
                new OpenZoneManagerActionTool(ui),
                new OpenGaReadinessActionTool(ui)
            };

        private static AiAgentRunSnapshot BuildBlockedRun()
        {
            return new AiAgentRunSnapshot
            {
                RunId = "run-readiness-1",
                Status = AiAgentRunStatus.Succeeded,
                ModelRevision = 7,
                ModelFingerprint = "model:rev:7:project:bahy",
                Conclusion = "Optimization is blocked because there are zero trusted GA-ready lift units, no crane is selected, and load-chart evidence is missing.",
                RecommendedActions = new[]
                {
                    "CRANES → MOBILE CRANE — Add or select a mobile crane.",
                    "CRANES → Import Load Chart — Verify manufacturer load-chart evidence.",
                    "IFC MODEL → Export Element List — Review trusted weight sources."
                },
                Plan = new[]
                {
                    new AiAgentPlanStep { Sequence = 1, ToolName = "get_crane_setup", Purpose = "Inspect crane setup." }
                },
                Trace = new[]
                {
                    new AiAgentTraceEntry
                    {
                        Sequence = 1,
                        ToolName = "get_crane_setup",
                        Purpose = "Inspect crane setup.",
                        Status = AiAgentStepStatus.Succeeded,
                        Summary = "No crane is selected."
                    }
                },
                ToolResults = new[]
                {
                    new AiToolExecutionResult
                    {
                        ToolName = "get_crane_setup",
                        Succeeded = true,
                        Evidence = "Crane instances: 0; load-chart evidence available: False.",
                        Facts = new Dictionary<string, string>
                        {
                            ["CraneCount"] = "0",
                            ["HasLoadChartEvidence"] = "False"
                        }
                    },
                    new AiToolExecutionResult
                    {
                        ToolName = "get_bim_readiness",
                        Succeeded = true,
                        Evidence = "Zero trusted GA-ready lift units; 327 estimated weights are blocked."
                    }
                },
                Citations = new[]
                {
                    new AiKnowledgeCitation
                    {
                        Id = "E1",
                        SourceLabel = "GA readiness",
                        Snippet = "Zero trusted GA-ready grouped lift units are available.",
                        ModelRevision = 7
                    },
                    new AiKnowledgeCitation
                    {
                        Id = "E2",
                        SourceLabel = "Crane setup",
                        Snippet = "No crane is selected and no load chart is available.",
                        ModelRevision = 7
                    },
                    new AiKnowledgeCitation
                    {
                        Id = "E3",
                        SourceLabel = "IFC weight evidence",
                        Snippet = "Estimated weight is blocked from GA.",
                        ModelRevision = 7
                    }
                }
            };
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

        private sealed class CapturingUiContext : IUiInteractionContextService
        {
            public event EventHandler? ContextChanged { add { } remove { } }
            public event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested;
            public string ActiveTabName => "HOME";
            public string ActivePanelName => "Home";
            public DateTimeOffset LastUpdatedAt => DateTimeOffset.UtcNow;
            public string? LastActionType { get; private set; }
            public string? LastTarget { get; private set; }
            public string? LastReason { get; private set; }

            public CapturingUiContext()
            {
                ActionRequested += (_, args) =>
                {
                    LastActionType = args.ActionType;
                    LastTarget = args.Target;
                    LastReason = args.Reason;
                };
            }

            public void SetActiveTab(string tabName, string panelName) { }

            public void RequestTabNavigation(string tabName, string reason)
            {
                ActionRequested?.Invoke(this, new AiUiActionRequestedEventArgs("NavigateTab", tabName, reason));
            }
        }

        private sealed class FakeMutatingActionTool : IAiAgentActionTool
        {
            public int ExecutionCount { get; private set; }

            public AiAgentActionToolDefinition Definition { get; } = new()
            {
                Name = "prepare_ga_settings",
                Title = "Prepare GA settings",
                Description = "Mutates draft GA settings.",
                ActionType = "PrepareGaSettings",
                Target = "ANALYZE",
                ButtonLabel = "Approve once",
                AccessMode = AiToolAccessMode.Mutating,
                PermissionLevel = "ProjectWrite",
                Risk = "Medium — changes draft optimizer settings."
            };

            public Task<AiAgentActionToolResult> ExecuteAsync(
                AiAgentActionContext context,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExecutionCount++;
                return Task.FromResult(new AiAgentActionToolResult
                {
                    Succeeded = true,
                    Summary = "Draft GA settings prepared."
                });
            }
        }
    }
}
