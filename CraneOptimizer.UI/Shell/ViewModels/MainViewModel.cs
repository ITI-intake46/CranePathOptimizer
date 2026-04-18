using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;
using CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.IO;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        // =========================
        // Fields
        // =========================
        private readonly IIfcReaderRepository _ifcReader;
        private readonly IGeneticAlgorithmEngine _ga;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;

        private Crane? _selectedCrane;
        private bool _disposed;

        // =========================
        // Collections
        // =========================
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<Crane> Cranes { get; } = new();

        // =========================
        // ViewModels
        // =========================
        public CranePlacementViewModel CranePlacement { get; }
        public SceneService Scene { get; }
        public DrawingViewModel Drawing { get; }

        // =========================
        // Status Properties (MVVM UI)
        // =========================
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

        // =========================
        // Commands
        // =========================
        public ICommand LoadIfcCommand { get; }
        public ICommand StartDrawingCommand { get; }
        public ICommand OptimizeCommand { get; }

        // =========================
        // Constructor
        // =========================
        public MainViewModel(
            SceneService scene,
            DrawingViewModel drawing,
            CranePlacementViewModel cranePlacement,
            IIfcReaderRepository ifcReader,
            IGeneticAlgorithmEngine ga,
            IRenderingService render,
            ICraneRepository craneRepo,
            IDialogService dialog)
        {
            Scene = scene;
            Drawing = drawing;
            CranePlacement = cranePlacement;

            _ifcReader = ifcReader;
            _ga = ga;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;

            Scene.ElementsChanged += OnElementsChanged;

            LoadIfcCommand = new RelayCommand(LoadIfc);
            StartDrawingCommand = new RelayCommand(() =>
            {
                Drawing.Start();
                StatusText = "Drawing started";
            });

            OptimizeCommand = new RelayCommand(RunGA);

            _ = LoadCranesAsync();
        }

        // =========================
        // Dispose
        // =========================
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Scene.ElementsChanged -= OnElementsChanged;
        }

        // =========================
        // Scene Sync
        // =========================
        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Elements.Clear();
            FilteredElements.Clear();

            foreach (var e in elements)
            {
                Elements.Add(e);
                FilteredElements.Add(e);
            }

            _render.RenderElements(elements);

            IsModelLoaded = true;
            StatusText = $"Model Loaded ({elements.Count} elements)";
        }

        // =========================
        // Load IFC
        // =========================
        private void LoadIfc()
        {
            var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
            if (string.IsNullOrEmpty(path)) return;

            var dialog = new IFCLoadOptionsDialog(path);
            if (dialog.ShowDialog() != true) return;

            var filter = new IfcFilterOptions
            {
                HideGrids = dialog.HideGrids,
                HideLevels = dialog.HideLevels,
                HideAnnotations = dialog.HideAnnotations,
                HideSite = dialog.HideSite,
                HideSpaces = dialog.HideSpaces,
                HideOpenings = dialog.HideOpenings,
                HideVirtualElements = dialog.HideVirtualElements
            };

            var all = _ifcReader.ReadElements(path);
            var visible = ApplyFilter(all, filter);

            Scene.SetElements(visible);

            HistoryBreadcrumb = $"Loaded IFC: {Path.GetFileName(path)}";
        }

        private static List<PrecastElement> ApplyFilter(List<PrecastElement> elements, IfcFilterOptions options)
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

        // =========================
        // GA
        // =========================
        private void RunGA()
        {
            if (SelectedCraneSpec == null)
            {
                _dialog.ShowMessage("No Crane Selected");
                return;
            }

            if (!Elements.Any())
            {
                _dialog.ShowMessage("No elements loaded");
                return;
            }

            StatusText = "Running Optimization...";

            var settings = new GASettings
            {
                PopulationSize = 30,
                MaxGenerations = 50,
                MutationRate = 0.1
            };

            var input = new PlacementInput
            {
                Elements = Elements.ToList(),
                CraneSpec = SelectedCraneSpec,
                Settings = settings
            };

            var solution = _ga.Run(input);

            if (solution == null)
            {
                StatusText = "No solution found";
                return;
            }

            foreach (var pos in solution.Chromosome.CranePositions)
                _render.RenderBestPosition(pos.X, pos.Y);

            StatusText = "Optimization Completed";
            HistoryBreadcrumb = $"Best Fitness: {solution.Fitness:F2}";
        }

        // =========================
        // Helpers
        // =========================
        public void SetCoordinates(double x, double y)
        {
            CoordX = $"{x:F2}M";
            CoordY = $"{y:F2}M";
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepo.GetAllAsync();
            Cranes.Clear();
            foreach (var c in cranes)
                Cranes.Add(c);
        }
    }
}