using System.Diagnostics;
using System.Text;
using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge.Rag;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Agents.Readiness
{
    /// <summary>
    /// Deterministic, governed project-readiness agent. It plans and executes only
    /// typed AI-owned tools against an immutable host snapshot. No tool in this first
    /// vertical slice may mutate crane-planning state.
    /// </summary>
    public sealed class ProjectReadinessAgentService : IProjectReadinessAgentService
    {
        private static readonly string[] ReadinessTerms =
        {
            "ready", "readiness", "blocked", "blocker", "what should i do next",
            "next step", "can i run", "can we run", "preflight", "prerequisite",
            "critical issue", "missing", "why is optimization", "why is ga",
            "project status", "workflow status", "diagnose", "troubleshoot"
        };

        private readonly IAiToolRegistry _toolRegistry;
        private readonly IAiAgentApprovalPolicy _approvalPolicy;
        private readonly IAiAgentAuditStore _auditStore;
        private readonly IWorkflowReadinessService _readinessService;

        public ProjectReadinessAgentService(
            IAiToolRegistry toolRegistry,
            IAiAgentApprovalPolicy approvalPolicy,
            IAiAgentAuditStore auditStore,
            IWorkflowReadinessService readinessService)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _approvalPolicy = approvalPolicy ?? throw new ArgumentNullException(nameof(approvalPolicy));
            _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
            _readinessService = readinessService ?? throw new ArgumentNullException(nameof(readinessService));
        }

        public bool CanHandle(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            var normalized = question.Trim().ToLowerInvariant();
            if (LooksLikeNarrowBimCountQuestion(normalized))
                return false;

            return ReadinessTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        }

        public async Task<AiAgentRunSnapshot> RunAsync(
            AiWorkflowContextSnapshot context,
            string question,
            string activeScreen,
            string nextAction,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var startedAt = DateTimeOffset.UtcNow;
            var normalizedQuestion = string.IsNullOrWhiteSpace(question)
                ? "Assess project readiness and identify the next safe action."
                : question.Trim();
            var normalizedScreen = string.IsNullOrWhiteSpace(activeScreen) ? "HOME" : activeScreen.Trim();
            var readiness = _readinessService.Evaluate(context, new CapturedAgentUiContext(normalizedScreen));
            var plan = BuildPlan(context, normalizedQuestion).Take(8).ToArray();
            var trace = new List<AiAgentTraceEntry>(plan.Length);
            var results = new List<AiToolExecutionResult>(plan.Length);

            try
            {
                foreach (var step in plan)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var stopwatch = Stopwatch.StartNew();

                    if (!_toolRegistry.TryGet(step.ToolName, out var tool) || tool == null)
                    {
                        trace.Add(new AiAgentTraceEntry
                        {
                            Sequence = step.Sequence,
                            ToolName = step.ToolName,
                            Purpose = step.Purpose,
                            Status = AiAgentStepStatus.Failed,
                            Summary = "The planned tool is not registered.",
                            Governance = "Execution stopped for this step because unregistered tools are never invoked.",
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                        continue;
                    }

                    var approval = _approvalPolicy.Evaluate(tool.Definition);
                    if (!approval.CanExecute)
                    {
                        trace.Add(new AiAgentTraceEntry
                        {
                            Sequence = step.Sequence,
                            ToolName = step.ToolName,
                            Purpose = step.Purpose,
                            Status = approval.RequiresApproval ? AiAgentStepStatus.ApprovalRequired : AiAgentStepStatus.Skipped,
                            Summary = approval.Reason,
                            Governance = "No project mutation was performed.",
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                        continue;
                    }

                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(tool.Definition.Timeout);

                        var result = await tool.ExecuteAsync(new AiAgentToolContext
                        {
                            Workflow = context,
                            Readiness = readiness,
                            Question = normalizedQuestion,
                            ActiveScreen = normalizedScreen,
                            NextAction = nextAction,
                            InputJson = step.InputJson
                        }, timeoutCts.Token).ConfigureAwait(false);

                        results.Add(result);
                        trace.Add(new AiAgentTraceEntry
                        {
                            Sequence = step.Sequence,
                            ToolName = step.ToolName,
                            Purpose = step.Purpose,
                            Status = result.Succeeded ? AiAgentStepStatus.Succeeded : AiAgentStepStatus.Failed,
                            Summary = result.Succeeded ? result.Summary : result.Error,
                            Governance = approval.Reason,
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        trace.Add(new AiAgentTraceEntry
                        {
                            Sequence = step.Sequence,
                            ToolName = step.ToolName,
                            Purpose = step.Purpose,
                            Status = AiAgentStepStatus.Failed,
                            Summary = $"Tool timed out after {tool.Definition.Timeout.TotalSeconds:0.#} seconds.",
                            Governance = "Timed-out tools are terminated and cannot continue in the background.",
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                    }
                    catch (Exception ex)
                    {
                        trace.Add(new AiAgentTraceEntry
                        {
                            Sequence = step.Sequence,
                            ToolName = step.ToolName,
                            Purpose = step.Purpose,
                            Status = AiAgentStepStatus.Failed,
                            Summary = ex.Message,
                            Governance = "The failure was isolated to this read-only tool call.",
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var cancelled = BuildRun(
                    context,
                    normalizedQuestion,
                    normalizedScreen,
                    startedAt,
                    readiness,
                    plan,
                    trace,
                    results,
                    AiAgentRunStatus.Cancelled,
                    nextAction);
                _auditStore.Record(cancelled);
                return cancelled;
            }

            var status = trace.Any(entry => entry.Status == AiAgentStepStatus.ApprovalRequired)
                ? AiAgentRunStatus.ApprovalRequired
                : trace.Any(entry => entry.Status == AiAgentStepStatus.Failed)
                    ? (trace.Any(entry => entry.Status == AiAgentStepStatus.Succeeded)
                        ? AiAgentRunStatus.PartiallySucceeded
                        : AiAgentRunStatus.Failed)
                    : AiAgentRunStatus.Succeeded;

            var run = BuildRun(
                context,
                normalizedQuestion,
                normalizedScreen,
                startedAt,
                readiness,
                plan,
                trace,
                results,
                status,
                nextAction);
            _auditStore.Record(run);
            return run;
        }

        private static IReadOnlyList<AiAgentPlanStep> BuildPlan(AiWorkflowContextSnapshot context, string question)
        {
            var steps = new List<(string Tool, string Purpose, string Input)>
            {
                ("get_project_overview", "Capture one immutable project and workflow baseline.", "{}"),
                ("get_bim_readiness", "Inspect BIM object scope and trusted GA-ready lift-unit evidence.", "{}"),
                ("get_zone_summary", "Verify allowed and forbidden placement-zone prerequisites.", "{}"),
                ("get_crane_setup", "Verify crane selection and load-chart evidence.", "{}"),
                ("get_ga_readiness", "Evaluate deterministic GA pre-run eligibility and result availability.", "{}")
            };

            if (context.ScreenContexts.Any(screen => screen.Issues.Count > 0)
                || question.Contains("issue", StringComparison.OrdinalIgnoreCase)
                || question.Contains("block", StringComparison.OrdinalIgnoreCase)
                || question.Contains("missing", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(("get_critical_issues", "Rank deterministic host-reported errors and warnings.", "{}"));
            }

            steps.Add(("search_project_evidence", "Retrieve citation-addressable evidence for the stated goal.",
                "{\"query\":\"" + EscapeJson(question) + "\"}"));

            return steps.Select((step, index) => new AiAgentPlanStep
            {
                Sequence = index + 1,
                ToolName = step.Tool,
                Purpose = step.Purpose,
                InputJson = step.Input,
                RequiresApproval = false
            }).ToArray();
        }

        private static AiAgentRunSnapshot BuildRun(
            AiWorkflowContextSnapshot context,
            string question,
            string activeScreen,
            DateTimeOffset startedAt,
            WorkflowReadinessSnapshot readiness,
            IReadOnlyList<AiAgentPlanStep> plan,
            IReadOnlyList<AiAgentTraceEntry> trace,
            IReadOnlyList<AiToolExecutionResult> results,
            AiAgentRunStatus status,
            string nextAction)
        {
            var conclusion = BuildConclusion(context, readiness, results);
            var actions = BuildActions(readiness, results, nextAction);
            var citations = results
                .SelectMany(result => result.Citations)
                .GroupBy(citation => citation.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(6)
                .ToArray();
            var governance = "All invoked tools were AI-owned, typed, read-only project inspections. " +
                             "No IFC, crane, zone, GA, report, or project state was modified. " +
                             "Any future mutating tool is blocked until explicit user approval.";
            var visibleAnswer = BuildVisibleAnswer(
                question,
                status,
                conclusion,
                plan,
                trace,
                results,
                actions,
                citations,
                governance,
                context.ModelRevision);

            return new AiAgentRunSnapshot
            {
                AgentName = "Project Readiness Agent",
                Goal = question,
                Status = status,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                ModelRevision = context.ModelRevision,
                ModelFingerprint = context.ModelFingerprint,
                ActiveScreen = activeScreen,
                Plan = plan,
                Trace = trace.ToArray(),
                ToolResults = results.ToArray(),
                Conclusion = conclusion,
                RecommendedActions = actions,
                Citations = citations,
                GovernanceSummary = governance,
                VisibleAnswer = visibleAnswer
            };
        }

        private static string BuildConclusion(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            IReadOnlyList<AiToolExecutionResult> results)
        {
            var bim = Find(results, "get_bim_readiness");
            var ga = Find(results, "get_ga_readiness");
            var trusted = ReadFactInt(bim, "TrustedGaLiftUnits");
            var hasGaBlocker = ReadFactBool(ga, "HasNoCandidateBlocker");

            if (!context.HasProject)
                return "The workflow is not ready because no active project workspace is available.";
            if (context.ElementCount == 0)
                return "The workflow is not ready because no IFC/BIM model objects are loaded.";
            if (trusted == 0 || hasGaBlocker)
                return "Optimization is blocked because the current snapshot exposes zero trusted GA-ready grouped lift units. Visible or estimated weights are not accepted as trusted optimization evidence.";
            if (context.ZoneCount == 0)
                return "Optimization is not ready because no crane-placement zones are defined.";
            if (context.CraneCount == 0)
                return "Optimization is not ready because no crane is selected or present in the project.";
            if (!context.HasLoadChartEvidence)
                return "Core scene inputs exist, but capacity-based optimization is not ready because load-chart evidence is missing.";
            if (readiness.MissingCriticalCount > 0)
                return $"The project still has {readiness.MissingCriticalCount:N0} critical prerequisite(s) to resolve before reliable optimization or reporting.";

            return "The deterministic project snapshot satisfies the core readiness prerequisites. Proceed with engineering review and the optimizer while preserving the captured evidence trail.";
        }

        private static IReadOnlyList<string> BuildActions(
            WorkflowReadinessSnapshot readiness,
            IReadOnlyList<AiToolExecutionResult> results,
            string nextAction)
        {
            var actions = new List<string>();
            actions.AddRange(readiness.Items
                .Where(item => item.IsCritical && !item.IsComplete)
                .Select(item => string.IsNullOrWhiteSpace(item.NextClick)
                    ? item.Recommendation
                    : $"{item.NextClick} — {item.Recommendation}"));
            actions.AddRange(results.SelectMany(result => result.RecommendedActions));
            if (!string.IsNullOrWhiteSpace(nextAction))
                actions.Add(nextAction.Trim());

            return actions
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToArray();
        }

        private static string BuildVisibleAnswer(
            string question,
            AiAgentRunStatus status,
            string conclusion,
            IReadOnlyList<AiAgentPlanStep> plan,
            IReadOnlyList<AiAgentTraceEntry> trace,
            IReadOnlyList<AiToolExecutionResult> results,
            IReadOnlyList<string> actions,
            IReadOnlyList<AiKnowledgeCitation> citations,
            string governance,
            long modelRevision)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Project Readiness Agent");
            builder.AppendLine();
            builder.AppendLine($"**Goal:** {question}");
            builder.AppendLine($"**Run status:** {StatusLabel(status)}");
            builder.AppendLine($"**Snapshot:** model revision {modelRevision:N0}");
            builder.AppendLine();
            builder.AppendLine("### Conclusion");
            builder.AppendLine(conclusion);
            builder.AppendLine();
            builder.AppendLine("### Plan and tool execution");

            foreach (var step in plan)
            {
                var entry = trace.FirstOrDefault(item => item.Sequence == step.Sequence);
                var statusLabel = entry?.StatusLabel ?? "Not executed";
                var summary = string.IsNullOrWhiteSpace(entry?.Summary) ? step.Purpose : entry.Summary;
                builder.AppendLine($"- **T{step.Sequence} · `{step.ToolName}` · {statusLabel}:** {summary}");
            }

            builder.AppendLine();
            builder.AppendLine("### Deterministic findings");
            foreach (var result in results.Where(result => result.Succeeded).Take(7))
                builder.AppendLine($"- **{result.ToolName}:** {result.Evidence}");

            if (actions.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("### Recommended actions");
                for (var index = 0; index < actions.Count; index++)
                    builder.AppendLine($"{index + 1}. {actions[index]}");
            }

            if (citations.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("### Retrieved evidence");
                foreach (var citation in citations)
                    builder.AppendLine($"- [{citation.Id}] {citation.SourceLabel} — {citation.Snippet}");
            }

            builder.AppendLine();
            builder.AppendLine("### Governance");
            builder.AppendLine(governance);
            builder.AppendLine("Engineering review remains required; this agent does not certify lift safety or crane feasibility.");
            return builder.ToString().Trim();
        }

        private static AiToolExecutionResult? Find(IReadOnlyList<AiToolExecutionResult> results, string toolName)
            => results.FirstOrDefault(result => result.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        private static int ReadFactInt(AiToolExecutionResult? result, string key)
        {
            if (result != null
                && result.Facts.TryGetValue(key, out var value)
                && int.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static bool ReadFactBool(AiToolExecutionResult? result, string key)
        {
            return result != null
                   && result.Facts.TryGetValue(key, out var value)
                   && bool.TryParse(value, out var parsed)
                   && parsed;
        }

        private static string StatusLabel(AiAgentRunStatus status) => status switch
        {
            AiAgentRunStatus.Succeeded => "Completed",
            AiAgentRunStatus.PartiallySucceeded => "Completed with tool warnings",
            AiAgentRunStatus.ApprovalRequired => "Approval required",
            AiAgentRunStatus.Cancelled => "Cancelled",
            AiAgentRunStatus.Failed => "Failed",
            _ => status.ToString()
        };

        private static string EscapeJson(string value) => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        private static bool LooksLikeNarrowBimCountQuestion(string normalized)
        {
            if (!normalized.Contains("how many", StringComparison.Ordinal))
                return false;

            return normalized.Contains("bim object", StringComparison.Ordinal)
                   || normalized.Contains("zone", StringComparison.Ordinal)
                   || normalized.Contains("slab", StringComparison.Ordinal)
                   || normalized.Contains("beam", StringComparison.Ordinal)
                   || normalized.Contains("column", StringComparison.Ordinal)
                   || normalized.Contains("wall", StringComparison.Ordinal)
                   || normalized.Contains("stair", StringComparison.Ordinal);
        }

        private sealed class CapturedAgentUiContext : IUiInteractionContextService
        {
            public CapturedAgentUiContext(string activeScreen)
            {
                ActiveTabName = activeScreen;
                ActivePanelName = activeScreen;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }

            public event EventHandler? ContextChanged;
            public event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested;
            public string ActiveTabName { get; }
            public string ActivePanelName { get; }
            public DateTimeOffset LastUpdatedAt { get; }
            public void SetActiveTab(string tabName, string panelName) { }
            public void RequestTabNavigation(string tabName, string reason) { }
        }
    }
}
