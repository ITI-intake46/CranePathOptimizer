using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.UI.Shell.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectNavigationService : IProjectNavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        public ProjectNavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void OpenMainWindow(ProjectSession session)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            if (mainWindow.DataContext is MainViewModel viewModel)
                viewModel.OpenProject(session);

            System.Windows.Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
