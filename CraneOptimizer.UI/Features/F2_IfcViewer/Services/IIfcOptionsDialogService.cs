using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public interface IIfcOptionsDialogService
    {
        IfcLoadOptionsDto? Show(string filePath);
        IfcLoadOptionsDto? ShowDisplayOptions(string? modelPath);
    }
}
