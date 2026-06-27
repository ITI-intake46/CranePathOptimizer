namespace CraneOptimizer.Application.AI.DTOs
{
    public sealed class AiSoftwareContextDto
    {
        public AiFrameworkInfoDto Framework { get; init; } = new();
        public IReadOnlyList<AiFeatureTabDto> Tabs { get; init; } = Array.Empty<AiFeatureTabDto>();
        public IReadOnlyList<AiWorkflowDto> Workflows { get; init; } = Array.Empty<AiWorkflowDto>();
        public IReadOnlyList<string> SafetyGuardrails { get; init; } = Array.Empty<string>();
    }

    public sealed class AiFrameworkInfoDto
    {
        public string ApplicationName { get; init; } = string.Empty;
        public string ProductPurpose { get; init; } = string.Empty;
        public string UiFramework { get; init; } = string.Empty;
        public string TargetFramework { get; init; } = string.Empty;
        public string Architecture { get; init; } = string.Empty;
        public IReadOnlyList<string> MajorTechnologies { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ArchitecturalLayers { get; init; } = Array.Empty<string>();
        public string AiIntegrationMode { get; init; } = string.Empty;
    }

    public sealed class AiFeatureTabDto
    {
        public string TabName { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string PrimaryView { get; init; } = string.Empty;
        public string PrimaryViewModel { get; init; } = string.Empty;
        public IReadOnlyList<AiFeatureActionDto> Actions { get; init; } = Array.Empty<AiFeatureActionDto>();
        public IReadOnlyList<string> TypicalQuestions { get; init; } = Array.Empty<string>();
    }

    public sealed class AiFeatureActionDto
    {
        public string Label { get; init; } = string.Empty;
        public string CommandOrHandler { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string Availability { get; init; } = string.Empty;
        public string SafetyNote { get; init; } = string.Empty;
    }

    public sealed class AiWorkflowDto
    {
        public string Name { get; init; } = string.Empty;
        public string Goal { get; init; } = string.Empty;
        public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RelatedTabs { get; init; } = Array.Empty<string>();
    }
}
