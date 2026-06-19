using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Infrastructure.Reports.Csv;

namespace CraneOptimizer.Application.Tests.Reports;

public class CsvReportExporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CsvReportExporter _exporter = new(new ReportValueFormatter());

    public CsvReportExporterTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, $"report-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ExportAsync_CreatesSingleCsv_ForOneTableReport()
    {
        var path = Path.Combine(_tempDir, "single.csv");

        var result = await _exporter.ExportAsync(CreateDocument(tableCount: 1), path);

        Assert.True(result.Success);
        Assert.True(File.Exists(path));
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal("Name,Amount (ton),Percent (%)", lines[0]);
    }

    [Fact]
    public async Task ExportAsync_CreatesPackage_ForMultiTableCsvPath()
    {
        var path = Path.Combine(_tempDir, "package.csv");

        var result = await _exporter.ExportAsync(CreateDocument(tableCount: 2), path);

        var packageDir = Path.Combine(_tempDir, "package");
        Assert.True(result.Success);
        Assert.Equal(packageDir, result.PrimaryOutputPath);
        Assert.True(Directory.Exists(packageDir));
        Assert.True(File.Exists(Path.Combine(packageDir, "_metadata.csv")));
        Assert.True(File.Exists(Path.Combine(packageDir, "table_1.csv")));
        Assert.True(File.Exists(Path.Combine(packageDir, "table_2.csv")));
        Assert.True(File.Exists(Path.Combine(packageDir, "report_manifest.json")));
    }

    [Fact]
    public async Task ExportAsync_WritesMetadataAndManifest_ForDirectoryOutput()
    {
        var packageDir = Path.Combine(_tempDir, "reports");

        await _exporter.ExportAsync(CreateDocument(tableCount: 2), packageDir);

        var metadata = await File.ReadAllTextAsync(Path.Combine(packageDir, "_metadata.csv"));
        var manifest = await File.ReadAllTextAsync(Path.Combine(packageDir, "report_manifest.json"));

        Assert.Contains("ProjectName,Project Alpha", metadata);
        Assert.Contains("\"reportId\": \"report\"", manifest);
        Assert.Contains("table_1.csv", manifest);
    }

    [Fact]
    public async Task ExportAsync_PreservesColumnOrder_AndEscapesCsvValues()
    {
        var path = Path.Combine(_tempDir, "single.csv");

        await _exporter.ExportAsync(CreateDocument(tableCount: 1), path);

        var lines = await File.ReadAllLinesAsync(path);

        Assert.Equal("Name,Amount (ton),Percent (%)", lines[0]);
        Assert.Equal("\"Value, with comma and \"\"quote\"\"\",12.346,45.68", lines[1]);
        Assert.StartsWith("\"Line", lines[2]);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailure_WhenReportHasNoTables()
    {
        var path = Path.Combine(_tempDir, "metrics-only.csv");

        var result = await _exporter.ExportAsync(CreateMetricsOnlyDocument(), path);

        Assert.False(result.Success);
        Assert.Contains("CSV export requires at least one report table", result.ErrorMessage);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ExportAsync_GeneratesSanitizedUniqueFileNames_ForDuplicateTableNames()
    {
        var packageDir = Path.Combine(_tempDir, "duplicate-names");

        var result = await _exporter.ExportAsync(CreateSanitizedDuplicateNameDocument(), packageDir);

        var firstPath = Path.Combine(packageDir, "element_lift_details.csv");
        var secondPath = Path.Combine(packageDir, "element_lift_details_2.csv");
        var thirdPath = Path.Combine(packageDir, "crane_positions.csv");
        var fourthPath = Path.Combine(packageDir, "table.csv");
        var fifthPath = Path.Combine(packageDir, "table_2.csv");
        var manifestPath = Path.Combine(packageDir, "report_manifest.json");

        Assert.True(result.Success);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.True(File.Exists(thirdPath));
        Assert.True(File.Exists(fourthPath));
        Assert.True(File.Exists(fifthPath));
        Assert.True(new FileInfo(firstPath).Length > 0);
        Assert.True(new FileInfo(secondPath).Length > 0);

        var manifest = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("element_lift_details.csv", manifest);
        Assert.Contains("element_lift_details_2.csv", manifest);
        Assert.Contains("crane_positions.csv", manifest);
        Assert.Contains("table.csv", manifest);
        Assert.Contains("table_2.csv", manifest);
    }

    [Fact]
    public async Task ExportAsync_UsesFullOrSafePath_ForRelativeOutput()
    {
        var absolutePath = Path.Combine(_tempDir, "relative.csv");
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), absolutePath);

        var result = await _exporter.ExportAsync(CreateDocument(tableCount: 1), relativePath);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(relativePath), result.PrimaryOutputPath);
        Assert.True(File.Exists(absolutePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static ReportDocument CreateDocument(int tableCount)
    {
        var document = new ReportDocument
        {
            ReportId = "report",
            Title = "Report",
            CreatedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc),
            Metadata = new ReportMetadata
            {
                ProjectName = "Project Alpha",
                ProjectId = "P-001",
                IfcFileName = "model.ifc",
                CraneName = "LTM 1300",
                PreparedBy = "Engineer",
                Extra =
                {
                    ["ScenarioId"] = "scenario-1"
                }
            }
        };

        var section = new ReportSection { Title = "Section" };
        for (var i = 1; i <= tableCount; i++)
            section.Tables.Add(CreateTable($"table_{i}"));

        document.Sections.Add(section);
        return document;
    }

    private static ReportDocument CreateMetricsOnlyDocument()
    {
        return new ReportDocument
        {
            ReportId = "metrics-only",
            Title = "Metrics Only",
            Sections =
            {
                new ReportSection
                {
                    Title = "Summary",
                    Metrics =
                    {
                        new ReportMetric { Label = "Coverage", Value = "75", Unit = "%" }
                    }
                }
            }
        };
    }

    private static ReportDocument CreateSanitizedDuplicateNameDocument()
    {
        var document = CreateDocument(tableCount: 0);
        document.Sections[0].Tables.Add(CreateTable("Element Lift Details"));
        document.Sections[0].Tables.Add(CreateTable("Element:Lift?Details"));
        document.Sections[0].Tables.Add(CreateTable("  Crane   Positions  "));
        document.Sections[0].Tables.Add(CreateTable("///"));
        document.Sections[0].Tables.Add(CreateTable(string.Empty));

        return document;
    }

    private static ReportTable CreateTable(string name)
    {
        return new ReportTable
        {
            Name = name,
            Title = name,
            Columns =
            {
                new ReportColumn { Key = "Name", Header = "Name" },
                new ReportColumn { Key = "Amount", Header = "Amount", Unit = "ton", Type = ReportColumnType.Number },
                new ReportColumn { Key = "Percent", Header = "Percent", Unit = "%", Type = ReportColumnType.Percentage }
            },
            Rows =
            {
                new ReportRow
                {
                    Values =
                    {
                        ["Name"] = "Value, with comma and \"quote\"",
                        ["Amount"] = 12.34567,
                        ["Percent"] = 45.678
                    }
                },
                new ReportRow
                {
                    Values =
                    {
                        ["Name"] = "Line\r\nbreak",
                        ["Amount"] = 1.2,
                        ["Percent"] = 3.4
                    }
                }
            }
        };
    }
}
