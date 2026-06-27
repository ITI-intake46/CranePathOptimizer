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
            if (!IsPlaceholder(element.AssemblyMark))
            {
                dto.DisplayLabel = element.AssemblyMark!.Trim();
                dto.UsedFallback = false;
                return dto;
            }

            if (!IsPlaceholder(element.ResolvedDisplayMark))
            {
                dto.DisplayLabel  = element.ResolvedDisplayMark!.Trim();
                dto.FallbackLabel = element.ResolvedDisplayMark;
                dto.UsedFallback  = true;
                return dto;
            }

            if (!IsPlaceholder(element.ElementMark))
            {
                dto.DisplayLabel  = element.ElementMark!.Trim();
                dto.FallbackLabel = element.ElementMark;
                dto.UsedFallback  = true;
                return dto;
            }

            if (!IsPlaceholder(element.ElementName))
            {
                dto.DisplayLabel  = element.ElementName!.Trim();
                dto.FallbackLabel = element.ElementName;
                dto.UsedFallback  = true;
                return dto;
            }

            if (!IsPlaceholder(element.ParentAssemblyName))
            {
                dto.DisplayLabel  = element.ParentAssemblyName!.Trim();
                dto.FallbackLabel = element.ParentAssemblyName;
                dto.UsedFallback  = true;
                return dto;
            }

            // Final fallback: always non-null
            dto.DisplayLabel  = !string.IsNullOrWhiteSpace(element.ElementID) ? element.ElementID.Trim() : string.Empty;
            dto.FallbackLabel = element.ElementID;
            dto.UsedFallback  = true;
            return dto;
        }

        private static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            var val = value.Trim().ToUpperInvariant();
            return val == "-" || val == "—" || val == "N/A" || val == "UNKNOWN" || val == "NULL";
        }

        /// <summary>
        /// Convenience method that returns only the best display label string.
        /// Use <see cref="Resolve"/> when metadata is also needed.
        /// </summary>
        public static string ResolveDisplayMark(PrecastElement element)
            => Resolve(element).DisplayLabel;
    }
}
