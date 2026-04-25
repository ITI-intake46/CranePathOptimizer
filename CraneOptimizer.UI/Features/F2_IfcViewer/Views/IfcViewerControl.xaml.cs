using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Views
{
    public partial class IfcViewerControl : UserControl
    {
        private IRenderingService? _renderingService;
        private ISceneService? _sceneService;

        public IfcViewerControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private IfcViewerViewModel? VM => DataContext as IfcViewerViewModel;

        private bool _isInitialized;

        public void Initialize(IRenderingService renderingService, ISceneService sceneService)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _renderingService = renderingService;
            _sceneService = sceneService;

            _renderingService.Initialize(IfcViewer);

            // Subscribe to events
            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneUpdated += OnZoneUpdated;
            _sceneService.ZoneRemoved += OnZoneRemoved;
            _sceneService.ZonesCleared += OnZonesCleared;
            _sceneService.ZoneSelected += OnZoneUpdated;

            RenderAllZones();
        }

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialization is now done via Initialize()
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_sceneService == null)
                return;

            // Prevent memory leaks
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneUpdated -= OnZoneUpdated;
            _sceneService.ZoneRemoved -= OnZoneRemoved;
            _sceneService.ZonesCleared -= OnZonesCleared;
            _sceneService.ZoneSelected -= OnZoneUpdated;
        }

        #endregion

        #region Zones Rendering

        private void OnZoneAdded(GreenAreaZone zone) => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_renderingService == null || _sceneService == null) return;
            _renderingService.AddOrUpdate(zone, new ZoneVisualBuilder(_sceneService), "Zones");
        });

        private void OnZoneUpdated(GreenAreaZone zone) => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_renderingService == null || _sceneService == null) return;
            _renderingService.AddOrUpdate(zone, new ZoneVisualBuilder(_sceneService), "Zones");
        });

        private void OnZoneRemoved(GreenAreaZone zone) => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_renderingService == null) return;
            _renderingService.Remove(zone, "Zones");
        });

        private void OnZonesCleared() => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_renderingService == null) return;
            _renderingService.ClearLayer("Zones");
        });

        private void RenderAllZones() => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_renderingService == null || _sceneService == null) return;
            _renderingService.Render(_sceneService.GetZones(), new ZoneVisualBuilder(_sceneService), "Zones");
        });

        #endregion

        #region Mouse Interaction

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var screen = e.GetPosition(IfcViewer);
            var world = GetWorldPoint(IfcViewer, screen);

            VM?.ViewportMoveCommand?.Execute(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                // We can pass null or handle right click differently if needed, 
                // but for now let's just not handle it here or we pass it to a different command.
                // Or we can just let CurrentInteraction handle it if we modify it.
                // We'll pass the point on right click too, or we can just ignore it for now.
                // Let's pass the point to the ViewportClickCommand anyway.
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            var screen = e.GetPosition(IfcViewer);
            var world = GetWorldPoint(IfcViewer, screen);

            VM?.ViewportClickCommand?.Execute(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
        }

        #endregion

        #region Helpers

        private Point3D GetWorldPoint(HelixViewport3D viewport, Point screenPt)
        {
            try
            {
                var hit = viewport.FindNearestPoint(screenPt);
                if (hit.HasValue)
                    return hit.Value;
            }
            catch { }

            try
            {
                bool ok = Viewport3DHelper.Point2DtoPoint3D(
                    viewport.Viewport,
                    screenPt,
                    out Point3D near,
                    out Point3D far);

                if (ok)
                {
                    var dir = far - near;

                    if (System.Math.Abs(dir.Z) > 1e-9)
                    {
                        double t = -near.Z / dir.Z;
                        if (t > 0)
                            return near + t * dir;
                    }
                }
            }
            catch { }

            return new Point3D();
        }

        #endregion
    }
}