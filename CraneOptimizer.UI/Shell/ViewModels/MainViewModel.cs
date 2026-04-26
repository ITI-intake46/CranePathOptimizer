using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.Interactions;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        #region Fields
        private readonly IIfcReaderRepository _ifcReader;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;
        private readonly ICranePlacementService _cranePlacementService;

        private Crane? _selectedCrane;
        private bool _disposed; 
        #endregion
        public ISceneService Scene { get; }

        #region Constructor
        public MainViewModel(
            ISceneService scene,
            ZoneDrawingViewModel zonesVM,
            CranePlacementViewModel cranePlacement,
            IfcViewerViewModel ifcViewer,
            DxfTabViewModel dxf,
            ElementManagementViewModel elementsVM,
            IIfcReaderRepository ifcReader,
            IRenderingService render,
            ICraneRepository craneRepo,
            IDialogService dialog,
            ICranePlacementService cranePlacementService)
        {
            Scene = scene;
            ZonesVM = zonesVM;
            ZonesInteraction = new ZoneDrawingInteraction(ZonesVM);

            CranePlacement = cranePlacement;
            IfcViewer = ifcViewer;
            Dxf = dxf;
            ElementsVM = elementsVM;

            // Initialize interaction lifecycle
            ZonesVM.DrawingStarted += OnZoneDrawingStarted;
            ZonesVM.DrawingEnded += OnZoneDrawingEnded;

            _ifcReader = ifcReader;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            _cranePlacementService = cranePlacementService;

            ElementsVM.ElementsLoaded += OnElementsLoaded;

            OptimizeCommand = new RelayCommand(OpenOptimizerDialog);

            _ = LoadCranesAsync();
        }
        #endregion

        #region Collections
        public ObservableCollection<Crane> Cranes { get; } = new();
        #endregion

        #region ViewModels
        public CranePlacementViewModel CranePlacement { get; }
        public ZoneDrawingViewModel ZonesVM { get; }
        public ZoneDrawingInteraction ZonesInteraction { get; }
        public IfcViewerViewModel IfcViewer { get; }
        public DxfTabViewModel Dxf { get; }
        public ElementManagementViewModel ElementsVM { get; }
        #endregion

        #region Commands
        public ICommand StartDrawingCommand { get; }
        public ICommand OptimizeCommand { get; } 
        #endregion

        #region Crane Selected
        // =========================
        // Selected Crane
        // =========================
        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCraneSpec));

                CranePlacement.SelectedCrane = value;
                StatusText = $"Crane Selected: {value?.Name}";
            }
        }

        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications.FirstOrDefault();
        #endregion

        #region GA_Run
        private void OpenOptimizerDialog()
        {
            var dialog = new CraneOptimizerDialog(
                _cranePlacementService,
                _render,
                _dialog,
                _craneRepo,
                Scene);

            if (System.Windows.Application.Current?.MainWindow != null)
                dialog.Owner = System.Windows.Application.Current.MainWindow;

            dialog.ShowDialog();

            if (dialog.LatestResult != null)
            {
                StatusText = "Optimization Completed";
                HistoryBreadcrumb = $"Best Fitness: {dialog.LatestResult.FitnessScore:F2}";
            }
        }
        #endregion

        #region Elements bridge
        private void OnElementsLoaded(int count)
        {
            IsModelLoaded = true;
            StatusText = $"Model Loaded ({count} elements)";
        }
        private void OnZoneDrawingStarted()
        {
            IfcViewer.CurrentInteraction = ZonesInteraction;
        }

        private void OnZoneDrawingEnded()
        {
            IfcViewer.CurrentInteraction = null;
        }

        #endregion

        #region Dispose
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ElementsVM.ElementsLoaded -= OnElementsLoaded;
            ZonesVM.DrawingStarted -= OnZoneDrawingStarted;
            ZonesVM.DrawingEnded -= OnZoneDrawingEnded;

            ElementsVM.Dispose();
            ZonesVM.Dispose();
        } 
        #endregion

        #region Status Properties
        private string _coordX = "0.00M";
        public string CoordX
        {
            get => _coordX;
            set { _coordX = value; OnPropertyChanged(); }
        }

        private string _coordY = "0.00M";
        public string CoordY
        {
            get => _coordY;
            set { _coordY = value; OnPropertyChanged(); }
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _historyBreadcrumb = "No recent action";
        public string HistoryBreadcrumb
        {
            get => _historyBreadcrumb;
            set { _historyBreadcrumb = value; OnPropertyChanged(); }
        }

        private bool _isModelLoaded;
        public bool IsModelLoaded
        {
            get => _isModelLoaded;
            set { _isModelLoaded = value; OnPropertyChanged(); }
        }

        private string _selectedElementName = string.Empty;
        public string SelectedElementName
        {
            get => _selectedElementName;
            set { _selectedElementName = value; OnPropertyChanged(); }
        }

        private string _renderQualityLabel = "● STD";
        public string RenderQualityLabel
        {
            get => _renderQualityLabel;
            set { _renderQualityLabel = value; OnPropertyChanged(); }
        } 
        #endregion

        #region Helpers
        public void SetCoordinates(double x, double y)
        {
            CoordX = $"{x:F2}M";
            CoordY = $"{y:F2}M";
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepo.GetAllWithSpecsAsync();
            Cranes.Clear();
            foreach (var c in cranes)
                Cranes.Add(c);
        } 
        #endregion
    }
}
