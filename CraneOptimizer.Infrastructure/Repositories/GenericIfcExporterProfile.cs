using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class GenericIfcExporterProfile : IIfcExporterProfile
    {
        public IfcExporterProfile Exporter => IfcExporterProfile.Generic;

        public IReadOnlyList<string> AssemblyIdentityAliases { get; } = new[]
        {
            "Assembly Mark", "Cast Unit Mark", "Assembly Number", "Cast Unit Number",
            "Cast Unit Position", "Assembly Position", "AssemblyName", "AssemblyID"
        };

        public IReadOnlyList<string> ElementIdentityAliases { get; } = new[]
        {
            "Mark", "Element Mark", "Part Mark", "PartMark", "Part Position", "PartPosition", "Position", "Tag"
        };

        public IReadOnlyList<string> TypeReferenceAliases { get; } = new[]
        {
            "Type Mark", "TypeMark", "Reference", "Type Name", "ObjectType", "Family", "Family Name"
        };

        public IReadOnlyList<string> WeightAliases { get; } = new[]
        {
            "Weight", "Mass", "NetWeight", "GrossWeight", "SelfWeight", "DeadLoad", "ShippingWeight", "AssemblyWeight", "ElementWeight"
        };

        public IReadOnlyList<string> VolumeAliases { get; } = new[]
        {
            "Volume", "NetVolume", "GrossVolume", "BaseQuantities.NetVolume", "BaseQuantities.GrossVolume"
        };

        public IReadOnlyList<string> MaterialAliases { get; } = new[]
        {
            "Material", "Structural Material", "Object Material", "IfcMaterial", "Layer Material", "Type Material", "Concrete Grade"
        };

        public IReadOnlyList<string> LevelAliases { get; } = new[]
        {
            "Top Level", "Bottom Level", "Top Elevation", "Bottom Elevation", "Base Constraint", "Top Constraint", "Reference Level", "Building Storey", "Storey"
        };

        public bool IsStructuralGroupingAssembly(string assemblyName, string objectType)
        {
            return false;
        }

        public bool IsPrecastLiftAssembly(string assemblyName, string objectType)
        {
            return false;
        }

        public bool IsWeightPropertyTrusted(string propertyName, string psetName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return false;

            var prop = propertyName.ToUpperInvariant();
            return prop.Contains("WEIGHT") || prop.Contains("MASS");
        }
    }
}
