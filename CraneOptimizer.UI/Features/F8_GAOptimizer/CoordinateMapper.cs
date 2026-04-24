using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer
{
    public static class CoordinateMapper
    {
        public const double RenderScale = 0.001;

        public static Point3D ToRenderPoint(double x, double y, double z = 0)
        {
            return new Point3D(
                x * RenderScale,
                y * RenderScale,
                z * RenderScale);
        }

        public static double ToRenderLength(double length)
        {
            return length * RenderScale;
        }

        public static double ToModelMillimeters(double renderLength)
        {
            return renderLength / RenderScale;
        }
    }
}
