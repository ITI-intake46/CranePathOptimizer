namespace CraneOptimizer.Application.Reports.Services;

public sealed record WeightReportMapping(
    string WeightVerified,
    string WeightStatus,
    WeightWarningCode WarningCode)
{
    public string WarningMessage => ReportWeightMapper.GetWarningMessage(WarningCode);
}
