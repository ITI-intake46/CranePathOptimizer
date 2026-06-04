using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F12_Reports.Views
{
    public partial class ReportsTabView : UserControl
    {
        public ReportsTabView()
        {
            InitializeComponent();
        }

        // ── ELEMENTS ─────────────────────────────────────────────────────────
        private void AllElementsList_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Full elements table (name, type, material, weight, L×W×H, volume, storey, IsLiftable).
            // Merged: previously separate "Weights & Dimensions" is now a column subset of this view.
        }

        // ── LIFTING ───────────────────────────────────────────────────────────
        private void LiftingSummary_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Full lifting record per element: Radius, Hoist Height, Hook Load,
            // Available Capacity from load chart (ConservativeStep), LiftStatus.
            // Combined from former: Radius & Height, Req. vs Available, Capacity per Element, Lift Status.
        }

        // ── CRANE CATALOG ─────────────────────────────────────────────────────
        private void AllCranesInDatabase_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Catalog table — brand, model, MaxCapacity, radius/boom range, outrigger config.
        }

        // ── COMPARE ───────────────────────────────────────────────────────────
        private void Compare2Cranes_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open dialog to pick 2 cranes → side-by-side spec comparison
            //       + which elements each can/cannot lift for this building.
        }

        // ── PROJECT ───────────────────────────────────────────────────────────
        private void ProjectOverview_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Project meta (name, GUID, unit system) + imported IFC files table.
            // Merged: "Project Summary" + "IFC Files Report" combined into one report.
        }

        private void SiteZonesReport_Click(object sender, RoutedEventArgs e)
        {
            // TODO: All site zones: name, type, vertex coordinates.
        }

        // ── ANALYSIS (wired commands — moved from AnalyzeTabView) ────────────
        // GA Diagnostics wired via Command="{Binding ShowDiagnosticsCommand}"
        // View Results    wired via Command="{Binding ShowResultsCommand}"
    }
}
