using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using HelixToolkit.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones.Views
{
    /// <summary>
    /// Interaction logic for ZonesTabView.xaml
    /// </summary>
    public partial class ZonesTabView : UserControl
    {
        public ZonesTabView()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<DrawingViewModel>();
        }
        private Point3D GetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt)
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

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            var screen = e.GetPosition(viewport);
            var world = GetWorldPoint(viewport, screen);

            var point = new DomainPoint3D(world.X, world.Y, world.Z);

            if (DataContext is DrawingViewModel vm)
            {
                if (!vm.IsDrawing) return;

                vm.AddPoint(point);
            }
        }
    }
}
