using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
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

            var selectedElementTypes = CreateElementTypeSet(settings.TargetElementTypes);
            var candidateElements = selectedElementTypes == null
                ? sceneElements
                : sceneElements
                    .Where(element => selectedElementTypes.Contains(NormalizeElementType(element.ElementType)))
                    .ToList();

            if (candidateElements.Count == 0)
                throw new InvalidOperationException("No scene elements match the selected GA target element types.");

            var elements = new List<PrecastElement>(candidateElements.Count);
            for (int i = 0; i < candidateElements.Count; i++)
            {
                var element = candidateElements[i];

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

        private static HashSet<string>? CreateElementTypeSet(IReadOnlyCollection<string>? elementTypes)
        {
            if (elementTypes == null || elementTypes.Count == 0)
                return null;

            var result = elementTypes
                .Select(NormalizeElementType)
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return result.Count == 0 ? null : result;
        }

        private static string NormalizeElementType(string? elementType)
        {
            return string.IsNullOrWhiteSpace(elementType)
                ? "Unknown"
                : elementType.Trim();
        }
    }
}
