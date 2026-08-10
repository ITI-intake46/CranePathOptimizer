using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Resources
{
    /// <summary>
    /// Keeps native captions on project-owned WPF windows aligned with the
    /// application theme without replacing the operating-system chrome.
    /// </summary>
    internal static class NativeTitleBarTheme
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
            ThemeService.Instance.ThemeChanged += OnThemeChanged;
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window && IsProjectOwned(window))
                Apply(window, ThemeService.Instance.CurrentTheme);
        }

        private static void OnThemeChanged(AppTheme theme)
        {
            System.Windows.Application? application =
                System.Windows.Application.Current;
            if (application == null)
                return;

            void ApplyToOpenWindows()
            {
                foreach (Window window in application.Windows)
                {
                    if (IsProjectOwned(window))
                        Apply(window, theme);
                }
            }

            if (application.Dispatcher.CheckAccess())
                ApplyToOpenWindows();
            else
                _ = application.Dispatcher.InvokeAsync(
                    ApplyToOpenWindows,
                    DispatcherPriority.Normal);
        }

        private static bool IsProjectOwned(Window window)
        {
            string? assemblyName = window.GetType().Assembly.GetName().Name;
            return assemblyName?.StartsWith(
                "CraneOptimizer.",
                StringComparison.Ordinal) == true;
        }

        private static void Apply(Window window, AppTheme theme)
        {
            nint handle = new WindowInteropHelper(window).Handle;
            if (handle == nint.Zero)
                return;

            int enabled = theme == AppTheme.Dark ? 1 : 0;
            int result = DwmSetWindowAttribute(
                handle,
                DwmUseImmersiveDarkMode,
                ref enabled,
                sizeof(int));
            if (result < 0)
            {
                _ = DwmSetWindowAttribute(
                    handle,
                    DwmUseImmersiveDarkModeBefore20H1,
                    ref enabled,
                    sizeof(int));
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            nint windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
