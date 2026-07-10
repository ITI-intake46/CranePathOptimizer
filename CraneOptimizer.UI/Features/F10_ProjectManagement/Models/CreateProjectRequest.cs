using CraneOptimizer.Domain.Units;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Models
{
    public sealed class CreateProjectRequest
    {
        public string ProjectName { get; init; } = string.Empty;

        public string ParentFolderPath { get; init; } = string.Empty;

        public UnitSettings UnitSettings { get; init; } = UnitSettings.Default.Clone();

        public string? Country { get; init; }

        public string? SiteLocation { get; init; }

        public string? CompanyName { get; init; }

        public string? ProjectImagePath { get; init; }
    }
}
