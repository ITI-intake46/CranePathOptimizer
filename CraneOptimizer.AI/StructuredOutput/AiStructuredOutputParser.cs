using System.Text.Json;

namespace CraneOptimizer.AI.StructuredOutput
{
    public sealed class AiStructuredOutputParser
    {
        private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public AiStructuredOutputResult<T> Parse<T>(string? content, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                return AiStructuredOutputResult<T>.Failure("The AI response was empty.");

            if (!TryExtractJson(content, out var json))
                return AiStructuredOutputResult<T>.Failure("The AI response did not contain a complete JSON object or array.");

            try
            {
                var value = JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
                return value == null
                    ? AiStructuredOutputResult<T>.Failure("The structured AI response resolved to null.")
                    : AiStructuredOutputResult<T>.FromValue(value);
            }
            catch (JsonException ex)
            {
                return AiStructuredOutputResult<T>.Failure("The structured AI response was invalid JSON: " + ex.Message);
            }
        }

        public static bool TryExtractJson(string content, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var trimmed = content.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                    trimmed = trimmed[(firstNewLine + 1)..lastFence].Trim();
            }

            var objectStart = trimmed.IndexOf('{');
            var arrayStart = trimmed.IndexOf('[');
            var start = objectStart < 0
                ? arrayStart
                : arrayStart < 0
                    ? objectStart
                    : Math.Min(objectStart, arrayStart);

            if (start < 0)
                return false;

            var opening = trimmed[start];
            var closing = opening == '{' ? '}' : ']';
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var index = start; index < trimmed.Length; index++)
            {
                var character = trimmed[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (character == '"')
                        inString = false;

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    continue;
                }

                if (character == opening)
                    depth++;
                else if (character == closing)
                    depth--;

                if (depth == 0)
                {
                    json = trimmed[start..(index + 1)];
                    return true;
                }
            }

            return false;
        }
    }
}
