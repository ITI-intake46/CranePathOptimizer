using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class GenericIfcExporterProfile : IIfcExporterProfile
    {
        public IfcExporterProfile Exporter => IfcExporterProfile.Generic;

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
