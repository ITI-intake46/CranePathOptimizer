using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class CraneConstraintResult
    {
        public bool IsValid { get; set; }

        public bool IsInsideAllowedZone { get; set; }
        public bool IsOutsideForbiddenZone { get; set; }
        public bool IsWithinRadius { get; set; }
        public bool IsWithinCapacity { get; set; }

        public double PenaltyScore { get; set; }
        public double NormalizedPenalty { get; set; }
    }

    public interface ICraneConstraintService
    {
        CraneConstraintResult Validate(
            DomainPoint3D cranePosition,
            PlacementInput input);
    }

    public class CraneConstraintService : ICraneConstraintService
    {
        public CraneConstraintResult Validate(
            DomainPoint3D cranePosition,
            PlacementInput input)
        {
            var result = new CraneConstraintResult();

            // 1. Zone Validation
            result.IsInsideAllowedZone = input.AllowedZones.Count == 0 || 
                                        input.AllowedZones.Any(z => IsPointInsidePolygon(cranePosition, z.Vertices));
            
            result.IsOutsideForbiddenZone = input.ForbiddenZones.All(z => !IsPointInsidePolygon(cranePosition, z.Vertices));

            // 2. Physical Constraints (Radius & Capacity)
            bool radiusOk = true;
            bool capacityOk = true;
            double maxRadiusRatio = 0;
            double maxOverloadRatio = 0;

            if (input.CraneSpec != null && input.ElementCenters != null && input.ElementWeights != null && input.Settings != null)
            {
                foreach (var i in input.SpatialIndex.Query(cranePosition, input.CraneSpec.MaxRadius))
                {
                    var center = input.ElementCenters[i];
                    var weight = input.ElementWeights[i];

                    double dx = cranePosition.X - center.X;
                    double dy = cranePosition.Y - center.Y;
                    double horizontalDistance = Math.Sqrt(dx * dx + dy * dy);
                    
                    if (horizontalDistance > input.CraneSpec.MaxRadius) continue;

                    double radius = horizontalDistance; 
                    double height = center.Z + input.Settings.RiggingLength; 
                    double boomLength = Math.Sqrt(radius * radius + height * height);

                    var status = LiftCalculation.DetermineLiftStatus(radius, boomLength, weight, input.CraneSpec, input.Settings);

                    if (status == LiftStatus.OutOfRadius) radiusOk = false;
                    if (status == LiftStatus.Overweight || status == LiftStatus.HeightFail || status == LiftStatus.SpecialCase) 
                        capacityOk = false;

                    // Compute ratios for penalty
                    double rRatio = radius / input.CraneSpec.MaxRadius;
                    if (rRatio > maxRadiusRatio) maxRadiusRatio = rRatio;

                    double capacity = LiftCalculation.CraneCapacity(input.CraneSpec, radius, boomLength, input.Settings);
                    if (capacity > 0)
                    {
                        double oRatio = weight / capacity;
                        if (oRatio > maxOverloadRatio) maxOverloadRatio = oRatio;
                    }
                    else if (weight > 0)
                    {
                        maxOverloadRatio = Math.Max(maxOverloadRatio, 2.0); 
                    }
                }
            }

            result.IsWithinRadius = radiusOk;
            result.IsWithinCapacity = capacityOk;

            // 3. Final Validity
            result.IsValid = result.IsInsideAllowedZone && 
                             result.IsOutsideForbiddenZone && 
                             result.IsWithinRadius && 
                             result.IsWithinCapacity;

            // 4. Soft Penalty System (Optimized & Normalized)
            double forbiddenPenalty = result.IsOutsideForbiddenZone ? 0 : 1;
            double allowedPenalty = result.IsInsideAllowedZone ? 0 : 1;

            double penalty = 0;
            penalty += forbiddenPenalty * 10.0;
            penalty += allowedPenalty * 5.0;
            penalty += Math.Max(0, maxRadiusRatio - 1.0) * 3.0;
            penalty += Math.Max(0, maxOverloadRatio - 1.0) * 7.0;
            
            result.PenaltyScore = penalty * 1000; // Keep backward compatibility if needed
            result.NormalizedPenalty = penalty;

            return result;
        }

        private bool IsPointInsidePolygon(DomainPoint3D point, List<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                 (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);
                if (intersect) inside = !inside;
            }
            return inside;
        }
    }
}
