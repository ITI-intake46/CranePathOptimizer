using System.Net;

namespace CraneOptimizer.AI.Runtime
{
    /// <summary>
    /// Classifies an AI destination from the actual endpoint URI. Provider names are
    /// deliberately ignored: a remotely hosted Ollama instance is a remote provider.
    /// Only loopback destinations are considered local/trusted for privacy policy.
    /// </summary>
    public static class AiEndpointSecurityClassifier
    {
        public static bool IsLocalEndpoint(string? endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && IsLocalEndpoint(uri);
        }

        public static bool IsLocalEndpoint(Uri? endpoint)
        {
            if (endpoint == null)
                return false;

            if (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)
                return false;

            if (endpoint.IsLoopback)
                return true;

            if (IPAddress.TryParse(endpoint.Host, out var address))
                return IPAddress.IsLoopback(address);

            return string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRemoteEndpoint(string? endpoint) => !IsLocalEndpoint(endpoint);
    }
}
