using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Dialogs;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;

using CraneInstance = WPF_IFC_test1.Utilities.CraneInstance;

namespace WPF_IFC_test1.Main_Application_UserControl
{
    public partial class CraneManagerPanel : UserControl
    {
        // ── Events forwarded to MainWindow ──────────────────────────────────
        public event RoutedEventHandler AddTowerCraneRequested;
        public event RoutedEventHandler AddMobileCraneRequested;
        public event RoutedEventHandler RemoveSelectedRequested;
        public event RoutedEventHandler RemoveAllRequested;
        public event RoutedEventHandler CloseRequested;

        private CraneManager Manager => DataContext as CraneManager;

        // Which view is currently showing
        private enum PanelView { List, Params }
        private PanelView _currentView = PanelView.List;

        // ── Saved chart option (for the Load Chart combo) ────────────────────
        private record ChartOption(
            int    Id,
            double MaxCapTonne,
            double MinRadiusM,
            double MaxRadiusM,
            double MinBoomM,
            double MaxBoomM,
            double OutriggerSpanM,    // pad-to-pad W (metres)
            double SpanToSpanM,       // front-to-rear B (metres)
            string OutriggerConfig,   // "Full" / "75%" / "50%" / "On Rubber"
            string Derating,          // e.g. "100%"
            double TotalLengthM,      // carrier length from chart (metres)
            double TransportWidthM,   // travel width incl. tires from chart (metres)
            double BoundingRadiusM);  // min exclusion zone from chart (metres)

        // Prevents feedback loop when radius slider drives boom angle
        private bool _updatingRadius = false;

        // Currently selected chart IDs (for Edit/Delete buttons)
        private int _selectedTowerChartId = -1;
        private int _selectedMobileChartId = -1;

        public CraneManagerPanel()
        {
            InitializeComponent();

            // UnitSystem.Changed: force WPF to re-evaluate all converter bindings.
            // Toggling DataContext null→value flushes the binding cache so every
            // {Binding ..., Converter={StaticResource UnitFmt}} re-runs.
            UnitSystem.Changed += () => Dispatcher.Invoke(() =>
            {
                var dc = DataContext;
                DataContext = null;
                DataContext = dc;
            });
        }

        // ── View switching ───────────────────────────────────────────────────

        private void ShowListView()
        {
            _currentView = PanelView.List;

            ViewList.Visibility = Visibility.Visible;
            ViewParams.Visibility = Visibility.Collapsed;

            BackBtn.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Visible;
            HeaderTitle.Text = "Crane Manager";
        }

        private void ShowParamsView(CraneInstance crane)
        {
            if (crane == null) return;

            _currentView = PanelView.Params;

            ViewList.Visibility = Visibility.Collapsed;
            ViewParams.Visibility = Visibility.Visible;

            BackBtn.Visibility = Visibility.Visible;
            CountBadge.Visibility = Visibility.Collapsed;
            HeaderTitle.Text = crane.Name;

            // Show the right parameter set
            bool isTower = crane.Type == CraneType.TowerCrane;
            TowerCraneControls.Visibility = isTower ? Visibility.Visible : Visibility.Collapsed;
            MobileCraneControls.Visibility = isTower ? Visibility.Collapsed : Visibility.Visible;

            // Load saved charts from DB for this crane type (fire-and-forget)
            _ = LoadChartOptionsAsync(isTower);
        }

        // ── Load Chart ComboBox population ───────────────────────────────────

        private async Task LoadChartOptionsAsync(bool isTower)
        {
            var combo = isTower ? CboTowerCraneChart : CboMobileCraneChart;

            // Reset to default first
            combo.SelectionChanged -= (isTower ? (SelectionChangedEventHandler)CboTowerCraneChart_Changed
                                                : CboMobileCraneChart_Changed);
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "— Default (no chart)", Tag = (ChartOption?)null });

