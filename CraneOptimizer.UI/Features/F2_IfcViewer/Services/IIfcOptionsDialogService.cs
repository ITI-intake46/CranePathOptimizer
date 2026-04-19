using CraneOptimizer.UI.Features.F2_IfcViewer.Models;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public interface IIfcOptionsDialogService
    {
        IfcFilterOptions? Show(string filePath);
    }
}
