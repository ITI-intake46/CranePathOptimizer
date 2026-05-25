using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class RevitIfcExporterProfile : IIfcExporterProfile
    {
        public IfcExporterProfile Exporter => IfcExporterProfile.Revit;

        public bool IsStructuralGroupingAssembly(string assemblyName, string objectType)
        {
            var combined = $"{assemblyName} {objectType}".ToUpperInvariant();
            return combined.Contains("STRUCTURAL BEAM SYSTEM") || 
                   combined.Contains("STRUCTURAL FRAMING SYSTEM") || 
                   combined.Contains("BEAM SYSTEM");
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
