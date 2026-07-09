namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// Stable AI-owned names for host-provided workflow facts.
    ///
    /// The desktop host may populate these values, but the contract lives in the
    /// isolated AI project so UI/Application/Infrastructure adapters stay thin and
    /// do not invent AI semantics locally.
    /// </summary>
    public static class AiWorkflowFactKeys
    {
        public const string HostModelRevision = nameof(HostModelRevision);
        public const string ContextModelRevision = nameof(ContextModelRevision);
        public const string HostModelFingerprint = nameof(HostModelFingerprint);
        public const string ContextModelFingerprint = nameof(ContextModelFingerprint);

        public const string TotalElements = nameof(TotalElements);
        public const string TotalBimObjects = nameof(TotalBimObjects);
        public const string SceneBimObjectCount = nameof(SceneBimObjectCount);
        public const string ImportedSceneBimObjectCount = nameof(ImportedSceneBimObjectCount);
        public const string LiveSceneBimObjectCount = nameof(LiveSceneBimObjectCount);
        public const string ElementTableItems = nameof(ElementTableItems);
        public const string VisibleElementTableItems = nameof(VisibleElementTableItems);

        public const string ReadinessElementCount = nameof(ReadinessElementCount);
        public const string PlanningElementCount = nameof(PlanningElementCount);
        public const string GaReadinessElementCount = nameof(GaReadinessElementCount);
        public const string ReferenceGeometryExcludedCount = nameof(ReferenceGeometryExcludedCount);
        public const string ReadinessExcludedReferenceGeometryCount = nameof(ReadinessExcludedReferenceGeometryCount);
        public const string ElementCountScopeNote = nameof(ElementCountScopeNote);

        public const string GaCandidateElements = nameof(GaCandidateElements);
        public const string TrustedGaLiftUnits = nameof(TrustedGaLiftUnits);
        public const string TrustedGroupedLiftUnits = nameof(TrustedGroupedLiftUnits);
        public const string RawCandidateLikeElements = nameof(RawCandidateLikeElements);
        public const string GaBlockedElements = nameof(GaBlockedElements);
        public const string EstimatedWeightBlockedCount = nameof(EstimatedWeightBlockedCount);
        public const string MissingTrustedWeightCount = nameof(MissingTrustedWeightCount);

        public const string ResolvedCategoryCounts = nameof(ResolvedCategoryCounts);
        public const string TopResolvedCategories = nameof(TopResolvedCategories);
        public const string RawIfcTypeCounts = nameof(RawIfcTypeCounts);
        public const string TopRawIfcTypes = nameof(TopRawIfcTypes);
        public const string ElementTypeCounts = nameof(ElementTypeCounts);
        public const string WeightOver1000KgCount = nameof(WeightOver1000KgCount);
        public const string WeightOver3000KgCount = nameof(WeightOver3000KgCount);
        public const string WeightOver5000KgCount = nameof(WeightOver5000KgCount);
    }
}
