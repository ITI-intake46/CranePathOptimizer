using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using HelixToolkit.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones.Views
{
    /// <summary>
    /// Code-behind for ZonesTabView.
    /// Responsibility: thin input forwarding ONLY.
    ///   Mouse / keyboard events → ViewModel methods.
    ///   All logic lives in DrawingViewModel.
    /// </summary>
    public partial class ZonesTabView : UserControl
    {
        private HelixViewport3D? _viewport;

        public ZonesTabView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            DataContext = App.ServiceProvider.GetRequiredService<DrawingViewModel>();

            Loaded += ZonesTabView_Loaded;
            Unloaded += ZonesTabView_Unloaded;
        }

        private void ZonesTabView_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(this);

            _viewport = FindViewport();
            if (_viewport != null)
            {
                // Use PreviewMouseDown instead of MouseDown
                _viewport.PreviewMouseDown -= Viewport_PreviewMouseDown;
                _viewport.PreviewMouseDown += Viewport_PreviewMouseDown;

                _viewport.MouseMove -= Viewport_MouseMove;
                _viewport.MouseMove += Viewport_MouseMove;

                _viewport.MouseDoubleClick -= Viewport_MouseDoubleClick;
                _viewport.MouseDoubleClick += Viewport_MouseDoubleClick;

                System.Diagnostics.Debug.WriteLine("✅ PreviewMouseDown attached");
            }
        }
        private void Viewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not DrawingViewModel vm) return;

            if (e.ChangedButton == MouseButton.Right)
            {
                System.Diagnostics.Debug.WriteLine("→ Preview Right Click");

                if (vm.IsDrawing)
                {
                    vm.Finish();
                    e.Handled = true;
                    return;
                }
                else if (vm.IsMeasuring)
                {
                    vm.ResetMeasurement();
                    e.Handled = true;
                    return;
                }
            }

            // Left click logic (keep original if needed)
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                // Call your existing left click logic here if needed
            }
        }

        private void ZonesTabView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewport != null)
            {
                _viewport.MouseDown -= Viewport_MouseDown;
                _viewport.MouseMove -= Viewport_MouseMove;
                _viewport.MouseDoubleClick -= Viewport_MouseDoubleClick;
            }
        }
        // ════════════════════════════════════════════════════════════════════
        // Find HelixViewport3D in visual tree
        // ════════════════════════════════════════════════════════════════════

        private HelixViewport3D? FindViewport()
        {
            var parent = VisualParent as FrameworkElement;
            while (parent != null)
            {
                if (parent is Window window)
                {
                    return FindViewportInTree(window);
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent) as FrameworkElement;
            }
            return null;
        }

        private static HelixViewport3D? FindViewportInTree(DependencyObject obj)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is HelixViewport3D viewport)
                    return viewport;

                var found = FindViewportInTree(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // World-space hit-test helper
        // ════════════════════════════════════════════════════════════════════

        private Point3D GetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt)
        {
            // 1. Try ray-cast against scene geometry.
            try
            {
                var hit = viewport.FindNearestPoint(screenPt);
                if (hit.HasValue) return hit.Value;
            }
            catch { }

            // 2. Fall back: unproject onto Z = 0 plane.
            try
            {
                bool ok = Viewport3DHelper.Point2DtoPoint3D(
                    viewport.Viewport, screenPt,
                    out Point3D near, out Point3D far);

                if (ok)
                {
                    var dir = far - near;
                    if (System.Math.Abs(dir.Z) > 1e-9)
                    {
                        double t = -near.Z / dir.Z;
                        if (t > 0) return near + t * dir;
                    }
                }
            }
            catch { }

            return new Point3D();
        }

        // ════════════════════════════════════════════════════════════════════
        // Keyboard
        // ════════════════════════════════════════════════════════════════════

        private void ZonesTabView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is DrawingViewModel vm)
                vm.HandleKeyDown(e);
        }

        // ════════════════════════════════════════════════════════════════════
        // Mouse — click
        // ════════════════════════════════════════════════════════════════════

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not HelixViewport3D viewport) return;
            if (DataContext is not DrawingViewModel vm) return;

            System.Diagnostics.Debug.WriteLine($"[MouseDown] Button={e.ChangedButton} | IsDrawing={vm.IsDrawing} | IsMeasuring={vm.IsMeasuring}");

            // RIGHT CLICK - أعلى أولوية
            if (e.ChangedButton == MouseButton.Right)
            {
                System.Diagnostics.Debug.WriteLine("→ Right Click Detected");

                if (vm.IsDrawing)
                {
                    System.Diagnostics.Debug.WriteLine("→ Finishing Zone...");
                    vm.Finish();
                    e.Handled = true;
                    return;
                }
                else if (vm.IsMeasuring)
                {
                    vm.ResetMeasurement();
                    e.Handled = true;
                    return;
                }
                else
                {
                    // Right click outside drawing mode (optional: can be used for other things later)
                    System.Diagnostics.Debug.WriteLine("→ Right Click outside drawing/measuring");
                }
            }

            // LEFT CLICK
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                bool shiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                var screen = e.GetPosition(viewport);
                var world = GetWorldPoint(viewport, screen);
                vm.HandleViewportLeftClick(new DomainPoint3D(world.X, world.Y, world.Z), shiftDown);
                e.Handled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Mouse — move (hover preview + snap indicator)
        // ════════════════════════════════════════════════════════════════════

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not HelixViewport3D viewport) return;
            if (DataContext is not DrawingViewModel vm) return;

            // Nothing to do if neither mode is active
            if (!vm.IsDrawing && !vm.IsMeasuring) return;

            bool shiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            var screen = e.GetPosition(viewport);
            var world = GetWorldPoint(viewport, screen);
            var pt = new DomainPoint3D(world.X, world.Y, world.Z);

            if (vm.IsDrawing)
                vm.SetHoverPoint(pt, shiftDown);

            if (vm.IsMeasuring)
                vm.SetMeasureHoverPoint(pt, shiftDown);
        }

        // ════════════════════════════════════════════════════════════════════
        // Mouse — double-click (finish shortcut)
        // ════════════════════════════════════════════════════════════════════

        private void Viewport_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (DataContext is DrawingViewModel vm && vm.IsDrawing)
            {
                vm.HandleDoubleClick();
                e.Handled = true;
            }
        }
    }
}