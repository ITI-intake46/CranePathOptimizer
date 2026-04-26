using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
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
        }

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialization is now done via Initialize()
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

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