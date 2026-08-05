using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CraneOptimizer.Domain.Policy
{
    public sealed record IfcCalculatedWeightCompatibilityProfile(
        string Id,
        string Sha256,
        string DiagnosticFileName,
        long ExpectedFileSizeBytes);

    public static class IfcCalculatedWeightCompatibilityPolicy
    {
        private static readonly Dictionary<string, IfcCalculatedWeightCompatibilityProfile> ApprovedProfiles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6"] = new(
                    Id: "R07-4906",
                    Sha256: "90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6",
                    DiagnosticFileName: "R07-HC2C22-QDC-49-XX-MOD-ST-04906.ifc",
                    ExpectedFileSizeBytes: 18347192L),

                ["46c1eb58a03b6c924afe1488a5a94d09ff81a488489a345f69177908e1345296"] = new(
                    Id: "R07-5101",
                    Sha256: "46c1eb58a03b6c924afe1488a5a94d09ff81a488489a345f69177908e1345296",
                    DiagnosticFileName: "R07-HC2C22-QDC-51-XX-MOD-ST-05101.ifc",
                    ExpectedFileSizeBytes: 24717102L),

                ["1d61375bc15013c63fdc58b9e68b29d07d88d8997bef32b9e511cd98c952d5cc"] = new(
                    Id: "L02-3801",
                    Sha256: "1d61375bc15013c63fdc58b9e68b29d07d88d8997bef32b9e511cd98c952d5cc",
                    DiagnosticFileName: "L02-R02C01-QDC-38-ZZ-MOD-ST-03801.ifc",
                    ExpectedFileSizeBytes: 124967132L)
            };

        public static bool IsApprovedModel(string? sha256)
        {
            if (string.IsNullOrWhiteSpace(sha256))
                return false;

            return ApprovedProfiles.ContainsKey(sha256.Trim());
        }

        public static bool TryResolve(string? sha256, [NotNullWhen(true)] out IfcCalculatedWeightCompatibilityProfile? profile)
        {
            if (!string.IsNullOrWhiteSpace(sha256) && ApprovedProfiles.TryGetValue(sha256.Trim(), out var found))
            {
                profile = found;
                return true;
            }

            profile = null;
            return false;
        }

        public static IReadOnlyCollection<IfcCalculatedWeightCompatibilityProfile> GetApprovedProfiles() => ApprovedProfiles.Values;
    }
}
