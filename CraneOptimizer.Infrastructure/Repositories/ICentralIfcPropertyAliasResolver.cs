using CraneOptimizer.Domain.Diagnostics;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public interface ICentralIfcPropertyAliasResolver
    {
        SourcePropertyResolutionResult ResolveProperty(IIfcProduct product, string logicalField, IfcExporterProfile profile);
        CanonicalAssemblyIdentityResult ResolveCanonicalAssemblyIdentity(IIfcProduct? product, string previewName, IfcExporterProfile profile);
    }
}
