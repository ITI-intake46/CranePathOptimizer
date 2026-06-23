using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F12_Reports.Views;

internal sealed class ReportWindowBoundsHelper
{
    private static readonly DependencyProperty InstanceProperty =
        DependencyProperty.RegisterAttached(
            "Instance",
            typeof(ReportWindowBoundsHelper),
            typeof(ReportWindowBoundsHelper),
            new PropertyMetadata(null));

    private readonly Window _window;
    private HwndSource? _source;

    private ReportWindowBoundsHelper(Window window)
    {
        _window = window;
    }

    public static void Attach(Window window)
    {
        var helper = new ReportWindowBoundsHelper(window);
        window.SetValue(InstanceProperty, helper);
        helper.Attach();
    }

    private void Attach()
    {
        _window.SourceInitialized += OnSourceInitialized;
        _window.StateChanged += OnStateChanged;
        _window.SizeChanged += OnSizeChanged;
        _window.Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
        _source?.AddHook(WindowProc);
        QueueFullMonitorBounds();
    }

    private void OnStateChanged(object? sender, EventArgs e) => QueueFullMonitorBounds();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => QueueFullMonitorBounds();

    private void OnClosed(object? sender, EventArgs e)
    {
        _window.SourceInitialized -= OnSourceInitialized;
        _window.StateChanged -= OnStateChanged;
        _window.SizeChanged -= OnSizeChanged;
        _window.Closed -= OnClosed;
        _source?.RemoveHook(WindowProc);
        _source = null;
    }

    private IntPtr WindowProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMonitorMaximizeBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void QueueFullMonitorBounds()
    {
        if (_window.WindowState != WindowState.Maximized)
            return;

        _window.Dispatcher.BeginInvoke(
            new Action(ForceFullMonitorBounds),
            DispatcherPriority.ApplicationIdle);
    }

    private void ForceFullMonitorBounds()
    {
        if (_window.WindowState != WindowState.Maximized)
            return;

        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        if (!TryGetMonitorArea(hwnd, out var monitorArea))
            return;

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            monitorArea.Left,
            monitorArea.Top,
            monitorArea.Right - monitorArea.Left,
            monitorArea.Bottom - monitorArea.Top,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow | SwpFrameChanged);
    }

    private static void ApplyMonitorMaximizeBounds(IntPtr hwnd, IntPtr lParam)
    {
        if (!TryGetMonitorArea(hwnd, out var monitorArea))
            return;

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        minMaxInfo.MaxPosition.X = monitorArea.Left;
        minMaxInfo.MaxPosition.Y = monitorArea.Top;
        minMaxInfo.MaxSize.X = monitorArea.Right - monitorArea.Left;
        minMaxInfo.MaxSize.Y = monitorArea.Bottom - monitorArea.Top;
        minMaxInfo.MaxTrackSize.X = minMaxInfo.MaxSize.X;
        minMaxInfo.MaxTrackSize.Y = minMaxInfo.MaxSize.Y;

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private static bool TryGetMonitorArea(IntPtr hwnd, out Rect monitorArea)
    {
        monitorArea = default;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return false;

        var monitorInfo = new MonitorInfo
        {
            CbSize = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return false;

        monitorArea = monitorInfo.Monitor;
        return true;
    }

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
