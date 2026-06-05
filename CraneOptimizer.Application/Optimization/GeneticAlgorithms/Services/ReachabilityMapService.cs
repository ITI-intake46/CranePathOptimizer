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

        public ReachabilityDiagnostics? LastDiagnostics { get; private set; }

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
            var validPoints = new ConcurrentBag<DomainPoint3D>();
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
                    var testPoint = new DomainPoint3D(point.X, point.Y, elevation.Value);
                    if (_constraintEngine.IsValidSetupPoint(testPoint, new[] { matchedZone }, forbiddenZonesList, settings))
                    {
                        validPoints.Add(testPoint);
                    }
                }
            });

            if (validPoints.Count == 0)
            {
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

            Parallel.ForEach(validPoints, pt =>
            {
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

                scoredResults.Add(new ScoredCoordinate(pt.X, pt.Y, reachableCount));
            });

            int totalPoints = validPoints.Count;
            int zeroCoveragePoints = scoredResults.Count(sc => sc.ReachableCount == 0);
            var sortedList = scoredResults
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

            LastDiagnostics = new ReachabilityDiagnostics(
                TotalCandidatePoints: totalPoints,
                ZeroCoveragePoints: zeroCoveragePoints,
                HotSpotCount: sortedList.Count,
                MaxHotSpotCoverage: maxCoverage,
                AverageHotSpotCoverage: avgCoverage
            );

            return sortedList.Take(topN).ToList();
        }

        private static double GetZoneElevation(GreenAreaZone zone)
        {
            return zone.Vertices == null || zone.Vertices.Count == 0
                ? 0d
                : zone.Vertices.Average(vertex => vertex.Z);
        }
    }
}
