using System;
using CraneOptimizer.Domain.Diagnostics;

namespace CraneOptimizer.Domain.Policy
{
    public static class IfcCalculatedWeightPromotionPolicy
    {
        public static bool CanPromote(
            string? modelSha256,
            IfcWeightTrustLevel currentTrustLevel,
            double? volumeM3,
            IfcVolumeEvidenceKind volumeEvidence,
            double? resolvedDensityKgPerM3,
            string? densitySource,
            double? weightTons)
        {
            // Condition 1: Approved model SHA-256
            if (!IfcCalculatedWeightCompatibilityPolicy.IsApprovedModel(modelSha256))
                return false;

            // Condition 2: Explicit weight priority
            if (IfcWeightTrustPolicy.IsExplicitIfcWeight(currentTrustLevel))
                return false;

            // Condition 3: Current state must be estimated fallback or missing (not ambiguous/suspicious explicit)
            if (currentTrustLevel is not (IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA or IfcWeightTrustLevel.Missing))
                return false;

            // Condition 4: Positive finite volume
            if (volumeM3 is null or <= 0 || !double.IsFinite(volumeM3.Value))
                return false;

            // Condition 5: Reliable per-element volume evidence (IFC Quantity, Property, or Mesh geometry; NOT BBox, Weight-derived, Missing, Unknown)
            if (volumeEvidence is not (IfcVolumeEvidenceKind.IfcQuantity or IfcVolumeEvidenceKind.IfcProperty or IfcVolumeEvidenceKind.MeshGeometry))
                return false;

            // Condition 6: Positive finite density & non-empty density source
            if (resolvedDensityKgPerM3 is null or <= 0 || !double.IsFinite(resolvedDensityKgPerM3.Value))
                return false;
            if (string.IsNullOrWhiteSpace(densitySource) || densitySource.Trim() == "-")
                return false;

            // Condition 7: Positive finite calculated weight
            if (weightTons is null or <= 0 || !double.IsFinite(weightTons.Value))
                return false;

            return true;
        }

        public static bool CanPromote(
            string? modelSha256,
            IfcWeightTrustLevel currentTrustLevel,
            double? volumeM3,
            IfcVolumeEvidenceKind volumeEvidence,
            string? material,
            double? weightTons)
        {
            var density = !string.IsNullOrWhiteSpace(material) && material.Trim() != "-" ? 2500.0 : (double?)null;
            var densitySource = !string.IsNullOrWhiteSpace(material) && material.Trim() != "-" ? "ConcreteDensity" : null;
            return CanPromote(modelSha256, currentTrustLevel, volumeM3, volumeEvidence, density, densitySource, weightTons);
        }
    }
}
