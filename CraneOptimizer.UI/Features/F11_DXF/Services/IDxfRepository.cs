using CraneOptimizer.UI.Features.F11_DXF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F11_DXF.Services
{
    /// <summary>
 /// Persistence contract for DXF overlay data.
 /// Implemented in the Infrastructure layer — never reference EF Core here.
 /// </summary>
    public interface IDxfRepository
    {
        /// <summary>Persist a DXF file path and its alignment transform for a project.</summary>
        Task SaveAsync(int projectId, string filePath, DxfTransformModel transform);

        /// <summary>Load the last saved transform for a project, or null if none.</summary>
        Task<(string? FilePath, DxfTransformModel? Transform)> LoadAsync(int projectId);

        /// <summary>Remove all DXF records for a project.</summary>
        Task DeleteAsync(int projectId);
    }
}
