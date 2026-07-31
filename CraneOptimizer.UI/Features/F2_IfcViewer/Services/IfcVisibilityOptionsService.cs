using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public sealed class IfcVisibilityOptionsService : IIfcVisibilityOptionsService
    {
        private readonly IElementVisibilityFilterService _filterService;
        private IfcLoadOptionsDto _currentOptions = IfcVisibilityOptionsCatalog.CreateDefaultOptions();

        public IfcVisibilityOptionsService(IElementVisibilityFilterService filterService)
        {
            _filterService = filterService;
        }

        public IReadOnlyList<IfcVisibilityOptionDefinition> OptionDefinitions => IfcVisibilityOptionsCatalog.Definitions;

        public IfcLoadOptionsDto GetCurrentOptions(CoordinateReferenceMode fallbackCoordinateMode = CoordinateReferenceMode.LocalOnly)
        {
            return _currentOptions with
            {
                CoordinateMode = _currentOptions.CoordinateMode == default
                    ? fallbackCoordinateMode
                    : _currentOptions.CoordinateMode
            };
        }

        public void StoreCurrentOptions(IfcLoadOptionsDto options)
        {
            _currentOptions = options;
        }

        public void ApplyOptions(IfcLoadOptionsDto options)
        {
            StoreCurrentOptions(options);
            ApplyOptionsToFilter(_filterService, options);
        }

        public static void ApplyOptionsToFilter(IElementVisibilityFilterService filterService, IfcLoadOptionsDto options)
        {
            filterService.ApplyLoadOptions(options);
        }
    }
}
