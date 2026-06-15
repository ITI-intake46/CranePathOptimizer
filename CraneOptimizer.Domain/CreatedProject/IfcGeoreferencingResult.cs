using System;

namespace CraneOptimizer.Domain.CreatedProject
{
    public sealed record IfcGeoreferencingResult(
        bool HasMapConversion,
        double Eastings,
        double Northings,
        double OrthogonalHeight,
        double? XAxisAbscissa,
        double? XAxisOrdinate,
        double? Scale,
        string? CrsName,
        string? CrsDescription,
        string? EpsgCode,
        bool HasSiteLatLon,
        double? SiteLatitude,
        double? SiteLongitude,
        double? SiteElevation
    );
}
