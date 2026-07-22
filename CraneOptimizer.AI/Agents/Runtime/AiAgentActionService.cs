using System.Diagnostics;
using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class AiAgentActionService : IAiAgentActionService
    {
        private readonly IAiAgentActionToolRegistry _registry;
        private readonly IAiAgentApprovalPolicy _approvalPolicy;
        private readonly IAiAgentActionAuditStore _auditStore;

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
                run.Conclusion,
                string.Join("\n", run.RecommendedActions),
                string.Join("\n", run.ToolResults.Select(result => result.Evidence)),
                string.Join("\n", run.Citations.Select(citation => citation.SourceLabel + " " + citation.Snippet))
            }).ToLowerInvariant();

            var craneResult = FindResult(run, "get_crane_setup");
            var craneCount = ReadInt(craneResult, "CraneCount");
            var hasLoadChart = ReadBool(craneResult, "HasLoadChartEvidence");
            if (craneCount == 0 || corpus.Contains("no crane") || corpus.Contains("select the crane"))
                requested.Add("open_crane_setup");
            if (!hasLoadChart || corpus.Contains("load-chart") || corpus.Contains("load chart"))
                requested.Add("open_load_chart_import");
            if (corpus.Contains("trusted weight") || corpus.Contains("estimated weight") || corpus.Contains("ga-ready") || corpus.Contains("export element list"))
                requested.Add("open_element_export");
            if (corpus.Contains("center of gravity") || corpus.Contains(" cog ") || corpus.Contains("assembly mark") || corpus.Contains("elementidentity"))
                requested.Add("open_element_management");
            if (corpus.Contains("zone") && (corpus.Contains("missing") || corpus.Contains("review") || corpus.Contains("placement")))
                requested.Add("open_zone_manager");
            if (corpus.Contains("ga ") || corpus.Contains("optimizer") || corpus.Contains("optimization") || corpus.Contains("pre-run"))
                requested.Add("open_ga_readiness");

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
                    AccessMode = definition.AccessMode,
                    PermissionLevel = definition.PermissionLevel,
                    EvidenceIds = SelectEvidenceIds(run, definition.Name),
                    ModelRevision = run.ModelRevision,
                    ModelFingerprint = run.ModelFingerprint
                });
            }

            return proposals.Take(6).ToArray();
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
                    Governance = approval.Reason + " No crane-planning data was modified by this navigation action."
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
