using CraneOptimizer.Domain.Diagnostics;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcExporterProfile
    {
        IfcExporterProfile Exporter { get; }

        /// <summary>
        /// True if the assembly acts as a hierarchical grouping container (e.g. Revit Beam Systems)
        /// rather than a single precast lift item.
        /// </summary>
        bool IsStructuralGroupingAssembly(string assemblyName, string objectType);

        /// <summary>
        /// True if the assembly represents a single precast lift unit (e.g. Tekla Cast Unit / Welded frame).
        /// </summary>
        bool IsPrecastLiftAssembly(string assemblyName, string objectType);

        /// <summary>
        /// Returns true if a given weight property name is highly trusted for total element weight.
        /// </summary>
        bool IsWeightPropertyTrusted(string propertyName, string psetName);
    }
}
