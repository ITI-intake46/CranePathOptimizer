using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public static class BenchmarkDiagnosticsIdentity
    {
        public const string SchemaVersion = "2.0";

        public static void Ensure(PlacementInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            input.RawElementCount = input.RawElementCount > 0
                ? input.RawElementCount
                : Math.Max(input.TotalElementsCount, input.Elements?.Count ?? 0);

            input.OptimizationScopeElementCount = input.OptimizationScopeElementCount > 0
                ? input.OptimizationScopeElementCount
                : Math.Max(input.FeatureScopeElements?.Count ?? 0, input.Elements?.Count ?? 0);

            if (input.FeatureScopeElements == null || input.FeatureScopeElements.Count == 0)
            {
                input.FeatureScopeElements = input.Elements?.ToList() ?? new List<PrecastElement>();
            }

            input.ScopeDefinitionHash = string.IsNullOrWhiteSpace(input.ScopeDefinitionHash)
                ? CreateScopeDefinitionHash(input.FeatureScopeElements, input.Settings?.TargetElementTypes)
                : input.ScopeDefinitionHash;

            string projectIdentity = !string.IsNullOrWhiteSpace(input.ProjectId)
                ? input.ProjectId
                : input.ProjectName ?? "N/A";
            string craneId = (input.CraneSpec?.CraneId > 0
                    ? input.CraneSpec.CraneId
                    : input.CraneSpec?.Crane?.Id)
                ?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            string specificationId = input.CraneSpec?.Id
                .ToString(CultureInfo.InvariantCulture) ?? "N/A";
            string targetTypes = NormalizeValues(input.Settings?.TargetElementTypes);

            input.ScenarioId = string.IsNullOrWhiteSpace(input.ScenarioId)
                ? CreateId(
                    "SCN",
                    projectIdentity,
                    input.IfcFileName,
                    targetTypes,
                    input.ScopeDefinitionHash,
                    craneId,
                    specificationId,
                    input.Settings?.GridStep.ToString("R", CultureInfo.InvariantCulture),
                    (input.RequestedMaxCranePositions > 0
                        ? input.RequestedMaxCranePositions
                        : input.Settings?.MaxCranePositions)
                    ?.ToString(CultureInfo.InvariantCulture))
                : input.ScenarioId;

            input.ProjectFeatureSnapshotId = string.IsNullOrWhiteSpace(input.ProjectFeatureSnapshotId)
                ? CreateId(
                    "PFS",
                    projectIdentity,
                    input.IfcFileName,
                    input.ScopeDefinitionHash,
                    craneId,
                    specificationId,
                    input.Settings?.GridStep.ToString("R", CultureInfo.InvariantCulture),
                    input.Settings?.CraneFootprintRadius.ToString("R", CultureInfo.InvariantCulture))
                : input.ProjectFeatureSnapshotId;
        }

        public static string CreateScopeDefinitionHash(
            IEnumerable<PrecastElement>? elements,
            IEnumerable<string>? targetElementTypes)
        {
            string elementIds = NormalizeValues(elements?
                .Select(element => element.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id)));
            string targetTypes = NormalizeValues(targetElementTypes);
            return CreateId("SCP", elementIds, targetTypes);
        }

        public static string CreateId(string prefix, params string?[] components)
        {
            string canonical = string.Join(
                "\n",
                components.Select(component => component?.Trim() ?? "N/A"));
            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return $"{prefix}-{Convert.ToHexString(digest.AsSpan(0, 12))}";
        }

        private static string NormalizeValues(IEnumerable<string>? values)
        {
            if (values == null)
                return "N/A";

            string[] normalized = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return normalized.Length == 0 ? "N/A" : string.Join(";", normalized);
        }
    }
}
