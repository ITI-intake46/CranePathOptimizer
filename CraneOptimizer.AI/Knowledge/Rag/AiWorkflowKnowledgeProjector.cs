using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// Converts a live workflow snapshot into stable, citation-addressable AI documents.
    /// This projection is deterministic and contains no model-generated facts.
    /// </summary>
    public sealed class AiWorkflowKnowledgeProjector : IAiWorkflowKnowledgeProjector
    {
        public IReadOnlyList<AiKnowledgeDocument> Project(AiWorkflowContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var documents = new List<AiKnowledgeDocument>
            {
                BuildProjectOverview(snapshot),
                BuildOptimizationDocument(snapshot)
            };

            foreach (var screen in snapshot.ScreenContexts)
            {
                documents.Add(BuildScreenDocument(snapshot, screen));

                foreach (var issue in screen.Issues)
                    documents.Add(BuildIssueDocument(snapshot, screen, issue));

                foreach (var action in screen.ActionHints)
                    documents.Add(BuildActionDocument(snapshot, screen, action));
            }

            return documents
                .Where(document => !string.IsNullOrWhiteSpace(document.Content))
                .GroupBy(document => document.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static AiKnowledgeDocument BuildProjectOverview(AiWorkflowContextSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Project: {snapshot.ProjectDisplayName}");
            builder.AppendLine($"Project key: {snapshot.ProjectKey}");
            builder.AppendLine($"Has project: {snapshot.HasProject}");
            builder.AppendLine($"Model: {snapshot.ModelDisplayName}");
            builder.AppendLine($"Model revision: {snapshot.ModelRevision}");
            builder.AppendLine($"Model fingerprint: {snapshot.ModelFingerprint}");
            builder.AppendLine($"BIM/model objects: {snapshot.ElementCount:N0}");
            builder.AppendLine($"Element table: {snapshot.ElementTableVisibleCount:N0} visible of {snapshot.ElementTableTotalCount:N0}");
            builder.AppendLine($"Zones: {snapshot.ZoneCount:N0}; allowed {snapshot.AllowedZoneCount:N0}; forbidden {snapshot.ForbiddenZoneCount:N0}");
            builder.AppendLine($"Zone areas: allowed {Format(snapshot.AllowedZoneArea)}; forbidden {Format(snapshot.ForbiddenZoneArea)}");
            builder.AppendLine($"Cranes: {snapshot.CraneCount:N0}");
            builder.AppendLine($"Load-chart evidence sources: {snapshot.LoadChartEvidenceCount:N0}; available {snapshot.HasLoadChartEvidence}");
            builder.AppendLine($"Building area: {Format(snapshot.BuildingArea)} {snapshot.BuildingAreaUnit}");
            builder.AppendLine($"Building height: {Format(snapshot.BuildingHeight)} {snapshot.BuildingLengthUnit}");

            return Create(
                snapshot,
                "project-overview",
                "Live project overview",
                "ProjectSnapshot",
                "project/overview",
                builder.ToString(),
                new Dictionary<string, string>
                {
                    ["projectKey"] = snapshot.ProjectKey,
                    ["modelFingerprint"] = snapshot.ModelFingerprint,
                    ["elementCount"] = snapshot.ElementCount.ToString(CultureInfo.InvariantCulture),
                    ["craneCount"] = snapshot.CraneCount.ToString(CultureInfo.InvariantCulture)
                });
        }

        private static AiKnowledgeDocument BuildOptimizationDocument(AiWorkflowContextSnapshot snapshot)
        {
            var result = snapshot.OptimizationResult;
            var builder = new StringBuilder();
            builder.AppendLine($"Completed optimization result: {result.HasResult}");
            if (result.HasResult)
            {
                builder.AppendLine($"Coverage ratio: {result.CoverageRatio:P2}");
                builder.AppendLine($"Lifted elements: {result.LiftedElementCount:N0} of {result.TotalElementCount:N0}");
                builder.AppendLine($"Crane positions: {result.PositionCount:N0}");
                builder.AppendLine($"Maximum capacity: {Format(result.MaxCapacity)}");
                builder.AppendLine($"Maximum radius: {Format(result.MaxRadius)}");
                builder.AppendLine($"Diagnostics path: {result.DiagnosticsPath ?? "not exposed"}");
            }
            else
            {
                builder.AppendLine("No completed GA result is available in the captured workflow snapshot.");
            }

            return Create(
                snapshot,
                "optimization-result",
                "Optimization result snapshot",
                "OptimizationSnapshot",
                "optimization/latest",
                builder.ToString(),
                new Dictionary<string, string>
                {
                    ["hasResult"] = result.HasResult.ToString(),
                    ["coverage"] = result.CoverageRatio.ToString(CultureInfo.InvariantCulture)
                });
        }

        private static AiKnowledgeDocument BuildScreenDocument(AiWorkflowContextSnapshot snapshot, AiScreenContextSnapshot screen)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Screen: {screen.ScreenName}");
            builder.AppendLine($"Stage: {screen.Stage}");
            builder.AppendLine($"Summary: {screen.Summary}");

            if (screen.Metrics.Count > 0)
            {
                builder.AppendLine("Metrics:");
                foreach (var metric in screen.Metrics)
                    builder.AppendLine($"- {metric.Name}: {metric.Value}{(string.IsNullOrWhiteSpace(metric.Detail) ? string.Empty : " — " + metric.Detail)}");
            }

            if (screen.Facts.Count > 0)
            {
                builder.AppendLine("Facts:");
                foreach (var fact in screen.Facts.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                    builder.AppendLine($"- {fact.Key}: {fact.Value}");
            }

            if (screen.Issues.Count > 0)
                builder.AppendLine($"Issues exposed by this screen: {screen.Issues.Count:N0}");
            if (screen.ActionHints.Count > 0)
                builder.AppendLine($"Action hints exposed by this screen: {screen.ActionHints.Count:N0}");

            var slug = Slug(screen.ScreenName);
            return Create(
                snapshot,
                $"screen-{slug}",
                $"{screen.ScreenName} evidence",
                "ScreenSnapshot",
                $"screen/{slug}",
                builder.ToString(),
                new Dictionary<string, string>
                {
                    ["screen"] = screen.ScreenName,
                    ["stage"] = screen.Stage,
                    ["issueCount"] = screen.Issues.Count.ToString(CultureInfo.InvariantCulture)
                });
        }

        private static AiKnowledgeDocument BuildIssueDocument(
            AiWorkflowContextSnapshot snapshot,
            AiScreenContextSnapshot screen,
            AiContextIssueSnapshot issue)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Issue code: {issue.Code}");
            builder.AppendLine($"Severity: {issue.Severity}");
            builder.AppendLine($"Area: {issue.Area}");
            builder.AppendLine($"Screen: {screen.ScreenName}");
            builder.AppendLine($"Stage: {screen.Stage}");
            builder.AppendLine($"Evidence: {issue.Evidence}");
            builder.AppendLine($"Recommendation: {issue.Recommendation}");
            builder.AppendLine($"Next click: {issue.NextClick}");
            builder.AppendLine($"Affected count: {issue.AffectedCount:N0}");
            builder.AppendLine($"Source confidence: {issue.Confidence}");

            var screenSlug = Slug(screen.ScreenName);
            var codeSlug = Slug(string.IsNullOrWhiteSpace(issue.Code) ? "unnamed-issue" : issue.Code);
            return Create(
                snapshot,
                $"issue-{screenSlug}-{codeSlug}",
                $"{issue.Code} · {screen.ScreenName}",
                "WorkflowIssue",
                $"screen/{screenSlug}/issue/{codeSlug}",
                builder.ToString(),
                new Dictionary<string, string>
                {
                    ["screen"] = screen.ScreenName,
                    ["issueCode"] = issue.Code,
                    ["severity"] = issue.Severity,
                    ["area"] = issue.Area,
                    ["nextClick"] = issue.NextClick,
                    ["confidence"] = issue.Confidence
                });
        }

        private static AiKnowledgeDocument BuildActionDocument(
            AiWorkflowContextSnapshot snapshot,
            AiScreenContextSnapshot screen,
            AiContextActionHintSnapshot action)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Action: {action.Label}");
            builder.AppendLine($"Action type: {action.ActionType}");
            builder.AppendLine($"Target: {action.Target}");
            builder.AppendLine($"Reason: {action.Reason}");
            builder.AppendLine($"Risk: {action.Risk}");
            builder.AppendLine($"Source screen: {screen.ScreenName}");

            var screenSlug = Slug(screen.ScreenName);
            var actionSlug = Slug(string.IsNullOrWhiteSpace(action.Label) ? action.Target : action.Label);
            return Create(
                snapshot,
                $"action-{screenSlug}-{actionSlug}",
                $"Action · {action.Label}",
                "ActionHint",
                $"screen/{screenSlug}/action/{actionSlug}",
                builder.ToString(),
                new Dictionary<string, string>
                {
                    ["screen"] = screen.ScreenName,
                    ["actionType"] = action.ActionType,
                    ["target"] = action.Target,
                    ["risk"] = action.Risk
                });
        }

        private static AiKnowledgeDocument Create(
            AiWorkflowContextSnapshot snapshot,
            string localId,
            string title,
            string sourceType,
            string sourceLocator,
            string content,
            IReadOnlyDictionary<string, string> metadata)
        {
            return new AiKnowledgeDocument
            {
                Id = $"{Slug(snapshot.ProjectKey)}:{snapshot.ModelRevision}:{localId}",
                Title = title,
                SourceType = sourceType,
                SourceLocator = sourceLocator,
                Content = content.Trim(),
                TrustLevel = "Deterministic live snapshot",
                ModelRevision = snapshot.ModelRevision,
                Metadata = metadata
            };
        }

        private static string Slug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "unknown" : slug;
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
