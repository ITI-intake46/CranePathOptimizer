using CraneOptimizer.Application.Reports.Definitions;

namespace CraneOptimizer.Application.Reports.Services;

public static class ReportTemplateCatalog
{
    public const string BasicElementList = "Basic Element List";
    public const string WeightReview = "Weight Review";
    public const string MissingEstimatedWeight = "Missing / Estimated Weight";
    public const string ReportDataSourceAudit = "Report Data Source Audit";
    public const string GaResultReport = "GA Result Report";
    private static readonly HashSet<string> ForbiddenUserFacingColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReadyForGa",
        "AssemblyMemberCount",
        "AssignedSetupIndex",
        "AssignedGaPoint",
        "AssignedCraneX",
        "AssignedCraneY",
        "AssignedCraneZ",
        "SetupX",
        "SetupY",
        "SetupZ",
        "SafetyMargin",
        "CapacityMargin",
        "ChartStatus",
        "AssignmentReason",
        "AssignmentScore",
        "FinalEvaluationId",
        "BestFailedEvaluationId",
        "CandidatePointIndex",
        "IsFinalAssignedToPoint",
        "DocumentId",
        "ReportBuildSessionId",
        "SourceSignature",
        "InputSignature",
        "CanonicalDocumentHash",
        "IncludedInCraneOptimization",
        "ValidationStatus",
        "BlockingReasons"
    };

    public static IReadOnlyList<ReportTemplateDefinition> GetBuiltInTemplates(ReportFamily family) =>
        family == ReportFamily.ElementList ? ElementTemplates : LiftingTemplates;

    public static IReadOnlyList<ReportTemplateDefinition> GetNormalTemplates() =>
    [
        GetBuiltInTemplate(ReportFamily.ElementList, BasicElementList),
        GetBuiltInTemplate(ReportFamily.LiftingResult, GaResultReport)
    ];

    public static IReadOnlyList<ReportTemplateDefinition> GetUserFacingTemplates(ReportFamily family) =>
        family == ReportFamily.ElementList
            ? ElementTemplates.Where(template => template.Name == BasicElementList).ToList()
            : LiftingTemplates.Where(template => template.Name == GaResultReport).ToList();

    public static ReportFamily GetFamilyForTemplateName(string templateName)
    {
        if (LiftingTemplates.Any(template => template.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase)))
            return ReportFamily.LiftingResult;

        return ReportFamily.ElementList;
    }

    public static ReportTemplateDefinition GetBuiltInTemplate(string name)
    {
        var template = ElementTemplates.Concat(LiftingTemplates)
            .FirstOrDefault(template => template.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return template ?? GetNormalTemplates()[0];
    }

    public static IReadOnlyList<ReportColumnDefinition> GetAvailableColumns(ReportFamily family) =>
        family == ReportFamily.ElementList
            ? ElementAvailableColumns
            : GaAvailableColumns;

    public static bool IsColumnSupported(ReportFamily family, string key) =>
        !IsUserFacingForbiddenColumn(key)
        && (GetAvailableColumns(family).Any(column => column.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            || GetBuiltInTemplates(family).SelectMany(template => template.Columns)
                .Any(column => column.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));

    public static bool IsUserFacingForbiddenColumn(string key) =>
        ForbiddenUserFacingColumns.Contains(key);

    public static bool IsPostGaColumn(string key) =>
        !ElementAvailableColumns.Any(column => column.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        && !ElementTemplates.SelectMany(template => template.Columns)
            .Any(column => column.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        &&
        GaAvailableColumns.Any(column =>
            column.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && column.EvidenceScope is not ReportEvidenceScope.SharedIdentity);

    public static ReportTemplateDefinition GetBuiltInTemplate(ReportFamily family, string name) =>
        GetBuiltInTemplates(family).FirstOrDefault(template => template.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? GetUserFacingTemplates(family)[0];

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
                Column("MaterialType", "Material Type", ReportColumnValueKind.Text, 4),
                Column("MaterialGrade", "Material Grade", ReportColumnValueKind.Text, 5),
                Column("Length", "Length (m)", ReportColumnValueKind.Meters, 6, "Geometry"),
                Column("Width", "Width (m)", ReportColumnValueKind.Meters, 7, "Geometry"),
                Column("Height", "Height (m)", ReportColumnValueKind.Meters, 8, "Geometry"),
                Column("VolumeM3", "Volume (m\u00b3)", ReportColumnValueKind.Number, 9, "Geometry"),
                Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 10, "Weight and Capacity"),
                Column("CogX", "COG X (m)", ReportColumnValueKind.Meters, 11, "Geometry"),
                Column("CogY", "COG Y (m)", ReportColumnValueKind.Meters, 12, "Geometry"),
                Column("CogZ", "COG Z (m)", ReportColumnValueKind.Meters, 13, "Geometry"),
                Column("IncludedInAnalysis", "Included in Analysis", ReportColumnValueKind.YesNo, 14, "Review / Lift Status"),
                Column("AnalysisExclusionReason", "Analysis Exclusion Reason", ReportColumnValueKind.Warning, 15, "Review / Lift Status")
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
        Column("ElementType", "Element Type", ReportColumnValueKind.Text, 3, "Identity", isSelected: false),
        Column("MaterialType", "Material Type", ReportColumnValueKind.Text, 4, "Identity", isSelected: false),
        Column("MaterialGrade", "Material Grade", ReportColumnValueKind.Text, 5, "Identity", isSelected: false),
        Column("Length", "Length (m)", ReportColumnValueKind.Meters, 6, "Geometry", isSelected: false),
        Column("Width", "Width (m)", ReportColumnValueKind.Meters, 7, "Geometry", isSelected: false),
        Column("Height", "Height (m)", ReportColumnValueKind.Meters, 8, "Geometry", isSelected: false),
        Column("VolumeM3", "Volume (m\u00b3)", ReportColumnValueKind.Number, 9, "Geometry", isSelected: false),
        Column("WeightTons", "Weight", ReportColumnValueKind.Tons, 10, "Weight and Capacity", isSelected: false),
        Column("CogX", "COG X (m)", ReportColumnValueKind.Meters, 11, "Geometry", isSelected: false),
        Column("CogY", "COG Y (m)", ReportColumnValueKind.Meters, 12, "Geometry", isSelected: false),
        Column("CogZ", "COG Z (m)", ReportColumnValueKind.Meters, 13, "Geometry", isSelected: false),
        Column("IncludedInAnalysis", "Included in Analysis", ReportColumnValueKind.YesNo, 14, "Review / Lift Status", isSelected: false),
        Column("AnalysisExclusionReason", "Analysis Exclusion Reason", ReportColumnValueKind.Warning, 15, "Review / Lift Status", isSelected: false)
    ];

    private static readonly IReadOnlyList<ReportTemplateDefinition> LiftingTemplates =
    [
        new()
        {
            Name = GaResultReport,
            Family = ReportFamily.LiftingResult,
            DefaultItemsIncluded = "All Submitted Lift Units",
            Columns =
            [
                GaColumn("No", "No.", ReportColumnValueKind.Number, "Identity", 0),
                GaColumn("ElementMark", "Element Mark", ReportColumnValueKind.Text, "Element / Load Point", 1),
                GaColumn("ElementName", "Element Name", ReportColumnValueKind.Text, "Element / Load Point", 2),
                GaColumn("ElementType", "Element Type", ReportColumnValueKind.Text, "Element / Load Point", 3),
                GaColumn("MaterialType", "Material Type", ReportColumnValueKind.Text, "Element / Load Point", 4),
                GaColumn("Length", "Length (m)", ReportColumnValueKind.Meters, "Shared Engineering", 5, isSelected: false),
                GaColumn("Width", "Width (m)", ReportColumnValueKind.Meters, "Shared Engineering", 6, isSelected: false),
                GaColumn("Height", "Height (m)", ReportColumnValueKind.Meters, "Shared Engineering", 7, isSelected: false),
                GaColumn("VolumeM3", "Volume (m\u00b3)", ReportColumnValueKind.Number, "Shared Engineering", 8, isSelected: false),
                GaColumn("Weight", "Weight", ReportColumnValueKind.Tons, "Element / Load Point", 9),
                GaColumn("CogX", "COG X (m)", ReportColumnValueKind.Meters, "Shared Engineering", 10),
                GaColumn("CogY", "COG Y (m)", ReportColumnValueKind.Meters, "Shared Engineering", 11),
                GaColumn("CogZ", "COG Z (m)", ReportColumnValueKind.Meters, "Shared Engineering", 12),
                GaColumn("LiftStatus", "Lift Status", ReportColumnValueKind.Status, "Lift Result", 13, isSelected: false),
                GaColumn("RequiredRadius", "Required Radius (m)", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 14),
                GaColumn("SelectedChartRadius", "Selected Chart Radius (m)", ReportColumnValueKind.Meters, "Load Chart Evidence", 15),
                GaColumn("RequiredBoomLength", "Required Boom Length (m)", ReportColumnValueKind.Meters, "Radius / Boom / Capacity", 16),
                GaColumn("SelectedChartBoomLength", "Selected Chart Boom Length (m)", ReportColumnValueKind.Meters, "Load Chart Evidence", 17),
                GaColumn("HookRiggingWeight", "Hook / Rigging Weight (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 18, isSelected: false),
                GaColumn("RequiredCapacity", "Required Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 19),
                GaColumn("AvailableCapacity", "Adjusted Allowable Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 20),
                GaColumn("RawChartCapacity", "Manufacturer Chart Capacity (t)", ReportColumnValueKind.Tons, "Radius / Boom / Capacity", 21),
                GaColumn("CapacityUtilizationPercent", "Capacity Utilization (%)", ReportColumnValueKind.Percentage, "Radius / Boom / Capacity", 22),
                GaColumn("ReachStatus", "Reach Status", ReportColumnValueKind.Status, "Load Chart Evidence", 23, isSelected: false),
                GaColumn("FailureCategory", "Failure Category", ReportColumnValueKind.Status, "Lift Result", 24),
                GaColumn("FailureReason", "Failure Reason", ReportColumnValueKind.Warning, "Lift Result", 25),
                GaColumn("SuggestedAction", "Suggested Action", ReportColumnValueKind.Warning, "Lift Result", 26)
            ]
        }
    ];

    private static IReadOnlyList<ReportColumnDefinition> GaAvailableColumns =>
        GetBuiltInTemplate(ReportFamily.LiftingResult, GaResultReport)
            .Columns
            .Select(column => new ReportColumnDefinition
            {
                Key = column.Key,
                Header = column.Header,
                ValueKind = column.ValueKind,
                EvidenceScope = column.EvidenceScope,
                SupportedFamilies = column.SupportedFamilies,
                RequiredCapabilities = column.RequiredCapabilities,
                ShowInPreview = column.ShowInPreview,
                ShowInPdf = column.ShowInPdf,
                ShowInExcel = column.ShowInExcel,
                IsSelected = false,
                Order = column.Order,
                Format = column.Format,
                Alignment = column.Alignment,
                TextSize = column.TextSize,
                Category = column.Category,
                Unit = column.Unit
            })
            .ToList();

    private static ReportColumnDefinition Column(
        string key,
        string header,
        ReportColumnValueKind kind,
        int order,
        string? category = null,
        bool isSelected = true,
        ReportEvidenceScope evidenceScope = ReportEvidenceScope.CurrentModel) =>
        new()
        {
            Key = key,
            Header = header,
            ValueKind = kind,
            EvidenceScope = evidenceScope,
            SupportedFamilies = [ReportFamily.ElementList],
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
        bool isSelected = true,
        ReportEvidenceScope evidenceScope = ReportEvidenceScope.GaFinalOutcome) =>
        new()
        {
            Key = key,
            Header = header,
            ValueKind = kind,
            EvidenceScope = evidenceScope,
            SupportedFamilies = [ReportFamily.LiftingResult],
            Order = order,
            Category = category,
            IsSelected = isSelected
        };
}
