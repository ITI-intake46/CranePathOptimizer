using CraneOptimizer.Application.Reports.Definitions;

namespace CraneOptimizer.Application.Reports.Services;

public static class ReportTemplateCatalog
{
    public const string BasicElementList = "Basic Element List";
    public const string WeightReview = "Weight Review";
    public const string MissingEstimatedWeight = "Missing / Estimated Weight";
    public const string GaReadiness = "GA Readiness";
    public const string ReportDataSourceAudit = "Report Data Source Audit";
    public const string BasicLiftingResult = "Basic Lifting Result";
    public const string CapacityUtilizationReview = "Capacity Utilization Review";
    public const string FailedLiftsOnly = "Failed Lifts Only";
    public const string GaAssignmentReview = "GA Assignment Review";

    public static IReadOnlyList<ReportTemplateDefinition> GetBuiltInTemplates(ReportFamily family) =>
        family == ReportFamily.ElementList ? ElementTemplates : LiftingTemplates;

    public static IReadOnlyList<ReportColumnDefinition> GetAvailableColumns(ReportFamily family) =>
        family == ReportFamily.ElementList
            ? ElementAvailableColumns
            : GaAvailableColumns;

    public static ReportTemplateDefinition GetBuiltInTemplate(ReportFamily family, string name) =>
        GetBuiltInTemplates(family).FirstOrDefault(template => template.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? GetBuiltInTemplates(family)[0];

    private static readonly IReadOnlyList<ReportTemplateDefinition> ElementTemplates =
    [
        new()
        {
            Name = BasicElementList,
            Family = ReportFamily.ElementList,
            DefaultItemsIncluded = "All Elements",
            Columns =
            [
                Column("No", "No.", ReportColumnValueKind.Number, 0),
                Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1),
                Column("ElementName", "Element Name", ReportColumnValueKind.Text, 2),
                Column("ElementType", "Element Type", ReportColumnValueKind.Text, 3),
                Column("Material", "Material", ReportColumnValueKind.Text, 4),
                Column("Length", "Length", ReportColumnValueKind.Meters, 5, "Geometry"),
                Column("Width", "Width", ReportColumnValueKind.Meters, 6, "Geometry"),
                Column("Height", "Height", ReportColumnValueKind.Meters, 7, "Geometry"),
                Column("VolumeM3", "Volume (m3)", ReportColumnValueKind.Number, 8, "Geometry"),
                Column("TopLevelElevation", "Top Level / Elevation", ReportColumnValueKind.Text, 9, "Location / Elevation"),
                Column("BottomLevelElevation", "Bottom Level / Elevation", ReportColumnValueKind.Text, 10, "Location / Elevation"),
                Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 11, "Weight and Capacity"),
                Column("WeightVerified", "Weight Verified", ReportColumnValueKind.YesNo, 12, "Weight and Capacity"),
                Column("WeightStatus", "Weight Status", ReportColumnValueKind.Status, 13, "Weight and Capacity")
            ]
        },
        new()
        {
            Name = WeightReview,
            Family = ReportFamily.ElementList,
            DefaultItemsIncluded = "All Elements",
            Columns =
            [
                Column("No", "No.", ReportColumnValueKind.Number, 0),
                Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1),
                Column("Material", "Material", ReportColumnValueKind.Text, 2),
                Column("VolumeM3", "Volume (m3)", ReportColumnValueKind.Number, 3, "Geometry"),
                Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 4, "Weight and Capacity"),
                Column("WeightVerified", "Weight Verified", ReportColumnValueKind.YesNo, 5, "Weight and Capacity"),
                Column("WeightStatus", "Weight Status", ReportColumnValueKind.Status, 6, "Weight and Capacity"),
                Column("WeightWarning", "Weight Warning", ReportColumnValueKind.Warning, 7, "Review / Lift Status")
            ]
        },
        new()
        {
            Name = MissingEstimatedWeight,
            Family = ReportFamily.ElementList,
            DefaultItemsIncluded = "Needs Review Elements",
            Columns =
            [
                Column("No", "No.", ReportColumnValueKind.Number, 0),
                Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1),
                Column("Material", "Material", ReportColumnValueKind.Text, 2),
                Column("VolumeM3", "Volume (m3)", ReportColumnValueKind.Number, 3, "Geometry"),
                Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 4, "Weight and Capacity"),
                Column("WeightSource", "Weight Source", ReportColumnValueKind.Text, 5, "Weight and Capacity"),
                Column("WeightStatus", "Weight Status", ReportColumnValueKind.Status, 6, "Weight and Capacity"),
                Column("WeightWarning", "Weight Warning", ReportColumnValueKind.Warning, 7, "Review / Lift Status")
            ]
        },
        new()
        {
            Name = GaReadiness,
            Family = ReportFamily.ElementList,
            DefaultItemsIncluded = "All Elements",
            Columns =
            [
                Column("No", "No.", ReportColumnValueKind.Number, 0, "Identity"),
                Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1, "Identity"),
                Column("MarkConfidence", "Mark Confidence", ReportColumnValueKind.Text, 2, "Identity"),
                Column("ValidationStatus", "Validation Status", ReportColumnValueKind.Status, 3, "Review / Lift Status"),
                Column("ValidationWarnings", "Validation Warnings", ReportColumnValueKind.Warning, 4, "Review / Lift Status"),
                Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 5, "Weight and Capacity"),
                Column("WeightTrustLevel", "Weight Trust Level", ReportColumnValueKind.Text, 6, "Weight and Capacity"),
                Column("WeightVerified", "Weight Verified", ReportColumnValueKind.YesNo, 7, "Weight and Capacity"),
                Column("WeightStatus", "Weight Status", ReportColumnValueKind.Status, 8, "Weight and Capacity"),
                Column("IncludedInCraneOptimization", "Included in GA Input", ReportColumnValueKind.YesNo, 9, "Review / Lift Status"),
                Column("ReadyForGa", "Ready for GA", ReportColumnValueKind.YesNo, 10, "Review / Lift Status"),
                Column("ReadinessStatus", "Readiness Status", ReportColumnValueKind.Status, 11, "Review / Lift Status"),
                Column("MissingReadinessReason", "Missing Readiness Reason", ReportColumnValueKind.Warning, 12, "Review / Lift Status"),
                Column("BlockingReasons", "Blocking Reasons", ReportColumnValueKind.Warning, 13, "Review / Lift Status")
            ]
        },
        new()
        {
            Name = ReportDataSourceAudit,
            Family = ReportFamily.ElementList,
            DefaultItemsIncluded = "All Elements",
            Columns =
            [
                Column("No", "No.", ReportColumnValueKind.Number, 0, "Audit"),
                Column("Area", "Area", ReportColumnValueKind.Text, 1, "Audit"),
                Column("ReportColumn", "Report Column", ReportColumnValueKind.Text, 2, "Audit"),
                Column("DisplayedHeader", "Displayed Header", ReportColumnValueKind.Text, 3, "Audit"),
                Column("CurrentSource", "Current Source", ReportColumnValueKind.Text, 4, "Audit"),
                Column("SourceObjectOrFile", "Source Object / File", ReportColumnValueKind.Text, 5, "Audit"),
                Column("SourceField", "Source Field", ReportColumnValueKind.Text, 6, "Audit"),
                Column("CurrentAvailability", "Current Availability", ReportColumnValueKind.Text, 7, "Audit"),
                Column("RowsFound", "Rows Found", ReportColumnValueKind.Number, 8, "Audit"),
                Column("WhyItMayShowDash", "Why It May Show \u2014", ReportColumnValueKind.Warning, 9, "Audit"),
                Column("RequiredAction", "Required Action", ReportColumnValueKind.Warning, 10, "Audit")
            ]
        }
    ];

    private static readonly IReadOnlyList<ReportColumnDefinition> ElementAvailableColumns =
    [
        Column("No", "No.", ReportColumnValueKind.Number, 0, "Identity", isSelected: false),
        Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1, "Identity", isSelected: false),
        Column("ElementName", "Element Name", ReportColumnValueKind.Text, 2, "Identity", isSelected: false),
        Column("SourceRevitId", "Element Source ID", ReportColumnValueKind.Text, 3, "Identity", isSelected: false),
        Column("ElementType", "Element Type", ReportColumnValueKind.Text, 4, "Identity", isSelected: false),
        Column("StoreyLevel", "Storey / Level", ReportColumnValueKind.Text, 5, "Location / Elevation", isSelected: false),
        Column("Material", "Material", ReportColumnValueKind.Text, 6, "Identity", isSelected: false),
        Column("Length", "Length", ReportColumnValueKind.Meters, 20, "Geometry", isSelected: false),
        Column("Width", "Width", ReportColumnValueKind.Meters, 21, "Geometry", isSelected: false),
        Column("Height", "Height", ReportColumnValueKind.Meters, 22, "Geometry", isSelected: false),
        Column("VolumeM3", "Volume (m3)", ReportColumnValueKind.Number, 23, "Geometry", isSelected: false),
        Column("VolumeSource", "Volume Source", ReportColumnValueKind.Text, 24, "Geometry", isSelected: false),
        Column("TopLevelElevation", "Top Level / Elevation", ReportColumnValueKind.Text, 25, "Location / Elevation", isSelected: false),
        Column("BottomLevelElevation", "Bottom Level / Elevation", ReportColumnValueKind.Text, 26, "Location / Elevation", isSelected: false),
        Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 40, "Weight and Capacity", isSelected: false),
        Column("WeightSource", "Weight Source", ReportColumnValueKind.Text, 41, "Weight and Capacity", isSelected: false),
        Column("WeightVerified", "Weight Verified", ReportColumnValueKind.YesNo, 42, "Weight and Capacity", isSelected: false),
        Column("WeightStatus", "Weight Status", ReportColumnValueKind.Status, 43, "Weight and Capacity", isSelected: false),
        Column("WeightWarning", "Weight Warning", ReportColumnValueKind.Warning, 44, "Weight and Capacity", isSelected: false),
        Column("IncludedInCraneOptimization", "Included in GA Input", ReportColumnValueKind.YesNo, 60, "Review / Lift Status", isSelected: false),
        Column("ReadyForGa", "Ready for GA", ReportColumnValueKind.YesNo, 61, "Review / Lift Status", isSelected: false),
        Column("MissingReadinessReason", "Missing Readiness Reason", ReportColumnValueKind.Warning, 62, "Review / Lift Status", isSelected: false),
        Column("MarkConfidence", "Mark Confidence", ReportColumnValueKind.Text, 63, "Identity", isSelected: false),
        Column("ValidationStatus", "Validation Status", ReportColumnValueKind.Status, 64, "Review / Lift Status", isSelected: false),
        Column("ValidationWarnings", "Validation Warnings", ReportColumnValueKind.Warning, 65, "Review / Lift Status", isSelected: false),
        Column("WeightTrustLevel", "Weight Trust Level", ReportColumnValueKind.Text, 66, "Weight and Capacity", isSelected: false),
        Column("ReadinessStatus", "Readiness Status", ReportColumnValueKind.Status, 67, "Review / Lift Status", isSelected: false),
        Column("BlockingReasons", "Blocking Reasons", ReportColumnValueKind.Warning, 68, "Review / Lift Status", isSelected: false),
        GaColumn("LiftStatus", "Lift Status", ReportColumnValueKind.Status, "Lift Result", 80, isSelected: false),
        GaColumn("FailureReason", "Failure Reason", ReportColumnValueKind.Warning, "Lift Result", 81, isSelected: false),
        GaColumn("SuggestedAction", "Suggested Action", ReportColumnValueKind.Warning, "Lift Result", 82, isSelected: false),
        GaColumn("ReachStatus", "Reach Status", ReportColumnValueKind.Status, "Lift Result", 83, isSelected: false),
        GaColumn("ChartStatus", "Chart Status", ReportColumnValueKind.Status, "Lift Result", 84, isSelected: false),
        GaColumn("BestRadius", "Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 100, isSelected: false),
        GaColumn("RequiredBoomLength", "Required Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 101, isSelected: false),
        GaColumn("HookRiggingWeight", "Hook / Rigging Weight (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 102, isSelected: false),
        GaColumn("RequiredCapacity", "Required Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 103, isSelected: false),
        GaColumn("RawChartCapacity", "Raw Chart Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 104, isSelected: false),
        GaColumn("AvailableCapacity", "Available Capacity After Derating/Safety (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 105, isSelected: false),
        GaColumn("CapacityUtilizationPercent", "Capacity Utilization %", ReportColumnValueKind.Percentage, "Radius / Boom / Capacity", 106, isSelected: false),
        GaColumn("SafetyMargin", "Safety Margin", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 107, isSelected: false)
    ];

    private static readonly IReadOnlyList<ReportTemplateDefinition> LiftingTemplates =
    [
        new()
        {
            Name = BasicLiftingResult,
            Family = ReportFamily.LiftingResult,
            DefaultItemsIncluded = "Optimization Result",
            Columns =
            [
                GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
                GaColumn("ElementMark", "Element Mark", ReportColumnValueKind.Text, "Element / Load Point", 1),
                GaColumn("Weight", "Element Weight (t)", ReportColumnValueKind.Tons, "Element / Load Point", 2),
                GaColumn("HookRiggingWeight", "Hook / Rigging Weight (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 3),
                GaColumn("RequiredCapacity", "Required Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 4),
                GaColumn("RawChartCapacity", "Raw Chart Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 5),
                GaColumn("AvailableCapacity", "Available Capacity After Derating/Safety (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 6),
                GaColumn("AssignedGaPoint", "Assigned GA Point", ReportColumnValueKind.Text, "Crane Setup / Assignment", 7),
                GaColumn("LiftStatus", "Lift Status", ReportColumnValueKind.Status, "Lift Result", 8),
                GaColumn("BestRadius", "Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 9),
                GaColumn("RequiredBoomLength", "Required Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 10),
                GaColumn("CapacityUtilizationPercent", "Capacity Utilization %", ReportColumnValueKind.Percentage, "Radius / Boom / Capacity", 11),
                GaColumn("SafetyMargin", "Safety Margin", ReportColumnValueKind.Tons, "Lift Result", 12),
                GaColumn("FailureReason", "Failure Reason", ReportColumnValueKind.Warning, "Lift Result", 13),
                GaColumn("ParentAssemblyGlobalId", "Parent Assembly ID", ReportColumnValueKind.Text, "Assembly Rollup", 14),
                GaColumn("AssemblyLiftWeight", "Assembly Lift Weight (t)", ReportColumnValueKind.Tons, "Assembly Rollup", 15),
                GaColumn("AssemblyLiftCOG", "Assembly Lift COG", ReportColumnValueKind.Text, "Assembly Rollup", 16),
                GaColumn("AssemblyMemberIds", "Assembly Member IDs", ReportColumnValueKind.Text, "Assembly Rollup", 17),
                GaColumn("SuggestedAction", "Suggested Action", ReportColumnValueKind.Warning, "Lift Result", 18)
            ]
        },
        new()
        {
            Name = CapacityUtilizationReview,
            Family = ReportFamily.LiftingResult,
            DefaultItemsIncluded = "Optimization Result",
            Columns =
            [
                GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
                GaColumn("ElementMark", "Element Mark", ReportColumnValueKind.Text, "Element / Load Point", 1),
                GaColumn("Weight", "Weight", ReportColumnValueKind.Tons, "Element / Load Point", 2),
                GaColumn("RequiredRadius", "Required Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 3),
                GaColumn("SelectedChartRadius", "Selected Chart Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 4),
                GaColumn("RequiredBoomLength", "Required Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 5),
                GaColumn("SelectedChartBoomLength", "Selected Chart Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 6),
                GaColumn("RequiredCapacity", "Required Capacity", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 7),
                GaColumn("AvailableCapacity", "Available Capacity", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 8),
                GaColumn("CapacityUtilizationPercent", "Capacity Utilization %", ReportColumnValueKind.Percentage, "Radius / Boom / Capacity", 9),
                GaColumn("SafetyMargin", "Safety Margin", ReportColumnValueKind.Tons, "Lift Result", 10),
                GaColumn("ChartStatus", "Chart Status", ReportColumnValueKind.Status, "Lift Result", 11),
                GaColumn("LookupMode", "Lookup Mode", ReportColumnValueKind.Text, "Lift Result", 12)
            ]
        },
        new()
        {
            Name = FailedLiftsOnly,
            Family = ReportFamily.LiftingResult,
            DefaultItemsIncluded = "Failed Items Only",
            Columns =
            [
                GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
                GaColumn("ElementMark", "Element Mark", ReportColumnValueKind.Text, "Element / Load Point", 1),
                GaColumn("Weight", "Weight", ReportColumnValueKind.Tons, "Element / Load Point", 2),
                GaColumn("AssignedGaPoint", "Assigned GA Point / Best Failed Point", ReportColumnValueKind.Text, "Crane Setup / Assignment", 3),
                GaColumn("LiftStatus", "Lift Status", ReportColumnValueKind.Status, "Lift Result", 4),
                GaColumn("FailureCategory", "Failure Category", ReportColumnValueKind.Status, "Lift Result", 5),
                GaColumn("FailureReason", "Failure Reason", ReportColumnValueKind.Warning, "Lift Result", 6),
                GaColumn("ReachStatus", "Reach Status", ReportColumnValueKind.Status, "Lift Result", 7),
                GaColumn("ChartStatus", "Chart Status", ReportColumnValueKind.Status, "Lift Result", 8),
                GaColumn("RequiredRadius", "Required Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 9),
                GaColumn("RequiredBoomLength", "Required Boom Length", ReportColumnValueKind.Meters, "Best Failed Candidate", 10),
                GaColumn("SafetyMargin", "Safety Margin", ReportColumnValueKind.Tons, "Best Failed Candidate", 11),
                GaColumn("SuggestedAction", "Suggested Action", ReportColumnValueKind.Warning, "Lift Result", 12)
            ]
        },
        new()
        {
            Name = GaAssignmentReview,
            Family = ReportFamily.LiftingResult,
            DefaultItemsIncluded = "Optimization Result",
            Columns =
            [
                GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
                GaColumn("ElementMark", "Element Mark", ReportColumnValueKind.Text, "Element / Load Point", 1),
                GaColumn("AssignedSetupIndex", "Assigned Setup Index", ReportColumnValueKind.Number, "Crane Setup / Assignment", 2),
                GaColumn("CandidatePointId", "Candidate Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 3),
                GaColumn("IsFeasibleFromPoint", "Is Feasible From Point", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 4),
                GaColumn("IsFinalAssignedToPoint", "Is Final Assigned To Point", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 5),
                GaColumn("AssignedPointId", "Assigned Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 6),
                GaColumn("AssignmentReason", "Assignment Reason", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 7),
                GaColumn("AssignmentScore", "Assignment Score", ReportColumnValueKind.Number, "GA Assignment Diagnostics", 8),
                GaColumn("DuplicateAssignmentResolved", "Duplicate Assignment Resolved", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 9),
                GaColumn("FeasiblePointIds", "Feasible Point IDs", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 10),
                GaColumn("FinalOwnerPointId", "Final Owner Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 11)
            ]
        }
    ];

    private static readonly IReadOnlyList<ReportColumnDefinition> GaAvailableColumns =
    [
        GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
        GaColumn("GARunTimestamp", "GA Run Timestamp", ReportColumnValueKind.Text, "GA Run Summary", 1),
        GaColumn("GARunHealthLabel", "GA Run Status / Health Label", ReportColumnValueKind.Status, "GA Run Summary", 2),
        GaColumn("ProjectName", "Project Name", ReportColumnValueKind.Text, "GA Run Summary", 3),
        GaColumn("IfcFileName", "IFC File Name", ReportColumnValueKind.Text, "GA Run Summary", 4),
        GaColumn("CraneNameOrId", "Crane Name / ID", ReportColumnValueKind.Text, "GA Run Summary", 5),
        GaColumn("TotalTargetElements", "Total Target Elements", ReportColumnValueKind.Number, "GA Run Summary", 6),
        GaColumn("SelectedElements", "Selected Elements", ReportColumnValueKind.Number, "GA Run Summary", 7),
        GaColumn("LiftedElements", "Lifted Elements", ReportColumnValueKind.Number, "GA Run Summary", 8),
        GaColumn("FailedElements", "Failed Elements", ReportColumnValueKind.Number, "GA Run Summary", 9),
        GaColumn("CoverageRatio", "Coverage Ratio", ReportColumnValueKind.Text, "GA Run Summary", 10),
        GaColumn("FinalCraneSetupCount", "Final Crane Setup Count", ReportColumnValueKind.Number, "GA Run Summary", 11),
        GaColumn("GreedyIterations", "Greedy Iterations", ReportColumnValueKind.Number, "GA Run Summary", 12),
        GaColumn("PopulationSize", "Population Size", ReportColumnValueKind.Number, "GA Run Summary", 13),
        GaColumn("ConfiguredGenerations", "Configured Generations", ReportColumnValueKind.Number, "GA Run Summary", 14),
        GaColumn("ActualGenerations", "Actual Generations", ReportColumnValueKind.Number, "GA Run Summary", 15),
        GaColumn("TotalGARuntime", "Total GA Runtime", ReportColumnValueKind.Text, "GA Run Summary", 16),
        GaColumn("ElementAssignmentTime", "Element Assignment Time", ReportColumnValueKind.Text, "GA Run Summary", 17),
        GaColumn("CapacityLookupCount", "Capacity Lookup Count", ReportColumnValueKind.Text, "GA Run Summary", 18),
        GaColumn("CapacityCacheHits", "Capacity Cache Hits", ReportColumnValueKind.Text, "GA Run Summary", 19),
        GaColumn("CapacityCacheMisses", "Capacity Cache Misses", ReportColumnValueKind.Text, "GA Run Summary", 20),
        GaColumn("CapacityCacheHitRatio", "Capacity Cache Hit Ratio", ReportColumnValueKind.Text, "GA Run Summary", 21),
        GaColumn("LiftStatus", "Lift Status", ReportColumnValueKind.Status, "Lift Result", 30),
        GaColumn("IsLifted", "Is Lifted", ReportColumnValueKind.YesNo, "Lift Result", 31),
        GaColumn("FailureReason", "Failure Reason", ReportColumnValueKind.Warning, "Lift Result", 32),
        GaColumn("FailureCategory", "Failure Category", ReportColumnValueKind.Status, "Lift Result", 33),
        GaColumn("SuggestedAction", "Suggested Action", ReportColumnValueKind.Warning, "Lift Result", 34),
        GaColumn("ReachStatus", "Reach Status", ReportColumnValueKind.Status, "Lift Result", 35),
        GaColumn("ChartStatus", "Chart Status", ReportColumnValueKind.Status, "Lift Result", 36),
        GaColumn("SafetyMargin", "Safety Margin", ReportColumnValueKind.Tons, "Lift Result", 37),
        GaColumn("LookupMode", "Lookup Mode", ReportColumnValueKind.Text, "Lift Result", 38),
        GaColumn("BestRadius", "Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 50),
        GaColumn("RequiredRadius", "Required Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 51),
        GaColumn("SelectedChartRadius", "Selected Chart Radius", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 52),
        GaColumn("RequiredBoomLength", "Required Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 53),
        GaColumn("SelectedChartBoomLength", "Selected Chart Boom Length", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 54),
        GaColumn("HookRiggingWeight", "Hook / Rigging Weight (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 55),
        GaColumn("RequiredCapacity", "Required Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 56),
        GaColumn("RawChartCapacity", "Raw Chart Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 57),
        GaColumn("AvailableCapacity", "Available Capacity After Derating/Safety (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 58),
        GaColumn("CapacityUtilizationPercent", "Capacity Utilization %", ReportColumnValueKind.Percentage, "Radius / Boom / Capacity", 59),
        GaColumn("CapacityMargin", "Safety Margin (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 60),
        GaColumn("IsExactMatch", "Is Exact Match", ReportColumnValueKind.YesNo, "Radius / Boom / Capacity", 59),
        GaColumn("IsSteppedRadius", "Is Stepped Radius", ReportColumnValueKind.YesNo, "Radius / Boom / Capacity", 60),
        GaColumn("IsBlankCell", "Is Blank Cell", ReportColumnValueKind.YesNo, "Radius / Boom / Capacity", 61),
        GaColumn("IsZeroCell", "Is Zero Cell", ReportColumnValueKind.YesNo, "Radius / Boom / Capacity", 62),
        GaColumn("AssignedSetupIndex", "Assigned Setup Index", ReportColumnValueKind.Number, "Crane Setup / Assignment", 80),
        GaColumn("AssignedGaPoint", "Assigned GA Point", ReportColumnValueKind.Text, "Crane Setup / Assignment", 81),
        GaColumn("AssignedCraneX", "Assigned Crane X", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 82),
        GaColumn("AssignedCraneY", "Assigned Crane Y", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 83),
        GaColumn("AssignedCraneZ", "Assigned Crane Z", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 84),
        GaColumn("CraneBaseX", "Crane Base X", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 85),
        GaColumn("CraneBaseY", "Crane Base Y", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 86),
        GaColumn("CraneBaseZ", "Crane Base Z", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 87),
        GaColumn("SlewCenterX", "Slew Center X", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 88),
        GaColumn("SlewCenterY", "Slew Center Y", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 89),
        GaColumn("SlewCenterZ", "Slew Center Z", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 90),
        GaColumn("BoomHingeX", "Boom Hinge X", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 91),
        GaColumn("BoomHingeY", "Boom Hinge Y", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 92),
        GaColumn("BoomHingeZ", "Boom Hinge Z", ReportColumnValueKind.Meters, "Crane Setup / Assignment", 93),
        GaColumn("RadiusOriginType", "Radius Origin Type", ReportColumnValueKind.Text, "Crane Setup / Assignment", 94),
        GaColumn("BoomOriginType", "Boom Origin Type", ReportColumnValueKind.Text, "Crane Setup / Assignment", 95),
        GaColumn("ElementId", "Element ID", ReportColumnValueKind.Text, "Element / Load Point", 110),
        GaColumn("ElementMark", "Element Mark / Name", ReportColumnValueKind.Text, "Element / Load Point", 111),
        GaColumn("ElementType", "Element Type", ReportColumnValueKind.Text, "Element / Load Point", 112),
        GaColumn("StoreyOrElevation", "Storey / Elevation", ReportColumnValueKind.Text, "Element / Load Point", 113),
        GaColumn("Weight", "Element Weight (t)", ReportColumnValueKind.Tons, "Element / Load Point", 114),
        GaColumn("CogX", "COG X", ReportColumnValueKind.Meters, "Element / Load Point", 115),
        GaColumn("CogY", "COG Y", ReportColumnValueKind.Meters, "Element / Load Point", 116),
        GaColumn("CogZ", "COG Z", ReportColumnValueKind.Meters, "Element / Load Point", 117),
        GaColumn("LoadPointX", "Load Point X", ReportColumnValueKind.Meters, "Element / Load Point", 118),
        GaColumn("LoadPointY", "Load Point Y", ReportColumnValueKind.Meters, "Element / Load Point", 119),
        GaColumn("LoadPointZ", "Load Point Z", ReportColumnValueKind.Meters, "Element / Load Point", 120),
        GaColumn("CandidatePointId", "Candidate Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 140),
        GaColumn("CandidatePointIndex", "Candidate Point Index", ReportColumnValueKind.Number, "GA Assignment Diagnostics", 141),
        GaColumn("IsFeasibleFromPoint", "Is Feasible From Point", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 142),
        GaColumn("IsFinalAssignedToPoint", "Is Final Assigned To Point", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 143),
        GaColumn("AssignedPointId", "Assigned Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 144),
        GaColumn("AssignedPointIndex", "Assigned Point Index", ReportColumnValueKind.Number, "GA Assignment Diagnostics", 145),
        GaColumn("AssignmentReason", "Assignment Reason", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 146),
        GaColumn("AssignmentScore", "Assignment Score", ReportColumnValueKind.Number, "GA Assignment Diagnostics", 147),
        GaColumn("DuplicateAssignmentResolved", "Duplicate Assignment Resolved", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 148),
        GaColumn("FeasiblePointIds", "Feasible Point IDs", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 149),
        GaColumn("FinalOwnerPointId", "Final Owner Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 150),
        GaColumn("IsFinalAssigned", "Is Final Assigned", ReportColumnValueKind.YesNo, "GA Assignment Diagnostics", 151),
        GaColumn("BestFailedPointId", "Best Failed Point ID", ReportColumnValueKind.Text, "GA Assignment Diagnostics", 152),
        GaColumn("BestFailureReason", "Best Failure Reason", ReportColumnValueKind.Warning, "GA Assignment Diagnostics", 153),
        GaColumn("HasBestFailedCandidate", "Has Best Failed Candidate", ReportColumnValueKind.YesNo, "Best Failed Candidate", 170),
        GaColumn("BestFailedLiftStatus", "Best Failed Lift Status", ReportColumnValueKind.Status, "Best Failed Candidate", 171),
        GaColumn("BestFailedReachStatus", "Best Failed Reach Status", ReportColumnValueKind.Status, "Best Failed Candidate", 172),
        GaColumn("BestFailedChartStatus", "Best Failed Chart Status", ReportColumnValueKind.Status, "Best Failed Candidate", 173),
        GaColumn("BestFailedSafetyMargin", "Best Failed Safety Margin", ReportColumnValueKind.Tons, "Best Failed Candidate", 174),
        GaColumn("BestFailedRequiredRadius", "Best Failed Required Radius", ReportColumnValueKind.Meters, "Best Failed Candidate", 175),
        GaColumn("BestFailedRequiredBoomLength", "Best Failed Required Boom Length", ReportColumnValueKind.Meters, "Best Failed Candidate", 176),
        GaColumn("CandidateBoomsChecked", "Candidate Booms Checked", ReportColumnValueKind.Text, "Load Chart Diagnostics", 190),
        GaColumn("RejectedCandidates", "Rejected Candidates", ReportColumnValueKind.Text, "Load Chart Diagnostics", 191),
        GaColumn("SelectedVisualColumnIndex", "Selected Visual Column Index", ReportColumnValueKind.Number, "Load Chart Diagnostics", 192),
        GaColumn("SelectedVisualColumnHeader", "Selected Visual Column Header", ReportColumnValueKind.Text, "Load Chart Diagnostics", 193),
        GaColumn("SelectedNumberOfLines", "Selected Number Of Lines", ReportColumnValueKind.Number, "Load Chart Diagnostics", 194),
        GaColumn("SelectedOperatingConditionLabel", "Selected Operating Condition Label", ReportColumnValueKind.Text, "Load Chart Diagnostics", 195),
        GaColumn("ConservativeDuplicateFallback", "Conservative Duplicate Fallback", ReportColumnValueKind.YesNo, "Load Chart Diagnostics", 196),
        GaColumn("DuplicateAlternativeCount", "Duplicate Alternative Count", ReportColumnValueKind.Number, "Load Chart Diagnostics", 197),
        GaColumn("DuplicateAlternativeSummary", "Duplicate Alternative Summary", ReportColumnValueKind.Text, "Load Chart Diagnostics", 198),
        GaColumn("ParentAssemblyGlobalId", "Parent Assembly ID", ReportColumnValueKind.Text, "Assembly Rollup", 199, isSelected: false),
        GaColumn("AssemblyLiftWeight", "Assembly Lift Weight (t)", ReportColumnValueKind.Tons, "Assembly Rollup", 200, isSelected: false),
        GaColumn("AssemblyLiftCOG", "Assembly Lift COG", ReportColumnValueKind.Text, "Assembly Rollup", 201, isSelected: false),
        GaColumn("AssemblyMemberIds", "Assembly Member IDs", ReportColumnValueKind.Text, "Assembly Rollup", 202, isSelected: false)
    ];

    private static ReportColumnDefinition Column(
        string key,
        string header,
        ReportColumnValueKind kind,
        int order,
        string? category = null,
        bool isSelected = true) =>
        new()
        {
            Key = key,
            Header = header,
            ValueKind = kind,
            Order = order,
            Category = category,
            IsSelected = isSelected
        };

    private static ReportColumnDefinition GaColumn(
        string key,
        string header,
        ReportColumnValueKind kind,
        string category,
        int order,
        bool isSelected = true) =>
        new()
        {
            Key = key,
            Header = header,
            ValueKind = kind,
            Order = order,
            Category = category,
            IsSelected = isSelected
        };
}
