namespace CraneOptimizer.AI.ViewModels
{
    public sealed class AiResponseHistoryItemViewModel
    {
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
        public string ProviderId { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string Question { get; init; } = string.Empty;
        public string Answer { get; init; } = string.Empty;
        public string Source { get; init; } = "AI";

        public string DisplayTitle
        {
            get
            {
                var model = string.IsNullOrWhiteSpace(Model) ? "model unknown" : Model;
                var source = string.IsNullOrWhiteSpace(Source) ? "AI" : Source;
                return $"{CreatedAt:MMM d, HH:mm} · {source} · {model}";
            }
        }

        public string Preview
        {
            get
            {
                var question = CleanQuestion(Question);
                return question.Length <= 120 ? question : $"{question[..120]}...";
            }
        }

        private static string CleanQuestion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Untitled question";

            var text = value.Trim();
            const string contextMarker = "Use this live UI context when answering:";
            var markerIndex = text.IndexOf(contextMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0)
                text = text[..markerIndex].Trim();
            else if (markerIndex == 0)
                text = ExtractHumanQuestionAfterContext(text);

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(text) ? "Untitled question" : text;
        }

        private static string ExtractHumanQuestionAfterContext(string text)
        {
            var lines = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            foreach (var line in lines)
            {
                if (line.Contains("Use this live UI context", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Project:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Active screen:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Screen:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Elements:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Optimization", StringComparison.OrdinalIgnoreCase))
                    continue;

                return line.Trim();
            }

            return "Screen-aware question";
        }
    }
}
