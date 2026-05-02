using System;
using System.Linq;
using WpfApp = System.Windows.Application;

namespace CraneOptimizer.UI.Resources
{
    /// <summary>
    /// Manages runtime theme switching between Dark and Light modes.
    /// Call ApplyTheme() on startup (from App.xaml.cs) and whenever
    /// the user toggles the theme in Settings.
    /// </summary>
    public enum AppTheme { Dark, Light }

    public class ThemeService
    {
        private const string DarkBrushesSource  = "/CraneOptimizer.UI;component/Resources/Theme/Dark.Brushes.xaml";
        private const string LightBrushesSource = "/CraneOptimizer.UI;component/Resources/Theme/Light.Brushes.xaml";

        // ── Singleton ─────────────────────────────────────────────────────
        public static ThemeService Instance { get; } = new ThemeService();
        private ThemeService() { }

        // ── Public state ──────────────────────────────────────────────────
        public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

        // ── Apply ─────────────────────────────────────────────────────────
        /// <summary>
        /// Swaps the active surface-brush dictionary in Application.Resources.
        /// Call from UI thread only.
        /// </summary>
        public void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme;

            var mergedDicts = WpfApp.Current.Resources.MergedDictionaries;

            // Remove whichever brush file is currently loaded
            var existing = mergedDicts
                .FirstOrDefault(d => d.Source != null &&
                               (d.Source.OriginalString.EndsWith("Dark.Brushes.xaml",  StringComparison.OrdinalIgnoreCase) ||
                                d.Source.OriginalString.EndsWith("Light.Brushes.xaml", StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
                mergedDicts.Remove(existing);

            // Add the correct one
            string newSource = theme == AppTheme.Dark ? DarkBrushesSource : LightBrushesSource;
            mergedDicts.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri(newSource, UriKind.Relative)
            });
        }

        /// <summary>Toggle between Dark and Light.</summary>
        public void ToggleTheme() =>
            ApplyTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }
}
