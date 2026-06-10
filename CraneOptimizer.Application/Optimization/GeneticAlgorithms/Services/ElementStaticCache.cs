using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Pre-computed static element data for the GA hot path.
    /// Built once before the GA generation loop from the element list + GASettings
    /// to avoid repeated null-coalescing, arithmetic, and property access per evaluation.
    /// </summary>
    public class ElementStaticCache
    {
        /// <summary>
        /// Per-element cached data, keyed by the case-sensitive IFC ElementID.
        /// </summary>
        public Dictionary<string, CachedElementData> ByElementId { get; }

        /// <summary>
        /// Builds the static cache from the element list and GA settings.
        /// </summary>
        public ElementStaticCache(List<PrecastElement> elements, GASettings settings)
        {
            ByElementId = new Dictionary<string, CachedElementData>(
                elements.Count, System.StringComparer.Ordinal);

            foreach (var element in elements)
            {
                if (string.IsNullOrEmpty(element.ElementID))
                    continue;

                ByElementId[element.ElementID] = new CachedElementData
                {
                    RequiredWeight = (element.ElementWT ?? 0) + settings.AdditionalWt,
                    HalfHeight = (element.ElementHeight ?? 0) / 2.0,
                    CogX = element.ElementCOG.X,
                    CogY = element.ElementCOG.Y,
                    CogZ = element.ElementCOG.Z
                };
            }
        }
    }

    /// <summary>
    /// Cached static data for a single precast element.
    /// These values never change during a GA run.
    /// </summary>
    public struct CachedElementData
    {
        /// <summary>
        /// Pre-computed: (ElementWT ?? 0) + AdditionalWt
        /// </summary>
        public double RequiredWeight;

        /// <summary>
        /// Pre-computed: (ElementHeight ?? 0) / 2.0
        /// </summary>
        public double HalfHeight;

        /// <summary>
        /// Element center of gravity X coordinate.
        /// </summary>
        public double CogX;

        /// <summary>
        /// Element center of gravity Y coordinate.
        /// </summary>
        public double CogY;

        /// <summary>
        /// Element center of gravity Z coordinate.
        /// </summary>
        public double CogZ;
    }
}
