using CraneOptimizer.AI.Host;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F16_Filter.Services;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Converts the authoritative IFC Element Filter scope into the AI-owned fact
    /// contract. It deliberately publishes facts only; interpretation remains in
    /// CraneOptimizer.AI.
    /// </summary>
    internal static class PlanningFilterAiFactBuilder
    {
        public static IReadOnlyDictionary<string, string> Build(
            IReadOnlyList<PrecastElement> sceneElements,
            IReadOnlyList<PrecastElement>? visibleSceneElements = null)
        {
            ArgumentNullException.ThrowIfNull(sceneElements);

            var filterableElements = sceneElements
                .Where(ElementFilterScopeRules.IsPlanningFilterable)
                .ToList();

            var categoryGroups = filterableElements
                .Select(ElementFilterScopeRules.GetTypeLabel)
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AiWorkflowFactKeys.FilterableElementCount] = filterableElements.Count.ToString(),
                [AiWorkflowFactKeys.CategoryCountScopeNote] =
                    "Planning/filterable categories use the same scope as HOME → Filter: top-level LiftAssembly elements with IncludeInGa=true. Complete-scene categories can additionally include categorized child/component objects."
            };

            if (categoryGroups.Count > 0)
            {
                var countFact = BuildCountFact(categoryGroups);
                var topCountFact = BuildTopCountFact(categoryGroups, 20);

                facts[AiWorkflowFactKeys.PlanningCategoryCounts] = countFact;
                facts[AiWorkflowFactKeys.TopPlanningCategories] = topCountFact;
                facts[AiWorkflowFactKeys.FilterableCategoryCounts] = countFact;
                facts[AiWorkflowFactKeys.TopFilterableCategories] = topCountFact;
            }

            if (visibleSceneElements != null)
            {
                facts[AiWorkflowFactKeys.VisibleFilterableElementCount] = visibleSceneElements
                    .Count(ElementFilterScopeRules.IsPlanningFilterable)
                    .ToString();
            }

            return facts;
        }

        private static string BuildCountFact(IEnumerable<IGrouping<string, string>> groups) =>
            string.Join("; ", groups.Select(group => $"{group.Key}={group.Count()}"));

        private static string BuildTopCountFact(IEnumerable<IGrouping<string, string>> groups, int take) =>
            string.Join(", ", groups.Take(take).Select(group => $"{group.Key} ({group.Count():N0})"));
    }
}
