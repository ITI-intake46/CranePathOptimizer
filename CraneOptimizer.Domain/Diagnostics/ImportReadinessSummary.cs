using System;
using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class ImportReadinessSummary
    {
        public int TotalElements { get; set; }
        public int RenderableElements { get; set; }
        public int GaCandidateElements { get; set; }
        public int GaBlockedElements { get; set; }

        public int MissingAssemblyMarkCount { get; set; }
        public int LowConfidenceMappingCount { get; set; }
        public int EstimatedWeightBlockedCount { get; set; }
        public int MissingTrustedWeightCount { get; set; }
        public int MissingVolumeCount { get; set; }
        public int MissingMaterialCount { get; set; }
        public int MissingCogCount { get; set; }
        public int ParentChildDoubleCountRiskCount { get; set; }

        public ValidationStatus OverallStatus { get; set; } = ValidationStatus.Valid;
        public IReadOnlyList<ValidationMessage> Messages { get; set; } = Array.Empty<ValidationMessage>();
        public IReadOnlyList<ImportReadinessElementDiagnostic> ElementDiagnostics { get; set; } = Array.Empty<ImportReadinessElementDiagnostic>();
    }
}
