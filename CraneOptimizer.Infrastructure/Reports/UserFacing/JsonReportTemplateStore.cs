using System.IO;
using System.Text.Json;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;

namespace CraneOptimizer.Infrastructure.Reports.UserFacing;

public sealed class JsonReportTemplateStore : IReportTemplateStore
{
    private readonly string _templateDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonReportTemplateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CraneOptimizer",
            "ReportTemplates"))
    {
    }

    public JsonReportTemplateStore(string templateDirectory)
    {
        _templateDirectory = templateDirectory;
    }

    public async Task SaveAsync(ReportTemplateDto template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        Directory.CreateDirectory(_templateDirectory);

        var fileName = $"{template.InternalFamily}_{SanitizeFileName(template.TemplateName)}.json";
        var path = Path.Combine(_templateDirectory, fileName);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, template, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReportTemplateDto>> LoadAsync(
        ReportFamily family,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_templateDirectory))
            return Array.Empty<ReportTemplateDto>();

        var templates = new List<ReportTemplateDto>();
        foreach (var path in Directory.EnumerateFiles(_templateDirectory, $"{family}_*.json"))
        {
            await using var stream = File.OpenRead(path);
            var template = await JsonSerializer.DeserializeAsync<ReportTemplateDto>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            if (template != null)
                templates.Add(template);
        }

        return templates;
    }

    public Task DeleteAsync(
        ReportFamily family,
        string templateName,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_templateDirectory) || string.IsNullOrWhiteSpace(templateName))
            return Task.CompletedTask;

        var fileName = $"{family}_{SanitizeFileName(templateName)}.json";
        var path = Path.Combine(_templateDirectory, fileName);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", value.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
