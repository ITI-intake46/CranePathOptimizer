using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Policy;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace CraneOptimizer.Application.Tests.Integration
{
    /// <summary>
    /// End-to-end integration smoke tests for all four required IFC models.
    /// Verifies SHA-256 identity, IFC audit, normalization, element extraction,
    /// weight trust promotion, and GA candidate gating without executing the GA itself.
    /// </summary>
    public class FourModelIfcGaSmokeTests
    {
        private readonly ITestOutputHelper _output;

        public FourModelIfcGaSmokeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly Dictionary<string, string> DefaultPaths = new()
        {
            ["R07_4906"] = @"D:\ITI\CRANE PROJECT\test\R07-HC2C22-QDC-49-XX-MOD-ST-04906.ifc",
            ["R07_5101"] = @"D:\ITI\CRANE PROJECT\test\R07-HC2C22-QDC-51-XX-MOD-ST-05101.ifc",
            ["L02_3801"] = @"D:\ITI\CRANE PROJECT\test\L02-R02C01-QDC-38-ZZ-MOD-ST-03801.ifc",
            ["JIZAN"]    = @"D:\ITI\CRANE PROJECT\test\jizan_4_Precastview.ifc"
        };

        private string ResolvePath(string key, string envVarName)
        {
            var env = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            if (DefaultPaths.TryGetValue(key, out var path) && File.Exists(path))
                return path;

            throw new FileNotFoundException(
                $"IFC test file for key '{key}' not found. Set env var '{envVarName}' or place at '{DefaultPaths[key]}'.");
        }

        [Theory]
        [InlineData("R07_4906", "CRANE_IFC_R07_4906", "90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6", true)]
        [InlineData("R07_5101", "CRANE_IFC_R07_5101", "46c1eb58a03b6c924afe1488a5a94d09ff81a488489a345f69177908e1345296", true)]
        [InlineData("L02_3801", "CRANE_IFC_L02_3801", "1d61375bc15013c63fdc58b9e68b29d07d88d8997bef32b9e511cd98c952d5cc", true)]
        [InlineData("JIZAN",    "CRANE_IFC_JIZAN",    null,                                                               false)]
        public async Task FourModel_EndToEnd_IfcImport_And_GaCandidate_Verification(
            string key,
            string envVar,
            string? expectedSha256,
            bool isApprovedCalculatedModel)
        {
            var filePath = ResolvePath(key, envVar);
            Assert.True(File.Exists(filePath), $"File {filePath} must exist.");

            _output.WriteLine($"[{key}] Testing: {filePath}");

            // ─── Build services ─────────────────────────────────────────────────────
            var profileResolver  = new IfcExporterProfileResolver();
            var aliasResolver    = new CentralIfcPropertyAliasResolver(profileResolver);
            var sessionService   = new IfcModelSessionService();
            var auditService     = new IfcDiagnosticAuditService(sessionService, aliasResolver);
            var normService      = new IfcNormalizationDecisionService(profileResolver);
            var readerRepository = new IfcReaderRepository(auditService, normService, sessionService);

            // ─── Step 1: Audit (SHA-256 verification) ────────────────────────────────
            _output.WriteLine($"[{key}] Step 1: Running audit...");
            var auditResult = await auditService.RunAuditAsync(filePath);
            Assert.NotNull(auditResult);
            Assert.NotNull(auditResult.Metadata);
            Assert.False(string.IsNullOrWhiteSpace(auditResult.Metadata.Sha256),
                $"[{key}] SHA-256 must be computed.");
            _output.WriteLine($"[{key}]   SHA-256 = {auditResult.Metadata.Sha256}");

            if (isApprovedCalculatedModel)
            {
                Assert.NotNull(expectedSha256);
                Assert.Equal(expectedSha256, auditResult.Metadata.Sha256, ignoreCase: true);
                Assert.True(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel(auditResult.Metadata.Sha256),
                    $"[{key}] SHA must be in the approved allowlist.");
            }
            else
            {
                Assert.False(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel(auditResult.Metadata.Sha256),
                    $"[{key}] Jizan SHA must NOT be in the calculated-weight allowlist.");
            }

            // ─── Step 2: Element extraction via ReadElements ─────────────────────────
            _output.WriteLine($"[{key}] Step 2: Extracting elements via ReadElements...");
            var elements = readerRepository.ReadElements(filePath);
            Assert.NotEmpty(elements);
            _output.WriteLine($"[{key}]   Total extracted: {elements.Count}");

            // ─── Step 3: Weight-trust assertions ────────────────────────────────────
            if (isApprovedCalculatedModel)
            {
                var approvedCount = elements.Count(e =>
                    e.WeightTrustLevel == IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA);

                _output.WriteLine($"[{key}]   ValidatedCalculated_ApprovedForGA: {approvedCount}");

                var byTrustLevel = elements
                    .GroupBy(e => e.WeightTrustLevel)
                    .Select(g => $"{g.Key}={g.Count()}")
                    .OrderBy(s => s);
                _output.WriteLine($"[{key}]   Trust levels: {string.Join(", ", byTrustLevel)}");

                Assert.True(approvedCount > 0,
                    $"[{key}] Must have at least one ValidatedCalculated_ApprovedForGA element.");

                // No promoted element must carry unreliable volume evidence
                var badPromoted = elements.Where(e =>
                    e.WeightTrustLevel == IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA &&
                    e.VolumeEvidence is
                        IfcVolumeEvidenceKind.BoundingBox or
                        IfcVolumeEvidenceKind.DerivedFromWeight or
                        IfcVolumeEvidenceKind.Missing or
                        IfcVolumeEvidenceKind.Unknown).ToList();

                foreach (var bad in badPromoted)
                    _output.WriteLine($"[{key}]   BAD VolumeEvidence: {bad.ElementID} trust={bad.WeightTrustLevel} vol={bad.VolumeEvidence}");

                Assert.Empty(badPromoted);
            }
            else
            {
                // Jizan: must have elements with explicit IFC weights
                var explicitCount = elements.Count(e =>
                    e.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                    e.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty);

                _output.WriteLine($"[{key}]   Explicit weight elements: {explicitCount}");
                Assert.True(explicitCount > 0,
                    $"[{key}] Jizan must have elements with ExplicitQuantity or ExplicitProperty weight.");

                // Jizan must NOT have any ValidatedCalculated promoted elements
                var wronglyPromoted = elements.Count(e =>
                    e.WeightTrustLevel == IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA);
                _output.WriteLine($"[{key}]   Wrongly promoted: {wronglyPromoted}");
                Assert.Equal(0, wronglyPromoted);
            }

            // ─── Step 4: GA Candidate Gate ───────────────────────────────────────────
            var explicitElements = elements.Where(e => IfcWeightTrustPolicy.IsExplicitIfcWeight(e.WeightTrustLevel)).ToList();
            _output.WriteLine($"[{key}] Explicit weight element breakdown:");
            _output.WriteLine($"[{key}]   Total explicit weight: {explicitElements.Count}");
            _output.WriteLine($"[{key}]   IncludeInGa=true: {explicitElements.Count(e => e.IncludeInGa)}");
            _output.WriteLine($"[{key}]   Role==LiftableElement: {explicitElements.Count(e => e.Role == IfcElementRole.LiftableElement)}");
            _output.WriteLine($"[{key}]   Role breakdown: {string.Join(", ", elements.GroupBy(e => e.Role).Select(g => $"{g.Key}={g.Count()}"))}");
            _output.WriteLine($"[{key}]   IsGaApproved: {explicitElements.Count(e => IfcWeightTrustPolicy.IsGaApproved(e.WeightTrustLevel))}");
            _output.WriteLine($"[{key}]   WeightTons > 0: {explicitElements.Count(e => e.WeightTons > 0)}");
            _output.WriteLine($"[{key}]   COG non-null/valid: {explicitElements.Count(e => e.ElementCOG != null && !double.IsNaN(e.ElementCOG.X) && !double.IsInfinity(e.ElementCOG.X))}");

            var gaCandidates = elements.Where(e =>
                e.IncludeInGa &&
                (e.Role == IfcElementRole.LiftableElement || e.Role == IfcElementRole.LiftAssembly) &&
                IfcWeightTrustPolicy.IsGaApproved(e.WeightTrustLevel) &&
                e.WeightTons is > 0 &&
                e.ElementCOG != null &&
                !double.IsNaN(e.ElementCOG.X) &&
                !double.IsInfinity(e.ElementCOG.X)).ToList();

            _output.WriteLine($"[{key}]   GA candidates: {gaCandidates.Count}");
            Assert.NotEmpty(gaCandidates);

            _output.WriteLine($"[{key}] ✓ PASS: {elements.Count} elements, {gaCandidates.Count} GA candidates.");
        }
    }
}
