using CraneOptimizer.UI.Features.F11_DXF.DTOs;

namespace CraneOptimizer.UI.Features.F11_DXF.Services
{
    /// <summary>
    /// Persistence contract for DXF overlay data.
    /// </summary>
    public interface IDxfRepository
    {
        /// <summary>Persist a DXF file path and its alignment transform for a project.</summary>
        Task SaveAsync(int projectId, string filePath, DxfTransformDto transform);

        /// <summary>Load the last saved transform for a project, or null if none.</summary>
        Task<(string? FilePath, DxfTransformDto? Transform)> LoadAsync(int projectId);

        /// <summary>Remove all DXF records for a project.</summary>
        Task DeleteAsync(int projectId);
    }
}
