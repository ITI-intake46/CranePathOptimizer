using System.Text;
using System.Text.RegularExpressions;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Services
{
    /// <summary>
    /// Capability-based question router for the side-dock and full AI copilot.
    /// It separates user intent from answer content so the assistant does not
    /// behave like a brittle Q&A table or over-summarize local documentation.
    /// </summary>
    public static class AiIntentOrchestrator
    {
        public enum AiIntentFamily
        {
            Unknown,
            DomainExplanation,
            ProjectFact,
            WorkflowStatus,
            BlockerDiagnosis,
            ModelOpsRecommendation,
            ReportGeneration,
            UiNavigation,
            WhatIfScenario,
            EngineeringQualityAssurance,
            DeliveryRequirements,
            RealWorldImplementation,
            AppCapabilityExplanation
        }

        public sealed class AiIntentDecision
        {
            public AiIntentFamily Intent { get; init; } = AiIntentFamily.Unknown;
            public string Scope { get; init; } = "general";
            public bool NeedsProjectFacts { get; init; }
            public bool NeedsRagDocs { get; init; }
            public bool NeedsOllama { get; init; }
            public string PreferredSection { get; init; } = "Chat";
            public string AnswerStyle { get; init; } = "direct";
            public IReadOnlyList<string> FactKeys { get; init; } = Array.Empty<string>();
            public string Reason { get; init; } = string.Empty;
        }

        private sealed class ProjectFacts
        {
            public string ProjectName { get; init; } = "none";
            public string ActiveScreen { get; init; } = "HOME";
            public string ModelLabel { get; init; } = "No IFC model loaded";
            public long ModelRevision { get; init; }
            public int ElementCount { get; init; }
            public int TableVisible { get; init; }
            public int TableTotal { get; init; }
            public int ZoneCount { get; init; }
            public int AllowedZoneCount { get; init; }
            public int ForbiddenZoneCount { get; init; }
            public double AllowedZoneArea { get; init; }
            public double ForbiddenZoneArea { get; init; }
            public int CraneCount { get; init; }
            public int LoadChartEvidenceCount { get; init; }
            public bool HasLoadChartEvidence { get; init; }
            public double BuildingArea { get; init; }
            public string BuildingAreaUnit { get; init; } = "m²";
            public double BuildingHeight { get; init; }
            public string BuildingLengthUnit { get; init; } = "m";
            public string GeometryUnit { get; init; } = "m";
            public string AreaUnit { get; init; } = "m²";
            public string StoredMassUnit { get; init; } = "t";
            public string ThresholdMassUnit { get; init; } = "kg";
            public string DensityUnit { get; init; } = "kg/m³";
            public bool HasOptimizationResult { get; init; }
            public string TopCategories { get; init; } = string.Empty;
            public string TopRawIfcTypes { get; init; } = string.Empty;
        }

        public static AiIntentDecision Classify(AiWorkflowContextSnapshot? context, string? question, string? activeScreen)
        {
            var normalized = Normalize(question);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Decision(
                    AiIntentFamily.WorkflowStatus,
                    "current_project",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: false,
                    preferredSection: "Mission",
                    answerStyle: "live_status",
                    reason: "Empty prompt defaults to current workflow status.");
            }

            if (AiBimProjectQuestionService.IsLiveBimDataQuestion(question))
            {
                return Decision(
                    AiIntentFamily.ProjectFact,
                    "current_project_bim",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: false,
                    preferredSection: "BIM",
                    answerStyle: "deterministic_bim_fact_with_scope_provenance",
                    factKeys: ExtractFactKeys(normalized),
                    reason: "Question asks for BIM quantities, count-scope comparison, classification coverage, or a Filter-versus-scene discrepancy that must be answered deterministically without a model provider.");
            }

            if (IsModelOpsQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.ModelOpsRecommendation,
                    "local_ai_runtime",
                    needsProjectFacts: false,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "AI Studio",
                    answerStyle: "modelops_recommendation",
                    reason: "Question asks about local models, Ollama, benchmark, speed, or reasoning model choice; local model should synthesize from current ModelOps evidence, not from a canned answer.");
            }

            if (IsProjectFactQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.ProjectFact,
                    "current_project",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: false,
                    preferredSection: normalized.Contains("bim") || normalized.Contains("ifc") || normalized.Contains("slab") || normalized.Contains("beam") ? "BIM" : "Mission",
                    answerStyle: "fact_with_evidence",
                    factKeys: ExtractFactKeys(normalized),
                    reason: "Question asks for a live count, quantity, area, height, or project metric.");
            }

            // Open-ended advisory and concept questions must be recognized before
            // status/blocker routing. Otherwise words such as "needed", "result",
            // "accuracy", or "optimization" can incorrectly pull the answer into
            // the current GA blocker and make the copilot feel like a lookup table.
            if (IsEngineeringQualityQuestion(normalized))
            {
                var projectSpecific = IsProjectSpecificWording(normalized);
                return Decision(
                    AiIntentFamily.EngineeringQualityAssurance,
                    projectSpecific ? "project_quality_review" : "engineering_quality",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Mission",
                    answerStyle: projectSpecific ? "project_specific_quality_review" : "quality_assurance",
                    reason: projectSpecific
                        ? "Question asks what to verify for the current project/result; answer must be driven by live project risk facts first, then the general QA method."
                        : "Question asks how to make outputs accurate, trustworthy, defensible, or verified; local model should synthesize a tailored QA plan from live evidence, not from a canned checklist or blocker summary.");
            }

            if (IsDeliveryRequirementsQuestion(normalized))
            {
                var projectSpecific = IsProjectSpecificWording(normalized);
                return Decision(
                    AiIntentFamily.DeliveryRequirements,
                    projectSpecific ? "project_delivery_readiness" : "delivery_readiness",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Mission",
                    answerStyle: projectSpecific ? "current_deliverable_readiness" : "deliverable_checklist",
                    reason: projectSpecific
                        ? "Question asks what must be delivered for the current project/state; answer must separate ready evidence from blocked/missing live evidence."
                        : "Question asks what needs to be delivered, completed, submitted, or handed over; local model should synthesize requirements from the actual workflow state, not from a fixed answer or a pure GA-blocker diagnosis.");
            }

            if (IsAppValueQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.AppCapabilityExplanation,
                    "application_value",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "AI Studio",
                    answerStyle: "product_value",
                    reason: "Question asks why the software/project matters; local model should provide a direct value explanation using domain and live project context.");
            }

            if (IsRealWorldImplementationQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.RealWorldImplementation,
                    "field_implementation",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Mission",
                    answerStyle: "field_workflow_then_software_support",
                    reason: "Question asks how to implement the idea in real life; answer must lead with the field/engineering workflow, then explain how the app supports it, and only then mention current project blockers.");
            }

            if (IsDomainExplanationQuestion(normalized))
            {
                var wantsProjectConnection = IsProjectConnectionRequested(normalized);
                return Decision(
                    AiIntentFamily.DomainExplanation,
                    wantsProjectConnection ? "domain_plus_current_project" : "general_domain",
                    needsProjectFacts: wantsProjectConnection,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Chat",
                    answerStyle: wantsProjectConnection ? "domain_then_project_context" : "direct_explanation",
                    reason: wantsProjectConnection
                        ? "Question asks for a construction concept connected to the current project; answer concept first, then apply only the live project facts that are actually exposed."
                        : "Question asks for general construction/crane/logistics meaning; local model should answer the concept directly and avoid software-documentation summaries.");
            }

            if (IsWorkflowStatusQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.WorkflowStatus,
                    "current_project",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: false,
                    preferredSection: "Mission",
                    answerStyle: "live_status",
                    reason: "Question asks for current readiness, health, missing inputs, or the next step.");
            }

            if (IsBlockerQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.BlockerDiagnosis,
                    "current_project",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: false,
                    preferredSection: "Optimize",
                    answerStyle: "blocker_diagnosis",
                    reason: "Question asks about optimizer/GA blockers or why an action cannot proceed.");
            }

            if (IsWhatIfQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.WhatIfScenario,
                    "scenario_reasoning",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Optimize",
                    answerStyle: "scenario_ranking",
                    reason: "Question asks for alternate planning scenario reasoning.");
            }

            if (IsReportQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.ReportGeneration,
                    "reporting",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Report",
                    answerStyle: "stakeholder_narrative",
                    reason: "Question asks for report wording, client language, or documentation output.");
            }

            if (IsUiNavigationQuestion(normalized))
            {
                return Decision(
                    AiIntentFamily.UiNavigation,
                    "software_navigation",
                    needsProjectFacts: true,
                    needsRagDocs: false,
                    needsOllama: true,
                    preferredSection: "Chat",
                    answerStyle: "visible_ui_navigation",
                    reason: "Question asks where/how to use visible software functions.");
            }

            return Decision(
                AiIntentFamily.Unknown,
                "mixed_or_open",
                needsProjectFacts: true,
                needsRagDocs: false,
                needsOllama: true,
                preferredSection: "Chat",
                answerStyle: "direct_answer_with_selective_context",
                reason: "No deterministic family matched; route to local model with evidence rules.");
        }

        public static bool TryBuildDeterministicAnswer(
            AiWorkflowContextSnapshot? context,
            WorkflowReadinessSnapshot? readiness,
            string? question,
            string? activeScreen,
            string? selectedModel,
            string? modelCapabilitySummary,
            string? benchmarkSummary,
            string? nextAction,
            out string answer,
            out string source,
            out AiIntentDecision decision)
        {
            decision = Classify(context, question, activeScreen);
            answer = string.Empty;
            source = string.Empty;

            var visibleQuestion = string.IsNullOrWhiteSpace(question) ? "How are things?" : question!.Trim();

            // BIM quantity and count-scope semantics are internal application facts.
            // Route them deterministically before any broader intent can reach an
            // Ollama/cloud provider, including natural discrepancy questions such as
            // "Why does Filter show 50 slabs while BIM contains 116?".
            if (AiBimProjectQuestionService.TryBuildAnswer(context, visibleQuestion, activeScreen, out answer, out source))
            {
                if (decision.Intent != AiIntentFamily.ProjectFact)
                {
                    decision = Decision(
                        AiIntentFamily.ProjectFact,
                        "current_project_bim",
                        needsProjectFacts: true,
                        needsRagDocs: false,
                        needsOllama: false,
                        preferredSection: "BIM",
                        answerStyle: "deterministic_bim_fact_with_scope_provenance",
                        factKeys: ExtractFactKeys(Normalize(visibleQuestion)),
                        reason: "BIM fact service accepted the question; provider fallback is prohibited for internal count semantics.");
                }

                return true;
            }

            if (decision.Intent == AiIntentFamily.ProjectFact)
            {
                if (TryBuildProjectFactAnswer(context, visibleQuestion, activeScreen, out answer, out source))
                    return true;
            }

            if (decision.Intent == AiIntentFamily.WorkflowStatus)
            {
                answer = BuildWorkflowStatusAnswer(context, readiness, activeScreen, nextAction);
                source = "Intent-routed deterministic workflow status answer produced from live project/readiness evidence.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.BlockerDiagnosis && context != null)
            {
                var workflowState = AiWorkflowStateClassifier.Classify(context, readiness);
                if (!workflowState.CanRunOptimizerNow)
                {
                    answer = BuildBlockerAnswer(context, visibleQuestion, activeScreen, nextAction);
                    source = "Intent-routed deterministic blocker answer produced from live readiness and screen-context evidence.";
                    return true;
                }
            }

            // Important: explanatory, advisory, delivery, application-value, and ModelOps
            // questions intentionally route to the local LLM with an intent/evidence contract.
            // Deterministic code may provide facts, counts, readiness, and safety blockers,
            // but it must not emit memorized prose for open-ended user questions.

            return false;
        }

        public static string BuildPromptContract(AiWorkflowContextSnapshot? context, string? question, string? activeScreen, string? deterministicNextAction)
        {
            var decision = Classify(context, question, activeScreen);
            var facts = BuildFacts(context, activeScreen);
            var normalizedQuestion = Normalize(question);
            var projectSpecificQuality = decision.Intent == AiIntentFamily.EngineeringQualityAssurance && IsProjectSpecificWording(normalizedQuestion);
            var projectSpecificDelivery = decision.Intent == AiIntentFamily.DeliveryRequirements && IsProjectSpecificWording(normalizedQuestion);
            var builder = new StringBuilder();
            builder.AppendLine("INTENT ROUTING CONTRACT — highest priority");
            builder.AppendLine($"Intent family: {decision.Intent}; scope: {decision.Scope}; answer style: {decision.AnswerStyle}");
            builder.AppendLine($"Scope: {decision.Scope}");
            builder.AppendLine($"Answer style: {decision.AnswerStyle}");
            builder.AppendLine($"Preferred section: {decision.PreferredSection}");
            builder.AppendLine($"Routing reason: {decision.Reason}");
            builder.AppendLine($"Use project facts: {decision.NeedsProjectFacts}");
            builder.AppendLine($"Use local documentation/RAG: {decision.NeedsRagDocs}");
            builder.AppendLine($"Suppress broad application documentation: {ShouldSuppressApplicationContext(decision)}");
            if (!string.IsNullOrWhiteSpace(deterministicNextAction))
                builder.AppendLine($"Deterministic next action, only if relevant: {deterministicNextAction!.Trim()}");
            builder.AppendLine();
            builder.AppendLine("Answer contract:");
            builder.AppendLine("- Start by answering the visible user question directly; do not open with 'This is documentation', 'This appears to be documentation', or 'Here is a summary of the documentation' unless the user explicitly asks for docs.");
            builder.AppendLine("- Use the three knowledge worlds separately: general crane/construction knowledge, current project facts, and software documentation/navigation.");
            builder.AppendLine("- Mention project blockers only when relevant to the question, or as one short separate current-project note after the direct answer.");
            builder.AppendLine("- For counts, areas, crane/load-chart state, zones, readiness, or project facts, use deterministic values only and say when a requested fact is not exposed.");
            builder.AppendLine("- Do not invent hidden buttons, catalog counts, building usage/type, safety certification, site rules, or engineering conclusions.");
            builder.AppendLine("- Do not use or imitate a memorized Q&A answer. Treat this as a fresh user request: infer intent, choose evidence, then synthesize a tailored response.");
            builder.AppendLine("- Deterministic services are allowed to supply facts only; explanatory prose must be generated from the user question plus the supplied evidence.");
            builder.AppendLine();
            builder.AppendLine("Intent-specific response policy:");
            builder.AppendLine("- DomainExplanation: answer the domain concept first using general crane/construction knowledge; do not summarize this application unless the user asks about the application.");
            builder.AppendLine("- EngineeringQualityAssurance: if the user asks about the current project/result, lead with the exact live verification priorities; otherwise explain the validation method first. Always distinguish method confidence from result confidence.");
            builder.AppendLine("- DeliveryRequirements: describe the deliverable package, then clearly mark live items that are ready, blocked, missing, or not exposed. Do not reduce the answer to a GA blocker unless the user asked only about GA.");
            builder.AppendLine("- RealWorldImplementation: answer the field implementation path first: survey/coordinates, BIM/source validation, zones, crane supplier data/load charts, ground/access constraints, lift engineering review, then software execution. Do not turn it into a UI tutorial unless the user asks for buttons.");
            builder.AppendLine("- AppCapabilityExplanation: explain product value in construction-planning terms, then connect it to visible app capabilities and live project state.");
            builder.AppendLine("- ModelOpsRecommendation: use current local model, installed/benchmark/capability evidence if supplied; say what is unknown rather than choosing blindly.");
            builder.AppendLine("- Unknown: infer intent from the wording; do not default to documentation or blocker summaries. Ask for clarification only when necessary.");
            builder.AppendLine();
            if (decision.Intent == AiIntentFamily.EngineeringQualityAssurance)
            {
                builder.AppendLine("Required answer shape for this request:");
                if (projectSpecificQuality)
                {
                    builder.AppendLine("1. Start with the exact current-project verification priorities ranked by live risk facts; do not open with a generic accuracy checklist.");
                    builder.AppendLine("2. Explain the validation method after the project-specific priorities: source data, assumptions, constraints, calculations/optimizer diagnostics, traceability, and engineering review.");
                    builder.AppendLine("3. End with one concrete next verification action and calibrate confidence: facts/method can be high confidence while the blocked result is not trustworthy yet.");
                }
                else
                {
                    builder.AppendLine("1. Directly explain how accuracy is assured in principle: validate source data, assumptions, constraints, calculations, diagnostics, and traceability.");
                    builder.AppendLine("2. Then connect to the current project facts as evidence/risk, not as the opening answer.");
                    builder.AppendLine("3. End with the next verification action and a safety review note.");
                }
                builder.AppendLine();
            }
            else if (decision.Intent == AiIntentFamily.DeliveryRequirements)
            {
                builder.AppendLine("Required answer shape for this request:");
                if (projectSpecificDelivery)
                {
                    builder.AppendLine("1. Start with the current delivery readiness verdict using live facts: what can be reported now, what is missing, and what is blocked.");
                    builder.AppendLine("2. Then list the defensible deliverable package categories.");
                    builder.AppendLine("3. End with the single next action that turns the highest-risk missing item into evidence.");
                }
                else
                {
                    builder.AppendLine("1. State the deliverable package categories first, independent of the current blocker.");
                    builder.AppendLine("2. Mark which deliverables are ready from live facts and which are blocked/missing.");
                    builder.AppendLine("3. End with the next action needed to turn blocked items into deliverable evidence.");
                }
                builder.AppendLine();
            }
            else if (decision.Intent == AiIntentFamily.DomainExplanation)
            {
                builder.AppendLine("Required answer shape for this request:");
                builder.AppendLine("1. Define/explain the construction concept directly.");
                builder.AppendLine("2. Explain why it matters in crane planning/logistics when relevant.");
                builder.AppendLine("3. Do not mention app documentation or current blockers unless explicitly asked.");
                builder.AppendLine();
            }
            else if (decision.Intent == AiIntentFamily.RealWorldImplementation)
            {
                builder.AppendLine("Required answer shape for this request:");
                builder.AppendLine("1. Start with the real construction/engineering implementation sequence, not with application screens.");
                builder.AppendLine("2. Then map the real-world steps to this software's role as a planning aid.");
                builder.AppendLine("3. End with a current-project note that distinguishes ready facts from missing/blocking evidence.");
                builder.AppendLine();
            }
            if (decision.NeedsProjectFacts)
            {
                builder.AppendLine("Current project fact summary for selective use:");
                builder.AppendLine($"- Project: {facts.ProjectName}; screen: {facts.ActiveScreen}; model: {facts.ModelLabel}; rev {facts.ModelRevision:N0}");
                builder.AppendLine($"- BIM objects: {facts.ElementCount:N0}; table: {facts.TableVisible:N0}/{facts.TableTotal:N0}; zones: {facts.ZoneCount:N0} total ({facts.AllowedZoneCount:N0} allowed/green, {facts.ForbiddenZoneCount:N0} forbidden)");
                builder.AppendLine($"- Cranes: {facts.CraneCount:N0}; active load-chart evidence sources: {facts.LoadChartEvidenceCount:N0}; has load-chart evidence: {facts.HasLoadChartEvidence}");
                builder.AppendLine($"- Units: geometry {facts.GeometryUnit}; area {facts.AreaUnit}; element mass display/storage {facts.StoredMassUnit}; threshold/filter mass {facts.ThresholdMassUnit}; concrete density {facts.DensityUnit}");
                if (facts.BuildingArea > 0)
                    builder.AppendLine($"- Building footprint area: {facts.BuildingArea:N2} {facts.BuildingAreaUnit}; height: {facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}");
                if (!string.IsNullOrWhiteSpace(facts.TopCategories))
                    builder.AppendLine($"- Top resolved BIM categories: {facts.TopCategories}");
                if (projectSpecificQuality || projectSpecificDelivery)
                {
                    var workflowState = context == null ? null : AiWorkflowStateClassifier.Classify(context);
                    builder.AppendLine("- Current verification priority facts: treat these as ranked evidence, not a generic checklist.");
                    builder.AppendLine($"  1) Crane setup: {facts.CraneCount:N0} crane(s); load-chart evidence sources: {facts.LoadChartEvidenceCount:N0}; has chart evidence: {facts.HasLoadChartEvidence}.");
                    builder.AppendLine($"  2) Site constraints: {facts.AllowedZoneCount:N0} allowed/green zone(s), {facts.ForbiddenZoneCount:N0} forbidden/no-go zone(s), {facts.ZoneCount:N0} total zone(s).");
                    builder.AppendLine($"  3) Model/lift evidence: {facts.ElementCount:N0} BIM/model object(s); table scope {facts.TableVisible:N0}/{facts.TableTotal:N0}; model area {(facts.BuildingArea > 0 ? facts.BuildingArea.ToString("0.##") + " " + facts.BuildingAreaUnit : "not exposed")}.");
                    if (workflowState != null)
                        builder.AppendLine($"  4) Readiness state: {workflowState.Kind}; can run optimizer now: {workflowState.CanRunOptimizerNow}; root cause: {workflowState.RootCause}");
                }
            }
            else
            {
                builder.AppendLine("Current project facts are intentionally not supplied for this concept question unless the user asks to connect it to the open project.");
            }

            return builder.ToString().Trim();
        }

        public static string BuildModelOpsEvidence(string? selectedModel, string? modelCapabilitySummary, string? benchmarkSummary)
        {
            var builder = new StringBuilder();
            builder.AppendLine("LOCAL MODEL / MODEL OPS EVIDENCE — factual context only");
            builder.AppendLine($"- Selected model: {(string.IsNullOrWhiteSpace(selectedModel) ? "not selected" : selectedModel!.Trim())}");
            builder.AppendLine($"- Benchmark summary: {(string.IsNullOrWhiteSpace(benchmarkSummary) ? "no benchmark result is visible" : benchmarkSummary!.Trim())}");
            builder.AppendLine($"- Capability summary: {(string.IsNullOrWhiteSpace(modelCapabilitySummary) ? "no model capability recommendation has been computed" : modelCapabilitySummary!.Trim())}");
            builder.AppendLine("Use this evidence for ModelOps questions. Do not claim a model is best unless benchmark/capability evidence supports it.");
            return builder.ToString().Trim();
        }

        public static bool ShouldSuppressApplicationContext(AiIntentDecision decision)
        {
            return decision.Intent == AiIntentFamily.DomainExplanation
                || decision.Intent == AiIntentFamily.ProjectFact
                || decision.Intent == AiIntentFamily.WorkflowStatus
                || decision.Intent == AiIntentFamily.BlockerDiagnosis
                || decision.Intent == AiIntentFamily.ModelOpsRecommendation
                || decision.Intent == AiIntentFamily.EngineeringQualityAssurance
                || decision.Intent == AiIntentFamily.DeliveryRequirements
                || decision.Intent == AiIntentFamily.RealWorldImplementation
                || decision.Intent == AiIntentFamily.WhatIfScenario
                || decision.Intent == AiIntentFamily.Unknown;
        }

        public static bool ShouldSuppressApplicationContext(AiWorkflowContextSnapshot? context, string? question, string? activeScreen)
            => ShouldSuppressApplicationContext(Classify(context, question, activeScreen));


        public static bool ShouldUseStrictWorkflowBlockerRouting(AiWorkflowContextSnapshot? context, string? question, string? activeScreen)
        {
            if (!AiWorkflowEvidenceTextBuilder.ShouldUseStrictNoGaCandidateRouting(context, question))
                return false;

            var decision = Classify(context, question, activeScreen);
            return decision.Intent == AiIntentFamily.BlockerDiagnosis
                || decision.Intent == AiIntentFamily.WorkflowStatus
                || decision.Intent == AiIntentFamily.WhatIfScenario;
        }

        public static bool ShouldIncludeDetailedWorkflowEvidence(AiIntentDecision decision)
        {
            return decision.Intent == AiIntentFamily.WorkflowStatus
                || decision.Intent == AiIntentFamily.BlockerDiagnosis
                || decision.Intent == AiIntentFamily.WhatIfScenario
                || decision.Intent == AiIntentFamily.ReportGeneration;
        }

        public static bool ShouldIncludeQualityWorkflowEvidence(AiIntentDecision decision)
        {
            // The compact evidence packet already carries the live project facts.
            // Full screen-context dumps are reserved for detailed workflow/blocker
            // intents only, otherwise low-parameter local models over-focus on the
            // largest warning and ignore the user's actual question.
            return false;
        }

        public static bool ShouldIncludeOptimizationResultSection(AiIntentDecision decision)
        {
            return decision.Intent == AiIntentFamily.WorkflowStatus
                || decision.Intent == AiIntentFamily.BlockerDiagnosis
                || decision.Intent == AiIntentFamily.WhatIfScenario
                || decision.Intent == AiIntentFamily.ReportGeneration;
        }

        public static bool ShouldIncludeAdvisorFindings(AiIntentDecision decision)
        {
            return decision.Intent == AiIntentFamily.WorkflowStatus
                || decision.Intent == AiIntentFamily.BlockerDiagnosis
                || decision.Intent == AiIntentFamily.WhatIfScenario
                || decision.Intent == AiIntentFamily.ReportGeneration;
        }

        public static bool ShouldIncludeLocalDocumentationContext(AiIntentDecision decision)
        {
            return decision.NeedsRagDocs
                || decision.Intent == AiIntentFamily.UiNavigation
                || decision.Intent == AiIntentFamily.AppCapabilityExplanation
                || decision.Intent == AiIntentFamily.ReportGeneration;
        }

        public static string BuildEvidenceRoutingPacket(
            AiWorkflowContextSnapshot? context,
            WorkflowReadinessSnapshot? readiness,
            string? question,
            string? activeScreen,
            string? deterministicNextAction)
        {
            var decision = Classify(context, question, activeScreen);
            var facts = BuildFacts(context, activeScreen);
            var workflowState = context == null ? null : AiWorkflowStateClassifier.Classify(context, readiness);
            var normalizedQuestion = Normalize(question);
            var projectSpecificQuality = decision.Intent == AiIntentFamily.EngineeringQualityAssurance && IsProjectSpecificWording(normalizedQuestion);
            var projectSpecificDelivery = decision.Intent == AiIntentFamily.DeliveryRequirements && IsProjectSpecificWording(normalizedQuestion);
            var builder = new StringBuilder();

            builder.AppendLine("GATED EVIDENCE PACKET — use silently; do not print section names");
            builder.AppendLine($"Visible question: {NormalizeForPrompt(question, "empty question")}");
            builder.AppendLine($"Intent: {decision.Intent}; evidence scope: {decision.Scope}; answer style: {decision.AnswerStyle}");
            builder.AppendLine("Allowed evidence worlds:");
            builder.AppendLine(decision.Intent == AiIntentFamily.DomainExplanation
                ? "- General crane/construction domain knowledge is primary. Do not use software documentation or optimizer blockers as the answer."
                : "- Answer from the visible question and only the deterministic facts that match the intent.");
            builder.AppendLine(decision.NeedsProjectFacts
                ? "- Current project facts are allowed when relevant; use them as evidence, not as a substitute for answering the question."
                : "- Current project facts are not required; do not let project blockers hijack the answer.");
            builder.AppendLine(decision.NeedsRagDocs
                ? "- Local documentation may be used because the user asked for software/documentation context."
                : "- Local documentation is not an answer source for this question unless the user explicitly asks for docs.");
            builder.AppendLine();

            if (decision.NeedsProjectFacts)
            {
                builder.AppendLine("Compact deterministic project facts available for selective use:");
                builder.AppendLine($"- Project: {facts.ProjectName}; active screen: {facts.ActiveScreen}; model: {facts.ModelLabel}; revision: {facts.ModelRevision:N0}");
                builder.AppendLine($"- BIM/model objects: {facts.ElementCount:N0}; table scope: {facts.TableVisible:N0}/{facts.TableTotal:N0}");
                builder.AppendLine($"- Zones: {facts.ZoneCount:N0} total; {facts.AllowedZoneCount:N0} allowed/green; {facts.ForbiddenZoneCount:N0} forbidden");
                builder.AppendLine($"- Cranes: {facts.CraneCount:N0}; load-chart evidence sources: {facts.LoadChartEvidenceCount:N0}; has load-chart evidence: {facts.HasLoadChartEvidence}");
                builder.AppendLine($"- Units visible to AI: geometry {facts.GeometryUnit}; area {facts.AreaUnit}; mass {facts.StoredMassUnit} for element display/storage; {facts.ThresholdMassUnit} for weight thresholds; density {facts.DensityUnit}");
                if (facts.BuildingArea > 0 || facts.BuildingHeight > 0)
                    builder.AppendLine($"- Building/model metrics: area {facts.BuildingArea:N2} {facts.BuildingAreaUnit}; height {facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}");
                if (projectSpecificQuality || projectSpecificDelivery)
                {
                    builder.AppendLine("Project-specific risk priorities for this question:");
                    builder.AppendLine($"- Crane/load-chart readiness: {facts.CraneCount:N0} crane(s), {facts.LoadChartEvidenceCount:N0} load-chart evidence source(s), has chart evidence {facts.HasLoadChartEvidence}.");
                    builder.AppendLine($"- Zone readiness: {facts.AllowedZoneCount:N0} allowed/green zone(s), {facts.ForbiddenZoneCount:N0} forbidden/no-go zone(s), total {facts.ZoneCount:N0}.");
                    builder.AppendLine($"- Model evidence: {facts.ElementCount:N0} BIM/model object(s); element table {facts.TableVisible:N0}/{facts.TableTotal:N0}; optimization result available {facts.HasOptimizationResult}.");
                    if (workflowState != null)
                        builder.AppendLine($"- Workflow readiness: {workflowState.Kind}; can run optimizer now {workflowState.CanRunOptimizerNow}; root cause {workflowState.RootCause}; recommended action {workflowState.RecommendedAction}.");
                    builder.AppendLine("For project-specific QA/delivery questions, these risk priorities must drive the answer before any generic checklist.");
                }
            }

            if (readiness != null && decision.NeedsProjectFacts && ShouldIncludeDetailedWorkflowEvidence(decision))
            {
                builder.AppendLine($"- Readiness: {readiness.CompleteCount}/{readiness.TotalCount} complete; {readiness.MissingCriticalCount} critical missing; stage {readiness.CurrentStage}");
                if (!string.IsNullOrWhiteSpace(readiness.Summary))
                    builder.AppendLine($"- Readiness summary: {readiness.Summary.Trim()}");
            }

            if (workflowState != null && ShouldIncludeDetailedWorkflowEvidence(decision))
            {
                builder.AppendLine($"- Workflow state: {workflowState.Kind}; can run optimizer now: {workflowState.CanRunOptimizerNow}");
                builder.AppendLine($"- Workflow root cause: {workflowState.RootCause}");
                builder.AppendLine($"- Workflow recommended action: {workflowState.RecommendedAction}");
            }
            else if (workflowState != null && decision.Intent == AiIntentFamily.DeliveryRequirements)
            {
                builder.AppendLine(projectSpecificDelivery
                    ? "- Delivery instruction: because the question is about the current project, start by separating ready deliverable evidence from blocked/missing evidence."
                    : "- Delivery caveat for later in the answer: optimization-result deliverables are not ready until optimizer readiness is resolved; do not make this the opening answer.");
            }
            else if (workflowState != null && decision.Intent == AiIntentFamily.EngineeringQualityAssurance)
            {
                builder.AppendLine(projectSpecificQuality
                    ? "- Accuracy instruction: because the question is about this result/project, start with the live verification priorities and readiness risks; then explain the QA method."
                    : "- Accuracy caveat for later in the answer: current project evidence has unresolved readiness issues, so result accuracy must be verified before optimization/reporting; do not make this the opening answer.");
            }
            else if (workflowState != null && decision.Intent == AiIntentFamily.RealWorldImplementation)
            {
                builder.AppendLine("- Field implementation caveat for later in the answer: the current software project is a planning model, not a certified field lift plan; mention unresolved readiness only after the real-world implementation sequence.");
            }
            else if (workflowState != null && workflowState.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked && decision.NeedsProjectFacts)
            {
                builder.AppendLine("- Separate caveat only if relevant: optimization is blocked by lift-unit readiness; do not make this the main answer unless the user asks about workflow/GA/blockers/next steps.");
            }

            if (!string.IsNullOrWhiteSpace(deterministicNextAction) && ShouldIncludeDetailedWorkflowEvidence(decision))
                builder.AppendLine($"- Deterministic next action, if relevant: {deterministicNextAction!.Trim()}");

            builder.AppendLine();
            builder.AppendLine("Response policy for this request:");
            builder.AppendLine("- First sentence must answer the user's actual question, not describe documentation or the application context.");
            builder.AppendLine("- Use headings only when they help; keep the first screen concise and useful.");
            builder.AppendLine("- If a deterministic fact is unavailable, say it is unavailable instead of estimating.");
            builder.AppendLine("- Never certify a crane lift plan; final plans require qualified engineering review.");
            return builder.ToString().Trim();
        }

        public static string BuildRepairContext(AiWorkflowContextSnapshot? context, string? question, string? activeScreen, string? deterministicNextAction)
        {
            var decision = Classify(context, question, activeScreen);
            var facts = BuildFacts(context, activeScreen);
            var builder = new StringBuilder();
            builder.AppendLine("MINIMAL REPAIR CONTEXT — use this instead of any previous oversized context");
            builder.AppendLine($"Visible question: {NormalizeForPrompt(question, "empty question")}");
            builder.AppendLine($"Intent family: {decision.Intent}; scope: {decision.Scope}; answer style: {decision.AnswerStyle}");
            builder.AppendLine("Repair objective: answer the visible question directly and remove documentation-summary, generic-checklist, or blocker-hijack behavior.");
            builder.AppendLine();
            builder.AppendLine("Allowed evidence:");
            if (decision.Intent == AiIntentFamily.DomainExplanation)
            {
                builder.AppendLine("- General crane/construction/logistics knowledge only.");
                builder.AppendLine("- Do not mention current project blockers unless the user explicitly asks to connect the concept to this project.");
            }
            else
            {
                builder.AppendLine($"- Project: {facts.ProjectName}; model: {facts.ModelLabel}; BIM objects: {facts.ElementCount:N0}; zones: {facts.ZoneCount:N0} ({facts.AllowedZoneCount:N0} allowed, {facts.ForbiddenZoneCount:N0} forbidden); cranes: {facts.CraneCount:N0}; load-chart evidence sources: {facts.LoadChartEvidenceCount:N0}; building area: {(facts.BuildingArea > 0 ? facts.BuildingArea.ToString("0.##") + " " + facts.BuildingAreaUnit : "not exposed")}");
                builder.AppendLine($"- Units: geometry {facts.GeometryUnit}; area {facts.AreaUnit}; element mass {facts.StoredMassUnit}; thresholds {facts.ThresholdMassUnit}; density {facts.DensityUnit}");
                if (decision.Scope == "project_quality_review" || decision.Scope == "project_delivery_readiness")
                {
                    var workflowState = context == null ? null : AiWorkflowStateClassifier.Classify(context);
                    builder.AppendLine($"- Project risk priorities: cranes {facts.CraneCount:N0}; load-chart evidence {facts.LoadChartEvidenceCount:N0}; zones {facts.ZoneCount:N0} ({facts.AllowedZoneCount:N0} allowed, {facts.ForbiddenZoneCount:N0} forbidden); optimization result {facts.HasOptimizationResult}.");
                    if (workflowState != null)
                        builder.AppendLine($"- Readiness root cause: {workflowState.Kind}; can run optimizer now {workflowState.CanRunOptimizerNow}; {workflowState.RootCause}");
                }
                if (!string.IsNullOrWhiteSpace(deterministicNextAction) && ShouldIncludeDetailedWorkflowEvidence(decision))
                    builder.AppendLine($"- Deterministic next action: {deterministicNextAction!.Trim()}");
            }
            builder.AppendLine();
            builder.AppendLine("Forbidden repair behavior:");
            builder.AppendLine("- Do not start with 'current workflow is blocked', 'unable to provide a crane lift plan', or 'this appears to be documentation'.");
            builder.AppendLine("- Do not summarize application documentation unless the visible question asks for documentation.");
            builder.AppendLine("- Do not give a generic template; tailor the answer to the user wording and the compact facts above.");
            builder.AppendLine("- If this is a current-project QA/delivery question, the first useful content must be the live project verification/delivery priorities, not an abstract checklist.");
            builder.AppendLine("- Never certify a crane lift plan; mention engineering review when safety/accuracy/results are discussed.");
            return builder.ToString().Trim();
        }

        public static bool NeedsResponseRepair(AiIntentDecision decision, string? answer, out string reason)
        {
            reason = string.Empty;
            var text = Normalize(answer);
            if (string.IsNullOrWhiteSpace(text))
            {
                reason = "The response is empty.";
                return true;
            }

            var first = text.Length > 260 ? text.Substring(0, 260) : text;
            if (ContainsAny(first,
                    "this appears to be a documentation",
                    "this appears to be documentation",
                    "appears to be a documentation",
                    "here's a summary of the main",
                    "here is a summary of the main",
                    "comprehensive documentation",
                    "documentation for a crane placement optimization software",
                    "local documentation context"))
            {
                reason = "The response opened as a documentation summary instead of answering the visible question.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.DomainExplanation
                && !decision.NeedsProjectFacts
                && ContainsAny(text, "current project caveat", "current project note", "optimization is blocked", "optimizer is blocked", "workflow state", "ga is blocked", "current project is blocked", "blocked by lift-unit", "next click"))
            {
                reason = "A general domain explanation was hijacked by current workflow/blocker context.";
                return true;
            }

            if ((decision.Intent == AiIntentFamily.EngineeringQualityAssurance || decision.Intent == AiIntentFamily.DeliveryRequirements)
                && (first.StartsWith("the current workflow state is blocked", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the current workflow state is liftunit", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the optimizer is blocked", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the ai copilot is unable", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("unable to provide", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the current workflow state", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the reason for this blockage", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("to proceed with optimization", StringComparison.OrdinalIgnoreCase)
                    || ContainsAny(first, "ga has zero trusted", "zero trusted ga-ready", "liftunitreadinessblocked")))
            {
                reason = "An advisory question was opened as a blocker diagnosis instead of a direct advisory answer.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.EngineeringQualityAssurance
                && !ContainsAny(text, "source", "assumption", "constraint", "diagnostic", "traceability", "load chart", "zone", "engineering review", "qualified"))
            {
                reason = "The accuracy answer did not cover the expected engineering evidence chain.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.EngineeringQualityAssurance
                && string.Equals(decision.Scope, "project_quality_review", StringComparison.OrdinalIgnoreCase))
            {
                var hasProjectEvidence = ContainsAny(text, "current project", "this project", "project evidence", "live", "readiness", "crane", "load-chart", "load chart", "zone", "bim", "ifc");
                var opensGeneric = ContainsAny(first, "accuracy is assured in principle", "to ensure accuracy", "to ensure accuracy and trustworthiness", "verification method");
                var opensWithProject = ContainsAny(first, "this project", "current project", "before trusting this result", "first verify", "do not trust", "verify the live");
                if (!hasProjectEvidence || (opensGeneric && !opensWithProject))
                {
                    reason = "The project-specific accuracy answer was still generic instead of being driven by live project verification priorities.";
                    return true;
                }
            }

            if (decision.Intent == AiIntentFamily.RealWorldImplementation
                && (ContainsAny(first, "click", "tab", "button", "use our application", "use the application", "create an ifc site model")
                    || !ContainsAny(text, "site", "survey", "ground", "load chart", "engineering review", "manufacturer", "field")))
            {
                reason = "The implementation answer did not lead with a real field/engineering workflow.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.DeliveryRequirements
                && string.Equals(decision.Scope, "project_delivery_readiness", StringComparison.OrdinalIgnoreCase))
            {
                var hasDeliveryEvidence = ContainsAny(text, "ready", "missing", "blocked", "crane", "load-chart", "load chart", "zone", "bim", "ifc", "optimization");
                if (!hasDeliveryEvidence
                    || first.StartsWith("a defensible crane", StringComparison.OrdinalIgnoreCase)
                    || first.StartsWith("the deliverable package", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "The project-specific delivery answer did not start from current ready/missing/blocked evidence.";
                    return true;
                }
            }

            if (decision.Intent == AiIntentFamily.AppCapabilityExplanation
                && ContainsAny(first, "this appears", "documentation", "workflow and functionality"))
            {
                reason = "The application-value answer sounded like a documentation summary instead of explaining value.";
                return true;
            }

            if ((decision.Intent == AiIntentFamily.EngineeringQualityAssurance
                    || decision.Intent == AiIntentFamily.DeliveryRequirements
                    || decision.Intent == AiIntentFamily.RealWorldImplementation)
                && ContainsAny(text, "confidence high", "high confidence", "confidence high")
                && ContainsAny(text, "blocked", "not ready", "missing", "zero trusted")
                && !ContainsAny(text, "low confidence in", "not confidence in", "high confidence in the method", "high confidence in the verification"))
            {
                reason = "The response did not calibrate confidence separately from the blocked project/result state.";
                return true;
            }

            if (decision.Intent == AiIntentFamily.ProjectFact
                && ContainsAny(text, "pounds-force", "lbf", "imperial")
                && ContainsAny(text, "what units", "units used", "unit used"))
            {
                reason = "A project-unit answer introduced unsupported imperial units instead of using visible project units.";
                return true;
            }

            return false;
        }

        public static string BuildResponseRepairPrompt(string userQuestion, string previousAnswer, AiIntentDecision decision, string repairReason)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Rewrite your previous answer because it violated the intent/evidence contract.");
            builder.AppendLine($"Reason: {repairReason}");
            builder.AppendLine($"User question: {NormalizeForPrompt(userQuestion, "empty question")}");
            builder.AppendLine($"Intent family: {decision.Intent}; scope: {decision.Scope}; answer style: {decision.AnswerStyle}");
            builder.AppendLine();
            builder.AppendLine("Rewrite rules:");
            builder.AppendLine("- Start with a direct answer to the user's question in plain professional language.");
            builder.AppendLine("- Do not say the material appears to be documentation.");
            builder.AppendLine("- Do not summarize software features unless the question is about the software.");
            builder.AppendLine("- Do not let optimizer/blocker context replace the answer; use it only in a separate Current project note when relevant.");
            builder.AppendLine("- If the intent is EngineeringQualityAssurance and the scope is project_quality_review, start from live project verification priorities first; otherwise discuss validation of inputs, assumptions, constraints, calculations/optimizer diagnostics, traceability, and engineering review before mentioning blockers.");
            builder.AppendLine("- If the intent is DeliveryRequirements and the scope is project_delivery_readiness, start from current ready/missing/blocked evidence first; otherwise distinguish the deliverable package from current missing/blocking items. Do not say simply that the copilot cannot provide a lift plan.");
            builder.AppendLine("- If the intent is RealWorldImplementation, start with real site/survey/BIM/crane supplier/load-chart/ground/access/engineering-review steps before mentioning application screens.");
            builder.AppendLine("- Do not use a canned Q&A response; synthesize a fresh concise answer from the question and allowed evidence.");
            builder.AppendLine();
            builder.AppendLine("Previous answer to repair:");
            builder.AppendLine(previousAnswer ?? string.Empty);
            return builder.ToString().Trim();
        }

        /// <summary>
        /// Last-resort answer builder used only after the local model violates the
        /// intent/evidence contract. It is deliberately intent-based rather than
        /// phrase-based: deterministic code supplies facts and a safe answer shape,
        /// while the user's wording still determines which evidence world is used.
        /// </summary>
        public static bool TryBuildGroundedFallbackAnswer(
            AiWorkflowContextSnapshot? context,
            WorkflowReadinessSnapshot? readiness,
            string? question,
            string? activeScreen,
            string? selectedModel,
            string? modelCapabilitySummary,
            string? benchmarkSummary,
            string? deterministicNextAction,
            out string answer,
            out string source)
        {
            answer = string.Empty;
            source = string.Empty;

            var decision = Classify(context, question, activeScreen);
            var facts = BuildFacts(context, activeScreen);
            var workflowState = context == null ? null : AiWorkflowStateClassifier.Classify(context, readiness);

            switch (decision.Intent)
            {
                case AiIntentFamily.EngineeringQualityAssurance:
                    answer = BuildQualityFallback(facts, workflowState, deterministicNextAction, question);
                    source = "Intent-grounded fallback answer for engineering quality/accuracy after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.DeliveryRequirements:
                    answer = BuildDeliveryFallback(facts, workflowState, deterministicNextAction, question);
                    source = "Intent-grounded fallback answer for delivery requirements after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.RealWorldImplementation:
                    answer = BuildRealWorldImplementationFallback(facts, workflowState, deterministicNextAction);
                    source = "Intent-grounded fallback answer for field implementation after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.AppCapabilityExplanation:
                    answer = BuildApplicationValueFallback(facts, workflowState);
                    source = "Intent-grounded fallback answer for application value after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.ModelOpsRecommendation:
                    answer = BuildModelOpsFallback(selectedModel, modelCapabilitySummary, benchmarkSummary);
                    source = "Intent-grounded fallback answer for local model selection after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.DomainExplanation:
                    answer = BuildDomainFallback(question);
                    source = "Intent-grounded fallback answer for a construction domain explanation after local model response failed the quality gate.";
                    return true;

                case AiIntentFamily.Unknown:
                    answer = BuildUnknownFallback(facts, workflowState, question);
                    source = "Intent-grounded fallback answer for an open/mixed question after local model response failed the quality gate.";
                    return true;
            }

            return false;
        }

        private static string BuildQualityFallback(ProjectFacts facts, AiWorkflowState? workflowState, string? nextAction, string? question)
        {
            var projectSpecific = IsProjectSpecificWording(Normalize(question));
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");

            if (projectSpecific)
            {
                builder.AppendLine("Before trusting this project result, verify the live blockers first. The exposed evidence shows the planning model is not ready for a trusted optimization result until lift-unit readiness, crane selection, load-chart evidence, and zone assumptions are checked and documented.");
                builder.AppendLine();
                builder.AppendLine("## Current project verification priorities");
                if (workflowState != null && !workflowState.CanRunOptimizerNow)
                    builder.AppendLine($"1. **Readiness blocker:** {workflowState.Kind} — {workflowState.RootCause}");
                else
                    builder.AppendLine("1. **Readiness state:** no active workflow blocker is exposed, but the result still requires evidence review before use.");
                builder.AppendLine($"2. **Crane and chart evidence:** {facts.CraneCount:N0} crane(s) configured and {facts.LoadChartEvidenceCount:N0} active load-chart evidence source(s). Verify the crane model, configuration, manufacturer chart, radius, capacity, derating, rigging/additional load, and chart units before trusting any lift result.");
                builder.AppendLine($"3. **Lift-object evidence:** {facts.ElementCount:N0} BIM/model object(s), table scope {facts.TableVisible:N0}/{facts.TableTotal:N0}. Verify trusted mass/weight source, COG, assembly/lift marks, geometry, and unresolved-data warnings.");
                builder.AppendLine($"4. **Site constraints:** {facts.AllowedZoneCount:N0} allowed/green zone(s), {facts.ForbiddenZoneCount:N0} forbidden/no-go zone(s), {facts.ZoneCount:N0} total zone(s). Confirm these represent real access, exclusion, swing, ground/outrigger, and site boundary constraints.");
                if (facts.BuildingArea > 0 || facts.BuildingHeight > 0)
                    builder.AppendLine($"5. **Model metrics and units:** building/model area {facts.BuildingArea:N2} {facts.BuildingAreaUnit}, height {facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}; geometry {facts.GeometryUnit}, area {facts.AreaUnit}, mass {facts.StoredMassUnit}, thresholds {facts.ThresholdMassUnit}, density {facts.DensityUnit}.");
                builder.AppendLine();
                builder.AppendLine("## Accuracy method");
                builder.AppendLine("After those project-specific checks, validate the calculation chain: source data → assumptions → constraints → crane/load-chart capacity check → optimizer diagnostics → exported report traceability. Any failed or missing item should appear as an explicit caveat in the report, not as an implied assumption.");
            }
            else
            {
                builder.AppendLine("Accuracy is assured by proving the full chain of evidence, not by trusting the optimizer output alone. For crane planning, that means validating the source BIM/site data, crane/load-chart data, allowed and forbidden zones, unit consistency, lift-object weights, assumptions, optimizer diagnostics, and report traceability before any result is used for engineering review.");
                builder.AppendLine();
                builder.AppendLine("## Verification method");
                builder.AppendLine("- **Input evidence:** confirm IFC geometry, coordinates, element identity, COG/lift marks, and trusted mass/weight sources.");
                builder.AppendLine("- **Unit consistency:** keep geometry, area, mass, density, radius, capacity, and report units consistent; never mix metric and imperial chart values.");
                builder.AppendLine("- **Crane evidence:** verify the selected crane configuration, manufacturer load chart, radius, capacity, derating, rigging/additional load, and setup assumptions.");
                builder.AppendLine("- **Site constraints:** confirm allowed zones, forbidden/no-go zones, access, swing clearance, ground/outrigger assumptions, and any project-specific exclusion zones.");
                builder.AppendLine("- **Result audit:** review optimizer diagnostics, failed elements, dominant failure mode, and exported evidence before reporting.");
                builder.AppendLine();
                builder.AppendLine("## Current project evidence");
                builder.AppendLine($"- Project: **{facts.ProjectName}**; BIM/model objects: **{facts.ElementCount:N0}**; zones: **{facts.ZoneCount:N0}** total (**{facts.AllowedZoneCount:N0} allowed**, **{facts.ForbiddenZoneCount:N0} forbidden**).");
                builder.AppendLine($"- Cranes: **{facts.CraneCount:N0}**; active load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**.");
                if (facts.BuildingArea > 0 || facts.BuildingHeight > 0)
                    builder.AppendLine($"- Building/model metrics: **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}** area, **{facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}** height.");
                builder.AppendLine($"- Units: geometry **{facts.GeometryUnit}**, area **{facts.AreaUnit}**, element mass **{facts.StoredMassUnit}**, thresholds **{facts.ThresholdMassUnit}**, density **{facts.DensityUnit}**.");
                if (workflowState != null && !workflowState.CanRunOptimizerNow)
                    builder.AppendLine($"- Current readiness risk: **{workflowState.Kind}** — {workflowState.RootCause}");
            }

            builder.AppendLine();
            builder.AppendLine("## Next verification action");
            builder.AppendLine(string.IsNullOrWhiteSpace(nextAction) ? "Export/review the element list and verify trusted weight, COG, lift-mark, zone, crane, and load-chart evidence before running or reporting optimization." : nextAction!.Trim());
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High confidence in the visible facts and verification method; do not treat the crane-placement result itself as high-confidence until the missing evidence, optimizer diagnostics, and qualified engineering review are complete.");
            return builder.ToString().Trim();
        }

        private static string BuildDeliveryFallback(ProjectFacts facts, AiWorkflowState? workflowState, string? nextAction, string? question)
        {
            var projectSpecific = IsProjectSpecificWording(Normalize(question));
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            if (projectSpecific)
            {
                builder.AppendLine("For the current project, the delivery package is not complete yet: the model and zone evidence can be reported, but crane/load-chart evidence and trusted lift-unit readiness must be resolved before a defensible optimization-result delivery can be issued.");
                builder.AppendLine();
                builder.AppendLine("## Current delivery readiness");
                builder.AppendLine($"- **Ready to describe:** project/model context, **{facts.ElementCount:N0}** BIM/model object(s), building/model area **{(facts.BuildingArea > 0 ? facts.BuildingArea.ToString("0.##") + " " + facts.BuildingAreaUnit : "not exposed")}**, height **{(facts.BuildingHeight > 0 ? facts.BuildingHeight.ToString("0.##") + " " + facts.BuildingLengthUnit : "not exposed")}**, and **{facts.ZoneCount:N0}** zone(s) (**{facts.AllowedZoneCount:N0} allowed**, **{facts.ForbiddenZoneCount:N0} forbidden**).");
                builder.AppendLine($"- **Missing for optimization delivery:** selected crane count **{facts.CraneCount:N0}**, active load-chart evidence sources **{facts.LoadChartEvidenceCount:N0}**, optimization result available **{facts.HasOptimizationResult}**.");
                if (workflowState != null && !workflowState.CanRunOptimizerNow)
                    builder.AppendLine($"- **Blocked item:** {workflowState.Kind} — {workflowState.RootCause}");
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine("A defensible crane-planning delivery package should include the verified input model, site/zone constraints, crane and load-chart evidence, lift-object evidence, optimization result or reason no result is available, diagnostics, and a clear engineering-review note. The package should separate what is ready from what is still missing.");
                builder.AppendLine();
            }

            builder.AppendLine("## Deliverable package");
            builder.AppendLine("- Verified IFC/BIM element list with geometry, COG/lift marks, weights/mass source, and unresolved-data warnings.");
            builder.AppendLine("- Site layout evidence: coordinate reference, allowed crane areas, forbidden/no-go zones, access, ground/outrigger, swing, and exclusion assumptions.");
            builder.AppendLine("- Crane evidence: selected crane model/configuration, manufacturer load charts, radius/capacity assumptions, derating, rigging/additional load, and chart units.");
            builder.AppendLine("- Optimization evidence: run settings, candidate setup positions, coverage, failed elements, dominant blocker/failure categories, and diagnostics export.");
            builder.AppendLine("- Report package: lifting summary, site-zones report, element list, project overview, and engineering-review/safety note.");
            builder.AppendLine();
            if (!projectSpecific)
            {
                builder.AppendLine("## Current project readiness");
                builder.AppendLine($"- BIM/model objects: **{facts.ElementCount:N0}**; zones: **{facts.ZoneCount:N0}** total (**{facts.AllowedZoneCount:N0} allowed**, **{facts.ForbiddenZoneCount:N0} forbidden**).");
                builder.AppendLine($"- Cranes: **{facts.CraneCount:N0}**; load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**; optimization result available: **{facts.HasOptimizationResult}**.");
                if (workflowState != null && !workflowState.CanRunOptimizerNow)
                    builder.AppendLine($"- Blocked/missing: **{workflowState.RootCause}**");
                builder.AppendLine();
            }
            builder.AppendLine("## Next action");
            builder.AppendLine(string.IsNullOrWhiteSpace(nextAction) ? "Resolve the highest-risk missing crane/load-chart/lift-unit evidence, then run GA and export the diagnostics/report package." : nextAction!.Trim());
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High confidence in the delivery structure and visible readiness facts; incomplete confidence in final optimization deliverables until missing evidence is resolved and reviewed.");
            return builder.ToString().Trim();
        }

        private static string BuildRealWorldImplementationFallback(ProjectFacts facts, AiWorkflowState? workflowState, string? nextAction)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("In real life, implement this as an engineering workflow first and a software workflow second. The app can organize and test crane-placement options, but field implementation still depends on verified survey data, crane supplier data, ground/access checks, and qualified lift-plan review.");
            builder.AppendLine();
            builder.AppendLine("## Field implementation path");
            builder.AppendLine("1. Confirm site survey, coordinate reference, building footprint, access routes, exclusion zones, and ground/outrigger assumptions.");
            builder.AppendLine("2. Validate the BIM/IFC lift objects: geometry, COG, weights, assembly/lift marks, and source confidence.");
            builder.AppendLine("3. Define allowed crane setup areas and forbidden/no-go areas from real site constraints.");
            builder.AppendLine("4. Select candidate crane models with the crane supplier and verify manufacturer load charts, configuration, radius, capacity, counterweight, rigging, derating, and safety factors.");
            builder.AppendLine("5. Use the software to test feasible crane positions, coverage, and failure reasons; export diagnostics and reports.");
            builder.AppendLine("6. Have the final lift plan reviewed against manufacturer data, site rules, project requirements, and applicable safety standards before field use.");
            builder.AppendLine();
            builder.AppendLine("## Current project note");
            builder.AppendLine($"This project currently exposes **{facts.ElementCount:N0}** BIM/model objects, **{facts.ZoneCount:N0}** zone(s), **{facts.CraneCount:N0}** crane(s), and **{facts.LoadChartEvidenceCount:N0}** active load-chart evidence source(s).");
            if (workflowState != null && !workflowState.CanRunOptimizerNow)
                builder.AppendLine($"The software planning model is not ready for a reviewed optimization result yet: {workflowState.RootCause}");
            builder.AppendLine();
            builder.AppendLine("## Next click");
            builder.AppendLine(string.IsNullOrWhiteSpace(nextAction) ? "Start with **CRANES → MOBILE CRANE** and then verify load-chart evidence, zones, and trusted lift-unit data before running GA." : nextAction!.Trim());
            return builder.ToString().Trim();
        }

        private static string BuildApplicationValueFallback(ProjectFacts facts, AiWorkflowState? workflowState)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("This application is important because crane placement is a high-impact construction logistics problem: the wrong crane position, capacity assumption, zone boundary, or lift-object weight can cause unsafe plans, poor coverage, delays, and expensive rework. The software helps turn BIM/site data into traceable crane-planning evidence before a human engineer reviews the final plan.");
            builder.AppendLine();
            builder.AppendLine("## Why it matters");
            builder.AppendLine("- It links BIM elements, site zones, crane setup positions, load-chart evidence, and optimizer diagnostics in one workflow.");
            builder.AppendLine("- It helps expose why elements are not liftable: radius, weight/capacity, missing chart data, missing trusted lift-unit evidence, or zone constraints.");
            builder.AppendLine("- It supports defensible reporting by separating model facts, assumptions, optimization results, and safety review notes.");
            builder.AppendLine();
            builder.AppendLine("## Current project note");
            builder.AppendLine($"The open project has **{facts.ElementCount:N0}** BIM/model objects, **{facts.ZoneCount:N0}** zone(s), **{facts.CraneCount:N0}** crane(s), and **{facts.LoadChartEvidenceCount:N0}** active load-chart evidence source(s).");
            if (workflowState != null && !workflowState.CanRunOptimizerNow)
                builder.AppendLine($"Current readiness issue: {workflowState.RootCause}");
            return builder.ToString().Trim();
        }

        private static string BuildModelOpsFallback(string? selectedModel, string? modelCapabilitySummary, string? benchmarkSummary)
        {
            var selected = string.IsNullOrWhiteSpace(selectedModel) ? "no model selected" : selectedModel!.Trim();
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("Choose the local model based on the task: use the fastest responsive installed model for side-dock help, and use the strongest locally benchmarked model for longer report reasoning. Do not declare a model “best” unless benchmark or capability evidence supports it on this machine.");
            builder.AppendLine();
            builder.AppendLine("## Current model evidence");
            builder.AppendLine($"- Selected model: **{selected}**");
            builder.AppendLine($"- Benchmark summary: {(string.IsNullOrWhiteSpace(benchmarkSummary) ? "not visible" : benchmarkSummary!.Trim())}");
            builder.AppendLine($"- Capability summary: {(string.IsNullOrWhiteSpace(modelCapabilitySummary) ? "not computed" : modelCapabilitySummary!.Trim())}");
            builder.AppendLine();
            builder.AppendLine("## Recommendation");
            builder.AppendLine("Run **AI Studio → Benchmark**. Then set the fastest acceptable model for dock chat and reserve the strongest successful model for deeper report/explanation tasks.");
            return builder.ToString().Trim();
        }

        private static string BuildDomainFallback(string? question)
        {
            var text = Normalize(question);
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            if (ContainsAny(text, "tight urban", "urban site", "congested", "limited space", "narrow"))
            {
                builder.AppendLine("A tight urban site is a construction site where crane choices are constrained by limited setup space, restricted access, nearby buildings, public roads, pedestrians, utilities, and small staging areas. For crane logistics, it is difficult because the crane must be strong enough and also physically able to arrive, set outriggers or mats, slew safely, reach the loads, and keep the public and adjacent assets protected.");
                builder.AppendLine();
                builder.AppendLine("## Crane-planning constraints to check");
                builder.AppendLine("- Access route, turning radius, delivery windows, traffic control, and temporary road closures.");
                builder.AppendLine("- Outrigger/mat footprint, ground bearing capacity, underground utilities, basements, slabs, and edge distances.");
                builder.AppendLine("- Working radius, boom length, hook height, counterweight tail swing, oversailing limits, and swing clearance.");
                builder.AppendLine("- Laydown/staging sequence, load pickup points, pedestrian exclusion, adjacent properties, and emergency access.");
            }
            else if (ContainsAny(text, "logistics"))
            {
                builder.AppendLine("Crane logistics is critical because the lift is only feasible when access, setup, load path, working radius, load capacity, swing clearance, ground support, sequencing, and exclusion zones all work together. A crane can be powerful enough on paper but still fail the plan if it cannot reach the load, stand safely, swing safely, or operate within site constraints.");
                builder.AppendLine();
                builder.AppendLine("## Main logistics checks");
                builder.AppendLine("- Crane arrival/access and setup footprint.");
                builder.AppendLine("- Load pickup points, laydown space, and lift sequence.");
                builder.AppendLine("- Radius/capacity from the manufacturer chart, including rigging and derating.");
                builder.AppendLine("- Ground/outrigger support, swing radius, oversailing, no-go zones, and public protection.");
            }
            else if (ContainsAny(text, "load chart", "capacity chart"))
            {
                builder.AppendLine("A crane load chart is the manufacturer’s capacity evidence for a specific crane configuration. It tells the maximum permitted load at different radii, boom lengths, counterweight setups, and operating conditions. It must be checked with the actual configuration and units before any lift decision is made.");
            }
            else if (ContainsAny(text, "rigging"))
            {
                builder.AppendLine("Rigging is the system of slings, shackles, spreader beams, hooks, lifting points, and procedures used to connect the crane to the load. It matters because rigging weight, sling angles, load balance, and lifting-point strength affect the actual load case and safety margin.");
            }
            else
            {
                builder.AppendLine("A crane is a lifting machine used to raise, lower, and move heavy materials horizontally and vertically. In construction, crane planning focuses on whether the crane can safely reach each load, lift it within capacity, stand on suitable ground, avoid no-go zones, and follow a reviewed lift sequence.");
            }

            builder.AppendLine();
            builder.AppendLine("## Why it matters");
            builder.AppendLine("The key checks are load weight, working radius, lift height, crane configuration, load-chart capacity, swing clearance, access, ground/outrigger support, sequencing, and exclusion zones. Final lift plans still require qualified engineering review against manufacturer data and site rules.");
            return builder.ToString().Trim();
        }

        private static string BuildUnknownFallback(ProjectFacts facts, AiWorkflowState? workflowState, string? question)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("I can answer this by separating the concept, the live project facts, and the software workflow. The visible question is open-ended, so the safest response is to summarize what is known and what needs confirmation rather than guessing hidden project details.");
            builder.AppendLine();
            builder.AppendLine("## Current project facts");
            builder.AppendLine($"- Project: **{facts.ProjectName}**; BIM/model objects: **{facts.ElementCount:N0}**; zones: **{facts.ZoneCount:N0}**; cranes: **{facts.CraneCount:N0}**; load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**.");
            if (facts.BuildingArea > 0 || facts.BuildingHeight > 0)
                builder.AppendLine($"- Building/model metrics: **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}**, height **{facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}**.");
            if (workflowState != null && !workflowState.CanRunOptimizerNow)
                builder.AppendLine($"- Readiness caveat: {workflowState.RootCause}");
            builder.AppendLine();
            builder.AppendLine("## Next step");
            builder.AppendLine("Ask for a specific project count, concept explanation, workflow status, or report/action recommendation and I will route the answer to the right evidence source.");
            return builder.ToString().Trim();
        }

        private static string NormalizeForPrompt(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace("\r", " ").Replace("\n", " ");
        }

        private static AiIntentDecision Decision(
            AiIntentFamily intent,
            string scope,
            bool needsProjectFacts,
            bool needsRagDocs,
            bool needsOllama,
            string preferredSection,
            string answerStyle,
            IEnumerable<string>? factKeys = null,
            string reason = "")
        {
            return new AiIntentDecision
            {
                Intent = intent,
                Scope = scope,
                NeedsProjectFacts = needsProjectFacts,
                NeedsRagDocs = needsRagDocs,
                NeedsOllama = needsOllama,
                PreferredSection = preferredSection,
                AnswerStyle = answerStyle,
                FactKeys = factKeys?.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>(),
                Reason = reason
            };
        }

        private static bool TryBuildProjectFactAnswer(AiWorkflowContextSnapshot? context, string question, string? activeScreen, out string answer, out string source)
        {
            answer = string.Empty;
            source = string.Empty;
            var text = Normalize(question);
            var facts = BuildFacts(context, activeScreen);

            if (context == null)
            {
                answer = "## Answer\nNo live project context is available yet, so I cannot give a deterministic project count or metric. Open or create a project first, then ask again.\n\n## Confidence\nHigh — no project snapshot is attached.";
                source = "Deterministic project-fact router found no live project context.";
                return true;
            }

            if (IsUnitFactQuestion(text))
            {
                var loadChartNote = facts.HasLoadChartEvidence
                    ? "Load-chart evidence is present; verify each manufacturer chart in its own published units before using it for lift decisions."
                    : "No active load-chart evidence is exposed yet, so the chart units for the selected crane are not available in the current AI context.";

                answer = $"## Answer\nThe current project is using a **metric unit profile** in the visible AI context. Geometry is in **{facts.GeometryUnit}**, area is in **{facts.AreaUnit}**, building height is in **{facts.BuildingLengthUnit}**, element mass is displayed/stored as **{facts.StoredMassUnit}**, weight thresholds use **{facts.ThresholdMassUnit}**, and concrete density is shown as **{facts.DensityUnit}**.\n\n## Current project evidence\n- Building/model area: **{(facts.BuildingArea > 0 ? facts.BuildingArea.ToString("0.##") + " " + facts.BuildingAreaUnit : "not exposed")}**\n- Building/model height: **{(facts.BuildingHeight > 0 ? facts.BuildingHeight.ToString("0.##") + " " + facts.BuildingLengthUnit : "not exposed")}**\n- BIM/model objects: **{facts.ElementCount:N0}**\n- Active load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**\n\n## Important note\n{loadChartNote} Do not mix metric and imperial units when checking crane capacity, rigging weight, radius, or report values.\n\n## Confidence\nHigh for the visible project unit profile; load-chart units require the actual manufacturer chart evidence.";
                source = "Deterministic project fact answer for the current unit profile.";
                return true;
            }

            if (ContainsAny(text, "load chart", "load charts", "capacity chart", "capacity charts", "charts inserted", "charts imported"))
            {
                var count = facts.LoadChartEvidenceCount;
                var hasEvidence = facts.HasLoadChartEvidence;
                var catalogCaveat = "This is the active scene/result load-chart evidence exposed to AI, not a guaranteed full saved-catalog count. For saved catalog inventory, open **CRANES → View Saved Charts**.";
                answer = $"## Answer\nThe current AI context exposes **{count:N0} active load-chart evidence source(s)**. Load-chart evidence is currently **{(hasEvidence ? "available" : "missing")}**.\n\n## Current project evidence\n- Cranes in scene: **{facts.CraneCount:N0}**\n- Active load-chart evidence sources: **{count:N0}**\n- Has load-chart evidence flag: **{hasEvidence}**\n\n## Caveat\n{catalogCaveat}\n\n## Next click\n**CRANES → View Saved Charts** to inspect the saved chart catalog, or **CRANES → Import Load Chart** to add manufacturer capacity data.";
                source = "Deterministic project fact answer for load-chart evidence count.";
                return true;
            }

            if (ContainsAny(text, "green area", "green areas", "allowed area", "allowed areas", "allowed zone", "allowed zones"))
            {
                var areaLine = facts.AllowedZoneArea > 0 ? $"\n- Allowed-zone stored area total: **{facts.AllowedZoneArea:0.##} m²**" : string.Empty;
                answer = $"## Answer\nThere are **{facts.AllowedZoneCount:N0} green/allowed area(s)** drawn in the current project context.\n\n## Current project evidence\n- Allowed/green zones: **{facts.AllowedZoneCount:N0}**{areaLine}\n- Forbidden zones: **{facts.ForbiddenZoneCount:N0}**\n- Total zones: **{facts.ZoneCount:N0}**\n\n## Next click\nUse **ZONES → Allowed Zone** to draw another allowed placement area, or review the existing zone geometry before running GA.";
                source = "Deterministic project fact answer for allowed/green zone count.";
                return true;
            }

            if (ContainsAny(text, "forbidden area", "forbidden areas", "red area", "red areas", "forbidden zone", "forbidden zones", "no go", "no-go"))
            {
                var areaLine = facts.ForbiddenZoneArea > 0 ? $"\n- Forbidden-zone stored area total: **{facts.ForbiddenZoneArea:0.##} m²**" : string.Empty;
                answer = $"## Answer\nThere are **{facts.ForbiddenZoneCount:N0} forbidden/no-go zone(s)** drawn in the current project context.\n\n## Current project evidence\n- Forbidden zones: **{facts.ForbiddenZoneCount:N0}**{areaLine}\n- Allowed/green zones: **{facts.AllowedZoneCount:N0}**\n- Total zones: **{facts.ZoneCount:N0}**";
                source = "Deterministic project fact answer for forbidden zone count.";
                return true;
            }

            if (ContainsAny(text, "zone", "zones", "placement area", "placement areas"))
            {
                answer = $"## Answer\nThe project currently has **{facts.ZoneCount:N0} zone(s)**: **{facts.AllowedZoneCount:N0} allowed/green** and **{facts.ForbiddenZoneCount:N0} forbidden**.\n\n## Current project evidence\n- Active screen: **{facts.ActiveScreen}**\n- Allowed/green zones: **{facts.AllowedZoneCount:N0}**\n- Forbidden zones: **{facts.ForbiddenZoneCount:N0}**\n- Total zones: **{facts.ZoneCount:N0}**";
                source = "Deterministic project fact answer for zone count.";
                return true;
            }

            if (ContainsAny(text, "building area", "building's area", "building footprint", "asset area", "asset's area", "project area"))
            {
                if (facts.BuildingArea > 0)
                {
                    var assetNote = ContainsAny(text, "asset area", "asset's area")
                        ? "If by asset you mean the imported building/model footprint, this is the available deterministic area. A selected individual asset/element area is not exposed in the AI context."
                        : "This is the application-computed building/model footprint from imported element extents, not a certified survey area.";
                    answer = $"## Answer\nThe current building/model footprint area is **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}**.\n\n## Current project evidence\n- Model context: **{facts.ModelLabel}**\n- BIM objects: **{facts.ElementCount:N0}**\n- Building footprint area: **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}**\n- Building height: **{facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}**\n\n## Caveat\n{assetNote}";
                }
                else
                {
                    answer = "## Answer\nThe building/model area is **not available yet** in the current AI context. Load an IFC model with valid element geometry first, then ask again.\n\n## Next click\n**IFC MODEL → Load IFC**.";
                }

                source = "Deterministic project fact answer for building/model area.";
                return true;
            }

            if (ContainsAny(text, "building height", "height of the building", "asset height", "model height"))
            {
                answer = facts.BuildingHeight > 0
                    ? $"## Answer\nThe current building/model height is **{facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}**.\n\n## Current project evidence\n- Building footprint area: **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}**\n- Building/model height: **{facts.BuildingHeight:0.##} {facts.BuildingLengthUnit}**\n- BIM objects: **{facts.ElementCount:N0}**"
                    : "## Answer\nThe building/model height is **not available yet** in the current AI context. Load an IFC model with valid geometry first.";
                source = "Deterministic project fact answer for building/model height.";
                return true;
            }

            if (ContainsAny(text, "crane", "cranes") && !ContainsAny(text, "what is a crane", "define crane"))
            {
                answer = $"## Answer\nThere are **{facts.CraneCount:N0} crane(s)** in the current scene.\n\n## Current project evidence\n- Cranes in scene: **{facts.CraneCount:N0}**\n- Active load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**\n- Load-chart evidence available: **{facts.HasLoadChartEvidence}**\n\n## Next click\nUse **CRANES → MOBILE CRANE** to add/select a crane before optimization.";
                source = "Deterministic project fact answer for crane count.";
                return true;
            }

            if (ContainsAny(text, "bim object", "bim objects", "model object", "model objects", "element count", "total elements", "how many elements"))
            {
                var tableLine = facts.TableTotal > 0 ? $"\n- Element table scope: **{facts.TableVisible:N0}/{facts.TableTotal:N0} visible**" : string.Empty;
                answer = $"## Answer\nThe current model context has **{facts.ElementCount:N0} BIM/model object(s)**.\n\n## Current project evidence\n- BIM/model objects: **{facts.ElementCount:N0}**{tableLine}\n- Model: **{facts.ModelLabel}**\n- Model revision: **{facts.ModelRevision:N0}**";
                source = "Deterministic project fact answer for element/BIM object count.";
                return true;
            }

            return false;
        }

        private static string BuildWorkflowStatusAnswer(AiWorkflowContextSnapshot? context, WorkflowReadinessSnapshot? readiness, string? activeScreen, string? nextAction)
        {
            var facts = BuildFacts(context, activeScreen);
            var state = context == null ? null : AiWorkflowStateClassifier.Classify(context, readiness);
            var status = state == null
                ? "No live project is attached."
                : state.CanRunOptimizerNow
                    ? "The core workflow appears ready for the next controlled optimization step."
                    : state.RootCause;
            var recommended = !string.IsNullOrWhiteSpace(nextAction)
                ? nextAction!.Trim()
                : state?.RecommendedAction ?? "Open or create a project, then load IFC data.";
            var readinessText = readiness == null
                ? "Readiness snapshot is not available."
                : $"{readiness.CompleteCount}/{readiness.TotalCount} complete; {readiness.MissingCriticalCount} critical missing.";

            var builder = new StringBuilder();
            builder.AppendLine("## Status");
            builder.AppendLine(status);
            builder.AppendLine();
            builder.AppendLine("## Current project evidence");
            builder.AppendLine($"- Project: **{facts.ProjectName}**");
            builder.AppendLine($"- Active screen: **{facts.ActiveScreen}**");
            builder.AppendLine($"- Readiness: **{readinessText}**");
            builder.AppendLine($"- BIM objects: **{facts.ElementCount:N0}**");
            builder.AppendLine($"- Zones: **{facts.ZoneCount:N0}** total (**{facts.AllowedZoneCount:N0} allowed/green**, **{facts.ForbiddenZoneCount:N0} forbidden**)");
            builder.AppendLine($"- Cranes: **{facts.CraneCount:N0}**");
            builder.AppendLine($"- Load-chart evidence sources: **{facts.LoadChartEvidenceCount:N0}**");
            if (facts.BuildingArea > 0)
                builder.AppendLine($"- Building/model area: **{facts.BuildingArea:N2} {facts.BuildingAreaUnit}**");
            builder.AppendLine();
            builder.AppendLine("## Next step");
            builder.AppendLine($"**{recommended}**");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — this status is based on deterministic live workflow state, not a model guess.");
            return builder.ToString().Trim();
        }

        private static string BuildBlockerAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, string? nextAction)
        {
            var state = AiWorkflowStateClassifier.Classify(context);
            var recommended = !string.IsNullOrWhiteSpace(nextAction) ? nextAction!.Trim() : state.RecommendedAction;
            var builder = new StringBuilder();
            builder.AppendLine("## Status");
            builder.AppendLine(state.RootCause);
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            builder.AppendLine(AiWorkflowEvidenceTextBuilder.BuildWorkflowBlockerSummary(context));
            builder.AppendLine();
            builder.AppendLine("## Recommendation");
            builder.AppendLine(recommended);
            builder.AppendLine();
            builder.AppendLine("## Risk");
            builder.AppendLine("Do not bypass optimizer eligibility, force GA, or use estimated/fallback weights for engineering optimization. Fix the deterministic missing input first.");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — this is grounded in live readiness and screen-context evidence.");
            return builder.ToString().Trim();
        }

        private static ProjectFacts BuildFacts(AiWorkflowContextSnapshot? context, string? activeScreen)
        {
            if (context == null)
            {
                return new ProjectFacts
                {
                    ActiveScreen = string.IsNullOrWhiteSpace(activeScreen) ? "HOME" : activeScreen!.Trim()
                };
            }

            var facts = MergeFacts(context);
            var modelLabel = !string.IsNullOrWhiteSpace(context.ModelDisplayName)
                ? context.ModelDisplayName.Trim()
                : !string.IsNullOrWhiteSpace(context.CurrentProjectName)
                    ? context.CurrentProjectName.Trim()
                    : context.ElementCount > 0 ? "Imported IFC model" : "No IFC model loaded";

            var loadChartEvidenceCount = context.LoadChartEvidenceCount;
            if (loadChartEvidenceCount <= 0 && GetBool(facts, "HasLoadChartEvidence"))
                loadChartEvidenceCount = 1;

            return new ProjectFacts
            {
                ProjectName = context.HasProject && !string.IsNullOrWhiteSpace(context.CurrentProjectName) ? context.CurrentProjectName : "none",
                ActiveScreen = string.IsNullOrWhiteSpace(activeScreen) ? "HOME" : activeScreen!.Trim(),
                ModelLabel = modelLabel,
                ModelRevision = context.ModelRevision,
                ElementCount = context.ElementCount,
                TableVisible = context.ElementTableVisibleCount,
                TableTotal = context.ElementTableTotalCount,
                ZoneCount = context.ZoneCount,
                AllowedZoneCount = context.AllowedZoneCount > 0 || context.ForbiddenZoneCount > 0 ? context.AllowedZoneCount : GetInt(facts, "AllowedZoneCount") ?? Math.Max(0, context.ZoneCount - (GetInt(facts, "ForbiddenZoneCount") ?? 0)),
                ForbiddenZoneCount = context.AllowedZoneCount > 0 || context.ForbiddenZoneCount > 0 ? context.ForbiddenZoneCount : GetInt(facts, "ForbiddenZoneCount") ?? 0,
                AllowedZoneArea = context.AllowedZoneArea,
                ForbiddenZoneArea = context.ForbiddenZoneArea,
                CraneCount = context.CraneCount,
                LoadChartEvidenceCount = Math.Max(0, loadChartEvidenceCount),
                HasLoadChartEvidence = context.HasLoadChartEvidence || loadChartEvidenceCount > 0 || GetBool(facts, "HasLoadChartEvidence"),
                BuildingArea = context.BuildingArea,
                BuildingAreaUnit = string.IsNullOrWhiteSpace(context.BuildingAreaUnit) ? "m²" : context.BuildingAreaUnit,
                BuildingHeight = context.BuildingHeight,
                BuildingLengthUnit = string.IsNullOrWhiteSpace(context.BuildingLengthUnit) ? "m" : context.BuildingLengthUnit,
                GeometryUnit = string.IsNullOrWhiteSpace(context.BuildingLengthUnit) ? "m" : context.BuildingLengthUnit,
                AreaUnit = string.IsNullOrWhiteSpace(context.BuildingAreaUnit) ? "m²" : context.BuildingAreaUnit,
                StoredMassUnit = "t",
                ThresholdMassUnit = "kg",
                DensityUnit = "kg/m³",
                HasOptimizationResult = context.OptimizationResult.HasResult,
                TopCategories = GetFirstFact(
                    facts,
                    AiWorkflowFactKeys.TopPlanningCategories,
                    AiWorkflowFactKeys.TopFilterableCategories,
                    AiWorkflowFactKeys.TopResolvedCategories),
                TopRawIfcTypes = GetFact(facts, "TopRawIfcTypes")
            };
        }

        private static IReadOnlyDictionary<string, string> MergeFacts(AiWorkflowContextSnapshot context)
        {
            return context.ScreenContexts
                .SelectMany(screen => screen.Facts)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsProjectSpecificWording(string text)
        {
            return ContainsAny(text,
                "this project", "current project", "this result", "current result", "the result", "our result", "before trusting", "trust the result", "trusting the result",
                "exactly", "here", "right now", "currently", "loaded", "visible", "shown", "open project", "this model", "current model", "in this file", "in this scene");
        }

        private static bool IsProjectConnectionRequested(string text)
        {
            return ContainsAny(text,
                "this project", "current project", "this site", "our site", "this model", "current model", "in this project", "in our project", "here", "right now", "on this site");
        }

        private static bool IsProjectFactQuestion(string text)
        {
            if (IsUnitFactQuestion(text))
                return true;

            var asksQuantity = ContainsAny(text, "how many", "count", "number of", "quantity", "total", "area", "height", "metric", "metrics", "how much");
            if (!asksQuantity)
                return false;

            return ContainsAny(text,
                "load chart", "chart", "green area", "allowed area", "allowed zone", "forbidden", "zone", "crane", "building area", "building's area", "asset area", "asset's area", "project area", "height", "bim", "ifc", "element", "slab", "beam", "column", "wall", "assembly", "rebar", "model object", "object", "unit", "units");
        }

        private static bool IsUnitFactQuestion(string text)
        {
            if (!ContainsAny(text, "unit", "units", "measurement", "measurements"))
                return false;

            return ContainsAny(text,
                "what", "which", "used", "using", "current", "project", "model", "building", "geometry", "area", "height", "weight", "mass", "density", "load", "chart", "calculation", "calculations");
        }

        private static bool IsWorkflowStatusQuestion(string text)
        {
            return ContainsAny(text,
                "how are things", "status", "project status", "health", "ready", "readiness", "what is missing", "what's missing", "missing before", "what should i do next", "next step", "next action", "where are we", "are we ready", "workflow state", "current state");
        }

        private static bool IsBlockerQuestion(string text)
        {
            return ContainsAny(text, "blocked", "blocker", "why can't", "cannot", "can't run", "ga", "optimizer", "optimization", "run optimizer", "not ready")
                   && ContainsAny(text, "why", "what", "how", "blocked", "missing", "fix", "solve", "run", "start");
        }

        private static bool IsModelOpsQuestion(string text)
        {
            return ContainsAny(text, "local model", "which model", "best model", "ollama", "llama", "qwen", "deepseek", "gemma", "benchmark", "fast model", "reasoning model", "model should i use", "selected model");
        }

        private static bool IsAppValueQuestion(string text)
        {
            var asksValue = ContainsAny(text,
                "important", "importance", "value", "purpose", "why do we need", "why use", "what is this for", "what is it for", "what does this do");
            if (!asksValue)
                return false;

            return ContainsAny(text,
                "program", "application", "app", "software", "tool", "system", "platform", "project", "optimizer", "copilot", "ai assist");
        }

        private static bool IsEngineeringQualityQuestion(string text)
        {
            return ContainsAny(text,
                "ensure accuracy", "assure accuracy", "accuracy", "accurate", "correctness", "correct result", "correct results",
                "trust the result", "trust results", "trustworthy", "reliable", "defensible", "valid result", "valid results",
                "validate", "validation", "verify", "verification", "quality assurance", "qa", "quality check", "quality control",
                "make sure the result", "make sure results", "how accurate", "result accuracy", "calculation accuracy", "engineering accuracy",
                "audit the result", "audit results", "check the result", "check results");
        }

        private static bool IsDeliveryRequirementsQuestion(string text)
        {
            return ContainsAny(text,
                "what is needed to be delivered", "what needs to be delivered", "what need to be delivered", "what needed delivered", "needed delivered", "what should be delivered",
                "what do we need to deliver", "needed to deliver", "required to deliver", "delivery requirement", "delivery requirements", "deliver", "delivered",
                "deliverable", "deliverables", "submission package", "handover package", "what is required for delivery",
                "what must be completed", "what should be completed", "what is needed for handover", "client delivery", "final package", "final deliverable", "handover", "submit", "submission");
        }

        private static bool IsRealWorldImplementationQuestion(string text)
        {
            var asksImplementation = ContainsAny(text,
                "how to implement", "how do we implement", "how would we implement", "implement this", "implementation in real life",
                "real life", "in the field", "on site", "site implementation", "field implementation", "real project", "actual project",
                "practical workflow", "practical steps", "real workflow");
            if (!asksImplementation)
                return false;

            return ContainsAny(text,
                "this", "crane", "lifting", "lift", "optimization", "optimizer", "program", "application", "software", "workflow", "logistics", "planning", "site");
        }

        private static bool IsDomainExplanationQuestion(string text)
        {
            var asksExplanation = ContainsAny(text, "what is", "what are", "define", "explain", "meaning of", "what does")
                                  || ContainsAny(text, "critical about", "why is", "why are", "why does");
            if (!asksExplanation)
                return false;

            if (IsAppValueQuestion(text) || IsProjectFactQuestion(text) || IsModelOpsQuestion(text))
                return false;

            return ContainsAny(text,
                "crane", "logistics", "lift", "lifting", "rigging", "load chart", "capacity chart", "swing radius", "working radius", "boom", "outrigger", "counterweight", "construction logistics", "heavy lift", "precast", "bim", "ifc");
        }

        private static bool IsUiNavigationQuestion(string text)
        {
            return ContainsAny(text, "how do i", "where do i", "where is", "which tab", "which button", "open", "click", "import", "export", "show me", "navigate", "use the software", "use this app", "how to");
        }

        private static bool IsWhatIfQuestion(string text)
        {
            return ContainsAny(text, "what if", "would", "should i change", "if i change", "scenario", "alternative", "larger crane", "different crane", "different zone", "fix first");
        }

        private static bool IsReportQuestion(string text)
        {
            return ContainsAny(text, "report", "client", "stakeholder", "narrative", "write", "draft", "summarize for", "summarise for", "appendix", "qa pack");
        }

        private static IReadOnlyList<string> ExtractFactKeys(string text)
        {
            var keys = new List<string>();
            if (ContainsAny(text, "zone", "green", "allowed", "forbidden")) keys.Add("zones");
            if (ContainsAny(text, "load chart", "chart")) keys.Add("loadChartEvidence");
            if (ContainsAny(text, "crane", "cranes")) keys.Add("craneCount");
            if (ContainsAny(text, "building area", "building's area", "asset area", "asset's area", "project area")) keys.Add("buildingArea");
            if (ContainsAny(text, "height")) keys.Add("buildingHeight");
            if (ContainsAny(text, "bim", "ifc", "element", "object")) keys.Add("elementCount");
            return keys;
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var text = value.Trim().ToLowerInvariant();
            text = Regex.Replace(text, @"[\u2018\u2019']", "'");
            text = Regex.Replace(text, @"[^a-z0-9>\s'/-]+", " ");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetFact(IReadOnlyDictionary<string, string> facts, string key)
        {
            return facts.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static string GetFirstFact(IReadOnlyDictionary<string, string> facts, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (facts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static int? GetInt(IReadOnlyDictionary<string, string> facts, string key)
        {
            return facts.TryGetValue(key, out var value) && int.TryParse(CleanNumber(value), out var parsed) ? parsed : null;
        }

        private static bool GetBool(IReadOnlyDictionary<string, string> facts, string key)
        {
            return facts.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
        }

        private static string CleanNumber(string value)
        {
            return (value ?? string.Empty).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        }
    }
}
