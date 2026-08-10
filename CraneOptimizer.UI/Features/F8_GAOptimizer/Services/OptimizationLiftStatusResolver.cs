using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public sealed record OptimizationLiftStatusSelection(
        IReadOnlySet<string> LiftableLiftUnitIds,
        IReadOnlySet<string> NotLiftableLiftUnitIds,
        IReadOnlySet<string> LiftableVisualElementIds,
        IReadOnlySet<string> NotLiftableVisualElementIds);

    public sealed class OptimizationLiftStatusData
    {
        private static readonly IReadOnlySet<string> EmptyIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly IReadOnlyDictionary<CranePositionResult, IReadOnlySet<string>>
            _liftableByPosition;
        private readonly IReadOnlyDictionary<string, IReadOnlySet<string>>
            _visualIdsByLiftUnit;

        internal OptimizationLiftStatusData(
            IReadOnlySet<string> eligibleLiftUnitIds,
            IReadOnlyDictionary<CranePositionResult, IReadOnlySet<string>> liftableByPosition,
            IReadOnlyDictionary<string, IReadOnlySet<string>> visualIdsByLiftUnit)
        {
            EligibleLiftUnitIds = eligibleLiftUnitIds;
            _liftableByPosition = liftableByPosition;
            _visualIdsByLiftUnit = visualIdsByLiftUnit;

            HashSet<string> overallLiftable = new(StringComparer.Ordinal);
            foreach (IReadOnlySet<string> positionIds in liftableByPosition.Values)
                overallLiftable.UnionWith(positionIds);

            OverallLiftableIds = overallLiftable;
            HashSet<string> overallNotLiftable =
                eligibleLiftUnitIds.ToHashSet(StringComparer.Ordinal);
            overallNotLiftable.ExceptWith(overallLiftable);
            OverallNotLiftableIds = overallNotLiftable;
        }

        public IReadOnlySet<string> EligibleLiftUnitIds { get; }
        public IReadOnlySet<string> OverallLiftableIds { get; }
        public IReadOnlySet<string> OverallNotLiftableIds { get; }

        public IReadOnlySet<string> GetLiftableIds(CranePositionResult? position)
        {
            if (position != null &&
                _liftableByPosition.TryGetValue(position, out IReadOnlySet<string>? ids))
            {
                return ids;
            }

            return EmptyIds;
        }

        public OptimizationLiftStatusSelection CreateSelection(
            bool showOverall,
            CranePositionResult? selectedPosition)
        {
            IReadOnlySet<string> liftable = showOverall
                ? OverallLiftableIds
                : GetLiftableIds(selectedPosition);
            IReadOnlySet<string> notLiftable = showOverall
                ? OverallNotLiftableIds
                : EmptyIds;

            return new OptimizationLiftStatusSelection(
                liftable,
                notLiftable,
                ExpandVisualIds(liftable),
                ExpandVisualIds(notLiftable));
        }

        private IReadOnlySet<string> ExpandVisualIds(IReadOnlySet<string> liftUnitIds)
        {
            HashSet<string> visualIds = new(StringComparer.Ordinal);
            foreach (string liftUnitId in liftUnitIds)
            {
                if (_visualIdsByLiftUnit.TryGetValue(
                        liftUnitId,
                        out IReadOnlySet<string>? mappedIds))
                {
                    visualIds.UnionWith(mappedIds);
                }
                else
                {
                    visualIds.Add(liftUnitId);
                }
            }

            return visualIds;
        }
    }

    public static class OptimizationLiftStatusResolver
    {
        public static OptimizationLiftStatusData Resolve(CranePlacementResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            HashSet<string> eligibleIds = new(StringComparer.Ordinal);
            Dictionary<CranePositionResult, IReadOnlySet<string>> byPosition = new();
            Dictionary<string, HashSet<string>> visualIdsByLiftUnit =
                new(StringComparer.Ordinal);

            IEnumerable<CranePositionResult> positions =
                result.Positions ?? Enumerable.Empty<CranePositionResult>();
            foreach (CranePositionResult position in positions)
            {
                HashSet<string> liftableIds = new(StringComparer.Ordinal);
                IEnumerable<ElementLiftResult> evaluations =
                    position.ElementResults ?? Enumerable.Empty<ElementLiftResult>();
                foreach (ElementLiftResult evaluation in evaluations)
                {
                    string liftUnitId = evaluation.ElementId ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(liftUnitId))
                        continue;

                    eligibleIds.Add(liftUnitId);
                    HashSet<string> visualIds = GetOrCreateVisualIds(
                        visualIdsByLiftUnit,
                        liftUnitId);
                    visualIds.Add(liftUnitId);

                    IEnumerable<string> assemblyMemberIds = evaluation.IsAssemblyGroup
                        ? evaluation.AssemblyMemberIds ?? Enumerable.Empty<string>()
                        : Enumerable.Empty<string>();
                    foreach (string memberId in assemblyMemberIds)
                    {
                        if (!string.IsNullOrWhiteSpace(memberId))
                            visualIds.Add(memberId);
                    }

                    if (evaluation.Status == LiftStatus.Liftable)
                        liftableIds.Add(liftUnitId);
                }

                byPosition[position] = liftableIds;
            }

            Dictionary<string, IReadOnlySet<string>> visualMap =
                visualIdsByLiftUnit.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlySet<string>)pair.Value,
                    StringComparer.Ordinal);

            return new OptimizationLiftStatusData(
                eligibleIds,
                byPosition,
                visualMap);
        }

        private static HashSet<string> GetOrCreateVisualIds(
            IDictionary<string, HashSet<string>> visualIdsByLiftUnit,
            string liftUnitId)
        {
            if (!visualIdsByLiftUnit.TryGetValue(
                    liftUnitId,
                    out HashSet<string>? visualIds))
            {
                visualIds = new HashSet<string>(StringComparer.Ordinal);
                visualIdsByLiftUnit.Add(liftUnitId, visualIds);
            }

            return visualIds;
        }
    }
}
