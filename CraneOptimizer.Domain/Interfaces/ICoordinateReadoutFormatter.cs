using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface ICoordinateReadoutFormatter
    {
        string FormatModelPoint(DomainPoint3D point);
        string FormatSitePoint(DomainPoint3D point);
        string FormatCombinedPoint(DomainPoint3D point);
        string BuildCoordinateReadout(DomainPoint3D point);
    }
}
