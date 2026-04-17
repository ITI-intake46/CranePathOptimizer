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
using CraneOptimizer.UI.Core.Scene;
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

        public SceneViewModel Scene { get; }
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
            SceneViewModel scene,
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
            var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
            if (string.IsNullOrEmpty(path)) return;

            var elements = _ifcReader.ReadElements(path);
            Scene.SetElements(elements);
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