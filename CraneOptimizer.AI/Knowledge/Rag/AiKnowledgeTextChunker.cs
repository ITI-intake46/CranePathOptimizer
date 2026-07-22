using System.Text.RegularExpressions;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    internal static class AiKnowledgeTextChunker
    {
        private const int TargetCharacters = 760;
        private const int MaximumCharacters = 980;
        private const int OverlapCharacters = 120;

        public static IReadOnlyList<AiKnowledgeChunk> Chunk(IReadOnlyList<AiKnowledgeDocument> documents)
        {
            var chunks = new List<AiKnowledgeChunk>();
            foreach (var document in documents)
            {
                var text = Normalize(document.Content);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var ordinal = 0;
                var start = 0;
                while (start < text.Length)
                {
                    var remaining = text.Length - start;
                    var length = Math.Min(MaximumCharacters, remaining);
                    if (length > TargetCharacters && start + length < text.Length)
                    {
                        var boundary = FindBoundary(text, start + TargetCharacters, start + length);
                        length = Math.Max(1, boundary - start);
                    }

                    var chunkText = text.Substring(start, length).Trim();
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        chunks.Add(new AiKnowledgeChunk
                        {
                            Id = $"{document.Id}#chunk-{ordinal + 1}",
                            Document = document,
                            Ordinal = ordinal,
                            Text = chunkText
                        });
                        ordinal++;
                    }

                    if (start + length >= text.Length)
                        break;

                    start = Math.Max(start + 1, start + length - OverlapCharacters);
                }
            }

            return chunks;
        }

        private static int FindBoundary(string text, int preferred, int maximum)
        {
            for (var index = maximum; index > preferred; index--)
            {
                var ch = text[index - 1];
                if (ch is '\n' or '.' or ';')
                    return index;
            }

            return maximum;
        }

        private static string Normalize(string value)
        {
            var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            normalized = Regex.Replace(normalized, "[ \\t]+", " ");
            normalized = Regex.Replace(normalized, "\\n{3,}", "\n\n");
            return normalized.Trim();
        }
    }
}
