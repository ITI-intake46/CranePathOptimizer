using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Infrastructure.Reports.Csv;

public sealed class CsvReportExporter : IReportExporter
{
    private readonly IReportValueFormatter _formatter;
    private readonly CsvExportOptions _options;

    public CsvReportExporter(IReportValueFormatter formatter)
        : this(formatter, new CsvExportOptions())
    {
    }

    public CsvReportExporter(IReportValueFormatter formatter, CsvExportOptions options)
    {
        _formatter = formatter;
        _options = options;
    }

    public ReportFormat Format => ReportFormat.Csv;

    public async Task<ReportExportResult> ExportAsync(
        ReportDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tables = document.Sections.SelectMany(section => section.Tables).ToList();
        if (tables.Count == 0)
            return ReportExportResult.Fail("CSV export requires at least one report table.");

        var target = ResolveOutputDirectoryOrFile(outputPath, tables.Count);
        var outputFiles = new List<string>();

        if (target.IsSingleFile)
        {
            CreateDirectoryForFile(target.FilePath!);
            await WriteTableCsvAsync(tables[0], target.FilePath!, cancellationToken).ConfigureAwait(false);
            return ReportExportResult.Ok(target.FilePath!);
        }

        Directory.CreateDirectory(target.DirectoryPath!);

        if (_options.IncludeMetadataFile)
        {
            var metadataPath = Path.Combine(target.DirectoryPath!, "_metadata.csv");
            await WriteMetadataCsvAsync(document, metadataPath, cancellationToken).ConfigureAwait(false);
            outputFiles.Add(metadataPath);
        }

        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tableFileName = GetUniqueFileName(SanitizeFileName(table.Name), usedFileNames);
            var tablePath = Path.Combine(target.DirectoryPath!, tableFileName);
            await WriteTableCsvAsync(table, tablePath, cancellationToken).ConfigureAwait(false);
            outputFiles.Add(tablePath);
        }

        if (_options.IncludeManifestFile)
        {
            var manifestPath = Path.Combine(target.DirectoryPath!, "report_manifest.json");
            await WriteManifestAsync(document, outputFiles, manifestPath, cancellationToken).ConfigureAwait(false);
            outputFiles.Add(manifestPath);
        }

        return ReportExportResult.Ok(target.DirectoryPath!, outputFiles);
    }

    private static ReportOutputTarget ResolveOutputDirectoryOrFile(string outputPath, int tableCount)
    {
        var extension = Path.GetExtension(outputPath);
        var hasCsvExtension = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);

        if (hasCsvExtension && tableCount == 1)
            return ReportOutputTarget.SingleFile(Path.GetFullPath(outputPath));

        if (hasCsvExtension)
        {
            var parent = Path.GetDirectoryName(outputPath) ?? ".";
            var folderName = Path.GetFileNameWithoutExtension(outputPath);
            return ReportOutputTarget.Directory(Path.GetFullPath(Path.Combine(parent, folderName)));
        }

        return ReportOutputTarget.Directory(Path.GetFullPath(outputPath));
    }

    private static void CreateDirectoryForFile(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private async Task WriteMetadataCsvAsync(
        ReportDocument document,
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("Key,Value").ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "ReportId", document.ReportId, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "Title", document.Title, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "CreatedAtUtc", document.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "ProjectName", document.Metadata.ProjectName, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "ProjectId", document.Metadata.ProjectId, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "IfcFileName", document.Metadata.IfcFileName, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "CraneName", document.Metadata.CraneName, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "PreparedBy", document.Metadata.PreparedBy, cancellationToken).ConfigureAwait(false);
        await WriteMetadataRowAsync(writer, "AppVersion", document.Metadata.AppVersion, cancellationToken).ConfigureAwait(false);

        foreach (var extra in document.Metadata.Extra)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteMetadataRowAsync(writer, extra.Key, extra.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task WriteMetadataRowAsync(
        StreamWriter writer,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return writer.WriteLineAsync($"{EscapeCsv(key)},{EscapeCsv(value)}");
    }

    private async Task WriteTableCsvAsync(
        ReportTable table,
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync(BuildHeader(table)).ConfigureAwait(false);

        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = table.Columns.Select(column =>
            {
                row.Values.TryGetValue(column.Key, out var value);
                return EscapeCsv(_formatter.FormatValue(value, column));
            });

            await writer.WriteLineAsync(string.Join(",", values)).ConfigureAwait(false);
        }
    }

    private static async Task WriteManifestAsync(
        ReportDocument document,
        IReadOnlyList<string> outputFiles,
        string path,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? ".";
        var manifest = new ReportManifest
        {
            ReportId = document.ReportId,
            Title = document.Title,
            CreatedAtUtc = document.CreatedAtUtc,
            Files = outputFiles.Select(file => Path.GetRelativePath(directory, file)).ToList()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, options, cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "table";

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        var lastWasUnderscore = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            var shouldBecomeUnderscore =
                char.IsWhiteSpace(character)
                || invalidCharacters.Contains(character)
                || character is '-' or ':' or '/' or '\\' or '?' or '*' or '"' or '<' or '>' or '|';

            if (shouldBecomeUnderscore)
            {
                if (!lastWasUnderscore)
                {
                    builder.Append('_');
                    lastWasUnderscore = true;
                }

                continue;
            }

            builder.Append(character);
            lastWasUnderscore = false;
        }

        var result = builder.ToString().Trim('_');

        return string.IsNullOrWhiteSpace(result) ? "table" : result;
    }

    private static string GetUniqueFileName(string baseName, HashSet<string> usedFileNames)
    {
        var safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "table" : baseName;
        var candidate = $"{safeBaseName}.csv";
        var index = 2;

        while (!usedFileNames.Add(candidate))
        {
            candidate = $"{safeBaseName}_{index}.csv";
            index++;
        }

        return candidate;
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string BuildHeader(ReportTable table)
    {
        return string.Join(
            ",",
            table.Columns.Select(column =>
                EscapeCsv(string.IsNullOrWhiteSpace(column.Unit)
                    ? column.Header
                    : $"{column.Header} ({column.Unit})")));
    }

    private sealed record ReportOutputTarget(bool IsSingleFile, string? FilePath, string? DirectoryPath)
    {
        public static ReportOutputTarget SingleFile(string filePath)
        {
            return new ReportOutputTarget(true, filePath, null);
        }

        public static ReportOutputTarget Directory(string directoryPath)
        {
            return new ReportOutputTarget(false, null, directoryPath);
        }
    }
}
