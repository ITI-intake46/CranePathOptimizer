using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

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
                .Where(element => element.IncludeInGa && !IfcReferenceGeometryPolicy.IsGridReference(element))
                .ToList();

            if (gaEnabledElements.Count == 0)
                throw new InvalidOperationException("No GA-enabled elements found in the scene.");

            var selectedAssemblyTypes = CreateElementTypeSet(settings.TargetElementTypes);
            var candidateElements = selectedAssemblyTypes == null
                ? gaEnabledElements
                : gaEnabledElements
                    .Where(element => selectedAssemblyTypes.Contains(NormalizeElementType(element.ElementType)))
                    .ToList();

            if (candidateElements.Count == 0)
                throw new InvalidOperationException("No scene elements match the selected target element types.");

            var gaLiftUnits = BuildAssemblyLiftUnits(settings, candidateElements, elements);

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

            settings.CraneFootprintRadius = CalculateCraneFootprintRadius(crane, craneSpec);
            settings.RenderedOperatingEnvelopeRadius =
                CraneEnvelopeContainmentRadiusResolver.ResolveRenderedOperatingEnvelopeRadius(craneSpec.MaxRadius);

            return new PlacementInput
            {
                Zones = allowedZones,
                ForbiddenZones = forbiddenZones,
                Elements = gaLiftUnits,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                RequestedMaxCranePositions = settings.MaxCranePositions,
                RawGaEligibleElementCount = candidateElements.Count,
                GroupedAssemblyCount = gaLiftUnits.Count(u => u.Role == IfcElementRole.LiftAssembly),
                UngroupedElementCount = gaLiftUnits.Count(u => u.Role != IfcElementRole.LiftAssembly)
            };
        }


        private static List<PrecastElement> BuildAssemblyLiftUnits(
            GASettings settings,
            IReadOnlyList<PrecastElement> candidateElements,
            IReadOnlyList<PrecastElement> allElements)
        {
            var result = new List<PrecastElement>();
            var groupedMemberIds = new HashSet<string>(StringComparer.Ordinal);

            if (settings.EnableGroupedLifts)
            {
                // Find all parent assemblies that are active GA candidates
                var parentAssemblies = allElements
                    .Where(e => e.Role == IfcElementRole.LiftAssembly && e.IncludeInGa)
                    .ToList();

                var elementsMap = allElements
                    .Where(e => !string.IsNullOrEmpty(e.ElementID))
                    .ToDictionary(e => e.ElementID, e => e, StringComparer.Ordinal);

                foreach (var parent in parentAssemblies)
                {
                    // Resolve children of this parent from the complete elements list (not just candidateElements)
                    var children = allElements
                        .Where(e => string.Equals(e.ParentGlobalId, parent.ElementID, StringComparison.Ordinal))
                        .ToList();

                    if (parent.ChildGlobalIds != null)
                    {
                        foreach (var cid in parent.ChildGlobalIds)
                        {
                            if (elementsMap.TryGetValue(cid, out var child) && !children.Contains(child))
                            {
                                children.Add(child);
                            }
                        }
                    }

                    if (children.Count == 0 && !parent.AssemblyLiftWeight.HasValue)
                    {
                        // Exclude empty parent assemblies
                        continue;
                    }

                    if (children.Count > 0)
                    {
                        // Reconstruct classifications just in case they were lost
                        CraneOptimizer.Domain.Services.AssemblyRollupService.ReconstructAssemblyClassifications(children);
                    }

                    List<string> warnings;
                    var liftUnit = CraneOptimizer.Domain.Services.AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out warnings);

                    // Exclude parent and children from individual GA target lists
                    groupedMemberIds.Add(parent.ElementID);
                    foreach (var child in children)
                    {
                        groupedMemberIds.Add(child.ElementID);
                    }

                    result.Add(liftUnit);
                }
            }
            else
            {
                // Grouped lifts is OFF.
                // We must unroll children of valid assemblies so they can be lifted individually,
                // while ensuring the parent container itself never enters the GA as a physical individual target.
                var parentAssemblies = allElements
                    .Where(e => e.Role == IfcElementRole.LiftAssembly && e.IncludeInGa)
                    .ToList();

                var parentIds = new HashSet<string>(parentAssemblies.Select(p => p.ElementID), StringComparer.Ordinal);

                // Exclude parent assemblies from candidateElements so they don't enter GA
                groupedMemberIds.UnionWith(parentIds);

                // Add child elements of valid parent assemblies to the GA target list
                foreach (var parent in parentAssemblies)
                {
                    var children = allElements
                        .Where(e => string.Equals(e.ParentGlobalId, parent.ElementID, StringComparison.Ordinal) &&
                                    e.Role != IfcElementRole.InternalAuxiliary)
                        .ToList();

                    foreach (var child in children)
                    {
                        if (!groupedMemberIds.Contains(child.ElementID))
                        {
                            result.Add(child);
                            groupedMemberIds.Add(child.ElementID);
                        }
                    }
                }
            }

            // Add remaining candidate elements not belonging to any assembly rollup group
            foreach (var element in candidateElements)
            {
                if (groupedMemberIds.Contains(element.ElementID))
                    continue;

                result.Add(element);
            }

            return result;
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

        private double CalculateCraneFootprintRadius(Crane selectedCrane, CraneSpecification selectedSpecification)
        {
            double vehicleLength = selectedCrane.VehicleLength;
            double vehicleWidth = selectedCrane.VehicleWidth;
            double? outriggerLength = selectedCrane.OutriggerLength;
            double? outriggerWidth = selectedCrane.OutriggerWidth;
            string configuration = selectedSpecification.OutriggerConfig?.Trim() ?? string.Empty;
            bool isOnRubber = string.Equals(configuration, "On Rubber", StringComparison.OrdinalIgnoreCase);

            ValidatePositiveFinite(vehicleLength, "VehicleLength");
            ValidatePositiveFinite(vehicleWidth, "VehicleWidth");

            double footprintLength = !isOnRubber && IsPositiveFinite(outriggerLength)
                ? outriggerLength!.Value
                : vehicleLength;
            double footprintWidth = !isOnRubber && IsPositiveFinite(outriggerWidth)
                ? outriggerWidth!.Value
                : vehicleWidth;

            double radius = 0.5 * Math.Sqrt((footprintLength * footprintLength) + (footprintWidth * footprintWidth));
            ValidatePositiveFinite(radius, "CraneFootprintRadius");

            System.Diagnostics.Debug.WriteLine(
                "[GA_FOOTPRINT] " +
                $"Configuration='{(string.IsNullOrWhiteSpace(configuration) ? "Unspecified" : configuration)}', " +
                $"VehicleLength={vehicleLength:F4}, VehicleWidth={vehicleWidth:F4}, " +
                $"OutriggerLength={(outriggerLength.HasValue ? outriggerLength.Value.ToString("F4") : "null")}, " +
                $"OutriggerWidth={(outriggerWidth.HasValue ? outriggerWidth.Value.ToString("F4") : "null")}, " +
                $"SelectedFootprintLength={footprintLength:F4}, SelectedFootprintWidth={footprintWidth:F4}, " +
                $"CraneFootprintRadius={radius:F4}, " +
                $"GroundFootprintHalfDiagonal={radius:F4}");

            return radius;
        }

        private static void ValidatePositiveFinite(double value, string name)
        {
            if (!IsPositiveFinite(value))
                throw new InvalidOperationException($"Crane specification contains invalid {name}: {value}.");
        }

        private static bool IsPositiveFinite(double? value)
        {
            return value.HasValue
                && value.Value > 0
                && !double.IsNaN(value.Value)
                && !double.IsInfinity(value.Value);
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
