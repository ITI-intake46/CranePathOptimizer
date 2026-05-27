using CraneOptimizer.Domain.Diagnostics;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcExporterProfileResolver
    {
        IfcExporterProfile ResolveProfile(string exporterApplication);
    }
}
