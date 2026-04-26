using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using HelixToolkit.Wpf;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Views
{
    public partial class IfcViewerControl : UserControl
    {
        private IRenderingService? _renderingService;
        private ISceneService? _sceneService;
        private ISelectionService? _selectionService;

        public IfcViewerControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            DataContextChanged += (s, e) =>
            {
                if (VM != null)
                {
                    VM.ViewportChildren = IfcViewer.Children;
                    VM.Camera = IfcViewer.Camera as PerspectiveCamera;
                }
            };
        }

        private IfcViewerViewModel? VM => DataContext as IfcViewerViewModel;

        private bool _isInitialized;

        public void Initialize(IRenderingService renderingService, ISceneService sceneService)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _renderingService = renderingService;
            _sceneService = sceneService;
            _selectionService = (App.ServiceProvider.GetService(typeof(ISelectionService)) as ISelectionService);

            _renderingService.Initialize(IfcViewer);

            if (VM != null)
            {
                VM.ViewportChildren = IfcViewer.Children;
                VM.Camera = IfcViewer.Camera as PerspectiveCamera;
            }

            if (_selectionService != null)
            {
                // UI logic for scrolling is handled in ElementManagementControl.xaml.cs
            }
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
                // Right click logic
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            // FIX-P5: Element selection via hit-test
            var hits = IfcViewer.Viewport.FindHits(e.GetPosition(IfcViewer));
            Visual3D? hitVisual = hits.FirstOrDefault()?.Visual;

            PrecastElement? hitElement = null;
            if (hitVisual != null && VM != null)
            {
                VM.ElementVisuals.TryGetValue(hitVisual, out hitElement);
            }

            if (hitElement != null)
            {
                _selectionService?.Select(hitElement);
                e.Handled = true;
            }
            else
            {
                // Fallback to coordinates for placement interactions
                var screen = e.GetPosition(IfcViewer);
                var world = GetWorldPoint(IfcViewer, screen);
                VM?.ViewportClickCommand?.Execute(new DomainPoint3D(world.X, world.Y, world.Z));
                
                // Clear selection if background clicked
                if (!hits.Any())
                    _selectionService?.Select(null);
            }
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