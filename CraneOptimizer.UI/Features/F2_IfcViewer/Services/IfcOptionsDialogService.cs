using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;
using CraneOptimizer.Domain.Interfaces;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public class IfcOptionsDialogService : IIfcOptionsDialogService
    {
        private readonly IIfcGeoreferencingExtractor _georefExtractor;
        private readonly IIfcVisibilityOptionsService _visibilityOptionsService;

        public IfcOptionsDialogService(
            IIfcGeoreferencingExtractor georefExtractor,
            IIfcVisibilityOptionsService visibilityOptionsService)
        {
            _georefExtractor = georefExtractor;
            _visibilityOptionsService = visibilityOptionsService;
        }

        public IfcLoadOptionsDto? Show(string filePath)
        {
            // Start the georeferencing pre-scan asynchronously in the background
            var georefTask = Task.Run(() => _georefExtractor.ExtractFromFile(filePath));

            var dialog = IFCLoadOptionsDialog.CreateLoadOptionsDialog(
                filePath,
                georefTask,
                _visibilityOptionsService.GetCurrentOptions(),
                _visibilityOptionsService.OptionDefinitions);

            if (dialog.ShowDialog() != true)
                return null;

            return dialog.GetOptions();
        }

        public IfcLoadOptionsDto? ShowDisplayOptions(string? modelPath)
        {
            var dialog = IFCLoadOptionsDialog.CreateDisplayOptionsDialog(
                modelPath,
                _visibilityOptionsService.GetCurrentOptions(),
                _visibilityOptionsService.OptionDefinitions);

            if (dialog.ShowDialog() != true)
                return null;

            return dialog.GetOptions();
        }
    }
}
