using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public enum IfcElementRole
    {
        LiftableElement,
        LiftAssembly,
        GroupingOnly,
        InternalAuxiliary,
        Excluded,
        NeedsReview
    }

    public enum IfcAssemblyDecision
    {
        NotAssembly,
        SingleChildWrapper,
        RevitStructuralBeamSystemGrouping,
        TeklaCastUnitOrWeldedLiftAssembly,
        GenericGroupingAssembly,
        HostWithInternalAuxiliaries,
        UnknownNeedsReview
    }

    public enum IfcWeightTrustLevel
    {
        ExplicitQuantity,
        ExplicitProperty,
        ExplicitZeroFromIFC,
        Missing,
        EstimatedFallback_NotAllowedForGA,
        AmbiguousUnit,
        SuspiciousExtreme,
        RequiresUserApproval
    }

    public enum IfcExporterProfile
    {
        Generic,
        Tekla,
        Revit
    }

    public sealed class IfcNormalizedElementPreview
    {
        public string GlobalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RawIfcType { get; set; } = string.Empty;
        public string CandidateCategory { get; set; } = string.Empty;
        public string? ParentGlobalId { get; set; }
        public string? ParentGlobalIdSource { get; set; }
        public List<string> ChildGlobalIds { get; set; } = new();
        public List<string> VisibleGeometryOwnerGlobalIds { get; set; } = new();
        public List<string> HiddenInternalGeometryOwnerGlobalIds { get; set; } = new();
        public bool IncludeInRenderPreview { get; set; } = true;
        public bool IncludeInElementTreePreview { get; set; } = true;
        public bool IncludeInGaPreview { get; set; } = true;
        public bool IncludeInWeightRollupPreview { get; set; } = true;
        public string WeightSource { get; set; } = "Missing";
        public double? WeightRawValue { get; set; }
        public double? WeightStandardValueTons { get; set; }
        public IfcWeightTrustLevel WeightTrustLevel { get; set; } = IfcWeightTrustLevel.Missing;
        public string DecisionReason { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public IfcElementRole Role { get; set; } = IfcElementRole.NeedsReview;
        public IfcAssemblyDecision AssemblyDecision { get; set; } = IfcAssemblyDecision.NotAssembly;
        
        // Internal tracking fields for reinforcement weight roll-ups
        public double? ReinforcementWeightContributionTons { get; set; }
        public bool HasMissingReinforcementWeight { get; set; }
        public bool HasChildren { get; set; }
    }
    
    public sealed class IfcNormalizationPreviewSummary
    {
        public string FileName { get; set; } = string.Empty;
        public IfcExporterProfile DetectedProfile { get; set; } = IfcExporterProfile.Generic;
        public string ExporterApplication { get; set; } = string.Empty;
        public int TotalRawProductCount { get; set; }
        public int NormalizedLiftableCount { get; set; }
        public int GroupingOnlyCount { get; set; }
        public int LiftAssemblyCount { get; set; }
        public int InternalAuxiliaryCount { get; set; }
        public int ExcludedCount { get; set; }
        public int NeedsReviewCount { get; set; }
        public int VisibleRenderGeometryCount { get; set; }
        public int HiddenInternalGeometryCount { get; set; }
        public int BlockedGaMissingWeightCount { get; set; }
        public int BlockedGaZeroWeightCount { get; set; }
        public int BlockedGaFallbackWeightCount { get; set; }
        public List<IfcNormalizedElementPreview> Elements { get; set; } = new();
    }

    public enum AssemblyMarkConfidence
    {
        None,
        Low,
        Medium,
        High
    }

    public enum ValidationStatus
    {
        Valid,
        Warning,
        Error
    }

    public enum SourceSystem
    {
        IFC,
        Revit,
        Tekla,
        JSON
    }
}
