using System;
using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class ValidationMessage
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info"; // Info / Warning / Error
        public string Message { get; set; } = string.Empty;
        public int AffectedCount { get; set; }
        public IReadOnlyList<string> AffectedElementIds { get; set; } = Array.Empty<string>();
        public string? RecommendedAction { get; set; }
        public SourceSystem? SourceSystem { get; set; }
        public string? Category { get; set; } // Identity / Weight / Geometry / GA / Mapping
    }
}
