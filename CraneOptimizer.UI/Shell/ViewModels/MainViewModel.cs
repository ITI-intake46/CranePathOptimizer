using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F3_CraneSetup;
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
        private readonly CraneVisualBuilder _craneVisualBuilder;

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
            ICraneSceneInteractionService craneSceneInteraction,
            CraneManagerViewModel craneManager,
            CraneVisualBuilder craneVisualBuilder)
        {
            Scene = scene;
            Drawing = drawing;
            CranePlacement = cranePlacement;
            IfcViewer = ifcViewer;
            Dxf = dxf;
            CraneSetup = craneSetup;
            ElementManagement = elementManagement;
            CraneManager = craneManager;

            _ifcReader = ifcReader;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            _cranePlacementService = cranePlacementService;
            _sceneRendering = sceneRendering;
            _status = status;
            _originService = originService;
            _craneSceneInteraction = craneSceneInteraction;
            _craneVisualBuilder = craneVisualBuilder;

            _status.StatusChanged += OnStatusChanged;
            _status.HistoryChanged += OnHistoryChanged;
            _originService.OriginChanged += OnOriginChanged;
            _originService.PickingChanged += OnOriginPickingChanged;
            _craneSceneInteraction.StateChanged += OnCraneInteractionStateChanged;

            // When any crane is built/re-rendered, push its parameters to the right panel.
            _craneVisualBuilder.CraneParametersReady += (scene, p) =>
            {
                var selected = _craneSceneInteraction.SelectedSceneCrane;
                if (selected == null || selected.Id == scene.Id)
                    CraneManager.UpdateActiveParams(p);
            };

            // Wire remove actions.
            CraneManager.RequestRemoveSelected = RemoveSelectedCrane;
            CraneManager.RequestRemoveAll = RemoveAllCranes;

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
        public CraneManagerViewModel CraneManager { get; }
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

                // Propagate to sub-ViewModels — actual placement is done by AddMobileCraneCommand.
                CranePlacement.SelectedCrane = value;
                CraneSetup.SelectedCrane = value;

                StatusText = value != null
                    ? $"Crane '{value.Name}' selected."
                    : "No crane selected.";
            }
        }

        /// <summary>
        /// Calculates a default position for the crane beside the IFC model bounds.
        /// Returns null if no elements are loaded.
        /// </summary>
        private DomainPoint3D? CalculateDefaultCranePosition()
        {
            var elements = Scene.GetElements();
            if (elements == null || elements.Count == 0)
                return null;

            // Calculate bounding box of all elements
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            foreach (var element in elements)
            {
                var cog = element.ElementCOG;
                var halfLength = (element.BBoxLength ?? 0) / 2.0;
                var halfWidth = (element.BBoxWidth ?? 0) / 2.0;
                var halfHeight = (element.BBoxHeight ?? 0) / 2.0;

                minX = Math.Min(minX, cog.X - halfLength);
                maxX = Math.Max(maxX, cog.X + halfLength);
                minY = Math.Min(minY, cog.Y - halfWidth);
                maxY = Math.Max(maxY, cog.Y + halfWidth);
                minZ = Math.Min(minZ, cog.Z - halfHeight);
                maxZ = Math.Max(maxZ, cog.Z + halfHeight);
            }

            // Place crane beside the IFC model (to the right with some offset)
            double offsetX = (maxX - minX) * 0.1 + 10; // 10% of model width + 10m offset
            double craneX = maxX + offsetX;
            double craneY = (minY + maxY) / 2.0; // Center of model in Y
            double craneZ = minZ; // Ground level

            return new DomainPoint3D(craneX, craneY, craneZ);
        }

        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications?.FirstOrDefault();

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
