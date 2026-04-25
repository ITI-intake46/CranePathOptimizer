using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel : ViewModelBase
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IIfcOptionsDialogService _dialogService;
        private readonly IDialogService _dialog;
        public IViewportInteraction? CurrentInteraction { get; set; }
        public ICommand ViewportClickCommand { get; }
        public ICommand ViewportMoveCommand { get; }
        public ICommand LoadIfcCommand { get; }

        #region State

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();

                // CanExecute
                (LoadIfcCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _status = "Idle";
        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) < 0.01) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        #endregion

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
            
            LoadIfcCommand = new RelayCommand(
                async () => await ExecuteLoadIfcAsync(),
                () => !IsLoading);

            ViewportClickCommand = new RelayCommand<DomainPoint3D>(
                p => CurrentInteraction?.OnClick(p));

            ViewportMoveCommand = new RelayCommand<DomainPoint3D>(
                p => CurrentInteraction?.OnMove(p));
        }

        #region Main Logic

        private async Task ExecuteLoadIfcAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                Progress = 0;
                Status = "Selecting IFC file...";

                // 1️⃣ select file
                var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
                if (string.IsNullOrWhiteSpace(path))
                {
                    Status = "Operation cancelled";
                    return;
                }

                // 2️⃣ filter
                var filter = _dialogService.Show(path);
                if (filter == null)
                {
                    Status = "Filter cancelled";
                    return;
                }

                // 3️⃣ read ifc data
                Status = "Reading IFC...";

                var progress = new Progress<double>(p =>
                {
                    Progress = p;
                    Status = $"Reading IFC... {p:F0}%";
                });

                var elements = await Task.Run(() =>
                    _ifcReader.ReadElements(path, progress));

                if (elements == null || elements.Count == 0)
                {
                    Status = "No elements found";
                    _dialog.ShowMessage("No elements found.");
                    return;
                }

                // 4️⃣ filteration
                Status = "Filtering elements...";
                var visible = ApplyFilter(elements, filter);

                // 5️⃣ update scene
                Status = "Updating 3D scene...";
                _scene.SetElements(visible);

                Progress = 100;
                Status = $"Loaded {visible.Count} elements successfully";
            }
            catch (Exception ex)
            {
                Status = "Error occurred";
                _dialog.ShowMessage(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Filtering

        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements,
            IfcFilterOptions options)
        {
            if (elements == null || elements.Count == 0)
                return new List<PrecastElement>();

            return elements.Where(e =>
            {
                var t = e.ElementType ?? string.Empty;

                if (options.HideGrids && Contains(t, "IfcGrid")) return false;
                if (options.HideLevels && Contains(t, "IfcBuildingStorey")) return false;
                if (options.HideAnnotations && Contains(t, "IfcAnnotation")) return false;
                if (options.HideSite && Contains(t, "IfcSite")) return false;
                if (options.HideSpaces && Contains(t, "IfcSpace")) return false;
                if (options.HideOpenings && Contains(t, "IfcOpeningElement")) return false;
                if (options.HideVirtualElements && Contains(t, "IfcVirtualElement")) return false;

                return true;

            }).ToList();
        }

        private static bool Contains(string source, string value)
        {
            return source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}