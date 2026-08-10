using System.Text.RegularExpressions;

namespace CraneOptimizer.AI.Security
{
    public sealed partial class AiSensitiveDataRedactor : IAiSensitiveDataRedactor
    {
        private const string RedactedValue = "[REDACTED]";

        public string Redact(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            var redacted = BearerTokenRegex().Replace(value, "Bearer " + RedactedValue);
            redacted = SecretAssignmentRegex().Replace(redacted, match => match.Groups[1].Value + RedactedValue);
            redacted = OpenAiStyleKeyRegex().Replace(redacted, RedactedValue);
            return redacted;
        }

        [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]{12,}", RegexOptions.CultureInvariant)]
        private static partial Regex BearerTokenRegex();

        [GeneratedRegex("(?i)(\\b(?:api[_-]?key|token|secret|password)\\b\\s*[:=]\\s*)[^\\s,;\\\"']{6,}", RegexOptions.CultureInvariant)]
        private static partial Regex SecretAssignmentRegex();

        [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{16,}\b", RegexOptions.CultureInvariant)]
        private static partial Regex OpenAiStyleKeyRegex();
    }
}
