using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public interface IProjectNavigationService
    {
        /// <summary>Opens the main workspace window for the given project session.</summary>
        Task OpenMainWindowAsync(ProjectSession session);

        /// <summary>
        /// Returns to the project-management (startup) screen.
        /// If <paramref name="activeSession"/> is supplied, the startup window shows a
        /// "Return to open project" banner so the user can go back.
        /// </summary>
        void NavigateToStartup(ProjectSession? activeSession = null);
    }
}
