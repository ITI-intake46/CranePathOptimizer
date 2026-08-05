using System;
using System.Collections.Generic;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Policy;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.Unit
{
    public class IfcCalculatedWeightPolicyAndRollupUnitTests
    {
        [Fact]
        public void ApprovedHashes_MatchPolicyProfiles()
        {
            Assert.True(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel("90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6"));
            Assert.True(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel("46c1eb58a03b6c924afe1488a5a94d09ff81a488489a345f69177908e1345296"));
            Assert.True(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel("1d61375bc15013c63fdc58b9e68b29d07d88d8997bef32b9e511cd98c952d5cc"));
        }

        [Fact]
        public void UnknownHash_DoesNotMatchPolicy()
        {
            Assert.False(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel("c11e6c9db884071df0e554d21c0118369c6efd2aec39945f8d00bc640f36f40e"));
            Assert.False(IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel("0000000000000000000000000000000000000000000000000000000000000000"));
        }

        [Fact]
        public void CanPromote_RequiresReliableVolumeEvidence()
        {
            const string sha = "90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6";
            var trust = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;

            // Reliable sources promote
            Assert.True(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.IfcQuantity, 2500.0, "Concrete", 6.25));
            Assert.True(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.IfcProperty, 2500.0, "Concrete", 6.25));
            Assert.True(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.MeshGeometry, 2500.0, "Concrete", 6.25));

            // Unreliable sources fail promotion
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.BoundingBox, 2500.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.DerivedFromWeight, 2500.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.Missing, 2500.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.Unknown, 2500.0, "Concrete", 6.25));
        }

        [Fact]
        public void CanPromote_RequiresValidDensity()
        {
            const string sha = "90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6";
            var trust = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;

            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.IfcQuantity, 0.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.IfcQuantity, -100.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, trust, 2.5, IfcVolumeEvidenceKind.IfcQuantity, 2500.0, "", 6.25));
        }

        [Fact]
        public void ExplicitWeight_HasPriorityOverPromotion()
        {
            const string sha = "90e98fae1cb4254250d715ca67a18f3802b726f1a63d5e37f97e9f9c7f3aeae6";
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, IfcWeightTrustLevel.ExplicitQuantity, 2.5, IfcVolumeEvidenceKind.IfcQuantity, 2500.0, "Concrete", 6.25));
            Assert.False(IfcCalculatedWeightPromotionPolicy.CanPromote(sha, IfcWeightTrustLevel.ExplicitProperty, 2.5, IfcVolumeEvidenceKind.IfcQuantity, 2500.0, "Concrete", 6.25));
        }

        [Fact]
        public void IfcWeightTrustPolicy_IsGaApproved_Semantics()
        {
            Assert.True(IfcWeightTrustPolicy.IsGaApproved(IfcWeightTrustLevel.ExplicitQuantity));
            Assert.True(IfcWeightTrustPolicy.IsGaApproved(IfcWeightTrustLevel.ExplicitProperty));
            Assert.True(IfcWeightTrustPolicy.IsGaApproved(IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA));

            Assert.False(IfcWeightTrustPolicy.IsGaApproved(IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA));
            Assert.False(IfcWeightTrustPolicy.IsGaApproved(IfcWeightTrustLevel.Missing));
        }

        [Fact]
        public void ElementWeightCalculator_IsEstimated_Semantics()
        {
            var calculatedApprovedElem = new PrecastElement { WeightTrustLevel = IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA };
            var explicitElem = new PrecastElement { WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity };
            var fallbackElem = new PrecastElement { WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA };

            Assert.False(ElementWeightCalculator.IsEstimated(calculatedApprovedElem));
            Assert.False(ElementWeightCalculator.IsEstimated(explicitElem));
            Assert.True(ElementWeightCalculator.IsEstimated(fallbackElem));
        }

        [Fact]
        public void AssemblyRollupService_DoesNotUseEnumMinMax()
        {
            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.GroupingOnly,
                WeightTrustLevel = IfcWeightTrustLevel.Missing
            };

            var child1 = new PrecastElement
            {
                ElementID = "CHILD_1",
                Role = IfcElementRole.LiftableElement,
                WeightTons = 5.0,
                ElementWT = 5.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity
            };

            var child2 = new PrecastElement
            {
                ElementID = "CHILD_2",
                Role = IfcElementRole.LiftableElement,
                WeightTons = 3.0,
                ElementWT = 3.0,
                WeightTrustLevel = IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA
            };

            var children = new List<PrecastElement> { child1, child2 };

            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out _);

            // Rollup containing a ValidatedCalculated_ApprovedForGA member must become ValidatedCalculated_ApprovedForGA (not ExplicitQuantity via enum Min)
            Assert.Equal(IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA, liftUnit.WeightTrustLevel);
            Assert.Equal(8.0, liftUnit.WeightTons);
        }
    }
}
