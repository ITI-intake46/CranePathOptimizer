using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using DomainPoint = CraneOptimizer.Domain.Common.Point3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        public MainWindow(MainViewModel vm, IRenderingService render)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = vm;

            render.Initialize(Viewport);
        }
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_vm.Drawing.IsDrawing) return;

            // ✅ RightClick يقفل الزون بغض النظر عن أي نقطة
            if (e.ChangedButton == MouseButton.Right)
            {
                _vm.Drawing.Finish();
                e.Handled = true;
                return;
            }

            // LeftClick يضيف نقطة
            if (e.ChangedButton == MouseButton.Left)
            {
                var pos = e.GetPosition(Viewport);
                var point = GetGroundPoint(pos);
                if (point == null) return;

                _vm.Drawing.AddPoint(new DomainPoint(point.Value.X, point.Value.Y, 0));
                e.Handled = true;
            }
        }

        // تحويل الماوس → نقطة على الأرض (Z=0)
        private Point3D? GetGroundPoint(System.Windows.Point mouse)
        {
            var ray = Viewport3DHelper.Point2DtoRay3D(Viewport.Viewport, mouse);

            var origin = ray.Origin;
            var dir = ray.Direction;

            if (Math.Abs(dir.Z) < 0.0001)
                return null;

            double t = -origin.Z / dir.Z;

            if (t < 0) return null;

            return new Point3D(
                origin.X + dir.X * t,
                origin.Y + dir.Y * t,
                0);
        }

    }
}