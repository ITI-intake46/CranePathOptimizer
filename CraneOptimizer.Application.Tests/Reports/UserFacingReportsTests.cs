using System.IO.Compression;
using System.Xml.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Reports.UserFacing;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers;

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
    public void LiftingUtilization_UsesAvailableCapacityWhenPresent()
    {
        var row = LiftRow(required: 5.0, available: 10.0, status: "OutOfRadius");

        var utilization = LiftingResultReportDataProvider.CalculateUtilization(row);

        Assert.Equal(50.0, utilization!.Value, precision: 3);
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
    public void ElementReportDataProvider_KeepsGuidAndIfcName_WhenAssemblyMarkSuppliesDisplayLabel()
    {
        var element = Element("01jhsjVXzBy81J1pRn_FyV");
        element.ElementRevitId = null;
        element.ElementName = "W5102 - Wall";
        element.ElementMark = "W5102";
        element.AssemblyMark = "W5102";
        element.AssemblyMarkSource = "Pset_Tekla_Common.ASSEMBLY_POS";
        element.AssemblyMarkIsExplicit = true;
        element.AssemblyMarkConfidence = AssemblyMarkConfidence.High;

        var rows = new ElementReportDataProvider(
                new ReportableElementResolver(),
                new ImportReadinessService())
            .BuildRows(ReportTemplateCatalog.BasicElementList, [element]);

        var row = Assert.Single(rows);
        Assert.Equal("01jhsjVXzBy81J1pRn_FyV", row.ElementId);
        Assert.Equal("W5102 - Wall", row.ElementName);
        Assert.Equal("W5102", row.ElementMark);
        Assert.Equal("W5102", row.CanonicalMark);
        Assert.Equal("01jhsjVXzBy81J1pRn_FyV", row.SourceRevitId);
    }

    [Fact]
    public void PrecastElementMapper_RoundTripsIfcNameWithoutFallingBackToAssemblyMark()
    {
        var element = Element("01jhsjVXzBy81J1pRn_FyV");
        element.ElementRevitId = null;
        element.ElementName = "W5102 - Wall";
        element.ElementMark = "W5102";
        element.AssemblyMark = "W5102";
        element.AssemblyMarkSource = "Pset_Tekla_Common.ASSEMBLY_POS";
        element.SourceSystem = SourceSystem.Tekla;
        element.ExporterProfile = IfcExporterProfile.Tekla;

        var entity = PrecastElementMapper.ToEntity(element, projectInfoId: 10, ifcFileId: 20);
        var roundTripped = PrecastElementMapper.ToDomain(entity);

        Assert.Equal("01jhsjVXzBy81J1pRn_FyV", entity.GlobalId);
        Assert.Null(entity.Name);
        Assert.Equal("W5102 - Wall", entity.DisplayName);
        Assert.Equal("W5102", entity.Mark);
        Assert.Equal("01jhsjVXzBy81J1pRn_FyV", roundTripped.ElementID);
        Assert.Equal("W5102 - Wall", roundTripped.ElementName);
        Assert.Equal("W5102", roundTripped.ElementMark);
        Assert.Equal("W5102", roundTripped.AssemblyMark);
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
    public void ReportTemplateCatalog_GaReadiness_IsPreGaReadinessTemplate()
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
                "MarkConfidence",
                "ValidationStatus",
                "ValidationWarnings",
                "WeightTons",
                "WeightTrustLevel",
                "WeightVerified",
                "WeightStatus",
                "IncludedInCraneOptimization",
                "ReadyForGa",
                "ReadinessStatus",
                "MissingReadinessReason",
                "BlockingReasons"
            ],
            selectedKeys);

        Assert.DoesNotContain(selectedKeys, key => key == "SourceRevitId");
        Assert.DoesNotContain(selectedKeys, key => key == "LiftStatus");
        Assert.DoesNotContain(selectedKeys, key => key == "BestRadius");
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
        var gaColumns = ReportTemplateCatalog.GetAvailableColumns(ReportFamily.ElementList)
            .Where(column => column.Key is "LiftStatus" or "CapacityUtilizationPercent" or "FailureReason")
            .Select((column, index) => new ReportColumnDefinition
            {
                Key = column.Key,
                Header = column.Header,
                ValueKind = column.ValueKind,
                Order = template.Columns.Count + index,
                IsSelected = true,
                Category = column.Category
            })
            .ToList();
        var request = ElementRequest(ReportTemplateCatalog.GaReadiness, "GA-Ready Elements", generalNotes: null);
        request = new ReportBuildRequestDto
        {
            Family = request.Family,
            TemplateName = request.TemplateName,
            ItemsIncluded = request.ItemsIncluded,
            Metadata = request.Metadata,
            SelectedColumns = template.Columns.Concat(gaColumns).ToList(),
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
    public async Task ReportBuilder_GaReadiness_DoesNotSilentlyChangeScope()
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

        Assert.Equal("GA-Ready Elements", document.Metadata.ItemsIncluded);
        Assert.Single(document.Rows);
        Assert.Equal("Missing", document.Rows[0].Values["WeightStatus"]);
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_BlocksMissingCogElement()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", ReportDate = new DateTime(2026, 6, 23) },
            SelectedColumns = template.Columns,
            SourceElements = [Element("missing-cog", hasCog: false)]
        };

        var document = await builder.BuildAsync(request);

        var values = Assert.Single(document.Rows).Values;
        Assert.Equal("No", values["ReadyForGa"]);
        Assert.Equal("Blocked", values["ReadinessStatus"]);
        Assert.Contains("COG", Assert.IsType<string>(values["MissingReadinessReason"]));
        Assert.Contains("COG", Assert.IsType<string>(values["BlockingReasons"]));
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_BlocksElementsExcludedFromGa()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", ReportDate = new DateTime(2026, 6, 23) },
            SelectedColumns = template.Columns,
            SourceElements = [Element("excluded", includeInGa: false)]
        };

        var document = await builder.BuildAsync(request);

        var values = Assert.Single(document.Rows).Values;
        Assert.Equal("No", values["IncludedInCraneOptimization"]);
        Assert.Equal("No", values["ReadyForGa"]);
        Assert.Equal("Blocked", values["ValidationStatus"]);
        Assert.Contains("not included in crane optimization", Assert.IsType<string>(values["BlockingReasons"]));
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_IncludesIdentityAndWeightReadinessWarnings()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", ReportDate = new DateTime(2026, 6, 23) },
            SelectedColumns = template.Columns,
            SourceElements = [Element("low-confidence", markConfidence: AssemblyMarkConfidence.Low, assemblyMarkIsExplicit: false)]
        };

        var document = await builder.BuildAsync(request);

        var values = Assert.Single(document.Rows).Values;
        Assert.Equal("Yes", values["ReadyForGa"]);
        Assert.Equal("Warning", values["ReadinessStatus"]);
        Assert.Equal("Low", values["MarkConfidence"]);
        Assert.Equal(IfcWeightTrustLevel.ExplicitQuantity.ToString(), values["WeightTrustLevel"]);
        Assert.Contains("assembly", Assert.IsType<string>(values["ValidationWarnings"]), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportBuilder_GaReadiness_IncludesNonWeightBlockingReasons()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.GaReadiness);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", ReportDate = new DateTime(2026, 6, 23) },
            SelectedColumns = template.Columns,
            SourceElements = [Element("grouping", role: IfcElementRole.GroupingOnly)]
        };

        var document = await builder.BuildAsync(request);

        var values = Assert.Single(document.Rows).Values;
        Assert.Equal("No", values["ReadyForGa"]);
        Assert.Equal("Blocked", values["ReadinessStatus"]);
        Assert.Contains("Invalid element role", Assert.IsType<string>(values["BlockingReasons"]));
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
            Assert.DoesNotContain("m", storey, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ReportBuilder_ElementMark_DoesNotFallbackToElementType()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.BasicElementList);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project" },
            SelectedColumns = template.Columns,
            SourceElements =
            [
                new PrecastElement
                {
                    ElementID = "e1",
                    ElementName = "FOOTING-A",
                    ElementType = "PAD-FOOTING",
                    ElementMaterial = "C30",
                    ElementLength = 1.5,
                    ElementWidth = 1.5,
                    ElementHeight = 0.6,
                    ElementWT = 1.0,
                    WeightTons = 1.0,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    WeightSource = "ExplicitQuantity",
                    IncludeInGa = true
                }
            ]
        };

        var document = await builder.BuildAsync(request);
        var row = Assert.Single(document.Rows).Values;

        Assert.Equal("FOOTING-A", row["ElementMark"]);
        Assert.Equal("PAD-FOOTING", row["ElementType"]);
        Assert.Equal("C30", row["Material"]);
    }

    [Fact]
    public async Task ReportBuilder_ElementMark_NeverFallsBackToMaterialOrElementType()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.BasicElementList);
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = template.Name,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project" },
            SelectedColumns = template.Columns,
            SourceElements =
            [
                new PrecastElement
                {
                    ElementID = "e1",
                    ElementMark = " ",
                    ElementName = " ",
                    ElementType = "Footing",
                    ElementMaterial = "C30",
                    ElementWT = 1.0,
                    WeightTons = 1.0,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    WeightSource = "ExplicitQuantity",
                    IncludeInGa = true
                }
            ]
        };

        var document = await builder.BuildAsync(request);
        var row = Assert.Single(document.Rows).Values;

        Assert.Equal("e1", row["ElementMark"]);
        Assert.Equal("Footing", row["ElementType"]);
        Assert.Equal("C30", row["Material"]);
    }

    [Fact]
    public async Task ReportBuilder_BasicElementMapping_UsesIndependentNormalizedFields()
    {
        var builder = CreateBuilder();
        var columns = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.BasicElementList)
            .Columns
            .Concat([
                new ReportColumnDefinition
                {
                    Key = "ElementName",
                    Header = "Element Name",
                    ValueKind = ReportColumnValueKind.Text,
                    Order = 99,
                    IsSelected = true
                },
                new ReportColumnDefinition
                {
                    Key = "IncludedInCraneOptimization",
                    Header = "Included in GA Input",
                    ValueKind = ReportColumnValueKind.YesNo,
                    Order = 100,
                    IsSelected = true
                }
            ])
            .ToList();

        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = ReportTemplateCatalog.BasicElementList,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project" },
            SelectedColumns = columns,
            SourceElements =
            [
                new PrecastElement
                {
                    ElementID = "E1",
                    ElementMark = "MARK-001",
                    ElementName = "FOOTING-A",
                    ElementType = "Footing",
                    ElementMaterial = "C30",
                    VolumeM3 = 1.25,
                    WeightTons = 3.30,
                    ElementWT = 3.30,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    WeightSource = "ExplicitQuantity",
                    IncludeInGa = true
                }
            ]
        };

        var document = await builder.BuildAsync(request);
        var row = Assert.Single(document.Rows).Values;

        Assert.Equal("MARK-001", row["ElementMark"]);
        Assert.Equal("FOOTING-A", row["ElementName"]);
        Assert.Equal("Footing", row["ElementType"]);
        Assert.Equal("C30", row["Material"]);
        Assert.Equal(1.25, Assert.IsType<double>(row["VolumeM3"]), precision: 3);
        Assert.Equal(3.30, Assert.IsType<double>(row["WeightTons"]), precision: 3);
        Assert.Equal("Yes", row["IncludedInCraneOptimization"]);
    }

    [Fact]
    public async Task ReportBuilder_VolumeFallsBackToDimensions()
    {
        var builder = CreateBuilder();
        var columns = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.BasicElementList)
            .Columns
            .Concat([
                new ReportColumnDefinition
                {
                    Key = "VolumeSource",
                    Header = "Volume Source",
                    ValueKind = ReportColumnValueKind.Text,
                    Order = 99,
                    IsSelected = true
                }
            ])
            .ToList();

        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = ReportTemplateCatalog.BasicElementList,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project" },
            SelectedColumns = columns,
            SourceElements =
            [
                new PrecastElement
                {
                    ElementID = "e1",
                    ElementMark = "M1",
                    ElementType = "Footing",
                    ElementMaterial = "C30",
                    ElementLength = 1.5,
                    ElementWidth = 1.5,
                    ElementHeight = 0.6,
                    ElementVolume = 0,
                    VolumeM3 = 0,
                    ElementWT = 1.0,
                    WeightTons = 1.0,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    WeightSource = "ExplicitQuantity",
                    IncludeInGa = true
                }
            ]
        };

        var document = await builder.BuildAsync(request);
        var row = Assert.Single(document.Rows).Values;

        Assert.Equal(1.35, Assert.IsType<double>(row["VolumeM3"]), precision: 3);
        Assert.Equal("Calculated from dimensions", row["VolumeSource"]);
    }

    [Fact]
    public void ReportTemplateCatalog_DefaultTemplates_DoNotSelectSourceIdOrDuplicateWeight()
    {
        foreach (var template in ReportTemplateCatalog.GetBuiltInTemplates(ReportFamily.ElementList)
                     .Concat(ReportTemplateCatalog.GetBuiltInTemplates(ReportFamily.LiftingResult)))
        {
            var selectedKeys = template.Columns.Where(column => column.IsSelected).Select(column => column.Key).ToList();

            Assert.DoesNotContain("SourceRevitId", selectedKeys);
            Assert.Equal(selectedKeys.Count, selectedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.True(selectedKeys.Count(key => key is "Weight" or "WeightTons") <= 1);
        }
    }

    [Fact]
    public async Task ReportBuilder_DuplicateSelectedColumns_AreCollapsed()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);
        request = new ReportBuildRequestDto
        {
            Family = request.Family,
            TemplateName = request.TemplateName,
            ItemsIncluded = request.ItemsIncluded,
            Metadata = request.Metadata,
            SelectedColumns = request.SelectedColumns.Concat([request.SelectedColumns.First(column => column.Key == "WeightTons")]).ToList(),
            SourceElements = request.SourceElements
        };

        var document = await builder.BuildAsync(request);

        Assert.Equal(
            document.Columns.Count,
            document.Columns.Select(column => column.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task ReportBuilder_BasicElementListSummary_DoesNotShowGaReadyItems()
    {
        var builder = CreateBuilder();
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.DoesNotContain(document.Metadata.SummaryItems, item => item.Label == "GA-Ready Items");
    }

    [Fact]
    public void LiftingStatus_Blank_DoesNotDefaultToLiftable()
    {
        Assert.Equal("\u2014", LiftingResultReportDataProvider.MapLiftStatus(""));
    }

    [Fact]
    public async Task ReportBuilder_LiftingResult_SeparatesElementHookRequiredAndAvailableCapacities()
    {
        var builder = CreateBuilder();
        var columns = new[]
        {
            Column("No", "No.", ReportColumnValueKind.Number, 0),
            Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1),
            Column("Weight", "Element Weight (t)", ReportColumnValueKind.Tons, 2),
            Column("HookRiggingWeight", "Hook / Rigging Weight (t)", ReportColumnValueKind.Tons, 3),
            Column("RequiredCapacity", "Required Capacity (t)", ReportColumnValueKind.Tons, 4),
            Column("RawChartCapacity", "Raw Chart Capacity (t)", ReportColumnValueKind.Tons, 5),
            Column("AvailableCapacity", "Available Capacity After Derating/Safety (t)", ReportColumnValueKind.Tons, 6),
            Column("CapacityUtilizationPercent", "Capacity Utilization (%)", ReportColumnValueKind.Percentage, 7),
            Column("SafetyMargin", "Safety Margin (t)", ReportColumnValueKind.Tons, 8)
        };
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.LiftingResult,
            TemplateName = ReportTemplateCatalog.BasicLiftingResult,
            ItemsIncluded = "Optimization Result",
            Metadata = new ReportMetadataDto
            {
                ProjectName = "Test Project",
                HasGaRun = true,
                CraneInfo = new CraneReportInfoDto
                {
                    CraneName = "Crane A",
                    DeratingRatioText = "0.90",
                    SafetyFactorText = "0.95",
                    CapacityFactor = 0.855
                },
                LiftingAccessories = new LiftingAccessoriesInfoDto { AdditionalWeightTons = 0.70 }
            },
            SelectedColumns = columns,
            LiftingResults =
            [
                new LiftingResultSourceRowDto
                {
                    ElementId = "E1",
                    ElementMark = "MARK-001",
                    AssignedSetupNo = 1,
                    Radius = 12,
                    BoomLength = 24,
                    RequiredCapacity = 4.00,
                    AvailableCapacity = 8.55,
                    InternalLiftStatus = "Liftable",
                    IsFinalAssigned = true,
                    Values = new Dictionary<string, object?>
                    {
                        ["Weight"] = 3.30,
                        ["HookRiggingWeight"] = 0.70,
                        ["RawChartCapacity"] = 10.00,
                        ["DeratingRatio"] = 0.90,
                        ["SafetyFactor"] = 0.95
                    }
                }
            ]
        };

        var document = await builder.BuildAsync(request);
        var row = Assert.Single(document.Rows).Values;

        Assert.Equal(3.30, Assert.IsType<double>(row["Weight"]), precision: 3);
        Assert.Equal(0.70, Assert.IsType<double>(row["HookRiggingWeight"]), precision: 3);
        Assert.Equal(4.00, Assert.IsType<double>(row["RequiredCapacity"]), precision: 3);
        Assert.Equal(10.00, Assert.IsType<double>(row["RawChartCapacity"]), precision: 3);
        Assert.Equal(8.55, Assert.IsType<double>(row["AvailableCapacity"]), precision: 3);
        Assert.Equal(46.78, Assert.IsType<double>(row["CapacityUtilizationPercent"]), precision: 2);
        Assert.Equal(4.55, Assert.IsType<double>(row["SafetyMargin"]), precision: 2);
    }

    [Fact]
    public async Task ReportBuilder_DataSourceAudit_ExplainsElementAndGaSources()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.ReportDataSourceAudit);

        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = ReportTemplateCatalog.ReportDataSourceAudit,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", HasGaRun = false },
            SelectedColumns = template.Columns,
            SourceElements = [Element("E1", 3.30, IfcWeightTrustLevel.ExplicitQuantity)],
            LiftingResults = []
        };

        var document = await builder.BuildAsync(request);

        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Element Mark")
            && Equals(row.Values["SourceObjectOrFile"], "PrecastElement")
            && row.Values["SourceField"]?.ToString()?.Contains("PrecastElement.ElementMark", StringComparison.Ordinal) == true);
        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Material")
            && Equals(row.Values["SourceField"], "PrecastElement.ElementMaterial"));
        Assert.Contains(document.Rows, row =>
            row.Values["RequiredAction"]?.ToString()?.Contains("No GA source rows found", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(document.Rows, row =>
            row.Values["RequiredAction"]?.ToString()?.Contains("Required Capacity = Element Weight + Hook / Rigging Weight", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ReportBuilder_DataSourceAudit_ReportsPlacementResultSource_WhenMemoryGaRowsPopulateRequest()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.ReportDataSourceAudit);

        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = ReportTemplateCatalog.ReportDataSourceAudit,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", HasGaRun = true },
            SelectedColumns = template.Columns,
            SourceElements = [Element("E1", 3.30, IfcWeightTrustLevel.ExplicitQuantity)],
            LiftingResults =
            [
                new LiftingResultSourceRowDto
                {
                    SourceType = "PlacementResult / ElementLiftResult",
                    ElementId = "E1",
                    Values = new Dictionary<string, object?>
                    {
                        ["LiftStatus"] = "Liftable",
                        ["RequiredCapacity"] = 4.00,
                        ["RawChartCapacity"] = 10.00,
                        ["AvailableCapacity"] = 8.55,
                        ["SafetyMargin"] = 4.55,
                        ["AssignedGaPoint"] = "Point_1"
                    }
                }
            ]
        };

        var document = await builder.BuildAsync(request);

        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Lift Status")
            && Equals(row.Values["SourceObjectOrFile"], "PlacementResult / ElementLiftResult")
            && Equals(row.Values["RowsFound"], 1));
        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Current in-memory GA result present/missing")
            && Equals(row.Values["SourceObjectOrFile"], "PlacementResult / ElementLiftResult")
            && Equals(row.Values["CurrentAvailability"], "Available"));
    }

    [Fact]
    public async Task ReportBuilder_DataSourceAudit_ReportsDiagnosticsSource_WhenDiagnosticsRowsPopulateRequest()
    {
        var builder = CreateBuilder();
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.ReportDataSourceAudit);

        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.ElementList,
            TemplateName = ReportTemplateCatalog.ReportDataSourceAudit,
            ItemsIncluded = "All Elements",
            Metadata = new ReportMetadataDto { ProjectName = "Test Project", HasGaRun = true },
            SelectedColumns = template.Columns,
            SourceElements = [Element("E1", 3.30, IfcWeightTrustLevel.ExplicitQuantity)],
            LiftingResults =
            [
                new LiftingResultSourceRowDto
                {
                    SourceType = "GADiagnosticsReaderService / GAElementDiagnosticDto",
                    ElementId = "E1",
                    Values = new Dictionary<string, object?> { ["LiftStatus"] = "Liftable" }
                }
            ]
        };

        var document = await builder.BuildAsync(request);

        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Lift Status")
            && Equals(row.Values["SourceObjectOrFile"], "GADiagnosticsReaderService / GAElementDiagnosticDto"));
    }

    [Fact]
    public void GaDiagnosticsReader_ReadsLegacyCapacityAndRunSettingAliases()
    {
        var path = Path.Combine(_tempDir, "ga_audit_diagnostics.csv");
        File.WriteAllText(path,
            "Timestamp,ProjectName,IfcFileName,CraneNameOrId,ElementId,ElementName,ElementType,StoreyOrElevation,Weight,COG_X,COG_Y,COG_Z,AssignedSetupIndex,AssignedCraneX,AssignedCraneY,AssignedCraneZ,BestRadius,RequiredBoomLength,RequiredWeight,ChartCapacity,AvailableCapacity,AdditionalWt,DeratingRatio,SafetyFactor,WireLength,LiftStatus,IsLifted,FailureReason" + Environment.NewLine +
            "2026-06-25 10:00,Project,model.ifc,Crane A,E1,MARK-001,Footing,L1,3.30,0,0,0,0,1,2,0,12,24,4.00,10.00,8.55,0.70,0.90,0.95,30,Liftable,true," + Environment.NewLine);

        var rows = new GADiagnosticsReaderService().ReadDetailedDiagnostics(path);
        var row = Assert.Single(rows);

        Assert.Equal("E1", row.ElementId);
        Assert.Equal(4.00, row.RequiredCapacity, precision: 3);
        Assert.Equal(10.00, row.RawChartCapacity, precision: 3);
        Assert.Equal(8.55, row.AvailableCapacity, precision: 3);
        Assert.Equal(0.70, row.AdditionalWt, precision: 3);
        Assert.Equal(0.90, row.DeratingRatio, precision: 3);
        Assert.Equal(0.95, row.SafetyFactor, precision: 3);
        Assert.Equal(30.0, row.WireLength, precision: 3);
        Assert.Equal("Liftable", row.LiftStatus);
    }

    [Fact]
    public async Task LiftingRows_AssignedGaPoint_UsesFinalOwnerBeforeAssignedFields()
    {
        var provider = new LiftingResultReportDataProvider();

        var rows = provider.BuildRows(
            ReportTemplateCatalog.BasicLiftingResult,
            "Optimization Result",
            [
                new LiftingResultSourceRowDto
                {
                    ElementId = "e1",
                    ElementMark = "M1",
                    AssignedSetupNo = 3,
                    InternalLiftStatus = "Liftable",
                    IsFinalAssigned = true,
                    Values = new Dictionary<string, object?>
                    {
                        ["FinalOwnerPointId"] = "2",
                        ["AssignedPointId"] = "4",
                        ["AssignedPointIndex"] = 0
                    }
                }
            ]);

        Assert.Equal("Point_2", Assert.Single(rows).Values["AssignedGaPoint"]);
        await Task.CompletedTask;
    }

    [Fact]
    public void PdfColumnPlanner_SplitsWideReports()
    {
        var method = typeof(UserFacingReportPdfExporter).GetMethod(
            "BuildColumnGroups",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var columns = Enumerable.Range(0, 30)
            .Select(index => new ReportColumnDefinition
            {
                Key = index == 0 ? "No" : index == 1 ? "ElementMark" : $"Column{index}",
                Header = index == 0 ? "No." : index == 1 ? "Element Mark" : $"Very Wide Column {index}",
                ValueKind = index == 0 ? ReportColumnValueKind.Number : ReportColumnValueKind.Text,
                IsSelected = true,
                Order = index
            })
            .ToList();

        var groups = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyList<ReportColumnDefinition>>>(
            method.Invoke(null, [columns, false, 10d]));

        Assert.True(groups.Count > 1);
        Assert.All(groups, group => Assert.Contains(group, column => column.Key == "No"));
    }

    [Fact]
    public void PreviewViewModel_AllRowsMode_ReturnsAllRows()
    {
        var report = new ReportDocumentDto
        {
            Metadata = new ReportMetadataDto { ReportName = "Preview Test", ItemsIncluded = "All Elements" },
            Columns =
            [
                new ReportColumnDefinition { Key = "No", Header = "No.", ValueKind = ReportColumnValueKind.Number }
            ],
            Rows = Enumerable.Range(1, 75)
                .Select(index => new ReportDataRowDto
                {
                    Values = new Dictionary<string, object?> { ["No"] = index }
                })
                .ToList()
        };

        var viewModel = new CraneOptimizer.UI.Features.F12_Reports.ViewModels.ReportPreviewViewModel(
            report,
            ReportLayoutFitResult.Ok(0, 0),
            previewRowLimit: null,
            () => Task.CompletedTask,
            () => Task.CompletedTask);

        Assert.Equal("All 75", viewModel.PreviewRowsText);
        Assert.Equal(75, viewModel.PreviewRows.Count);
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
            new ElementReportDataProvider(resolver, new ImportReadinessService()),
            new LiftingResultReportDataProvider());
    }

    private static ReportBuildRequestDto ElementRequest(string templateName, string itemsIncluded, string? generalNotes)
    {
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, templateName);

        var allElements = new List<PrecastElement>
        {
            Element("e1", 10.0, IfcWeightTrustLevel.ExplicitQuantity),
            Element("e2", null, IfcWeightTrustLevel.Missing)
        };

        var scopedElements = itemsIncluded switch
        {
            "GA-Ready Elements" => allElements.Where(e => e.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity).ToList(),
            "Needs Review Elements" => allElements.Where(e => e.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity).ToList(),
            _ => allElements
        };

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
            SourceElements = scopedElements
        };
    }

    private static PrecastElement Element(
        string id,
        double? weight = 10.0,
        IfcWeightTrustLevel trustLevel = IfcWeightTrustLevel.ExplicitQuantity,
        string? parentId = null,
        bool? includeInGa = null,
        bool hasCog = true,
        AssemblyMarkConfidence markConfidence = AssemblyMarkConfidence.High,
        bool assemblyMarkIsExplicit = true,
        IfcElementRole role = IfcElementRole.LiftableElement)
    {
        var hasTrustedWeight = trustLevel is IfcWeightTrustLevel.ExplicitQuantity or IfcWeightTrustLevel.ExplicitProperty;

        return new PrecastElement
        {
            ElementID = id,
            ElementRevitId = $"revit-{id}",
            ElementMark = $"M-{id}",
            ElementType = "IfcWall",
            ElementMaterial = "Concrete",
            ParentGlobalId = parentId,
            Role = role,
            WeightTons = weight,
            ElementWT = weight,
            WeightTrustLevel = trustLevel,
            WeightSource = trustLevel.ToString(),
            VolumeM3 = 4.0,
            ElementCOG = hasCog ? new DomainPoint3D(1, 2, 3) : new DomainPoint3D(),
            COG_M = hasCog ? new DomainPoint3D(1, 2, 3) : new DomainPoint3D(),
            IncludeInGa = includeInGa ?? (hasTrustedWeight && weight is > 0),
            IncludeInRender = true,
            AssemblyMark = $"M-{id}",
            AssemblyMarkIsExplicit = assemblyMarkIsExplicit,
            AssemblyMarkConfidence = markConfidence
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

    private static ReportColumnDefinition Column(
        string key,
        string header,
        ReportColumnValueKind kind,
        int order) =>
        new()
        {
            Key = key,
            Header = header,
            ValueKind = kind,
            Order = order,
            IsSelected = true
        };
}
