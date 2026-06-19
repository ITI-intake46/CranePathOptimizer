using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Infrastructure.Reports.Pdf;

namespace CraneOptimizer.Application.Tests.Reports;

public class PdfReportExporterTests : IDisposable
{
    private readonly string _tempDir;

    public PdfReportExporterTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, $"report-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ExportAsync_WritesPdfFile()
    {
        var exporter = new PdfReportExporter(new ReportValueFormatter());
        var path = Path.Combine(_tempDir, "report.pdf");

        var result = await exporter.ExportAsync(CreateDocument(), path);

        Assert.True(result.Success);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public async Task ExportAsync_WithRelativeFileName_CreatesPdf()
    {
        var exporter = new PdfReportExporter(new ReportValueFormatter());
        var absolutePath = Path.Combine(_tempDir, "report.pdf");
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), absolutePath);

        var result = await exporter.ExportAsync(CreateDocument(), relativePath);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(relativePath), result.PrimaryOutputPath);
        Assert.True(File.Exists(absolutePath));
        Assert.True(new FileInfo(absolutePath).Length > 0);
    }

    [Fact]
    public async Task ExportAsync_UsesSafeDirectoryCreation()
    {
        var exporter = new PdfReportExporter(new ReportValueFormatter());
        var nestedDirectory = Path.Combine(_tempDir, "reports");
        var path = Path.Combine(nestedDirectory, "output.pdf");

        var result = await exporter.ExportAsync(CreateDocument(), path);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(nestedDirectory));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ExportAsync_HonorsCancellationBeforeGeneration()
    {
        var exporter = new PdfReportExporter(new ReportValueFormatter());
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            exporter.ExportAsync(
                CreateDocument(),
                Path.Combine(_tempDir, "cancelled.pdf"),
                cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static ReportDocument CreateDocument()
    {
        return new ReportDocument
        {
            ReportId = "report",
            Title = "Report",
            Metadata = new ReportMetadata
            {
                ProjectName = "Project Alpha",
                IfcFileName = "model.ifc",
                CraneName = "LTM 1300"
            },
            Sections =
            {
                new ReportSection
                {
                    Title = "Summary",
                    Metrics =
                    {
                        new ReportMetric { Label = "Coverage", Value = "75", Unit = "%" }
                    },
                    Tables =
                    {
                        new ReportTable
                        {
                            Name = "summary",
                            Title = "Summary",
                            Columns =
                            {
                                new ReportColumn { Key = "Name", Header = "Name" },
                                new ReportColumn { Key = "Value", Header = "Value", Type = ReportColumnType.Number }
                            },
                            Rows =
                            {
                                new ReportRow
                                {
                                    Values =
                                    {
                                        ["Name"] = "Lifted",
                                        ["Value"] = 3
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
