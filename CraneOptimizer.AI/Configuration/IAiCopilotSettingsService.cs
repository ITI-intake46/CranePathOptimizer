namespace CraneOptimizer.AI.Configuration
{
    public interface IAiCopilotSettingsService
    {
        AiCopilotSettings Current { get; }
        AiCopilotSettings Load();
        void Save();
        void Update(Action<AiCopilotSettings> updateAction);
    }
}
