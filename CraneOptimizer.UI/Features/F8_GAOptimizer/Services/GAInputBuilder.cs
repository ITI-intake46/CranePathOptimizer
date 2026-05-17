using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAInputBuilder
    {
        private readonly ISceneService _sceneService;
        private readonly CoordinateService _coordinateService;

        public GAInputBuilder(
            ISceneService sceneService,
            CoordinateService coordinateService)
        {
            _sceneService = sceneService;
            _coordinateService = coordinateService;
        }

        public Task<PlacementInput> BuildAsync(GASettings settings, Crane selectedCrane)
        {
            return Task.FromResult(Build(settings, selectedCrane));
        }

        private PlacementInput Build(GASettings settings, Crane selectedCrane)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (selectedCrane == null)
                throw new InvalidOperationException("No crane selected.");

            var sceneElements = _sceneService.GetElements();
            if (sceneElements == null || sceneElements.Count == 0)
                throw new InvalidOperationException("No elements found in the scene.");

            var elements = new List<PrecastElement>(sceneElements.Count);
            for (int i = 0; i < sceneElements.Count; i++)
            {
                var element = sceneElements[i];

                if (!_coordinateService.IsValidPoint(element.ElementCOG))
                    throw new InvalidOperationException($"Invalid coordinates found for element '{element.ElementID}'.");

                elements.Add(_coordinateService.NormalizeElementToMeters(element));
            }

            var sceneZones = _sceneService.GetZones();
            var zones = new List<GreenAreaZone>(sceneZones.Count);
            var forbiddenZones = new List<GreenAreaZone>();

            for (int i = 0; i < sceneZones.Count; i++)
            {
                var zone = sceneZones[i];
                if (zone.Type != ZoneType.Allowed && zone.Type != ZoneType.Forbidden)
                    continue;

                ValidateZone(zone);
                var normalizedZone = _coordinateService.NormalizeZoneToMeters(zone);
                if (zone.Type == ZoneType.Allowed)
                    zones.Add(normalizedZone);
                else
                    forbiddenZones.Add(normalizedZone);
            }

            if (zones.Count == 0)
                throw new InvalidOperationException("No allowed zones found in the scene.");

            //  FIX: use selected crane from UI
            var craneSpec = SelectCraneSpecification(selectedCrane);

            if (craneSpec == null)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' does not contain a valid specification.");

            if (craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' does not contain a load chart.");

            if (craneSpec.MaxRadius <= 0 || craneSpec.MaxBoomLength <= 0)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' contains invalid specification limits.");

            if (settings.MaxRadiusForNormalisation <= 0)
                settings.MaxRadiusForNormalisation = craneSpec.MaxRadius;

            if (craneSpec.DeratingRatio.HasValue
                && craneSpec.DeratingRatio.Value > 0
                && craneSpec.DeratingRatio.Value <= 1.0
                && Math.Abs(settings.DeratingRatio - 0.9) < 0.0001)
            {
                settings.DeratingRatio = craneSpec.DeratingRatio.Value;
            }

            settings.CraneFootprintRadius = CalculateCraneFootprintRadius(selectedCrane);

            if (!HasValidCraneSetupPoint(zones, forbiddenZones, settings))
            {
                throw new InvalidOperationException(
                    "No valid crane setup point was found inside the allowed green area. " +
                    "The allowed zone may be too small/narrow for the selected crane footprint, " +
                    "or it may be blocked by forbidden zones. Draw a larger allowed zone or choose a smaller crane.");
            }

            return new PlacementInput
            {
                Zones = zones,
                ForbiddenZones = forbiddenZones,
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings
            };
        }

        private CraneSpecification? SelectCraneSpecification(Crane crane)
        {
            CraneSpecification? firstSpec = null;

            foreach (var specification in crane.Specifications)
            {
                if (specification == null)
                    continue;

                firstSpec ??= specification;

                if (specification.LoadChart != null && specification.LoadChart.Count > 0)
                    return specification;
            }

            return firstSpec;
        }

        private void ValidateZone(GreenAreaZone zone)
        {
            if (zone.Vertices == null || zone.Vertices.Count < 3)
                throw new InvalidOperationException($"Allowed zone '{zone.Name}' is invalid.");

            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                if (!_coordinateService.IsValidPoint(zone.Vertices[i]))
                    throw new InvalidOperationException($"Allowed zone '{zone.Name}' contains invalid coordinates.");
            }
        }

        /// <summary>
        /// Calculates a simplified circular crane footprint in meters from the selected crane.
        /// The input crane dimensions are stored in millimeters; the output uses the larger
        /// of vehicle body size and outrigger span so GA setup points keep practical clearance
        /// from allowed-zone boundaries and forbidden zones.
        /// </summary>
        private double CalculateCraneFootprintRadius(Crane selectedCrane)
        {
            double maxBodyDimension = Math.Max(selectedCrane.VehicleLength, selectedCrane.VehicleWidth);
            double maxOutriggerDimension = Math.Max(
                selectedCrane.OutriggerLength ?? 0,
                selectedCrane.OutriggerWidth ?? 0);

            double footprintDiameterMillimeters = Math.Max(maxBodyDimension, maxOutriggerDimension);
            return _coordinateService.NormalizeLength(footprintDiameterMillimeters) / 2.0;
        }

        private static bool HasValidCraneSetupPoint(
            IReadOnlyList<GreenAreaZone> zones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            foreach (var zone in zones)
            {
                if (zone.Vertices.Count < 3)
                    continue;

                var centroid = new Domain.Common.DomainPoint3D(
                    zone.Vertices.Average(vertex => vertex.X),
                    zone.Vertices.Average(vertex => vertex.Y),
                    0);

                if (CranePlacementConstraintService.IsValidSetupPoint(centroid, zones, forbiddenZones, settings))
                    return true;

                if (HasValidSamplePoint(zone, zones, forbiddenZones, settings))
                    return true;
            }

            return false;
        }

        private static bool HasValidSamplePoint(
            GreenAreaZone zone,
            IReadOnlyList<GreenAreaZone> zones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            var minX = zone.Vertices.Min(vertex => vertex.X);
            var maxX = zone.Vertices.Max(vertex => vertex.X);
            var minY = zone.Vertices.Min(vertex => vertex.Y);
            var maxY = zone.Vertices.Max(vertex => vertex.Y);

            const int samplesPerAxis = 7;
            for (int ix = 1; ix < samplesPerAxis; ix++)
            {
                var x = minX + (maxX - minX) * ix / samplesPerAxis;
                for (int iy = 1; iy < samplesPerAxis; iy++)
                {
                    var y = minY + (maxY - minY) * iy / samplesPerAxis;
                    var point = new Domain.Common.DomainPoint3D(x, y, 0);
                    if (CranePlacementConstraintService.IsValidSetupPoint(point, zones, forbiddenZones, settings))
                        return true;
                }
            }

            return false;
        }
    }
}
