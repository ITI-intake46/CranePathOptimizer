using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
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
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
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
        private readonly IUnitSettingsService _unitSettings;
        private readonly CraneVisualBuilder _craneVisualBuilder;
        private readonly IProjectStateService _projectStateService;

        private Crane? _selectedCrane;
        private bool _disposed;
        private bool _suppressProjectStateSave;
        private Guid? _currentProjectId;
        private ProjectSession? _currentProjectSession;
        private string _currentProjectName = string.Empty;
        private string? _currentProjectIfcPath;
        private string _currentProjectFolderPath = string.Empty;
        private string _currentProjectDatabasePath = string.Empty;

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
            IUnitSettingsService unitSettings,
            CraneVisualBuilder craneVisualBuilder,
            IProjectStateService projectStateService)
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
            _unitSettings = unitSettings;
            _craneVisualBuilder = craneVisualBuilder;
            _projectStateService = projectStateService;

            _status.StatusChanged += OnStatusChanged;
            _status.HistoryChanged += OnHistoryChanged;
            _originService.OriginChanged += OnOriginChanged;
            _originService.PickingChanged += OnOriginPickingChanged;
            _craneSceneInteraction.StateChanged += OnCraneInteractionStateChanged;
            _unitSettings.SettingsChanged += OnUnitSettingsChanged;
            Scene.ZoneAdded += OnProjectStateZoneChanged;
            Scene.ZoneRemoved += OnProjectStateZoneChanged;
            Scene.ZoneUpdated += OnProjectStateZoneChanged;
            Scene.ZonesCleared += OnProjectStateChanged;
            Scene.CraneAdded += OnProjectStateCraneChanged;
            Scene.CraneRemoved += OnProjectStateCraneChanged;
            Scene.CraneMoved += OnProjectStateCraneChanged;
            Scene.CraneRotated += OnProjectStateCraneChanged;
            Scene.CraneUpdated += OnProjectStateCraneChanged;
            Scene.CranesCleared += OnProjectStateChanged;
            IfcViewer.IfcModelChanged += OnProjectStateChanged;
            Dxf.DxfStateChanged += OnProjectStateChanged;

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

        public string CurrentProjectFolderPath
        {
            get => _currentProjectFolderPath;
            private set
            {
                _currentProjectFolderPath = value;
                OnPropertyChanged();
            }
        }

        public string CurrentProjectDatabasePath
        {
            get => _currentProjectDatabasePath;
            private set
            {
                _currentProjectDatabasePath = value;
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
            double craneZ = CalculateSceneGroundElevation();

            return new DomainPoint3D(craneX, craneY, craneZ);
        }

        private double CalculateSceneGroundElevation()
        {
            var zoneVertices = Scene.GetZones()
                .Where(zone => zone.Vertices.Count > 0)
                .SelectMany(zone => zone.Vertices)
                .ToList();

            return zoneVertices.Count > 0
                ? zoneVertices.Average(vertex => vertex.Z)
                : 0d;
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

        private string _renderQualityLabel = "Medium";
        public string RenderQualityLabel
        {
            get => _renderQualityLabel;
            set { _renderQualityLabel = value; OnPropertyChanged(); }
        }

        // ── Unit selector ──────────────────────────────────────────────

        public IReadOnlyList<string> AvailableUnits { get; } = new[] { "m", "mm" };

        private string _selectedUnit = "m";
        public string SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (_selectedUnit == value) return;
                _selectedUnit = value;
                OnPropertyChanged();
                ApplySelectedUnit();
            }
        }

        // ── Building info (populated after IFC load) ───────────────────

        private double _buildingArea;
        public double BuildingArea
        {
            get => _buildingArea;
            private set { _buildingArea = value; OnPropertyChanged(); }
        }

        private double _buildingHeight;
        public double BuildingHeight
        {
            get => _buildingHeight;
            private set { _buildingHeight = value; OnPropertyChanged(); }
        }

        private string _areaUnit = "m²";
        public string AreaUnit
        {
            get => _areaUnit;
            private set { _areaUnit = value; OnPropertyChanged(); }
        }

        private string _buildingName = "No Building Loaded";
        public string BuildingName
        {
            get => _buildingName;
            private set { _buildingName = value; OnPropertyChanged(); }
        }

        private void ApplySelectedUnit()
        {
            var lengthUnit = _selectedUnit == "mm" ? LengthUnit.Millimeter : LengthUnit.Meter;
            var current = _unitSettings.Current;

            if (current.DisplayLengthUnit == lengthUnit)
                return;

            var updated = current.Clone();
            updated.DisplayLengthUnit = lengthUnit;
            _unitSettings.ApplyRuntimeSettings(updated);
        }

        private void OnUnitSettingsChanged(object? sender, UnitSettings settings)
        {
            // Sync the ComboBox selection with the service state
            var symbol = UnitSystem.LengthSymbol(settings.DisplayLengthUnit);
            if (_selectedUnit != symbol)
            {
                _selectedUnit = symbol;
                OnPropertyChanged(nameof(SelectedUnit));
            }

            AreaUnit = UnitSystem.AreaSymbol(settings.DisplayAreaUnit);

            // Re-format building metrics with new units
            RefreshBuildingMetrics();
        }

        /// <summary>
        /// Computes building bounding-box area (footprint) and height from loaded elements.
        /// </summary>
        public void RefreshBuildingMetrics()
        {
            var elements = Scene.GetElements();
            if (elements == null || elements.Count == 0)
            {
                BuildingArea = 0;
                BuildingHeight = 0;
                return;
            }

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            foreach (var el in elements)
            {
                var cog = el.ElementCOG;
                var hL = (el.BBoxLength ?? 0) / 2.0;
                var hW = (el.BBoxWidth ?? 0) / 2.0;
                var hH = (el.BBoxHeight ?? 0) / 2.0;

                minX = Math.Min(minX, cog.X - hL);
                maxX = Math.Max(maxX, cog.X + hL);
                minY = Math.Min(minY, cog.Y - hW);
                maxY = Math.Max(maxY, cog.Y + hW);
                minZ = Math.Min(minZ, cog.Z - hH);
                maxZ = Math.Max(maxZ, cog.Z + hH);
            }

            var settings = _unitSettings.Current;

            double areaM2 = (maxX - minX) * (maxY - minY);
            BuildingArea = UnitSystem.FromSquareMeters(areaM2, settings.DisplayAreaUnit);
            AreaUnit = UnitSystem.AreaSymbol(settings.DisplayAreaUnit);

            double heightM = maxZ - minZ;
            BuildingHeight = UnitSystem.FromMeters(heightM, settings.DisplayLengthUnit);
        }

        public void OpenProject(Guid projectId, string projectName, string? ifcFilePath)
        {
            CurrentProjectId = projectId;
            CurrentProjectName = projectName;
            CurrentProjectIfcPath = ifcFilePath;
            _ = LoadProjectUnitsAsync(projectId);
            _status.Set($"Project opened: {projectName}", "Load IFC when needed.");
        }

        public void OpenProject(ProjectSession session)
        {
            _currentProjectSession = session;
            CurrentProjectId = session.ProjectId;
            CurrentProjectName = session.ProjectName;
            CurrentProjectFolderPath = session.ProjectFolderPath;
            CurrentProjectDatabasePath = session.ProjectDatabasePath;
            CurrentProjectIfcPath = session.ProjectCard.IfcFilePath;
            _ = LoadProjectUnitsAsync(session.ProjectId);
            _ = LoadProjectStateAsync(session);
            _status.Set($"Project opened: {session.ProjectName}", "Load IFC when needed.");
        }

        private async Task LoadProjectUnitsAsync(Guid projectId)
        {
            try
            {
                await _unitSettings.LoadProjectSettingsAsync(projectId);
            }
            catch (Exception ex)
            {
                _status.Set($"Project opened, but unit settings could not be loaded: {ex.Message}");
            }
        }

        public void SetCoordinates(double x, double y)
        {
            var settings = _unitSettings.Current;
            CoordX = UnitSystem.FormatLength(x, settings);
            CoordY = UnitSystem.FormatLength(y, settings);
        }

        private void OnStatusChanged() => OnPropertyChanged(nameof(StatusText));
        private void OnHistoryChanged() => OnPropertyChanged(nameof(HistoryBreadcrumb));
    }
}
