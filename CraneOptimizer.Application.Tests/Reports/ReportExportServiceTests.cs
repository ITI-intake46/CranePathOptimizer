using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Application.Reports.Services;

namespace CraneOptimizer.Application.Tests.Reports;

public class ReportExportServiceTests
{
    [Fact]
    public async Task ExportAsync_SelectsExporterByFormat()
    {
        var exporter = new FakeExporter(ReportFormat.Csv);
        var service = new ReportExportService([exporter], new ReportValidator());

        var result = await service.ExportAsync(CreateValidDocument(), ReportFormat.Csv, "report.csv");

        Assert.True(result.Success);
        Assert.True(exporter.WasCalled);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailure_WhenExporterMissing()
    {
        var service = new ReportExportService([], new ReportValidator());

        var result = await service.ExportAsync(CreateValidDocument(), ReportFormat.Pdf, "report.pdf");

        Assert.False(result.Success);
        Assert.Contains("No report exporter", result.ErrorMessage);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailure_WhenDocumentNull()
    {
        var exporter = new FakeExporter(ReportFormat.Csv);
        var service = new ReportExportService([exporter], new ReportValidator());

        var result = await service.ExportAsync(null!, ReportFormat.Csv, "report.csv");

        Assert.False(result.Success);
        Assert.False(exporter.WasCalled);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailure_WhenOutputPathEmpty()
    {
        var exporter = new FakeExporter(ReportFormat.Csv);
        var service = new ReportExportService([exporter], new ReportValidator());

        var result = await service.ExportAsync(CreateValidDocument(), ReportFormat.Csv, string.Empty);

        Assert.False(result.Success);
        Assert.False(exporter.WasCalled);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailureBeforeExporter_WhenReportInvalid()
    {
        var exporter = new FakeExporter(ReportFormat.Csv);
        var service = new ReportExportService([exporter], new ReportValidator());

        var result = await service.ExportAsync(new ReportDocument(), ReportFormat.Csv, "report.csv");

        Assert.False(result.Success);
        Assert.False(exporter.WasCalled);
    }

    private static ReportDocument CreateValidDocument()
    {
        return new ReportDocument
        {
            ReportId = "report",
            Title = "Report",
            Sections =
            {
                new ReportSection { Title = "Section" }
            }
        };
    }

    private sealed class FakeExporter : IReportExporter
    {
        public FakeExporter(ReportFormat format)
        {
            Format = format;
        }

        public ReportFormat Format { get; }
        public bool WasCalled { get; private set; }

        public Task<ReportExportResult> ExportAsync(
            ReportDocument document,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(ReportExportResult.Ok(outputPath));
        }
    }
}
