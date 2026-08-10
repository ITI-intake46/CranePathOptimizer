using System.IO.Compression;
using System.Xml.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Reports.UserFacing;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers;
using UglyToad.PdfPig;

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

    [Fact]
    public async Task ExcelExporter_ReportInfo_UsesReportReferenceWithoutInternalIds()
    {
        var report = new ReportDocumentDto
        {
            DocumentId = "doc-internal-123",
            ReportBuildSessionId = "session-internal-456",
            SourceSignature = "source-signature-internal",
            InputSignature = "input-signature-internal",
            ReportReference = "ELR-20260712-0110-MODEL123-ABCD",
            InternalFamily = ReportFamily.ElementList,
            Metadata = new ReportMetadataDto
            {
                ReportName = "Basic Element List",
                ItemsIncluded = "All Elements",
                ProjectName = "Project Reference",
                ReportDate = new DateTime(2026, 7, 12, 1, 10, 0)
            },
            Columns =
            [
                new ReportColumnDefinition { Key = "No", Header = "No.", ValueKind = ReportColumnValueKind.Number, Order = 0, TextSize = "10" }
            ],
            Rows =
            [
                new ReportDataRowDto { Values = new Dictionary<string, object?> { ["No"] = 1 } }
            ]
        };

        var path = Path.Combine(_tempDir, "report-reference.xlsx");
        await new UserFacingReportExcelExporter().ExportAsync(report, path);

        using var archive = ZipFile.OpenRead(path);
        var infoSheet = ReadArchiveText(archive, "xl/worksheets/sheet1.xml");

        Assert.Contains("Report Reference", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Created On", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Generated On", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GA Run", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report.ReportReference, infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Document ID", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Build Session ID", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source Signature", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Input Signature", infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(report.DocumentId, infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(report.ReportBuildSessionId, infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(report.SourceSignature, infoSheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(report.InputSignature, infoSheet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfExporter_UsesCraneOptimizerBrandingAndCleanFooter()
    {
        var report = BuildGaMetadataReport();
        var path = Path.Combine(_tempDir, "clean-footer.pdf");

        await new UserFacingReportPdfExporter().ExportAsync(report, path);

        using var document = PdfDocument.Open(path);
        var text = string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));

        Assert.Contains("Crane Optimizer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mobile Crane Path Optimizer", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Report Reference", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report.ReportReference, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(text, report.ReportReference));
        Assert.Contains("Created On", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Generated On", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GA Run", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Historic", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Current", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crane Hoist Wire Length", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Estimated Weights", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Total Additional Weight", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Snapshot Completeness", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExcelExporter_UsesSeparatedWireRowsAndCleanSummary()
    {
        var report = BuildGaMetadataReport();
        var path = Path.Combine(_tempDir, "clean-ga.xlsx");

        await new UserFacingReportExcelExporter().ExportAsync(report, path);

        using var archive = ZipFile.OpenRead(path);
        var reportSummary = ReadArchiveText(archive, "xl/worksheets/sheet1.xml");
        var workbook = ReadArchiveText(archive, "xl/workbook.xml");

        Assert.Contains("Crane Optimizer", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mobile Crane Path Optimizer", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Estimated Weights", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Created On", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Generated On", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GA Run", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crane Hoist Wire Length", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("18.50 m", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failure Breakdown", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Overweight", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crane / Analysis Information", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lifting Accessories", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Wire Rope Length", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("7.25 m", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Total Lifting Accessories Weight", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.35 tons", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hook / Rigging Weight", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Total Below-Hook Weight", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Snapshot Completeness", reportSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Crane\"", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Lifting Accessories\"", workbook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfExporter_GaDetailTablesUseReadableColumnSet_WithMaterialTypeAndNoMaterialGrade()
    {
        var metadataReport = BuildGaMetadataReport();
        var report = new ReportDocumentDto
        {
            ReportReference = metadataReport.ReportReference,
            InternalFamily = metadataReport.InternalFamily,
            Metadata = metadataReport.Metadata,
            Columns = metadataReport.Columns,
            Rows =
            [
                new ReportDataRowDto
                {
                    Values = new Dictionary<string, object?>
                    {
                        ["No"] = 88,
                        ["ElementId"] = "LU-1",
                        ["ElementMark"] = "SGS-2LWP-40",
                        ["ElementName"] = "OutsideShell",
                        ["ElementType"] = "Wall",
                        ["MaterialType"] = "Concrete",
                        ["MaterialGrade"] = "C30",
                        ["Length"] = 3.1,
                        ["Width"] = 0.2,
                        ["Height"] = 5.0,
                        ["VolumeM3"] = 3.1,
                        ["Weight"] = 9.15,
                        ["CogX"] = 15.23,
                        ["CogY"] = 22.31,
                        ["CogZ"] = 11.06,
                        ["AssignedSetupNo"] = 1,
                        ["AssignedSetupIndex"] = 1,
                        ["LiftStatus"] = "Liftable",
                        ["IsLifted"] = "Yes",
                        ["RequiredRadius"] = 12.3,
                        ["SelectedChartRadius"] = 13.0,
                        ["RequiredBoomLength"] = 25.0,
                        ["SelectedChartBoomLength"] = 27.0,
                        ["RequiredCapacity"] = 9.15,
                        ["AvailableCapacity"] = 11.0,
                        ["RawChartCapacity"] = 12.5,
                        ["CapacityUtilizationPercent"] = 83.2
                    }
                },
                new ReportDataRowDto
                {
                    Values = new Dictionary<string, object?>
                    {
                        ["No"] = 120,
                        ["ElementId"] = "LU-2",
                        ["ElementMark"] = "SGS-4LWP-74",
                        ["ElementName"] = "OutsideShell",
                        ["ElementType"] = "Wall",
                        ["MaterialType"] = "Concrete",
                        ["MaterialGrade"] = "C30",
                        ["Length"] = 3.1,
                        ["Width"] = 0.2,
                        ["Height"] = 5.0,
                        ["VolumeM3"] = 3.1,
                        ["Weight"] = 9.15,
                        ["CogX"] = 15.23,
                        ["CogY"] = 22.31,
                        ["CogZ"] = 11.06,
                        ["AssignedSetupNo"] = 0,
                        ["AssignedSetupIndex"] = 0,
                        ["LiftStatus"] = "Overweight",
                        ["IsLifted"] = "No",
                        ["RequiredRadius"] = 12.3,
                        ["SelectedChartRadius"] = 13.0,
                        ["RequiredBoomLength"] = 25.0,
                        ["SelectedChartBoomLength"] = 27.0,
                        ["RequiredCapacity"] = 9.15,
                        ["AvailableCapacity"] = 8.0,
                        ["RawChartCapacity"] = 9.0,
                        ["CapacityUtilizationPercent"] = 114.4,
                        ["FailureCategory"] = "Overweight",
                        ["FailureReason"] = "Required capacity exceeds adjusted allowable capacity.",
                        ["SuggestedAction"] = "Use a larger crane or reduce element weight."
                    }
                }
            ],
            CraneSetups =
            [
                new CraneSetupReportRowDto
                {
                    SetupNo = 1,
                    SetupReference = "Point_1",
                    CraneX = 10,
                    CraneY = 20,
                    CraneZ = 0,
                    LiftedItemsCount = 1,
                    LiftedItemsPercentage = 100
                }
            ]
        };
        var path = Path.Combine(_tempDir, "readable-ga.pdf");

        await new UserFacingReportPdfExporter().ExportAsync(report, path);

        using var document = PdfDocument.Open(path);
        var text = string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));

        Assert.Contains("Material Type", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Concrete", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Material Grade", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C30", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Required Capacity", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Adjusted Allowable Capacity", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Manufacturer Chart Capacity", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Width (m)", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Height (m)", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Volume (m", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadArchiveText(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string? ReadCellValue(XDocument document, string reference)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cell = document
            .Descendants(ns + "c")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("r"), reference, StringComparison.OrdinalIgnoreCase));

        if (cell == null)
            return null;

        return cell.Element(ns + "v")?.Value
            ?? cell.Descendants(ns + "t").FirstOrDefault()?.Value;
    }

    private static ReportDataRowDto GaReportRow(
        int displayNo,
        string liftUnitId,
        string elementMark,
        int setupNo,
        bool isLifted)
    {
        return new ReportDataRowDto
        {
            Values = new Dictionary<string, object?>
            {
                ["No"] = displayNo,
                ["ElementId"] = liftUnitId,
                ["LiftUnitId"] = liftUnitId,
                ["ElementMark"] = elementMark,
                ["ElementName"] = "Unit",
                ["ElementType"] = "Wall",
                ["MaterialType"] = "Concrete",
                ["MaterialGrade"] = "C35",
                ["Weight"] = 1.0,
                ["AssignedSetupNo"] = setupNo,
                ["AssignedSetupIndex"] = setupNo,
                ["LiftStatus"] = isLifted ? "Liftable" : "Overweight",
                ["IsLifted"] = isLifted ? "Yes" : "No",
                ["FailureCategory"] = isLifted ? "\u2014" : "Overweight"
            }
        };
    }

    private static ReportDocumentDto BuildGaMetadataReport() => new()
    {
        ReportReference = "GAR-20260713-0102-RUN12345-ABCD",
        InternalFamily = ReportFamily.LiftingResult,
        Metadata = new ReportMetadataDto
        {
            ReportName = "GA Result Report",
            ItemsIncluded = "All Submitted Lift Units",
            ProjectName = "Project Metadata",
            ReportDate = new DateTime(2026, 7, 13, 1, 2, 0),
            SummaryItems =
            [
                new ReportSummaryItemDto { Label = "Total Items", Value = "357" },
                new ReportSummaryItemDto { Label = "Liftable", Value = "295 (82.6%)" },
                new ReportSummaryItemDto { Label = "Not Lifted", Value = "62 (17.4%)" },
                new ReportSummaryItemDto { Label = "Final Setup Count", Value = "3" },
                new ReportSummaryItemDto { Label = "Average Capacity Utilization", Value = "70.0%" },
                new ReportSummaryItemDto { Label = "Maximum Capacity Utilization", Value = "99.5%" }
            ],
            FailureBreakdownItems =
            [
                new ReportSummaryItemDto { Label = "Overweight", Value = "62 (17.4%)" }
            ],
            CraneInfo = new CraneReportInfoDto
            {
                CraneName = "Saved crane",
                ModelName = "AC 300",
                SpecificationName = "Spec A",
                Manufacturer = "Maker",
                CraneType = "Mobile",
                CounterweightTons = 80,
                OutriggerConfiguration = "Full",
                WorkingRadiusRangeText = "10 - 30 m",
                BoomLengthRangeText = "20 - 60 m",
                MaximumCapacityTons = 300,
                SafetyFactorText = "1.10",
                DeratingRatioText = "0.90",
                CraneHoistWireLengthText = "18.50 m"
            },
            LiftingAccessories = new LiftingAccessoriesInfoDto
            {
                RiggingWireRopeLengthM = 7.25,
                TotalLiftingAccessoriesWeightTons = 1.35,
                HookRiggingWeightTons = 9.99,
                TotalBelowHookWeightTons = 9.99,
                Completeness = "AggregateOnly"
            }
        },
        Columns = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.GaResultReport).Columns,
        Rows =
        [
            new ReportDataRowDto { Values = new Dictionary<string, object?> { ["No"] = 1, ["LiftStatus"] = "Liftable" } }
        ]
    };

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
    public void LiftingResultProvider_RequiredBoomLength_UsesRequiredBoomNotGenericBoom()
    {
        var provider = new LiftingResultReportDataProvider();
        var rows = provider.BuildRows(
            ReportTemplateCatalog.GaResultReport,
            "Optimization Result",
            [
                new LiftingResultSourceRowDto
                {
                    ElementId = "lift-1",
                    ElementMark = "M-1",
                    BoomLength = 44,
                    RequiredCapacity = 5,
                    AvailableCapacity = 10,
                    InternalLiftStatus = "Liftable",
                    Values = new Dictionary<string, object?>
                    {
                        ["RequiredBoomLength"] = 31.5,
                        ["SelectedChartBoomLength"] = 36.0,
                        ["BestFailedRequiredBoomLength"] = 29.0
                    }
                }
            ]);

        var row = Assert.Single(rows);
        Assert.Equal(31.5, row.RequiredBoomLengthM);
        Assert.Equal(31.5, row.Values["RequiredBoomLength"]);
        Assert.Equal(36.0, row.Values["SelectedChartBoomLength"]);
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
    public void BasicElementList_UsesCanonicalLiftUnitsAndExcludesAnomalies()
    {
        var elements = new[]
        {
            CanonicalFixture("SGS-PC1-37", "SGS-PC1-37", "COLUMN", "Column", "C35", 0.50, 0.70, 12.42, 4.50, 11.15, new DomainPoint3D(5.64, 22.52, 6.78)),
            CanonicalFixture("SGS-HC20-29", "SGS-HC20-29", "HOLLOWCORE", "Slab", "C30", 1.96, 0.48, 0.20, 0.50, 0.89, new DomainPoint3D(1, 2, 3)),
            new PrecastElement
            {
                ElementID = "INVALID-1",
                ElementMark = "INVALID-1",
                ElementName = "InsideShell",
                ElementType = "Unknown",
                ElementMaterial = "Unknown",
                IncludeInGa = false
            }
        };

        var snapshot = CanonicalLiftUnitSnapshotBuilder.BuildCurrentSnapshot(elements);
        var provider = new ElementReportDataProvider(new ReportableElementResolver(), new ImportReadinessService());
        var rows = provider.BuildRows(ReportTemplateCatalog.BasicElementList, elements);

        Assert.Equal(2, snapshot.CanonicalLiftUnits.Count);
        Assert.Single(snapshot.ImportAnomalies);
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, row => row.ElementId == "INVALID-1");
    }

    [Fact]
    public void BasicElementList_CogMatchesGaPreparedAssemblyRollup_ForGroupedAssembly()
    {
        var parent = Element("ASM-LWP-40", 12.0, IfcWeightTrustLevel.ExplicitQuantity, role: IfcElementRole.LiftAssembly);
        parent.ElementMark = "SGS-4LWP-40";
        parent.ElementName = "InsideShell";
        parent.ElementType = "Wall";
        parent.MainPartGlobalId = "MEM-B";
        parent.ChildGlobalIds = ["MEM-B", "MEM-A", "REBAR-1"];
        parent.AssemblyLiftWeight = 12.0;
        parent.AssemblyLiftCOG = new DomainPoint3D(99, 99, 99);

        var memberB = Element("MEM-B", 10.0, IfcWeightTrustLevel.ExplicitQuantity, parent.ElementID);
        memberB.ElementCOG = new DomainPoint3D(21.0, 20.0, 11.0);
        memberB.COG_M = memberB.ElementCOG;
        memberB.ElementName = "InsideShell";
        memberB.RawIfcType = "IfcWall";

        var memberA = Element("MEM-A", 2.0, IfcWeightTrustLevel.ExplicitQuantity, parent.ElementID);
        memberA.ElementCOG = new DomainPoint3D(26.0, 20.6, 11.4);
        memberA.COG_M = memberA.ElementCOG;
        memberA.ElementName = "OutsideShell";
        memberA.RawIfcType = "IfcWall";

        var rebar = Element("REBAR-1", 0.1, IfcWeightTrustLevel.ExplicitQuantity, parent.ElementID, includeInGa: false, hasCog: false, role: IfcElementRole.InternalAuxiliary);
        rebar.ElementName = "REBAR";
        rebar.RawIfcType = "IfcReinforcingBar";
        rebar.PhysicalCOGAvailable = false;

        var allElements = new[] { parent, memberB, memberA, rebar };
        var gaChildren = new List<PrecastElement> { memberB.CreateCopy(), memberA.CreateCopy() };
        AssemblyRollupService.ReconstructAssemblyClassifications(gaChildren);
        var gaLiftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent.CreateCopy(), gaChildren, out _);

        var row = Assert.Single(new ElementReportDataProvider(new ReportableElementResolver(), new ImportReadinessService())
            .BuildRows(ReportTemplateCatalog.BasicElementList, allElements));

        Assert.Equal(parent.ElementID, row.ElementId);
        Assert.Equal(["MEM-B", "MEM-A"], gaChildren.Select(child => child.ElementID).ToArray());
        Assert.Equal(gaLiftUnit.ElementCOG.X, row.CogX);
        Assert.Equal(gaLiftUnit.ElementCOG.Y, row.CogY);
        Assert.Equal(gaLiftUnit.ElementCOG.Z, row.CogZ);
        Assert.NotEqual(parent.AssemblyLiftCOG.Y, row.CogY);
        Assert.DoesNotContain("REBAR-1", gaChildren.Select(child => child.ElementID));
    }

    [Fact]
    public void BasicElementList_GaEquivalentCogCalculation_DoesNotMutateSourceElements()
    {
        var parent = Element("ASM-LWP-40", 12.0, IfcWeightTrustLevel.ExplicitQuantity, role: IfcElementRole.LiftAssembly);
        parent.MainPartGlobalId = "MEM-B";
        parent.ChildGlobalIds = ["MEM-B", "MEM-A"];
        parent.AssemblyLiftWeight = 12.0;
        parent.AssemblyLiftCOG = new DomainPoint3D(99, 99, 99);

        var memberB = Element("MEM-B", 10.0, IfcWeightTrustLevel.ExplicitQuantity, parent.ElementID);
        memberB.ElementCOG = new DomainPoint3D(21.0, 20.0, 11.0);
        memberB.COG_M = memberB.ElementCOG;

        var memberA = Element("MEM-A", 2.0, IfcWeightTrustLevel.ExplicitQuantity, parent.ElementID);
        memberA.ElementCOG = new DomainPoint3D(26.0, 20.6, 11.4);
        memberA.COG_M = memberA.ElementCOG;

        var originalParentCog = parent.AssemblyLiftCOG;
        var originalChildIds = parent.ChildGlobalIds.ToArray();
        var originalMemberBCog = memberB.ElementCOG;
        var originalMemberACog = memberA.ElementCOG;

        _ = new ElementReportDataProvider(new ReportableElementResolver(), new ImportReadinessService())
            .BuildRows(ReportTemplateCatalog.BasicElementList, [parent, memberB, memberA]);

        Assert.Same(originalParentCog, parent.AssemblyLiftCOG);
        Assert.Equal(originalChildIds, parent.ChildGlobalIds);
        Assert.Same(originalMemberBCog, memberB.ElementCOG);
        Assert.Same(originalMemberACog, memberA.ElementCOG);
        Assert.True(parent.IncludeInGa);
        Assert.True(memberB.IncludeInGa);
        Assert.True(memberA.IncludeInGa);
    }

    [Fact]
    public void SharedFieldsMatchBetweenBasicSnapshotAndGaResultByLiftUnitId()
    {
        var element = CanonicalFixture("SGS-PC1-37", "SGS-PC1-37", "COLUMN", "Column", "C35", 0.50, 0.70, 12.42, 4.50, 11.15, new DomainPoint3D(5.64, 22.52, 6.78));
        var provider = new ElementReportDataProvider(new ReportableElementResolver(), new ImportReadinessService());
        var basicRow = Assert.Single(provider.BuildRows(ReportTemplateCatalog.BasicElementList, [element]));
        var canonical = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([element]));
        var gaRow = Assert.Single(new GaSnapshotReportAdapter().BuildRows(
            BuildSingleLiftSnapshot(canonical),
            ReportTemplateCatalog.GaResultReport,
            "All Submitted Lift Units"));

        Assert.Equal(basicRow.ElementId, canonical.LiftUnitId);
        Assert.Equal(basicRow.ElementId, gaRow.ElementId);
        Assert.Equal(basicRow.ElementMark, gaRow.Values["ElementMark"]);
        Assert.Equal(basicRow.ElementName, gaRow.Values["ElementName"]);
        Assert.Equal(basicRow.ElementType, gaRow.Values["ElementType"]);
        Assert.Equal(basicRow.MaterialType, gaRow.Values["MaterialType"]);
        Assert.Equal(basicRow.MaterialGrade, gaRow.Values["MaterialGrade"]);
        Assert.Equal(basicRow.Length, gaRow.Values["Length"]);
        Assert.Equal(basicRow.Width, gaRow.Values["Width"]);
        Assert.Equal(basicRow.Height, gaRow.Values["Height"]);
        Assert.Equal(basicRow.VolumeM3, gaRow.Values["VolumeM3"]);
        Assert.Equal(basicRow.WeightTons, gaRow.Values["Weight"]);
        Assert.Equal(basicRow.CogX, gaRow.Values["CogX"]);
        Assert.Equal(basicRow.CogY, gaRow.Values["CogY"]);
        Assert.Equal(basicRow.CogZ, gaRow.Values["CogZ"]);
        Assert.Equal(canonical.SharedDataHash, gaRow.Values["SharedDataHash"]);
    }

    [Theory]
    [InlineData("Unknown", "Wall", "", "", "Wall")]
    [InlineData("Unknown", "", "InsideShell", "SGS-3LWP-85", "Wall")]
    [InlineData("Unknown", "", "HOLLOWCORE", "", "Slab")]
    [InlineData("Unknown", "", "TEE SLAB", "", "Slab")]
    [InlineData("Unknown", "", "BEAM_ROOF", "", "Beam")]
    [InlineData("Unknown", "", "COLUMN", "", "Column")]
    public void CanonicalElementTypeResolver_IgnoresUnknownPlaceholder(
        string elementType,
        string resolvedCategory,
        string elementName,
        string elementMark,
        string expected)
    {
        var element = CanonicalFixture("TYPE-1", "TYPE-1", "Fallback", "Column", "C35", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));
        element.ElementType = elementType;
        element.ResolvedCategory = resolvedCategory;
        element.ElementName = elementName;
        element.ElementMark = string.IsNullOrWhiteSpace(elementMark) ? "TYPE-1" : elementMark;

        var unit = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([element]));

        Assert.Equal(expected, unit.ElementType);
    }

    [Fact]
    public void CanonicalSharedFieldMerge_DoesNotLetUnknownBlockAuthoritativeSource()
    {
        var target = CanonicalFixture("MERGE-1", "MERGE-1", "Unknown", "Unknown", "Unknown", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));
        target.ElementType = "Unknown";
        target.ElementName = "Unknown";
        target.ElementMaterial = "Unknown";
        var source = CanonicalFixture("MERGE-1", "MERGE-1", "InsideShell", "Wall", "Concrete C35", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));

        var unit = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([target], [source]));

        Assert.Equal("Wall", unit.ElementType);
        Assert.Equal("InsideShell", unit.ElementName);
        Assert.Equal("Concrete", unit.MaterialType);
    }

    [Fact]
    public void CanonicalCog_UsesPhysicalLiftCog_NotGeometryCenter()
    {
        var parent = CanonicalFixture("ASM-1", "SGS-PC1-37", "COLUMN", "Column", "Concrete", 0.50, 0.70, 12.42, 4.50, 11.15, new DomainPoint3D(6.04, 21.45, 6.39));
        parent.Role = IfcElementRole.LiftAssembly;
        parent.MainPartGlobalId = "MAIN-1";
        parent.ChildGlobalIds = ["MAIN-1"];
        parent.AssemblyMemberIds = ["MAIN-1"];
        parent.PhysicalCOGAvailable = true;
        parent.COG_M = new DomainPoint3D(6.04, 21.45, 6.39);

        var mainPart = CanonicalFixture("MAIN-1", "SGS-PC1-37", "COLUMN", "Column", "Concrete C35", 0.50, 0.70, 12.42, 4.50, 11.15, new DomainPoint3D(5.91, 22.13, 6.78));
        mainPart.ParentGlobalId = "ASM-1";
        mainPart.PhysicalCOGAvailable = true;

        var unit = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([parent, mainPart]));

        Assert.Equal(5.91, unit.Cog.PhysicalCogProjectM.X, 2);
        Assert.Equal(22.13, unit.Cog.PhysicalCogProjectM.Y, 2);
        Assert.Equal(6.78, unit.Cog.PhysicalCogProjectM.Z, 2);
        Assert.Equal(unit.Cog.PhysicalCogProjectM.X, unit.LiftCogLocal?.X);
        Assert.Equal("Concrete", unit.MaterialType);
        Assert.Equal("C35", unit.MaterialGrade);
    }

    [Fact]
    public void CanonicalType_UsesEngineeringTypeInsteadOfGenericIfcType()
    {
        var element = CanonicalFixture("SGS-BCAN-3", "SGS-BCAN-3", "BCAN", "IfcBuildingElementPart", "Concrete C35", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));
        element.RawIfcType = "IfcBuildingElementPart";
        element.ResolvedCategory = string.Empty;

        var unit = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([element]));

        Assert.Equal("Slab", unit.ElementType);
    }

    [Fact]
    public void CanonicalMaterial_PrefersSpecificMemberMaterialOverMiscellaneousParent()
    {
        var parent = CanonicalFixture("PAD-ROOF-ASM", "PAD ROOF", "PAD ROOF", "Roof", "MISCELLANEOUS", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));
        parent.Role = IfcElementRole.LiftAssembly;
        parent.MainPartGlobalId = "PAD-ROOF-MAIN";
        parent.ChildGlobalIds = ["PAD-ROOF-MAIN"];
        parent.AssemblyMemberIds = ["PAD-ROOF-MAIN"];

        var mainPart = CanonicalFixture("PAD-ROOF-MAIN", "PAD ROOF", "PAD ROOF", "Roof", "Rubber", 1, 1, 1, 1, 1, new DomainPoint3D(1, 2, 3));
        mainPart.ParentGlobalId = "PAD-ROOF-ASM";

        var unit = Assert.Single(CanonicalLiftUnitSnapshotBuilder.BuildCanonicalLiftUnits([parent, mainPart]));

        Assert.Equal("Rubber", unit.MaterialType);
        Assert.Equal("\u2014", unit.MaterialGrade);
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
        var request = ElementRequest(ReportTemplateCatalog.BasicElementList, "All Elements", generalNotes: null);

        var document = await builder.BuildAsync(request);

        Assert.NotEmpty(document.Rows);
        Assert.Equal(
            Enumerable.Range(1, document.Rows.Count).Cast<object>(),
            document.Rows.Select(row => row.Values["No"]));
    }

    [Fact]
    public void ReportTemplateCatalog_LiftingAvailableColumns_ExposeOnlyNormalGaResultColumns()
    {
        var columns = ReportTemplateCatalog.GetAvailableColumns(ReportFamily.LiftingResult);
        var keys = columns.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("RequiredCapacity", keys);
        Assert.Contains("AvailableCapacity", keys);
        Assert.DoesNotContain("GARunTimestamp", keys);
        Assert.DoesNotContain("SelectedVisualColumnHeader", keys);
        Assert.DoesNotContain("BestFailedSafetyMargin", keys);
        Assert.DoesNotContain("SafetyMargin", keys);
        Assert.DoesNotContain("ChartStatus", keys);
        Assert.Equal([ReportTemplateCatalog.GaResultReport], ReportTemplateCatalog.GetBuiltInTemplates(ReportFamily.LiftingResult).Select(template => template.Name));
    }

    [Fact]
    public void ReportTemplateCatalog_UserFacingTemplates_ExposeOnlyElementListAndGaResultReport()
    {
        var elementTemplates = ReportTemplateCatalog.GetUserFacingTemplates(ReportFamily.ElementList);
        var liftingTemplates = ReportTemplateCatalog.GetUserFacingTemplates(ReportFamily.LiftingResult);

        Assert.Equal([ReportTemplateCatalog.BasicElementList], elementTemplates.Select(template => template.Name));
        Assert.Equal([ReportTemplateCatalog.GaResultReport], liftingTemplates.Select(template => template.Name));
        Assert.DoesNotContain(elementTemplates, template => template.Name == "GA Readiness");
    }

    [Fact]
    public void ReportTemplateCatalog_BasicElementList_DefaultColumns_AreCanonicalUserFacingColumns()
    {
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.ElementList, ReportTemplateCatalog.BasicElementList);
        var selected = template.Columns.Where(column => column.IsSelected).ToList();
        var selectedKeys = selected.Select(column => column.Key).ToList();
        var headersByKey = selected.ToDictionary(column => column.Key, column => column.Header, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            [
                "No",
                "ElementMark",
                "ElementName",
                "ElementType",
                "MaterialType",
                "MaterialGrade",
                "Length",
                "Width",
                "Height",
                "VolumeM3",
                "WeightTons",
                "CogX",
                "CogY",
                "CogZ",
                "IncludedInAnalysis",
                "AnalysisExclusionReason"
            ],
            selectedKeys);
        Assert.Equal("Material Type", headersByKey["MaterialType"]);
        Assert.Equal("Material Grade", headersByKey["MaterialGrade"]);
        Assert.Equal("Length (m)", headersByKey["Length"]);
        Assert.Equal("Width (m)", headersByKey["Width"]);
        Assert.Equal("Height (m)", headersByKey["Height"]);
        Assert.Equal("Volume (m\u00b3)", headersByKey["VolumeM3"]);
        Assert.Equal("Weight", headersByKey["WeightTons"]);
        Assert.Equal("Included in Analysis", headersByKey["IncludedInAnalysis"]);
        Assert.Equal("Analysis Exclusion Reason", headersByKey["AnalysisExclusionReason"]);
        Assert.Equal("COG X (m)", headersByKey["CogX"]);
        Assert.Equal("COG Y (m)", headersByKey["CogY"]);
        Assert.Equal("COG Z (m)", headersByKey["CogZ"]);
        Assert.DoesNotContain("ReadyForGa", selectedKeys);
        Assert.DoesNotContain("AssemblyMemberCount", selectedKeys);
        Assert.DoesNotContain("IncludedInCraneOptimization", selectedKeys);
        Assert.DoesNotContain("ValidationStatus", selectedKeys);
        Assert.DoesNotContain("BlockingReasons", selectedKeys);

        var availableKeys = ReportTemplateCatalog.GetAvailableColumns(ReportFamily.ElementList)
            .Select(column => column.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("VolumeSource", availableKeys);
        Assert.DoesNotContain("SourceRevitId", availableKeys);
        Assert.DoesNotContain("MarkConfidence", availableKeys);
        Assert.DoesNotContain("TopLevelElevation", availableKeys);
        Assert.DoesNotContain("BottomLevelElevation", availableKeys);
        Assert.DoesNotContain("ValidationWarnings", availableKeys);
        Assert.DoesNotContain("WeightSource", availableKeys);
        Assert.DoesNotContain("WeightStatus", availableKeys);
        Assert.DoesNotContain("WeightTrustLevel", availableKeys);
        Assert.DoesNotContain("WeightVerified", availableKeys);
        Assert.DoesNotContain("WeightWarning", availableKeys);
        Assert.DoesNotContain("AssemblyMemberCount", availableKeys);
        Assert.DoesNotContain("IncludedInCraneOptimization", availableKeys);
        Assert.DoesNotContain("ValidationStatus", availableKeys);
        Assert.DoesNotContain("BlockingReasons", availableKeys);
    }

    [Fact]
    public void ReportTemplateCatalog_GaResultReport_DefaultColumns_DoNotExposeInternalTechnicalColumns()
    {
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.GaResultReport);
        var selected = template.Columns.Where(column => column.IsSelected).ToList();
        var selectedKeysInOrder = selected.Select(column => column.Key).ToList();
        var selectedKeys = selectedKeysInOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var headersByKey = selected.ToDictionary(column => column.Key, column => column.Header, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            [
                "No",
                "ElementMark",
                "ElementName",
                "ElementType",
                "MaterialType",
                "Weight",
                "CogX",
                "CogY",
                "CogZ",
                "RequiredRadius",
                "SelectedChartRadius",
                "RequiredBoomLength",
                "SelectedChartBoomLength",
                "RequiredCapacity",
                "AvailableCapacity",
                "RawChartCapacity",
                "CapacityUtilizationPercent",
                "FailureCategory",
                "FailureReason",
                "SuggestedAction"
            ],
            selectedKeysInOrder);
        Assert.Contains("RequiredBoomLength", selectedKeys);
        Assert.Equal("Material Type", headersByKey["MaterialType"]);
        Assert.DoesNotContain("MaterialGrade", selectedKeys);
        Assert.DoesNotContain("Length", selectedKeys);
        Assert.DoesNotContain("Width", selectedKeys);
        Assert.DoesNotContain("Height", selectedKeys);
        Assert.DoesNotContain("VolumeM3", selectedKeys);
        Assert.Equal("Weight", headersByKey["Weight"]);
        Assert.Equal("COG X (m)", headersByKey["CogX"]);
        Assert.Equal("Required Radius (m)", headersByKey["RequiredRadius"]);
        Assert.Equal("Adjusted Allowable Capacity (t)", headersByKey["AvailableCapacity"]);
        Assert.Equal("Manufacturer Chart Capacity (t)", headersByKey["RawChartCapacity"]);
        Assert.Equal("Failure Category", headersByKey["FailureCategory"]);
        Assert.DoesNotContain("LiftStatus", selectedKeys);
        Assert.DoesNotContain("HookRiggingWeight", selectedKeys);
        Assert.DoesNotContain("ReachStatus", selectedKeys);

        foreach (var internalKey in new[]
        {
            "ReadyForGa",
            "AssemblyMemberCount",
            "AssignedSetupIndex",
            "AssignedCraneX",
            "AssignedCraneY",
            "AssignedCraneZ",
            "SafetyMargin",
            "ChartStatus",
            "AssignmentReason",
            "AssignmentScore",
            "FinalEvaluationId",
            "BestFailedEvaluationId",
            "ReportReference",
            "LiftUnitId",
            "SourceElementGlobalId",
            "ParentAssemblyGlobalId",
            "MainPartGlobalId",
            "AssemblyMemberIds"
        })
        {
        Assert.DoesNotContain(internalKey, selectedKeys);
        }
    }

    [Fact]
    public void LiftingResultProvider_GaResultReport_DoesNotFilterOutSubmittedRows()
    {
        var provider = new LiftingResultReportDataProvider();
        var rows = provider.BuildRows(
            ReportTemplateCatalog.GaResultReport,
            "Failed Items Only",
            [
                new LiftingResultSourceRowDto { ElementId = "lifted", InternalLiftStatus = "Liftable", IsFinalAssigned = true },
                new LiftingResultSourceRowDto { ElementId = "failed", InternalLiftStatus = "OutOfRadius", IsFinalAssigned = false }
            ]);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => Equals("lifted", row.Values["ElementId"]));
        Assert.Contains(rows, row => Equals("failed", row.Values["ElementId"]));
    }

    [Fact]
    public void ReportTemplateCatalog_ElementListAvailableColumns_DoNotExposePostGaFields()
    {
        var columns = ReportTemplateCatalog.GetAvailableColumns(ReportFamily.ElementList);
        var keys = columns.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("LiftStatus", keys);
        Assert.DoesNotContain("IsLifted", keys);
        Assert.DoesNotContain("FailureReason", keys);
        Assert.DoesNotContain("RequiredRadius", keys);
        Assert.DoesNotContain("SelectedChartRadius", keys);
        Assert.DoesNotContain("RequiredBoomLength", keys);
        Assert.DoesNotContain("SelectedChartBoomLength", keys);
        Assert.DoesNotContain("RequiredCapacity", keys);
        Assert.DoesNotContain("RawChartCapacity", keys);
        Assert.DoesNotContain("AvailableCapacity", keys);
        Assert.DoesNotContain("CapacityUtilizationPercent", keys);
        Assert.DoesNotContain("SafetyMargin", keys);
        Assert.DoesNotContain("AssignedSetupIndex", keys);
        Assert.DoesNotContain("FinalOwnerPointId", keys);
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
                    ElementMark = "FOOTING-A",
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
                    IncludeInGa = true,
                    ElementCOG = new DomainPoint3D(1, 2, 3),
                    COG_M = new DomainPoint3D(1, 2, 3),
                    PhysicalCOGAvailable = true
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
                    IncludeInGa = true,
                    ElementCOG = new DomainPoint3D(1, 2, 3),
                    COG_M = new DomainPoint3D(1, 2, 3),
                    PhysicalCOGAvailable = true
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
                    Key = "IncludedInAnalysis",
                    Header = "Included in Analysis",
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
                    IncludeInGa = true,
                    ElementCOG = new DomainPoint3D(1, 2, 3),
                    COG_M = new DomainPoint3D(1, 2, 3),
                    PhysicalCOGAvailable = true
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
        Assert.Equal("Yes", row["IncludedInAnalysis"]);
        Assert.Equal("\u2014", row["AnalysisExclusionReason"]);
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
        Assert.Equal("Canonical resolved geometry", row["VolumeSource"]);
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
    public async Task ReportBuilder_GaResultSummary_UsesRequestedFailureBreakdown()
    {
        var builder = CreateBuilder();
        var columns = new[]
        {
            Column("No", "No.", ReportColumnValueKind.Number, 0),
            Column("ElementMark", "Element Mark", ReportColumnValueKind.Text, 1),
            Column("LiftStatus", "Lift Status", ReportColumnValueKind.Text, 2),
            Column("CapacityUtilizationPercent", "Capacity Utilization (%)", ReportColumnValueKind.Percentage, 3)
        };
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.LiftingResult,
            TemplateName = ReportTemplateCatalog.GaResultReport,
            ItemsIncluded = "All Submitted Lift Units",
            Metadata = new ReportMetadataDto
            {
                ProjectName = "Test Project",
                HasGaRun = true
            },
            SelectedColumns = columns,
            CraneSetups =
            [
                new CraneSetupReportRowDto
                {
                    SetupNo = 1,
                    SetupReference = "Point_1",
                    LiftedItemsCount = 2,
                    LiftedItemsPercentage = 50
                }
            ],
            LiftingResults =
            [
                LiftSummaryRow("E1", "M1", "Liftable", 5, 10),
                LiftSummaryRow("E2", "M2", "Liftable", 8, 10),
                LiftSummaryRow("E3", "M3", "OutOfRadius", 6, 10),
                LiftSummaryRow("E4", "M4", "Overweight", 12, 10)
            ]
        };

        var document = await builder.BuildAsync(request);
        var labels = document.Metadata.SummaryItems.Select(item => item.Label).ToList();

        Assert.Equal(
            [
                "Total Items",
                "Liftable",
                "Not Lifted",
                "Final Setup Count",
                "Average Capacity Utilization",
                "Maximum Capacity Utilization"
            ],
            labels);
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Total Items" && item.Value == "4");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Liftable" && item.Value == "2 (50.0%)");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Not Lifted" && item.Value == "2 (50.0%)");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Final Setup Count" && item.Value == "1");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Average Capacity Utilization" && item.Value == "77.5%");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Maximum Capacity Utilization" && item.Value == "120.0%");
        Assert.DoesNotContain(document.Metadata.SummaryItems, item => item.Label == "Failure Breakdown");
        Assert.Contains(document.Metadata.FailureBreakdownItems, item => item.Label == "Out of Radius" && item.Value == "1 (25.0%)");
        Assert.Contains(document.Metadata.FailureBreakdownItems, item => item.Label == "Overweight" && item.Value == "1 (25.0%)");
        Assert.DoesNotContain(document.Metadata.FailureBreakdownItems, item => item.Label == "Height Failure");
        Assert.DoesNotContain(document.Metadata.FailureBreakdownItems, item => item.Label == "Missing Load Chart Coverage");
        Assert.DoesNotContain(document.Metadata.FailureBreakdownItems, item => item.Label == "No Chart Data");
    }

    [Fact]
    public void LiftingStatus_Blank_DoesNotDefaultToLiftable()
    {
        Assert.Equal("\u2014", LiftingResultReportDataProvider.MapLiftStatus(""));
    }

    [Theory]
    [InlineData("No Chart Data")]
    [InlineData("NotCharted")]
    [InlineData("MissingLoadChartData")]
    public void LiftingStatus_MissingChartCoverage_UsesUserFacingLabel(string rawStatus)
    {
        Assert.Equal("Missing Load Chart Coverage", LiftingResultReportDataProvider.MapLiftStatus(rawStatus));
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
            Column("CapacityUtilizationPercent", "Capacity Utilization (%)", ReportColumnValueKind.Percentage, 7)
        };
        var request = new ReportBuildRequestDto
        {
            Family = ReportFamily.LiftingResult,
            TemplateName = ReportTemplateCatalog.GaResultReport,
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
        Assert.DoesNotContain("SafetyMargin", row.Keys);
        Assert.DoesNotContain("ChartStatus", row.Keys);
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
            Equals(row.Values["ReportColumn"], "Material Type")
            && row.Values["SourceField"]?.ToString()?.Contains("canonical material category", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(document.Rows, row =>
            Equals(row.Values["ReportColumn"], "Material Grade")
            && row.Values["SourceField"]?.ToString()?.Contains("canonical material grade", StringComparison.OrdinalIgnoreCase) == true);
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
            ReportTemplateCatalog.GaResultReport,
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
    public async Task PdfExporter_WideReports_UseSingleFullWidthTableWithoutHorizontalBands()
    {
        var columns = Enumerable.Range(0, 30)
            .Select(index => new ReportColumnDefinition
            {
                Key = index == 0 ? "No" : index == 1 ? "ElementMark" : $"Column{index}",
                Header = index == 0 ? "No." : index == 1 ? "Element Mark" : $"Very Wide Column {index}",
                ValueKind = index == 0 ? ReportColumnValueKind.Number : ReportColumnValueKind.Text,
                IsSelected = true,
                Order = index,
                TextSize = "8"
            })
            .ToList();

        var report = new ReportDocumentDto
        {
            DocumentId = "doc-wide-report-1234567890abcdef",
            ReportBuildSessionId = "session-wide-report",
            SourceSignature = "source-wide-report",
            InputSignature = "input-wide-report",
            ReportReference = "GAR-20260712-0110-RUNWIDE1-7F2C",
            InternalFamily = ReportFamily.LiftingResult,
            TextSizePt = 8,
            Metadata = new ReportMetadataDto
            {
                ReportName = "GA Result Report",
                ItemsIncluded = "All Submitted Lift Units",
                ProjectName = "Project Wide",
                GaRunStatusText = "Run run-wide-1234567890"
            },
            Columns = columns,
            Rows =
            [
                new ReportDataRowDto
                {
                    Values = columns.ToDictionary(
                        column => column.Key,
                        column => (object?)$"Value {column.Key}",
                        StringComparer.OrdinalIgnoreCase)
                }
            ]
        };

        var path = Path.Combine(_tempDir, "wide-report.pdf");
        await new UserFacingReportPdfExporter().ExportAsync(report, path);

        using var document = PdfDocument.Open(path);
        var pages = document.GetPages().ToList();
        var text = string.Join(Environment.NewLine, pages.Select(page => page.Text));

        Assert.DoesNotContain("Columns 1 of", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Columns 2 of", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GA Result Report", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GAR-20260712-0110-RUNWIDE1-7F2C", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doc-wide", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session-wide-report", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source-wide-report", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("input-wide-report", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(pages.Max(page => page.Width) > 2384);
    }

    [Fact]
    public async Task ExcelExporter_GaResultReport_CreatesRequiredFiveWorksheets()
    {
        var report = new ReportDocumentDto
        {
            ReportReference = "GAR-20260712-0110-RUN12345-7F2C",
            InternalFamily = ReportFamily.LiftingResult,
            Metadata = new ReportMetadataDto
            {
                ReportName = "GA Result Report",
                ItemsIncluded = "All Submitted Lift Units",
                ProjectName = "Project GA",
                ReportDate = new DateTime(2026, 7, 12, 1, 10, 0),
                CraneInfo = new CraneReportInfoDto
                {
                    CraneName = "Saved Crane",
                    Manufacturer = "Maker",
                    ModelName = "Model A",
                    LoadChartName = "Chart 1",
                    LoadChartSource = "Saved snapshot"
                },
                LiftingAccessories = new LiftingAccessoriesInfoDto
                {
                    WireRopeLengthM = 30,
                    HookRiggingWeightTons = 0.7,
                    TotalBelowHookWeightTons = 0.7,
                    NumberOfFallsOrLines = 4,
                    RiggingConfigurationName = "Main boom",
                    Completeness = "Partial"
                }
            },
            Columns =
            [
                new ReportColumnDefinition { Key = "No", Header = "No.", ValueKind = ReportColumnValueKind.Number, Order = 0, IsSelected = true },
                new ReportColumnDefinition { Key = "LiftStatus", Header = "Lift Status", ValueKind = ReportColumnValueKind.Text, Order = 1, IsSelected = true }
            ],
            Rows =
            [
                new ReportDataRowDto
                {
                    Values = new Dictionary<string, object?>
                    {
                        ["No"] = 1,
                        ["LiftStatus"] = "Lifted",
                        ["IsLifted"] = "Yes",
                        ["AssignedSetupNo"] = 1
                    }
                }
            ],
            CraneSetups =
            [
                new CraneSetupReportRowDto
                {
                    SetupNo = 1,
                    SetupReference = "Point_1",
                    CraneX = 10,
                    CraneY = 20,
                    CraneZ = 0,
                    LiftedItemsCount = 1,
                    LiftedItemsPercentage = 100,
                    Zone = "Zone A"
                }
            ]
        };

        var path = Path.Combine(_tempDir, "ga-result.xlsx");
        await new UserFacingReportExcelExporter().ExportAsync(report, path);

        using var archive = ZipFile.OpenRead(path);
        var workbook = ReadArchiveText(archive, "xl/workbook.xml");

        Assert.Contains("Report Summary", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Final Setups", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Setup Point_1", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed Lifts", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Crane\"", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Lifting Accessories\"", workbook, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet3.xml"));
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet4.xml"));
        Assert.Contains(report.ReportReference, ReadArchiveText(archive, "xl/worksheets/sheet1.xml"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExcelExporter_GaSetupAndFailedSheets_UseSectionDisplayNumbers()
    {
        var report = new ReportDocumentDto
        {
            ReportReference = "GAR-20260712-0110-RUN12345-7F2C",
            InternalFamily = ReportFamily.LiftingResult,
            Metadata = new ReportMetadataDto
            {
                ReportName = "GA Result Report",
                ItemsIncluded = "All Submitted Lift Units",
                ProjectName = "Project GA"
            },
            Columns = ReportTemplateCatalog.GetBuiltInTemplate(
                ReportFamily.LiftingResult,
                ReportTemplateCatalog.GaResultReport).Columns,
            Rows =
            [
                GaReportRow(101, "LU-1", "M-1", 1, true),
                GaReportRow(102, "LU-2", "M-2", 1, true),
                GaReportRow(201, "LU-3", "M-3", 2, true),
                GaReportRow(301, "LU-4", "M-4", 0, false),
                GaReportRow(302, "LU-5", "M-5", 0, false)
            ],
            CraneSetups =
            [
                new CraneSetupReportRowDto
                {
                    SetupNo = 1,
                    SetupReference = "Point_1",
                    LiftedItemsCount = 2,
                    LiftedItemsPercentage = 66.7
                },
                new CraneSetupReportRowDto
                {
                    SetupNo = 2,
                    SetupReference = "Point_2",
                    LiftedItemsCount = 1,
                    LiftedItemsPercentage = 33.3
                }
            ]
        };

        var path = Path.Combine(_tempDir, "ga-section-numbering.xlsx");
        await new UserFacingReportExcelExporter().ExportAsync(report, path);

        using var archive = ZipFile.OpenRead(path);
        var setup1Xml = ReadArchiveText(archive, "xl/worksheets/sheet3.xml");
        var setup2Xml = ReadArchiveText(archive, "xl/worksheets/sheet4.xml");
        var failedXml = ReadArchiveText(archive, "xl/worksheets/sheet5.xml");
        var setup1 = XDocument.Parse(setup1Xml);
        var setup2 = XDocument.Parse(setup2Xml);
        var failed = XDocument.Parse(failedXml);

        Assert.Equal("1", ReadCellValue(setup1, "A9"));
        Assert.Equal("2", ReadCellValue(setup1, "A10"));
        Assert.Equal("1", ReadCellValue(setup2, "A9"));
        Assert.Equal("1", ReadCellValue(failed, "A4"));
        Assert.Equal("2", ReadCellValue(failed, "A5"));
        Assert.Contains("Material Type", setup1Xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Material Type", failedXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Material Grade", setup1Xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Material Grade", failedXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("101", setup1Xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("201", setup2Xml, StringComparison.OrdinalIgnoreCase);
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
        var template = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.GaResultReport);
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
        Assert.DoesNotContain("LiftStatus", document.Columns.Select(column => column.Key));
        Assert.DoesNotContain(document.Metadata.SummaryItems, item => item.Label == "GA Run");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Total Items" && item.Value == "0");
        Assert.Contains(document.Metadata.SummaryItems, item => item.Label == "Final Setup Count" && item.Value == "0");
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

    private static LiftingResultSourceRowDto LiftSummaryRow(
        string id,
        string mark,
        string status,
        double required,
        double available) => new()
    {
        ElementId = id,
        ElementMark = mark,
        AssignedSetupNo = status.Equals("Liftable", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
        RequiredCapacity = required,
        AvailableCapacity = available,
        InternalLiftStatus = status,
        IsFinalAssigned = status.Equals("Liftable", StringComparison.OrdinalIgnoreCase)
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

    [Fact]
    public void ResolveReportableElements_FiltersOutHiddenAndAccessoryAndIgnored()
    {
        var resolver = new ReportableElementResolver();
        var mainPart = Element("main", role: IfcElementRole.LiftableElement);
        mainPart.ChildClassification = AssemblyChildClassification.MainPart;

        var secondary = Element("sec", role: IfcElementRole.LiftableElement);
        secondary.ChildClassification = AssemblyChildClassification.VisibleSecondaryPart;

        var hidden = Element("hid", role: IfcElementRole.InternalAuxiliary);
        hidden.ChildClassification = AssemblyChildClassification.HiddenWeightContributor;

        var accessory = Element("acc", role: IfcElementRole.InternalAuxiliary);
        accessory.ChildClassification = AssemblyChildClassification.AccessoryWeightContributor;

        var ignored = Element("ign", role: IfcElementRole.InternalAuxiliary);
        ignored.ChildClassification = AssemblyChildClassification.IgnoredReference;

        var list = new List<PrecastElement> { mainPart, secondary, hidden, accessory, ignored };
        var resolved = resolver.ResolveReportableElements(list);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(resolved, e => e.ElementID == "main");
        Assert.Contains(resolved, e => e.ElementID == "sec");
        Assert.DoesNotContain(resolved, e => e.ElementID == "hid");
        Assert.DoesNotContain(resolved, e => e.ElementID == "acc");
        Assert.DoesNotContain(resolved, e => e.ElementID == "ign");
    }

    [Fact]
    public void BuildRows_IncludesAssemblyRollupColumns()
    {
        var provider = new LiftingResultReportDataProvider();
        var row = new LiftingResultSourceRowDto
        {
            ElementId = "main",
            ElementMark = "M-main",
            AssignedSetupNo = 1,
            Radius = 10,
            BoomLength = 15,
            RequiredCapacity = 5,
            AvailableCapacity = 10,
            InternalLiftStatus = "Liftable",
            ParentAssemblyGlobalId = "parent",
            AssemblyLiftWeight = 9.28,
            AssemblyLiftCOG = new DomainPoint3D(89.5, 109.1, 1.9),
            AssemblyMemberIds = new List<string> { "main", "sec1", "sec2" }
        };

        var rows = provider.BuildRows(ReportTemplateCatalog.GaResultReport, "Optimization Result", new[] { row });
        var reportRow = Assert.Single(rows);

        Assert.Equal("parent", reportRow.Values["ParentAssemblyGlobalId"]);
        Assert.Equal(9.28, reportRow.Values["AssemblyLiftWeight"]);
        Assert.Equal("(89.500; 109.100; 1.900)", reportRow.Values["AssemblyLiftCOG"]);
        Assert.Equal("main, sec1, sec2", reportRow.Values["AssemblyMemberIds"]);
    }

    [Fact]
    public void BuildRows_DeduplicatesAssemblyRowsByParentIdentity()
    {
        var provider = new LiftingResultReportDataProvider();
        var rows = new[]
        {
            new LiftingResultSourceRowDto
            {
                ElementId = "main-a",
                InternalLiftStatus = "Liftable",
                Values = new Dictionary<string, object?>
                {
                    ["IsAssemblyGroup"] = "Yes",
                    ["ParentAssemblyGlobalId"] = "assembly-1",
                    ["MainPartGlobalId"] = "main-a"
                }
            },
            new LiftingResultSourceRowDto
            {
                ElementId = "child-b",
                InternalLiftStatus = "Liftable",
                Values = new Dictionary<string, object?>
                {
                    ["IsAssemblyGroup"] = "Yes",
                    ["ParentAssemblyGlobalId"] = "assembly-1",
                    ["MainPartGlobalId"] = "main-a"
                }
            }
        };

        IReadOnlyList<LiftingResultReportRowDto> reportRows = provider.BuildRows(
            ReportTemplateCatalog.GaResultReport,
            "Optimization Result",
            rows);

        Assert.Single(reportRows);
    }

    private static PrecastElement CanonicalFixture(
        string id,
        string mark,
        string name,
        string type,
        string material,
        double length,
        double width,
        double height,
        double volume,
        double weight,
        DomainPoint3D cog) => new()
    {
        ElementID = id,
        ElementMark = mark,
        ElementName = name,
        ElementType = type,
        ElementMaterial = material,
        ElementLength = length,
        ElementWidth = width,
        ElementHeight = height,
        VolumeM3 = volume,
        ElementVolume = volume,
        WeightTons = weight,
        ElementWT = weight,
        WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
        WeightSource = "ExplicitQuantity",
        ElementCOG = cog,
        COG_M = cog,
        PhysicalCOGAvailable = true,
        IncludeInGa = true,
        Role = IfcElementRole.LiftableElement
    };

    private static GaResultSnapshotDto BuildSingleLiftSnapshot(CanonicalLiftUnitSnapshotDto unit) => new()
    {
        SchemaVersion = 2,
        CanonicalSnapshotId = "canonical-test",
        CanonicalPayloadHash = CanonicalLiftUnitSnapshotBuilder.ComputePayloadHash([unit]),
        Run = new GaRunIdentitySnapshotDto
        {
            OptimizationRunId = "run-1",
            Status = "Completed",
            CompletedAtUtc = DateTimeOffset.UtcNow
        },
        Project = new GaProjectSnapshotDto { ProjectGuid = "project-1", ProjectName = "Project" },
        Model = new GaModelSnapshotDto
        {
            CanonicalSnapshotId = "canonical-test",
            CanonicalPayloadHash = CanonicalLiftUnitSnapshotBuilder.ComputePayloadHash([unit]),
            ModelFingerprint = CanonicalLiftUnitSnapshotBuilder.ComputePayloadHash([unit])
        },
        Crane = new GaCraneSnapshotDto { CraneName = "Crane" },
        Summary = new GaRunSummarySnapshotDto
        {
            TotalTargetLiftUnitCount = 1,
            LiftedLiftUnitCount = 1,
            UnliftedLiftUnitCount = 0,
            PositionCount = 1,
            CoverageRatio = 1
        },
        LiftUnits = [unit],
        Positions =
        [
            new GaPositionSnapshotDto
            {
                PositionId = "P1",
                SetupIndex = 1,
                SetupReference = "Point_1",
                LocalPosition = new GaPointSnapshotDto { X = 1, Y = 2, Z = 0 },
                FinalAssignedCount = 1,
                FinalAssignedLiftUnitIds = [unit.LiftUnitId],
                Evaluations =
                [
                    new GaElementEvaluationSnapshotDto
                    {
                        EvaluationId = "E1",
                        LiftUnitId = unit.LiftUnitId,
                        CandidatePointId = "P1",
                        LiftStatus = "Liftable",
                        ReachStatus = "Reachable",
                        ElementWeightTon = unit.LiftWeightTon,
                        RequiredRadiusM = 10,
                        RequiredBoomLengthM = 20,
                        RequiredCapacityTon = unit.LiftWeightTon,
                        AvailableCapacityTon = 20,
                        IsFinalAssignedToPoint = true,
                        IsFinalAssigned = true,
                        FinalOwnerPointId = "P1"
                    }
                ]
            }
        ],
        FinalOutcomes =
        [
            new GaFinalOutcomeSnapshotDto
            {
                LiftUnitId = unit.LiftUnitId,
                IsLifted = true,
                FinalEvaluationId = "E1",
                FinalOwnerPositionId = "P1",
                FinalLiftStatus = "Liftable"
            }
        ],
        Capabilities = GaSnapshotCapabilitiesDto.Production(false, false, false)
    };
}
