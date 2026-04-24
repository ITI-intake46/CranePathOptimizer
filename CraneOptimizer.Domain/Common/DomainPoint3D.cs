namespace CraneOptimizer.Domain.Common
{
    public class DomainPoint3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public DomainPoint3D() { }
        public DomainPoint3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
