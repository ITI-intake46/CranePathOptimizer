using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GADiagnosticsWindowService : IGADiagnosticsWindowService
    {
        private readonly IGADiagnosticsReaderService _readerService;
        private readonly IProjectWorkspaceService _workspaceService;

        public GADiagnosticsWindowService(
            IGADiagnosticsReaderService readerService,
            IProjectWorkspaceService workspaceService)
        {
            _readerService = readerService;
            _workspaceService = workspaceService;
        }

        public void Show()
        {
            try
            {
                var viewModel = new GADiagnosticsViewModel(_readerService, _workspaceService);
                var window = new GADiagnosticsWindow(viewModel);

                if (System.Windows.Application.Current?.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }

                window.ShowDialog();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open GA Diagnostics Viewer: {ex.Message}\n\nPlease check the reports configuration or log files.",
                    "Viewer Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
