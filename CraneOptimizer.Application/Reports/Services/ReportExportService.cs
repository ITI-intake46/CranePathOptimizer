using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ReportExportService : IReportExportService
{
    private readonly IReadOnlyDictionary<ReportFormat, IReportExporter> _exporters;
    private readonly IReportValidator _validator;

    public ReportExportService(
        IEnumerable<IReportExporter> exporters,
        IReportValidator validator)
    {
        _exporters = exporters.ToDictionary(exporter => exporter.Format);
        _validator = validator;
    }

    public Task<ReportExportResult> ExportAsync(
        ReportDocument document,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (document == null)
        {
            return Task.FromResult(ReportExportResult.Fail("Report document is null."));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Task.FromResult(ReportExportResult.Fail("Output path is required."));
        }

        var validation = _validator.Validate(document);
        if (!validation.IsValid)
        {
            return Task.FromResult(
                ReportExportResult.Fail(string.Join(Environment.NewLine, validation.Errors)));
        }

        if (!_exporters.TryGetValue(format, out var exporter))
        {
            return Task.FromResult(
                ReportExportResult.Fail($"No report exporter is registered for format: {format}."));
        }

        return exporter.ExportAsync(document, outputPath, cancellationToken);
    }
}
