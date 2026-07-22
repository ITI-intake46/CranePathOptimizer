using System.Text.RegularExpressions;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// Dependency-free hybrid retriever. It combines weighted lexical matching with
    /// deterministic hashed semantic vectors and source-authority boosting. The
    /// implementation is local, reproducible, and safe when Ollama is unavailable.
    /// </summary>
    public sealed class AiHybridKnowledgeRetriever : IAiHybridKnowledgeRetriever
    {
        private readonly IAiTextEmbeddingService _embeddingService;
        private readonly object _chunkCacheSync = new();
        private string _cachedCorpusKey = string.Empty;
        private IReadOnlyList<AiKnowledgeChunk> _cachedChunks = Array.Empty<AiKnowledgeChunk>();

        public AiHybridKnowledgeRetriever(IAiTextEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        }

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "that", "this", "with", "from", "what", "which", "where", "when", "why",
            "how", "are", "was", "were", "have", "has", "had", "into", "about", "should", "would", "could",
            "there", "their", "then", "than", "your", "you", "our", "can", "not", "but", "all", "any",
            "is", "do", "to", "of", "it", "we", "us"
        };

        private static readonly IReadOnlyDictionary<string, string[]> Expansions =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["optimization"] = new[] { "optimizer", "ga", "genetic", "run" },
                ["optimizer"] = new[] { "optimization", "ga", "genetic", "run" },
                ["ga"] = new[] { "optimizer", "optimization", "candidate", "lift" },
                ["blocked"] = new[] { "blocker", "cannot", "missing", "zero", "error", "not ready" },
                ["blocker"] = new[] { "blocked", "cannot", "missing", "zero", "error" },
                ["next"] = new[] { "recommendation", "action", "click", "fix", "review" },
                ["crane"] = new[] { "mobile", "load chart", "capacity", "radius" },
                ["weight"] = new[] { "mass", "trusted", "estimated", "fallback", "quantity" },
                ["bim"] = new[] { "ifc", "model", "element", "assembly" },
                ["ifc"] = new[] { "bim", "model", "element", "assembly" },
                ["report"] = new[] { "evidence", "qa", "narrative", "export" },
                ["zone"] = new[] { "allowed", "forbidden", "green", "area" }
            };

        public IReadOnlyList<AiKnowledgeCitation> Retrieve(
            IReadOnlyList<AiKnowledgeDocument> documents,
            string query,
            int maxResults = 6)
        {
            if (documents == null || documents.Count == 0 || string.IsNullOrWhiteSpace(query))
                return Array.Empty<AiKnowledgeCitation>();

            var chunks = GetOrCreateChunks(documents);
            if (chunks.Count == 0)
                return Array.Empty<AiKnowledgeCitation>();

            var queryTerms = ExpandTerms(Tokenize(query)).ToArray();
            if (queryTerms.Length == 0)
                return Array.Empty<AiKnowledgeCitation>();

            var queryVector = _embeddingService.Embed(string.Join(" ", queryTerms));
            var scored = new List<ScoredChunk>(chunks.Count);
            foreach (var chunk in chunks)
            {
                var searchableTitle = chunk.Document.Title + " " + chunk.Document.SourceType + " " + chunk.Document.SourceLocator;
                var titleTerms = Tokenize(searchableTitle).ToArray();
                var bodyTerms = Tokenize(chunk.Text).ToArray();
                var lexical = ComputeLexicalScore(queryTerms, titleTerms, bodyTerms, query, chunk);
                var semanticTerms = ExpandTerms(bodyTerms.Concat(titleTerms));
                var semantic = Cosine(queryVector, _embeddingService.Embed(string.Join(" ", semanticTerms)));
                var authority = SourceAuthority(chunk.Document.SourceType);
                var score = Clamp01((0.58 * lexical) + (0.32 * semantic) + (0.10 * authority));

                if (score >= 0.15)
                    scored.Add(new ScoredChunk(chunk, score, lexical, semantic));
            }

            var bestPerDocument = scored
                .OrderByDescending(item => item.Score)
                .GroupBy(item => item.Chunk.Document.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Chunk.Document.Title, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Clamp(maxResults, 1, 12))
                .ToArray();

            return bestPerDocument
                .Select((item, index) => new AiKnowledgeCitation
                {
                    Id = $"E{index + 1}",
                    SourceId = item.Chunk.Document.Id,
                    SourceLabel = item.Chunk.Document.Title,
                    SourceType = item.Chunk.Document.SourceType,
                    SourceLocator = item.Chunk.Document.SourceLocator,
                    Snippet = BuildSnippet(item.Chunk.Text, queryTerms),
                    Score = item.Score,
                    Confidence = Confidence(item.Score),
                    TrustLevel = item.Chunk.Document.TrustLevel,
                    ModelRevision = item.Chunk.Document.ModelRevision
                })
                .ToArray();
        }

        private IReadOnlyList<AiKnowledgeChunk> GetOrCreateChunks(IReadOnlyList<AiKnowledgeDocument> documents)
        {
            var corpusKey = string.Join("|", documents.Select(document => document.Id + ":" + document.ModelRevision));
            lock (_chunkCacheSync)
            {
                if (string.Equals(_cachedCorpusKey, corpusKey, StringComparison.Ordinal))
                    return _cachedChunks;

                _cachedChunks = AiKnowledgeTextChunker.Chunk(documents);
                _cachedCorpusKey = corpusKey;
                return _cachedChunks;
            }
        }

        private static double ComputeLexicalScore(
            IReadOnlyList<string> queryTerms,
            IReadOnlyList<string> titleTerms,
            IReadOnlyList<string> bodyTerms,
            string rawQuery,
            AiKnowledgeChunk chunk)
        {
            if (bodyTerms.Count == 0 && titleTerms.Count == 0)
                return 0;

            var titleCounts = titleTerms.GroupBy(term => term, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            var bodyCounts = bodyTerms.GroupBy(term => term, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var rawScore = 0d;
            var matched = 0;
            foreach (var term in queryTerms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                titleCounts.TryGetValue(term, out var titleFrequency);
                bodyCounts.TryGetValue(term, out var bodyFrequency);
                if (titleFrequency + bodyFrequency <= 0)
                    continue;

                matched++;
                rawScore += (2.4 * Math.Log(1 + titleFrequency)) + Math.Log(1 + bodyFrequency);
            }

            var coverage = matched / (double)Math.Max(1, queryTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            var phrase = NormalizePhrase(rawQuery);
            if (phrase.Length >= 8 && chunk.Text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                rawScore += 2.5;

            if (chunk.Document.SourceType.Equals("WorkflowIssue", StringComparison.OrdinalIgnoreCase)
                && queryTerms.Any(term => term is "blocked" or "blocker" or "error" or "missing" or "why"))
            {
                rawScore += 1.4;
            }

            var saturation = 1d - Math.Exp(-rawScore / 5.5d);
            return Clamp01((0.72 * saturation) + (0.28 * coverage));
        }

        private static IEnumerable<string> ExpandTerms(IEnumerable<string> terms)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                if (seen.Add(term))
                    yield return term;

                if (!Expansions.TryGetValue(term, out var expansions))
                    continue;

                foreach (var expansion in expansions.SelectMany(Tokenize))
                {
                    if (seen.Add(expansion))
                        yield return expansion;
                }
            }
        }

        private static IEnumerable<string> Tokenize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            foreach (Match match in Regex.Matches(value.ToLowerInvariant(), "[a-z0-9][a-z0-9_-]*"))
            {
                var term = match.Value.Trim('-', '_');
                if (term.Length < 2 || StopWords.Contains(term))
                    continue;
                yield return term;
            }
        }

        private static double Cosine(IReadOnlyList<double> left, IReadOnlyList<double> right)
        {
            var length = Math.Min(left.Count, right.Count);
            var dot = 0d;
            for (var index = 0; index < length; index++)
                dot += left[index] * right[index];
            return Math.Max(0d, dot);
        }

        private static double SourceAuthority(string sourceType)
        {
            if (sourceType.Equals("WorkflowIssue", StringComparison.OrdinalIgnoreCase)) return 1.0;
            if (sourceType.Equals("ActionHint", StringComparison.OrdinalIgnoreCase)) return 0.96;
            if (sourceType.Equals("OptimizationSnapshot", StringComparison.OrdinalIgnoreCase)) return 0.94;
            if (sourceType.Equals("ScreenSnapshot", StringComparison.OrdinalIgnoreCase)) return 0.91;
            return 0.88;
        }

        private static string BuildSnippet(string text, IReadOnlyList<string> queryTerms)
        {
            var first = queryTerms
                .Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase))
                .Where(index => index >= 0)
                .DefaultIfEmpty(0)
                .Min();
            var start = Math.Max(0, first - 120);
            var length = Math.Min(520, text.Length - start);
            var snippet = Regex.Replace(text.Substring(start, length), "\\s+", " ").Trim();
            return snippet.Length < text.Length - start ? snippet + "..." : snippet;
        }

        private static string Confidence(double score)
        {
            if (score >= 0.72) return "High";
            if (score >= 0.46) return "Medium";
            return "Low";
        }

        private static string NormalizePhrase(string value) => Regex.Replace(value.Trim(), "\\s+", " ");
        private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));

        private sealed record ScoredChunk(AiKnowledgeChunk Chunk, double Score, double Lexical, double Semantic);
    }
}
