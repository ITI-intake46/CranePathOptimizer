using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;

namespace WPF_IFC_test1.Dialogs
{
    public partial class LoadChartCompareWindow : Window
    {
        private List<CraneLoadChartEntity> _charts = new();
        private CraneLoadChartEntity? _chartA, _chartB;

        public LoadChartCompareWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadChartsAsync();
        }

        private async Task LoadChartsAsync()
        {
            try
            {
                using var db = DatabaseService.Instance.CreateContext();
                _charts = await db.CraneLoadCharts
                    .Include(c => c.Points)
                    .OrderBy(c => c.CraneModel)
                    .ToListAsync();

                foreach (var c in _charts)
                {
                    string label = $"{c.CraneModel}  —  {c.ChartType} / {c.OutriggerConfig}";
                    CboChartA.Items.Add(new ComboBoxItem { Content = label, Tag = c });
                    CboChartB.Items.Add(new ComboBoxItem { Content = label, Tag = c });
                }
                TxtStatus.Text = $"{_charts.Count} charts loaded.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void CboChartA_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CboChartA.SelectedItem is ComboBoxItem { Tag: CraneLoadChartEntity chart })
            {
                _chartA = chart;
                GridA.ItemsSource = BuildDataTable(chart).DefaultView;
                TryBuildDelta();
            }
        }

        private void CboChartB_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CboChartB.SelectedItem is ComboBoxItem { Tag: CraneLoadChartEntity chart })
            {
                _chartB = chart;
                GridB.ItemsSource = BuildDataTable(chart).DefaultView;
                TryBuildDelta();
            }
        }

        private void TryBuildDelta()
        {
            if (_chartA == null || _chartB == null) return;

            var ptA = _chartA.Points.Where(p => p.CapacityTonne.HasValue).ToList();
            var ptB = _chartB.Points.Where(p => p.CapacityTonne.HasValue).ToList();

            // Use the intersection of boom lengths for comparison columns
            var boomSetA = ptA.Select(p => p.BoomLengthM).Distinct().ToHashSet();
            var boomSetB = ptB.Select(p => p.BoomLengthM).Distinct().ToHashSet();
            var booms = boomSetA.Intersect(boomSetB).OrderBy(b => b).ToList();

            if (booms.Count == 0)
            {
                TxtStatus.Text = "No overlapping boom lengths between charts.";
                return;
            }

            // Union of all radii for rows
            var radii = ptA.Select(p => p.RadiusM)
                .Union(ptB.Select(p => p.RadiusM))
                .Distinct().OrderBy(r => r).ToList();

            var dt = new DataTable();
            dt.Columns.Add("Radius");
            foreach (double b in booms) dt.Columns.Add($"{b:G}m");

            foreach (double r in radii)
            {
                var row = dt.NewRow();
                row[0] = $"{r:G}m";
                for (int i = 0; i < booms.Count; i++)
                {
                    double? capA = CraneLoadChartLookup.GetCapacity(ptA, r, booms[i]);
                    double? capB = CraneLoadChartLookup.GetCapacity(ptB, r, booms[i]);
                    if (capA.HasValue && capB.HasValue)
                        row[i + 1] = $"{(capA.Value - capB.Value):+0.##;-0.##;0}";
                    else
                        row[i + 1] = "";
                }
                dt.Rows.Add(row);
            }

            GridDelta.ItemsSource = dt.DefaultView;
            TxtStatus.Text = $"Comparing: {_chartA.CraneModel} vs {_chartB.CraneModel}  |  {booms.Count} common boom length(s).";
        }

        public static DataTable BuildDataTable(CraneLoadChartEntity chart)
        {
            var pts   = chart.Points.Where(p => p.CapacityTonne.HasValue).ToList();
            var booms = pts.Select(p => p.BoomLengthM).Distinct().OrderBy(b => b).ToList();
            var radii = pts.Select(p => p.RadiusM).Distinct().OrderBy(r => r).ToList();
            var lkp   = pts.ToDictionary(
                p => (Math.Round(p.RadiusM, 4), Math.Round(p.BoomLengthM, 4)),
                p => p.CapacityTonne!.Value);

            var dt = new DataTable();
            dt.Columns.Add("Radius");
            foreach (double b in booms) dt.Columns.Add($"{b:G}m");

            foreach (double r in radii)
            {
                var row = dt.NewRow();
                row[0] = $"{r:G}m";
                for (int i = 0; i < booms.Count; i++)
                    row[i + 1] = lkp.TryGetValue((Math.Round(r, 4), Math.Round(booms[i], 4)), out double v)
                        ? $"{v:G}" : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
