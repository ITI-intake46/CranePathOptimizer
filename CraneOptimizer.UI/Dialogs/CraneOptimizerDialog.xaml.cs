using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Analysis;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.Dialogs
{
    /// <summary>
    /// Crane Position Optimizer dialog.
    /// Left panel  : crane selection, GA settings, penalty weights, results summary.
    /// Right panel : tri-state element hierarchy tree.
    /// Bottom bar  : progress bar, Run / Cancel / Apply buttons.
    /// </summary>
    public partial class CraneOptimizerDialog : Window, INotifyPropertyChanged
    {
        // ── Inputs provided by MainWindow ──────────────────────────────────
        private readonly IReadOnlyList<IfcElementModel>  _allElements;
        private readonly IReadOnlyList<CraneInstance>    _cranes;
        private readonly IReadOnlyList<CraneZone>        _zones;

        // ── State ──────────────────────────────────────────────────────────
        private CancellationTokenSource _cts;
        private OptimizerResult         _lastResult;

        // ── Tree view model ────────────────────────────────────────────────
        public ObservableCollection<ElementHierarchyNode> RootNodes { get; } = new();

        // ── Result published back to MainWindow ────────────────────────────
        /// <summary>Non-null after a successful run and Apply click.</summary>
        public ApplyRequest ApplyResult { get; private set; }

        // ── Constructor ─────────────────────────────────────────────────────

        /// <summary>
        /// Create the dialog with live project data.
        /// </summary>
        public CraneOptimizerDialog(
            IReadOnlyList<IfcElementModel> elements,
            IReadOnlyList<CraneInstance>   cranes,
            IReadOnlyList<CraneZone>       zones)
        {
            _allElements = elements ?? new List<IfcElementModel>();
            _cranes      = cranes   ?? new List<CraneInstance>();
            _zones       = zones    ?? new List<CraneZone>();

            InitializeComponent();
            DataContext = this;

            PopulateCraneCombo();
            BuildElementTree();

            // Populate exclusion info for the initially-selected crane
            UpdateExclusionInfo(CboCrane.SelectedItem as CraneInstance);
        }

        // ── Initialisation ──────────────────────────────────────────────────

        private void PopulateCraneCombo()
        {
            CboCrane.ItemsSource = _cranes;
            if (_cranes.Count > 0)
                CboCrane.SelectedIndex = 0;
        }

        private void BuildElementTree()
        {
            RootNodes.Clear();

            // Group elements by IFC type (strip "Ifc" prefix for display)
            var groups = _allElements
                .GroupBy(e => CleanTypeName(e.Type))
                .OrderBy(g => g.Key);

            foreach (var g in groups)
            {
                double totalTonnes = g.Sum(e => (e.Weight ?? 0) / 1000.0);
                string weightSummary = totalTonnes > 0
                    ? $"{totalTonnes:F1} t total"
                    : "no weight data";
                var parent = new ElementHierarchyNode(
                    g.Key,
                    $"{g.Count()} items  •  {weightSummary}");

                foreach (var el in g.OrderBy(e => e.DisplayName ?? e.Name))
                    parent.AddChild(new ElementHierarchyNode(el));

                RootNodes.Add(parent);
            }
        }

        private static string CleanTypeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            var s = raw.Trim();
            if (s.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase) && s.Length > 3)
                s = s.Substring(3);
            return s;
        }

        // ── Crane selection ─────────────────────────────────────────────────

        private void CboCrane_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CboCrane.SelectedItem is CraneInstance crane)
            {
                int chartId = crane.Parameters.AssignedChartId;
                TxtChartStatus.Text = chartId >= 0
                    ? $"✔ Load chart assigned (ID {chartId})"
                    : "⚠ No load chart assigned — capacity checks will mark all Non-Liftable";
                TxtChartStatus.Foreground = new SolidColorBrush(
                    chartId >= 0
                        ? Color.FromRgb(0xA6, 0xE3, 0xA1)   // green
                        : Color.FromRgb(0xF3, 0x8B, 0xA8));  // red

                UpdateExclusionInfo(crane);
            }
        }

        // ── Quick-filter buttons ────────────────────────────────────────────

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) =>
            SetAllNodes(true);

        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) =>
            SetAllNodes(false);

        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) =>
            ExcludeCategory("Slab");

        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) =>
            ExcludeCategory("Stair");

        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) =>
            ExcludeCategory("Roof");

        private void SetAllNodes(bool checked_)
        {
            foreach (var n in RootNodes) n.IsChecked = checked_;
        }

        private void ExcludeCategory(string keyword)
        {
            foreach (var n in RootNodes)
                if (n.Label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    n.IsChecked = false;
        }

        // ── Run / Cancel / Apply ────────────────────────────────────────────

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (CboCrane.SelectedItem is not CraneInstance crane)
            {
                MessageBox.Show("Please select a crane.", "Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedElements = RootNodes
                .SelectMany(n => n.GetCheckedElements())
                .ToList();

            if (selectedElements.Count == 0)
            {
                MessageBox.Show("No elements selected. Include at least one element type.",
                    "Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = BuildSettings();

            // ── Load chart points ────────────────────────────────────────────
            List<CraneLoadChartPointEntity> chartPoints = null;
            int chartId = crane.Parameters.AssignedChartId;
            if (chartId >= 0)
            {
                try
                {
                    using var db = DatabaseService.Instance.CreateContext();
                    chartPoints = await db.CraneLoadChartPoints
                        .AsNoTracking()
                        .Where(p => p.ChartId == chartId)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load chart points:\n{ex.Message}",
                        "Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // ── Compute exclusion radius from the selected mode ────────────────
            double exclusionRadius = ComputeExclusionRadius(crane);
            UpdateExclusionInfo(crane);

            // ── Sample valid candidates ───────────────────────────────────────
            TxtStatus.Text = "Sampling allowed zones…";
            ProgBar.Value  = 0;

            List<System.Windows.Point> candidates = null;
            await Task.Run(() =>
            {
                candidates = AllowedZoneSampler.Sample(
                    _zones,
                    settings.GridStepM,
                    exclusionRadius);
            });

            if (candidates == null || candidates.Count == 0)
            {
                string hint = exclusionRadius > 0
                    ? $"If using exclusion radius ({exclusionRadius:F1} m), the zone may be too small to contain the disk. " +
                      "Try 'Ignore exclusion zone' or reduce the radius."
                    : "Make sure you have drawn at least one Allowed (green) zone.";
                MessageBox.Show(
                    "No valid crane positions found.\n\n" + hint,
                    "Optimizer — No Candidates",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtStatus.Text = "No valid positions found.";
                return;
            }

            TxtStatus.Text = $"Found {candidates.Count} candidate positions. Running GA…";

            // ── Run GA ───────────────────────────────────────────────────────
            BtnRun.IsEnabled    = false;
            BtnCancel.IsEnabled = true;
            BtnApply.IsEnabled  = false;
            PanelResults.Visibility = Visibility.Collapsed;
            _lastResult = null;

            _cts = new CancellationTokenSource();

            var progress = new Progress<OptimizerProgress>(p =>
            {
                ProgBar.Value  = p.GenerationPercent;
                TxtStatus.Text = $"Gen {p.Generation}/{settings.MaxGenerations}  ·  Best score: {p.BestScore:F0}" +
                                 $"  ({p.CountLiftable} Liftable / {p.CountNonLiftable} NL / {p.CountUnreachable} UR)";
            });

            OptimizerResult result = null;
            try
            {
                result = await Task.Run(() =>
                    new CranePositionGA().Run(
                        candidates, selectedElements, crane,
                        chartPoints ?? new List<CraneLoadChartPointEntity>(),
                        settings, progress, _cts.Token),
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Optimizer error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Error during optimization.";
            }
            finally
            {
                BtnRun.IsEnabled    = true;
                BtnCancel.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }

            if (result != null)
            {
                _lastResult = result;
                ShowResults(result);
                BtnApply.IsEnabled = true;
                TxtStatus.Text =
                    $"Done — {result.GenerationsRun} generations  ·  Best score: {result.BestScore:F0}";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            TxtStatus.Text = "Cancelling…";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null) return;
            if (CboCrane.SelectedItem is not CraneInstance crane) return;

            ApplyResult = new ApplyRequest(crane, _lastResult);
            DialogResult = true;
            Close();
        }

        // ── Settings builder ────────────────────────────────────────────────

        private OptimizerSettings BuildSettings()
        {
            static int    ParseInt(string s, int    def) => int.TryParse(s, out var v) ? v : def;
            static double ParseDbl(string s, double def) => double.TryParse(s, out var v) ? v : def;

            return new OptimizerSettings
            {
                PopulationSize          = Math.Max(5,  ParseInt(TxtPopulation.Text, 30)),
                MaxGenerations          = Math.Max(1,  ParseInt(TxtGenerations.Text, 30)),
                MutationRate            = Math.Clamp(ParseDbl(TxtMutRate.Text,   0.10), 0.01, 0.50),
                MutationSigma           = Math.Clamp(ParseDbl(TxtMutSigma.Text,  0.05), 0.01, 0.30),
                GridStepM               = Math.Max(0.5, ParseDbl(TxtGridStep.Text, 2.0)),
                ConvergenceThresholdPct = Math.Max(0.0, ParseDbl(TxtConverge.Text, 0.5)),
                PenaltyNonLiftable      = Math.Max(0,   ParseInt(TxtPenNL.Text, 20)),
                PenaltyUnreachable      = Math.Max(0,   ParseInt(TxtPenUR.Text, 100))
            };
        }

        // ── Exclusion zone helpers ──────────────────────────────────────────

        private void RbExclusion_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtExcInfo == null) return;   // called before InitializeComponent finishes
            UpdateExclusionInfo(CboCrane.SelectedItem as CraneInstance);
        }

        private void UpdateExclusionInfo(CraneInstance crane)
        {
            if (TxtExcInfo == null) return;
            if (RbExcIgnore?.IsChecked == true)
            {
                TxtExcInfo.Text = "Crane may be placed anywhere inside the Allowed zone with no keep-clear disk.";
                return;
            }
            if (crane == null) { TxtExcInfo.Text = ""; return; }

            double r = ComputeExclusionRadius(crane);
            TxtExcInfo.Text = r > 0
                ? $"Keep-clear disk radius: {r:F2} m"
                : "Exclusion radius = 0 m (no disk filter applied).";
        }

        private double ComputeExclusionRadius(CraneInstance crane)
        {
            if (RbExcIgnore?.IsChecked == true) return 0;

            if (RbExcDimensions?.IsChecked == true)
            {
                // Derive radius from physical crane dimensions
                if (crane.Parameters is MobileCraneParameters mob)
                {
                    // Half-diagonal of carrier footprint × 1.2 safety factor
                    double halfL = mob.CarrierLength / 2.0;
                    double halfW = mob.CarrierWidth  / 2.0;
                    return Math.Sqrt(halfL * halfL + halfW * halfW) * 1.2;
                }
                if (crane.Parameters is TowerCraneParameters tw)
                {
                    // Use twice the mast base radius as keep-clear disk
                    return tw.BaseRadius * 2.0;
                }
                // Fallback to standard radius
            }

            // Standard: use ExclusionRadiusM set by the user on the crane
            return crane.Parameters.ExclusionRadiusM;
        }

        // ── Results display ─────────────────────────────────────────────────

        private void ShowResults(OptimizerResult r)
        {
            PanelResults.Visibility = Visibility.Visible;

            TxtScore.Text    = $"Score: {r.BestScore:F0}";
            TxtPosition.Text = $"Optimal position  X={r.BestX:F1} m  Y={r.BestY:F1} m";
            TxtCountL.Text   = r.CountLiftable.ToString();
            TxtCountNL.Text  = r.CountNonLiftable.ToString();
            TxtCountUR.Text  = r.CountUnreachable.ToString();
            TxtCoverage.Text = $"{r.CoveragePct:F1}% of elements are Liftable";
            TxtStopReason.Text = r.StopReason;

            // Colour the score label by quality
            if (r.CoveragePct >= 95)
                TxtScore.Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));  // green
            else if (r.CoveragePct >= 70)
                TxtScore.Foreground = new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87));  // orange
            else
                TxtScore.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));  // red
        }

        // ── INotifyPropertyChanged ──────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ── ApplyRequest DTO ─────────────────────────────────────────────────────

    /// <summary>
    /// Passed back to MainWindow so it can move the crane and recolour elements.
    /// </summary>
    public sealed class ApplyRequest
    {
        public CraneInstance    Crane  { get; }
        public OptimizerResult  Result { get; }

        public ApplyRequest(CraneInstance crane, OptimizerResult result)
        {
            Crane  = crane;
            Result = result;
        }
    }
}
