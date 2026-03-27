using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F7_Zones;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // =========================
        // Fields
        // =========================
        private readonly IIfcReader _ifcReader;
        private readonly IGeneticAlgorithmEngine _ga;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;
        private readonly IServiceScopeFactory _scopeFactory;

        private Crane? _selectedCrane;

        // =========================
        // Properties
        // =========================
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<Crane> Cranes { get; } = new();

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCraneSpec));
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
            IIfcReader ifcReader,
            IGeneticAlgorithmEngine ga,
            IRenderingService render,
            ICraneRepository craneRepo,
            IServiceScopeFactory scopeFactory,
            IDialogService dialog)
        {
            _scopeFactory = scopeFactory;
            Scene = scene;
            Drawing = drawing;
            _ifcReader = ifcReader;
            _ga = ga;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;

            // 🔥 CONNECT SCENE TO RENDERER
            Scene.ElementsChanged += elements => _render.RenderElements(elements);
            Scene.ZoneAdded += zone => _render.RenderZone(zone);

            LoadIfcCommand = new RelayCommand(LoadIfc);
            StartDrawingCommand = new RelayCommand(() => Drawing.Start());
            OptimizeCommand = new RelayCommand(RunGA);

            _ = LoadCranesAsync();
        }

        // =========================
        // Methods
        // =========================

        private async Task LoadCranesAsync()
        {
            // ✅ Scope منفصل — آمن تماماً مع async
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICraneRepository>();

            var cranes = await repo.GetAllAsync();

            // ✅ UI update على الـ dispatcher
            App.Current.Dispatcher.Invoke(() =>
            {
                Cranes.Clear();
                foreach (var c in cranes)
                    Cranes.Add(c);
            });
        }

        private void LoadIfc()
        {
            var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
            if (string.IsNullOrEmpty(path)) return;

            var elements = _ifcReader.ReadElements(path);

            // 🔥 ONLY THIS
            Scene.SetElements(elements);
        }

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

            var assignment = new Dictionary<int, List<string>>();

            var settings = new GASettings
            {
                PopulationSize = 30,
                MaxGenerations = 50,
                MutationRate = 0.1,
                MaxCranePositions = 1,
                EarlyStoppingGenerations = 10
            };

            var result = _ga.Run(
                Scene.Zones.ToList(),
                Scene.Elements.ToList(),
                assignment,
                SelectedCraneSpec,
                settings);

            var pos = result?.Chromosome?.CranePositions?.FirstOrDefault();

            if (pos == null)
            {
                _dialog.ShowMessage("No valid solution found");
                return;
            }

            _render.RenderBestPosition(pos.X, pos.Y);
        }
    }
}