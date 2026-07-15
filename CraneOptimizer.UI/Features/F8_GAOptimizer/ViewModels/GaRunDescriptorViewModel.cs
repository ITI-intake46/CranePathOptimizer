using System.Globalization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

public sealed class GaRunDescriptorViewModel
{
    public GaRunDescriptorViewModel(GaRunDescriptorDto descriptor)
    {
        Descriptor = descriptor;
    }

    public GaRunDescriptorDto Descriptor { get; }

    public string OptimizationRunId => Descriptor.OptimizationRunId;
    public string SourceKind => Descriptor.SourceKind;
    public string Classification => Descriptor.Classification;
    public bool IsUsableCompleted =>
        string.Equals(Descriptor.Status, "Completed", StringComparison.OrdinalIgnoreCase)
        && Descriptor.Classification is "Current" or "CurrentByIfcHash" or "CurrentProjectUnverified" or "Historic"
        && Descriptor.SourceKind is "ProductionJson" or "LegacyCsv" or "PartialLegacy";

    public string CompletedText => Descriptor.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "Unknown time";

    public string SummaryText =>
        $"{Descriptor.LiftedLiftUnitCount} / {Descriptor.TotalTargetLiftUnitCount} - {Descriptor.CoverageRatio:P2} - {Descriptor.SetupCount} setups";

    public string CraneModeText =>
        string.Join(" - ", new[] { Descriptor.CraneName, Descriptor.EffectiveLiftMode }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string WarningsText => string.Join(Environment.NewLine, Descriptor.Warnings);

    public string SourceText => Descriptor.SourceKind switch
    {
        "LegacyCsv" => "Legacy GA result",
        "PartialLegacy" => "Legacy GA result - Partial data",
        _ when Descriptor.Classification == "Historic" => "Historic model - Production JSON",
        _ when Descriptor.Classification == "CurrentProjectUnverified" => "Project match unverified - Production JSON",
        _ => $"{Descriptor.Classification} - Production JSON"
    };

    public override string ToString() => $"{CompletedText} | {SummaryText} | {CraneModeText} | {SourceText}";
}
