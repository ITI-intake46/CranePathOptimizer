using System.Collections.Generic;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class IfcAuditResult
    {
        public IfcAuditMetadata Metadata { get; set; } = new();
        public List<RawIfcTypeSummary> RawTypes { get; set; } = new();
        public AssemblyDecompositionAudit Assemblies { get; set; } = new();
        public List<ElementClassificationAuditDetail> Classifications { get; set; } = new();
        public List<ElementWeightAuditDetail> Weights { get; set; } = new();
        public List<ElementVolumeMaterialAuditDetail> VolumesMaterials { get; set; } = new();
        public List<ElementGeometryOwnershipAuditDetail> Geometries { get; set; } = new();
    }

    public sealed class IfcAuditMetadata
    {
        public string FileName { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty; // e.g. "IFC2X3", "IFC4", "IFC4X3"
        public string ExporterApplication { get; set; } = string.Empty;
        public string LengthUnit { get; set; } = "m";
        public double LengthConversionFactor { get; set; } = 1.0;
        public string AreaUnit { get; set; } = "m²";
        public double AreaConversionFactor { get; set; } = 1.0;
        public string VolumeUnit { get; set; } = "m³";
        public double VolumeConversionFactor { get; set; } = 1.0;
        public string MassUnit { get; set; } = "kg";
        public double? MassConversionFactor { get; set; } = 1.0;
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class RawIfcTypeSummary
    {
        public string RawIfcClass { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int CountWithGlobalId { get; set; }
        public int CountWithName { get; set; }
        public int CountWithObjectPlacement { get; set; }
        public int CountWithShapeInstances { get; set; }
        public int CountWithQuantitySets { get; set; }
        public int CountWithPropertySets { get; set; }
        public int CountWithMaterialAssociation { get; set; }
    }

    public sealed class AssemblyDecompositionAudit
    {
        public int TotalAssemblyCount { get; set; }
        public int AssembliesWithChildren { get; set; }
        public int AssembliesWithoutChildren { get; set; }
        public int TotalDirectChildCount { get; set; }
        public int TotalRecursiveChildCount { get; set; }
        public List<string> ChildRawIfcTypes { get; set; } = new();
        public int MaxDecompositionDepth { get; set; }
        public int DoubleCountingRisksCount { get; set; }
        public int ChildrenSwallowedCount { get; set; }
        public List<AssemblyDecompositionDetail> Details { get; set; } = new();
    }

    public sealed class AssemblyDecompositionDetail
    {
        public string AssemblyGlobalId { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public int DirectChildCount { get; set; }
        public int RecursiveChildCount { get; set; }
        public List<string> ChildTypes { get; set; } = new();
        public int NestingDepth { get; set; }
        public bool ParentHasGeometry { get; set; }
        public bool ChildrenHaveGeometry { get; set; }
        public string GeometryOwnershipPattern { get; set; } = "None"; // "ParentOnly", "ChildrenOnly", "Both", "None"
        public string NestingRelationshipType { get; set; } = "IfcRelAggregates"; // "IfcRelAggregates", "IfcRelNests", "Mixed"
        public List<AssemblyChildDetail> ChildDetails { get; set; } = new();
    }

    public sealed class AssemblyChildDetail
    {
        public string ChildGlobalId { get; set; } = string.Empty;
        public string ChildRawIfcType { get; set; } = string.Empty;
        public string ChildName { get; set; } = string.Empty;
        public bool HasGeometry { get; set; }
        public bool DoubleCountingRisk { get; set; }
        public bool SwallowedRisk { get; set; }
    }

    public sealed class ElementClassificationAuditDetail
    {
        public string GlobalId { get; set; } = string.Empty;
        public int EntityLabel { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RawIfcType { get; set; } = string.Empty;
        public string ExpressTypeName { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public string PredefinedType { get; set; } = string.Empty;
        public string CandidateResolvedCategory { get; set; } = string.Empty; // Column, Beam, Slab, etc.
        public string CandidateReason { get; set; } = string.Empty;
        public bool CurrentUiShowAsAssembly { get; set; }
    }

    public sealed class ElementWeightAuditDetail
    {
        public string GlobalId { get; set; } = string.Empty;
        public string ResolvedType { get; set; } = string.Empty;
        public double? WeightRawValue { get; set; }
        public double? WeightStandardValueTons { get; set; }
        public string WeightSource { get; set; } = "Missing"; // "IfcQuantityWeight.Instance", etc.
        public string WeightStatus { get; set; } = "MissingWeight"; // "ExplicitPositive", etc.
        public string WeightUnitDetected { get; set; } = string.Empty;
        public string WeightPropertyName { get; set; } = string.Empty;
        public string QuantitySetNameOrPropertySetName { get; set; } = string.Empty;
        public bool IsWeightUsedByProductionCurrently { get; set; }
        public string Warning { get; set; } = string.Empty;
    }

    public sealed class ElementVolumeMaterialAuditDetail
    {
        public string GlobalId { get; set; } = string.Empty;
        public string VolumeSource { get; set; } = "Missing";
        public double? VolumeRawValue { get; set; }
        public double? VolumeStandardValueM3 { get; set; }
        public string MaterialSource { get; set; } = "Missing";
        public string MaterialName { get; set; } = string.Empty;
        public bool IsMaterialFromInstance { get; set; }
        public string MaterialAssociationType { get; set; } = "Unknown"; // "Simple", "LayerSet", "ProfileSet", "ConstituentSet", "Unknown"
        public bool WeightFallbackDependsOnMaterial { get; set; }
    }

    public sealed class ElementGeometryOwnershipAuditDetail
    {
        public string LogicalGlobalId { get; set; } = string.Empty;
        public int? ShapeOwnerLabel { get; set; }
        public int ShapeCount { get; set; }
        public bool HasGeometryDirectly { get; set; }
        public bool HasGeometryOnlyOnParent { get; set; }
        public bool HasGeometryOnlyOnChildren { get; set; }
        public bool HasNoGeometry { get; set; }
        public string GeometryOwnerCandidateGlobalId { get; set; } = string.Empty;
        public string RenderRisk { get; set; } = "OK"; // "OK", "ChildHasGeometryButParentIsExtracted", etc.
    }
}
