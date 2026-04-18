using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;  // IfcFilterOptions
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;   // IFCLoadOptionsDialog
using CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        // =========================
        // Fields
        // =========================
        private readonly IIfcReader _ifcReader;
        private readonly IGeneticAlgorithmEngine _ga;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;

        private Crane? _selectedCrane;
        private bool _disposed;

        // =========================
        // Properties
        // =========================
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<Crane> Cranes { get; } = new();

        public CranePlacementViewModel CranePlacement { get; }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCraneSpec));

                CranePlacement.SelectedCrane = value;
            }
        }

        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications.FirstOrDefault();

        public SceneService Scene { get; }
        public DrawingViewModel Drawing { get; }

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
            IIfcReader ifcReader,
            IGeneticAlgorithmEngine ga,
            IRenderingService render,
            ICraneRepository craneRepo,
            IServiceScopeFactory scopeFactory,
            IDialogService dialog)
        {
            Scene = scene;
            Drawing = drawing;
            _ifcReader = ifcReader;
            _ga = ga;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            CranePlacement = cranePlacement;

            Scene.ElementsChanged += OnElementsChanged;
            Scene.ZoneAdded += OnZoneAdded;

            LoadIfcCommand = new RelayCommand(LoadIfc);
            StartDrawingCommand = new RelayCommand(() => Drawing.Start());
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
            Scene.ZoneAdded -= OnZoneAdded;
        }

        // =========================
        // Events
        // =========================
        private void OnElementsChanged(List<PrecastElement> elements)
            => _render.RenderElements(elements);

        private void OnZoneAdded(CranePlacementZone zone)
            => _render.RenderZone(zone);

        // =========================
        // Load Data
        // =========================
        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepo.GetAllAsync();
            Cranes.Clear();
            foreach (var c in cranes) Cranes.Add(c);
        }

        private void LoadIfc()
        {
            // 1. File picker
            var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
            if (string.IsNullOrEmpty(path)) return;

            // 2. Show filter dialog (UI-level concern, F2_IfcViewer feature)
            var optionsDialog = new IFCLoadOptionsDialog(path);
            if (optionsDialog.ShowDialog() != true) return;   // user cancelled

            // 3. Map dialog checkboxes → local UI model
            var filter = new IfcFilterOptions
            {
                HideGrids           = optionsDialog.HideGrids,
                HideLevels          = optionsDialog.HideLevels,
                HideAnnotations     = optionsDialog.HideAnnotations,
                HideSite            = optionsDialog.HideSite,
                HideSpaces          = optionsDialog.HideSpaces,
                HideOpenings        = optionsDialog.HideOpenings,
                HideVirtualElements = optionsDialog.HideVirtualElements,
            };

            // 4. Read ALL elements unchanged (IIfcReader not modified)
            var all = _ifcReader.ReadElements(path);

            // 5. Apply visual filter here in the ViewModel
            var visible = ApplyFilter(all, filter);

            // 6. Send filtered list to scene → triggers rendering
            Scene.SetElements(visible);
        }

        /// <summary>
        /// Filters <paramref name="elements"/> according to <paramref name="options"/>.
        /// Matching is case-insensitive on <see cref="PrecastElement.ElementType"/>
        /// so version-prefixed names (e.g. "Ifc4.IfcGrid") are also excluded.
        /// </summary>
        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements, IfcFilterOptions options)
        {
            return elements.Where(e =>
            {
                var t = e.ElementType ?? string.Empty;

                if (options.HideGrids           && t.Contains("IfcGrid",            StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideLevels          && t.Contains("IfcBuildingStorey",  StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideAnnotations     && t.Contains("IfcAnnotation",      StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSite            && t.Contains("IfcSite",            StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSpaces          && t.Contains("IfcSpace",           StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideOpenings        && t.Contains("IfcOpeningElement",  StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideVirtualElements && t.Contains("IfcVirtualElement",  StringComparison.OrdinalIgnoreCase)) return false;

                return true;
            }).ToList();
        }

        // =========================
        // GA Execution (Correct Version)
        // =========================
        private void RunGA()
        {
            if (SelectedCraneSpec == null)
            {
                _dialog.ShowMessage("No Crane Selected");
                return;
            }

            if (!Scene.Zones.Any() || !Scene.Elements.Any())
            {
                _dialog.ShowMessage("Missing zones or elements");
                return;
            }

            var settings = new GASettings
            {
                PopulationSize = 30,
                MaxGenerations = 50,
                MutationRate = 0.1,
                MaxCranePositions = 1,
                EarlyStoppingGenerations = 10
            };

            // ✅ استخدام PlacementInput (المهم)
            var input = new PlacementInput
            {
                Zones = Scene.Zones.ToList(),
                Elements = Scene.Elements.ToList(),
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = SelectedCraneSpec,
                Settings = settings
            };

            var solution = _ga.Run(input);

            if (solution == null || solution.Chromosome.CranePositions.Count == 0)
            {
                _dialog.ShowMessage("No valid solution found");
                return;
            }

            var result = BuildResult(solution, input.Elements, input.CraneSpec, input.Settings);

            foreach (var pos in result.Positions)
            {
                _render.RenderBestPosition(pos.X, pos.Y);
            }

            //CranePlacement.Result = result;
        }

        // =========================
        // Result Builder
        // =========================
        private CranePlacementResult BuildResult(
            CraneSolution solution,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new CranePlacementResult
            {
                FitnessScore = solution.Fitness,
                CoverageRatio = solution.CoverageRatio
            };

            var stats = new LiftStatistics();

            foreach (var pos in solution.Chromosome.CranePositions)
            {
                var posResult = new CranePositionResult
                {
                    X = pos.X,
                    Y = pos.Y
                };

                foreach (var element in elements)
                {
                    double radius = LiftCalculation.CalculateRadius(pos, element);
                    double height = LiftCalculation.CalculateLiftHeight(element, settings);
                    double boom = LiftCalculation.CalculateRequiredBoomLength(radius, height);

                    double requiredCapacity = element.ElementWT ?? 0;

                    var status = LiftCalculation.DetermineLiftStatus(
                        radius,
                        boom,
                        requiredCapacity,
                        craneSpec,
                        settings);

                    switch (status)
                    {
                        case LiftStatus.Liftable: stats.Liftable++; break;
                        case LiftStatus.OutOfRadius: stats.OutOfRadius++; break;
                        case LiftStatus.Overweight: stats.Overweight++; break;
                        case LiftStatus.HeightFail: stats.HeightFail++; break;
                        case LiftStatus.SpecialCase: stats.SpecialCase++; break;
                    }

                    posResult.ElementResults.Add(new ElementLiftResult
                    {
                        ElementId = element.ElementID,
                        Status = status,
                        Radius = radius,
                        RequiredCapacity = requiredCapacity
                    });
                }

                result.Positions.Add(posResult);
            }

            result.Statistics = stats;

            return result;
        }
    }
}