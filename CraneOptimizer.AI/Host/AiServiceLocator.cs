namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// Minimal host-provided service locator for WPF controls created from XAML.
    /// CraneOptimizer.UI assigns this after building its ServiceProvider.
    /// </summary>
    public static class AiServiceLocator
    {
        public static IServiceProvider? ServiceProvider { get; set; }
    }
}
