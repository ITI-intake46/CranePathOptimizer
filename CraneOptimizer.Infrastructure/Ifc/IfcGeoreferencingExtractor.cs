using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc;

namespace CraneOptimizer.Infrastructure.Ifc
{
    public sealed class IfcGeoreferencingExtractor : IIfcGeoreferencingExtractor
    {
        public IfcGeoreferencingResult? ExtractFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                return null;
            }
            try
            {
                using (var model = IfcStore.Open(filePath))
                {
                    return Extract(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IfcGeoreferencing] Exception during ExtractFromFile for '{filePath}': {ex}");
                return null;
            }
        }

        public IfcGeoreferencingResult? Extract(object model)
        {
            if (model == null)
            {
                return null;
            }

            if (!(model is IModel xbimModel))
            {
                throw new ArgumentException("Model must be an xBIM IModel instance.", nameof(model));
            }

            var mapConversion = xbimModel.Instances.OfType<IIfcMapConversion>().FirstOrDefault();
            var crs = xbimModel.Instances.OfType<IIfcCoordinateReferenceSystem>().FirstOrDefault();
            var site = xbimModel.Instances.OfType<IIfcSite>().FirstOrDefault();

            bool hasMapConversion = mapConversion != null;
            double eastings = 0.0;
            double northings = 0.0;
            double orthogonalHeight = 0.0;
            double? xAxisAbscissa = null;
            double? xAxisOrdinate = null;
            double? scale = null;

            string? crsName = null;
            string? crsDescription = null;
            string? epsgCode = null;

            if (mapConversion != null)
            {
                eastings = mapConversion.Eastings;
                northings = mapConversion.Northings;
                orthogonalHeight = mapConversion.OrthogonalHeight;
                xAxisAbscissa = mapConversion.XAxisAbscissa;
                xAxisOrdinate = mapConversion.XAxisOrdinate;
                scale = mapConversion.Scale;

                if (mapConversion.TargetCRS != null)
                {
                    crsName = mapConversion.TargetCRS.Name;
                    crsDescription = mapConversion.TargetCRS.Description;
                }
            }

            if (crs != null)
            {
                crsName ??= crs.Name;
                crsDescription ??= crs.Description;
            }

            if (!string.IsNullOrWhiteSpace(crsName))
            {
                // Parse EPSG code, e.g., "EPSG:32637" -> "32637"
                var match = Regex.Match(crsName, @"(?:EPSG:+)??(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    epsgCode = match.Groups[1].Value;
                }
            }

            bool hasSiteLatLon = false;
            double? siteLatitude = null;
            double? siteLongitude = null;
            double? siteElevation = null;

            if (site != null)
            {
                siteElevation = site.RefElevation;
                
                var latDecimal = ConvertCompoundPlaneAngleToDecimal(site.RefLatitude);
                var lonDecimal = ConvertCompoundPlaneAngleToDecimal(site.RefLongitude);

                if (latDecimal.HasValue || lonDecimal.HasValue)
                {
                    hasSiteLatLon = true;
                    siteLatitude = latDecimal;
                    siteLongitude = lonDecimal;
                }
            }

            if (!hasMapConversion && !hasSiteLatLon)
            {
                return null;
            }

            return new IfcGeoreferencingResult(
                HasMapConversion: hasMapConversion,
                Eastings: eastings,
                Northings: northings,
                OrthogonalHeight: orthogonalHeight,
                XAxisAbscissa: xAxisAbscissa,
                XAxisOrdinate: xAxisOrdinate,
                Scale: scale,
                CrsName: crsName,
                CrsDescription: crsDescription,
                EpsgCode: epsgCode,
                HasSiteLatLon: hasSiteLatLon,
                SiteLatitude: siteLatitude,
                SiteLongitude: siteLongitude,
                SiteElevation: siteElevation
            );
        }

        private static double? ConvertCompoundPlaneAngleToDecimal(object? angle)
        {
            if (angle == null)
            {
                return null;
            }

            if (!(angle is System.Collections.IEnumerable enumerable))
            {
                return null;
            }

            var components = new List<double>();
            foreach (var item in enumerable)
            {
                components.Add(Convert.ToDouble(item));
            }

            if (components.Count == 0)
            {
                return null;
            }

            double degrees = components[0];
            double minutes = components.Count > 1 ? components[1] : 0.0;
            double seconds = components.Count > 2 ? components[2] : 0.0;
            double millionths = components.Count > 3 ? components[3] : 0.0;

            double sign = 1.0;
            if (degrees < 0)
            {
                sign = -1.0;
                degrees = -degrees;
            }
            else if (degrees == 0)
            {
                if (minutes < 0)
                {
                    sign = -1.0;
                    minutes = -minutes;
                }
                else if (minutes == 0)
                {
                    if (seconds < 0)
                    {
                        sign = -1.0;
                        seconds = -seconds;
                    }
                    else if (seconds == 0 && millionths < 0)
                    {
                        sign = -1.0;
                        millionths = -millionths;
                    }
                }
            }

            double decimalDegrees = degrees + (minutes / 60.0) + (seconds / 3600.0) + (millionths / 3600000000.0);
            return decimalDegrees * sign;
        }
    }
}
