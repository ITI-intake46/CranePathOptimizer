using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class SourcePropertyResolutionResult
    {
        public string? LogicalField { get; set; }
        public string? Value { get; set; }
        public string? SourcePropertySet { get; set; }
        public string? SourcePropertyName { get; set; }
        public string? SourceSystem { get; set; }
        public string? ExporterProfile { get; set; }
        public string? Confidence { get; set; }
        public bool UsedFallback { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
