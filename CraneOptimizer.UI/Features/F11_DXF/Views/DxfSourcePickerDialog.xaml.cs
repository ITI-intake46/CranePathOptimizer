using System.Windows;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public partial class DxfSourcePickerDialog : Window
    {
        public DxfSourcePickerDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
