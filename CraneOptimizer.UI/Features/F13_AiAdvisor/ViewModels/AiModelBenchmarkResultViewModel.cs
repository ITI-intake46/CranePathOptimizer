namespace CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels
{
    public sealed class AiModelBenchmarkResultViewModel
    {
        public string Model { get; init; } = string.Empty;
        public bool Success { get; init; }
        public long ElapsedMilliseconds { get; init; }
        public int ResponseCharacters { get; init; }
        public string Notes { get; init; } = string.Empty;
        public string DisplayStatus => Success
            ? $"{ElapsedMilliseconds:N0} ms · {ResponseCharacters:N0} chars"
            : Notes;
    }
}
