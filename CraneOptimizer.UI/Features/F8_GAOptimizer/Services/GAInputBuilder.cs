using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAInputBuilder
    {
        private readonly ISceneService _sceneService;
        private readonly ICraneRepository _craneRepository;
        private readonly CoordinateService _coordinateService;

        public GAInputBuilder(
            ISceneService sceneService,
            ICraneRepository craneRepository,
            CoordinateService coordinateService)
        {
            _sceneService = sceneService;
            _craneRepository = craneRepository;
            _coordinateService = coordinateService;
        }

        public async Task<PlacementInput> BuildAsync(GASettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

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

            for (int i = 0; i < sceneZones.Count; i++)
            {
                var zone = sceneZones[i];
                if (zone.Type != ZoneType.Allowed)
                    continue;

                ValidateZone(zone);
                zones.Add(_coordinateService.NormalizeZoneToMeters(zone));
            }

            if (zones.Count == 0)
                throw new InvalidOperationException("No allowed zones found in the scene.");

            // ***************************** CODE *****************************
            var cranes = await _craneRepository.GetAllWithSpecsAsync();
            if (cranes == null || cranes.Count == 0)
                throw new InvalidOperationException("No crane found in the database.");

            var selectedCrane = cranes[0];
            var craneSpec = SelectCraneSpecification(selectedCrane);
            // ***************************** CODE *****************************

            if (craneSpec == null)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' does not contain a valid specification.");

            if (craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' does not contain a load chart.");

            if (craneSpec.MaxRadius <= 0 || craneSpec.MaxBoomLength <= 0)
                throw new InvalidOperationException($"Crane '{selectedCrane.Name}' contains invalid specification limits.");

            if (settings.MaxRadiusForNormalisation <= 0)
                settings.MaxRadiusForNormalisation = craneSpec.MaxRadius;

            return new PlacementInput
            {
                Zones = zones,
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
    }
}