            try
            {
                using var db = DatabaseService.Instance.CreateContext();
                string category = isTower ? "Tower" : "Mobile";

                var charts = await db.CraneLoadCharts
                    .Include(c => c.Points)
                    .Where(c => c.CraneCategory == category)
                    .OrderByDescending(c => c.ImportedAt)
                    .ToListAsync();

                foreach (var chart in charts)
                {
                    if (!chart.Points.Any()) continue;

                    double maxCap    = chart.Points
                        .Where(p => p.CapacityTonne.HasValue)
                        .Select(p => p.CapacityTonne!.Value)
                        .DefaultIfEmpty(0).Max();
                    double minRadius = chart.Points.Min(p => p.RadiusM);
                    double maxRadius = chart.Points.Max(p => p.RadiusM);
                    double minBoom   = chart.Points.Min(p => p.BoomLengthM);
                    double maxBoom   = chart.Points.Max(p => p.BoomLengthM);
                    double outW        = chart.OutriggerSpanMm    / 1000.0;
                    double outB        = chart.SpanToSpanMm       / 1000.0;
                    double totalLenM   = chart.TotalLengthMm      / 1000.0;
                    double travelWidM  = chart.TransportWidthMm   / 1000.0;
                    double boundingR   = chart.BoundingRadiusM;
                    string outCfg      = string.IsNullOrEmpty(chart.OutriggerConfig) ? "Full" : chart.OutriggerConfig;
                    string derating    = $"{chart.DeratingPct}%";

                    string label = string.IsNullOrWhiteSpace(chart.CraneModel)
                        ? $"Chart #{chart.Id}"
                        : chart.CraneModel;

                    // Append outrigger config to label if not 100%
                    if (outCfg != "Full")
                        label += $" ({outCfg})";

                    combo.Items.Add(new ComboBoxItem
                    {
                        Content = label,
                        Tag     = new ChartOption(chart.Id, maxCap, minRadius, maxRadius,
                                                  minBoom, maxBoom, outW, outB, outCfg, derating,
                                                  totalLenM, travelWidM, boundingR)
                    });
                }
            }
            catch { /* DB not ready — silently skip */ }

