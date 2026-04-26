using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Assignment;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Application.Services;

public sealed class ElementAssignmentService
{
    private readonly ICraneConstraintService _constraintService;

    public ElementAssignmentService(ICraneConstraintService constraintService)
    {
        _constraintService = constraintService;
    }

    /// <summary>
    /// Assigns every element to the nearest crane that can reach and lift it.
    /// Stateless — safe for concurrent calls.
    /// </summary>
    public CraneAssignment Assign(
        IReadOnlyList<DomainPoint3D> placements,   // decoded from chromosome
        PlacementInput input)
    {
        var craneToElements = placements
            .Select((_, i) => i)
            .ToDictionary(i => i, _ => new List<int>());
        var unassigned = new List<int>();

        var elementCount = input.ElementCenters.Count;
        var bestCranes = new int[elementCount];
        var bestDistancesSq = new double[elementCount];

        for (int i = 0; i < elementCount; i++)
        {
            bestCranes[i] = -1;
            bestDistancesSq[i] = double.MaxValue;
        }

        for (int cIdx = 0; cIdx < placements.Count; cIdx++)
        {
            var cranePos = placements[cIdx];
            double maxR2 = input.CraneSpec.MaxRadius * input.CraneSpec.MaxRadius;

            // FIX 3 — skip invalid crane early
            var validation = _constraintService.Validate(cranePos, input);
            if (!validation.IsValid)
                continue;

            // FIX 2 — spatial loop
            var nearbyIndices = input.SpatialIndex.Query(cranePos, input.CraneSpec.MaxRadius);
            foreach (var eIdx in nearbyIndices)
            {
                var center = input.ElementCenters[eIdx];
                var weight = input.ElementWeights[eIdx];

                // FIX 4 — squared distance (remove Sqrt from hot path)
                double dx = cranePos.X - center.X;
                double dy = cranePos.Y - center.Y;
                double dist2 = dx * dx + dy * dy;

                if (dist2 > maxR2)
                    continue;

                if (dist2 < bestDistancesSq[eIdx])
                {
                    // Compute capacity only if it's potentially the best crane
                    double dist = Math.Sqrt(dist2);
                    double height = center.Z + input.Settings.RiggingLength;
                    double boomLength = Math.Sqrt(dist2 + height * height);
                    double capacity = LiftCalculation.CraneCapacity(input.CraneSpec, dist, boomLength, input.Settings);

                    if (weight <= capacity)
                    {
                        bestDistancesSq[eIdx] = dist2;
                        bestCranes[eIdx] = cIdx;
                    }
                }
            }
        }

        // Finalize assignment mapping
        for (int eIdx = 0; eIdx < elementCount; eIdx++)
        {
            if (bestCranes[eIdx] >= 0)
                craneToElements[bestCranes[eIdx]].Add(eIdx);
            else
                unassigned.Add(eIdx);
        }

        return new CraneAssignment
        {
            CraneToElements = craneToElements,
            UnassignedElements = unassigned
        };
    }
}
