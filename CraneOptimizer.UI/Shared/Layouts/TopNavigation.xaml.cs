using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Shared.Layouts
{
    public partial class TopNavigation : UserControl
    {
        public TopNavigation()
        {
            InitializeComponent();
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            //// Hide all panels
            //PanelHome.Visibility    = Visibility.Collapsed;
            //PanelIFC.Visibility     = Visibility.Collapsed;
            //PanelCranes.Visibility  = Visibility.Collapsed;
            //PanelZones.Visibility   = Visibility.Collapsed;
            //PanelAnalyze.Visibility = Visibility.Collapsed;

            //// Show selected panel
            //if (sender == TabHome)    PanelHome.Visibility    = Visibility.Visible;
            //if (sender == TabIFC)     PanelIFC.Visibility     = Visibility.Visible;
            //if (sender == TabCranes)  PanelCranes.Visibility  = Visibility.Visible;
            //if (sender == TabZones)   PanelZones.Visibility   = Visibility.Visible;
            //if (sender == TabAnalyze) PanelAnalyze.Visibility = Visibility.Visible;
        }
    }
}
