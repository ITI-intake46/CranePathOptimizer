namespace CraneOptimizer.AI.StructuredOutput
{
    public sealed record AiStructuredOutputResult<T>(bool Success, T? Value, string ErrorMessage)
    {
        public static AiStructuredOutputResult<T> FromValue(T value) => new(true, value, string.Empty);

        public static AiStructuredOutputResult<T> Failure(string errorMessage) =>
            new(false, default, string.IsNullOrWhiteSpace(errorMessage) ? "The AI response did not contain valid structured output." : errorMessage);
    }
}
