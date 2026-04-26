using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Spatial;
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
            ICraneRepository craneRepository,
            CoordinateService coordinateService)
        {
            _sceneService = sceneService;
            _coordinateService = coordinateService;
        }

        public async Task<PlacementInput> BuildAsync(GASettings settings, Crane selectedCrane)
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
            var allowedZones = new List<GreenAreaZone>();
            var forbiddenZones = new List<GreenAreaZone>();

            for (int i = 0; i < sceneZones.Count; i++)
            {
                var zone = sceneZones[i];
                var normalized = _coordinateService.NormalizeZoneToMeters(zone);
                
                if (zone.Type == ZoneType.Allowed)
                {
                    ValidateZone(normalized);
                    allowedZones.Add(normalized);
                }
                else if (zone.Type == ZoneType.Forbidden)
                {
                    ValidateZone(normalized);
                    forbiddenZones.Add(normalized);
                }
            }

            if (allowedZones.Count == 0)
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

            var centers = elements.Select(e => e.ElementCOG).ToList();
            var weights = elements.Select(e => e.ElementWT ?? 0).ToList();

            return new PlacementInput
            {
                AllowedZones = allowedZones,
                ForbiddenZones = forbiddenZones,
                Elements = elements,
                ElementCenters = centers,
                ElementWeights = weights,
                SpatialIndex = new SpatialGridIndex(centers, cellSize: 10.0),
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
    }
}
