using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators
{
    /// <summary>
    /// Mutation operator that slightly modifies crane positions
    /// to maintain population diversity.
    /// </summary>
    public class MutationOperator : IMutationOperator
    {
        private readonly IConstraintEngine _constraintEngine;
        private Random _random => GARandomManager.Instance;

        public MutationOperator(IConstraintEngine constraintEngine)
        {
            _constraintEngine = constraintEngine;
        }

        /// <summary>
        /// Mutates crane setups using an 80/10/10 probability distribution.
        /// - 80% Position perturbation with boundary projection.
        /// - 10% Setup addition.
        /// - 10% Setup removal.
        /// </summary>
        public void Mutate(
            CraneSolution solution,
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings,
            int currentGeneration)
        {
            var positions = solution.Chromosome.CranePositions;
            double roll = _random.NextDouble();

            if (roll < 0.80)
            {
                // 1. Position Perturbation Mutation (80%)
                if (positions == null || positions.Count == 0)
                    return;

                int index = _random.Next(positions.Count);
                var point = positions[index];

                double sigma = ResolveMutationSigma(settings, currentGeneration);
                var otherPositions = positions
                    .Where((_, idx) => idx != index)
                    .ToList();

                int attempts = Math.Max(1, settings.PositionSamplingAttempts / 4);
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    double deltaX = NextGaussian() * sigma;
                    double deltaY = NextGaussian() * sigma;

                    var mutated = new DomainPoint3D(
                        SnapToGrid(point.X + deltaX, settings.GridStep),
                        SnapToGrid(point.Y + deltaY, settings.GridStep),
                        point.Z
                    );

                    // Boundary Projection on drift
                    if (!_constraintEngine.IsValidSetupPoint(mutated, zones, forbiddenZones, settings))
                    {
                        var closestZone = zones
                            .OrderBy(z => DistanceToZoneCentroid(mutated, z))
                            .First();

                        mutated = SnapPointToGrid(ProjectPointToZoneBoundary(mutated, closestZone), settings.GridStep);
                    }

                    if (!_constraintEngine.IsValidSetupPoint(mutated, zones, forbiddenZones, settings))
                        continue;

                    if (!_constraintEngine.IsMeaningfullySeparated(mutated, otherPositions, settings))
                        continue;

                    positions[index] = mutated;
                    return;
                }
            }
            else if (roll < 0.90)
            {
                // 2. Setup Addition Mutation (10%)
                if (positions == null || positions.Count >= settings.MaxCranePositions)
                    return;

                var newPoint = GenerateDiversePointInsideZones(zones, forbiddenZones, positions, settings);
                if (_constraintEngine.IsValidSetupPoint(newPoint, zones, forbiddenZones, settings)
                    && _constraintEngine.IsMeaningfullySeparated(newPoint, positions, settings))
                {
                    positions.Add(newPoint);
                }
            }
            else
            {
                // 3. Setup Removal Mutation (10%)
                if (positions == null || positions.Count <= 1)
                    return;

                int removeIndex = _random.Next(positions.Count);
                positions.RemoveAt(removeIndex);
            }
        }

        private double ResolveMutationSigma(GASettings settings, int currentGeneration)
        {
            if (settings.MutationSigma <= 0)
                return 5.0;

            double sigmaBase = settings.MutationSigma <= 1.0
                ? settings.MutationSigma * settings.MaxRadiusForNormalisation
                : settings.MutationSigma;

            sigmaBase = Math.Max(1.0, sigmaBase * Math.Max(1.0, settings.MutationExplorationMultiplier));

            if (settings.EnableAdaptiveSigma && settings.MaxGenerations > 0)
            {
                double progress = Math.Max(0.0, 1.0 - ((double)currentGeneration / settings.MaxGenerations));
                return sigmaBase * Math.Pow(progress, settings.SigmaCoolingExponent) + settings.MinimumMutationSigma;
            }

            return sigmaBase;
        }

        private DomainPoint3D ProjectPointToZoneBoundary(DomainPoint3D point, GreenAreaZone zone)
        {
            if (zone.Vertices == null || zone.Vertices.Count == 0)
                return point;

            double minDistance = double.MaxValue;
            DomainPoint3D closestProjection = point;

            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                var p1 = zone.Vertices[i];
                var p2 = zone.Vertices[(i + 1) % zone.Vertices.Count];

                var projection = ProjectPointOntoSegment2D(point, p1, p2);
                double dist = SpatialMath.Distance2D(point, projection);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestProjection = projection;
                }
            }

            return closestProjection;
        }

        private DomainPoint3D ProjectPointOntoSegment2D(DomainPoint3D point, DomainPoint3D a, DomainPoint3D b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double wx = point.X - a.X;
            double wy = point.Y - a.Y;

            double c1 = vx * wx + vy * wy;
            if (c1 <= 0)
                return new DomainPoint3D(a.X, a.Y, point.Z);

            double c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return new DomainPoint3D(b.X, b.Y, point.Z);

            double t = c1 / c2;
            return new DomainPoint3D(a.X + t * vx, a.Y + t * vy, point.Z);
        }

        private double DistanceToZoneCentroid(DomainPoint3D point, GreenAreaZone zone)
        {
            if (zone.Vertices == null || zone.Vertices.Count == 0)
                return double.MaxValue;

            double zx = zone.Vertices.Average(v => v.X);
            double zy = zone.Vertices.Average(v => v.Y);

            return SpatialMath.Distance2D(point, new DomainPoint3D(zx, zy, 0));
        }

        private DomainPoint3D GenerateDiversePointInsideZones(
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            List<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            DomainPoint3D? bestCandidate = null;
            double bestDistance = double.MinValue;
            int attempts = Math.Max(1, settings.PositionSamplingAttempts);

            for (int i = 0; i < attempts; i++)
            {
                var zone = zones[_random.Next(zones.Count)];
                if (!TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var candidate))
                    continue;

                double nearestDistance = existingPoints.Count == 0
                    ? double.MaxValue
                    : existingPoints.Min(point => SpatialMath.Distance2D(point, candidate));

                if (_constraintEngine.IsMeaningfullySeparated(candidate, existingPoints, settings))
                    return candidate;

                if (nearestDistance > bestDistance)
                {
                    bestDistance = nearestDistance;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
                return bestCandidate;

            foreach (var zone in zones)
            {
                if (TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var candidate))
                    return candidate;
            }

            throw new InvalidOperationException("No valid crane setup point has a complete physical ground footprint inside an allowed zone without overlapping forbidden zones.");
        }

        private DomainPoint3D GenerateRandomPointInsideZone(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            return TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var point)
                ? point
                : throw new InvalidOperationException($"No valid crane setup point fits inside allowed zone '{zone.Name}'.");
        }

        private bool TryGenerateRandomPointInsideZone(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings,
            out DomainPoint3D point)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);

            int attempts = Math.Max(1, settings.PositionSamplingAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double x = bbox.minX + _random.NextDouble() * (bbox.maxX - bbox.minX);
                double y = bbox.minY + _random.NextDouble() * (bbox.maxY - bbox.minY);

                var candidatePoint = SnapPointToGrid(new DomainPoint3D(x, y, GetZoneElevation(zone)), settings.GridStep);

                if (_constraintEngine.IsValidSetupPoint(candidatePoint, new[] { zone }, forbiddenZones, settings))
                {
                    point = candidatePoint;
                    return true;
                }
            }

            if (TryFindFirstValidGridPoint(zone, forbiddenZones, settings, out point))
                return true;

            point = new DomainPoint3D();
            return false;
        }

        private bool TryFindFirstValidGridPoint(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings,
            out DomainPoint3D point)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);
            int divisions = Math.Max(4, settings.PositionSamplingAttempts);
            double stepX = (bbox.maxX - bbox.minX) / divisions;
            double stepY = (bbox.maxY - bbox.minY) / divisions;

            if (stepX <= 0 || stepY <= 0)
            {
                point = new DomainPoint3D();
                return false;
            }

            for (int ix = 0; ix <= divisions; ix++)
            {
                for (int iy = 0; iy <= divisions; iy++)
                {
                    var candidate = SnapPointToGrid(
                        new DomainPoint3D(bbox.minX + (ix * stepX), bbox.minY + (iy * stepY), GetZoneElevation(zone)),
                        settings.GridStep);

                    if (_constraintEngine.IsValidSetupPoint(candidate, new[] { zone }, forbiddenZones, settings))
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            point = new DomainPoint3D();
            return false;
        }

        private DomainPoint3D SnapPointToGrid(DomainPoint3D point, double gridStep)
        {
            if (gridStep <= 0)
                return point;

            return new DomainPoint3D(
                Math.Round(point.X / gridStep) * gridStep,
                Math.Round(point.Y / gridStep) * gridStep,
                point.Z);
        }

        private static double GetZoneElevation(GreenAreaZone zone)
        {
            return zone.Vertices.Count == 0
                ? 0d
                : zone.Vertices.Average(vertex => vertex.Z);
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private double SnapToGrid(double value, double gridStep)
        {
            if (gridStep <= 0)
                return value;

            return Math.Round(value / gridStep) * gridStep;
        }
    }
}
