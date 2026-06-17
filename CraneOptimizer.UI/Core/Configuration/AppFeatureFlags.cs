namespace CraneOptimizer.UI.Core.Configuration
{
    /// <summary>
    /// Centralized feature flags for UI visibility toggles.
    /// </summary>
    public static class AppFeatureFlags
    {
        // Temporarily hidden by product decision. Keep logic intact for future re-enable.
        public static bool ShowSettingsTab { get; set; } = false;

        // Temporarily hidden by product decision. Keep logic intact for future re-enable.
        public static bool ShowCoordinateReferenceUi { get; set; } = false;
    }
}
