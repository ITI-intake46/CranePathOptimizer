using System;
using System.Windows;
using System.Windows.Controls;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class ViewRibbonTab : UserControl
    {
        public event Action OnEnterPlanView;
        public event Action OnReturnTo3D;
        public event Action OnFitToSite;
        public event Action OnToggleGrid;
        public event Action<bool> OnToggleZoneVolumes;
        public event Action OnToggleFlatZones;

        public ViewRibbonTab() => InitializeComponent();

        private void BtnPerspective_Click(object s, RoutedEventArgs e) => OnReturnTo3D?.Invoke();
        private void BtnPlanView_Click(object s, RoutedEventArgs e) => OnEnterPlanView?.Invoke();
        private void BtnFit_Click(object s, RoutedEventArgs e) => OnFitToSite?.Invoke();
        private void TglGrid_Changed(object s, RoutedEventArgs e) => OnToggleGrid?.Invoke();
        private void TglZones_Changed(object s, RoutedEventArgs e) => OnToggleZoneVolumes?.Invoke(TglZones.IsChecked == true);
        private void TglFlatZones_Changed(object s, RoutedEventArgs e) => OnToggleFlatZones?.Invoke();
    }
}