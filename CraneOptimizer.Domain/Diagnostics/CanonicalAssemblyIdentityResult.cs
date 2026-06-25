using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class CanonicalAssemblyIdentityResult
    {
        public string? AssemblyMark { get; set; }
        public string? SourcePropertyName { get; set; }
        public string? SourcePropertySet { get; set; }
        public string? SourceSystem { get; set; }
        public string? Confidence { get; set; } // "High", "Medium", "Low", "None"
        public bool IsExplicitAssemblyMark { get; set; }
        public bool UsedFallback { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
