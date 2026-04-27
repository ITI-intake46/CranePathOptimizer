using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        const string POINT_LAYER = "GA_SelectedPoint";
        const double CompactDialogWidth = 460;
        const double ExpandedDialogWidth = 780;
        const double AdvancedPanelWidth = 320;

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
        private bool _isAdvancedPanelOpen;
        private bool _cancelRequested;

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
            _cancelRequested = false;

            ClearResultPanel();
            _renderingService.ClearLayer(POINT_LAYER);

            BtnRun.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            BtnApply.IsEnabled = false;

            ProgBar.IsIndeterminate = true;
            TxtStatus.Text = "Running optimizer...";

            try
            {
                var settings = BuildSettings();
                var result = await _placementService.RunOptimizationAsync(settings, selectedCrane);

                if (_cancelRequested)
                {
                    LatestResult = null;
                    _currentResult = null;
                    TxtStatus.Text = "Optimization cancelled.";
                    ProgBar.IsIndeterminate = false;
                    ProgBar.Value = 0;
                    return;
                }

                LatestResult = result;
                _currentResult = result;

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
                BtnCancel.IsEnabled = false;
            }
        }

        private void PointsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PointsList.SelectedItem is not PointItem item)
                return;

            ShowPoint(item.Point);
            UpdateResultPanelForPoint(item.Point);
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

        private double CalculateMarkerRadius() => 200;
        private double CalculateMarkerZ(double r) => 500;

        private void BtnAdvancedToggle_Click(object sender, RoutedEventArgs e)
        {
            _isAdvancedPanelOpen = !_isAdvancedPanelOpen;

            AdvancedColumn.Width = _isAdvancedPanelOpen
                ? new GridLength(AdvancedPanelWidth)
                : new GridLength(0);

            PanelAdvanced.Visibility = Visibility.Visible;

            Width = _isAdvancedPanelOpen
                ? ExpandedDialogWidth
                : CompactDialogWidth;

            BtnAdvancedToggle.Content = _isAdvancedPanelOpen
                ? "Hide Advanced"
                : "Advanced Options";
        }

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void RbExclusion_Checked(object sender, RoutedEventArgs e) { }

        private void BtnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
                return;

            _cancelRequested = true;
            BtnCancel.IsEnabled = false;
            BtnApply.IsEnabled = false;
            TxtStatus.Text = "Cancel requested...";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null)
                return;

            LatestResult = _currentResult;
            DialogResult = true;
            Close();
        }

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }
    }
}
