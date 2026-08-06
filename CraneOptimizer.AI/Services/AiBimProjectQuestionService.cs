using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Services
{
    /// <summary>
    /// Deterministic live-BIM question router. It answers direct project-data questions
    /// from screen contributor facts before any LLM/RAG context can produce generic
    /// documentation prose. This keeps questions such as "How many slabs are there?"
    /// grounded in the imported model snapshot.
    /// </summary>
    public static class AiBimProjectQuestionService
    {
        private sealed record CountMatch(string Label, int Count, string Source, string Caveat);

        private sealed record ReportedScopeCount(int Count, string Provenance);

        private sealed record QueryTarget(
            string CanonicalLabel,
            string[] Aliases,
            bool PreferRawIfc,
            string UnavailableNoun);

        private static readonly QueryTarget[] CategoryTargets =
        {
            new("Slab", new[] { "slab", "slabs", "ifcslab", "floor slab", "floor slabs" }, false, "slabs"),
            new("Beam", new[] { "beam", "beams", "ifcbeam" }, false, "beams"),
            new("Column", new[] { "column", "columns", "ifccolumn", "pillar", "pillars" }, false, "columns"),
            new("Wall", new[] { "wall", "walls", "ifcwall", "ifcwallstandardcase" }, false, "walls"),
            new("Roof", new[] { "roof", "roofs", "ifcroof" }, false, "roofs"),
            new("Unknown", new[] { "unknown", "unclassified", "uncategorized", "uncategorised", "not categorized", "not categorised" }, false, "unknown/unclassified elements"),
            new("Assembly", new[] { "assembly", "assemblies", "element assembly", "element assemblies", "lift unit", "lift units", "grouped lift", "grouped lifts" }, false, "assemblies or lift units"),
            new("Reinforcement", new[] { "reinforcement", "reinforcing", "rebar", "rebars", "reinforcing bar", "reinforcing bars", "ifcreinforcingbar" }, false, "reinforcement/rebar elements"),
            new("Grid", new[] { "grid", "grids", "ifcgrid" }, false, "grid elements"),
            new("Door", new[] { "door", "doors", "ifcdoor" }, false, "doors"),
            new("Window", new[] { "window", "windows", "ifcwindow" }, false, "windows"),
            new("Stair", new[] { "stair", "stairs", "staircase", "staircases", "ifcstair" }, false, "stairs"),
            new("Footing", new[] { "footing", "footings", "foundation", "foundations", "ifcfooting" }, false, "footings/foundations")
        };

        public static bool IsLiveBimDataQuestion(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            var normalized = NormalizeQuestion(question);
            if (ContainsAny(normalized, "building usage", "building type", "metadata", "property set"))
                return false;

            if (IsCountScopeDiscrepancyQuestion(normalized))
                return true;

            var categoryTargets = FindCategoryTargets(normalized);
            var numericCount = Regex.Matches(normalized, @"\b\d[\d,]*\b").Count;
            var hasExplicitCountScope = ContainsAny(
                normalized,
                "ifc element filter",
                "filter dialog",
                "the filter",
                "filter says",
                "filter shows",
                "filter reports",
                "filter count",
                "planning count",
                "planning/filterable",
                "filterable planning",
                "complete-scene",
                "complete scene",
                "ifc scene",
                "bim context",
                "bim contains",
                "bim says",
                "bim reports");
            if (categoryTargets.Count > 0
                && (numericCount >= 2
                    || (numericCount >= 1 && hasExplicitCountScope)
                    || ContainsAny(
                        normalized,
                        "which count",
                        "what count",
                        "count should i use",
                        "should i use",
                        "use for lift planning",
                        "for lift planning",
                        "both scopes",
                        "in both scopes",
                        "planning/filterable",
                        "filterable planning",
                        "complete-scene",
                        "complete scene")))
            {
                return true;
            }

            if (ContainsAny(
                    normalized,
                    "what does unknown mean",
                    "explain unknown",
                    "why are elements unknown",
                    "why is unknown so high",
                    "unknown category meaning"))
            {
                return true;
            }

            var asksForCount = ContainsAny(normalized,
                "how many", "count", "number of", "quantity of", "quantities of", "total", "breakdown", "summarize categories", "summarise categories");

            if (!asksForCount)
                return false;

            if (ContainsAny(normalized,
                    "element", "elements", "ifc", "bim", "model object", "model objects", "slab", "slabs", "beam", "beams", "column", "columns",
                    "wall", "walls", "roof", "roofs", "window", "windows", "door", "doors", "stair", "stairs", "footing", "footings", "foundation", "foundations", "unknown", "assembly", "assemblies", "rebar", "reinforcing", "candidate", "candidates",
                    "lift unit", "lift units", "weight", "kg", "ton", "tonne", "over", "above", ">"))
            {
                return true;
            }

            return false;
        }

        public static bool TryBuildAnswer(
            AiWorkflowContextSnapshot? context,
            string? question,
            string? activeScreen,
            out string answer,
            out string source)
        {
            answer = string.Empty;
            source = string.Empty;

            if (!IsLiveBimDataQuestion(question))
                return false;

            var visibleQuestion = question!.Trim();
            if (context == null)
            {
                answer = BuildNoContextAnswer(visibleQuestion);
                source = "Deterministic BIM data question handled locally; no live project context was available.";
                return true;
            }

            if (context.ElementCount <= 0)
            {
                answer = BuildNoIfcAnswer(visibleQuestion, activeScreen, context);
                source = "Deterministic BIM data question handled locally; IFC is not loaded.";
                return true;
            }

            if (TryDetectStaleScreenFacts(context, activeScreen, out answer))
            {
                source = "Deterministic BIM data question blocked because explicit host revision/fingerprint facts did not match the captured live model snapshot.";
                return true;
            }

            if (TryBuildCountScopeDiscrepancyAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic BIM count-scope comparison produced locally from host-confirmed facts and explicitly labelled user-reported values.";
                return true;
            }

            if (TryBuildSceneInputSummaryAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic BIM object and zone count answer produced locally from live project facts.";
                return true;
            }

            if (TryBuildUnknownCategoryExplanation(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic explanation of the Unknown BIM category produced locally from live model facts.";
                return true;
            }

            if (TryBuildVisibleElementAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic visible-element scope answer produced locally from live project facts.";
                return true;
            }

            if (TryBuildThresholdAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic BIM threshold/count answer produced locally from live model facts.";
                return true;
            }

            if (TryBuildCandidateAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic GA/lift-unit count answer produced locally from live readiness facts.";
                return true;
            }

            if (TryBuildTotalElementAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic total element count answer produced locally from live context.";
                return true;
            }

            if (TryBuildCategoryAnswer(context, visibleQuestion, activeScreen, out answer))
            {
                source = "Deterministic BIM category count answer produced locally from live model facts.";
                return true;
            }

            answer = BuildUnavailableAnswer(context, visibleQuestion, activeScreen, "the requested BIM category or count");
            source = "Deterministic BIM data question handled locally; requested category was not exposed in the current AI context.";
            return true;
        }

        private static bool TryBuildSceneInputSummaryAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var asksForBimObjects = ContainsAny(
                text,
                "bim object", "bim objects", "model object", "model objects", "scene object", "scene objects",
                "how many elements", "total elements", "element count", "number of elements", "count elements");
            var asksForZones = ContainsAny(text, "zone", "zones", "placement zone", "placement zones");

            if (!asksForBimObjects || !asksForZones)
                return false;

            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"The current project has {context.ElementCount:N0} BIM/model object(s) and {context.ZoneCount:N0} zone(s).");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- BIM/model objects in the captured scene: {context.ElementCount:N0}");
            builder.AppendLine($"- Zones: {context.ZoneCount:N0} total ({context.AllowedZoneCount:N0} allowed/green, {context.ForbiddenZoneCount:N0} forbidden)");
            AppendCountScopeEvidence(builder, context);
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("Use IFC MODEL to inspect/export model objects, or ZONES to review the allowed and forbidden placement areas.");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — both counts come directly from the same captured live project snapshot.");
            answer = builder.ToString().Trim();
            return true;
        }

        private static bool TryBuildCountScopeDiscrepancyAnswer(
            AiWorkflowContextSnapshot context,
            string question,
            string? activeScreen,
            out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            if (!IsCountScopeDiscrepancyQuestion(text))
                return false;

            var facts = MergeFacts(context);
            var target = FindCategoryTarget(text);
            var categoryLabel = target?.CanonicalLabel;
            var categoryPhrase = string.IsNullOrWhiteSpace(categoryLabel)
                ? "elements"
                : $"{categoryLabel} elements";

            var planningCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.PlanningCategoryCounts,
                AiWorkflowFactKeys.TopPlanningCategories,
                AiWorkflowFactKeys.FilterableCategoryCounts,
                AiWorkflowFactKeys.TopFilterableCategories);
            var sceneCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.ResolvedCategoryCounts,
                AiWorkflowFactKeys.TopResolvedCategories);
            var rawCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.RawIfcTypeCounts,
                AiWorkflowFactKeys.TopRawIfcTypes);

            int? hostPlanningCount = target == null
                ? GetFirstInt(facts, AiWorkflowFactKeys.FilterableElementCount)
                : FindCount(planningCounts, target)?.Count;
            int? hostSceneCount = target == null
                ? context.ElementCount
                : FindCount(sceneCounts, target)?.Count;

            var reportedPlanning = TryExtractReportedScopeCount(text, planningScope: true, target);
            var reportedScene = TryExtractReportedScopeCount(text, planningScope: false, target);

            var builder = new StringBuilder();
            builder.AppendLine("## Answer");

            var asksForRecommendedCount = IsPlanningRecommendationQuestion(text)
                || Regex.IsMatch(
                    text,
                    @"\b(which|what)\b.*\bcount\b.*\b(correct|right|authoritative|use)\b",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            var hasReportedMismatch = false;
            if (hostPlanningCount.HasValue
                && reportedPlanning != null
                && hostPlanningCount.Value != reportedPlanning.Count)
            {
                builder.AppendLine($"The question's planning/filter value of {reportedPlanning.Count:N0} conflicts with the current host-confirmed value of {hostPlanningCount.Value:N0}. The host fact is authoritative.");
                hasReportedMismatch = true;
            }

            if (hostSceneCount.HasValue
                && reportedScene != null
                && hostSceneCount.Value != reportedScene.Count)
            {
                builder.AppendLine($"The question's complete-scene value of {reportedScene.Count:N0} conflicts with the current host-confirmed value of {hostSceneCount.Value:N0}. The host fact is authoritative.");
                hasReportedMismatch = true;
            }

            if (!hasReportedMismatch && hostPlanningCount.HasValue && hostSceneCount.HasValue)
            {
                var questionNumbers = ExtractQuestionNumbers(text);
                if (questionNumbers.Contains(hostPlanningCount.Value)
                    && questionNumbers.Contains(hostSceneCount.Value))
                {
                    builder.AppendLine($"Both numbers are correct for different scopes: {hostPlanningCount.Value:N0} planning/filterable {categoryPhrase} and {hostSceneCount.Value:N0} complete-scene {categoryPhrase}.");
                }
                else if (questionNumbers.Count >= 2
                         && questionNumbers.Any(value => value != hostPlanningCount.Value && value != hostSceneCount.Value))
                {
                    builder.AppendLine($"The supplied alternatives conflict with the live project facts. The host-confirmed values are {hostPlanningCount.Value:N0} planning/filterable {categoryPhrase} and {hostSceneCount.Value:N0} complete-scene {categoryPhrase}.");
                }
            }

            if (asksForRecommendedCount && hostPlanningCount.HasValue)
            {
                builder.AppendLine($"Use the planning/filterable count for lift planning: **{hostPlanningCount.Value:N0} {categoryPhrase}**. Use the same planning/filterable scope for engineering filtering. Use the complete-scene count for classification and hierarchy review.");
            }

            builder.AppendLine("The values use different count scopes and must not be treated as interchangeable.");
            builder.AppendLine();
            builder.AppendLine("## Count comparison");
            AppendScopeCountLine(
                builder,
                "Planning/filterable",
                categoryPhrase,
                hostPlanningCount,
                reportedPlanning,
                "the engineering Filter/lift-review scope");
            AppendScopeCountLine(
                builder,
                "Complete IFC scene",
                categoryPhrase,
                hostSceneCount,
                reportedScene,
                "the complete-scene classification scope");

            var comparisonPlanning = hostPlanningCount ?? reportedPlanning?.Count;
            var comparisonScene = hostSceneCount ?? reportedScene?.Count;
            if (comparisonPlanning.HasValue && comparisonScene.HasValue)
            {
                var delta = comparisonScene.Value - comparisonPlanning.Value;
                var provenance = hostPlanningCount.HasValue && hostSceneCount.HasValue
                    ? "Both values are host-confirmed."
                    : "The arithmetic uses at least one value reported in the question; that value is not independently confirmed by the current AI evidence.";

                if (delta > 0)
                    builder.AppendLine($"- Difference: **{delta:N0} more complete-scene object(s)**. {provenance}");
                else if (delta < 0)
                    builder.AppendLine($"- Difference: **{Math.Abs(delta):N0} fewer complete-scene object(s)**. {provenance}");
                else
                    builder.AppendLine($"- Difference: **0**. {provenance}");
            }

            AppendReportedMismatchWarning(builder, "planning/filterable", hostPlanningCount, reportedPlanning);
            AppendReportedMismatchWarning(builder, "complete-scene", hostSceneCount, reportedScene);

            builder.AppendLine();
            builder.AppendLine("## Why the scopes can differ");
            builder.AppendLine("- Planning/filterable counts represent the top-level engineering elements exposed for filtering and lift review.");
            builder.AppendLine("- Complete-scene counts include every scene object resolved to the category and can therefore include parent assemblies plus categorized child/component objects.");
            builder.AppendLine("- A complete-scene value is not an authoritative planning count unless the host supplies the corresponding planning/filter fact.");

            if (HasAssemblyAndPartStructure(rawCounts, out var assemblyCount, out var partCount))
                builder.AppendLine($"- Current IFC hierarchy evidence: {assemblyCount:N0} assembly object(s) and {partCount:N0} building-element part object(s).");

            if (facts.TryGetValue(AiWorkflowFactKeys.CategoryCountScopeNote, out var categoryScopeNote)
                && !string.IsNullOrWhiteSpace(categoryScopeNote))
            {
                builder.AppendLine($"- Host scope note: {categoryScopeNote.Trim()}");
            }
            else if (facts.TryGetValue(AiWorkflowFactKeys.ElementCountScopeNote, out var elementScopeNote)
                     && !string.IsNullOrWhiteSpace(elementScopeNote))
            {
                builder.AppendLine($"- Host scope note: {elementScopeNote.Trim()}");
            }

            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- Planning/filterable provenance: {BuildScopeProvenance(hostPlanningCount, reportedPlanning)}");
            builder.AppendLine($"- Complete-scene provenance: {BuildScopeProvenance(hostSceneCount, reportedScene)}");
            AppendPlanningCategoryEvidence(builder, facts);
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine(hostPlanningCount.HasValue
                ? "HOME → Filter / verify the active scope, or IFC MODEL → Export Element List / inspect parent-child identifiers"
                : "HOME → Filter / verify the planning value; a host context adapter is required before AI can independently confirm that Filter count");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine(hostPlanningCount.HasValue && hostSceneCount.HasValue
                ? "High — both compared values came from deterministic live project facts; no model provider was invoked."
                : "High for host-confirmed values and scope semantics; limited for any user-reported Filter value because it is not present in the AI evidence snapshot. No model provider was invoked.");

            answer = builder.ToString().Trim();
            return true;
        }

        private static bool TryBuildUnknownCategoryExplanation(
            AiWorkflowContextSnapshot context,
            string question,
            string? activeScreen,
            out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            if (!ContainsAny(
                    text,
                    "what does unknown mean",
                    "explain unknown",
                    "why are elements unknown",
                    "why is unknown so high",
                    "unknown category meaning"))
            {
                return false;
            }

            var facts = MergeFacts(context);
            var sceneCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.ResolvedCategoryCounts,
                AiWorkflowFactKeys.TopResolvedCategories);
            var planningCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.PlanningCategoryCounts,
                AiWorkflowFactKeys.TopPlanningCategories,
                AiWorkflowFactKeys.FilterableCategoryCounts,
                AiWorkflowFactKeys.TopFilterableCategories);
            var rawCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.RawIfcTypeCounts,
                AiWorkflowFactKeys.TopRawIfcTypes);

            sceneCounts.TryGetValue("Unknown", out var sceneUnknown);
            planningCounts.TryGetValue("Unknown", out var planningUnknown);

            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("`Unknown` means the application could not resolve that scene object to a supported planning category from the available IFC identity, hierarchy, and mapping evidence. It does not mean the IFC object is missing or invalid.");
            builder.AppendLine();
            builder.AppendLine("## Current counts");
            if (planningCounts.Count > 0)
                builder.AppendLine($"- Planning/filterable Unknown elements: {planningUnknown:N0}");
            else
                builder.AppendLine("- Planning/filterable Unknown count: not exposed in the current AI context.");

            if (sceneCounts.Count > 0)
            {
                builder.AppendLine($"- Complete-scene Unknown objects: {sceneUnknown:N0}");
                if (context.ElementCount > 0)
                {
                    var percentage = sceneUnknown * 100d / context.ElementCount;
                    builder.AppendLine($"- Complete-scene unresolved share: {percentage:F1}% of {context.ElementCount:N0} BIM/model objects.");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Why the number can be high");
            if (HasAssemblyAndPartStructure(rawCounts, out var assemblyCount, out var partCount))
            {
                builder.AppendLine($"- The captured IFC contains {assemblyCount:N0} IfcElementAssembly object(s) and {partCount:N0} IfcBuildingElementPart object(s). Child parts can remain Unknown even when their parent assembly has a resolved planning category.");
            }
            else
            {
                builder.AppendLine("- Child parts, reinforcement, grids/reference geometry, accessories, and unsupported IFC classes can remain unresolved even when their parent assembly is usable for planning.");
            }
            builder.AppendLine("- Unknown should therefore be reviewed by object role and parent/child relationship, not treated as one physical lift category.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            AppendPlanningCategoryEvidence(builder, facts);
            AppendFactIfPresent(builder, context, AiWorkflowFactKeys.TopRawIfcTypes, "Top IFC classes");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("IFC MODEL → Export Element List / inspect raw IFC type, resolved category, role, parent assembly, and IncludeInGa fields");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High for the meaning and reported counts; the exact reason for each Unknown object requires row-level IFC/export evidence.");

            answer = builder.ToString().Trim();
            return true;
        }

        private static bool TryBuildVisibleElementAnswer(
            AiWorkflowContextSnapshot context,
            string question,
            string? activeScreen,
            out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var asksVisible = ContainsAny(
                                  text,
                                  "currently visible",
                                  "visible elements",
                                  "elements visible",
                                  "shown elements",
                                  "elements shown",
                                  "visible now")
                              && ContainsAny(text, "element", "elements", "bim", "model", "table", "filter");
            if (!asksVisible)
                return false;

            var facts = MergeFacts(context);
            var filterableTotal = GetFirstInt(facts, AiWorkflowFactKeys.FilterableElementCount);
            var visibleFilterable = GetFirstInt(facts, AiWorkflowFactKeys.VisibleFilterableElementCount);

            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            if (visibleFilterable.HasValue)
            {
                var denominator = filterableTotal.HasValue
                    ? $" of {filterableTotal.Value:N0} filterable planning element(s)"
                    : " filterable planning element(s)";
                builder.AppendLine($"The engineering filter currently exposes {visibleFilterable.Value:N0}{denominator} as visible.");
            }
            else if (context.ElementTableTotalCount > 0)
            {
                builder.AppendLine($"The live AI context currently exposes {context.ElementTableVisibleCount:N0} visible element-table item(s) of {context.ElementTableTotalCount:N0}.");
                builder.AppendLine("The 3D/filter-dialog visible count is not exposed to the AI, so I will not treat the table count as the filterable planning count.");
            }
            else
            {
                builder.AppendLine("The current AI context does not expose a deterministic visible-element count.");
            }

            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            AppendPlanningCategoryEvidence(builder, facts);
            builder.AppendLine();
            builder.AppendLine("## Scope warning");
            builder.AppendLine("Scene BIM objects, element-table rows, and filterable planning elements are distinct scopes and must not be used interchangeably.");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine(visibleFilterable.HasValue
                ? "HOME → Filter / verify the live filter scope and visible count"
                : "HOME → Filter / inspect the authoritative 3D planning-filter count");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine(visibleFilterable.HasValue
                ? "High — the host exposed an explicit filterable visibility count."
                : "High for the table count; unavailable for the 3D/filter-dialog count because that fact is not present in the AI context.");

            answer = builder.ToString().Trim();
            return true;
        }

        private static bool TryBuildTotalElementAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var asksTotalElements = ContainsAny(
                                        text,
                                        "how many elements", "total elements", "element count", "number of elements", "count elements",
                                        "how many bim objects", "total bim objects", "bim object count", "number of bim objects", "count bim objects",
                                        "how many model objects", "total model objects", "model object count", "number of model objects")
                                    && !CategoryTargets.Any(target => ContainsAny(text, target.Aliases));
            if (!asksTotalElements)
                return false;

            answer = BuildCountAnswer(
                context,
                activeScreen,
                question,
                new CountMatch("scene BIM/model object(s)", context.ElementCount, "captured workflow scene-object count", "This is the full scene BIM/model object count. Planning/readiness counts can be lower when IFC reference geometry is intentionally excluded."),
                "IFC MODEL → Export Element List / verify model scope");
            return true;
        }

        private static bool TryBuildCandidateAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            if (!ContainsAny(text, "ga candidate", "ga candidates", "trusted lift", "trusted lift unit", "trusted lift units", "valid lift unit", "valid lift units", "lift unit", "lift units", "lift candidate", "lift candidates"))
                return false;

            var facts = MergeFacts(context);
            var hasZeroTrustedBlocker = HasZeroTrustedLiftUnitBlocker(context, facts);
            var trusted = GetFirstInt(facts, AiWorkflowFactKeys.TrustedGroupedLiftUnits, AiWorkflowFactKeys.TrustedGaLiftUnits);
            if (!trusted.HasValue)
                trusted = hasZeroTrustedBlocker ? 0 : GetFirstInt(facts, AiWorkflowFactKeys.GaCandidateElements);
            var raw = GetFirstInt(facts, AiWorkflowFactKeys.RawCandidateLikeElements, AiWorkflowFactKeys.GaCandidateElements);
            var blocked = GetFirstInt(facts, AiWorkflowFactKeys.GaBlockedElements);
            var estimated = GetFirstInt(facts, AiWorkflowFactKeys.EstimatedWeightBlockedCount);
            var missingTrusted = GetFirstInt(facts, AiWorkflowFactKeys.MissingTrustedWeightCount);

            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            if (trusted.HasValue)
                builder.AppendLine($"There are {trusted.Value:N0} trusted GA-ready lift unit candidate(s) in the captured deterministic readiness context.");
            else
                builder.AppendLine("The current AI context does not expose a trusted GA-ready lift-unit count.");
            builder.AppendLine();

            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            if (raw.HasValue)
                builder.AppendLine($"- Raw/import-screen candidate-like elements: {raw.Value:N0}");
            if (trusted.HasValue)
                builder.AppendLine($"- Trusted GA-ready lift units: {trusted.Value:N0}");
            if (blocked.HasValue)
                builder.AppendLine($"- Deterministically blocked elements: {blocked.Value:N0}");
            if (estimated.HasValue)
                builder.AppendLine($"- Estimated/fallback-weight elements blocked from GA: {estimated.Value:N0}");
            if (missingTrusted.HasValue)
                builder.AppendLine($"- Elements missing trusted weight evidence: {missingTrusted.Value:N0}");
            builder.AppendLine();

            builder.AppendLine("## Caveat");
            builder.AppendLine("Raw candidate-like element counts are not the same as trusted grouped lift units. The optimizer may still block GA when grouping, trusted-weight, COG, or role evidence is insufficient.");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine(trusted.GetValueOrDefault() > 0 ? "ANALYSIS → Run Optimizer after zones, crane, and load-chart evidence are complete" : "IFC MODEL → Export Element List / trusted weight-source review");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine(trusted.HasValue ? "High — this answer used deterministic readiness facts." : "Medium — total element context exists, but trusted lift-unit facts were not exposed.");
            answer = builder.ToString().Trim();
            return true;
        }

        private static bool TryBuildThresholdAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var match = Regex.Match(text, @"(?:over|above|greater than|more than|>|>=)\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>kg|kilogram|kilograms|t|ton|tons|tonne|tonnes)?", RegexOptions.IgnoreCase);
            if (!match.Success || !ContainsAny(text, "weight", "kg", "ton", "tonne", ">"))
                return false;

            if (!double.TryParse(match.Groups["value"].Value, out var threshold))
                return false;

            var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : "kg";
            var thresholdKg = unit.StartsWith("t", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("ton", StringComparison.OrdinalIgnoreCase)
                ? threshold * 1000d
                : threshold;

            var facts = MergeFacts(context);
            int? count = null;
            if (Math.Abs(thresholdKg - 3000d) < 0.001)
                count = GetFirstInt(facts, AiWorkflowFactKeys.WeightOver3000KgCount);
            else if (Math.Abs(thresholdKg - 5000d) < 0.001)
                count = GetFirstInt(facts, AiWorkflowFactKeys.WeightOver5000KgCount);
            else if (Math.Abs(thresholdKg - 1000d) < 0.001)
                count = GetFirstInt(facts, AiWorkflowFactKeys.WeightOver1000KgCount);

            if (!count.HasValue)
            {
                answer = BuildUnavailableAnswer(context, question, activeScreen, $"elements over {thresholdKg:N0} kg");
                return true;
            }

            answer = BuildCountAnswer(
                context,
                activeScreen,
                question,
                new CountMatch($"element(s) over {thresholdKg:N0} kg", count.Value, "captured IFC weight-band facts", "This count uses imported element WeightTons values. It is a model-data count, not a capacity/GA eligibility approval."),
                "IFC MODEL → Export Element List / verify weight source");
            return true;
        }

        private static bool TryBuildCategoryAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var facts = MergeFacts(context);
            var planningCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.PlanningCategoryCounts,
                AiWorkflowFactKeys.TopPlanningCategories,
                AiWorkflowFactKeys.FilterableCategoryCounts,
                AiWorkflowFactKeys.TopFilterableCategories);
            var resolvedCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.ResolvedCategoryCounts,
                AiWorkflowFactKeys.TopResolvedCategories);
            var rawCounts = BuildCountMap(
                facts,
                AiWorkflowFactKeys.RawIfcTypeCounts,
                AiWorkflowFactKeys.TopRawIfcTypes);
            var preferRaw = ContainsAny(text, "raw ifc", "ifc class", "ifc entity", "raw class", "raw type", "ifctype", "ifc type");
            var requestedTargets = FindCategoryTargets(text);

            if (!preferRaw && requestedTargets.Count > 1)
            {
                answer = BuildMultiCategoryComparisonAnswer(
                    context,
                    activeScreen,
                    requestedTargets,
                    planningCounts,
                    resolvedCounts,
                    facts);
                return true;
            }

            foreach (var target in requestedTargets)
            {

                var planningMatch = FindCount(planningCounts, target);
                if (planningMatch != null)
                {
                    planningMatch = planningMatch with
                    {
                        Label = $"filterable planning element(s) categorized as {target.CanonicalLabel}",
                        Source = "planning/filterable BIM category summary",
                        Caveat = "This is the engineering planning/filter scope, which can intentionally exclude child parts, reinforcement, reference geometry, and other complete-scene objects."
                    };
                }

                var sceneMatch = FindCount(resolvedCounts, target);
                var rawMatch = FindCount(rawCounts, target);

                if (!preferRaw
                    && planningMatch != null
                    && IsPlanningRecommendationQuestion(text))
                {
                    answer = BuildPlanningRecommendationAnswer(
                        context,
                        activeScreen,
                        planningMatch,
                        sceneMatch,
                        facts,
                        target.CanonicalLabel);
                    return true;
                }

                if (!preferRaw && planningMatch != null)
                {
                    answer = BuildScopedCategoryCountAnswer(
                        context,
                        activeScreen,
                        planningMatch,
                        sceneMatch,
                        rawCounts,
                        facts,
                        target.CanonicalLabel);
                    return true;
                }

                var match = preferRaw
                    ? rawMatch ?? sceneMatch
                    : sceneMatch ?? rawMatch;

                if (match == null)
                {
                    answer = BuildUnavailableAnswer(context, question, activeScreen, target.UnavailableNoun);
                    return true;
                }

                answer = preferRaw
                    ? BuildCountAnswer(context, activeScreen, question, match, "IFC MODEL → Export Element List / verify raw IFC class")
                    : BuildSceneOnlyCategoryCountAnswer(
                        context,
                        activeScreen,
                        match,
                        rawCounts,
                        facts,
                        target.CanonicalLabel);
                return true;
            }

            if (ContainsAny(text, "category breakdown", "categories", "category counts", "breakdown"))
            {
                var builder = new StringBuilder();
                builder.AppendLine("## Answer");
                builder.AppendLine("Here is the deterministic BIM category breakdown from the captured project snapshot:");
                builder.AppendLine();
                if (planningCounts.Count > 0)
                {
                    builder.AppendLine("## Planning/filterable categories");
                    foreach (var item in planningCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Take(20))
                        builder.AppendLine($"- {item.Key}: {item.Value:N0}");

                    builder.AppendLine();
                    builder.AppendLine("## Resolved categories — complete IFC scene");
                }
                else
                {
                    builder.AppendLine("## Resolved categories — complete IFC scene");
                    builder.AppendLine("- Planning/filterable category counts are not exposed in the current AI context.");
                }

                if (resolvedCounts.Count == 0)
                    builder.AppendLine("- No complete-scene resolved category counts are exposed in the current AI context.");
                else
                    foreach (var item in resolvedCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Take(20))
                        builder.AppendLine($"- {item.Key}: {item.Value:N0}");

                AppendUnknownCoverage(builder, context, resolvedCounts);
                builder.AppendLine();
                builder.AppendLine("## Evidence");
                AppendModelEvidence(builder, context, activeScreen);
                builder.AppendLine("- Source: captured IFC category contributor facts.");
                AppendCountScopeEvidence(builder, context);
                AppendPlanningCategoryEvidence(builder, facts);
                builder.AppendLine();
                builder.AppendLine("## Caveat");
                if (planningCounts.Count > 0)
                {
                    builder.AppendLine("Planning/filterable counts are the primary engineering scope. Complete-scene resolved counts can be higher because parent assemblies and child parts are separate IFC objects.");
                }
                else
                {
                    builder.AppendLine("These are complete-scene resolved counts, not authoritative planning/filter counts. The host must expose planning-category facts before the AI can safely use them as the default engineering answer.");
                }
                answer = builder.ToString().Trim();
                return true;
            }

            return false;
        }

        private static bool IsPlanningRecommendationQuestion(string normalizedQuestion) =>
            ContainsAny(
                normalizedQuestion,
                "which count should i use",
                "what count should i use",
                "count should i use",
                "use for lift planning",
                "for lift planning",
                "planning count should",
                "which count is appropriate");

        private static string BuildPlanningRecommendationAnswer(
            AiWorkflowContextSnapshot context,
            string? activeScreen,
            CountMatch planningMatch,
            CountMatch? sceneMatch,
            IReadOnlyDictionary<string, string> facts,
            string categoryLabel)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"Use the planning/filterable count for lift planning: **{planningMatch.Count:N0} {categoryLabel} elements**.");

            if (sceneMatch != null)
            {
                builder.AppendLine($"The complete IFC scene separately contains {sceneMatch.Count:N0} objects classified as {categoryLabel}. That complete-scene value is useful for model classification and hierarchy review, not as the lift-planning quantity.");
            }

            builder.AppendLine();
            builder.AppendLine("## Why");
            builder.AppendLine("- Planning/filterable counts represent the top-level engineering elements exposed for filtering and lift review.");
            builder.AppendLine("- Complete-scene counts can also include categorized parent assemblies and child/component objects.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- Planning/filterable source: {planningMatch.Source}.");
            if (sceneMatch != null)
                builder.AppendLine($"- Complete-scene source: {sceneMatch.Source}.");
            AppendPlanningCategoryEvidence(builder, facts);
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine(sceneMatch != null
                ? "High — the planning recommendation and comparison value came from explicit deterministic host facts; no model provider was invoked."
                : "High — the planning recommendation came from an explicit deterministic host fact; no model provider was invoked.");
            return builder.ToString().Trim();
        }

        private static string BuildMultiCategoryComparisonAnswer(
            AiWorkflowContextSnapshot context,
            string? activeScreen,
            IReadOnlyList<QueryTarget> targets,
            IReadOnlyDictionary<string, int> planningCounts,
            IReadOnlyDictionary<string, int> sceneCounts,
            IReadOnlyDictionary<string, string> facts)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("Here are the host-confirmed category counts in both scopes:");
            builder.AppendLine();
            builder.AppendLine("## Count comparison");

            foreach (var target in targets)
            {
                var planningMatch = FindCount(planningCounts, target);
                var sceneMatch = FindCount(sceneCounts, target);
                var planningText = planningMatch == null ? "not exposed" : planningMatch.Count.ToString("N0");
                var sceneText = sceneMatch == null ? "not exposed" : sceneMatch.Count.ToString("N0");
                var differenceText = "not available";
                if (planningMatch != null && sceneMatch != null)
                {
                    var difference = sceneMatch.Count - planningMatch.Count;
                    differenceText = difference > 0
                        ? $"{difference:N0} additional complete-scene objects"
                        : difference < 0
                            ? $"{Math.Abs(difference):N0} fewer complete-scene objects"
                            : "0";
                }

                builder.AppendLine($"- {target.CanonicalLabel}: **{planningText} planning/filterable**; **{sceneText} complete scene**; difference **{differenceText}**.");
            }

            builder.AppendLine();
            builder.AppendLine("## Scope guidance");
            builder.AppendLine("- Use planning/filterable counts for engineering filtering and lift-planning quantities.");
            builder.AppendLine("- Use complete-scene counts for model classification, hierarchy, and parent/child auditing.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            AppendPlanningCategoryEvidence(builder, facts);
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — all exposed values came from deterministic live project facts; no model provider was invoked.");
            return builder.ToString().Trim();
        }

        private static string BuildScopedCategoryCountAnswer(
            AiWorkflowContextSnapshot context,
            string? activeScreen,
            CountMatch planningMatch,
            CountMatch? sceneMatch,
            IReadOnlyDictionary<string, int> rawCounts,
            IReadOnlyDictionary<string, string> facts,
            string categoryLabel)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"There are {planningMatch.Count:N0} filterable planning elements classified as {categoryLabel}.");

            if (sceneMatch != null && sceneMatch.Count != planningMatch.Count)
            {
                var delta = sceneMatch.Count - planningMatch.Count;
                builder.AppendLine($"The complete IFC scene separately contains {sceneMatch.Count:N0} objects classified as {categoryLabel}.");
                builder.AppendLine(delta > 0
                    ? $"That is {delta:N0} additional complete-scene objects. The complete scene can include parent assemblies and categorized child/component objects, while the planning/filter scope represents the top-level engineering elements exposed for filtering and lift review."
                    : "The scopes are different even though the complete-scene value is not higher. Review the host category mapping before treating either count as interchangeable.");
            }

            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine("- Count scope: planning/filterable is primary; complete scene is the comparison scope.");
            builder.AppendLine($"- Primary source: {planningMatch.Source}.");
            if (sceneMatch != null)
                builder.AppendLine($"- Complete-scene resolved source: {sceneMatch.Source} ({sceneMatch.Count:N0}).");
            AppendPlanningCategoryEvidence(builder, facts);
            AppendCountScopeEvidence(builder, context);

            if (HasAssemblyAndPartStructure(rawCounts, out var assemblyCount, out var partCount))
                builder.AppendLine($"- IFC hierarchy evidence: {assemblyCount:N0} assembly object(s); {partCount:N0} building-element part object(s).");

            builder.AppendLine();
            builder.AppendLine("## Caveat");
            builder.AppendLine(planningMatch.Caveat);
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("HOME → Filter / verify the planning scope, or IFC MODEL → Export Element List / inspect parent-child category mapping");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine(sceneMatch != null
                ? "High — both planning/filterable and complete-scene values came from explicit deterministic host facts."
                : "High for the planning/filterable count; no separate complete-scene value was exposed for comparison.");
            return builder.ToString().Trim();
        }

        private static string BuildSceneOnlyCategoryCountAnswer(
            AiWorkflowContextSnapshot context,
            string? activeScreen,
            CountMatch sceneMatch,
            IReadOnlyDictionary<string, int> rawCounts,
            IReadOnlyDictionary<string, string> facts,
            string categoryLabel)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"There are {sceneMatch.Count:N0} objects classified as {categoryLabel} in the complete IFC scene.");
            builder.AppendLine($"The Filter dialog's planning count for {categoryLabel} is not available to AI, so {sceneMatch.Count:N0} is not the planning/filter count.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine("- Count scope: complete IFC scene.");
            builder.AppendLine("- Planning/filterable category count: not exposed in the current AI context.");
            builder.AppendLine($"- Source: {sceneMatch.Source}.");
            AppendPlanningCategoryEvidence(builder, facts);
            AppendCountScopeEvidence(builder, context);

            if (HasAssemblyAndPartStructure(rawCounts, out var assemblyCount, out var partCount))
            {
                builder.AppendLine($"- IFC hierarchy evidence: {assemblyCount:N0} IfcElementAssembly object(s) and {partCount:N0} IfcBuildingElementPart object(s).");
            }

            builder.AppendLine();
            builder.AppendLine("## Caveat");
            builder.AppendLine("Complete-scene category counts can include both top-level assemblies and categorized child parts. The engineering filter may therefore show a lower count for the same label.");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("HOME → Filter / inspect the authoritative filterable planning count, or IFC MODEL → Export Element List / compare parent and child identifiers");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — the complete-scene count came from deterministic live project facts, no unavailable planning count was invented, and no model provider was invoked.");
            return builder.ToString().Trim();
        }

        private static void AppendUnknownCoverage(
            StringBuilder builder,
            AiWorkflowContextSnapshot context,
            IReadOnlyDictionary<string, int> resolvedCounts)
        {
            if (!resolvedCounts.TryGetValue("Unknown", out var unknownCount) || context.ElementCount <= 0)
                return;

            var percentage = unknownCount * 100d / context.ElementCount;
            builder.AppendLine();
            builder.AppendLine("## Classification coverage");
            builder.AppendLine($"- Unknown/unresolved complete-scene objects: {unknownCount:N0} ({percentage:F1}% of {context.ElementCount:N0}).");
            builder.AppendLine("- A high Unknown share can be normal for child parts, reinforcement, accessories, grids/reference objects, or unsupported mappings, but it limits broad category-based conclusions.");
        }

        private static void AppendPlanningCategoryEvidence(
            StringBuilder builder,
            IReadOnlyDictionary<string, string> facts)
        {
            var filterableTotal = GetFirstInt(facts, AiWorkflowFactKeys.FilterableElementCount);
            var visibleFilterable = GetFirstInt(facts, AiWorkflowFactKeys.VisibleFilterableElementCount);

            if (filterableTotal.HasValue)
                builder.AppendLine($"- Filterable planning elements: {filterableTotal.Value:N0}");
            if (visibleFilterable.HasValue)
                builder.AppendLine($"- Currently visible filterable planning elements: {visibleFilterable.Value:N0}");
            if (facts.TryGetValue(AiWorkflowFactKeys.CategoryCountScopeNote, out var scopeNote)
                && !string.IsNullOrWhiteSpace(scopeNote))
            {
                builder.AppendLine($"- Category scope note: {scopeNote.Trim()}");
            }
        }

        private static bool HasAssemblyAndPartStructure(
            IReadOnlyDictionary<string, int> rawCounts,
            out int assemblyCount,
            out int partCount)
        {
            assemblyCount = 0;
            partCount = 0;

            foreach (var pair in rawCounts)
            {
                var key = NormalizeKey(pair.Key);
                if (key.Equals(NormalizeKey("IfcElementAssembly"), StringComparison.OrdinalIgnoreCase))
                    assemblyCount = pair.Value;
                else if (key.Equals(NormalizeKey("IfcBuildingElementPart"), StringComparison.OrdinalIgnoreCase))
                    partCount = pair.Value;
            }

            return assemblyCount > 0 && partCount > 0;
        }

        private static string BuildCountAnswer(AiWorkflowContextSnapshot context, string? activeScreen, string question, CountMatch match, string nextClick)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"There are {match.Count:N0} {match.Label} in the captured imported model context.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- Source: {match.Source}.");
            AppendCountScopeEvidence(builder, context);
            AppendFactIfPresent(builder, context, AiWorkflowFactKeys.TopResolvedCategories, "Top resolved categories");
            AppendFactIfPresent(builder, context, AiWorkflowFactKeys.TopRawIfcTypes, "Top IFC classes");
            builder.AppendLine();
            builder.AppendLine("## Caveat");
            builder.AppendLine(match.Caveat);
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine(nextClick);
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — this answer was produced from deterministic live project facts before invoking Ollama/RAG.");
            return builder.ToString().Trim();
        }

        private static string BuildUnavailableAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, string requested)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine($"I cannot find a deterministic count for {requested} in the current AI context.");
            builder.AppendLine();
            builder.AppendLine("## Current evidence");
            AppendModelEvidence(builder, context, activeScreen);
            AppendFactIfPresent(builder, context, AiWorkflowFactKeys.TopResolvedCategories, "Top resolved categories");
            AppendFactIfPresent(builder, context, AiWorkflowFactKeys.TopRawIfcTypes, "Top IFC classes");
            builder.AppendLine();
            builder.AppendLine("## Recommendation");
            builder.AppendLine("Export the element list and inspect resolved category, raw IFC type, element name/type, and source property columns. I should not invent a count that is not exposed by the live model facts; do not invent missing BIM quantities.");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("IFC MODEL → Export Element List / inspect category fields");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High that the requested deterministic count is not currently exposed to the AI context; not a statement that the raw IFC file lacks it.");
            return builder.ToString().Trim();
        }

        private static string BuildNoIfcAnswer(string question, string? activeScreen, AiWorkflowContextSnapshot context)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("No BIM element count is available yet because no IFC/model elements are loaded in the current scene.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- Zones: {context.ZoneCount:N0}");
            builder.AppendLine($"- Cranes: {context.CraneCount:N0}");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("IFC MODEL → Load IFC");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — the live element count is zero, so any slab/beam/column/category count would be invented.");
            return builder.ToString().Trim();
        }

        private static string BuildNoContextAnswer(string question)
        {
            return "## Answer\nNo live project context is available, so I cannot answer this BIM count question deterministically.\n\n## Recommendation\nOpen/create a project and load IFC data, then ask the count question again.\n\n## Confidence\nHigh — no context snapshot was supplied.";
        }

        private static bool TryDetectStaleScreenFacts(AiWorkflowContextSnapshot context, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var facts = MergeFacts(context);

            var factRevision = GetFirstLong(facts, AiWorkflowFactKeys.HostModelRevision, AiWorkflowFactKeys.ContextModelRevision);
            if (factRevision.HasValue && factRevision.Value != context.ModelRevision)
            {
                answer = BuildExplicitMismatchAnswer(
                    context,
                    activeScreen,
                    $"screen facts were captured for model revision {factRevision.Value:N0}, but the answer snapshot is revision {context.ModelRevision:N0}");
                return true;
            }

            var factFingerprint = GetFirstNonEmpty(facts, AiWorkflowFactKeys.HostModelFingerprint, AiWorkflowFactKeys.ContextModelFingerprint);
            if (!string.IsNullOrWhiteSpace(factFingerprint)
                && !string.IsNullOrWhiteSpace(context.ModelFingerprint)
                && !string.Equals(factFingerprint, context.ModelFingerprint, StringComparison.Ordinal))
            {
                answer = BuildExplicitMismatchAnswer(
                    context,
                    activeScreen,
                    "screen facts were captured for a different model fingerprint than the answer snapshot");
                return true;
            }

            var sceneCount = GetFirstInt(facts, AiWorkflowFactKeys.SceneBimObjectCount, AiWorkflowFactKeys.ImportedSceneBimObjectCount, AiWorkflowFactKeys.LiveSceneBimObjectCount);
            if (sceneCount.HasValue && sceneCount.Value != context.ElementCount)
            {
                answer = BuildExplicitMismatchAnswer(
                    context,
                    activeScreen,
                    $"screen facts reported {sceneCount.Value:N0} scene BIM object(s), but the answer snapshot has {context.ElementCount:N0}");
                return true;
            }

            return false;
        }

        private static string BuildExplicitMismatchAnswer(AiWorkflowContextSnapshot context, string? activeScreen, string reason)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Answer");
            builder.AppendLine("I detected an explicit host snapshot mismatch, so I will not answer this BIM count from mixed model evidence.");
            builder.AppendLine();
            builder.AppendLine("## Freshness check");
            AppendModelEvidence(builder, context, activeScreen);
            builder.AppendLine($"- Mismatch: {reason}.");
            builder.AppendLine();
            builder.AppendLine("## Recommendation");
            builder.AppendLine("Refresh AI context once and ask again after the current IFC load, merge, move, or delete operation is finished.");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High that the explicit host revision/fingerprint evidence is mixed; no count was invented.");
            return builder.ToString().Trim();
        }

        private static void AppendModelEvidence(StringBuilder builder, AiWorkflowContextSnapshot context, string? activeScreen)
        {
            builder.AppendLine($"- Active workflow area: {NormalizeInline(activeScreen, "unknown")}");
            builder.AppendLine($"- Model: {BuildModelContextLabel(context)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Model fingerprint: {ShortFingerprint(context.ModelFingerprint)}");
            builder.AppendLine($"- Complete-scene BIM/model objects: {context.ElementCount:N0}");
            if (context.ElementTableTotalCount > 0)
            {
                builder.AppendLine($"- Element table items: {context.ElementTableTotalCount:N0}");
                builder.AppendLine($"- Currently visible table items: {context.ElementTableVisibleCount:N0}");
            }
        }

        private static void AppendCountScopeEvidence(StringBuilder builder, AiWorkflowContextSnapshot context)
        {
            var facts = MergeFacts(context);
            var readinessCount = GetFirstInt(facts, AiWorkflowFactKeys.ReadinessElementCount, AiWorkflowFactKeys.PlanningElementCount, AiWorkflowFactKeys.GaReadinessElementCount);
            var excludedReferenceCount = GetFirstInt(facts, AiWorkflowFactKeys.ReferenceGeometryExcludedCount, AiWorkflowFactKeys.ReadinessExcludedReferenceGeometryCount);

            if (readinessCount.HasValue && readinessCount.Value != context.ElementCount)
                builder.AppendLine($"- Planning/readiness element count: {readinessCount.Value:N0}");
            if (excludedReferenceCount.HasValue && excludedReferenceCount.Value > 0)
                builder.AppendLine($"- IFC reference geometry excluded from readiness: {excludedReferenceCount.Value:N0}");
            if (facts.TryGetValue(AiWorkflowFactKeys.ElementCountScopeNote, out var note) && !string.IsNullOrWhiteSpace(note))
                builder.AppendLine($"- Count scope note: {note.Trim()}");
        }

        private static string BuildModelContextLabel(AiWorkflowContextSnapshot context)
        {
            if (!string.IsNullOrWhiteSpace(context.ModelDisplayName))
                return context.ModelDisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(context.CurrentProjectName))
                return context.CurrentProjectName.Trim();

            return context.ElementCount > 0 ? "Imported IFC model" : "No IFC model loaded";
        }

        private static string ShortFingerprint(string? fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
                return "not provided";

            var trimmed = fingerprint.Trim();
            return trimmed.Length <= 32 ? trimmed : trimmed[..32] + "…";
        }

        private static CountMatch? FindCount(IReadOnlyDictionary<string, int> counts, QueryTarget target)
        {
            foreach (var alias in target.Aliases.Prepend(target.CanonicalLabel))
            {
                var normalizedAlias = NormalizeKey(alias);
                foreach (var pair in counts)
                {
                    if (NormalizeKey(pair.Key).Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase))
                    {
                        var isRaw = pair.Key.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase);
                        return new CountMatch(
                            isRaw ? $"raw IFC class {pair.Key} element(s)" : $"elements categorized as {pair.Key}",
                            pair.Value,
                            isRaw ? "raw IFC type summary" : "resolved BIM category summary",
                            isRaw
                                ? "This is a raw IFC entity/type count. The app's resolved planning category may differ."
                                : "This is the app's resolved BIM category, not necessarily the raw IFC entity class.");
                    }
                }
            }

            return null;
        }

        private static bool HasZeroTrustedLiftUnitBlocker(AiWorkflowContextSnapshot context, IReadOnlyDictionary<string, string> facts)
        {
            if (facts.TryGetValue("OptimizerPrecheckStatus", out var optimizerPrecheck)
                && optimizerPrecheck.Equals("BlockedNoGaCandidates", StringComparison.OrdinalIgnoreCase))
                return true;

            if (facts.TryGetValue("PreRunStatus", out var preRun)
                && preRun.Equals("BlockedNoGaCandidates", StringComparison.OrdinalIgnoreCase))
                return true;

            return context.ScreenContexts
                .SelectMany(screen => screen.Issues)
                .Any(issue => issue.Code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
                              || issue.Code.Equals("GaPreRunNoCandidates", StringComparison.OrdinalIgnoreCase)
                              || issue.Code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyDictionary<string, string> MergeFacts(AiWorkflowContextSnapshot context)
        {
            return context.ScreenContexts
                .SelectMany(screen => screen.Facts)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyDictionary<string, int> BuildCountMap(
            IReadOnlyDictionary<string, string> facts,
            params string[] factKeys)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var factKey in factKeys)
            {
                if (facts.TryGetValue(factKey, out var value))
                    ParseDelimitedCounts(value, map);
            }

            return map;
        }

        private static void ParseDelimitedCounts(string value, IDictionary<string, int> map)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            foreach (Match match in Regex.Matches(value, @"(?<name>[^,;|=]+?)\s*\((?<count>[\d,]+)\)"))
            {
                if (int.TryParse(CleanNumber(match.Groups["count"].Value), out var parenCount))
                {
                    var key = match.Groups["name"].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                        map[key] = parenCount;
                }
            }

            foreach (var part in value.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;

                var equals = trimmed.LastIndexOf('=');
                if (equals > 0 && int.TryParse(CleanNumber(trimmed[(equals + 1)..]), out var equalsCount))
                {
                    var key = trimmed[..equals].Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                        map[key] = equalsCount;
                }
            }
        }

        private static int? GetFirstInt(IReadOnlyDictionary<string, string> facts, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (facts.TryGetValue(key, out var value) && int.TryParse(CleanNumber(value), out var parsed))
                    return parsed;
            }

            return null;
        }

        private static long? GetFirstLong(IReadOnlyDictionary<string, string> facts, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (facts.TryGetValue(key, out var value) && long.TryParse(CleanNumber(value), out var parsed))
                    return parsed;
            }

            return null;
        }

        private static string? GetFirstNonEmpty(IReadOnlyDictionary<string, string> facts, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (facts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }

        private static void AppendFactIfPresent(StringBuilder builder, AiWorkflowContextSnapshot context, string key, string label)
        {
            var facts = MergeFacts(context);
            if (facts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                builder.AppendLine($"- {label}: {value}");
        }

        private static bool IsCountScopeDiscrepancyQuestion(string normalizedQuestion)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuestion))
                return false;

            var targets = FindCategoryTargets(normalizedQuestion);
            var target = targets.FirstOrDefault();
            var hasCountSubject = target != null || ContainsAny(
                normalizedQuestion,
                "category", "categories", "element", "elements", "object", "objects", "bim", "ifc");
            if (!hasCountSubject)
                return false;

            var hasPlanningScope = ContainsAny(
                normalizedQuestion,
                "ifc element filter", "filter dialog", "filterable", "planning", "planning count", "filter count", "the filter", "filter says", "filter shows", "filter show", "filter reports");
            var hasSceneScope = ContainsAny(
                normalizedQuestion,
                "complete-scene", "complete scene", "ifc-scene", "ifc scene", "bim context", "scene count", "scene counts", "scene object", "scene objects", "bim contains", "bim says", "bim reports");
            var hasComparisonLanguage = ContainsAny(
                normalizedQuestion,
                "compare", "comparison", "difference", "different", "differ", "discrepancy", "mismatch", "versus", " vs ", "while", "why does", "why do", "why is", "why are", "twice as many", "more than", "less than");
            var numericCount = Regex.Matches(normalizedQuestion, @"\b\d[\d,]*\b").Count;
            var hasAlternativeValues = target != null
                && numericCount >= 2
                && ContainsAny(
                    normalizedQuestion,
                    " or ",
                    "are there",
                    "do we have",
                    "which is correct",
                    "which count is correct",
                    "is it");

            if (hasAlternativeValues)
                return true;

            var hasSingleReportedScopeValue = target != null
                && numericCount >= 1
                && (hasPlanningScope || hasSceneScope)
                && Regex.IsMatch(
                    normalizedQuestion,
                    @"\b(says?|shows?|reports?|contains?|has|is)\b",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (hasSingleReportedScopeValue)
                return true;

            var asksWhichCountIsCorrect = target != null
                && Regex.IsMatch(
                    normalizedQuestion,
                    @"\b(which|what)\b.*\bcount\b.*\b(correct|right|authoritative|use)\b",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (asksWhichCountIsCorrect)
                return true;

            // Mentioning both scopes is not by itself a discrepancy request. A scoped
            // category-breakdown prompt such as "show the category counts for the
            // planning/filter scope and the complete IFC scene" must continue to the
            // category-breakdown handler. Treat it as a comparison only when the user
            // supplies comparison language or two explicit numeric values.
            if (hasPlanningScope && hasSceneScope && (hasComparisonLanguage || numericCount >= 2))
                return true;

            if (target != null
                && hasComparisonLanguage
                && (hasPlanningScope || hasSceneScope)
                && ContainsAny(normalizedQuestion, "twice as many", "more than", "less than", "different", "differ", "why"))
            {
                return true;
            }

            if (target != null && ContainsAny(normalizedQuestion, "count scope", "count scopes"))
                return true;

            return ContainsAny(
                       normalizedQuestion,
                       "why do planning and scene counts differ",
                       "why are planning and scene counts different",
                       "explain the count scopes",
                       "explain count scopes",
                       "count-scope discrepancy",
                       "count scope discrepancy")
                   || (ContainsAny(normalizedQuestion, "count scope", "count scopes") && hasComparisonLanguage);
        }

        private static QueryTarget? FindCategoryTarget(string normalizedQuestion) =>
            FindCategoryTargets(normalizedQuestion).FirstOrDefault();

        private static IReadOnlyList<QueryTarget> FindCategoryTargets(string normalizedQuestion)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuestion))
                return Array.Empty<QueryTarget>();

            return CategoryTargets
                .Where(target => target.Aliases.Any(alias => ContainsAlias(normalizedQuestion, alias)))
                .ToList();
        }

        private static bool ContainsAlias(string text, string alias)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(alias))
                return false;

            return Regex.IsMatch(
                text,
                $@"(?<![a-z0-9]){Regex.Escape(alias.ToLowerInvariant())}(?![a-z0-9])",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }

        private static ReportedScopeCount? TryExtractReportedScopeCount(
            string normalizedQuestion,
            bool planningScope,
            QueryTarget? target)
        {
            var planningTerms = new[]
            {
                "ifc element filter", "filter dialog", "filterable", "planning", "the filter", "filter"
            };
            var sceneTerms = new[]
            {
                "complete ifc scene", "complete-scene", "complete scene", "ifc-scene", "ifc scene", "bim context", "scene context", "scene", "bim"
            };

            foreach (Match numberMatch in Regex.Matches(normalizedQuestion, @"\b\d[\d,]*\b"))
            {
                if (!int.TryParse(CleanNumber(numberMatch.Value), out var count))
                    continue;

                var prefixStart = Math.Max(0, numberMatch.Index - 120);
                var prefix = normalizedQuestion[prefixStart..numberMatch.Index];
                var planningBefore = LastIndexOfAny(prefix, planningTerms);
                var sceneBefore = LastIndexOfAny(prefix, sceneTerms);
                bool? assignedToPlanning = null;

                if (planningBefore >= 0 || sceneBefore >= 0)
                    assignedToPlanning = planningBefore > sceneBefore;

                if (!assignedToPlanning.HasValue)
                {
                    var suffixLength = Math.Min(60, normalizedQuestion.Length - (numberMatch.Index + numberMatch.Length));
                    var suffix = normalizedQuestion.Substring(numberMatch.Index + numberMatch.Length, suffixLength);
                    var nextNumber = Regex.Match(suffix, @"\b\d[\d,]*\b");
                    if (nextNumber.Success)
                        suffix = suffix[..nextNumber.Index];

                    var planningAfter = IndexOfAny(suffix, planningTerms);
                    var sceneAfter = IndexOfAny(suffix, sceneTerms);
                    var hasCountSubjectAfter = target != null
                        ? target.Aliases.Any(alias => ContainsAlias(suffix, alias))
                        : ContainsAny(suffix, "element", "elements", "object", "objects", "category", "categories", "count", "total");

                    if (hasCountSubjectAfter && (planningAfter >= 0 || sceneAfter >= 0))
                    {
                        assignedToPlanning = planningAfter >= 0
                                             && (sceneAfter < 0 || planningAfter < sceneAfter);
                    }
                }

                if (assignedToPlanning.HasValue && assignedToPlanning.Value == planningScope)
                {
                    return new ReportedScopeCount(
                        count,
                        planningScope
                            ? "reported in the user question as a Filter/planning value"
                            : "reported in the user question as a complete-scene/BIM value");
                }
            }

            return null;
        }

        private static int LastIndexOfAny(string text, IEnumerable<string> terms)
        {
            var result = -1;
            foreach (var term in terms)
                result = Math.Max(result, text.LastIndexOf(term, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static int IndexOfAny(string text, IEnumerable<string> terms)
        {
            var result = -1;
            foreach (var term in terms)
            {
                var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (result < 0 || index < result))
                    result = index;
            }
            return result;
        }

        private static void AppendScopeCountLine(
            StringBuilder builder,
            string scopeLabel,
            string categoryPhrase,
            int? hostCount,
            ReportedScopeCount? reportedCount,
            string scopeDescription)
        {
            if (hostCount.HasValue)
            {
                builder.AppendLine($"- {scopeLabel} {categoryPhrase}: **{hostCount.Value:N0}** (host-confirmed from {scopeDescription}).");
                return;
            }

            if (reportedCount != null)
            {
                builder.AppendLine($"- {scopeLabel} {categoryPhrase}: **{reportedCount.Count:N0}** ({reportedCount.Provenance}; not independently confirmed by AI evidence).");
                return;
            }

            builder.AppendLine($"- {scopeLabel} {categoryPhrase}: **not exposed to AI**.");
        }

        private static IReadOnlySet<int> ExtractQuestionNumbers(string normalizedQuestion) =>
            Regex.Matches(normalizedQuestion, @"\b\d[\d,]*\b")
                .Select(match => int.TryParse(CleanNumber(match.Value), out var value) ? value : (int?)null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToHashSet();

        private static void AppendReportedMismatchWarning(
            StringBuilder builder,
            string scopeLabel,
            int? hostCount,
            ReportedScopeCount? reportedCount)
        {
            if (!hostCount.HasValue || reportedCount == null || hostCount.Value == reportedCount.Count)
                return;

            builder.AppendLine($"- Provenance warning: the user-reported {scopeLabel} value is {reportedCount.Count:N0}, but the host-confirmed value is {hostCount.Value:N0}. The host fact is authoritative for this answer snapshot.");
        }

        private static string BuildScopeProvenance(int? hostCount, ReportedScopeCount? reportedCount)
        {
            if (hostCount.HasValue)
            {
                if (reportedCount == null)
                    return "host-confirmed deterministic fact";

                return hostCount.Value == reportedCount.Count
                    ? "host-confirmed deterministic fact; the user-reported value agrees"
                    : $"host-confirmed deterministic fact; user reported {reportedCount.Count:N0}, which conflicts";
            }

            return reportedCount == null
                ? "not exposed in the current AI context"
                : $"{reportedCount.Provenance}; not host-confirmed";
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeQuestion(string value)
        {
            return value.Replace('\r', ' ').Replace('\n', ' ').Trim().ToLowerInvariant();
        }

        private static string NormalizeKey(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(ch);
            }

            var normalized = builder.ToString();
            if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 3)
                normalized = normalized[..^1];
            return normalized;
        }

        private static string CleanNumber(string value)
        {
            return value.Replace(",", string.Empty).Trim();
        }

        private static string NormalizeInline(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
