using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;

namespace WPF_IFC_test1.Dialogs
{
    public partial class CraneCatalogBrowser : Window
    {
        private List<CraneCatalogEntry> _allEntries = new();
        private CraneCatalogEntry? _selectedEntry;
        private CraneLoadChartEntity? _selectedChart;

        // Callback: when user assigns a chart (passes the chart ID and config name)
        public event Action<int, string>? ChartAssigned;

        public CraneCatalogBrowser()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadCatalogAsync();
        }

        private record CraneCatalogEntry(
            CraneCatalogEntity? Catalog,
            string DisplayName,
            string Manufacturer,
            List<CraneLoadChartEntity> Charts);

        private async Task LoadCatalogAsync()
        {
            try
            {
                using var db = DatabaseService.Instance.CreateContext();

                // Load all charts grouped by model name
                var charts = await db.CraneLoadCharts
                    .Include(c => c.Points)
                    .Include(c => c.Catalog)
                    .OrderBy(c => c.Manufacturer).ThenBy(c => c.CraneModel)
                    .ToListAsync();

                var entries = new List<CraneCatalogEntry>();

                var byModel = charts.GroupBy(c => new { c.CraneModel, c.Manufacturer });
                foreach (var grp in byModel)
                {
                    entries.Add(new CraneCatalogEntry(
                        grp.First().Catalog,
                        grp.Key.CraneModel,
                        grp.Key.Manufacturer,
                        grp.ToList()));
                }

                _allEntries = entries;
                RenderList(_allEntries);
                TxtStatus.Text = $"{entries.Count} crane model(s) found.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void RenderList(List<CraneCatalogEntry> entries)
        {
            CatalogListPanel.Children.Clear();
            foreach (var entry in entries)
            {
                var border = new Border
                {
                    Margin       = new Thickness(4, 2, 4, 0),
                    Padding      = new Thickness(10, 7, 10, 7),
                    Background   = new SolidColorBrush(Color.FromRgb(0x12, 0x13, 0x25)),
                    CornerRadius = new CornerRadius(4),
                    Cursor       = Cursors.Hand,
                    Tag          = entry
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text       = entry.DisplayName,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC4, 0xE8)),
                    FontSize   = 11
                });
                sp.Children.Add(new TextBlock
                {
                    Text       = $"{entry.Manufacturer}  •  {entry.Charts.Count} config(s)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4C, 0x78)),
                    FontSize   = 9
                });
                border.Child = sp;
                border.MouseLeftButtonDown += EntryClick;
                CatalogListPanel.Children.Add(border);
            }
        }

        private void EntryClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is CraneCatalogEntry entry)
            {
                _selectedEntry = entry;
                ShowDetail(entry);
            }
        }

        private void ShowDetail(CraneCatalogEntry entry)
        {
            DetailEmpty.Visibility  = Visibility.Collapsed;
            DetailScroll.Visibility = Visibility.Visible;

            DetailModel.Text = entry.DisplayName;
            DetailMfg.Text   = entry.Manufacturer;

            // Populate chart list
            ChartListPanel.Children.Clear();
            foreach (var chart in entry.Charts)
            {
                double maxCap = chart.Points
                    .Where(p => p.CapacityTonne.HasValue)
                    .Select(p => p.CapacityTonne!.Value)
                    .DefaultIfEmpty(0).Max();

                var border = new Border
                {
                    Margin       = new Thickness(0, 2, 0, 0),
                    Padding      = new Thickness(8, 5, 8, 5),
                    Background   = new SolidColorBrush(Color.FromRgb(0x0C, 0x0D, 0x1E)),
                    CornerRadius = new CornerRadius(3),
                    Cursor       = Cursors.Hand,
                    Tag          = chart
                };

                string label = $"{chart.ChartType}  /  {chart.OutriggerConfig}" +
                               (chart.DeratingPct < 100 ? $"  @ {chart.DeratingPct}%" : "") +
                               $"  •  max {maxCap:F1} t";

                border.Child = new TextBlock
                {
                    Text       = label,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x82, 0xAC)),
                    FontSize   = 10
                };
                border.MouseLeftButtonDown += ChartClick;
                ChartListPanel.Children.Add(border);
            }

            // Auto-select first chart
            if (entry.Charts.Count > 0)
                SelectChart(entry.Charts[0]);
        }

        private void ChartClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is CraneLoadChartEntity chart)
                SelectChart(chart);
        }

        private void SelectChart(CraneLoadChartEntity chart)
        {
            _selectedChart  = chart;
            BtnAssign.IsEnabled = true;

            TxtSelectedChart.Text       = $"Preview: {chart.CraneModel} — {chart.ChartType} / {chart.OutriggerConfig}";
            TxtSelectedChart.Visibility = Visibility.Visible;

            // Build mini DataTable for preview
            var pts = chart.Points.Where(p => p.CapacityTonne.HasValue).ToList();
            if (pts.Count > 0)
            {
                var booms  = pts.Select(p => p.BoomLengthM).Distinct().OrderBy(b => b).Take(6).ToList();
                var radii  = pts.Select(p => p.RadiusM).Distinct().OrderBy(r => r).Take(8).ToList();
                var lookup = pts.ToDictionary(p => (p.RadiusM, p.BoomLengthM), p => p.CapacityTonne!.Value);

                var dt = new System.Data.DataTable();
                dt.Columns.Add("R\\Boom");
                foreach (double b in booms) dt.Columns.Add($"{b:G}m");

                foreach (double r in radii)
                {
                    var row = dt.NewRow();
                    row[0] = $"{r:G}m";
                    for (int i = 0; i < booms.Count; i++)
                        row[i + 1] = lookup.TryGetValue((r, booms[i]), out double v) ? $"{v:G}" : "";
                    dt.Rows.Add(row);
                }

                MiniGrid.ItemsSource    = dt.DefaultView;
                MiniPreview.Visibility  = Visibility.Visible;
            }
            else
            {
                MiniPreview.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtSearch.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
                RenderList(_allEntries);
            else
                RenderList(_allEntries.Where(en =>
                    en.DisplayName.ToLowerInvariant().Contains(q) ||
                    en.Manufacturer.ToLowerInvariant().Contains(q)).ToList());
        }

        private void BtnAssign_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChart == null) return;
            string config = $"{_selectedChart.ChartType} / {_selectedChart.OutriggerConfig}";
            ChartAssigned?.Invoke(_selectedChart.Id, config);
            TxtStatus.Text = $"Chart assigned: {_selectedChart.CraneModel} — {config}";
            MessageBox.Show(
                $"Load chart '{_selectedChart.CraneModel} ({config})' assigned to the selected crane.\n\n" +
                "The chart combo in the Crane Manager Panel will refresh automatically.",
                "Chart Assigned", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LoadChartImportDialog { Owner = this };
            if (dlg.ShowDialog() == true)
                _ = LoadCatalogAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
