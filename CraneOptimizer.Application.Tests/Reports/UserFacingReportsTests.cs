using System.IO.Compression;
using System.Xml.Linq;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Reports.UserFacing;

namespace CraneOptimizer.Application.Tests.Reports;

public sealed class UserFacingReportsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"crane-report-tests-{Guid.NewGuid():N}");

    public UserFacingReportsTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExcelExporter_GeneratesParsableWorksheets_WithInvalidTextCharacters()
    {
        var report = new ReportDocumentDto
        {
            InternalFamily = ReportFamily.ElementList,
            TextAlignment = "Left",
            TextSizePt = 10,
            Metadata = new ReportMetadataDto
            {
                ReportName = "Basic Element List",
                ItemsIncluded = "All Elements",
                ProjectName = "Project 33",
                ReportDate = new DateTime(2026, 6, 22, 23, 5, 0),
                GeneralNotes = "General notes with invalid XML char \u0001 kept safe."
            },
            Columns =
            [
                new ReportColumnDefinition { Key = "No", Header = "No.", ValueKind = ReportColumnValueKind.Number, Order = 0, TextSize = "10" },
                new ReportColumnDefinition { Key = "ElementMark", Header = "Element Mark", ValueKind = ReportColumnValueKind.Text, Order = 1, TextSize = "10" },
                new ReportColumnDefinition { Key = "Weight", Header = "Weight (t)", ValueKind = ReportColumnValueKind.Tons, Order = 2, TextSize = "10" }
            ],
            Rows =
            [
                new ReportDataRowDto
                {
                    Values = new Dictionary<string, object?>
                    {
                        ["No"] = 1,
                        ["ElementMark"] = "Beam\u0002 & <unsafe>",
                        ["Weight"] = 12.34
                    }
                }
            ]
        };

        var path = Path.Combine(_tempDir, "report.xlsx");
        await new UserFacingReportExcelExporter().ExportAsync(report, path);

        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            await using var stream = entry.Open();
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            Assert.NotNull(document.Root);
        }

        var sheet2 = ReadArchiveText(archive, "xl/worksheets/sheet2.xml");
        Assert.Contains("<sheetViews>", sheet2);
        Assert.True(sheet2.IndexOf("<sheetViews>", StringComparison.Ordinal) < sheet2.IndexOf("<cols>", StringComparison.Ordinal));
        Assert.Contains("width=\"42\"", sheet2);
        Assert.DoesNotContain(sheet2, ch => ch < 0x20 && ch is not '\t' and not '\n' and not '\r');
        Assert.Contains("&lt;unsafe&gt;", sheet2);
    }

    private static string ReadArchiveText(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void ReportableElementResolver_ChildElement_ResolvesToParent()
    {
        var parent = Element("parent", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var child = Element("child", parentId: "parent", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var resolver = new ReportableElementResolver();

        var result = resolver.ResolveReportableElements([child, parent]);

        Assert.Single(result);
        Assert.Same(parent, result[0]);
    }

    [Fact]
    public void ReportableElementResolver_StandaloneElement_ResolvesToItself()
    {
        var standalone = Element("standalone", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var resolver = new ReportableElementResolver();

        var result = resolver.ResolveReportableElements([standalone]);

        Assert.Single(result);
        Assert.Same(standalone, result[0]);
    }

    [Fact]
    public void ReportableElementResolver_DuplicateParents_AreRemoved()
    {
        var parent = Element("parent", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var childA = Element("child-a", parentId: "parent", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var childB = Element("child-b", parentId: "parent", trustLevel: IfcWeightTrustLevel.ExplicitQuantity);
        var resolver = new ReportableElementResolver();

        var result = resolver.ResolveReportableElements([childA, childB, parent]);

        Assert.Single(result);
        Assert.Same(parent, result[0]);
    }

    [Theory]
    [InlineData(IfcWeightTrustLevel.ExplicitQuantity, 10.0, "Yes", "OK", WeightWarningCode.None)]
    [InlineData(IfcWeightTrustLevel.Missing, null, "No", "Missing", WeightWarningCode.MissingExplicitWeight)]
    [InlineData(IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA, 10.0, "No", "Estimated", WeightWarningCode.EstimatedFromVolumeDensity)]
    [InlineData(IfcWeightTrustLevel.ExplicitZeroFromIFC, 0.0, "No", "Invalid", WeightWarningCode.ZeroWeightFromIFC)]
    public void WeightMapping_ReturnsCleanUserFacingValues(
        IfcWeightTrustLevel trustLevel,
        double? weight,
        string verified,
        string status,
        WeightWarningCode warning)
    {
        var element = Element("e1", weight, trustLevel: trustLevel);

        var result = ReportWeightMapper.Map(element);

        Assert.Equal(verified, result.WeightVerified);
        Assert.Equal(status, result.WeightStatus);
        Assert.Equal(warning, result.WarningCode);
    }

    [Fact]
    public void LiftingUtilization_UsesReducedAvailableCapacity()
    {
        var row = LiftRow(required: 9.0, available: 12.0 * 0.90, status: "Liftable");

        var utilization = LiftingResultReportDataProvider.CalculateUtilization(row);

        Assert.Equal(83.333, utilization!.Value, precision: 3);
    }

    [Fact]
    public void LiftingUtilization_Overweight_CanExceedOneHundredPercent()
    {
        var row = LiftRow(required: 11.0, available: 10.0, status: "Overweight");

        var utilization = LiftingResultReportDataProvider.CalculateUtilization(row);

        Assert.Equal(110.0, utilization!.Value, precision: 3);
    }

    [Fact]
    public void LiftingUtilization_OutOfRadius_ReturnsNull()
    {
        var row = LiftRow(required: 5.0, available: 10.0, status: "OutOfRadius");

        var utilization = LiftingResultReportDataProvider.CalculateUtilization(row);

        Assert.Null(utilization);
    }

    [Fact]
    public async Task TemplateStore_SavesColumnsAndSettings_NotRows()
    {
        var store = new JsonReportTemplateStore(_tempDir);
        var template = new ReportTemplateDto
        {
            TemplateName = "Custom Element Report",
            InternalFamily = ReportFamily.ElementList,
            ItemsIncluded = "All Elements",
            Columns =
            [
                new ReportColumnDefinition
                {
                    Key = "ElementMark",
                    Header = "Element Mark",
                    Order = 0,
                    IsSelected = true
                }
            ]
        };

        await store.SaveAsync(template);

        var loaded = await store.LoadAsync(ReportFamily.ElementList);
        var json = File.ReadAllText(Directory.GetFiles(_tempDir).Single());

        Assert.Single(loaded);
        Assert.Equal("Custom Element Report", loaded[0].TemplateName);
        Assert.Contains("ElementMark", json);
        Assert.DoesNotContain("Rows", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ElementIds", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportBuilder_UsesTemplateNameAsReportName()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.WeightReview, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.Equal(ReportTemplateCatalog.WeightReview, document.Metadata.ReportName);
        Assert.DoesNotContain(document.Metadata.SummaryItems, item => item.Label == "Report Type");
    }

    [Fact]
    public async Task ReportBuilder_MetadataExistsAndRowsAreSeparate()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.NotNull(document.Metadata);
        Assert.NotEmpty(document.Metadata.SummaryItems);
        Assert.NotEmpty(document.Rows);
        Assert.Equal("Basic Element List", document.Metadata.ReportName);
    }

    [Fact]
    public async Task ReportBuilder_UsesRequestedGeneratedOn()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.Equal(request.Metadata.ReportDate, document.Metadata.ReportDate);
    }

    [Fact]
    public async Task ReportBuilder_NotesIncludedOnlyWhenProvided()
    {
        var builder = CreateBuilder();

        var withoutNotes = await builder.BuildAsync(ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: ""));
        var withNotes = await builder.BuildAsync(ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: "Review with site team."));

        Assert.True(string.IsNullOrWhiteSpace(withoutNotes.Metadata.GeneralNotes));
        Assert.Equal("Review with site team.", withNotes.Metadata.GeneralNotes);
    }

    [Fact]
    public async Task ReportBuilder_NoColumn_IsSequentialAfterFiltering()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.MissingEstimatedWeight, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.NotEmpty(document.Rows);
        Assert.Equal(
            Enumerable.Range(1, document.Rows.Count).Cast<object>(),
            document.Rows.Select(row => row.Values["No"]));
    }

    [Fact]
    public void ReportTemplateCatalog_LiftingAvailableColumns_ExposeGaDiagnosticsGroups()
    {
        var columns = ReportTemplateCatalog.GetAvailableColumns(ReportFamily.LiftingResult);

        Assert.Contains(columns, column => column.Key == "GARunTimestamp" && column.Category == "GA Run Summary");
        Assert.Contains(columns, column => column.Key == "SelectedVisualColumnHeader" && column.Category == "Load Chart Diagnostics");
        Assert.Contains(columns, column => column.Key == "BestFailedSafetyMargin" && column.Category == "Best Failed Candidate");
        Assert.Contains(
            ReportTemplateCatalog.GetBuiltInTemplates(ReportFamily.LiftingResult),
            template => template.Name == ReportTemplateCatalog.GaAssignmentReview);
    }

    [Fact]
    public void ReportTemplateCatalog_GaReadiness_IncludesRequiredLiftColumns()
    {
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var selectedKeys = template.Columns
            .Where(column => column.IsSelected)
            .Select(column => column.Key)
            .ToList();

        Assert.Equal("All Elements", template.DefaultItemsIncluded);
        Assert.Equal(
            [
                "No",
                "ElementMark",
                "SourceRevitId",
                "StoreyLevel",
                "WeightTons",
                "WeightVerified",
                "WeightStatus",
                "IncludedInCraneOptimization",
                "LiftStatus",
                "BestRadius",
                "RequiredBoomLength",
                "RequiredCapacity",
                "AvailableCapacity",
                "CapacityUtilizationPercent",
                "SafetyMargin",
                "FailureReason",
                "SuggestedAction"
            ],
            selectedKeys);

        Assert.Contains(template.Columns, column => column.Key == "ReachStatus" && !column.IsSelected);
        Assert.Contains(template.Columns, column => column.Key == "ChartStatus" && !column.IsSelected);
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_UsesLiftValuesAndCalculatedUtilization()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = ElementRequest(ReportTemplateCatalog.GaReadiness, "GA-Ready Elements", generalNotes: null);
        request = new ReportBuildRequestDto
        {
            Family = request.Family,
            TemplateName = request.TemplateName,
            ItemsIncluded = request.ItemsIncluded,
            Metadata = request.Metadata,
            SelectedColumns = template.Columns,
            SourceElements = request.SourceElements,
            LiftingResults =
            [
                new LiftingResultSourceRowDto
                {
                    ElementId = "revit-e1",
                    ElementMark = "M-e1",
                    Radius = 18.0,
                    BoomLength = 26.0,
                    RequiredCapacity = 9.0,
                    AvailableCapacity = 10.0,
                    InternalLiftStatus = "Liftable",
                    IsFinalAssigned = true,
                    Values = new Dictionary<string, object?>
                    {
                        ["FailureReason"] = "\u2014",
                        ["SuggestedAction"] = "\u2014"
                    }
                }
            ]
        };

        var document = await builder.BuildAsync(request);

        Assert.Single(document.Rows);
        var row = document.Rows[0].Values;
        Assert.Equal(18.0, row["BestRadius"]);
        Assert.Equal(26.0, row["RequiredBoomLength"]);
        Assert.Equal(9.0, row["RequiredCapacity"]);
        Assert.Equal(10.0, row["AvailableCapacity"]);
        Assert.Equal(90.0, Assert.IsType<double>(row["CapacityUtilizationPercent"]));
        Assert.Equal("Liftable", row["LiftStatus"]);
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_MatchesLiftValuesByElementId()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = ElementRequest(ReportTemplateCatalog.GaReadiness, "All Elements", generalNotes: null);
        request = new ReportBuildRequestDto
        {
            Family = request.Family,
            TemplateName = request.TemplateName,
            ItemsIncluded = request.ItemsIncluded,
            Metadata = request.Metadata,
            SelectedColumns = template.Columns,
            SourceElements = request.SourceElements,
            LiftingResults =
            [
                new LiftingResultSourceRowDto
                {
                    ElementId = "e1",
                    ElementMark = "Different display name",
                    Radius = 18.0,
                    BoomLength = 26.0,
                    RequiredCapacity = 9.0,
                    AvailableCapacity = 10.0,
                    InternalLiftStatus = "Liftable",
                    IsFinalAssigned = true
                }
            ]
        };

        var document = await builder.BuildAsync(request);

        var row = Assert.Single(document.Rows.Where(row => Equals(row.Values["ElementId"], "e1")));
        Assert.Equal(18.0, row.Values["BestRadius"]);
        Assert.Equal("Liftable", row.Values["LiftStatus"]);
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_UsesPlaceholdersWhenGaValuesAreMissing()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = ElementRequest(ReportTemplateCatalog.GaReadiness, "GA-Ready Elements", generalNotes: null);
        request = new ReportBuildRequestDto
        {
            Family = request.Family,
            TemplateName = request.TemplateName,
            ItemsIncluded = request.ItemsIncluded,
            Metadata = request.Metadata,
            SelectedColumns = template.Columns,
            SourceElements = request.SourceElements,
            LiftingResults = []
        };

        var document = await builder.BuildAsync(request);

        Assert.Single(document.Rows);
        Assert.Equal("\u2014", document.Rows[0].Values["LiftStatus"]);
        Assert.Equal("\u2014", document.Rows[0].Values["CapacityUtilizationPercent"]);
        Assert.Equal("\u2014", document.Rows[0].Values["FailureReason"]);
    }

    [Fact]
    public async Task ReportBuilder_GaReadinessReadyOnlyEmpty_FallsBackToAllElementsForExport()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "GA-Ready Elements",
            Metadata = new ReportMetadataDto
            {
                ProjectName = "Test Project",
                ReportDate = new DateTime(2026, 6, 23)
            },
            SelectedColumns = template.Columns,
            SourceElements =
            [
                Element("not-ready", null, IfcWeightTrustLevel.Missing)
            ]
        };

        var document = await builder.BuildAsync(request);

        Assert.Equal("All Elements", document.Metadata.ItemsIncluded);
        Assert.Single(document.Rows);
        Assert.Equal("Missing", document.Rows[0].Values["WeightStatus"]);
    }

    [Fact]
    public async Task ReportBuilder_ElementReports_DeriveStoreyLevelFromElevation()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.All(document.Rows, row =>
        {
            var storey = Assert.IsType<string>(row.Values["StoreyLevel"]);
            Assert.NotEqual("-", storey);
            Assert.StartsWith("Level", storey);
            Assert.DoesNotContain("m", storey, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ReportBuilder_LiftingWithoutGaRun_UsesNoRunMetadataAndPlaceholders()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.BasicLiftingResult);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.LiftingResult,
            TemplateName = template.Name,
            ItemsIncluded = template.DefaultItemsIncluded,
            Metadata = new ReportMetadataDto
            {
                ProjectName = "Test Project",
                ReportDate = new DateTime(2026, 6, 23),
                HasGaRun = false,
                GaRunStatusText = "No GA run",
                GaWarningText = "No GA analysis has been run for this project. Crane and lift result fields are unavailable.",
                CraneInfo = new CraneReportInfoDto { CraneName = "\u2014", CapacityFactor = 0.90 }
            },
            SelectedColumns = template.Columns,
            LiftingResults = []
        };

        var document = await builder.BuildAsync(request);

        Assert.False(document.Metadata.HasGaRun);
        Assert.Equal("No GA run", document.Metadata.GaRunStatusText);
        Assert.Equal("\u2014", document.Metadata.CraneInfo?.CraneName);
        Assert.Single(document.Rows);
        Assert.Equal(1, document.Rows[0].Values["No"]);
        Assert.Equal("\u2014", document.Rows[0].Values["LiftStatus"]);
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "GA Run" && item.Value == "No GA run");
    }

    private static UserFacingReportBuilder CreateBuilder()
    {
        var resolver = new ReportableElementResolver();
        return new UserFacingReportBuilder(
            new ElementReportDataProvider(resolver),
            new LiftingResultReportDataProvider());
    }

    private static ReportBuildRequestDto ElementRequest(string templateName, string itemsIncluded, string? generalNotes)
    {
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, templateName);

        return new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = itemsIncluded,
            Metadata = new ReportMetadataDto
            {
                ProjectName = "Test Project",
                ReportDate = new DateTime(2026, 6, 20),
                GeneralNotes = generalNotes
            },
            SelectedColumns = template.Columns,
            SourceElements =
            [
                Element("e1", 10.0, IfcWeightTrustLevel.ExplicitQuantity),
                Element("e2", null, IfcWeightTrustLevel.Missing)
            ]
        };
    }

    private static PrecastElement Element(
        string id,
        double? weight = 10.0,
        IfcWeightTrustLevel trustLevel = IfcWeightTrustLevel.ExplicitQuantity,
        string? parentId = null)
    {
        return new PrecastElement
        {
            ElementID = id,
            ElementRevitId = $"revit-{id}",
            ElementMark = $"M-{id}",
            ElementType = "IfcWall",
            ElementMaterial = "Concrete",
            ParentGlobalId = parentId,
            WeightTons = weight,
            ElementWT = weight,
            WeightTrustLevel = trustLevel,
            WeightSource = trustLevel.ToString(),
            VolumeM3 = 4.0,
            IncludeInGa = trustLevel == IfcWeightTrustLevel.ExplicitQuantity && weight is > 0,
            IncludeInRender = true
        };
    }

    private static LiftingResultSourceRowDto LiftRow(double required, double available, string status) => new()
    {
        ElementId = "e1",
        ElementMark = "M-e1",
        AssignedSetupNo = 1,
        Radius = 12.0,
        BoomLength = 20.0,
        RequiredCapacity = required,
        AvailableCapacity = available,
        InternalLiftStatus = status,
        IsFinalAssigned = true
    };
}
