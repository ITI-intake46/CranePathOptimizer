using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        const string POINT_LAYER = "GA_SelectedPoint";
        private class PointItem
        {
            public CranePositionResult Point { get; set; }

            public string DisplayText =>
                $"Point ({Point.X:F0}, {Point.Y:F0}) - {Point.ElementResults.Count} elements";
        }

        private readonly ICranePlacementService _placementService;
        private readonly IRenderingService _renderingService;
        private readonly IDialogService _dialogService;
        private readonly ICraneRepository _craneRepository;
        private readonly ISceneService _sceneService;

        private CranePlacementResult? _currentResult;
        private bool _isRunning;

        public CranePlacementResult? LatestResult { get; private set; }

        public CraneOptimizerDialog(
            ICranePlacementService placementService,
            IRenderingService renderingService,
            IDialogService dialogService,
            ICraneRepository craneRepository,
            ISceneService sceneService)
        {
            InitializeComponent();

            _placementService = placementService;
            _renderingService = renderingService;
            _dialogService = dialogService;
            _craneRepository = craneRepository;
            _sceneService = sceneService;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadCranesAsync();
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepository.GetAllWithSpecsAsync();
            CboCrane.ItemsSource = cranes;
        }

        // ============================
        // RUN GA (FULLY FIXED)
        // ============================
        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            var selectedCrane = CboCrane.SelectedItem as Crane;

            if (selectedCrane == null)
            {
                _dialogService.ShowMessage("Please select a crane.");
                return;
            }

            _isRunning = true;

            ClearResultPanel();
            _renderingService.ClearLayer("GA_SelectedPoint");

            BtnRun.IsEnabled = false;
            BtnApply.IsEnabled = false;

            ProgBar.IsIndeterminate = true;
            TxtStatus.Text = "Running optimizer...";

            try
            {
                var settings = BuildSettings();

                //  FIX: pass crane to service
                var result = await _placementService.RunOptimizationAsync(settings, selectedCrane);

                LatestResult = result;
                _currentResult = result;

                // Bind points
                PointsList.ItemsSource = result.Positions
                    .Select(p => new PointItem { Point = p })
                    .ToList();

                if (PointsList.Items.Count > 0)
                    PointsList.SelectedIndex = 0;

                UpdateResultPanel(result);

                TxtStatus.Text = "Optimization completed.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 100;

                BtnApply.IsEnabled = true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Optimization failed.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;

                _dialogService.ShowMessage(ex.Message);
            }
            finally
            {
                _isRunning = false;
                BtnRun.IsEnabled = true;
            }
        }

        // ============================
        // POINT SELECTION (FULLY LINKED)
        // ============================
        private void PointsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PointsList.SelectedItem is not PointItem item)
                return;

            ShowPoint(item.Point);
            UpdateResultPanelForPoint(item.Point); //  FIX: UI sync
        }

        private void ShowPoint(CranePositionResult point)
        {
            if (point == null)
                return;

            _renderingService.ClearLayer(POINT_LAYER);

            var markerRadius = CalculateMarkerRadius();
            var markerZ = CalculateMarkerZ(markerRadius);

            _renderingService.Render(
                new[] { (point.X, point.Y, markerZ) },
                new BestPointVisualBuilder(markerRadius),
                POINT_LAYER
            );

            var elements = _sceneService.GetElements();
            if (elements == null) return;

            var statusMap = point.ElementResults
                .ToDictionary(e => e.ElementId, e => e.Status);

            var elementsWithStatus = elements.Select(e =>
            {
                if (statusMap.TryGetValue(e.ElementID, out var status))
                {
                    return (e, (LiftStatus?)status);
                }
                else
                {
                    return (e, null);
                }
            });

            _renderingService.Render(
                elementsWithStatus,
                new ElementVisualBuilder(),
                "Elements"
            );
        }

        // ============================
        // SETTINGS (FIXED)
        // ============================
        private GASettings BuildSettings()
        {
            if (CboCrane.SelectedItem == null)
                throw new InvalidOperationException("Please select a crane.");

            return new GASettings
            {
                PopulationSize = int.TryParse(TxtPopulation.Text, out var p) ? p : 30,
                MaxGenerations = int.TryParse(TxtGenerations.Text, out var g) ? g : 30,
                MutationRate = double.TryParse(TxtMutRate.Text, out var m) ? m : 0.1,
                RandomSeed = 42
            };
        }

        // ============================
        // RESULT PANEL (FULLY FIXED)
        // ============================
        private void UpdateResultPanel(CranePlacementResult result)
        {
            PanelResults.Visibility = Visibility.Visible;

            TxtScore.Text = $"Fitness: {result.FitnessScore:F2}";
            TxtCoverage.Text = $"Total Coverage: {result.CoverageRatio:P0}";

            if (result.Positions.Any())
                UpdateResultPanelForPoint(result.Positions[0]);
        }

        private void UpdateResultPanelForPoint(CranePositionResult point)
        {
            TxtPosition.Text = $"({point.X:F0}, {point.Y:F0})";

            int liftable = point.ElementResults.Count(e => e.Status == LiftStatus.Liftable);
            int nonLiftable = point.ElementResults.Count(e => e.Status == LiftStatus.Overweight);
            int unreachable = point.ElementResults.Count(e => e.Status == LiftStatus.OutOfRadius);

            TxtCountL.Text = liftable.ToString();
            TxtCountNL.Text = nonLiftable.ToString();
            TxtCountUR.Text = unreachable.ToString();

            int total = point.ElementResults.Count;
            double coverage = total == 0 ? 0 : (double)liftable / total;

            TxtCoverage.Text = $"Point Coverage: {coverage:P0}";
        }

        private void ClearResultPanel()
        {
            PanelResults.Visibility = Visibility.Collapsed;
            PointsList.ItemsSource = null;
        }

        // ============================
        // HELPERS
        // ============================
        private double CalculateMarkerRadius() => 200;
        private double CalculateMarkerZ(double r) => 500;

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void RbExclusion_Checked(object sender, RoutedEventArgs e) { }
        private void BtnCancelRun_Click(object sender, RoutedEventArgs e) { }
        private void BtnApply_Click(object sender, RoutedEventArgs e) { }
        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }
    }
}
