using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace CraneOptimizer.UI.Core.Helpers
{
    /// <summary>
    /// Correct maximize/restore for windows using WindowStyle=None + AllowsTransparency=True.
    ///
    /// Why this is needed:
    ///   WPF's "AllowsTransparency=True" uses a Win32 layered window (WS_EX_LAYERED).
    ///   In that mode, WM_GETMINMAXINFO is never consulted for the maximized bounds —
    ///   WPF instead asks the OS for the full monitor screen size, bypassing the work area.
    ///   The result is that WindowState.Maximized either covers the taskbar or positions
    ///   the window at an unexpected negative offset that pushes it off-screen.
    ///
    /// Solution:
    ///   1. Never set WindowState = WindowState.Maximized.
    ///   2. Track "is maximized" manually with a flag.
    ///   3. On maximize: save current bounds, read the current monitor's WORK area via
    ///      GetMonitorInfo, convert physical px → WPF DIPs, apply to Left/Top/Width/Height.
    ///   4. On restore: write back saved bounds.
    ///   5. Sync the maximize button icon automatically.
    ///
    /// Usage:
    ///   Call MaximizeTaskbarHelper.Attach(this, MaximizeButton) in the constructor.
    ///   Call MaximizeTaskbarHelper.ToggleMaximize(this) from the maximize button click handler,
    ///   OR bind the button click to the helper via Attach().
    /// </summary>
    public static class MaximizeTaskbarHelper
    {
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ── PInvoke ────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int    cbSize;
            public Rect32 rcMonitor;
            public Rect32 rcWork;
            public uint   dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect32 { public int left, top, right, bottom; }

        // ── Per-window state ───────────────────────────────────────────────

        private sealed class State
        {
            public bool   IsMaximized;
            public double NormalLeft, NormalTop, NormalWidth, NormalHeight;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, State>
            _states = new();

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Attaches the helper to a window.
        /// Optionally pass the maximize <see cref="Button"/> so its Content icon is
        /// kept in sync ("☐" normal / "❐" maximized).
        /// </summary>
        public static void Attach(Window window, Button? maximizeButton = null)
        {
            _states.GetOrCreateValue(window);   // register state entry

            // Sync icon when WPF WindowState changes from external sources (e.g. taskbar)
            window.StateChanged += (_, _) =>
            {
                if (window.WindowState == WindowState.Minimized) return;

                // If WPF somehow set Maximized (e.g. double-click on non-custom title bar),
                // switch it back to Normal and do our custom maximize instead.
                if (window.WindowState == WindowState.Maximized)
                {
                    window.WindowState = WindowState.Normal;
                    ApplyMaximize(window, maximizeButton);
                }
                else
                {
                    // WindowState became Normal from outside — sync icon only
                    if (_states.TryGetValue(window, out var s))
                        s.IsMaximized = false;

                    if (maximizeButton != null)
                        maximizeButton.Content = "☐";
                }
            };
        }

        /// <summary>
        /// Toggles maximize / restore for the given window. Call this from the
        /// maximize button click handler.
        /// </summary>
        public static void ToggleMaximize(Window window, Button? maximizeButton = null)
        {
            if (!_states.TryGetValue(window, out var state))
            {
                state = new State();
                _states.Add(window, state);
            }

            if (state.IsMaximized)
                ApplyRestore(window, state, maximizeButton);
            else
                ApplyMaximize(window, maximizeButton);
        }

        // ── Private implementation ─────────────────────────────────────────

        private static void ApplyMaximize(Window window, Button? btn)
        {
            if (!_states.TryGetValue(window, out var state))
            {
                state = new State();
                _states.Add(window, state);
            }

            // Save current bounds before maximizing
            state.NormalLeft   = window.Left;
            state.NormalTop    = window.Top;
            state.NormalWidth  = window.Width;
            state.NormalHeight = window.Height;
            state.IsMaximized  = true;

            // Get the work area of the monitor nearest to this window
            var wa = GetWorkArea(window);

            window.Left   = wa.Left;
            window.Top    = wa.Top;
            window.Width  = wa.Width;
            window.Height = wa.Height;

            if (btn != null) btn.Content = "❐";
        }

        private static void ApplyRestore(Window window, State state, Button? btn)
        {
            state.IsMaximized = false;

            window.Left   = state.NormalLeft;
            window.Top    = state.NormalTop;
            window.Width  = state.NormalWidth;
            window.Height = state.NormalHeight;

            if (btn != null) btn.Content = "☐";
        }

        /// <summary>
        /// Returns the WPF DIP-space work area of the monitor nearest to the window.
        /// Multi-monitor aware. Falls back to SystemParameters.WorkArea on failure.
        /// </summary>
        private static Rect GetWorkArea(Window window)
        {
            try
            {
                var hwnd    = new WindowInteropHelper(window).Handle;
                var hMon    = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var info    = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };

                if (hMon != IntPtr.Zero && GetMonitorInfo(hMon, ref info))
                {
                    // MonitorInfo returns physical pixels — convert to WPF DIPs
                    var src = HwndSource.FromHwnd(hwnd);
                    if (src?.CompositionTarget != null)
                    {
                        var m   = src.CompositionTarget.TransformFromDevice;
                        var tl  = m.Transform(new Point(info.rcWork.left,  info.rcWork.top));
                        var br  = m.Transform(new Point(info.rcWork.right, info.rcWork.bottom));
                        return new Rect(tl, br);
                    }

                    // No DPI transform available — use raw values (1× DPI assumed)
                    return new Rect(
                        info.rcWork.left,
                        info.rcWork.top,
                        info.rcWork.right  - info.rcWork.left,
                        info.rcWork.bottom - info.rcWork.top);
                }
            }
            catch { /* fall through */ }

            // Ultimate fallback: primary monitor work area
            return SystemParameters.WorkArea;
        }
    }
}
