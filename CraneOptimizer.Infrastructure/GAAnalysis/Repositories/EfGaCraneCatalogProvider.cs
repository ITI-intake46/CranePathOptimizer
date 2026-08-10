using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Infrastructure.GAAnalysis;

/// <summary>
/// Reads a detached catalog snapshot through a fresh DbContext for each analysis job.
/// </summary>
public sealed class EfGaCraneCatalogProvider : IGaCraneCatalogProvider
{
    private readonly IDbContextFactory<CraneDbContext> _contextFactory;

    public EfGaCraneCatalogProvider(IDbContextFactory<CraneDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<GaCraneCandidate>> GetCandidatesAsync(
        int? currentCraneSpecificationId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var specifications = await context.CraneSpecifications
            .AsNoTracking()
            .Where(specification => specification.Id != currentCraneSpecificationId)
            .OrderBy(specification => specification.MaxCapacity)
            .ThenBy(specification => specification.MaxRadius)
            .ThenBy(specification => specification.Id)
            .Select(specification => new GaCraneCandidateSpecification
            {
                Id = specification.Id,
                CraneName = specification.Crane.Name,
                MaxCapacity = specification.MaxCapacity,
                MaxRadius = specification.MaxRadius,
                MinRadius = specification.MinRadius,
                MaxBoomLength = specification.MaxBoomLength,
                MinBoomLength = specification.MinBoomLength,
                CounterWeight = specification.CounterWeight,
                MachineRotation = specification.MachineRotation,
                CraneDerating = specification.CraneDerating,
                OutriggerConfig = specification.OutriggerConfig,
                LoadChartStorageMode = specification.LoadChartStorageMode,
                LoadChartCapacityPolicy = specification.LoadChartCapacityPolicy,
                ExactLoadChartRawGridJson = specification.ExactLoadChartRawGridJson,
                LoadChartSourceDescription = specification.LoadChartSourceDescription,
                LoadChart = specification.LoadChart
                    .OrderBy(point => point.Radius)
                    .ThenBy(point => point.BoomLength)
                    .ThenBy(point => point.VisualColumnIndex)
                    .Select(point => new GaCraneCandidateLoadChartPoint
                    {
                        Radius = point.Radius,
                        BoomLength = point.BoomLength,
                        Capacity = point.Capacity,
                        VisualColumnIndex = point.VisualColumnIndex,
                        VisualColumnHeader = point.VisualColumnHeader,
                        NumberOfLines = point.NumberOfLines,
                        OperatingConditionLabel = point.OperatingConditionLabel
                    })
                    .ToArray()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return specifications
            .Select(specification => new GaCraneCandidate(
                specification,
                BuildDisplayName(specification.CraneName, specification.Id)))
            .ToArray();
    }

    private static string BuildDisplayName(string? craneName, int specificationId) =>
        string.IsNullOrWhiteSpace(craneName)
            ? $"Crane specification {specificationId}"
            : $"{craneName} — specification {specificationId}";
}
