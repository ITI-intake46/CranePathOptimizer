using System.IO;
using System.Text.RegularExpressions;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    /// <summary>
    /// Lightweight local retrieval over committed project documentation. This is a
    /// deterministic supplement for prompts; it does not require a vector database,
    /// internet access, or external model tooling.
    /// </summary>
    public sealed class AiLocalDocumentSearchService : IAiLocalDocumentSearchService
    {
        private static readonly string[] SearchRoots = { "docs", ".agents", ".github" };
        private static readonly string[] Extensions = { ".md", ".mdc", ".txt" };

        public IReadOnlyList<AiDocumentSearchResult> Search(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<AiDocumentSearchResult>();

            var root = ResolveRepositoryRoot();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return Array.Empty<AiDocumentSearchResult>();

            var tokens = Tokenize(query).ToArray();
            if (tokens.Length == 0)
                return Array.Empty<AiDocumentSearchResult>();

            var results = new List<AiDocumentSearchResult>();
            foreach (var searchRoot in SearchRoots)
            {
                var directory = Path.Combine(root, searchRoot);
                if (!Directory.Exists(directory))
                    continue;

                foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                {
                    if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                        continue;

                    TryScoreFile(root, file, tokens, results);
                }
            }

            return results
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Clamp(maxResults, 1, 20))
                .ToArray();
        }

        private static void TryScoreFile(string root, string file, IReadOnlyList<string> tokens, List<AiDocumentSearchResult> results)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                return;
            }

            var score = tokens.Sum(token => CountOccurrences(text, token));
            if (score <= 0)
                return;

            results.Add(new AiDocumentSearchResult
            {
                Title = Path.GetFileName(file),
                RelativePath = Path.GetRelativePath(root, file),
                Snippet = BuildSnippet(text, tokens),
                Score = score
            });
        }

        private static IEnumerable<string> Tokenize(string query)
        {
            return Regex.Split(query.ToLowerInvariant(), "[^a-z0-9_]+")
                .Where(token => token.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static int CountOccurrences(string text, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static string BuildSnippet(string text, IReadOnlyList<string> tokens)
        {
            var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var firstMatch = tokens
                .Select(token => normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase))
                .Where(index => index >= 0)
                .DefaultIfEmpty(0)
                .Min();

            var start = Math.Max(0, firstMatch - 180);
            var length = Math.Min(520, normalized.Length - start);
            var snippet = normalized.Substring(start, length).Trim();
            snippet = Regex.Replace(snippet, "\\s+", " ");
            return snippet.Length <= 520 ? snippet : snippet[..520] + "...";
        }

        private static string ResolveRepositoryRoot()
        {
            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

            foreach (var candidate in candidates)
            {
                var root = WalkUp(candidate);
                if (!string.IsNullOrWhiteSpace(root))
                    return root;
            }

            return Directory.GetCurrentDirectory();
        }

        private static string? WalkUp(string start)
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CraneOptimizer.sln"))
                    || Directory.Exists(Path.Combine(directory.FullName, "docs")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
