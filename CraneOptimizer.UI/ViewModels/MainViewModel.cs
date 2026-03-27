using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Commands;
using CraneOptimizer.UI.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.ViewModels
{
    /// <summary>
    /// Root ViewModel.
    /// Coordinates UI actions.
    /// </summary>
    public class MainViewModel
    {
        private readonly IIfcReader _ifcReader;
        public ObservableCollection<PrecastElement> Elements { get; set; } = new();
        private readonly IGeneticAlgorithmEngine _ga;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        public ObservableCollection<Crane> Cranes { get; set; } = new();

        public Crane? SelectedCrane { get; set; }
        public ICommand OptimizeCommand { get; }
        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications.FirstOrDefault();
        public SceneViewModel Scene { get; }
        public DrawingViewModel Drawing { get; }

        public ICommand LoadIfcCommand { get; }
        public ICommand StartDrawingCommand { get; }

        public MainViewModel(
            SceneViewModel scene,
            DrawingViewModel drawing,
            IIfcReader ifcReader,
            IGeneticAlgorithmEngine ga,
            IRenderingService render,
            ICraneRepository craneRepo)
        {
            Scene = scene;
            Drawing = drawing;
            _ifcReader = ifcReader;
            _ga = ga;
            _render = render;
            _craneRepo = craneRepo;
            LoadIfcCommand = new RelayCommand(LoadIfc);
            StartDrawingCommand = new RelayCommand(() => Drawing.Start());
            OptimizeCommand = new RelayCommand(RunGA);
            LoadCranes();
        }
        private void RunGA()
        {
            MessageBox.Show(Scene.Zones.Count.ToString());
            if (SelectedCraneSpec == null)
            {
                MessageBox.Show("No Crane Selected");
                return;
            }
            MessageBox.Show(
    $"Zones: {Scene.Zones.Count}\n" +
    $"Elements: {Scene.Elements.Count}\n" +
    $"Crane: {(SelectedCraneSpec != null)}"
);
            if (!Scene.Zones.Any() || !Scene.Elements.Any())
                return;

            if (SelectedCraneSpec == null)
                return;

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
                Scene.Zones,
                Scene.Elements,
                assignment,
                SelectedCraneSpec,
                settings);

            // 👇 أهم سطر
            var pos = result.Chromosome.CranePositions.First();

            _render.RenderBestPosition(pos.X, pos.Y);
        }
        private async void LoadCranes()
        {
            var cranes = await _craneRepo.GetAllAsync();

            Cranes.Clear();
            foreach (var c in cranes)
                Cranes.Add(c);
        }
        private void LoadIfc()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "IFC (*.ifc)|*.ifc"
            };

            if (dialog.ShowDialog() == true)
            {
                var elements = _ifcReader.ReadElements(dialog.FileName);
                Scene.SetElements(elements);

                Elements.Clear();
                foreach (var e in elements)
                    Elements.Add(e);
            }
        }
    }
}