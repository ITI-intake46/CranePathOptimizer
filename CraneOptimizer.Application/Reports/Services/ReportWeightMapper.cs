using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public static class ReportWeightMapper
{
    public static WeightReportMapping Map(PrecastElement element)
    {
        bool hasRollupWarning = element.ExtractionWarnings.Any(w =>
            w.Contains("roll", StringComparison.OrdinalIgnoreCase)
            || w.Contains("child", StringComparison.OrdinalIgnoreCase));

        if (hasRollupWarning)
            return new WeightReportMapping("No", "Needs Review", WeightWarningCode.AssemblyRollupFromChildren);

        return element.WeightTrustLevel switch
        {
            IfcWeightTrustLevel.ExplicitQuantity or IfcWeightTrustLevel.ExplicitProperty
                when ResolveWeight(element) > 0 => new WeightReportMapping("Yes", "OK", WeightWarningCode.None),
            IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA
                when ResolveWeight(element) > 0 => new WeightReportMapping("Yes", "Calculated Approved", WeightWarningCode.None),
            IfcWeightTrustLevel.ExplicitZeroFromIFC => new WeightReportMapping("No", "Invalid", WeightWarningCode.ZeroWeightFromIFC),
            IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA => new WeightReportMapping("No", "Estimated", WeightWarningCode.EstimatedFromVolumeDensity),
            IfcWeightTrustLevel.AmbiguousUnit => new WeightReportMapping("No", "Needs Review", WeightWarningCode.AmbiguousUnit),
            IfcWeightTrustLevel.SuspiciousExtreme => new WeightReportMapping("No", "Needs Review", WeightWarningCode.SuspiciousWeight),
            IfcWeightTrustLevel.RequiresUserApproval => new WeightReportMapping("No", "Needs Review", WeightWarningCode.RequiresUserApproval),
            IfcWeightTrustLevel.Missing => new WeightReportMapping("No", "Missing", WeightWarningCode.MissingExplicitWeight),
            _ when ResolveWeight(element) <= 0 => new WeightReportMapping("No", "Missing", WeightWarningCode.MissingExplicitWeight),
            _ => new WeightReportMapping("No", "Needs Review", WeightWarningCode.RequiresUserApproval)
        };
    }

    public static string GetWarningMessage(WeightWarningCode code) => code switch
    {
        WeightWarningCode.None => "-",
        WeightWarningCode.MissingExplicitWeight => "No explicit weight found in IFC.",
        WeightWarningCode.EstimatedFromVolumeDensity => "Weight estimated from volume and density.",
        WeightWarningCode.ZeroWeightFromIFC => "Weight is zero in IFC.",
        WeightWarningCode.AmbiguousUnit => "Weight unit could not be converted safely.",
        WeightWarningCode.SuspiciousWeight => "Weight value is suspicious and needs review.",
        WeightWarningCode.RequiresUserApproval => "Weight requires review before optimization.",
        WeightWarningCode.AssemblyRollupFromChildren => "Assembly weight was rolled up from child elements.",
        _ => "-"
    };

    public static double? ResolveWeight(PrecastElement element)
    {
        if (element.WeightTons is > 0)
            return element.WeightTons.Value;

        if (element.ElementWT is > 0)
            return element.ElementWT.Value;

        return null;
    }
}
