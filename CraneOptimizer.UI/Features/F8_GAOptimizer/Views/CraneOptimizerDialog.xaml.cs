using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        private readonly ICranePlacementService _placementService;
        private readonly IRenderingService _renderingService;
        private readonly IDialogService _dialogService;
        private readonly ICraneRepository _craneRepository;
        private readonly ISceneService _sceneService;

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
            try
            {
                var cranes = await _craneRepository.GetAllWithSpecsAsync();
                CboCrane.ItemsSource = cranes;
                CboCrane.IsEnabled = false;

                if (cranes.Count > 0)
                    CboCrane.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Failed to load cranes.";
                _dialogService.ShowMessage($"Failed to load cranes: {ex.Message}");
            }
        }

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboCrane.SelectedItem is not Crane crane)
            {
                TxtChartStatus.Text = string.Empty;
                return;
            }

            CraneSpecification? firstSpec = null;
            bool hasLoadChart = false;

            foreach (var specification in crane.Specifications)
            {
                firstSpec ??= specification;

                if (specification.LoadChart != null && specification.LoadChart.Count > 0)
                {
                    hasLoadChart = true;
                    break;
                }
            }

            if (firstSpec == null)
            {
                TxtChartStatus.Text = "Specification missing";
                return;
            }

            TxtChartStatus.Text = hasLoadChart
                ? "Load chart assigned"
                : "Load chart missing";
        }

        private void RbExclusion_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtExcInfo == null)
                return;

            TxtExcInfo.Text = RbExcIgnore?.IsChecked == true
                ? "Crane overlap checks are currently ignored."
                : string.Empty;
        }

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            LatestResult = null;
            ClearResultPanel();
            _renderingService.ClearLayer("GA_BestPoint");

            BtnRun.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            BtnApply.IsEnabled = false;
            ProgBar.IsIndeterminate = true;
            ProgBar.Value = 0;
            TxtStatus.Text = "Running optimizer...";

            try
            {
                var settings = BuildSettings();
                var result = await _placementService.RunOptimizationAsync(settings);

                LatestResult = result;
                RenderBestPoint(result);
                UpdateResultPanel(result);
                //***************COLORING * ****************

                // 1) Get scene elements
                var elements = _sceneService.GetElements();
                if (elements == null || elements.Count == 0)
                    return;

                // 2) Build ElementId -> Status map from GA result
                var statusMap = result.Positions
                    .SelectMany(p => p.ElementResults)
                    .GroupBy(e => e.ElementId) // in case of duplicates across positions
                    .ToDictionary(g => g.Key, g => g.First().Status);

                // 3) Map each element to its LiftStatus (nullable)
                var elementsWithStatus = elements.Select(e =>
                {
                    statusMap.TryGetValue(e.ElementID, out var status);
                    return (e, (LiftStatus?)status);
                });

                // 4) Re-render elements layer with colors
                _renderingService.Render(
                    elementsWithStatus,
                    new CraneOptimizer.UI.Features.F2_IfcViewer.ElementVisualBuilder(),
                    "Elements"
                );

                //***************COLORING * ****************
                TxtStatus.Text = "Optimization completed.";
                BtnApply.IsEnabled = true;
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 100;
            }
            catch (InvalidOperationException ex)
            {
                TxtStatus.Text = "Ready";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;
                _dialogService.ShowMessage(ex.Message);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Optimization failed.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;
                _dialogService.ShowMessage($"Optimization failed: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                BtnRun.IsEnabled = true;
                BtnCancel.IsEnabled = false;
            }
        }

        private void BtnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                TxtStatus.Text = "Cancellation is not available during the active GA run.";
                return;
            }

            TxtStatus.Text = "Ready";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (LatestResult == null)
                return;

            DialogResult = true;
            Close();
        }

        private GASettings BuildSettings()
        {
            int populationSize = ParseInt(TxtPopulation.Text, 30, 1);
            int maxGenerations = ParseInt(TxtGenerations.Text, 50, 1);
            double mutationRate = ParseDouble(TxtMutRate.Text, 0.10, 0.0, 1.0);
            double nonLiftablePenalty = ParseDouble(TxtPenNL.Text, 10.0, 0.0, double.MaxValue);
            double unreachablePenalty = ParseDouble(TxtPenUR.Text, 3.0, 0.0, double.MaxValue);

            return new GASettings
            {
                PopulationSize = populationSize,
                MaxGenerations = maxGenerations,
                MutationRate = mutationRate,
                CrossoverRate = 0.7,
                MaxCranePositions = 1,
                EarlyStoppingGenerations = Math.Max(5, maxGenerations / 3),
                OverweightPenaltyWeight = nonLiftablePenalty,
                HeightPenaltyWeight = nonLiftablePenalty,
                RadiusPenaltyWeight = unreachablePenalty
            };
        }

        private void RenderBestPoint(CranePlacementResult result)
        {
            if (result.Positions.Count == 0)
                return;

            var bestPosition = result.Positions[0];
            var markerRadius = CalculateMarkerRadius();
            var markerZ = CalculateMarkerZ(markerRadius);

            Console.WriteLine(
                $"GA_BestPoint Render -> X: {bestPosition.X:F2}, Y: {bestPosition.Y:F2}, Z: {markerZ:F2}, Radius: {markerRadius:F2}");

            _renderingService.ClearLayer("GA_BestPoint");
            _renderingService.Render(
                new[] { (bestPosition.X, bestPosition.Y, markerZ) },
                new BestPointVisualBuilder(markerRadius),
                "GA_BestPoint");
        }

        private double CalculateMarkerRadius()
        {
            var elements = _sceneService.GetElements();
            if (elements == null || elements.Count == 0)
                return 200.0;

            bool initialized = false;
            double minX = 0;
            double minY = 0;
            double minZ = 0;
            double maxX = 0;
            double maxY = 0;
            double maxZ = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];

                double halfLength = (element.BBoxLength ?? 0) * 0.5;
                double halfWidth = (element.BBoxWidth ?? 0) * 0.5;
                double halfHeight = (element.BBoxHeight ?? 0) * 0.5;

                double elementMinX = element.ElementCOG.X - halfLength;
                double elementMaxX = element.ElementCOG.X + halfLength;
                double elementMinY = element.ElementCOG.Y - halfWidth;
                double elementMaxY = element.ElementCOG.Y + halfWidth;
                double elementMinZ = element.ElementCOG.Z - halfHeight;
                double elementMaxZ = element.ElementCOG.Z + halfHeight;

                if (!initialized)
                {
                    minX = elementMinX;
                    maxX = elementMaxX;
                    minY = elementMinY;
                    maxY = elementMaxY;
                    minZ = elementMinZ;
                    maxZ = elementMaxZ;
                    initialized = true;
                    continue;
                }

                if (elementMinX < minX) minX = elementMinX;
                if (elementMaxX > maxX) maxX = elementMaxX;
                if (elementMinY < minY) minY = elementMinY;
                if (elementMaxY > maxY) maxY = elementMaxY;
                if (elementMinZ < minZ) minZ = elementMinZ;
                if (elementMaxZ > maxZ) maxZ = elementMaxZ;
            }

            double sizeX = maxX - minX;
            double sizeY = maxY - minY;
            double sizeZ = maxZ - minZ;
            double sceneSize = Math.Max(sizeX, Math.Max(sizeY, sizeZ));

            if (sceneSize <= 0)
                return 200.0;

            var adaptiveRadius = sceneSize * 0.005;
            return Math.Clamp(adaptiveRadius, 150.0, 400.0);
        }

        private double CalculateMarkerZ(double markerRadius)
        {
            var elements = _sceneService.GetElements();
            if (elements == null || elements.Count == 0)
                return 500.0;

            double minBaseZ = double.MaxValue;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                double halfHeight = (element.BBoxHeight ?? 0) * 0.5;
                double baseZ = element.ElementCOG.Z - halfHeight;

                if (baseZ < minBaseZ)
                    minBaseZ = baseZ;
            }

            if (double.IsInfinity(minBaseZ) || double.IsNaN(minBaseZ) || minBaseZ == double.MaxValue)
                minBaseZ = 0;

            return minBaseZ + Math.Max(500.0, markerRadius * 2.0);
        }

        private void UpdateResultPanel(CranePlacementResult result)
        {
            if (result.Positions.Count == 0)
                return;

            int total = result.Statistics.Liftable
                + result.Statistics.OutOfRadius
                + result.Statistics.Overweight
                + result.Statistics.HeightFail
                + result.Statistics.SpecialCase;

            int nonLiftable = total - result.Statistics.Liftable;
            var bestPosition = result.Positions[0];

            TxtScore.Text = result.FitnessScore.ToString("F2", CultureInfo.InvariantCulture);
            TxtCountL.Text = result.Statistics.Liftable.ToString(CultureInfo.InvariantCulture);
            TxtCountNL.Text = nonLiftable.ToString(CultureInfo.InvariantCulture);
            TxtCountUR.Text = result.Statistics.OutOfRadius.ToString(CultureInfo.InvariantCulture);
            TxtCoverage.Text = $"Coverage: {result.CoverageRatio:P1}";

            if (result.Positions.Count == 1)
            {
                TxtPosition.Text = $"X = {bestPosition.X:F0} mm, Y = {bestPosition.Y:F0} mm";
            }
            else
            {
                TxtPosition.Text = $"Displaying position 1 of {result.Positions.Count}: X = {bestPosition.X:F0} mm, Y = {bestPosition.Y:F0} mm";
            }

            TxtStopReason.Text = $"Evaluated {total} elements using the first crane available in the database.";
            PanelResults.Visibility = Visibility.Visible;
        }

        private void ClearResultPanel()
        {
            PanelResults.Visibility = Visibility.Collapsed;
            TxtScore.Text = string.Empty;
            TxtPosition.Text = string.Empty;
            TxtCountL.Text = "0";
            TxtCountNL.Text = "0";
            TxtCountUR.Text = "0";
            TxtCoverage.Text = string.Empty;
            TxtStopReason.Text = string.Empty;
        }

        private static int ParseInt(string? text, int fallback, int minimumValue)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                value = fallback;

            return Math.Max(minimumValue, value);
        }

        private static double ParseDouble(string? text, double fallback, double minValue, double maxValue)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                value = fallback;

            if (value < minValue)
                return minValue;

            if (value > maxValue)
                return maxValue;

            return value;
        }
    }
}
