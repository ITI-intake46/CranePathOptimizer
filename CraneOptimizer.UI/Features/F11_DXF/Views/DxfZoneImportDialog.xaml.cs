using System.Windows;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public partial class DxfZoneImportDialog : Window
    {
        public DxfZoneImportDialog()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
