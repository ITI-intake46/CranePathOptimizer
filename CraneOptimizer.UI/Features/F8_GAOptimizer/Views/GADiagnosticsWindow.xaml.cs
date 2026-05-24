using System.Windows;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    /// <summary>
    /// Interaction logic for GADiagnosticsWindow.xaml
    /// </summary>
    public partial class GADiagnosticsWindow : Window
    {
        public GADiagnosticsWindow(GADiagnosticsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
