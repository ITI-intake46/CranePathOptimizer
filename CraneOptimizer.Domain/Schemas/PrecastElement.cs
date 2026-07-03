using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using System;
using System.Collections.Generic;

namespace CraneOptimizer.Domain.Schemas
{
    public class PrecastElement
    {
        public PrecastElement CreateCopy()
        {
            var copy = (PrecastElement)MemberwiseClone();
            copy.ElementCOG = new DomainPoint3D(ElementCOG.X, ElementCOG.Y, ElementCOG.Z);
            copy.COG_M = new DomainPoint3D(COG_M.X, COG_M.Y, COG_M.Z);
            copy.BoundingBox_M = new DomainPoint3D(BoundingBox_M.X, BoundingBox_M.Y, BoundingBox_M.Z);
            copy.ChildGlobalIds = new List<string>(ChildGlobalIds);
            copy.VisibleGeometryOwnerGlobalIds = new List<string>(VisibleGeometryOwnerGlobalIds);
            copy.HiddenInternalGeometryOwnerGlobalIds = new List<string>(HiddenInternalGeometryOwnerGlobalIds);
            copy.ExtractionWarnings = new List<string>(ExtractionWarnings);
            copy.ValidationWarnings = new List<string>(ValidationWarnings);
            return copy;
        }

        public string ElementID { get; set; }

        public string? ElementRevitId { get; set; }

        public string? ElementMark { get; set; }

        public string? ElementName { get; set; }

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
        public string? ParentGlobalIdSource { get; set; }
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

        // Canonical Assembly Data Schema (Phase 2)
        public string? AssemblyMark { get; set; }
        public string? AssemblyMarkSource { get; set; }
        public AssemblyMarkConfidence AssemblyMarkConfidence { get; set; } = AssemblyMarkConfidence.None;
        public bool AssemblyMarkIsExplicit { get; set; }
        public bool PhysicalCOGAvailable { get; set; } = true;

        // Phase 3 Assembly Classification fields
        public string? MainPartGlobalId { get; set; }
        public string? MainPartName { get; set; }
        public string? MainPartCategory { get; set; }
        public string? MainPartDetectionMethod { get; set; }
        public int ChildCount { get; set; }
        public int VisibleSecondaryPartCount { get; set; }
        public int HiddenWeightContributorCount { get; set; }
        public int AccessoryWeightContributorCount { get; set; }
        public int IgnoredReferenceCount { get; set; }
        public bool HasSecondaryParts { get; set; }
        public bool HasHiddenContributors { get; set; }

        // Non-authoritative mirrors of active engineering fields
        public double? AssemblyWeightTons => WeightTons;
        public string? AssemblyWeightSource => WeightSource;
        public IfcWeightTrustLevel AssemblyWeightTrustLevel => WeightTrustLevel;
        public double? AssemblyVolumeM3 => VolumeM3;
        public string? AssemblyMaterial => ElementMaterial;
        public DomainPoint3D AssemblyCenterOfGravity => COG_M;
        public double? AssemblyLength => ElementLength;
        public double? AssemblyWidth => ElementWidth;
        public double? AssemblyHeight => ElementHeight;

        // Phase 5 In-memory Assembly Rollup properties
        public double? AssemblyLiftWeight { get; set; }
        public DomainPoint3D? AssemblyLiftCOG { get; set; }
        public List<string> AssemblyMemberIds { get; set; } = new();
        public Diagnostics.AssemblyChildClassification ChildClassification { get; set; } = Diagnostics.AssemblyChildClassification.UnknownNeedsReview;

        public SourceSystem SourceSystem { get; set; } = SourceSystem.IFC;
        public IfcExporterProfile ExporterProfile { get; set; } = IfcExporterProfile.Generic;
        public string? SourceGlobalId => ElementID;
        public string? ParentAssemblyGlobalId => ParentGlobalId;

        public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Valid;
        public List<string> ValidationWarnings { get; set; } = new();
    }
}

