namespace CraneOptimizer.Domain.Units
{
    public interface IUnitSettingsService
    {
        UnitSettings Current { get; }
        event EventHandler<UnitSettings>? SettingsChanged;

        Task LoadProjectSettingsAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task SaveProjectSettingsAsync(Guid projectId, UnitSettings settings, CancellationToken cancellationToken = default);
        void ApplyRuntimeSettings(UnitSettings settings);
    }
}
