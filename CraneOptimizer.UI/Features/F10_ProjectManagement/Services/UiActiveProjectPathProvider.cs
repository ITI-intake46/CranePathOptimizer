using System;
using CraneOptimizer.Application.Services;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    /// <summary>
    /// UI-layer implementation of <see cref="IActiveProjectPathProvider"/>.
    /// Wraps <see cref="IProjectWorkspaceService"/> to expose the current
    /// session's project folder path to the Application layer without
    /// introducing a UI dependency in Application.
    /// </summary>
    public sealed class UiActiveProjectPathProvider : IActiveProjectPathProvider
    {
        private readonly IProjectWorkspaceService _workspaceService;

        public UiActiveProjectPathProvider(IProjectWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService
                ?? throw new ArgumentNullException(nameof(workspaceService));
        }

        /// <inheritdoc/>
        public string? GetActiveProjectPath()
        {
            return _workspaceService.CurrentSession?.ProjectFolderPath;
        }
    }
}
