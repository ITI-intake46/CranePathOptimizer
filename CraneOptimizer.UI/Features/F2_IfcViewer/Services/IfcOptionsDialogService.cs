using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;
using CraneOptimizer.Domain.Interfaces;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public class IfcOptionsDialogService : IIfcOptionsDialogService
    {
        private readonly IIfcGeoreferencingExtractor _georefExtractor;

        public IfcOptionsDialogService(IIfcGeoreferencingExtractor georefExtractor)
        {
            _georefExtractor = georefExtractor;
        }

        public IfcLoadOptionsDto? Show(string filePath)
        {
            // Start the georeferencing pre-scan asynchronously in the background
            var georefTask = Task.Run(() => _georefExtractor.ExtractFromFile(filePath));

            var dialog = new IFCLoadOptionsDialog(filePath, georefTask);

            if (dialog.ShowDialog() != true)
                return null;

            return new IfcLoadOptionsDto
            {
                HideGrids = dialog.HideGrids,
                HideLevels = dialog.HideLevels,
                HideAnnotations = dialog.HideAnnotations,
                HideSite = dialog.HideSite,
                HideSpaces = dialog.HideSpaces,
                HideOpenings = dialog.HideOpenings,
                HideVirtualElements = dialog.HideVirtualElements,
                CoordinateMode = dialog.SelectedCoordinateMode
            };
        }
    }
}
