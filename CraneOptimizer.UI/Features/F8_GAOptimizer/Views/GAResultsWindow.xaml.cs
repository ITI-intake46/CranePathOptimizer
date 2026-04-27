using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    /// <summary>
    /// Interaction logic for GAResultsWindow.xaml
    /// </summary>
    public partial class GAResultsWindow : Window
    {
        private readonly GAResultsWindowViewModel _viewModel;

        public GAResultsWindow(GAResultsWindowViewModel viewModel)
        {
            _viewModel = viewModel;

            InitializeComponent();
            DataContext = _viewModel;

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.OnLoaded();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            _viewModel.OnClosing();
        }
    }
}
