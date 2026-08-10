using CraneOptimizer.AI.Agents.Actions;
using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Agents.Runtime;
using CraneOptimizer.AI.Knowledge.Rag;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiApprovalGatedDraftArtifactTests
    {
        [Fact]
        public void BuildProposals_PutsApprovalGatedDraftBeforeNavigationActions()
        {
            using var fixture = new DraftFixture();
            var proposals = fixture.Service.BuildProposals(BuildRun());

            var proposal = Assert.Single(proposals);
            Assert.Equal("prepare_readiness_remediation_draft", proposal.ToolName);
            Assert.Equal(AiToolAccessMode.Mutating, proposal.AccessMode);
            Assert.True(proposal.RequiresExplicitApproval);
            Assert.Equal("Approve once", proposal.ApprovalLabel);
            Assert.Contains("one-time approval", proposal.ApprovalScope, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reversible", proposal.Reversibility, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(proposal.EvidenceIds);
        }

        [Fact]
        public void BuildProposals_ReadyRunDoesNotProposeMutatingDraft()
        {
            using var fixture = new DraftFixture();

            var proposals = fixture.Service.BuildProposals(BuildReadyRun());

            Assert.Empty(proposals);
        }

        [Fact]
        public async Task ExecuteAsync_WithoutApprovalDoesNotCreateArtifact()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var proposal = Assert.Single(fixture.Service.BuildProposals(run));

            var execution = await fixture.Service.ExecuteAsync(
                proposal,
                run,
                userApproved: false,
                initiatedBy: "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.ApprovalRequired, execution.Status);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*", SearchOption.AllDirectories));
            Assert.Single(fixture.AuditStore.GetRecent());
        }

        [Fact]
        public async Task ExecuteAsync_ApprovedDraftCreatesReversibleMarkdownArtifact()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var proposal = Assert.Single(fixture.Service.BuildProposals(run));

            var execution = await fixture.Service.ExecuteAsync(
                proposal,
                run,
                userApproved: true,
                initiatedBy: "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.Succeeded, execution.Status);
            Assert.True(execution.ExplicitApproval);
            Assert.True(execution.HasArtifact);
            Assert.True(execution.ArtifactPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(execution.ArtifactPath));
            Assert.Equal(64, execution.ArtifactSha256.Length);
            Assert.True(execution.ArtifactSizeBytes > 0);
            Assert.Contains("Only a local draft artifact was created", execution.Governance, StringComparison.OrdinalIgnoreCase);

            var markdown = await File.ReadAllTextAsync(execution.ArtifactPath);
            Assert.Contains("# Project Readiness Remediation Draft", markdown, StringComparison.Ordinal);
            Assert.Contains("Project: Bahy", markdown, StringComparison.Ordinal);
            Assert.Contains("Current Run Readiness", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[E1]", markdown, StringComparison.Ordinal);
            Assert.Contains("explicit one-time user approval", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("start optimization", markdown, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_OneTimeApprovalCannotBeReplayed()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var proposal = Assert.Single(fixture.Service.BuildProposals(run));

            var first = await fixture.Service.ExecuteAsync(proposal, run, true, "Test engineer");
            var replay = await fixture.Service.ExecuteAsync(proposal, run, true, "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.Succeeded, first.Status);
            Assert.Equal(AiAgentActionExecutionStatus.Rejected, replay.Status);
            Assert.Contains("already been consumed", replay.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Single(Directory.GetFiles(fixture.RootDirectory, "*.md", SearchOption.AllDirectories));
            Assert.Equal(2, fixture.AuditStore.GetRecent().Count);
        }

        [Fact]
        public async Task Reject_InvalidatesMutatingProposalAndPreventsLaterExecution()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var proposal = Assert.Single(fixture.Service.BuildProposals(run));

            var rejected = fixture.Service.Reject(proposal, run, "Test engineer");
            var replay = await fixture.Service.ExecuteAsync(proposal, run, true, "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.Rejected, rejected.Status);
            Assert.Equal(AiAgentActionExecutionStatus.Rejected, replay.Status);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task ExecuteAsync_AnonymousMutatingApprovalIsBlocked()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var proposal = Assert.Single(fixture.Service.BuildProposals(run));

            var execution = await fixture.Service.ExecuteAsync(
                proposal,
                run,
                userApproved: true,
                initiatedBy: string.Empty);

            Assert.Equal(AiAgentActionExecutionStatus.ApprovalRequired, execution.Status);
            Assert.Contains("named user", execution.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task ExecuteAsync_StaleModelRevisionIsBlockedBeforeWriting()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var original = Assert.Single(fixture.Service.BuildProposals(run));
            var stale = CopyProposal(original, modelRevision: run.ModelRevision - 1);

            var execution = await fixture.Service.ExecuteAsync(stale, run, true, "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.Failed, execution.Status);
            Assert.Contains("stale", execution.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task ExecuteAsync_DowngradedAccessMetadataIsBlockedBeforeWriting()
        {
            using var fixture = new DraftFixture();
            var run = BuildRun();
            var original = Assert.Single(fixture.Service.BuildProposals(run));
            var downgraded = CopyProposal(original, accessMode: AiToolAccessMode.Navigation);

            var execution = await fixture.Service.ExecuteAsync(downgraded, run, true, "Test engineer");

            Assert.Equal(AiAgentActionExecutionStatus.Failed, execution.Status);
            Assert.Contains("metadata", execution.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task FileSystemStore_SanitizesNamesAndUsesAtomicFinalFile()
        {
            using var fixture = new DraftFixture();
            var store = new FileSystemAiAgentDraftArtifactStore(fixture.RootDirectory);

            var result = await store.WriteAsync(new AiAgentDraftArtifactRequest
            {
                RunId = "run/unsafe",
                ProjectName = "Bahy/Project",
                FileStem = "readiness/remediation",
                Content = "# Draft\n\nEvidence-backed content."
            });

            Assert.True(result.Succeeded, result.Error);
            Assert.True(File.Exists(result.ArtifactPath));
            Assert.Equal(fixture.RootDirectory, Path.GetDirectoryName(result.ArtifactPath));
            Assert.DoesNotContain(".tmp-", Path.GetFileName(result.ArtifactPath), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetFiles(fixture.RootDirectory, "*.tmp-*", SearchOption.AllDirectories));
        }

        [Fact]
        public void DependencyInjection_RegistersDraftStoreAndApprovalGatedTool()
        {
            var root = FindRepositoryRoot();
            var source = File.ReadAllText(Path.Combine(
                root,
                "CraneOptimizer.AI",
                "DependencyInjection",
                "CraneOptimizerAiServiceCollectionExtensions.cs"));

            Assert.Contains("IAiAgentDraftArtifactStore, FileSystemAiAgentDraftArtifactStore", source, StringComparison.Ordinal);
            Assert.Contains("PrepareReadinessRemediationDraftActionTool", source, StringComparison.Ordinal);
        }

        private static AiAgentActionProposal CopyProposal(
            AiAgentActionProposal source,
            long? modelRevision = null,
            AiToolAccessMode? accessMode = null)
            => new()
            {
                ProposalId = source.ProposalId,
                RunId = source.RunId,
                ToolName = source.ToolName,
                Title = source.Title,
                Description = source.Description,
                ActionType = source.ActionType,
                Target = source.Target,
                ButtonLabel = source.ButtonLabel,
                Risk = source.Risk,
                Impact = source.Impact,
                Reversibility = source.Reversibility,
                InputJson = source.InputJson,
                AccessMode = accessMode ?? source.AccessMode,
                PermissionLevel = source.PermissionLevel,
                EvidenceIds = source.EvidenceIds,
                ModelRevision = modelRevision ?? source.ModelRevision,
                ModelFingerprint = source.ModelFingerprint
            };

        private static AiAgentRunSnapshot BuildReadyRun()
            => new()
            {
                RunId = "run-ready-1",
                AgentName = "Project Readiness Agent",
                Goal = "Is the project ready?",
                Status = AiAgentRunStatus.Succeeded,
                ModelRevision = 9,
                ModelFingerprint = "model:rev:9:project:ready",
                Conclusion = "The deterministic project snapshot satisfies the core readiness prerequisites.",
                RecommendedActions = Array.Empty<string>(),
                Plan = new[]
                {
                    new AiAgentPlanStep { Sequence = 1, ToolName = "get_project_overview", Purpose = "Capture context." }
                },
                Trace = new[]
                {
                    new AiAgentTraceEntry
                    {
                        Sequence = 1,
                        ToolName = "get_project_overview",
                        Purpose = "Capture context.",
                        Status = AiAgentStepStatus.Succeeded,
                        Summary = "Project ready."
                    }
                },
                ToolResults = new[]
                {
                    new AiToolExecutionResult
                    {
                        ToolName = "get_crane_setup",
                        Succeeded = true,
                        Summary = "Crane and load chart are available.",
                        Facts = new Dictionary<string, string>
                        {
                            ["CraneCount"] = "1",
                            ["HasLoadChartEvidence"] = "True"
                        }
                    }
                },
                Citations = new[]
                {
                    new AiKnowledgeCitation
                    {
                        Id = "E1",
                        SourceLabel = "Project overview",
                        Snippet = "Core prerequisites are satisfied.",
                        ModelRevision = 9
                    }
                }
            };

        private static AiAgentRunSnapshot BuildRun()
            => new()
            {
                RunId = "run-readiness-draft-1",
                AgentName = "Project Readiness Agent",
                Goal = "Which visible prerequisite is missing before I verify optimizer readiness?",
                Status = AiAgentRunStatus.Succeeded,
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                CompletedAt = DateTimeOffset.UtcNow,
                ModelRevision = 7,
                ModelFingerprint = "model:rev:7:project:bahy",
                Conclusion = "A visible project prerequisite is missing. After fixing it, use the optimizer built-in Current Run Readiness for the run decision.",
                GovernanceSummary = "All diagnostic tools were read-only and no project state was modified.",
                RecommendedActions = new[]
                {
                    "CRANES → MOBILE CRANE — Add or select a mobile crane.",
                    "ANALYSIS → Run Optimizer — use Current Run Readiness after the visible prerequisites are complete."
                },
                Plan = new[]
                {
                    new AiAgentPlanStep
                    {
                        Sequence = 1,
                        ToolName = "get_project_overview",
                        Purpose = "Capture project context."
                    }
                },
                Trace = new[]
                {
                    new AiAgentTraceEntry
                    {
                        Sequence = 1,
                        ToolName = "get_project_overview",
                        Purpose = "Capture project context.",
                        Status = AiAgentStepStatus.Succeeded,
                        Summary = "Active project context captured."
                    }
                },
                ToolResults = new[]
                {
                    new AiToolExecutionResult
                    {
                        ToolName = "get_project_overview",
                        Succeeded = true,
                        Summary = "Active project context captured.",
                        Evidence = "Project: Bahy; model revision 7; 362 BIM/model objects.",
                        Facts = new Dictionary<string, string>
                        {
                            ["Project"] = "Bahy",
                            ["ModelRevision"] = "7",
                            ["BimObjects"] = "362"
                        }
                    },
                    new AiToolExecutionResult
                    {
                        ToolName = "get_bim_readiness",
                        Succeeded = true,
                        Summary = "Trusted GA-ready lift-unit evidence is not available.",
                        Evidence = "Zero trusted GA-ready grouped lift units; 327 estimated weights are blocked.",
                        Facts = new Dictionary<string, string>
                        {
                            ["TrustedGaLiftUnits"] = "0",
                            ["EstimatedWeightBlocked"] = "327"
                        }
                    },
                    new AiToolExecutionResult
                    {
                        ToolName = "get_ga_readiness",
                        Succeeded = true,
                        Summary = "Visible project prerequisites are incomplete; optimizer eligibility is not certified by AI.",
                        Evidence = "One or more core prerequisites are missing. Current Run Readiness remains optimizer-owned.",
                        Facts = new Dictionary<string, string>
                        {
                            ["MissingCorePrerequisiteCount"] = "2",
                            ["CorePrerequisitesComplete"] = "False",
                            ["HasAdvisoryNoCandidateSignal"] = "True",
                            ["CandidateEvidenceConflict"] = "False"
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
                        SourceLabel = "IFC weight evidence",
                        Snippet = "Estimated fallback weight is blocked from GA.",
                        ModelRevision = 7
                    }
                }
            };

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

        private sealed class DraftFixture : IDisposable
        {
            public DraftFixture()
            {
                RootDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "CraneOptimizer-AI-draft-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootDirectory);

                AuditStore = new InMemoryAiAgentActionAuditStore();
                var registry = new AiAgentActionToolRegistry(new IAiAgentActionTool[]
                {
                    new PrepareReadinessRemediationDraftActionTool(
                        new FileSystemAiAgentDraftArtifactStore(RootDirectory))
                });
                Service = new AiAgentActionService(
                    registry,
                    new DefaultAiAgentApprovalPolicy(),
                    AuditStore);
            }

            public string RootDirectory { get; }
            public InMemoryAiAgentActionAuditStore AuditStore { get; }
            public AiAgentActionService Service { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootDirectory))
                        Directory.Delete(RootDirectory, recursive: true);
                }
                catch
                {
                    // Test cleanup only.
                }
            }
        }
    }
}
