using CraneOptimizer.AI.Host;
using CraneOptimizer.Application.Services.Identity;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Exposes F5-style element identity, selection, and per-element risk evidence.
    /// </summary>
    public sealed class ElementManagementAiContextContributor : IAiScreenContextContributor
    {
        public string ScreenName => "Element review";

        public AiScreenContextSnapshot BuildSnapshot(MainViewModel shell)
        {
            var elements = shell.Scene.GetElements().Where(element => element.IncludeInElementTree).ToList();
            var selectedElements = shell.Scene.GetSelectedElements();
            var selected = selectedElements.FirstOrDefault() ?? shell.ElementManagement.SelectedElement;

            var lowConfidence = elements.Count(IsLowConfidenceIdentity);
            var missingTrustedWeight = elements.Count(element => element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity
                && element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty);
            var missingCog = elements.Count(HasMissingCog);
            var hiddenContributors = elements.Count(element => element.HasHiddenContributors || element.HiddenWeightContributorCount > 0);
            var validationWarnings = elements.Sum(element => element.ValidationWarnings?.Count ?? 0);

            var issues = new List<AiContextIssueSnapshot>();
            if (selected != null)
                issues.AddRange(BuildSelectedElementIssues(selected));

            if (lowConfidence > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ElementIdentityReviewNeeded",
                    "Warning",
                    "Identity",
                    $"{lowConfidence:N0} element(s) have no explicit assembly mark or low/none confidence mapping.",
                    "Review Assembly Mark, source, confidence, explicit flag, and validation warnings before using the lift mark in reports.",
                    "IFC MODEL → Element Management",
                    lowConfidence));
            }

            if (missingTrustedWeight > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ElementWeightReviewNeeded",
                    "Warning",
                    "Weight",
                    $"{missingTrustedWeight:N0} element(s) do not have an explicit trusted weight source.",
                    "Confirm source model quantities or provide explicit mass/weight data before relying on GA capacity checks.",
                    "IFC MODEL → Element Management",
                    missingTrustedWeight));
            }

            if (missingCog > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ElementCogReviewNeeded",
                    "Warning",
                    "Geometry",
                    $"{missingCog:N0} element(s) have missing, zero, or invalid COG evidence.",
                    "Review COG extraction/export settings before optimization because radius and boom calculations depend on lift points.",
                    "IFC MODEL → Element Management",
                    missingCog));
            }

            var selectedSummary = selected == null
                ? "No selected element."
                : $"Selected {CanonicalMarkResolver.Resolve(selected).DisplayLabel}; mark confidence {selected.AssemblyMarkConfidence}; validation {selected.ValidationStatus}.";

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = issues.Any(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase))
                    ? "Blocked"
                    : issues.Any(issue => issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                        ? "Review"
                        : "Ready",
                Summary = selectedSummary,
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Selected elements", selectedElements.Count.ToString("N0"), selected == null ? "No active selection" : selected.ElementID ?? string.Empty),
                    AiContextContributorHelpers.Metric("Identity review", lowConfidence.ToString("N0"), "Low/none confidence or no explicit mark"),
                    AiContextContributorHelpers.Metric("Weight review", missingTrustedWeight.ToString("N0"), "Not explicit trusted weight"),
                    AiContextContributorHelpers.Metric("Validation warnings", validationWarnings.ToString("N0"), $"Hidden contributors: {hiddenContributors:N0}")
                },
                Issues = issues.Take(10).ToList(),
                ActionHints = new[]
                {
                    AiContextContributorHelpers.Hint("Review selected element", "IFC MODEL", "Inspect F5 identity diagnostics and lift state evidence for the selected element."),
                    AiContextContributorHelpers.Hint("Filter unresolved identities", "IFC MODEL", "Use search/filter to focus low-confidence lift marks and missing trusted weights.")
                },
                Facts = BuildFacts(selected, lowConfidence, missingTrustedWeight, missingCog, hiddenContributors)
            };
        }

        private static IEnumerable<AiContextIssueSnapshot> BuildSelectedElementIssues(PrecastElement element)
        {
            if (IsLowConfidenceIdentity(element))
            {
                yield return AiContextContributorHelpers.Issue(
                    "SelectedElementIdentityLowConfidence",
                    "Warning",
                    "Identity",
                    $"Selected element mark '{element.AssemblyMark ?? "-"}' has confidence {element.AssemblyMarkConfidence}; explicit mark: {(element.AssemblyMarkIsExplicit ? "yes" : "no")}; source: {element.AssemblyMarkSource ?? "-"}.",
                    "Confirm the displayed lift mark against IFC source properties before reporting or grouping lifts by mark.",
                    "IFC MODEL → Element Management",
                    1);
            }

            if (element.ValidationWarnings?.Count > 0)
            {
                yield return AiContextContributorHelpers.Issue(
                    "SelectedElementValidationWarnings",
                    element.ValidationStatus == ValidationStatus.Error ? "Error" : "Warning",
                    "Validation",
                    string.Join("; ", element.ValidationWarnings.Take(3)),
                    "Review all selected-element validation warnings and correct model/export data when needed.",
                    "IFC MODEL → Element Management",
                    element.ValidationWarnings.Count);
            }

            if (element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity
                && element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty)
            {
                yield return AiContextContributorHelpers.Issue(
                    "SelectedElementWeightNotTrusted",
                    "Warning",
                    "Weight",
                    $"Selected element weight source is {element.WeightTrustLevel}; WeightTons={FormatNullable(element.WeightTons)}.",
                    "Use explicit quantity/property weight for crane capacity decisions; estimated or missing values need review.",
                    "IFC MODEL → Element Management",
                    1);
            }

            if (HasMissingCog(element))
            {
                yield return AiContextContributorHelpers.Issue(
                    "SelectedElementCogMissing",
                    "Warning",
                    "Geometry",
                    "Selected element has missing, zero, NaN, or infinite COG evidence.",
                    "Correct COG data or verify fallback geometry before optimization.",
                    "IFC MODEL → Element Management",
                    1);
            }

            if (element.HasHiddenContributors || element.HiddenWeightContributorCount > 0)
            {
                yield return AiContextContributorHelpers.Issue(
                    "SelectedAssemblyHiddenContributors",
                    "Info",
                    "Assembly",
                    $"Selected assembly includes {element.HiddenWeightContributorCount:N0} hidden weight contributor(s).",
                    "Confirm hidden contributors are intentional so lift weight matches the physical assembly.",
                    "IFC MODEL → Element Management",
                    element.HiddenWeightContributorCount);
            }
        }

        private static Dictionary<string, string> BuildFacts(
            PrecastElement? selected,
            int lowConfidence,
            int missingTrustedWeight,
            int missingCog,
            int hiddenContributors)
        {
            var facts = new Dictionary<string, string>
            {
                ["LowConfidenceIdentityCount"] = lowConfidence.ToString(),
                ["MissingTrustedWeightCount"] = missingTrustedWeight.ToString(),
                ["MissingCogCount"] = missingCog.ToString(),
                ["HiddenContributorElementCount"] = hiddenContributors.ToString()
            };

            if (selected == null)
                return facts;

            var identity = CanonicalMarkResolver.Resolve(selected);
            facts["SelectedElementId"] = selected.ElementID ?? string.Empty;
            facts["SelectedIfcName"] = selected.ElementName ?? string.Empty;
            facts["SelectedDisplayLabel"] = identity.DisplayLabel;
            facts["SelectedAssemblyMark"] = selected.AssemblyMark ?? string.Empty;
            facts["SelectedAssemblyMarkSource"] = selected.AssemblyMarkSource ?? string.Empty;
            facts["SelectedAssemblyMarkConfidence"] = selected.AssemblyMarkConfidence.ToString();
            facts["SelectedAssemblyMarkIsExplicit"] = selected.AssemblyMarkIsExplicit.ToString();
            facts["SelectedValidationStatus"] = selected.ValidationStatus.ToString();
            facts["SelectedWeightTrustLevel"] = selected.WeightTrustLevel.ToString();
            facts["SelectedRole"] = selected.Role.ToString();
            return facts;
        }

        private static bool IsLowConfidenceIdentity(PrecastElement element) =>
            !element.AssemblyMarkIsExplicit
            || element.AssemblyMarkConfidence == AssemblyMarkConfidence.Low
            || element.AssemblyMarkConfidence == AssemblyMarkConfidence.None;

        private static bool HasMissingCog(PrecastElement element)
        {
            var cog = element.ElementCOG;
            return cog == null
                || (Math.Abs(cog.X) < 0.0001 && Math.Abs(cog.Y) < 0.0001 && Math.Abs(cog.Z) < 0.0001)
                || double.IsNaN(cog.X) || double.IsInfinity(cog.X)
                || double.IsNaN(cog.Y) || double.IsInfinity(cog.Y)
                || double.IsNaN(cog.Z) || double.IsInfinity(cog.Z);
        }

        private static string FormatNullable(double? value) => value.HasValue ? value.Value.ToString("0.###") : "-";
    }
}
