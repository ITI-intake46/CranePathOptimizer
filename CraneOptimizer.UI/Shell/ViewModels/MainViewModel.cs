using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

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
        private readonly List<PrecastElement> _sceneElements = new();

        private Crane? _selectedCrane;
        private bool _disposed;
        private bool _isPickingOrigin;
        private DomainPoint3D? _originPoint;
        private Guid? _currentProjectId;
        private string _currentProjectName = string.Empty;
        private string? _currentProjectIfcPath;
        #endregion
        public ISceneService Scene { get; }

        #region Constructor
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
            ICranePlacementService cranePlacementService)
        {
            Scene = scene;
            Drawing = drawing;
            CranePlacement = cranePlacement;
            IfcViewer = ifcViewer;
            Dxf = dxf;

            _ifcReader = ifcReader;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            _cranePlacementService = cranePlacementService;

            Scene.ElementsChanged += OnElementsChanged;

            OptimizeCommand = new RelayCommand(OpenOptimizerDialog);

            _ = LoadCranesAsync();
        }
        #endregion

        #region Collections
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<Crane> Cranes { get; } = new();

        #endregion

        #region ViewModels
        public CranePlacementViewModel CranePlacement { get; }
        public DrawingViewModel Drawing { get; }
        public IfcViewerViewModel IfcViewer { get; }
        public DxfTabViewModel Dxf { get; }
        #endregion

        #region Commands
        public ICommand StartDrawingCommand { get; }
        public ICommand OptimizeCommand { get; }
        #endregion

        #region Project Context
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

        public void OpenProject(Guid projectId, string projectName, string? ifcFilePath)
        {
            CurrentProjectId = projectId;
            CurrentProjectName = projectName;
            CurrentProjectIfcPath = ifcFilePath;
            StatusText = $"Project opened: {projectName}";
            HistoryBreadcrumb = "Load IFC when needed.";
        }
        #endregion

        #region Crane Selected
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
            if (CranePlacement.RunOptimizationWorkflow() && CranePlacement.Result != null)
            {
                StatusText = "Optimization Completed";
                HistoryBreadcrumb = $"Best Fitness: {CranePlacement.Result.FitnessScore:F2}";
            }
        }
        #endregion

        #region Scene sync
        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Debug.WriteLine("Scene ElementsChanged: " + elements.Count);

            _sceneElements.Clear();
            _sceneElements.AddRange(elements);

            Elements.Clear();
            FilteredElements.Clear();

            foreach (var e in elements)
            {
                Elements.Add(e);
                FilteredElements.Add(e);
            }

            if (_sceneElements.Count == 0)
            {
                _render.ClearLayer("Elements");
                OriginPoint = null;
                IsPickingOrigin = false;
                IsModelLoaded = false;
                StatusText = "Model cleared";
                return;
            }

            OriginPoint = null;
            IsPickingOrigin = false;

            IsModelLoaded = true;
            ReRenderElements();
            StatusText = $"Model loaded ({elements.Count} elements).";
        }

        public bool IsPickingOrigin
        {
            get => _isPickingOrigin;
            set
            {
                if (_isPickingOrigin == value) return;
                _isPickingOrigin = value;
                OnPropertyChanged();
            }
        }

        public DomainPoint3D? OriginPoint
        {
            get => _originPoint;
            private set
            {
                _originPoint = value;
                OnPropertyChanged();
            }
        }

        public void SetOrigin(DomainPoint3D origin)
        {
            OriginPoint = origin;
            IsPickingOrigin = false;
            ReRenderElements();
        }

        public void ReRenderElements()
        {
            _render.ClearLayer("Elements");

            if (_sceneElements.Count == 0)
                return;

            Debug.WriteLine("Rendering Elements: " + _sceneElements.Count);

            _render.Render(
                _sceneElements.Select(e => (e, (LiftStatus?)null)),
                new ElementVisualBuilder(OriginPoint),
                "Elements"
            );

            if (OriginPoint != null)
                StatusText = $"Origin set at ({OriginPoint.X:F2}, {OriginPoint.Y:F2}, {OriginPoint.Z:F2}).";
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Scene.ElementsChanged -= OnElementsChanged;
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

        private string _renderQualityLabel = "STD";
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
