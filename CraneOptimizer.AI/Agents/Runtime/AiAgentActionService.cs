using System.Collections.Concurrent;
using System.Diagnostics;
using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class AiAgentActionService : IAiAgentActionService
    {
        private readonly IAiAgentActionToolRegistry _registry;
        private readonly IAiAgentApprovalPolicy _approvalPolicy;
        private readonly IAiAgentActionAuditStore _auditStore;
        private readonly ConcurrentDictionary<string, byte> _consumedOneTimeApprovals = new(StringComparer.Ordinal);

        public AiAgentActionService(
            IAiAgentActionToolRegistry registry,
            IAiAgentApprovalPolicy approvalPolicy,
            IAiAgentActionAuditStore auditStore)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _approvalPolicy = approvalPolicy ?? throw new ArgumentNullException(nameof(approvalPolicy));
            _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        }

        public IReadOnlyList<AiAgentActionProposal> BuildProposals(AiAgentRunSnapshot run)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (string.IsNullOrWhiteSpace(run.RunId) || !run.HasTrace)
                return Array.Empty<AiAgentActionProposal>();

            var requested = new List<string>();
            var corpus = string.Join("\n", new[]
            {
                run.Goal,
                run.Conclusion,
                string.Join("\n", run.RecommendedActions)
            }).ToLowerInvariant();

            var gaResult = FindResult(run, "get_ga_readiness");
            var missingCorePrerequisites = ReadInt(gaResult, "MissingCorePrerequisiteCount");
            var corePrerequisitesComplete = ReadBool(gaResult, "CorePrerequisitesComplete");
            var advisoryNoCandidate = ReadBool(gaResult, "HasAdvisoryNoCandidateSignal");
            var candidateConflict = ReadBool(gaResult, "CandidateEvidenceConflict");
            var hasOptimizationResult = ReadBool(gaResult, "HasOptimizationResult");

            if (gaResult != null && missingCorePrerequisites > 0)
                requested.Add("prepare_readiness_remediation_draft");

            var craneResult = FindResult(run, "get_crane_setup");
            var craneCount = ReadInt(craneResult, "CraneCount");
            var hasLoadChart = ReadBool(craneResult, "HasCurrentLoadChartEvidence")
                               || ReadBool(craneResult, "HasLoadChartEvidence");
            if (craneResult != null && craneCount == 0)
                requested.Add("open_crane_setup");
            if (craneResult != null && craneCount > 0 && !hasLoadChart)
                requested.Add("open_load_chart_import");

            var zoneResult = FindResult(run, "get_zone_summary");
            var zoneCount = ReadInt(zoneResult, "ZoneCount");
            if (zoneResult != null && zoneCount == 0)
                requested.Add("open_zone_manager");

            var asksForElementEvidence = corpus.Contains("weight", StringComparison.Ordinal)
                                         || corpus.Contains("mass", StringComparison.Ordinal)
                                         || corpus.Contains("element export", StringComparison.Ordinal)
                                         || corpus.Contains("bim exporter", StringComparison.Ordinal);
            if (asksForElementEvidence && advisoryNoCandidate && !candidateConflict)
                requested.Add("open_element_export");

            var goal = (run.Goal ?? string.Empty).ToLowerInvariant();
            var asksExplicitRunEligibility = goal.Contains("can i run", StringComparison.Ordinal)
                                             || goal.Contains("can we run", StringComparison.Ordinal)
                                             || goal.Contains("can i rerun", StringComparison.Ordinal)
                                             || goal.Contains("can we rerun", StringComparison.Ordinal)
                                             || goal.Contains("run the optimizer now", StringComparison.Ordinal)
                                             || goal.Contains("run ga now", StringComparison.Ordinal);
            var asksAboutOptimizer = corpus.Contains("optimizer", StringComparison.Ordinal)
                                     || corpus.Contains("optimization", StringComparison.Ordinal)
                                     || corpus.Contains("ga ", StringComparison.Ordinal)
                                     || asksExplicitRunEligibility;
            if (gaResult != null
                && corePrerequisitesComplete
                && (asksExplicitRunEligibility || (!hasOptimizationResult && asksAboutOptimizer)))
            {
                requested.Add("open_ga_readiness");
            }

            if (corpus.Contains("center of gravity", StringComparison.Ordinal)
                || corpus.Contains(" cog ", StringComparison.Ordinal)
                || corpus.Contains("assembly mark", StringComparison.Ordinal)
                || corpus.Contains("elementidentity", StringComparison.Ordinal))
            {
                requested.Add("open_element_management");
            }

            var proposals = new List<AiAgentActionProposal>();
            foreach (var toolName in requested.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_registry.TryGet(toolName, out var tool) || tool == null)
                    continue;

                var definition = tool.Definition;
                proposals.Add(new AiAgentActionProposal
                {
                    RunId = run.RunId,
                    ToolName = definition.Name,
                    Title = definition.Title,
                    Description = definition.Description,
                    ActionType = definition.ActionType,
                    Target = definition.Target,
                    ButtonLabel = definition.ButtonLabel,
                    Risk = definition.Risk,
                    Impact = definition.Impact,
                    Reversibility = definition.Reversibility,
                    InputJson = BuildProposalInput(run, definition.Name),
                    AccessMode = definition.AccessMode,
                    PermissionLevel = definition.PermissionLevel,
                    EvidenceIds = SelectEvidenceIds(run, definition.Name),
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint
                });
            }

            return proposals.Take(7).ToArray();
        }

        public async Task<AiAgentActionExecutionSnapshot> ExecuteAsync(
            AiAgentActionProposal proposal,
            AiAgentRunSnapshot run,
            bool userApproved,
            string initiatedBy = "User",
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ArgumentNullException.ThrowIfNull(run);

            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var userInitiated = !string.IsNullOrWhiteSpace(initiatedBy);

            if (!ProposalMatchesRun(proposal, run, out var proposalValidationError))
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = false,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = proposalValidationError,
                    Governance = "Stale or mismatched proposals are never executed."
                });
            }

            if (!_registry.TryGet(proposal.ToolName, out var tool) || tool == null)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = "The requested action tool is not registered.",
                    Governance = "Unregistered action tools are never invoked."
                });
            }

            if (!ProposalMatchesDefinition(proposal, tool.Definition, out var definitionValidationError))
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = false,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = definitionValidationError,
                    Governance = "Action metadata cannot be downgraded or altered after the proposal is created."
                });
            }

            if (tool.Definition.AccessMode == AiToolAccessMode.Mutating && !userInitiated)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.ApprovalRequired,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = false,
                    ExplicitApproval = false,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = "A named user must explicitly approve this mutating action.",
                    Governance = "Background or anonymous approval is not accepted. No change was made."
                });
            }

            var approval = _approvalPolicy.Evaluate(tool.Definition, userApproved);
            if (!approval.CanExecute)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = approval.RequiresApproval
                        ? AiAgentActionExecutionStatus.ApprovalRequired
                        : AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = approval.Reason,
                    Governance = "No application or project action was executed."
                });
            }

            if (tool.Definition.AccessMode == AiToolAccessMode.Mutating
                && !_consumedOneTimeApprovals.TryAdd(proposal.ProposalId, 0))
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Rejected,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = "The one-time approval for this proposal has already been consumed.",
                    Governance = "Mutating action approvals cannot be replayed. No additional change was made."
                });
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(tool.Definition.Timeout);
                var result = await tool.ExecuteAsync(new AiAgentActionContext
                {
                    AgentRun = run,
                    Proposal = proposal,
                    UserApproved = userApproved,
                    InitiatedBy = initiatedBy
                }, timeoutCts.Token).ConfigureAwait(false);

                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = result.Succeeded
                        ? AiAgentActionExecutionStatus.Succeeded
                        : AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = result.Succeeded ? result.Summary : result.Error,
                    Governance = BuildSuccessGovernance(tool.Definition, approval.Reason, result),
                    ArtifactPath = result.ArtifactPath,
                    ArtifactKind = result.ArtifactKind,
                    ArtifactSha256 = result.ArtifactSha256,
                    ArtifactSizeBytes = result.ArtifactSizeBytes
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = $"The action timed out after {tool.Definition.Timeout.TotalSeconds:0.#} seconds.",
                    Governance = "Timed-out actions are cancelled and cannot continue in the background."
                });
            }
            catch (OperationCanceledException)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Cancelled,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = "The action was cancelled.",
                    Governance = "No application or project mutation was performed."
                });
            }
            catch (Exception ex)
            {
                return Record(new AiAgentActionExecutionSnapshot
                {
                    RunId = run.RunId,
                    ProposalId = proposal.ProposalId,
                    ToolName = proposal.ToolName,
                    ActionType = proposal.ActionType,
                    Target = proposal.Target,
                    Status = AiAgentActionExecutionStatus.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    UserInitiated = userInitiated,
                    ExplicitApproval = userApproved,
                    InitiatedBy = initiatedBy,
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint,
                    EvidenceIds = proposal.EvidenceIds,
                    Summary = ex.Message,
                    Governance = "The failure was isolated to this user-invoked action."
                });
            }
        }

        public AiAgentActionExecutionSnapshot Reject(
            AiAgentActionProposal proposal,
            AiAgentRunSnapshot run,
            string initiatedBy = "User")
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ArgumentNullException.ThrowIfNull(run);

            if (proposal.RequiresExplicitApproval && !string.IsNullOrWhiteSpace(proposal.ProposalId))
                _consumedOneTimeApprovals.TryAdd(proposal.ProposalId, 0);

            return Record(new AiAgentActionExecutionSnapshot
            {
                RunId = run.RunId,
                ProposalId = proposal.ProposalId,
                ToolName = proposal.ToolName,
                ActionType = proposal.ActionType,
                Target = proposal.Target,
                Status = AiAgentActionExecutionStatus.Rejected,
                UserInitiated = true,
                ExplicitApproval = false,
                InitiatedBy = initiatedBy,
                ModelRevision = run.ModelRevision,
                ModelFingerprint = run.ModelFingerprint,
                EvidenceIds = proposal.EvidenceIds,
                Summary = "The user rejected the proposed action.",
                Governance = "The proposal was dismissed and no application or project action was executed."
            });
        }

        private AiAgentActionExecutionSnapshot Record(AiAgentActionExecutionSnapshot execution)
        {
            _auditStore.Record(execution);
            return execution;
        }

        private static string BuildProposalInput(AiAgentRunSnapshot run, string toolName)
        {
            if (!toolName.Equals("prepare_readiness_remediation_draft", StringComparison.OrdinalIgnoreCase))
                return "{}";

            return "{\"format\":\"markdown\",\"scope\":\"readiness-remediation\",\"runId\":\""
                   + EscapeJson(run.RunId)
                   + "\",\"modelRevision\":"
                   + run.ModelRevision
                   + "}";
        }

        private static bool ProposalMatchesRun(
            AiAgentActionProposal proposal,
            AiAgentRunSnapshot run,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(proposal.ProposalId))
            {
                error = "The action proposal has no approval identity.";
                return false;
            }

            if (!string.Equals(proposal.RunId, run.RunId, StringComparison.Ordinal))
            {
                error = "The action proposal belongs to a different agent run.";
                return false;
            }

            if (proposal.ModelRevision != run.ModelRevision
                || !string.Equals(proposal.ModelFingerprint, run.ModelFingerprint, StringComparison.Ordinal))
            {
                error = "The action proposal is stale because the captured model revision or fingerprint has changed.";
                return false;
            }

            var validEvidenceIds = run.Citations
                .Select(citation => citation.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (proposal.EvidenceIds.Any(id => !validEvidenceIds.Contains(id)))
            {
                error = "The action proposal references evidence that is not part of the captured agent run.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ProposalMatchesDefinition(
            AiAgentActionProposal proposal,
            AiAgentActionToolDefinition definition,
            out string error)
        {
            if (proposal.AccessMode != definition.AccessMode
                || !string.Equals(proposal.PermissionLevel, definition.PermissionLevel, StringComparison.Ordinal)
                || !string.Equals(proposal.ActionType, definition.ActionType, StringComparison.Ordinal)
                || !string.Equals(proposal.Target, definition.Target, StringComparison.Ordinal))
            {
                error = "The action proposal metadata no longer matches the registered tool definition.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string BuildSuccessGovernance(
            AiAgentActionToolDefinition definition,
            string approvalReason,
            AiAgentActionToolResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Governance))
                return approvalReason + " " + result.Governance.Trim();

            return definition.AccessMode switch
            {
                AiToolAccessMode.Mutating => approvalReason +
                    " The approved change was limited to the declared action scope. No optimizer run was started.",
                AiToolAccessMode.Navigation => approvalReason +
                    " No crane-planning data was modified by this navigation action.",
                _ => approvalReason + " No project state was modified by this read-only action."
            };
        }

        private static string EscapeJson(string? value)
            => (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);

        private static AiToolExecutionResult? FindResult(AiAgentRunSnapshot run, string toolName)
            => run.ToolResults.FirstOrDefault(result => result.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        private static int ReadInt(AiToolExecutionResult? result, string key)
            => result != null
               && result.Facts.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
                ? parsed
                : 0;

        private static bool ReadBool(AiToolExecutionResult? result, string key)
            => result != null
               && result.Facts.TryGetValue(key, out var value)
               && bool.TryParse(value, out var parsed)
               && parsed;

        private static IReadOnlyList<string> SelectEvidenceIds(AiAgentRunSnapshot run, string toolName)
        {
            var terms = toolName switch
            {
                "prepare_readiness_remediation_draft" => new[] { "ga", "optimization", "weight", "crane", "readiness", "issue" },
                "open_crane_setup" => new[] { "crane", "load chart" },
                "open_load_chart_import" => new[] { "load chart", "capacity", "crane" },
                "open_element_export" => new[] { "weight", "trusted", "ga-ready" },
                "open_element_management" => new[] { "cog", "center of gravity", "assembly", "identity" },
                "open_zone_manager" => new[] { "zone", "placement" },
                "open_ga_readiness" => new[] { "ga", "optimization", "pre-run", "candidate" },
                _ => Array.Empty<string>()
            };

            var matched = run.Citations
                .Where(citation => terms.Any(term =>
                    citation.SourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || citation.Snippet.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(citation => citation.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (matched.Count == 0)
            {
                matched.AddRange(run.Citations
                    .Select(citation => citation.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2));
            }

            return matched;
        }
    }
}
