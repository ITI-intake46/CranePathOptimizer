namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Configuration
{
    public interface IAiCopilotSettingsService
    {
        AiCopilotSettings Current { get; }
        AiCopilotSettings Load();
        void Save();
        void Update(Action<AiCopilotSettings> updateAction);
    }
}
