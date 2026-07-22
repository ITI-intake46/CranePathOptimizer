using System.Text.RegularExpressions;

namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// Offline-safe semantic baseline using signed feature hashing over unigrams and
    /// adjacent bigrams. It is deterministic across machines and requires no model.
    /// </summary>
    public sealed class AiDeterministicHashEmbeddingService : IAiTextEmbeddingService
    {
        public const int DefaultDimensions = 384;

        public string ProviderId => "deterministic-hash-v1";
        public int Dimensions => DefaultDimensions;

        public IReadOnlyList<double> Embed(string text)
        {
            var vector = new double[Dimensions];
            if (string.IsNullOrWhiteSpace(text))
                return vector;

            var tokens = Regex.Matches(text.ToLowerInvariant(), "[a-z0-9][a-z0-9_-]*")
                .Cast<Match>()
                .Select(match => match.Value.Trim('-', '_'))
                .Where(token => token.Length >= 2)
                .ToArray();

            for (var index = 0; index < tokens.Length; index++)
            {
                AddFeature(vector, tokens[index], 1d);
                if (index + 1 < tokens.Length)
                    AddFeature(vector, tokens[index] + "::" + tokens[index + 1], 0.55d);
            }

            var norm = Math.Sqrt(vector.Sum(value => value * value));
            if (norm <= 0)
                return vector;

            for (var index = 0; index < vector.Length; index++)
                vector[index] /= norm;
            return vector;
        }

        private static void AddFeature(double[] vector, string feature, double weight)
        {
            var hash = Fnv1a(feature);
            var bucket = (int)(hash % (uint)vector.Length);
            var sign = (hash & 0x80000000u) == 0 ? 1d : -1d;
            vector[bucket] += weight * sign;
        }

        private static uint Fnv1a(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var ch in value)
            {
                hash ^= (uint)char.ToLowerInvariant(ch);
                hash = unchecked(hash * prime);
            }
            return hash;
        }
    }
}
