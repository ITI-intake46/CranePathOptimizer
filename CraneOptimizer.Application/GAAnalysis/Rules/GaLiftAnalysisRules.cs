using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CraneOptimizer.Application.GAAnalysis;

public static class GaLiftAnalysisRules
{
    public const string EngineId = "ga-lift-analysis-engine";
    public const string EngineVersion = "1";
    public const string ProfileId = "ConservativeProfile.v1";

    public const double BorderlineUtilization = 0.90;
    public const int MinimumRightSizingLiftCount = 5;
    public const double OversizeCapacityP95Maximum = 0.50;
    public const double OversizeCapacityMaximum = 0.70;
    public const double OversizeGeometryP95Maximum = 0.70;
    public const double MeaningfullySmallerRatio = 0.90;
    public const double MaterialChangeRatio = 0.80;
    public const double RecommendedCapacityP95Minimum = 0.50;
    public const double RecommendedCapacityP95Maximum = 0.85;

    public static string PolicyHash { get; } = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join(
                "|",
                ProfileId,
                BorderlineUtilization.ToString("R", CultureInfo.InvariantCulture),
                MinimumRightSizingLiftCount.ToString(CultureInfo.InvariantCulture),
                OversizeCapacityP95Maximum.ToString("R", CultureInfo.InvariantCulture),
                OversizeCapacityMaximum.ToString("R", CultureInfo.InvariantCulture),
                OversizeGeometryP95Maximum.ToString("R", CultureInfo.InvariantCulture),
                MeaningfullySmallerRatio.ToString("R", CultureInfo.InvariantCulture),
                MaterialChangeRatio.ToString("R", CultureInfo.InvariantCulture),
                RecommendedCapacityP95Minimum.ToString("R", CultureInfo.InvariantCulture),
                RecommendedCapacityP95Maximum.ToString("R", CultureInfo.InvariantCulture)))))
        .ToLowerInvariant();

    public static string UtilizationBand(double? value) => value switch
    {
        null => "Unavailable",
        < 0.50 => "Low",
        < 0.80 => "Moderate",
        < 0.90 => "High",
        _ => "Near limit"
    };

    /// <summary>Hyndman-Fan R-7 percentile, matching common spreadsheet defaults.</summary>
    public static double? PercentileR7(IEnumerable<double> source, double probability)
    {
        var values = source
            .Where(double.IsFinite)
            .OrderBy(value => value)
            .ToArray();
        if (values.Length == 0)
            return null;
        if (values.Length == 1)
            return values[0];

        var index = (values.Length - 1) * Math.Clamp(probability, 0, 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
            return values[lower];

        return values[lower] + ((index - lower) * (values[upper] - values[lower]));
    }
}
