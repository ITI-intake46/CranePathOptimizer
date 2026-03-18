using System;
using System.Windows;
using System.Windows.Controls;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class IFCRibbonTab : UserControl
    {
        public event Action OnLoadIFC;
        public event Action OnMergeIFC;
        public event Action OnClearModel;
        public event Action OnToggleCOG;
        public event Action OnColorByWeight;
        public event Action OnExportElements;
        public event Action OnLoadDXF;
        public event Action OnAlignDXF;
        public event Action OnFilterElements;

        public IFCRibbonTab() => InitializeComponent();

        private void BtnLoadIFC_Click(object s, RoutedEventArgs e) => OnLoadIFC?.Invoke();
        private void BtnMergeIFC_Click(object s, RoutedEventArgs e) => OnMergeIFC?.Invoke();
        private void BtnClearModel_Click(object s, RoutedEventArgs e) => OnClearModel?.Invoke();
        private void BtnShowCOG_Click(object s, RoutedEventArgs e) => OnToggleCOG?.Invoke();
        private void BtnColorByWeight_Click(object s, RoutedEventArgs e) => OnColorByWeight?.Invoke();
        private void BtnExportElements_Click(object s, RoutedEventArgs e) => OnExportElements?.Invoke();
        private void BtnLoadDXF_Click(object s, RoutedEventArgs e) => OnLoadDXF?.Invoke();
        private void BtnAlignDXF_Click(object s, RoutedEventArgs e) => OnAlignDXF?.Invoke();
        private void BtnFilterElements_Click(object s, RoutedEventArgs e) => OnFilterElements?.Invoke();

        /// <summary>
        /// Updates the visual active-filter indicator dot on the Filter button.
        /// </summary>
        public void SetFilterActive(bool active)
        {
            // Find the indicator dot by name inside the visual tree
            var dot = FindVisualChild<System.Windows.Shapes.Ellipse>(this, "FilterActiveDot");
            if (dot != null)
                dot.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.Name == name) return t;
                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}