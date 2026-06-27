using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Mappers
{
    /// <summary>
    /// Builds the simple DTOs used by the GA debug CSV export.
    /// </summary>
    internal static class GADebugReportMapper
    {
        public static GADebugReportDto ToDebugReportDto(this CranePlacementResult result)
        {
            var coveredElements = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<GADebugReportRowDto>();

            for (int index = 0; index < result.Positions.Count; index++)
            {
                var position = result.Positions[index];

                foreach (var element in position.ElementResults)
                {
                    bool newlyCovered = element.Status == LiftStatus.Liftable
                        && coveredElements.Add(element.ElementId);

                    rows.Add(new GADebugReportRowDto(
                        index + 1,
                        position.X,
                        position.Y,
                        element.ElementId,
                        element.ElementMark ?? string.Empty,
                        element.ElementType ?? string.Empty,
                        element.Weight,
                        element.CogX,
                        element.CogY,
                        element.CogZ,
                        element.Radius,
                        element.BoomLength,
                        element.RequiredCapacity,
                        element.AvailableCapacity,
                        element.Status.ToString(),
                        newlyCovered,
                        element.ReachStatus.ToString(),
                        element.ChartStatus.ToString(),
                        element.IsExactMatch,
                        element.IsSteppedRadius,
                        element.IsBlankCell,
                        element.IsZeroCell,
                        element.SafetyMargin,
                        element.LookupMode,
                        element.RequiredRadius,
                        element.SelectedChartRadius,
                        element.SelectedChartBoomLength));
                }
            }

            return new GADebugReportDto(
                Guid.NewGuid(),
                DateTime.UtcNow,
                rows,
                result.DebugReportError);
        }
    }
}
