using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class IfcExporterProfileResolver : IIfcExporterProfileResolver
    {
        public IfcExporterProfile ResolveProfile(string exporterApplication)
        {
            if (string.IsNullOrWhiteSpace(exporterApplication))
                return IfcExporterProfile.Generic;

            var app = exporterApplication.ToUpperInvariant();

            if (app.Contains("TEKLA"))
                return IfcExporterProfile.Tekla;

            if (app.Contains("REVIT"))
                return IfcExporterProfile.Revit;

            return IfcExporterProfile.Generic;
        }
    }
}
