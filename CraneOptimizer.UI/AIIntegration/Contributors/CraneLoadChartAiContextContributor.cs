using CraneOptimizer.AI.Host;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Exposes crane setup and load-chart provenance/availability signals.
    /// </summary>
    public sealed class CraneLoadChartAiContextContributor : IAiScreenContextContributor
    {
        public string ScreenName => "Crane and load chart";

        public AiScreenContextSnapshot BuildSnapshot(MainViewModel shell)
        {
            var cranes = shell.Scene.GetCranes();
            var selected = cranes.FirstOrDefault(crane => crane.IsSelected) ?? cranes.FirstOrDefault();
            var hasLoadChartEvidence = cranes.Any(crane => crane.ActiveSpecification != null || crane.CurrentCapacity.HasValue || crane.CurrentRadius.HasValue)
                || (shell.CranePlacement.Result is { MaxCapacity: > 0, MaxRadius: > 0 });
            var issues = new List<AiContextIssueSnapshot>();

            if (cranes.Count == 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "CraneMissing",
                    "Warning",
                    "Crane",
                    "No crane instance exists in the scene.",
                    "Add/select a crane before running lift reachability and capacity checks.",
                    "CRANES → MOBILE CRANE"));
            }

            if (!hasLoadChartEvidence)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "LoadChartEvidenceMissing",
                    "Warning",
                    "Load chart",
                    "No active load-chart/capacity/radius evidence was detected in the crane or latest result context.",
                    "Import or verify manufacturer load chart data before relying on capacity feasibility.",
                    "CRANES → Import Load Chart"));
            }

            var result = shell.CranePlacement.Result;
            var notCharted = result?.Statistics?.NotCharted ?? 0;
            if (notCharted > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "LoadChartNotChartedFromResult",
                    "Warning",
                    "Load chart",
                    $"Latest GA result has {notCharted:N0} not-charted element(s).",
                    "Review chart radius/boom coverage and imported cells for the selected crane specification.",
                    "CRANES → Import Load Chart",
                    notCharted));
            }

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = cranes.Count == 0 ? "Missing crane" : hasLoadChartEvidence ? "Evidence available" : "Needs load chart",
                Summary = selected == null
                    ? "No selected crane."
                    : $"Selected crane context: {selected.CraneDefinition?.Name ?? "Crane"}; radius {Format(selected.CurrentRadius)}; capacity {Format(selected.CurrentCapacity)}; specification {(selected.ActiveSpecification == null ? "not attached" : "attached")}.",
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Cranes", cranes.Count.ToString("N0"), selected == null ? "None selected" : selected.CraneDefinition?.Name ?? "Selected crane"),
                    AiContextContributorHelpers.Metric("Load chart evidence", hasLoadChartEvidence ? "Available" : "Missing", "From active specification/current capacity/latest result"),
                    AiContextContributorHelpers.Metric("Current radius", Format(selected?.CurrentRadius), "Scene crane display value"),
                    AiContextContributorHelpers.Metric("Current capacity", Format(selected?.CurrentCapacity), "Scene crane display value")
                },
                Issues = issues,
                ActionHints = new[]
                {
                    AiContextContributorHelpers.Hint("Open cranes", "CRANES", "Review crane model, specification, load chart, and active radius/capacity."),
                    AiContextContributorHelpers.Hint("Open analyze", "ANALYZE", "Use GA result failures to distinguish crane reach from load-chart coverage.")
                },
                Facts = new Dictionary<string, string>
                {
                    ["CraneCount"] = cranes.Count.ToString(),
                    ["HasLoadChartEvidence"] = hasLoadChartEvidence.ToString(),
                    ["SelectedCraneName"] = selected?.CraneDefinition?.Name ?? string.Empty,
                    ["SelectedHasSpecification"] = (selected?.ActiveSpecification != null).ToString(),
                    ["SelectedCurrentRadius"] = selected?.CurrentRadius?.ToString("0.###") ?? string.Empty,
                    ["SelectedCurrentCapacity"] = selected?.CurrentCapacity?.ToString("0.###") ?? string.Empty
                }
            };
        }

        private static string Format(double? value) => value.HasValue ? value.Value.ToString("0.###") : "-";
    }
}
