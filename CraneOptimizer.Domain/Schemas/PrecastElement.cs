using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using System.Collections.Generic;

namespace CraneOptimizer.Domain.Schemas
{
    public class PrecastElement
    {
        public string ElementID { get; set; }

        public string? ElementRevitId { get; set; }

        public string? ElementMark { get; set; }

        public bool ExplicitAssemblyMarkFound { get; set; }

        public string? ResolvedDisplayMark { get; set; }

        public string ElementType { get; set; }

        public double? ElementLength { get; set; }

        public double? ElementWidth { get; set; }

        public double? ElementHeight { get; set; }

        public string ElementMaterial { get; set; }

        public double? ElementWT { get; set; }

        public double? ElementVolume { get; set; }

        public DomainPoint3D ElementCOG { get; set; } = new DomainPoint3D();
        public double? BBoxLength { get; set; }
        public double? BBoxWidth { get; set; }
        public double? BBoxHeight { get; set; }

        // Phase 2A Normalization fields
        public string RawIfcType { get; set; } = string.Empty;
        public string ResolvedCategory { get; set; } = string.Empty;
        public IfcElementRole Role { get; set; } = IfcElementRole.LiftableElement;
        public string? ParentGlobalId { get; set; }
        public List<string> ChildGlobalIds { get; set; } = new();
        public List<string> VisibleGeometryOwnerGlobalIds { get; set; } = new();
        public List<string> HiddenInternalGeometryOwnerGlobalIds { get; set; } = new();
        public bool IncludeInRender { get; set; } = true;
        public bool IncludeInElementTree { get; set; } = true;
        public bool IncludeInGa { get; set; } = true;
        public double? WeightTons { get; set; }
        public string WeightSource { get; set; } = "Missing";
        public IfcWeightTrustLevel WeightTrustLevel { get; set; } = IfcWeightTrustLevel.Missing;
        public double? VolumeM3 { get; set; }
        public DomainPoint3D COG_M { get; set; } = new();
        public DomainPoint3D BoundingBox_M { get; set; } = new();
        public double? DensityKgPerM3 { get; set; }

        public bool IsFromRevitAssemblyWrapper { get; set; }
        public bool MaterialInheritedFromParent { get; set; }
        public bool DensityInheritedFromParent { get; set; }

        public string? ParentAssemblyName { get; set; }
        public List<string> ExtractionWarnings { get; set; } = new();
        public string NormalizationDecisionReason { get; set; } = string.Empty;
    }
}

