using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class HomeRibbonTab : UserControl
    {
        public event Action OnNewProject;
        public event Action OnOpenProject;
        public event Action OnSaveProject;
        public event Action OnProjectSettings;

        public HomeRibbonTab() => InitializeComponent();

        private void BtnNew_Click(object s, RoutedEventArgs e) => OnNewProject?.Invoke();
        private void BtnOpen_Click(object s, RoutedEventArgs e) => OnOpenProject?.Invoke();
        private void BtnSave_Click(object s, RoutedEventArgs e) => OnSaveProject?.Invoke();
        private void BtnSettings_Click(object s, RoutedEventArgs e) => OnProjectSettings?.Invoke();
        private void BtnUnits_Click(object s, RoutedEventArgs e) => OnProjectSettings?.Invoke();

        private void BtnGotoIFC_Click(object s, RoutedEventArgs e) => FindRibbon()?.SwitchToIFCTab();
        private void BtnGotoCrane_Click(object s, RoutedEventArgs e) => FindRibbon()?.SwitchToCraneTab();
        private void BtnGotoZone_Click(object s, RoutedEventArgs e) => FindRibbon()?.SwitchToZoneTab();

        private MainRibbonControl FindRibbon()
        {
            DependencyObject p = this;
            while (p != null)
            {
                if (p is MainRibbonControl r) return r;
                p = VisualTreeHelper.GetParent(p);
            }
            return null;
        }
    }
}
