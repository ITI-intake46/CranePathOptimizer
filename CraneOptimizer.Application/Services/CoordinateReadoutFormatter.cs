using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;

namespace CraneOptimizer.Application.Services
{
    public sealed class CoordinateReadoutFormatter : ICoordinateReadoutFormatter
    {
        private readonly IProjectCoordinateTransformService _transformService;

        public CoordinateReadoutFormatter(IProjectCoordinateTransformService transformService)
        {
            _transformService = transformService;
        }

        public string FormatModelPoint(DomainPoint3D point)
        {
            return $"X: {point.X:F3}, Y: {point.Y:F3}, Z: {point.Z:F3}";
        }

        public string FormatSitePoint(DomainPoint3D point)
        {
            var sitePoint = _transformService.LocalToSurvey(point);
            return $"E: {sitePoint.X:F3}, N: {sitePoint.Y:F3}, H: {sitePoint.Z:F3}";
        }

        public string FormatCombinedPoint(DomainPoint3D point)
        {
            var localStr = FormatModelPoint(point);
            if (!_transformService.IsEnabled)
            {
                return localStr;
            }
            var siteStr = FormatSitePoint(point);
            return $"{localStr} | {siteStr}";
        }

        public string BuildCoordinateReadout(DomainPoint3D point)
        {
            if (!_transformService.IsEnabled)
            {
                return $"Model: {FormatModelPoint(point)}";
            }
            return $"Model: {FormatModelPoint(point)} | Site: {FormatSitePoint(point)}";
        }
    }
}
