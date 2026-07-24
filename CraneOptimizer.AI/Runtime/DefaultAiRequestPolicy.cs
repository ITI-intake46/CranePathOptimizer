using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Runtime
{
    public sealed class DefaultAiRequestPolicy : IAiRequestPolicy
    {
        private readonly IAiCopilotSettingsService _settingsService;

        public DefaultAiRequestPolicy(IAiCopilotSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public AiRequestPolicyDecision Evaluate(AiChatRequestDto request)
        {
            if (request == null)
                return AiRequestPolicyDecision.Deny("The AI request is missing.");

            if (string.IsNullOrWhiteSpace(request.Model))
                return AiRequestPolicyDecision.Deny("Select an AI model before sending the request.");

            if (string.IsNullOrWhiteSpace(request.UserPrompt))
                return AiRequestPolicyDecision.Deny("Enter a question or instruction before sending the request.");

            var settings = _settingsService.Current;
            if (request.SystemPrompt.Length > settings.MaxSystemPromptCharacters)
            {
                return AiRequestPolicyDecision.Deny(
                    $"The system prompt exceeds the configured {settings.MaxSystemPromptCharacters:N0}-character limit.");
            }

            if (request.UserPrompt.Length > settings.MaxUserPromptCharacters)
            {
                return AiRequestPolicyDecision.Deny(
                    $"The user prompt exceeds the configured {settings.MaxUserPromptCharacters:N0}-character limit.");
            }

            if (request.ContextJson.Length > settings.MaxContextCharacters)
            {
                return AiRequestPolicyDecision.Deny(
                    $"The grounded project context exceeds the configured {settings.MaxContextCharacters:N0}-character limit. Reduce or summarize the context before retrying.");
            }

            if (request.TimeoutSeconds is <= 0)
                return AiRequestPolicyDecision.Deny("The AI request timeout must be greater than zero seconds.");

            var providerId = string.IsNullOrWhiteSpace(request.ProviderId)
                ? settings.DefaultProviderId
                : request.ProviderId.Trim();
            var profile = settings.ProviderProfiles.FirstOrDefault(item =>
                string.Equals(item.Id, providerId, StringComparison.OrdinalIgnoreCase));
            if (profile is { IsLocal: false })
            {
                var containsProjectContext = !request.Metadata.TryGetValue("ContainsProjectContext", out var value)
                    ? !string.IsNullOrWhiteSpace(request.ContextJson)
                    : !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                if (containsProjectContext
                    && !settings.CloudConsentAcceptedProviderIds.Contains(profile.Id))
                {
                    return AiRequestPolicyDecision.Deny(
                        $"Cloud privacy consent is required before project context can be sent to {profile.DisplayName}.");
                }
            }

            return AiRequestPolicyDecision.Allow();
        }
    }
}
