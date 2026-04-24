using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public class IfcOptionsDialogService : IIfcOptionsDialogService
    {
        public IfcFilterOptions? Show(string filePath)
        {
            var dialog = new IFCLoadOptionsDialog(filePath);

            if (dialog.ShowDialog() != true)
                return null;

            return new IfcFilterOptions
            {
                HideGrids = dialog.HideGrids,
                HideLevels = dialog.HideLevels,
                HideAnnotations = dialog.HideAnnotations,
                HideSite = dialog.HideSite,
                HideSpaces = dialog.HideSpaces,
                HideOpenings = dialog.HideOpenings,
                HideVirtualElements = dialog.HideVirtualElements
            };
        }
    }
}
