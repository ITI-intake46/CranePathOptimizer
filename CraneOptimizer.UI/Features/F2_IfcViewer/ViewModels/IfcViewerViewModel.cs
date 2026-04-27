using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using System.Diagnostics;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel : ViewModelBase
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IIfcOptionsDialogService _dialogService;
        private readonly IDialogService _dialog;

        public ICommand LoadIfcCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _status = "Idle";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public IfcViewerViewModel(
            IIfcReaderRepository ifcReader,
            ISceneService scene,
            IDialogService dialog,
            IIfcOptionsDialogService dialogService)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;

            LoadIfcCommand = new RelayCommand(async () => await LoadIfcAsync(), () => !IsLoading);
        }

        private async Task LoadIfcAsync()
        {
            try
            {
                IsLoading = true;
                Progress = 0;
                Status = "Selecting file...";

                var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
                if (string.IsNullOrEmpty(path)) return;

                var filter = _dialogService.Show(path);
                if (filter == null) return;

                Status = "Reading IFC...";

                var progress = new Progress<double>(p =>
                {
                    Progress = p;
                    Status = $"Reading IFC... {p:F0}%";
                });

                var all = await Task.Run(() =>
                    _ifcReader.ReadElements(path, progress));

                Debug.WriteLine("IFC Loaded: " + (all?.Count ?? 0));

                if (all == null || !all.Any())
                {
                    _dialog.ShowMessage("No elements found.");
                    return;
                }

                Status = "Filtering...";
                var visible = ApplyFilter(all, filter);

                Status = "Updating scene...";
                Debug.WriteLine("IFC Visible Elements: " + visible.Count);
                _scene.SetElements(visible);

                Progress = 100;
                Status = $"Loaded {visible.Count} elements";
            }
            catch (Exception ex)
            {
                _dialog.ShowMessage(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
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
