using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public interface IIfcVisibilityOptionsService
    {
        IReadOnlyList<IfcVisibilityOptionDefinition> OptionDefinitions { get; }
        IfcLoadOptionsDto GetCurrentOptions(CoordinateReferenceMode fallbackCoordinateMode = CoordinateReferenceMode.LocalOnly);
        void StoreCurrentOptions(IfcLoadOptionsDto options);
        void ApplyOptions(IfcLoadOptionsDto options);
    }
}
