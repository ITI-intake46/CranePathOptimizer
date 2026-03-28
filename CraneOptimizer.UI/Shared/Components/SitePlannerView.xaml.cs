using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Shared.Components
{
    public partial class SitePlannerView : UserControl
    {
        public SitePlannerView()
        {
            InitializeComponent();
        }

        private void TabCranes_Click(object sender, RoutedEventArgs e)
        {
            TabCranes.IsChecked  = true;
            TabZones.IsChecked   = false;
            //CranePanel.Visibility = Visibility.Visible;
            //ZonePanel.Visibility  = Visibility.Collapsed;
        }

        private void TabZones_Click(object sender, RoutedEventArgs e)
        {
            TabZones.IsChecked   = true;
            TabCranes.IsChecked  = false;
            //ZonePanel.Visibility  = Visibility.Visible;
            //CranePanel.Visibility = Visibility.Collapsed;
        }
    }
}
