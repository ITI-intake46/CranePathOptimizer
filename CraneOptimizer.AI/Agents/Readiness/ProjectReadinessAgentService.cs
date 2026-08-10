using System.Diagnostics;
using System.Text;
using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge.Rag;
using CraneOptimizer.AI.Services;
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

            if (AiBimProjectQuestionService.IsLiveBimDataQuestion(question))
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
                ("get_bim_readiness", "Inspect BIM object scope and AI-visible import/readiness candidate evidence without replacing the optimizer preflight.", "{}"),
                ("get_zone_summary", "Verify allowed and forbidden placement-zone prerequisites.", "{}"),
                ("get_crane_setup", "Verify crane selection and load-chart evidence.", "{}"),
                ("get_ga_readiness", "Inspect AI-visible optimizer prerequisites, advisory candidate evidence, and result availability without replacing the optimizer preflight.", "{}")
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
            var conclusion = BuildConclusion(context, readiness, results, question);
            var actions = BuildActions(context, readiness, results, nextAction, question);
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

        private enum ReadinessQuestionIntent
        {
            General,
            RunEligibility,
            NextStep
        }

        private static string BuildConclusion(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            IReadOnlyList<AiToolExecutionResult> results,
            string question)
        {
            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var intent = ClassifyReadinessQuestion(question);

            if (!context.HasProject)
                return "The workflow is not ready because no active project workspace is available.";
            if (context.ElementCount == 0)
                return "The workflow is not ready because no IFC/BIM model objects are loaded.";
            if (context.ZoneCount == 0)
                return "The next visible prerequisite is to define crane-placement zones.";
            if (context.CraneCount == 0)
                return "The next visible prerequisite is to add or select a crane.";
            if (!assessment.HasCurrentLoadChartEvidence)
                return "The next visible prerequisite is to verify current crane/load-chart evidence. Historical result capacity/radius values are not treated as proof of the current crane setup.";

            if (intent == ReadinessQuestionIntent.NextStep)
            {
                if (context.OptimizationResult.HasResult)
                {
                    return "AI-captured optimization result evidence is available, but the strict host contract does not identify which saved Analysis result is currently selected. " +
                           "The next workflow step is ANALYSIS → Lift Summary so the user can review the result selected in the application. " +
                           "If another run is desired afterward, its current eligibility must still be checked in the optimizer's built-in readiness indicator.";
                }

                return BuildOptimizerVerificationConclusion(assessment, leadWithEligibilityAnswer: false);
            }

            if (intent == ReadinessQuestionIntent.RunEligibility)
                return BuildOptimizerVerificationConclusion(assessment, leadWithEligibilityAnswer: true);

            if (context.OptimizationResult.HasResult)
            {
                return "Core project prerequisites are visible and AI-captured optimization result evidence exists. " +
                       "The strict host contract does not bind that evidence to the saved Analysis result currently selected on screen, so the AI does not present its captured percentage as the selected result. " +
                       "Result review and current rerun eligibility are separate; the AI does not certify the optimizer's current preflight.";
            }

            return BuildOptimizerVerificationConclusion(assessment, leadWithEligibilityAnswer: false);
        }

        private static string BuildOptimizerVerificationConclusion(
            AiOptimizerEvidenceAssessment assessment,
            bool leadWithEligibilityAnswer)
        {
            var prefix = leadWithEligibilityAnswer
                ? "The AI cannot certify the optimizer's current run eligibility from this snapshot. "
                : "Core project prerequisites visible to AI are present. ";

            if (assessment.CandidateEvidenceConflict)
            {
                return prefix +
                       $"AI import/readiness evidence reports a zero-candidate condition while a completed optimization result previously evaluated {assessment.HistoricalEvaluatedLiftUnits:N0} lift unit(s). " +
                       "Those signals conflict or use different scopes, so the AI must not declare the optimizer blocked from them. Open ANALYSIS → Run Optimizer and use Current Run Readiness as the definitive decision.";
            }

            if (assessment.HasNoCandidateAdvisory)
            {
                var countText = assessment.AdvisoryTrustedLiftUnits.HasValue
                    ? $" (reported count {assessment.AdvisoryTrustedLiftUnits.Value:N0})"
                    : string.Empty;
                return prefix +
                       "The AI-visible import/readiness context contains a no-candidate warning" + countText + ", but that warning is troubleshooting evidence rather than the optimizer dialog's authoritative current-run preflight. " +
                       "Open ANALYSIS → Run Optimizer and use Current Run Readiness. If the optimizer itself reports a blocker, then use the IFC/weight evidence to troubleshoot it; do not bypass the optimizer.";
            }

            return prefix +
                   "The current AI host contract does not expose the optimizer dialog's authoritative current-run preflight. Open ANALYSIS → Run Optimizer and use Current Run Readiness as the source of truth.";
        }

        private static IReadOnlyList<string> BuildActions(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            IReadOnlyList<AiToolExecutionResult> results,
            string nextAction,
            string question)
        {
            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var intent = ClassifyReadinessQuestion(question);
            var actions = new List<string>();

            var missingCore = readiness.Items
                .Where(item => item.IsCritical && !item.IsComplete)
                .ToArray();

            if (missingCore.Length > 0)
            {
                actions.AddRange(missingCore.Select(item => string.IsNullOrWhiteSpace(item.NextClick)
                    ? item.Recommendation
                    : $"{item.NextClick} — {item.Recommendation}"));
            }
            else if (intent == ReadinessQuestionIntent.NextStep && context.OptimizationResult.HasResult)
            {
                actions.Add("ANALYSIS → Lift Summary — review the result selected in the application and inspect unresolved lifts and dominant failure modes.");
            }
            else
            {
                actions.Add("ANALYSIS → Run Optimizer — use the optimizer's built-in Current Run Readiness for the definitive run decision.");
            }

            if (assessment.HasNoCandidateAdvisory || assessment.HasEstimatedWeightAdvisory)
                actions.Add("Keep IFC/import readiness warnings as secondary troubleshooting evidence; review them if the optimizer itself confirms a blocker.");

            if (context.OptimizationResult.HasResult
                && intent != ReadinessQuestionIntent.NextStep
                && context.OptimizationResult.CoverageRatio < 0.95)
            {
                actions.Add("ANALYSIS → Lift Summary — review the captured result before deciding whether a rerun is useful.");
            }

            return actions
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToArray();
        }

        private static ReadinessQuestionIntent ClassifyReadinessQuestion(string? question)
        {
            var normalized = question?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalized.Contains("can i run", StringComparison.Ordinal)
                || normalized.Contains("can we run", StringComparison.Ordinal)
                || normalized.Contains("can i rerun", StringComparison.Ordinal)
                || normalized.Contains("can we rerun", StringComparison.Ordinal)
                || normalized.Contains("run the optimizer now", StringComparison.Ordinal)
                || normalized.Contains("run ga now", StringComparison.Ordinal))
            {
                return ReadinessQuestionIntent.RunEligibility;
            }

            if (normalized.Contains("what should i do next", StringComparison.Ordinal)
                || normalized.Contains("next step", StringComparison.Ordinal)
                || normalized.Contains("what next", StringComparison.Ordinal))
            {
                return ReadinessQuestionIntent.NextStep;
            }

            return ReadinessQuestionIntent.General;
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

            public event EventHandler? ContextChanged { add { } remove { } }
            public event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested { add { } remove { } }
            public string ActiveTabName { get; }
            public string ActivePanelName { get; }
            public DateTimeOffset LastUpdatedAt { get; }
            public void SetActiveTab(string tabName, string panelName) { }
            public void RequestTabNavigation(string tabName, string reason) { }
        }
    }
}
