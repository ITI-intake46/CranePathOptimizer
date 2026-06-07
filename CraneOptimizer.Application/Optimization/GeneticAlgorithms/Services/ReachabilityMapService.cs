using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class ReachabilityMapService : IReachabilityMapService
    {
        private readonly IConstraintEngine _constraintEngine;
        private readonly ILoadChartCapacityResolver _capacityResolver;
        private readonly object _summaryLock = new();
        private List<ZoneReachabilitySummary> _lastZoneSummaries = new();
        private readonly Dictionary<(Guid ZoneId, double X, double Y), int> _selectedSeedCounts = new();

        public ReachabilityDiagnostics? LastDiagnostics { get; private set; }
        public IReadOnlyList<ScoredCoordinate> LastHotSpots { get; private set; } = Array.Empty<ScoredCoordinate>();
        public IReadOnlyList<ZoneReachabilitySummary> LastZoneSummaries => _lastZoneSummaries;

        public ReachabilityMapService(
            IConstraintEngine constraintEngine,
            ILoadChartCapacityResolver capacityResolver)
        {
            _constraintEngine = constraintEngine;
            _capacityResolver = capacityResolver;
        }

        public IReadOnlyList<ScoredCoordinate> ComputeHotSpots(
            IEnumerable<PrecastElement> elements,
            IEnumerable<GreenAreaZone> allowedZones,
            IEnumerable<GreenAreaZone> forbiddenZones,
            CraneSpecification craneSpec,
            GASettings settings,
            int topN)
        {
            var elementsList = elements?.ToList() ?? new List<PrecastElement>();
            var allowedZonesList = allowedZones?.ToList() ?? new List<GreenAreaZone>();
            var forbiddenZonesList = forbiddenZones?.ToList() ?? new List<GreenAreaZone>();
            LastDiagnostics = null;
            LastHotSpots = Array.Empty<ScoredCoordinate>();
            _lastZoneSummaries = new List<ZoneReachabilitySummary>();
            lock (_summaryLock)
            {
                _selectedSeedCounts.Clear();
            }

            if (elementsList.Count == 0 || allowedZonesList.Count == 0 || craneSpec == null)
            {
                return Array.Empty<ScoredCoordinate>();
            }

            double step = settings.GridStep > 0 ? settings.GridStep : 0.5;
            var uniquePoints = new HashSet<(double X, double Y)>();

            foreach (var zone in allowedZonesList)
            {
                if (zone.Vertices == null || zone.Vertices.Count == 0)
                    continue;

                var bbox = SpatialMath.BoundingBox(zone.Vertices);

                // Align starting coordinates to grid step
                double startX = Math.Ceiling(bbox.minX / step) * step;
                double startY = Math.Ceiling(bbox.minY / step) * step;

                for (double x = startX; x <= bbox.maxX; x += step)
                {
                    for (double y = startY; y <= bbox.maxY; y += step)
                    {
                        double rx = Math.Round(x, 4);
                        double ry = Math.Round(y, 4);
                        uniquePoints.Add((rx, ry));
                    }
                }
            }

            // Filter unique points to only valid setup coordinates
            var validPoints = new ConcurrentBag<(DomainPoint3D Point, Guid ZoneId)>();
            var candidateCounts = new ConcurrentDictionary<Guid, int>();
            Parallel.ForEach(uniquePoints, point =>
            {
                // Find matching zones to get correct elevation (if multiple zones overlap, we can use the first matching one)
                double? elevation = null;
                GreenAreaZone? matchedZone = null;
                
                foreach (var zone in allowedZonesList)
                {
                    var testPoint = new DomainPoint3D(point.X, point.Y, GetZoneElevation(zone));
                    if (SpatialMath.PointInPolygon(testPoint, zone.Vertices))
                    {
                        elevation = testPoint.Z;
                        matchedZone = zone;
                        break;
                    }
                }

                if (elevation.HasValue && matchedZone != null)
                {
                    candidateCounts.AddOrUpdate(matchedZone.ZoneId, 1, (_, count) => count + 1);
                    var testPoint = new DomainPoint3D(point.X, point.Y, elevation.Value);
                    if (_constraintEngine.IsValidSetupPoint(testPoint, new[] { matchedZone }, forbiddenZonesList, settings))
                    {
                        validPoints.Add((testPoint, matchedZone.ZoneId));
                    }
                }
            });

            if (validPoints.Count == 0)
            {
                _lastZoneSummaries = allowedZonesList
                    .Select(zone =>
                    {
                        int candidateCount = candidateCounts.GetValueOrDefault(zone.ZoneId);
                        return new ZoneReachabilitySummary
                        {
                            ZoneId = zone.ZoneId,
                            CandidateGridPointCount = candidateCount,
                            ValidGridPointCount = 0,
                            RejectedGridPointCount = candidateCount
                        };
                    })
                    .ToList();
                int candidateCount = candidateCounts.Values.Sum();
                LastDiagnostics = new ReachabilityDiagnostics(
                    candidateCount,
                    0,
                    candidateCount,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
                return Array.Empty<ScoredCoordinate>();
            }

            // Pre-build the load chart index and cache element fields for performance
            var loadChartIndex = new LoadChartIndex(craneSpec);
            
            double maxRadius = craneSpec.MaxRadius;
            if (maxRadius <= 0 && craneSpec.LoadChart != null && craneSpec.LoadChart.Count > 0)
            {
                maxRadius = craneSpec.LoadChart.Max(tp => tp.Radius);
            }

            var cachedElements = elementsList.Select(e => new
            {
                CogX = e.ElementCOG.X,
                CogY = e.ElementCOG.Y,
                CogZ = e.ElementCOG.Z,
                HalfHeight = (e.ElementHeight ?? 0) / 2.0,
                RequiredWeight = (e.ElementWT ?? 0) + settings.AdditionalWt
            }).ToList();

            var scoredResults = new ConcurrentBag<ScoredCoordinate>();

            Parallel.ForEach(validPoints, validPoint =>
            {
                DomainPoint3D pt = validPoint.Point;
                int reachableCount = 0;
                double slewX = pt.X + settings.SlewOffsetX;
                double slewY = pt.Y + settings.SlewOffsetY;
                double hingeX = pt.X + settings.BoomHingeOffsetX;
                double hingeY = pt.Y + settings.BoomHingeOffsetY;
                double hingeZ = pt.Z + settings.BoomHingeOffsetZ;

                foreach (var el in cachedElements)
                {
                    double dx = slewX - el.CogX;
                    double dy = slewY - el.CogY;
                    double radius = Math.Sqrt(dx * dx + dy * dy);

                    if (radius > maxRadius)
                        continue;

                    double dxHinge = el.CogX - hingeX;
                    double dyHinge = el.CogY - hingeY;
                    double dHinge = Math.Sqrt(dxHinge * dxHinge + dyHinge * dyHinge);

                    double zHook = el.CogZ + el.HalfHeight + settings.WireLength;
                    double hHinge = Math.Max(0.0, zHook - hingeZ);
                    double boomLength = Math.Sqrt(dHinge * dHinge + hHinge * hHinge);

                    var resolveResult = _capacityResolver.Resolve(
                        craneSpec,
                        radius,
                        boomLength,
                        el.RequiredWeight,
                        settings,
                        buildDiagnostics: false,
                        index: loadChartIndex);

                    if (resolveResult.LiftStatus == LiftStatus.Liftable)
                    {
                        reachableCount++;
                    }
                }

                scoredResults.Add(new ScoredCoordinate(pt.X, pt.Y, reachableCount, validPoint.ZoneId));
            });

            int totalCandidatePoints = candidateCounts.Values.Sum();
            int totalPoints = validPoints.Count;
            int zeroCoveragePoints = scoredResults.Count(sc => sc.ReachableCount == 0);

            // Group scoredResults by ZoneId to ensure zone diversity in hot spots seeding
            var pointsByZone = scoredResults
                .GroupBy(sc => sc.ZoneId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(sc => sc.ReachableCount).ToList());

            var selectedList = new List<ScoredCoordinate>();
            int zonesCount = allowedZonesList.Count;
            int targetPerZone = Math.Max(50, topN / zonesCount); // Ensure a healthy minimum per zone

            // First pass: Take up to targetPerZone from each zone
            foreach (var kv in pointsByZone)
            {
                var zonePoints = kv.Value;
                selectedList.AddRange(zonePoints.Take(targetPerZone));
            }

            // Second pass: If we still need more to reach topN, fill with remaining best points overall
            if (selectedList.Count < topN)
            {
                var takenSet = new HashSet<ScoredCoordinate>(selectedList);
                var remaining = scoredResults
                    .Where(sc => !takenSet.Contains(sc))
                    .OrderByDescending(sc => sc.ReachableCount)
                    .ThenBy(sc => sc.X)
                    .ThenBy(sc => sc.Y);

                selectedList.AddRange(remaining.Take(topN - selectedList.Count));
            }

            var sortedList = selectedList
                .OrderByDescending(sc => sc.ReachableCount)
                .ThenBy(sc => sc.X)
                .ThenBy(sc => sc.Y)
                .ToList();

            double maxCoverage = 0;
            double avgCoverage = 0;
            if (sortedList.Count > 0 && elementsList.Count > 0)
            {
                maxCoverage = (double)sortedList[0].ReachableCount / elementsList.Count;
                avgCoverage = sortedList.Average(sc => (double)sc.ReachableCount / elementsList.Count);
            }

            double reachabilityMean = scoredResults.Count == 0
                ? 0
                : scoredResults.Average(result => (double)result.ReachableCount);
            double reachabilityStdDev = CalculateStdDev(
                scoredResults.Select(result => (double)result.ReachableCount),
                reachabilityMean);

            _lastZoneSummaries = allowedZonesList
                .Select(zone =>
                {
                    List<ScoredCoordinate> zonePoints = scoredResults
                        .Where(result => result.ZoneId == zone.ZoneId)
                        .ToList();
                    int candidateCount = candidateCounts.GetValueOrDefault(zone.ZoneId);
                    double mean = zonePoints.Count == 0
                        ? 0
                        : zonePoints.Average(result => (double)result.ReachableCount);

                    return new ZoneReachabilitySummary
                    {
                        ZoneId = zone.ZoneId,
                        CandidateGridPointCount = candidateCount,
                        ValidGridPointCount = zonePoints.Count,
                        RejectedGridPointCount = Math.Max(0, candidateCount - zonePoints.Count),
                        ZeroReachabilityPointCount = zonePoints.Count(result => result.ReachableCount == 0),
                        ReachabilityMax = zonePoints.Count == 0 ? 0 : zonePoints.Max(result => result.ReachableCount),
                        ReachabilityMean = mean,
                        ReachabilityStdDev = CalculateStdDev(
                            zonePoints.Select(result => (double)result.ReachableCount),
                            mean)
                    };
                })
                .ToList();

            LastDiagnostics = new ReachabilityDiagnostics(
                TotalCandidatePoints: totalCandidatePoints,
                ValidCandidatePoints: totalPoints,
                RejectedCandidatePoints: Math.Max(0, totalCandidatePoints - totalPoints),
                ZeroCoveragePoints: zeroCoveragePoints,
                HotSpotCount: sortedList.Count,
                MaxHotSpotCoverage: maxCoverage,
                AverageHotSpotCoverage: avgCoverage,
                ReachabilityMean: reachabilityMean,
                ReachabilityStdDev: reachabilityStdDev
            );

            LastHotSpots = sortedList.Take(topN).ToList();
            return LastHotSpots;
        }

        public void RecordSelectedSeed(ScoredCoordinate coordinate)
        {
            lock (_summaryLock)
            {
                ZoneReachabilitySummary? summary = _lastZoneSummaries
                    .FirstOrDefault(item => item.ZoneId == coordinate.ZoneId);
                if (summary != null)
                {
                    summary.SelectedSeedCount++;
                }

                var key = (coordinate.ZoneId, coordinate.X, coordinate.Y);
                _selectedSeedCounts[key] = _selectedSeedCounts.GetValueOrDefault(key) + 1;
            }
        }

        public bool WasSelectedAsSeed(ScoredCoordinate coordinate)
        {
            lock (_summaryLock)
            {
                return _selectedSeedCounts.ContainsKey((coordinate.ZoneId, coordinate.X, coordinate.Y));
            }
        }

        private static double GetZoneElevation(GreenAreaZone zone)
        {
            return zone.Vertices == null || zone.Vertices.Count == 0
                ? 0d
                : zone.Vertices.Average(vertex => vertex.Z);
        }

        private static double CalculateStdDev(IEnumerable<double> values, double mean)
        {
            double[] materialized = values.ToArray();
            if (materialized.Length == 0)
                return 0;

            double variance = materialized.Average(value => Math.Pow(value - mean, 2));
            return Math.Sqrt(variance);
        }
    }
}
