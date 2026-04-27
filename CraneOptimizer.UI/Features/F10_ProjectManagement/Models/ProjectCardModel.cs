using System.IO;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Models
{
    public sealed class ProjectCardModel
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string IfcFilePath { get; init; } = string.Empty;
        public DateTime CreatedDate { get; init; }
        public DateTime LastOpenedDate { get; init; }
        public DateTime LastModifiedDate { get; init; }
        public bool IsFavorite { get; init; }
        public string Notes { get; init; } = string.Empty;

        public string IfcFileName =>
            string.IsNullOrWhiteSpace(IfcFilePath)
                ? "No IFC linked"
                : Path.GetFileName(IfcFilePath);
    }
}
