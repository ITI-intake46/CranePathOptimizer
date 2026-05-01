using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    public class CraneVisualBuilder : IVisualBuilder<SceneCrane>
    {
        public Visual3D Build(SceneCrane item)
        {
            var group = new ModelVisual3D();

            var vehicleLength = item.CraneDefinition.VehicleLength;
            var vehicleWidth = item.CraneDefinition.VehicleWidth;
            var vehicleHeight = item.CraneDefinition.VehicleHeight > 0 ? item.CraneDefinition.VehicleHeight : 1.0;

            var centerX = item.Position.X;
            var centerY = item.Position.Y;
            var baseZ = item.Position.Z;

            var bodyColor = item.IsValidPlacement ? Color.FromRgb(53, 132, 228) : Color.FromRgb(220, 53, 69);

            var truck = new BoxVisual3D
            {
                Width = vehicleWidth,
                Length = vehicleLength,
                Height = vehicleHeight,
                Fill = new SolidColorBrush(bodyColor),
                Center = new Point3D(centerX, centerY, baseZ + vehicleHeight / 2.0)
            };

            truck.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), item.Rotation),
                new Point3D(centerX, centerY, baseZ));

            var pivot = item.GetBoomPivotWorld();
            var tip = item.GetBoomTipWorld();

            var boom = new PipeVisual3D
            {
                Point1 = new Point3D(pivot.X, pivot.Y, pivot.Z),
                Point2 = new Point3D(tip.X, tip.Y, tip.Z),
                Diameter = 0.25,
                Fill = Brushes.Goldenrod
            };

            var pivotMarker = new SphereVisual3D
            {
                Center = new Point3D(pivot.X, pivot.Y, pivot.Z),
                Radius = 0.2,
                Fill = Brushes.OrangeRed
            };

            truck.SetValue(System.Windows.FrameworkElement.TagProperty, item);
            boom.SetValue(System.Windows.FrameworkElement.TagProperty, item);
            pivotMarker.SetValue(System.Windows.FrameworkElement.TagProperty, item);

            group.Children.Add(truck);
            group.Children.Add(boom);
            group.Children.Add(pivotMarker);
            group.SetValue(System.Windows.FrameworkElement.TagProperty, item);

            return group;
        }
    }
}
