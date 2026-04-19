using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IIfcOptionsDialogService _dialogService; 
        private readonly IDialogService _dialog;


        public ICommand LoadIfcCommand { get; }

        public IfcViewerViewModel(
            IIfcReaderRepository ifcReader,
            ISceneService scene,
            IDialogService dialog,
            IIfcOptionsDialogService dialogService)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialogService = dialogService;
            _dialog = dialog;

            LoadIfcCommand = new RelayCommand(LoadIfc);
        }

        private void LoadIfc()
        {
            var path = OpenFile();
            if (string.IsNullOrEmpty(path)) return;

            var filter = _dialogService.Show(path);
            if (filter == null) return;

            var all = _ifcReader.ReadElements(path);
            var visible = ApplyFilter(all, filter);

            _scene.SetElements(visible);
        }
        private string? OpenFile()
        {
            var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
            return path;
        }

        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements,
            IfcFilterOptions options)
        {
            return elements.Where(e =>
            {
                var t = e.ElementType ?? "";

                if (options.HideGrids && t.Contains("IfcGrid", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideLevels && t.Contains("IfcBuildingStorey", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideAnnotations && t.Contains("IfcAnnotation", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSite && t.Contains("IfcSite", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSpaces && t.Contains("IfcSpace", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideOpenings && t.Contains("IfcOpeningElement", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideVirtualElements && t.Contains("IfcVirtualElement", StringComparison.OrdinalIgnoreCase)) return false;

                return true;
            }).ToList();
        }
    }
}