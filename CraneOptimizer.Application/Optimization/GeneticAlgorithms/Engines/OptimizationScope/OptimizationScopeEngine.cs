using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope
{
    public class OptimizationScopeEngine : IOptimizationScopeEngine
    {
        public List<PrecastElement> FilterElements(
            List<PrecastElement> allElements,
            OptimizationScopeFilter filter)
        {
            if (allElements == null)
                return new List<PrecastElement>();

            if (filter == null)
                return allElements.ToList();

            var filtered = allElements.AsEnumerable();

            // 1. Filter by Selected Element IDs
            if (filter.SelectedElementIds != null && filter.SelectedElementIds.Count > 0)
            {
                filtered = filtered.Where(e => !string.IsNullOrEmpty(e.ElementID) && filter.SelectedElementIds.Contains(e.ElementID));
            }

            // 2. Filter by Excluded Element IDs
            if (filter.ExcludedElementIds != null && filter.ExcludedElementIds.Count > 0)
            {
                filtered = filtered.Where(e => string.IsNullOrEmpty(e.ElementID) || !filter.ExcludedElementIds.Contains(e.ElementID));
            }

            // 3. Filter by Element Type / Categories
            if (filter.TargetCategories != null && filter.TargetCategories.Count > 0)
            {
                var normalizedCategories = filter.TargetCategories
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (normalizedCategories.Count > 0)
                {
                    filtered = filtered.Where(e =>
                    {
                        string type = string.IsNullOrWhiteSpace(e.ElementType) ? "Unknown" : e.ElementType.Trim();
                        return normalizedCategories.Contains(type);
                    });
                }
            }

            // 4. Filter by Area Bounding Box (COG coordinates)
            filtered = filtered.Where(e =>
            {
                if (e.ElementCOG == null)
                    return false;

                if (filter.MinX.HasValue && e.ElementCOG.X < filter.MinX.Value)
                    return false;
                if (filter.MaxX.HasValue && e.ElementCOG.X > filter.MaxX.Value)
                    return false;
                if (filter.MinY.HasValue && e.ElementCOG.Y < filter.MinY.Value)
                    return false;
                if (filter.MaxY.HasValue && e.ElementCOG.Y > filter.MaxY.Value)
                    return false;

                return true;
            });

            return filtered.ToList();
        }
    }
}
