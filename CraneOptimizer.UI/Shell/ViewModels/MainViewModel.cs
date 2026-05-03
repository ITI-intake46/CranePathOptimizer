using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public partial class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;
        private readonly ICranePlacementService _cranePlacementService;
        private readonly ISceneRenderingManager _sceneRendering;
        private readonly IStatusService _status;
        private readonly IOriginService _originService;
        private readonly ICraneSceneInteractionService _craneSceneInteraction;

        private Crane? _selectedCrane;
        private bool _disposed;
        private Guid? _currentProjectId;
        private string _currentProjectName = string.Empty;
        private string? _currentProjectIfcPath;

        public MainViewModel(
            ISceneService scene,
            DrawingViewModel drawing,
            CranePlacementViewModel cranePlacement,
            IfcViewerViewModel ifcViewer,
            DxfTabViewModel dxf,
            IIfcReaderRepository ifcReader,
            IRenderingService render,
            ICraneRepository craneRepo,
            IDialogService dialog,
            CraneSetupViewModel craneSetup,
            ICranePlacementService cranePlacementService,
            ElementManagementViewModel elementManagement,
            ISceneRenderingManager sceneRendering,
            IStatusService status,
            IOriginService originService,
            ICraneSceneInteractionService craneSceneInteraction)
        {
            Scene = scene;
            Drawing = drawing;
            CranePlacement = cranePlacement;
            IfcViewer = ifcViewer;
            Dxf = dxf;
            CraneSetup = craneSetup;
            ElementManagement = elementManagement;

            _ifcReader = ifcReader;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            _cranePlacementService = cranePlacementService;
            _sceneRendering = sceneRendering;
            _status = status;
            _originService = originService;
            _craneSceneInteraction = craneSceneInteraction;

            _status.StatusChanged += OnStatusChanged;
            _status.HistoryChanged += OnHistoryChanged;
            _originService.OriginChanged += OnOriginChanged;
            _originService.PickingChanged += OnOriginPickingChanged;
            _craneSceneInteraction.StateChanged += OnCraneInteractionStateChanged;

            CranePlacement.PlaceCraneInScene = PlaceSelectedCrane;
            CranePlacement.RenderElementsWithCurrentStatus = ReRenderElements;

            ConfigureSceneRendering();

            OptimizeCommand = new RelayCommand(OpenOptimizerDialog);
            AddMobileCraneCommand = new RelayCommand(AddMobileCraneAndStartPlacement, CanAddCrane);
            RemoveSelectedCraneCommand = new RelayCommand(RemoveSelectedCrane, CanRemoveSelectedCrane);
            RemoveAllCranesCommand = new RelayCommand(RemoveAllCranes, () => Scene.GetCranes().Count > 0);

            _ = LoadCranesAsync();

            ElementManagement.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ElementManagementViewModel.SelectedElement))
                    ReRenderElements();
            };
        }

        public ISceneService Scene { get; }
        public CraneSetupViewModel CraneSetup { get; }
        public ObservableCollection<Crane> Cranes { get; } = new();
        public ElementManagementViewModel ElementManagement { get; }
        public CranePlacementViewModel CranePlacement { get; }
        public DrawingViewModel Drawing { get; }
        public IfcViewerViewModel IfcViewer { get; }
        public DxfTabViewModel Dxf { get; }

        public ICommand StartDrawingCommand { get; }
        public ICommand OptimizeCommand { get; }
        public ICommand AddMobileCraneCommand { get; }
        public ICommand RemoveSelectedCraneCommand { get; }
        public ICommand RemoveAllCranesCommand { get; }

        public Guid? CurrentProjectId
        {
            get => _currentProjectId;
            private set
            {
                _currentProjectId = value;
                OnPropertyChanged();
            }
        }

        public string CurrentProjectName
        {
            get => _currentProjectName;
            private set
            {
                _currentProjectName = value;
                OnPropertyChanged();
            }
        }

        public string? CurrentProjectIfcPath
        {
            get => _currentProjectIfcPath;
            private set
            {
                _currentProjectIfcPath = value;
                OnPropertyChanged();
            }
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCraneSpec));

                CranePlacement.SelectedCrane = value;
                CraneSetup.SelectedCrane = value;

                if (value != null)
                {
                    CranePlacement.IsPlacing = true;
                    StatusText = "Click in viewport to place crane.";
                }
                else
                {
                    StatusText = "No crane selected.";
                }
            }
        }

        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications.FirstOrDefault();

        public string StatusText
        {
            get => _status.StatusText;
            set => _status.SetStatus(value);
        }

        public string HistoryBreadcrumb
        {
            get => _status.HistoryBreadcrumb;
            set => _status.SetHistory(value);
        }

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

        private bool _isModelLoaded;
        public bool IsModelLoaded
        {
            get => _isModelLoaded;
            set { _isModelLoaded = value; OnPropertyChanged(); }
        }

        private string _renderQualityLabel = "STD";
        public string RenderQualityLabel
        {
            get => _renderQualityLabel;
            set { _renderQualityLabel = value; OnPropertyChanged(); }
        }

        public void OpenProject(Guid projectId, string projectName, string? ifcFilePath)
        {
            CurrentProjectId = projectId;
            CurrentProjectName = projectName;
            CurrentProjectIfcPath = ifcFilePath;
            _status.Set($"Project opened: {projectName}", "Load IFC when needed.");
        }

        public void SetCoordinates(double x, double y)
        {
            CoordX = $"{x:F2}M";
            CoordY = $"{y:F2}M";
        }

        private void OnStatusChanged() => OnPropertyChanged(nameof(StatusText));
        private void OnHistoryChanged() => OnPropertyChanged(nameof(HistoryBreadcrumb));
    }
}
