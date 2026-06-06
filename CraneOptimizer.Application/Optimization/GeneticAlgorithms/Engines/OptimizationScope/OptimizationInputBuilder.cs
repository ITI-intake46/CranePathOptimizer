using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope
{
    public class OptimizationInputBuilder : IOptimizationInputBuilder
    {
        public PlacementInput Build(
            GASettings settings,
            Crane crane,
            IReadOnlyList<PrecastElement> elements,
            IReadOnlyList<GreenAreaZone> zones)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (crane == null)
                throw new InvalidOperationException("No crane selected.");

            if (elements == null || elements.Count == 0)
                throw new InvalidOperationException("No elements found in the scene.");

            var gaEnabledElements = elements
                .Where(element => element.IncludeInGa)
                .ToList();

            if (gaEnabledElements.Count == 0)
                throw new InvalidOperationException("No GA-enabled elements found in the scene.");

            var selectedElementTypes = CreateElementTypeSet(settings.TargetElementTypes);
            var candidateElements = selectedElementTypes == null
                ? gaEnabledElements
                : gaEnabledElements
                    .Where(element => selectedElementTypes.Contains(NormalizeElementType(element.ElementType)))
                    .ToList();

            if (candidateElements.Count == 0)
                throw new InvalidOperationException("No scene elements match the selected GA target element types.");

            var allowedZones = new List<GreenAreaZone>();
            var forbiddenZones = new List<GreenAreaZone>();

            if (zones != null)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    var zone = zones[i];
                    if (zone.Type != ZoneType.Allowed && zone.Type != ZoneType.Forbidden)
                        continue;

                    ValidateZone(zone);

                    if (zone.Type == ZoneType.Allowed)
                        allowedZones.Add(zone);
                    else
                        forbiddenZones.Add(zone);
                }
            }

            if (allowedZones.Count == 0)
                throw new InvalidOperationException("No allowed zones found in the scene.");

            var craneSpec = SelectCraneSpecification(crane);

            if (craneSpec == null)
                throw new InvalidOperationException($"Crane '{crane.Name}' does not contain a valid specification.");

            if (craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                throw new InvalidOperationException($"Crane '{crane.Name}' does not contain a load chart.");

            if (craneSpec.MaxRadius <= 0 || craneSpec.MaxBoomLength <= 0)
                throw new InvalidOperationException($"Crane '{crane.Name}' contains invalid specification limits.");

            if (settings.MaxRadiusForNormalisation <= 0)
                settings.MaxRadiusForNormalisation = craneSpec.MaxRadius;

            if (craneSpec.DeratingRatio.HasValue
                && craneSpec.DeratingRatio.Value > 0
                && craneSpec.DeratingRatio.Value <= 1.0
                && Math.Abs(settings.DeratingRatio - 0.9) < 0.0001)
            {
                settings.DeratingRatio = craneSpec.DeratingRatio.Value;
            }

            settings.CraneFootprintRadius = CalculateCraneFootprintRadius(crane);

            return new PlacementInput
            {
                Zones = allowedZones,
                ForbiddenZones = forbiddenZones,
                Elements = candidateElements,
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
                var vertex = zone.Vertices[i];
                if (vertex == null
                    || double.IsNaN(vertex.X) || double.IsInfinity(vertex.X)
                    || double.IsNaN(vertex.Y) || double.IsInfinity(vertex.Y)
                    || double.IsNaN(vertex.Z) || double.IsInfinity(vertex.Z))
                {
                    throw new InvalidOperationException($"Allowed zone '{zone.Name}' contains invalid coordinates.");
                }
            }
        }

        private double CalculateCraneFootprintRadius(Crane selectedCrane)
        {
            double maxBodyDimension = Math.Max(selectedCrane.VehicleLength, selectedCrane.VehicleWidth);
            double maxOutriggerDimension = Math.Max(
                selectedCrane.OutriggerLength ?? 0,
                selectedCrane.OutriggerWidth ?? 0);

            double footprintDiameterMeters = Math.Max(maxBodyDimension, maxOutriggerDimension);
            return footprintDiameterMeters / 2.0;
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
