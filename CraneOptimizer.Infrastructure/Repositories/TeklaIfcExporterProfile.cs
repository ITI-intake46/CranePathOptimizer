using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class TeklaIfcExporterProfile : IIfcExporterProfile
    {
        public IfcExporterProfile Exporter => IfcExporterProfile.Tekla;

        public bool IsStructuralGroupingAssembly(string assemblyName, string objectType)
        {
            return false;
        }

        public bool IsPrecastLiftAssembly(string assemblyName, string objectType)
        {
            var combined = $"{assemblyName} {objectType}".ToUpperInvariant();
            return combined.Contains("CAST UNIT") || 
                   combined.Contains("ASSEMBLY") || 
                   combined.Contains("WELDED") || 
                   combined.Contains("PRECAST");
        }

        public bool IsWeightPropertyTrusted(string propertyName, string psetName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return false;

            var prop = propertyName.ToUpperInvariant();
            return prop.Contains("WEIGHT") || prop.Contains("MASS");
        }
    }
}
