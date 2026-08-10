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
        public void Registry_ExposesSixNavigationToolsAndOneApprovalGatedDraftTool()
        {
            var registry = CreateRegistry(new CapturingUiContext());

            Assert.Equal(7, registry.Definitions.Count);
            Assert.Equal(6, registry.Definitions.Count(definition => definition.AccessMode == AiToolAccessMode.Navigation));
            Assert.Equal(1, registry.Definitions.Count(definition => definition.AccessMode == AiToolAccessMode.Mutating));
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_crane_setup");
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_element_export");
            Assert.Contains(registry.Definitions, definition => definition.Name == "open_ga_readiness");
            Assert.Contains(registry.Definitions, definition => definition.Name == "prepare_readiness_remediation_draft");
        }

        [Fact]
        public void BuildProposals_MapsReadinessFindingsToExecutableActions()
        {
            var service = CreateService(new CapturingUiContext(), out _);
            var proposals = service.BuildProposals(BuildBlockedRun());

            Assert.Equal("prepare_readiness_remediation_draft", proposals[0].ToolName);
            Assert.Contains(proposals, proposal => proposal.ToolName == "prepare_readiness_remediation_draft" && proposal.RequiresExplicitApproval);
            Assert.Contains(proposals, proposal => proposal.ToolName == "open_crane_setup");
            Assert.Contains(proposals, proposal => proposal.ToolName == "open_element_export");
            Assert.DoesNotContain(proposals, proposal => proposal.ToolName == "open_load_chart_import");
            Assert.DoesNotContain(proposals, proposal => proposal.ToolName == "open_ga_readiness");
            Assert.All(proposals, proposal => Assert.Equal(7, proposal.ModelRevision));
            Assert.All(proposals, proposal => Assert.NotEmpty(proposal.EvidenceIds));
        }

        [Fact]
        public void BuildProposals_ResultReviewNextStep_DoesNotReplaceLiftSummaryWithOptimizerNavigation()
        {
            var service = CreateService(new CapturingUiContext(), out _);

            var nextStepProposals = service.BuildProposals(BuildResultReviewRun("What should I do next from this screen?"));
            var rerunProposals = service.BuildProposals(BuildResultReviewRun("Can I run the optimizer now?"));

            Assert.DoesNotContain(nextStepProposals, proposal => proposal.ToolName == "open_ga_readiness");
            Assert.Contains(rerunProposals, proposal => proposal.ToolName == "open_ga_readiness");
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
            var proposal = service.BuildProposals(run).First(item => item.ToolName == "open_crane_setup");

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
            Assert.Contains("Review details", companion, StringComparison.Ordinal);
            Assert.Contains("ApprovalScope", advisor, StringComparison.Ordinal);
            Assert.Contains("ArtifactLabel", advisor, StringComparison.Ordinal);
            Assert.Contains("ToolArgumentsLabel", companion, StringComparison.Ordinal);
            Assert.Contains("AuditMetadataLabel", advisor, StringComparison.Ordinal);
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
                new PrepareReadinessRemediationDraftActionTool(new RecordingDraftArtifactStore()),
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
                Conclusion = "A crane is missing. AI-visible zero-candidate/weight evidence is advisory; optimizer eligibility is verified only after core prerequisites are complete.",
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
                    },
                    new AiToolExecutionResult
                    {
                        ToolName = "get_ga_readiness",
                        Succeeded = true,
                        Evidence = "Visible core prerequisites are incomplete; AI does not certify optimizer eligibility.",
                        Facts = new Dictionary<string, string>
                        {
                            ["MissingCorePrerequisiteCount"] = "2",
                            ["CorePrerequisitesComplete"] = "False",
                            ["HasAdvisoryNoCandidateSignal"] = "True",
                            ["CandidateEvidenceConflict"] = "False"
                        }
                    },
                    new AiToolExecutionResult
                    {
                        ToolName = "get_zone_summary",
                        Succeeded = true,
                        Facts = new Dictionary<string, string>
                        {
                            ["ZoneCount"] = "5"
                        }
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

        private static AiAgentRunSnapshot BuildResultReviewRun(string goal)
        {
            return new AiAgentRunSnapshot
            {
                RunId = "run-result-review-1",
                Goal = goal,
                Status = AiAgentRunStatus.Succeeded,
                ModelRevision = 8,
                ModelFingerprint = "model:rev:8:project:bahy",
                Conclusion = "The captured result needs review at 3.92%. Current rerun eligibility remains owned by the optimizer's built-in Current Run Readiness.",
                RecommendedActions = new[]
                {
                    "ANALYSIS → Lift Summary — inspect unresolved lifts and dominant failure modes."
                },
                Plan = new[]
                {
                    new AiAgentPlanStep { Sequence = 1, ToolName = "get_ga_readiness", Purpose = "Inspect AI-visible optimizer evidence." }
                },
                Trace = new[]
                {
                    new AiAgentTraceEntry
                    {
                        Sequence = 1,
                        ToolName = "get_ga_readiness",
                        Purpose = "Inspect AI-visible optimizer evidence.",
                        Status = AiAgentStepStatus.Succeeded,
                        Summary = "Captured result available; optimizer eligibility not exposed to AI."
                    }
                },
                ToolResults = new[]
                {
                    new AiToolExecutionResult
                    {
                        ToolName = "get_ga_readiness",
                        Succeeded = true,
                        Evidence = "Core prerequisites are complete; captured result 14/357; current optimizer eligibility requires the built-in preflight.",
                        Facts = new Dictionary<string, string>
                        {
                            ["MissingCorePrerequisiteCount"] = "0",
                            ["CorePrerequisitesComplete"] = "True",
                            ["HasAdvisoryNoCandidateSignal"] = "True",
                            ["CandidateEvidenceConflict"] = "True",
                            ["HasOptimizationResult"] = "True"
                        }
                    }
                },
                Citations = new[]
                {
                    new AiKnowledgeCitation
                    {
                        Id = "E1",
                        SourceLabel = "Captured optimization result",
                        Snippet = "14 of 357 evaluated lifts completed.",
                        ModelRevision = 8
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

        private sealed class RecordingDraftArtifactStore : IAiAgentDraftArtifactStore
        {
            public Task<AiAgentDraftArtifactResult> WriteAsync(
                AiAgentDraftArtifactRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new AiAgentDraftArtifactResult
                {
                    Succeeded = true,
                    ArtifactPath = Path.Combine(Path.GetTempPath(), "readiness-remediation.md"),
                    ArtifactKind = "Markdown readiness-remediation draft",
                    SizeBytes = request.Content.Length,
                    Sha256 = "test-sha256"
                });
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
