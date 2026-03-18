using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Dialogs
{
    public partial class LoadChartViewerWindow : Window
    {
        // Full list loaded once from DB
        private List<ChartSummary> _allCharts = new();

        // Currently selected chart id (-1 = none)
        private int _selectedChartId = -1;

        // ── Simple display model (no EF tracking needed) ──────────────────────
        private record ChartSummary(
            int    Id,
            string Category,       // "Mobile" | "Tower"
            string Model,
            string Manufacturer,
            string ChartType,
            string OutriggerConfig,
            double CounterweightKg,
            double OutriggerSpanMm,
            double TotalLengthMm,
            double HeightMm,
            double SpanToSpanMm,
            double BoundingRadiusM,
            string ImportMethod,
            DateTime ImportedAt,
            int    PointCount,
            double MaxCapacityTonne,
            double MaxRadiusM
        );

        public LoadChartViewerWindow()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadChartsAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Data loading
        // ─────────────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task LoadChartsAsync()
        {
            TxtStatus.Text = "Loading saved charts…";

            try
            {
                using var db = DatabaseService.Instance.CreateContext();

                var charts = await db.CraneLoadCharts
                    .Include(c => c.Points)
                    .OrderByDescending(c => c.ImportedAt)
                    .ToListAsync();

                _allCharts = charts.Select(c => new ChartSummary(
                    Id:               c.Id,
                    Category:         string.IsNullOrEmpty(c.CraneCategory) ? "Mobile" : c.CraneCategory,
                    Model:            c.CraneModel,
                    Manufacturer:     c.Manufacturer,
                    ChartType:        c.ChartType,
                    OutriggerConfig:  c.OutriggerConfig,
                    CounterweightKg:  c.CounterweightKg,
                    OutriggerSpanMm:  c.OutriggerSpanMm,
                    TotalLengthMm:    c.TotalLengthMm,
                    HeightMm:         c.HeightMm,
                    SpanToSpanMm:     c.SpanToSpanMm,
                    BoundingRadiusM:  c.BoundingRadiusM,
                    ImportMethod:     c.ImportMethod,
                    ImportedAt:       c.ImportedAt,
                    PointCount:       c.Points.Count,
                    MaxCapacityTonne: c.Points.Any(p => p.CapacityTonne.HasValue)
                                        ? c.Points.Where(p => p.CapacityTonne.HasValue).Max(p => p.CapacityTonne!.Value)
                                        : 0,
                    MaxRadiusM:       c.Points.Any()
                                        ? c.Points.Max(p => p.RadiusM)
                                        : 0
                )).ToList();

                TxtStatus.Text = $"{_allCharts.Count} chart{(_allCharts.Count == 1 ? "" : "s")} saved.";
                RenderList(_allCharts);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"❌  {ex.Message}";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  List rendering
        // ─────────────────────────────────────────────────────────────────────

        private void RenderList(IEnumerable<ChartSummary> charts)
        {
            ChartListPanel.Children.Clear();

            foreach (var c in charts)
            {
                bool isTower = c.Category == "Tower";

                // Colour strip
                var strip = new Border
                {
                    Width = 4,
                    CornerRadius = new CornerRadius(2, 0, 0, 2),
                    Background = isTower
                        ? new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12))
                        : new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                    Margin = new Thickness(0, 2, 6, 2)
                };

                string icon = isTower ? "🏗️" : "🚛";
                string dateStr = c.ImportedAt.ToLocalTime().ToString("dd/MM/yy");
                string mfr = string.IsNullOrWhiteSpace(c.Manufacturer) ? "" : $"  ·  {c.Manufacturer}";

                var nameBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(c.Model) ? "(unnamed)" : c.Model,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xF0)),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                var subBlock = new TextBlock
                {
                    Text = $"{c.ChartType}{mfr}  ·  {dateStr}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4C, 0x78)),
                    FontSize = 9,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                var iconBlock = new TextBlock
                {
                    Text = icon,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(nameBlock);
                info.Children.Add(subBlock);

                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(strip, 0);
                Grid.SetColumn(iconBlock, 1);
                Grid.SetColumn(info, 2);
                row.Children.Add(strip);
                row.Children.Add(iconBlock);
                row.Children.Add(info);

                var item = new Border
                {
                    Padding = new Thickness(8, 7, 8, 7),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1C, 0x38)),
                    Tag = c
                };
                item.Child = row;

                item.MouseEnter  += (s, _) => { if (s is Border b && !(bool)(b.GetValue(IsSelectedProperty) ?? false)) b.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x2A)); };
                item.MouseLeave  += (s, _) => { if (s is Border b && !(bool)(b.GetValue(IsSelectedProperty) ?? false)) b.Background = Brushes.Transparent; };
                item.MouseLeftButtonUp += Item_Click;

                ChartListPanel.Children.Add(item);
            }

            if (!charts.Any())
            {
                ChartListPanel.Children.Add(new TextBlock
                {
                    Text = "No charts found.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x50)),
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(12, 16, 12, 16)
                });
            }
        }

        // Attached property trick to track selected state on border items
        private static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(LoadChartViewerWindow), new PropertyMetadata(false));

        private Border? _selectedItem;

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border item || item.Tag is not ChartSummary chart) return;

            // Deselect previous
            if (_selectedItem != null)
            {
                _selectedItem.SetValue(IsSelectedProperty, false);
                _selectedItem.Background = Brushes.Transparent;
            }

            // Select new
            _selectedItem = item;
            item.SetValue(IsSelectedProperty, true);
            item.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x27, 0x48));

            _selectedChartId = chart.Id;
            BtnEdit.IsEnabled = true;
            BtnDelete.IsEnabled = true;

            ShowDetail(chart);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Detail panel
        // ─────────────────────────────────────────────────────────────────────

        private void ShowDetail(ChartSummary c)
        {
            DetailEmpty.Visibility = Visibility.Collapsed;
            DetailScroll.Visibility = Visibility.Visible;

            bool isTower = c.Category == "Tower";

            // Title
            DetailModel.Text = string.IsNullOrWhiteSpace(c.Model) ? "(unnamed)" : c.Model;
            DetailManufacturer.Text = string.IsNullOrWhiteSpace(c.Manufacturer) ? "— manufacturer not set —" : c.Manufacturer;
            DetailImportDate.Text = $"Imported {c.ImportedAt.ToLocalTime():dd MMM yyyy HH:mm}  via {c.ImportMethod}";

            // Category badge
            DetailCategory.Text = isTower ? "🏗️  Tower Crane" : "🚛  Mobile Crane";
            DetailCatBadge.Background = isTower
                ? new SolidColorBrush(Color.FromRgb(0x40, 0x28, 0x08))
                : new SolidColorBrush(Color.FromRgb(0x08, 0x30, 0x18));
            DetailCategory.Foreground = isTower
                ? new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12))
                : new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));

            // Config fields
            DetailChartType.Text = string.IsNullOrWhiteSpace(c.ChartType) ? "—" : c.ChartType;
            DetailOutrigger.Text = string.IsNullOrWhiteSpace(c.OutriggerConfig) ? "—" : c.OutriggerConfig;
            DetailCounterweight.Text = c.CounterweightKg > 0
                ? $"{c.CounterweightKg / 1000:F1} t  ({c.CounterweightKg:N0} kg)"
                : "—";
            DetailDerating.Text = "100%  (stored capacity)";
            DetailMethod.Text = c.ImportMethod;

            // Physical dimensions
            DetailLength.Text       = c.TotalLengthMm    > 0 ? $"{c.TotalLengthMm / 1000:F2} m  ({c.TotalLengthMm:N0} mm)" : "—";
            DetailHeight.Text       = c.HeightMm         > 0 ? $"{c.HeightMm / 1000:F2} m  ({c.HeightMm:N0} mm)"         : "—";
            DetailOutriggerSpan.Text= c.OutriggerSpanMm  > 0 ? $"{c.OutriggerSpanMm / 1000:F2} m  ({c.OutriggerSpanMm:N0} mm)" : "—";
            DetailSpanToSpan.Text   = c.SpanToSpanMm     > 0 ? $"{c.SpanToSpanMm / 1000:F2} m  ({c.SpanToSpanMm:N0} mm)"  : "—";
            DetailBoundingRadius.Text = c.BoundingRadiusM > 0
                ? $"{c.BoundingRadiusM:F2} m   ∅ {c.BoundingRadiusM * 2:F2} m clearance"
                : "—";

            // Capacity stats
            DetailPointCount.Text = c.PointCount.ToString("N0");
            DetailMaxCap.Text     = c.MaxCapacityTonne > 0 ? $"{c.MaxCapacityTonne:F1} t" : "—";
            DetailMaxRadius.Text  = c.MaxRadiusM       > 0 ? $"{c.MaxRadiusM:F1} m"       : "—";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Search
        // ─────────────────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            string q = TxtSearch.Text.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrEmpty(q)
                ? _allCharts
                : _allCharts.Where(c =>
                    c.Model.ToLowerInvariant().Contains(q) ||
                    c.Manufacturer.ToLowerInvariant().Contains(q) ||
                    c.Category.ToLowerInvariant().Contains(q) ||
                    c.ChartType.ToLowerInvariant().Contains(q));

            RenderList(filtered);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Edit / Delete
        // ─────────────────────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChartId < 0) return;
            var dlg = new LoadChartImportDialog(_selectedChartId) { Owner = this };
            if (dlg.ShowDialog() == true)
                _ = LoadChartsAsync(); // Reload list after edit
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChartId < 0) return;
            var result = MessageBox.Show(
                "Permanently delete this load chart and all its data points?\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var db = DatabaseService.Instance.CreateContext();
                var chart = await db.CraneLoadCharts.FindAsync(_selectedChartId);
                if (chart != null)
                {
                    db.CraneLoadCharts.Remove(chart);
                    await db.SaveChangesAsync();
                }
                _selectedChartId = -1;
                BtnEdit.IsEnabled = false;
                BtnDelete.IsEnabled = false;
                DetailEmpty.Visibility = Visibility.Visible;
                DetailScroll.Visibility = Visibility.Collapsed;
                await LoadChartsAsync();
                TxtStatus.Text = "Chart deleted.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Close
        // ─────────────────────────────────────────────────────────────────────

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
