using CraneOptimizer.UI.F10_ProjectManagement.Views;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
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
            // Reuse an existing (hidden) MainWindow if one is already in the application
            MainWindow? existing = null;
            foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            {
                if (w is MainWindow mw) { existing = mw; break; }
            }

            if (existing != null)
            {
                System.Windows.Application.Current.MainWindow = existing;
                existing.Show();
                existing.Activate();
                return;
            }

            // First time — create and initialise a new MainWindow
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            if (mainWindow.DataContext is MainViewModel viewModel)
                viewModel.OpenProject(session);

            System.Windows.Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }

        public void NavigateToStartup(ProjectSession? activeSession = null)
        {
            // Resolve a fresh (Transient) startup window
            var startupWindow = _serviceProvider.GetRequiredService<ProjectManagementWindow>();

            // Give it the active session so it can show a "Return to project" banner
            if (startupWindow.DataContext is ProjectManagementViewModel vm)
                vm.ActiveSession = activeSession;

            System.Windows.Application.Current.MainWindow = startupWindow;
            startupWindow.Show();
        }
    }
}