            combo.SelectedIndex = 0;
            combo.SelectionChanged += (isTower ? (SelectionChangedEventHandler)CboTowerCraneChart_Changed
                                                : CboMobileCraneChart_Changed);
        }

        // ── Load Chart selection handlers ────────────────────────────────────

        private void CboMobileCraneChart_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Guard: elements below the combo in XAML may not exist yet during InitializeComponent
            if (MobileBoomSlider == null || MobileChartInfoBox == null) return;
            if (CboMobileCraneChart.SelectedItem is not ComboBoxItem item) return;

            if (item.Tag is not ChartOption opt)
            {
                // Reset to default generic range
                MobileBoomSlider.Minimum = 15;
                MobileBoomSlider.Maximum = 100;
                MobileRadiusSlider.Minimum = 0;
                MobileRadiusSlider.Maximum = 100;
                MobileChartInfoBox.Visibility = Visibility.Collapsed;
                BtnSwapMobile.Visibility = Visibility.Collapsed;
                _selectedMobileChartId = -1;
                if (BtnEditMobileChart != null) BtnEditMobileChart.IsEnabled = false;
                if (BtnDeleteMobileChart != null) BtnDeleteMobileChart.IsEnabled = false;
                return;
            }

            // Constrain boom-length slider to this chart's actual range
            MobileBoomSlider.Minimum = opt.MinBoomM;
            MobileBoomSlider.Maximum = opt.MaxBoomM;

            // Constrain working-radius slider to chart's radius range
            MobileRadiusSlider.Minimum = opt.MinRadiusM;
            MobileRadiusSlider.Maximum = opt.MaxRadiusM;

            // Apply chart dimensions to the crane 3D model
            if (Manager?.SelectedCrane?.Parameters is MobileCraneParameters mp)
            {
                mp.OutriggerSpanM = opt.OutriggerSpanM > 0 ? opt.OutriggerSpanM : 7.0;
                mp.SpanToSpanM    = opt.SpanToSpanM    > 0 ? opt.SpanToSpanM    : 7.0;

                // Carrier length = real transport length from chart
                if (opt.TotalLengthM > 0)
                    mp.CarrierLength = opt.TotalLengthM;

                // Carrier visual width = total travel width including tires from chart
                if (opt.TransportWidthM > 0)
                    mp.TravelWidthM = opt.TransportWidthM;

                // Exclusion zone minimum = bounding radius from chart
                double minExclusion = opt.BoundingRadiusM > 0 ? opt.BoundingRadiusM : 0;
                MobileExclusionSlider.Minimum = minExclusion;
                if (mp.ExclusionRadiusM < minExclusion)
                    mp.ExclusionRadiusM = minExclusion;
            }

            // Populate expanded info strip
            TxtMobileMaxCap.Text       = $"{opt.MaxCapTonne:F1} t";
            TxtMobileRadiusRange.Text  = $"{opt.MinRadiusM:F1} – {opt.MaxRadiusM:F1} m";
            TxtMobileBoomRange.Text    = $"{opt.MinBoomM:F0} – {opt.MaxBoomM:F0} m";
            TxtMobileOutriggerCfg.Text = opt.OutriggerConfig;
            TxtMobileDerating.Text     = opt.Derating;
            MobileChartInfoBox.Visibility = Visibility.Visible;

            // Show swap button only when outrigger dims are actually from the chart
            BtnSwapMobile.Visibility = (opt.OutriggerSpanM > 0 || opt.SpanToSpanM > 0)
                ? Visibility.Visible : Visibility.Collapsed;

            // Enable Edit/Delete chart buttons
            _selectedMobileChartId = opt.Id;
            if (BtnEditMobileChart != null) BtnEditMobileChart.IsEnabled = true;
            if (BtnDeleteMobileChart != null) BtnDeleteMobileChart.IsEnabled = true;

            // Persist chart ID on the crane parameters for the optimizer
            if (Manager?.SelectedCrane?.Parameters != null)
                Manager.SelectedCrane.Parameters.AssignedChartId = opt.Id;
        }

        private void CboTowerCraneChart_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Guard: elements below the combo in XAML may not exist yet during InitializeComponent
            if (TowerBoomSlider == null || TowerChartInfoBox == null) return;
            if (CboTowerCraneChart.SelectedItem is not ComboBoxItem item) return;

            if (item.Tag is not ChartOption opt)
            {
                // Reset to default generic range
                TowerBoomSlider.Minimum = 20;
                TowerBoomSlider.Maximum = 80;
                TowerChartInfoBox.Visibility = Visibility.Collapsed;
                _selectedTowerChartId = -1;
                if (BtnEditTowerChart != null) BtnEditTowerChart.IsEnabled = false;
                if (BtnDeleteTowerChart != null) BtnDeleteTowerChart.IsEnabled = false;
                return;
            }

            // Constrain jib-length slider to this chart's actual range
            TowerBoomSlider.Minimum = opt.MinBoomM;
            TowerBoomSlider.Maximum = opt.MaxBoomM;

            // Populate expanded info strip
            TxtTowerMaxCap.Text       = $"{opt.MaxCapTonne:F1} t";
            TxtTowerRadiusRange.Text  = $"{opt.MinRadiusM:F1} – {opt.MaxRadiusM:F1} m";
            TxtTowerBoomRange.Text    = $"{opt.MinBoomM:F0} – {opt.MaxBoomM:F0} m";
            TxtTowerOutriggerCfg.Text = opt.OutriggerConfig;
            TxtTowerDerating.Text     = opt.Derating;
            TowerChartInfoBox.Visibility = Visibility.Visible;

            // Enable Edit/Delete chart buttons
            _selectedTowerChartId = opt.Id;
            if (BtnEditTowerChart != null) BtnEditTowerChart.IsEnabled = true;
            if (BtnDeleteTowerChart != null) BtnDeleteTowerChart.IsEnabled = true;

            // Persist chart ID on the crane parameters for the optimizer
            if (Manager?.SelectedCrane?.Parameters != null)
                Manager.SelectedCrane.Parameters.AssignedChartId = opt.Id;
        }

        // ── Public API (called from MainWindow) ──────────────────────────────

        /// <summary>
        /// Called when a crane is selected from outside (e.g. drag-select on viewport).
        /// Switches to params view automatically.
        /// </summary>
        public void RefreshParameterPanel(CraneInstance crane)
        {
            if (crane == null) { ShowListView(); return; }
            ShowParamsView(crane);
        }

        // ── Header buttons ───────────────────────────────────────────────────

        private void BackBtn_Click(object sender, RoutedEventArgs e)
            => ShowListView();

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => CloseRequested?.Invoke(this, e);

        // ── List view – add buttons ──────────────────────────────────────────

        private void AddTower_Click(object sender, RoutedEventArgs e)
            => AddTowerCraneRequested?.Invoke(this, e);

        private void AddMobile_Click(object sender, RoutedEventArgs e)
            => AddMobileCraneRequested?.Invoke(this, e);

        // ── List view – crane item click → go to params ──────────────────────

        private void CraneItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is CraneInstance crane && Manager != null)
            {
                Manager.SelectedCrane = crane;
                ShowParamsView(crane);
            }
        }

        // ── List view – delete button ────────────────────────────────────────

        private void DeleteCraneBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CraneInstance crane && Manager != null)
            {
                Manager.SelectedCrane = crane;
                RemoveSelectedRequested?.Invoke(this, e);

                // If we deleted the last crane, stay on list; otherwise stay on list too
                ShowListView();
            }
        }

        // ── Params view – rename ─────────────────────────────────────────────

        private void InlineRename_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                InlineRenameBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void ApplyRename_Click(object sender, RoutedEventArgs e)
        {
            InlineRenameBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            // Update the header title with the new name
            if (Manager?.SelectedCrane != null)
                HeaderTitle.Text = Manager.SelectedCrane.Name;
        }

        // ── Params view – remove ─────────────────────────────────────────────

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelectedRequested?.Invoke(this, e);
            ShowListView();
        }

        // ── Swap outrigger W ↔ L ─────────────────────────────────────────────
        // Same physical crane, just rotated 90° — like re-orienting a rectangular column

        private void BtnSwapMobile_Click(object sender, RoutedEventArgs e)
        {
            if (Manager?.SelectedCrane?.Parameters is not MobileCraneParameters mp) return;

            // Swap pad-to-pad W ↔ front-to-rear B in memory
            (mp.OutriggerSpanM, mp.SpanToSpanM) = (mp.SpanToSpanM, mp.OutriggerSpanM);

            // Rotate the entire crane body 90° so the 3D visual matches the new orientation.
            // BodyAngle is applied to the whole group (carrier + outriggers + superstructure),
            // while SlewAngle continues to slew the superstructure relative to the (rotated) body.
            mp.BodyAngle = (mp.BodyAngle + 90) % 360;
            // 3D model rebuilds automatically via PropertyChanged
        }

        // ── Working Radius slider — back-calculates BoomAngle via arccos ─────

        private void MobileRadiusSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingRadius) return;
            if (MobileRadiusSlider == null || TxtMobileRadius == null) return;
            if (Manager?.SelectedCrane?.Parameters is not MobileCraneParameters mp) return;

            double r = e.NewValue;

            // Clamp to valid range so arccos won't NaN
            double boomLen = mp.BoomLength;
            if (boomLen <= 0) return;
            r = Math.Max(0.0, Math.Min(r, boomLen));

            double angleDeg = Math.Acos(r / boomLen) * 180.0 / Math.PI;

            _updatingRadius = true;
            mp.BoomAngle = angleDeg;
            TxtMobileRadius.Text = $"{r:F1} m";
            _updatingRadius = false;
        }

        // ── Feasibility Checker ──────────────────────────────────────────────────────

        private async void BtnMobileCheck_Click(object sender, RoutedEventArgs e)
            => await RunFeasibilityCheck(false);

        private async void BtnTowerCheck_Click(object sender, RoutedEventArgs e)
            => await RunFeasibilityCheck(true);

        private async Task RunFeasibilityCheck(bool isTower)
        {
            var combo        = isTower ? CboTowerCraneChart    : CboMobileCraneChart;
            var txtWeight    = isTower ? TxtTowerWeight         : TxtMobileWeight;
            var txtRadius    = isTower ? TxtTowerCheckRadius    : TxtMobileCheckRadius;
            var txtBoom      = isTower ? TxtTowerCheckBoom      : TxtMobileCheckBoom;
            var resultBorder = isTower ? TowerFeasibilityResult : MobileFeasibilityResult;
            var statusTxt    = isTower ? TxtTowerFeasibilityStatus : TxtMobileFeasibilityStatus;
            var detailTxt    = isTower ? TxtTowerFeasibilityDetail : TxtMobileFeasibilityDetail;

            if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not ChartOption opt)
            {
                ShowFeasibilityResult(resultBorder, statusTxt, detailTxt,
                    "No chart assigned", "Assign a load chart first", "#4A4C78", "#1A1B2D");
                return;
            }

            if (!double.TryParse(txtWeight.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double weightT) || weightT <= 0
                || !double.TryParse(txtRadius.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double radius) || radius <= 0
                || !double.TryParse(txtBoom.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double boom) || boom <= 0)
            {
                ShowFeasibilityResult(resultBorder, statusTxt, detailTxt,
                    "Invalid inputs", "Enter valid weight, radius and boom", "#9A7A3A", "#1A1608");
                return;
            }

            try
            {
                List<WPF_IFC_test1.Database.Entities.CraneLoadChartPointEntity> points;
                using (var db = WPF_IFC_test1.Database.DatabaseService.Instance.CreateContext())
                {
                    points = await db.CraneLoadChartPoints
                        .Where(p => p.ChartId == opt.Id && p.CapacityTonne.HasValue)
                        .ToListAsync();
                }

                double? cap = WPF_IFC_test1.Database.Services.CraneLoadChartLookup.GetCapacity(points, radius, boom);

                if (!cap.HasValue || cap.Value <= 0)
                {
                    ShowFeasibilityResult(resultBorder, statusTxt, detailTxt,
                        "Not Rated", $"No capacity data at R={radius:F1}m / Boom={boom:F0}m", "#6B6D9A", "#10111E");
                    return;
                }

                double utilPct = weightT / cap.Value * 100.0;
                var status = WPF_IFC_test1.Database.Services.CraneLoadChartLookup.Classify(utilPct);

                (string label, string detail, string fg, string bg) = status switch
                {
                    WPF_IFC_test1.Database.Services.CraneLoadChartLookup.FeasibilityStatus.Safe =>
                        ($"✓ SAFE  {utilPct:F1}%", $"Cap: {cap.Value:F1} t  |  Load: {weightT:F1} t", "#27AE60", "#0A1E0A"),
                    WPF_IFC_test1.Database.Services.CraneLoadChartLookup.FeasibilityStatus.Marginal =>
                        ($"⚠ MARGINAL  {utilPct:F1}%", $"Cap: {cap.Value:F1} t  |  Load: {weightT:F1} t", "#D4A820", "#1A1508"),
                    WPF_IFC_test1.Database.Services.CraneLoadChartLookup.FeasibilityStatus.OverRated =>
                        ($"✗ OVER-RATED  {utilPct:F1}%", $"Cap: {cap.Value:F1} t  |  Load: {weightT:F1} t  (UNSAFE)", "#E05A60", "#1E0A0A"),
                    _ =>
                        ($"✗ OVER-RATED  {utilPct:F1}%", $"Cap: {cap.Value:F1} t  |  Load: {weightT:F1} t  (UNSAFE)", "#E05A60", "#1E0A0A")
                };

                ShowFeasibilityResult(resultBorder, statusTxt, detailTxt, label, detail, fg, bg);
            }
            catch (Exception ex)
            {
                ShowFeasibilityResult(resultBorder, statusTxt, detailTxt,
                    "Error", ex.Message, "#6B6D9A", "#10111E");
            }
        }

        private static void ShowFeasibilityResult(Border border, TextBlock status, TextBlock detail,
            string statusText, string detailText, string fgHex, string bgHex)
        {
            var fg = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFrom(fgHex)!;
            var bg = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFrom(bgHex)!;
            border.Background  = bg;
            border.BorderBrush = fg;
            status.Text        = statusText;
            status.Foreground  = fg;
            detail.Text        = detailText;
            border.Visibility  = Visibility.Visible;
        }

        // ── Edit / Delete chart buttons ──────────────────────────────────────

        private void BtnEditTowerChart_Click(object sender, RoutedEventArgs e)
            => OpenEditDialog(_selectedTowerChartId, isTower: true);

        private void BtnEditMobileChart_Click(object sender, RoutedEventArgs e)
            => OpenEditDialog(_selectedMobileChartId, isTower: false);

        private void BtnDeleteTowerChart_Click(object sender, RoutedEventArgs e)
            => DeleteChart(_selectedTowerChartId, isTower: true);

        private void BtnDeleteMobileChart_Click(object sender, RoutedEventArgs e)
            => DeleteChart(_selectedMobileChartId, isTower: false);

        private void OpenEditDialog(int chartId, bool isTower)
        {
            if (chartId < 0) return;

            var dlg = new LoadChartImportDialog(chartId)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                // Reload combo so the updated chart name/info is shown
                _ = LoadChartOptionsAsync(isTower);
            }
        }

        private async void DeleteChart(int chartId, bool isTower)
        {
            if (chartId < 0) return;

            var result = MessageBox.Show(
                "Permanently delete this load chart and all its data points?\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var db = DatabaseService.Instance.CreateContext();
                var chart = await db.CraneLoadCharts.FindAsync(chartId);
                if (chart != null)
                {
                    db.CraneLoadCharts.Remove(chart);
                    await db.SaveChangesAsync();
                }

                // Reset chart id and disable buttons
                if (isTower)
                {
                    _selectedTowerChartId = -1;
                    if (BtnEditTowerChart != null) BtnEditTowerChart.IsEnabled = false;
                    if (BtnDeleteTowerChart != null) BtnDeleteTowerChart.IsEnabled = false;
                }
                else
                {
                    _selectedMobileChartId = -1;
                    if (BtnEditMobileChart != null) BtnEditMobileChart.IsEnabled = false;
                    if (BtnDeleteMobileChart != null) BtnDeleteMobileChart.IsEnabled = false;
                }

                // Refresh combo
                await LoadChartOptionsAsync(isTower);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
