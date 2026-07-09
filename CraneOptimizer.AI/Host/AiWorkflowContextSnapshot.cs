namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// UI-neutral snapshot of the current crane-planning workflow. The host UI is
    /// responsible for translating its live view models into this DTO.
    /// </summary>
    public sealed class AiWorkflowContextSnapshot
    {
        public string ProjectKey { get; init; } = "global";
        public string ProjectDisplayName { get; init; } = "Global";
        public bool HasProject { get; init; }
        public string CurrentProjectName { get; init; } = string.Empty;

        /// <summary>
        /// Monotonic host-side model/session revision. It increments when IFC/model
        /// elements are loaded, merged, moved, deleted, or when the active project
        /// identity changes. AI answers can use it to detect stale project facts.
        /// </summary>
        public long ModelRevision { get; init; }

        /// <summary>
        /// Compact fingerprint for the current model context. It is intentionally
        /// session-local rather than a persistent model identifier.
        /// </summary>
        public string ModelFingerprint { get; init; } = "model:none";

        public string ModelDisplayName { get; init; } = string.Empty;
        public DateTimeOffset? ModelLoadedAt { get; init; }

        /// <summary>Total BIM/model objects currently in the scene.</summary>
        public int ElementCount { get; init; }

        /// <summary>Rows available to the element-management table after lift/display filtering.</summary>
        public int ElementTableTotalCount { get; init; }

        /// <summary>Rows currently visible in the element-management table after active UI filters/search.</summary>
        public int ElementTableVisibleCount { get; init; }

        public int ZoneCount { get; init; }
        public int AllowedZoneCount { get; init; }
        public int ForbiddenZoneCount { get; init; }
        public double AllowedZoneArea { get; init; }
        public double ForbiddenZoneArea { get; init; }
        public int CraneCount { get; init; }
        public int LoadChartEvidenceCount { get; init; }
        public bool HasLoadChartEvidence { get; init; }
        public double BuildingArea { get; init; }
        public string BuildingAreaUnit { get; init; } = "m²";
        public double BuildingHeight { get; init; }
        public string BuildingLengthUnit { get; init; } = "m";

        public AiOptimizationResultSnapshot OptimizationResult { get; init; } = new();

        /// <summary>
        /// Rich, screen-level deterministic context supplied by the host UI.
        /// </summary>
        public IReadOnlyList<AiScreenContextSnapshot> ScreenContexts { get; init; } = Array.Empty<AiScreenContextSnapshot>();
    }
}
