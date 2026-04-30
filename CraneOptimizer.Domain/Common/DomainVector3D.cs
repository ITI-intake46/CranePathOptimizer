namespace CraneOptimizer.Domain.Common
{
    public class DomainVector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public DomainVector3D() { }

        public DomainVector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
