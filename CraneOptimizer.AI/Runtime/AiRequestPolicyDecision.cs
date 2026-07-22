namespace CraneOptimizer.AI.Runtime
{
    public sealed record AiRequestPolicyDecision(bool IsAllowed, string ErrorMessage)
    {
        public static AiRequestPolicyDecision Allow() => new(true, string.Empty);

        public static AiRequestPolicyDecision Deny(string errorMessage) =>
            new(false, string.IsNullOrWhiteSpace(errorMessage) ? "The AI request was rejected by policy." : errorMessage);
    }
}
