using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class UnitSettingsService : IUnitSettingsService
    {
        private readonly IDbContextFactory<CraneDbContext> _dbFactory;
        private UnitSettings _current = UnitSettings.Default.Clone();

        public UnitSettingsService(IDbContextFactory<CraneDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public UnitSettings Current => _current.Clone();

        public event EventHandler<UnitSettings>? SettingsChanged;

        public async Task LoadProjectSettingsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

            ApplyRuntimeSettings(project == null
                ? UnitSettings.Default
                : new UnitSettings
                {
                    DisplayLengthUnit = (LengthUnit)project.DisplayLengthUnit,
                    DisplayMassUnit = (MassUnit)project.DisplayMassUnit,
                    DisplayAreaUnit = (AreaUnit)project.DisplayAreaUnit,
                    DecimalPlaces = project.UnitDecimalPlaces
                });
        }

        public async Task SaveProjectSettingsAsync(
            Guid projectId,
            UnitSettings settings,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
            if (project == null)
            {
                ApplyRuntimeSettings(settings);
                return;
            }

            project.DisplayLengthUnit = (int)settings.DisplayLengthUnit;
            project.DisplayMassUnit = (int)settings.DisplayMassUnit;
            project.DisplayAreaUnit = (int)settings.DisplayAreaUnit;
            project.UnitDecimalPlaces = Math.Clamp(settings.DecimalPlaces, 0, 6);
            project.LastModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            ApplyRuntimeSettings(settings);
        }

        public void ApplyRuntimeSettings(UnitSettings settings)
        {
            _current = (settings ?? UnitSettings.Default).Clone();
            _current.DecimalPlaces = Math.Clamp(_current.DecimalPlaces, 0, 6);
            SettingsChanged?.Invoke(this, _current.Clone());
        }
    }
}
