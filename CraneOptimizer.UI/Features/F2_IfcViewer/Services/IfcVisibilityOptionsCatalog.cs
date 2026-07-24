using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Services
{
    public static class IfcVisibilityOptionsCatalog
    {
        public static IReadOnlyList<IfcVisibilityOptionDefinition> Definitions { get; } =
            new[]
            {
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideGrids),
                    "Hide Grids",
                    new[] { "IfcGrid" },
                    options => options.HideGrids),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideLevels),
                    "Hide Levels / Storeys",
                    new[] { "IfcBuildingStorey" },
                    options => options.HideLevels),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideAnnotations),
                    "Hide Annotations",
                    new[] { "IfcAnnotation" },
                    options => options.HideAnnotations),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideSite),
                    "Hide Site",
                    new[] { "IfcSite" },
                    options => options.HideSite),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideSpaces),
                    "Hide Spaces / Rooms",
                    new[] { "IfcSpace" },
                    options => options.HideSpaces),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideOpenings),
                    "Hide Opening Elements",
                    new[] { "IfcOpeningElement" },
                    options => options.HideOpenings),
                new IfcVisibilityOptionDefinition(
                    nameof(IfcLoadOptionsDto.HideVirtualElements),
                    "Hide Virtual Elements",
                    new[] { "IfcVirtualElement" },
                    options => options.HideVirtualElements)
            };

        public static IfcLoadOptionsDto CreateDefaultOptions(CoordinateReferenceMode coordinateMode = CoordinateReferenceMode.LocalOnly)
        {
            return new IfcLoadOptionsDto
            {
                HideGrids = true,
                HideLevels = true,
                HideAnnotations = true,
                HideSite = false,
                HideSpaces = true,
                HideOpenings = true,
                HideVirtualElements = true,
                CoordinateMode = coordinateMode
            };
        }

        public static bool IsHiddenByOptions(IfcLoadOptionsDto options, CraneOptimizer.Domain.Schemas.PrecastElement element)
        {
            return Definitions.Any(definition => definition.IsHidden(options) && definition.MatchesElement(element));
        }
    }
}
