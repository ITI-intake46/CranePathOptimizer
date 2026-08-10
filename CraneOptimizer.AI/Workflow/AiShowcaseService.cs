using System.Text;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Builds the visible AI makeover layer. The goal is to make AI obvious to a
    /// reviewer: multiple agent-like capabilities, explainable traces, what-if
    /// prompts, and report-ready outputs are visible before the user asks anything.
    /// </summary>
    public sealed class AiShowcaseService
    {
        public AiShowcaseSnapshot Build(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiMissionControlSnapshot mission,
            string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));
            if (mission == null) throw new ArgumentNullException(nameof(mission));

            var state = AiWorkflowStateClassifier.Classify(context, readiness);
            var issueCount = context.ScreenContexts.SelectMany(screen => screen.Issues)
                .Count(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                                || issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
            var evidenceAreaCount = context.ScreenContexts.Count;
            var capabilityCount = context.OptimizationResult.HasResult ? 8 : 7;
            var hero = BuildHeroHeadline(state, readiness);
            var subheadline = BuildHeroSubheadline(context, readiness, state, evidenceAreaCount, issueCount);
            var capabilities = BuildCapabilities(context, readiness, mission, state, activeScreen).ToList();
            var prompts = BuildDemoPrompts(state, context.OptimizationResult.HasResult).ToList();
            var trace = BuildDecisionTrace(context, readiness, mission, state, activeScreen, evidenceAreaCount, issueCount).ToList();
            var narrative = BuildNarrative(context, readiness, mission, state, capabilityCount, evidenceAreaCount, issueCount);
            var brief = BuildPresentationBrief(hero, subheadline, narrative, capabilities, prompts, trace, mission);

            return new AiShowcaseSnapshot
            {
                HeroHeadline = hero,
                HeroSubheadline = subheadline,
                PowerScoreLabel = $"AI visibility: {capabilityCount} live capabilities · {evidenceAreaCount} evidence area(s) · {issueCount} flagged issue(s)",
                AgentStackSummary = BuildAgentStackSummary(state, context.OptimizationResult.HasResult),
                Narrative = narrative,
                PresentationBrief = brief,
                Capabilities = capabilities,
                DemoPrompts = prompts,
                DecisionTrace = trace
            };
        }

        private static string BuildHeroHeadline(AiWorkflowState state, WorkflowReadinessSnapshot readiness)
        {
            return state.Kind switch
            {
                AiWorkflowStateKind.NoProject => "AI Studio: project-aware crane-planning copilot",
                AiWorkflowStateKind.NoIfcModel => "AI Studio: BIM intake copilot is ready",
                AiWorkflowStateKind.OptimizerVerificationRequired => "AI Studio: optimizer verification is the next authority check",
                AiWorkflowStateKind.ResultReview => "AI Studio: optimization result explainer is active",
                AiWorkflowStateKind.ReportCandidate => "AI Studio: report narrative agent is ready",
                _ when state.CanProceedToOptimizerCheck => "AI Studio: project prerequisites complete; verify optimizer readiness",
                _ => "AI Studio: mission-control intelligence is active"
            };
        }

        private static bool IsCorePrerequisiteName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.Contains("Placement zones", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Crane setup", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Load chart evidence", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildHeroSubheadline(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiWorkflowState state,
            int evidenceAreaCount,
            int issueCount)
        {
            return $"Live state: {state.Status}. AI is reading {context.ElementCount:N0} element(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s), {evidenceAreaCount:N0} contributor(s), and {issueCount:N0} warning/error issue(s). Readiness is {readiness.CompleteCount}/{readiness.TotalCount}.";
        }

        private static string BuildAgentStackSummary(AiWorkflowState state, bool hasResult)
        {
            var agents = new List<string>
            {
                "Readiness Sentinel",
                "BIM/IFC Intelligence",
                "Lift-Unit Evidence Reviewer",
                "What-If Advisor",
                "Report Narrative Generator",
                "Local RAG Assistant",
                "ModelOps Benchmarking"
            };

            if (hasResult)
                agents.Insert(3, "Optimization Failure Explainer");

            return string.Join(" · ", agents.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<AiShowcaseCapability> BuildCapabilities(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiMissionControlSnapshot mission,
            AiWorkflowState state,
            string activeScreen)
        {
            yield return new AiShowcaseCapability
            {
                Title = "Readiness Sentinel",
                Badge = state.CanProceedToOptimizerCheck ? "VERIFY" : "GUIDE",
                Purpose = "Classifies visible workflow prerequisites while preserving the optimizer as the authority for current-run readiness.",
                Evidence = $"State={state.Kind}; readiness={readiness.CompleteCount}/{readiness.TotalCount}; active screen={Normalize(activeScreen, readiness.ActiveScreen)}.",
                VisiblePower = "The assistant changes its diagnosis, next click, and risk language as the project moves through IFC, zones, crane, load-chart, GA, and reports.",
                DemoPrompt = "What should I do next from this screen?",
                RiskBoundary = "It can guide navigation and explanation only; it does not modify project data or certify optimizer readiness."
            };

            yield return new AiShowcaseCapability
            {
                Title = "BIM / IFC Intelligence",
                Badge = context.ElementCount > 0 ? "BIM" : "INTAKE",
                Purpose = "Explains model metadata, import readiness, IFC categories, assembly marks, quantities, and exporter requirements.",
                Evidence = context.ElementCount > 0
                    ? $"{context.ElementCount:N0} imported element(s) and IFC screen evidence are available."
                    : "No elements are loaded yet; the AI correctly asks for IFC intake first.",
                VisiblePower = "Shows that AI understands BIM data quality, not just generic chat.",
                DemoPrompt = "Can the IFC tell me the building usage/type and what data is missing for crane optimization?",
                RiskBoundary = "It reports unknown metadata instead of inventing building type or usage."
            };

            yield return new AiShowcaseCapability
            {
                Title = "Lift-Unit Evidence Reviewer",
                Badge = state.HasNoGaCandidateBlocker || state.HasEstimatedWeightBlocker ? "REVIEW" : "CHECK",
                Purpose = "Reviews AI-visible import/readiness weight and candidate signals without pretending they are the optimizer's authoritative preflight.",
                Evidence = state.HasNoGaCandidateBlocker
                    ? "AI-visible import/readiness evidence reports zero candidate units; this is troubleshooting evidence only."
                    : state.HasEstimatedWeightBlocker
                        ? "Estimated/fallback weight evidence is flagged for review."
                        : "No candidate/weight advisory is currently the top AI evidence item.",
                VisiblePower = "It separates data-quality warnings from the untouched optimizer's run decision.",
                DemoPrompt = "How should I interpret the AI candidate/weight warnings, and where do I verify whether the optimizer can actually run?",
                RiskBoundary = "It never certifies ready or blocked from AI candidate counts and never suggests bypassing the optimizer's own gate."
            };

            if (context.OptimizationResult.HasResult)
            {
                var result = context.OptimizationResult;
                yield return new AiShowcaseCapability
                {
                    Title = "Optimization Failure Explainer",
                    Badge = "GA",
                    Purpose = "Turns GA coverage, lifted count, failed elements, diagnostics, and dominant failure modes into plain-language recommendations.",
                    Evidence = $"Captured result={result.CoverageRatio:P2}; lifted={result.LiftedElementCount:N0}/{result.TotalElementCount:N0}; positions={result.PositionCount:N0}.",
                    VisiblePower = "Makes an algorithmic result explainable for PM, site, BIM, and engineering stakeholders.",
                    DemoPrompt = "Explain why the captured optimization result did not reach 100% coverage and rank the fixes.",
                    RiskBoundary = "It explains deterministic results; it does not certify crane capacity or rigging."
                };
            }

            yield return new AiShowcaseCapability
            {
                Title = "What-If Advisor",
                Badge = "SCENARIO",
                Purpose = "Ranks whether to fix BIM data, zones, crane model, load chart, or optimizer setup first.",
                Evidence = mission.WhatIfAdvisor.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? mission.RecommendedAction,
                VisiblePower = "Shows reasoning over alternatives instead of only returning a static checklist.",
                DemoPrompt = "What-if advisor: should I fix data, zones, crane, or load chart first?",
                RiskBoundary = "It proposes investigation order only; deterministic services remain the source of truth."
            };

            yield return new AiShowcaseCapability
            {
                Title = "Report Narrative Generator",
                Badge = "REPORT",
                Purpose = "Drafts reviewed report wording, blocker notes, QA evidence packs, and appendix text from deterministic project context.",
                Evidence = context.OptimizationResult.HasResult ? "Optimization result snapshot is attached." : "No final result yet; blocker/resolution narrative is available instead.",
                VisiblePower = "Converts technical state into stakeholder-ready language while retaining caveats.",
                DemoPrompt = "Draft the report language for the current state with assumptions and caveats.",
                RiskBoundary = "The output requires review and is not engineering certification."
            };

            yield return new AiShowcaseCapability
            {
                Title = "Local RAG + Software Navigator",
                Badge = "RAG",
                Purpose = "Uses local docs, XAML inventory, and screen context to answer how to use the actual product, not an imagined UI.",
                Evidence = "AI context includes software map, visible tabs/actions, and screen contributor facts.",
                VisiblePower = "The assistant can point to real tabs, real commands, and hidden/coming-soon boundaries.",
                DemoPrompt = "Explain this screen and its important buttons using only the visible software context.",
                RiskBoundary = "It is instructed not to invent unavailable buttons or hidden features."
            };

            yield return new AiShowcaseCapability
            {
                Title = "ModelOps Benchmarking",
                Badge = "MLOPS",
                Purpose = "Supports local Ollama model checks, pulls, streaming, benchmarks, and model choice guidance.",
                Evidence = "The full AI Optimization Studio includes model status, installed model list, benchmark summaries, streaming cancel, and history.",
                VisiblePower = "Shows AI engineering maturity: not only prompts, but model management and evaluation UX.",
                DemoPrompt = "Which local model should I use for fast side-dock help versus deep report reasoning?",
                RiskBoundary = "Benchmarks guide model choice; they do not change optimization outputs."
            };
        }

        private static IEnumerable<AiShowcasePrompt> BuildDemoPrompts(AiWorkflowState state, bool hasResult)
        {
            yield return new AiShowcasePrompt
            {
                Label = "Next best action",
                Question = "What should I do next from this screen? Give evidence, next click, risk, and confidence.",
                WhyItShowsAiPower = "Demonstrates live workflow classification and grounded navigation."
            };

            yield return new AiShowcasePrompt
            {
                Label = "Data-trust diagnosis",
                Question = state.HasNoGaCandidateBlocker
                    ? "Why does AI candidate/readiness evidence report zero while the optimizer may use a different authoritative preflight, and how should I verify it?"
                    : "Audit the current model data trust for crane optimization and tell me what evidence is advisory versus authoritative.",
                WhyItShowsAiPower = "Demonstrates engineering-aware AI guardrails rather than generic advice."
            };

            yield return new AiShowcasePrompt
            {
                Label = "What-if ranking",
                Question = "What-if advisor: should I fix BIM data, zones, crane setup, load chart, or optimizer parameters first?",
                WhyItShowsAiPower = "Demonstrates scenario reasoning over multiple project levers."
            };

            yield return new AiShowcasePrompt
            {
                Label = hasResult ? "Result explainer" : "Report-ready blocker note",
                Question = hasResult
                    ? "Explain the captured optimization result for a project manager and rank the practical fixes."
                    : "Draft a report-ready blocker note for the current state with assumptions, evidence, and caveats.",
                WhyItShowsAiPower = "Demonstrates stakeholder translation from deterministic context."
            };
        }

        private static IEnumerable<string> BuildDecisionTrace(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiMissionControlSnapshot mission,
            AiWorkflowState state,
            string activeScreen,
            int evidenceAreaCount,
            int issueCount)
        {
            yield return $"1. Ingested live UI evidence from {evidenceAreaCount:N0} contributor(s) on {Normalize(activeScreen, readiness.ActiveScreen)}.";
            yield return $"2. Classified workflow as {state.Kind} / {state.Status}: {state.RootCause}";
            yield return $"3. Reconciled project counts: {context.ElementCount:N0} elements, {context.ZoneCount:N0} zones, {context.CraneCount:N0} cranes, result={(context.OptimizationResult.HasResult ? "available" : "not available")}.";
            yield return $"4. Prioritized {issueCount:N0} deterministic warning/error issue(s) before any LLM wording.";
            yield return $"5. Recommended next action: {mission.RecommendedAction}";
            yield return "6. Kept AI inside advisory boundaries: explain, draft, copy, navigate only after user confirmation.";
        }

        private static string BuildNarrative(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiMissionControlSnapshot mission,
            AiWorkflowState state,
            int capabilityCount,
            int evidenceAreaCount,
            int issueCount)
        {
            var builder = new StringBuilder();
            builder.Append($"This project now presents AI as a visible engineering copilot, not a small chat add-on. ");
            builder.Append($"The side dock exposes {capabilityCount:N0} AI capabilities driven by live crane-planning evidence: workflow guidance, BIM/IFC intelligence, lift-unit evidence review, what-if ranking, report drafting, local RAG, and model operations. ");
            builder.Append($"Current state is {state.Status}; AI is reading {evidenceAreaCount:N0} evidence area(s), {issueCount:N0} issue(s), and readiness {readiness.CompleteCount}/{readiness.TotalCount}. ");
            builder.Append($"The headline recommendation is: {mission.RecommendedAction}");
            if (context.OptimizationResult.HasResult)
                builder.Append($" Captured optimization coverage is {context.OptimizationResult.CoverageRatio:P2}.");
            return builder.ToString();
        }

        private static string BuildPresentationBrief(
            string hero,
            string subheadline,
            string narrative,
            IReadOnlyList<AiShowcaseCapability> capabilities,
            IReadOnlyList<AiShowcasePrompt> prompts,
            IReadOnlyList<string> trace,
            AiMissionControlSnapshot mission)
        {
            var builder = new StringBuilder();
            builder.AppendLine("AI Makeover Showcase Brief");
            builder.AppendLine();
            builder.AppendLine(hero);
            builder.AppendLine(subheadline);
            builder.AppendLine();
            builder.AppendLine("Story");
            builder.AppendLine(narrative);
            builder.AppendLine();
            builder.AppendLine("Visible AI capabilities");
            foreach (var capability in capabilities)
            {
                builder.AppendLine($"- {capability.Title} [{capability.Badge}]: {capability.Purpose}");
                builder.AppendLine($"  Evidence: {capability.Evidence}");
                builder.AppendLine($"  Demo prompt: {capability.DemoPrompt}");
                builder.AppendLine($"  Boundary: {capability.RiskBoundary}");
            }
            builder.AppendLine();
            builder.AppendLine("Explainable decision trace");
            foreach (var item in trace)
                builder.AppendLine("- " + item);
            builder.AppendLine();
            builder.AppendLine("Demo prompt chips");
            foreach (var prompt in prompts)
                builder.AppendLine($"- {prompt.Label}: {prompt.Question}");
            builder.AppendLine();
            builder.AppendLine("Mission Control recommendation");
            builder.AppendLine(mission.RecommendedAction);
            builder.AppendLine();
            builder.AppendLine("Safety boundary");
            builder.AppendLine("AI explains, drafts, audits, ranks, and proposes confirmed navigation/text exports. Application-owned optimizer and engineering checks remain authoritative; AI-side import/readiness signals do not replace them.");
            return builder.ToString().Trim();
        }

        private static string Normalize(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
