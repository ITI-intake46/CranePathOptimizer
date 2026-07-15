namespace CraneOptimizer.UI.Core.Configuration
{
    /// <summary>
    /// Centralized feature flags for UI visibility toggles.
    /// </summary>
    public static class AppFeatureFlags
    {
        /// <summary>
        /// Master switch for project-scoped debug, diagnostics, audit, and performance capture outputs.
        /// Default false keeps end-user exports/reports/save-load unaffected while suppressing internal files.
        /// </summary>
        public static bool EnableProjectPerformanceCapture { get; set; } = false;
        // Temporarily hidden by product decision. Keep logic intact for future re-enable.
        public static bool ShowSettingsTab { get; set; } = false;

        // Temporarily hidden by product decision. Keep logic intact for future re-enable.
        public static bool ShowCoordinateReferenceUi { get; set; } = false;

        /// <summary>
        /// Flag to show/hide developer/research-only GA parameters in the Advanced Options panel.
        /// </summary>
        public static bool ShowGADeveloperParameters { get; set; } = false;
    }
}
