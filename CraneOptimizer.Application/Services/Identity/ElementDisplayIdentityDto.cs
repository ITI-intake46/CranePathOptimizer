using System.Collections.Generic;

namespace CraneOptimizer.Application.Services.Identity
{
    /// <summary>
    /// Canonical display identity for a precast element.
    /// Carries all display-facing identity fields resolved from the Phase 2/3
    /// canonical assembly identity schema (AssemblyMark, AssemblyMarkConfidence,
    /// AssemblyMarkIsExplicit, ResolvedDisplayMark, ValidationStatus).
    ///
    /// Used across GA outputs, reports, and CSV diagnostics as the authoritative
    /// display identity contract. DTOs that cross layer or subsystem boundaries
    /// should carry this data as flat fields rather than passing PrecastElement directly.
    /// </summary>
    public sealed class ElementDisplayIdentityDto
    {
        /// <summary>
        /// The IFC GlobalId / ElementID — always available, used as the last fallback.
        /// </summary>
        public string ElementId { get; set; } = string.Empty;

        /// <summary>
        /// Best resolved display label for UI, reports, and CSV output.
        /// Canonical priority:
        ///   AssemblyMark (explicit) > AssemblyMark > ResolvedDisplayMark
        ///   > ElementMark > ParentAssemblyName > ElementID
        /// </summary>
        public string DisplayLabel { get; set; } = string.Empty;

        /// <summary>
        /// The canonical assembly mark from the Phase 2 schema.
        /// Null if no canonical mark was resolved by the IFC reader / alias resolver.
        /// </summary>
        public string? AssemblyMark { get; set; }

        /// <summary>
        /// The raw mark or name used as a fallback when no canonical AssemblyMark exists.
        /// May be ElementMark, ResolvedDisplayMark, ParentAssemblyName, or ElementID.
        /// </summary>
        public string? FallbackLabel { get; set; }

        /// <summary>
        /// True when <see cref="DisplayLabel"/> was resolved from a fallback (not from AssemblyMark).
        /// </summary>
        public bool UsedFallback { get; set; }

        /// <summary>
        /// Source property path from which the canonical mark was resolved,
        /// e.g. "Pset_Revit_Identity Data.Assembly Mark".
        /// Null when the mark came from a fallback chain.
        /// </summary>
        public string? MarkSource { get; set; }

        /// <summary>
        /// Confidence of the resolved mark: "High", "Medium", "Low", or "None".
        /// Stored as a string for DTO portability (avoids enum coupling in downstream layers).
        /// </summary>
        public string MarkConfidence { get; set; } = "None";

        /// <summary>
        /// True when the mark was sourced from an explicit IFC property
        /// (not derived from a name-only fallback).
        /// </summary>
        public bool IsExplicitMark { get; set; }

        /// <summary>
        /// Validation status of the element as of the last readiness check.
        /// "Valid", "Warning", or "Error".
        /// </summary>
        public string ValidationStatus { get; set; } = "Valid";

        /// <summary>
        /// Validation warnings collected for this element during import readiness checks.
        /// </summary>
        public IReadOnlyList<string> ValidationWarnings { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Source system: "IFC", "Revit", "Tekla", or "JSON".
        /// </summary>
        public string SourceSystem { get; set; } = "IFC";

        /// <summary>
        /// Detected exporter profile: "Generic", "Tekla", or "Revit".
        /// </summary>
        public string ExporterProfile { get; set; } = "Generic";
    }
}
