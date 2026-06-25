using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Diagnostics;
using System.Linq;

namespace CraneOptimizer.Application.Services.Identity
{
    /// <summary>
    /// Resolves the canonical display identity for a precast element using the
    /// Phase 2/3 canonical assembly identity schema.
    ///
    /// This is the single authoritative source for display mark resolution across:
    ///   - GA result outputs (ElementLiftResult)
    ///   - Report providers (ElementReportDataProvider, LiftingResultReportDataProvider)
    ///   - CSV diagnostics (GACsvDiagnosticsService / GAElementDiagnosticDto)
    ///
    /// Priority chain:
    ///   1. AssemblyMark + IsExplicit=true  → DisplayLabel, UsedFallback=false, IsExplicitMark=true
    ///   2. AssemblyMark (any confidence)   → DisplayLabel, UsedFallback=false
    ///   3. ResolvedDisplayMark             → DisplayLabel, UsedFallback=true (canonical fallback)
    ///   4. ElementMark                     → DisplayLabel, UsedFallback=true
    ///   5. ParentAssemblyName              → DisplayLabel, UsedFallback=true
    ///   6. ElementID                       → DisplayLabel, UsedFallback=true (last resort)
    ///
    /// GA physics data (weight, COG, radius, capacity) is never touched by this class.
    /// </summary>
    public static class CanonicalMarkResolver
    {
        /// <summary>
        /// Resolves the full canonical display identity DTO for the given element.
        /// </summary>
        public static ElementDisplayIdentityDto Resolve(PrecastElement element)
        {
            var dto = new ElementDisplayIdentityDto
            {
                ElementId         = element.ElementID ?? string.Empty,
                AssemblyMark      = element.AssemblyMark,
                MarkSource        = element.AssemblyMarkSource,
                MarkConfidence    = element.AssemblyMarkConfidence.ToString(),
                IsExplicitMark    = element.AssemblyMarkIsExplicit,
                ValidationStatus  = element.ValidationStatus.ToString(),
                ValidationWarnings = (IReadOnlyList<string>?)element.ValidationWarnings ?? System.Array.Empty<string>(),
                SourceSystem      = element.SourceSystem.ToString(),
                ExporterProfile   = element.ExporterProfile.ToString(),
            };

            // Canonical priority resolution
            if (!string.IsNullOrWhiteSpace(element.AssemblyMark))
            {
                dto.DisplayLabel = element.AssemblyMark;
                dto.UsedFallback = false;
                return dto;
            }

            if (!string.IsNullOrWhiteSpace(element.ResolvedDisplayMark))
            {
                dto.DisplayLabel  = element.ResolvedDisplayMark;
                dto.FallbackLabel = element.ResolvedDisplayMark;
                dto.UsedFallback  = true;
                return dto;
            }

            if (!string.IsNullOrWhiteSpace(element.ElementMark))
            {
                dto.DisplayLabel  = element.ElementMark;
                dto.FallbackLabel = element.ElementMark;
                dto.UsedFallback  = true;
                return dto;
            }

            if (!string.IsNullOrWhiteSpace(element.ParentAssemblyName))
            {
                dto.DisplayLabel  = element.ParentAssemblyName;
                dto.FallbackLabel = element.ParentAssemblyName;
                dto.UsedFallback  = true;
                return dto;
            }

            // Final fallback: always non-null
            dto.DisplayLabel  = element.ElementID ?? string.Empty;
            dto.FallbackLabel = element.ElementID;
            dto.UsedFallback  = true;
            return dto;
        }

        /// <summary>
        /// Convenience method that returns only the best display label string.
        /// Use <see cref="Resolve"/> when metadata is also needed.
        /// </summary>
        public static string ResolveDisplayMark(PrecastElement element)
            => Resolve(element).DisplayLabel;
    }
}
