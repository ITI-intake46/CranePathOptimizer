using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public enum AssemblyChildClassification
    {
        MainPart,
        VisibleSecondaryPart,
        HiddenWeightContributor,
        AccessoryWeightContributor,
        IgnoredReference,
        UnknownNeedsReview
    }

    public enum AssemblyMainPartDecision
    {
        ExplicitMainPart,
        TrustedTeklaProperty,
        InferredHighConfidence,
        InferredLowConfidence,
        NotDetectable
    }

    public sealed class AssemblyGraphNode
    {
        public string ChildGlobalId { get; set; } = string.Empty;
        public string ChildName { get; set; } = string.Empty;
        public string ChildIfcClass { get; set; } = string.Empty;
        public string ChildCategory { get; set; } = string.Empty;
        public double? ChildWeightTons { get; set; }
        public AssemblyChildClassification ChildClassification { get; set; } = AssemblyChildClassification.UnknownNeedsReview;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AssemblyRelationshipInfo
    {
        public string ParentGlobalId { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string ParentGlobalIdSource { get; set; } = string.Empty; // "IfcDecomposition", "IfcRelNests", "TeklaCastUnit"
        public string AssemblyMark { get; set; } = string.Empty;

        public string? MainPartGlobalId { get; set; }
        public string? MainPartName { get; set; }
        public string? MainPartCategory { get; set; }
        public AssemblyMainPartDecision MainPartDetectionMethod { get; set; } = AssemblyMainPartDecision.NotDetectable;
        public string MainPartConfidence => MainPartDetectionMethod.ToString();

        public int ChildCount { get; set; }
        public int VisibleSecondaryPartCount { get; set; }
        public int HiddenWeightContributorCount { get; set; }
        public int AccessoryWeightContributorCount { get; set; }
        public int IgnoredReferenceCount { get; set; }
        public int UnknownNeedsReviewCount { get; set; }

        public bool HasSecondaryParts => VisibleSecondaryPartCount > 0;
        public bool HasHiddenContributors => HiddenWeightContributorCount > 0;

        public List<string> Warnings { get; set; } = new();
        public List<AssemblyGraphNode> Children { get; set; } = new();
    }
}
