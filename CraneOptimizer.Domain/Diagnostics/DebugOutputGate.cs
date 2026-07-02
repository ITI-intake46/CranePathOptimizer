namespace CraneOptimizer.Domain.Diagnostics
{
    /// <summary>
    /// Lower-layer mirror for the UI-owned project performance capture feature flag.
    /// UI sets this from AppFeatureFlags.EnableProjectPerformanceCapture on startup.
    /// </summary>
    public static class DebugOutputGate
    {
        public static bool EnableProjectPerformanceCapture { get; set; } = false;
    }
}