using System;
using System.Windows;
using System.Windows.Controls;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class AnalyzeRibbonTab : UserControl
    {
        public event Action OnRunOptimizer;
        public event Action OnExportReport;
        public event Action OnExportZones;
        public event Action OnOpenLiftPlan;

        public AnalyzeRibbonTab() => InitializeComponent();

        private void BtnRun_Click(object s, RoutedEventArgs e) => OnRunOptimizer?.Invoke();
        private void BtnReport_Click(object s, RoutedEventArgs e) => OnExportReport?.Invoke();
        private void BtnExportZones_Click(object s, RoutedEventArgs e) => OnExportZones?.Invoke();
        private void BtnLiftPlan_Click(object s, RoutedEventArgs e) => OnOpenLiftPlan?.Invoke();
    }
}
