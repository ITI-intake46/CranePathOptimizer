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

        private void AllCranesInDatabase_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Catalog table - brand, model, MaxCapacity, radius/boom range, outrigger config.
        }

        private void Compare2Cranes_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open dialog to pick 2 cranes for side-by-side spec comparison.
        }

        private void ProjectOverview_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Project meta plus imported IFC files table.
        }

        private void SiteZonesReport_Click(object sender, RoutedEventArgs e)
        {
            // TODO: All site zones: name, type, vertex coordinates.
        }
    }
}
