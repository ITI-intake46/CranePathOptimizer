using CraneOptimizer.Domain.Diagnostics;

namespace CraneOptimizer.Domain.Policy
{
    public static class IfcWeightTrustPolicy
    {
        public static bool IsGaApproved(IfcWeightTrustLevel level) =>
            level is IfcWeightTrustLevel.ExplicitQuantity
                or IfcWeightTrustLevel.ExplicitProperty
                or IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA;

        public static bool IsExplicitIfcWeight(IfcWeightTrustLevel level) =>
            level is IfcWeightTrustLevel.ExplicitQuantity
                or IfcWeightTrustLevel.ExplicitProperty;

        public static bool IsCalculatedWeight(IfcWeightTrustLevel level) =>
            level is IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA
                or IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA;

        public static bool IsBlockedEstimatedWeight(IfcWeightTrustLevel level) =>
            level is IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;
    }
}
