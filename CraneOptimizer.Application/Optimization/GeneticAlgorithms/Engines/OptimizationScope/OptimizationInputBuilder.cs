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

            var gaLiftUnits = BuildAssemblyLiftUnits(candidateElements);

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
                Elements = gaLiftUnits,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                RequestedMaxCranePositions = settings.MaxCranePositions
            };
        }


        private static List<PrecastElement> BuildAssemblyLiftUnits(IReadOnlyList<PrecastElement> candidateElements)
        {
            var result = new List<PrecastElement>();
            var groupedMemberIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in candidateElements
                .Where(IsAssemblyGroupCandidate)
                .GroupBy(element => element.AssemblyMark!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var members = group
                    .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                    .OrderBy(element => element.ElementID, StringComparer.Ordinal)
                    .ToList();

                if (members.Count <= 1)
                    continue;

                var liftUnit = CreateAssemblyLiftUnit(group.Key, members);
                result.Add(liftUnit);

                foreach (var member in members)
                {
                    groupedMemberIds.Add(member.ElementID);
                }
            }

            result.AddRange(candidateElements.Where(element =>
                string.IsNullOrWhiteSpace(element.ElementID) ||
                !groupedMemberIds.Contains(element.ElementID)));

            return result;
        }

        private static bool IsAssemblyGroupCandidate(PrecastElement element)
        {
            if (element == null)
                return false;

            if (!element.AssemblyMarkIsExplicit || element.AssemblyMarkConfidence != AssemblyMarkConfidence.High)
                return false;

            if (string.IsNullOrWhiteSpace(element.AssemblyMark) || IsPlaceholderAssemblyMark(element.AssemblyMark))
                return false;

            return element.WeightTons is > 0 && element.ElementCOG != null;
        }

        private static bool IsPlaceholderAssemblyMark(string mark)
        {
            var normalized = mark.Trim().ToUpperInvariant();
            return normalized is "WALL" or "BEAM" or "COLUMN" or "SLAB" or "ASSEMBLY" or "ELEMENT" or "UNKNOWN" or "-";
        }

        private static PrecastElement CreateAssemblyLiftUnit(string assemblyMark, IReadOnlyList<PrecastElement> members)
        {
            var totalWeight = members.Sum(member => member.WeightTons ?? member.ElementWT ?? 0);
            var cog = ComputeWeightedCog(members, totalWeight) ?? ComputeAverageCog(members);
            var memberIds = members.Select(member => member.ElementID).ToList();
            var warnings = new List<string>
            {
                $"Assembly lift unit grouped from explicit AssemblyMark '{assemblyMark}'. Members={memberIds.Count}."
            };

            return new PrecastElement
            {
                ElementID = $"ASM:{SanitizeAssemblyId(assemblyMark)}",
                ElementMark = assemblyMark,
                ElementName = assemblyMark,
                ResolvedDisplayMark = assemblyMark,
                ElementType = "Assembly",
                ElementMaterial = JoinDistinct(members.Select(member => member.ElementMaterial)),
                ElementWT = totalWeight,
                WeightTons = totalWeight,
                WeightSource = "AssemblyGroupExplicitMarkRollup",
                WeightTrustLevel = ResolveRollupTrustLevel(members),
                ElementVolume = SumNullable(members.Select(member => member.VolumeM3 ?? member.ElementVolume)),
                VolumeM3 = SumNullable(members.Select(member => member.VolumeM3 ?? member.ElementVolume)),
                ElementCOG = cog,
                COG_M = cog,
                ElementLength = MaxNullable(members.Select(member => member.ElementLength)),
                ElementWidth = MaxNullable(members.Select(member => member.ElementWidth)),
                ElementHeight = MaxNullable(members.Select(member => member.ElementHeight)),
                BBoxLength = MaxNullable(members.Select(member => member.BBoxLength)),
                BBoxWidth = MaxNullable(members.Select(member => member.BBoxWidth)),
                BBoxHeight = MaxNullable(members.Select(member => member.BBoxHeight)),
                BoundingBox_M = new DomainPoint3D(
                    MaxNullable(members.Select(member => member.BBoxLength)) ?? 0,
                    MaxNullable(members.Select(member => member.BBoxWidth)) ?? 0,
                    MaxNullable(members.Select(member => member.BBoxHeight)) ?? 0),
                RawIfcType = "AssemblyLiftUnit",
                ResolvedCategory = "Assembly",
                Role = IfcElementRole.LiftAssembly,
                ChildGlobalIds = memberIds,
                VisibleGeometryOwnerGlobalIds = members
                    .SelectMany(member => member.VisibleGeometryOwnerGlobalIds?.Count > 0
                        ? member.VisibleGeometryOwnerGlobalIds
                        : new List<string> { member.ElementID })
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                IncludeInRender = false,
                IncludeInElementTree = false,
                IncludeInGa = true,
                AssemblyMark = assemblyMark,
                AssemblyMarkSource = "GA.AssemblyMarkGroup",
                AssemblyMarkConfidence = AssemblyMarkConfidence.High,
                AssemblyMarkIsExplicit = true,
                ExplicitAssemblyMarkFound = true,
                SourceSystem = ResolveSourceSystem(members),
                ExporterProfile = ResolveExporterProfile(members),
                ValidationStatus = ValidationStatus.Valid,
                ValidationWarnings = warnings,
                ExtractionWarnings = warnings,
                NormalizationDecisionReason = "Grouped explicit assembly mark members into one GA lift unit."
            };
        }

        private static DomainPoint3D ComputeAverageCog(IReadOnlyList<PrecastElement> members)
        {
            var valid = members
                .Select(member => member.ElementCOG ?? member.COG_M)
                .Where(point => point != null)
                .ToList();

            return valid.Count == 0
                ? new DomainPoint3D()
                : new DomainPoint3D(valid.Average(point => point!.X), valid.Average(point => point!.Y), valid.Average(point => point!.Z));
        }

        private static DomainPoint3D? ComputeWeightedCog(IReadOnlyList<PrecastElement> members, double totalWeight)
        {
            if (totalWeight <= 0)
                return null;

            double x = 0, y = 0, z = 0, weightSum = 0;
            foreach (var member in members)
            {
                var weight = member.WeightTons ?? member.ElementWT ?? 0;
                var cog = member.ElementCOG ?? member.COG_M;
                if (weight <= 0 || cog == null)
                    continue;

                x += cog.X * weight;
                y += cog.Y * weight;
                z += cog.Z * weight;
                weightSum += weight;
            }

            return weightSum > 0
                ? new DomainPoint3D(x / weightSum, y / weightSum, z / weightSum)
                : null;
        }

        private static string SanitizeAssemblyId(string assemblyMark)
        {
            var chars = assemblyMark
                .Trim()
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_')
                .ToArray();
            return new string(chars);
        }

        private static string JoinDistinct(IEnumerable<string?> values)
        {
            var distinct = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return distinct.Count == 0 ? string.Empty : string.Join(", ", distinct);
        }

        private static double? SumNullable(IEnumerable<double?> values)
        {
            double sum = 0;
            bool hasValue = false;
            foreach (var value in values)
            {
                if (value is > 0)
                {
                    sum += value.Value;
                    hasValue = true;
                }
            }
            return hasValue ? sum : null;
        }

        private static double? MaxNullable(IEnumerable<double?> values)
        {
            var positive = values.Where(value => value is > 0).Select(value => value!.Value).ToList();
            return positive.Count == 0 ? null : positive.Max();
        }

        private static IfcWeightTrustLevel ResolveRollupTrustLevel(IReadOnlyList<PrecastElement> members)
        {
            return members.All(member => member.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity)
                ? IfcWeightTrustLevel.ExplicitQuantity
                : IfcWeightTrustLevel.ExplicitProperty;
        }

        private static SourceSystem ResolveSourceSystem(IReadOnlyList<PrecastElement> members)
        {
            var first = members[0].SourceSystem;
            return members.All(member => member.SourceSystem == first) ? first : SourceSystem.IFC;
        }

        private static IfcExporterProfile ResolveExporterProfile(IReadOnlyList<PrecastElement> members)
        {
            var first = members[0].ExporterProfile;
            return members.All(member => member.ExporterProfile == first) ? first : IfcExporterProfile.Generic;
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
