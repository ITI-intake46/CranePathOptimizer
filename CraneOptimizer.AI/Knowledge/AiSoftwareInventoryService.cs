using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CraneOptimizer.AI.Knowledge
{
    /// <summary>
    /// Best-effort XAML metadata scanner. It complements the curated software map by
    /// extracting visible labels from committed XAML where source files are available.
    /// Runtime builds without source files fall back gracefully.
    /// </summary>
    public sealed class AiSoftwareInventoryService : IAiSoftwareInventoryService
    {
        private static readonly Regex LabelRegex = new("(?:Header|Content|Text)\\s*=\\s*\"([^\"]{2,80})\"", RegexOptions.Compiled);

        public string BuildXamlInventorySummary(int maxFiles = 25)
        {
            var root = FindRepositoryRoot();
            if (root == null)
                return "XAML source inventory is unavailable in this runtime layout.";

            var xamlRoots = new[]
            {
                Path.Combine(root, "CraneOptimizer.UI"),
                Path.Combine(root, "CraneOptimizer.AI")
            }
            .Where(Directory.Exists)
            .ToArray();

            if (xamlRoots.Length == 0)
                return "XAML source inventory is unavailable because neither CraneOptimizer.UI nor CraneOptimizer.AI was found.";

            var files = xamlRoots
                .SelectMany(path => Directory.EnumerateFiles(path, "*.xaml", SearchOption.AllDirectories))
                .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(maxFiles)
                .ToArray();

            if (files.Length == 0)
                return "No XAML files were found for software-context extraction.";

            var builder = new StringBuilder();
            builder.AppendLine("XAML-DERIVED UI INVENTORY");
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var labels = LabelRegex.Matches(text)
                    .Select(match => match.Groups[1].Value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("{", StringComparison.Ordinal))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(18)
                    .ToArray();

                if (labels.Length == 0)
                    continue;

                builder.AppendLine($"- {Path.GetRelativePath(root, file)}");
                builder.AppendLine($"  Labels: {string.Join(", ", labels)}");
            }

            return builder.ToString().Trim();
        }

        private static string? FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;

                current = current.Parent;
            }

            var developmentRoot = Directory.GetCurrentDirectory();
            current = new DirectoryInfo(developmentRoot);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;

                current = current.Parent;
            }

            return null;
        }
    }
}
