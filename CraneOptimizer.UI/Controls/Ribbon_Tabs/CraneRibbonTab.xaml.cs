using System;
using System.Windows;
using System.Windows.Controls;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class CraneRibbonTab : UserControl
    {
        public event Action OnAddTowerCrane;
        public event Action OnAddMobileCrane;
        public event Action OnRemoveCrane;
        public event Action OnRemoveAllCranes;
        public event Action OnRunOptimizer;
        public event Action<bool> OnToggleSwingRadius;

        // ── NEW: Load chart events ────────────────────────────────────────────
        public event Action OnImportLoadChart;
        public event Action OnViewLoadCharts;

        public CraneRibbonTab() => InitializeComponent();

        private void BtnAddTower_Click(object s, RoutedEventArgs e) => OnAddTowerCrane?.Invoke();
        private void BtnAddMobile_Click(object s, RoutedEventArgs e) => OnAddMobileCrane?.Invoke();
        private void BtnRemove_Click(object s, RoutedEventArgs e) => OnRemoveCrane?.Invoke();
        private void BtnRemoveAll_Click(object s, RoutedEventArgs e) => OnRemoveAllCranes?.Invoke();
        private void BtnOptimize_Click(object s, RoutedEventArgs e) => OnRunOptimizer?.Invoke();
        private void TglSwing_Changed(object s, RoutedEventArgs e) => OnToggleSwingRadius?.Invoke(TglSwingRadius.IsChecked == true);

        // ── NEW handlers ──────────────────────────────────────────────────────
        private void BtnImportLoadChart_Click(object s, RoutedEventArgs e) => OnImportLoadChart?.Invoke();
        private void BtnViewLoadCharts_Click(object s, RoutedEventArgs e) => OnViewLoadCharts?.Invoke();

        public event Action OnOpenCatalog;
        public event Action OnCompareCharts;
        public event Action<bool> OnToggleCapacityRings;

        private void BtnCatalog_Click(object s, RoutedEventArgs e) => OnOpenCatalog?.Invoke();
        private void BtnCompare_Click(object s, RoutedEventArgs e) => OnCompareCharts?.Invoke();
        private void TglCapacityRings_Changed(object s, RoutedEventArgs e)
            => OnToggleCapacityRings?.Invoke(TglCapacityRings.IsChecked == true);
    }
}
