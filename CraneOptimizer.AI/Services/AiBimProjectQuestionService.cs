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
            if (ContainsAny(normalized, "building usage", "building type", "metadata", "property set", "classification"))
                return false;

            var asksForCount = ContainsAny(normalized,
                "how many", "count", "number of", "quantity of", "quantities of", "total", "breakdown", "summarize categories", "summarise categories");

            if (!asksForCount)
                return false;

            if (ContainsAny(normalized,
                    "element", "elements", "ifc", "bim", "model", "slab", "slabs", "beam", "beams", "column", "columns",
                    "wall", "walls", "window", "windows", "door", "doors", "stair", "stairs", "footing", "footings", "foundation", "foundations", "unknown", "assembly", "assemblies", "rebar", "reinforcing", "candidate", "candidates",
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

        private static bool TryBuildTotalElementAnswer(AiWorkflowContextSnapshot context, string question, string? activeScreen, out string answer)
        {
            answer = string.Empty;
            var text = NormalizeQuestion(question);
            var asksTotalElements = ContainsAny(text, "how many elements", "total elements", "element count", "number of elements", "count elements")
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
            builder.AppendLine(trusted.GetValueOrDefault() > 0 ? "ANALYZE → Run Optimizer after zones, crane, and load-chart evidence are complete" : "IFC MODEL → Export Element List / trusted weight-source review");
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
            var resolvedCounts = BuildCountMap(facts, AiWorkflowFactKeys.ResolvedCategoryCounts, AiWorkflowFactKeys.TopResolvedCategories);
            var rawCounts = BuildCountMap(facts, AiWorkflowFactKeys.RawIfcTypeCounts, AiWorkflowFactKeys.TopRawIfcTypes);
            var preferRaw = ContainsAny(text, "raw ifc", "ifc class", "ifc entity", "raw class", "raw type", "ifctype", "ifc type");

            foreach (var target in CategoryTargets)
            {
                if (!ContainsAny(text, target.Aliases))
                    continue;

                var match = preferRaw
                    ? FindCount(rawCounts, target) ?? FindCount(resolvedCounts, target)
                    : FindCount(resolvedCounts, target) ?? FindCount(rawCounts, target);

                if (match == null)
                {
                    answer = BuildUnavailableAnswer(context, question, activeScreen, target.UnavailableNoun);
                    return true;
                }

                answer = BuildCountAnswer(context, activeScreen, question, match, "IFC MODEL → Export Element List / verify category mapping");
                return true;
            }

            if (ContainsAny(text, "category breakdown", "categories", "category counts", "breakdown"))
            {
                var builder = new StringBuilder();
                builder.AppendLine("## Answer");
                builder.AppendLine("Here is the deterministic BIM category breakdown from the captured project snapshot:");
                builder.AppendLine();
                builder.AppendLine("## Resolved categories");
                if (resolvedCounts.Count == 0)
                    builder.AppendLine("- No resolved category counts are exposed in the current AI context.");
                else
                {
                    foreach (var item in resolvedCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Take(20))
                        builder.AppendLine($"- {item.Key}: {item.Value:N0}");
                }
                builder.AppendLine();
                builder.AppendLine("## Evidence");
                AppendModelEvidence(builder, context, activeScreen);
                builder.AppendLine("- Source: captured IFC category contributor facts.");
                AppendCountScopeEvidence(builder, context);
                builder.AppendLine();
                builder.AppendLine("## Caveat");
                builder.AppendLine("Resolved categories are the application/BIM mapping used for planning review; raw IFC entity counts can differ.");
                answer = builder.ToString().Trim();
                return true;
            }

            return false;
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
            builder.AppendLine($"- Active screen at send time: {NormalizeInline(activeScreen, "unknown")}");
            builder.AppendLine($"- Model context: {BuildModelContextLabel(context)}");
            builder.AppendLine($"- Model revision captured at send time: {context.ModelRevision:N0}");
            builder.AppendLine($"- Model fingerprint captured at send time: {ShortFingerprint(context.ModelFingerprint)}");
            builder.AppendLine($"- Total scene BIM/model objects: {context.ElementCount:N0}");
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
            return trimmed.Length <= 96 ? trimmed : trimmed[..96] + "…";
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

        private static IReadOnlyDictionary<string, int> BuildCountMap(IReadOnlyDictionary<string, string> facts, string countFactKey, string topFactKey)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (facts.TryGetValue(countFactKey, out var allCounts))
                ParseDelimitedCounts(allCounts, map);
            if (facts.TryGetValue(topFactKey, out var topCounts))
                ParseDelimitedCounts(topCounts, map);
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
