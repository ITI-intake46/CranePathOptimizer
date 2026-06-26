namespace CraneOptimizer.Application.Services
{
    /// <summary>
    /// Provides the active project's root folder path to the Application layer
    /// without coupling it to any UI or workspace service directly.
    /// Implementations live in the UI layer and wrap the active session.
    /// </summary>
    public interface IActiveProjectPathProvider
    {
        /// <summary>
        /// Returns the absolute path of the currently loaded project folder,
        /// or <c>null</c> if no project is currently active.
        /// </summary>
        string? GetActiveProjectPath();
    }
}
