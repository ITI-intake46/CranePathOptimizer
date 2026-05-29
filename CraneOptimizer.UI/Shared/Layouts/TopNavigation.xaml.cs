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
            // Guard: elements may not yet be ready during InitializeComponent
            if (PanelHome == null) return;

            // Hide all panels
            PanelHome.Visibility    = Visibility.Collapsed;
            PanelIFC.Visibility     = Visibility.Collapsed;
            PanelDXF.Visibility     = Visibility.Collapsed;
            PanelCranes.Visibility  = Visibility.Collapsed;
            PanelZones.Visibility   = Visibility.Collapsed;
            PanelAnalyze.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelCoordinateReference.Visibility = Visibility.Collapsed;

            // Show the selected panel
            if (sender == TabHome)    PanelHome.Visibility    = Visibility.Visible;
            if (sender == TabIFC)     PanelIFC.Visibility     = Visibility.Visible;
            if (sender == TabDXF)     PanelDXF.Visibility     = Visibility.Visible;
            if (sender == TabCranes)  PanelCranes.Visibility  = Visibility.Visible;
            if (sender == TabZones)   PanelZones.Visibility   = Visibility.Visible;
            if (sender == TabAnalyze) PanelAnalyze.Visibility = Visibility.Visible;
            if (sender == TabSettings) PanelSettings.Visibility = Visibility.Visible;
            if (sender == TabCoordinateReference) PanelCoordinateReference.Visibility = Visibility.Visible;
        }
    }
}
