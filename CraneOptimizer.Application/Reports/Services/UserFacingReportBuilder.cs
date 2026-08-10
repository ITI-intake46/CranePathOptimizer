using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class UserFacingReportBuilder : IReportBuilder
{
    private readonly IElementReportDataProvider _elementProvider;
    private readonly ILiftingResultReportDataProvider _liftingProvider;

    public UserFacingReportBuilder(
        IElementReportDataProvider elementProvider,
        ILiftingResultReportDataProvider liftingProvider)
    {
        _elementProvider = elementProvider;
        _liftingProvider = liftingProvider;
    }

    public Task<ReportDocumentDto> BuildAsync(
        ReportBuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var columns = request.SelectedColumns
            .Where(column => column.IsSelected)
            .Where(column => !ReportTemplateCatalog.IsUserFacingForbiddenColumn(column.Key))
            .Where(column => request.Family != ReportFamily.LiftingResult
                || !column.Key.Equals("MaterialGrade", StringComparison.OrdinalIgnoreCase))
            .OrderBy(column => column.Order)
            .GroupBy(column => column.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => NormalizeSelectedColumn(request.Family, group.First()))
            .ToList();

        if (request.TemplateName.Equals(ReportTemplateCatalog.ReportDataSourceAudit, StringComparison.OrdinalIgnoreCase))
        {
            var auditRows = ReportDataSourceAuditProvider.BuildRows(request)
                .Select(ToReportRow)
                .ToList();
            EnsureSelectedColumnValues(auditRows, columns);
            ApplySequentialNumbers(auditRows);

            return Task.FromResult(CreateDocument(
                request.Family,
                CloneMetadata(request, BuildAuditSummary(auditRows)),
                columns,
                auditRows,
                Array.Empty<CraneSetupReportRowDto>()));
        }

        IReadOnlyList<ReportDataRowDto> rows;
        IReadOnlyList<ReportSummaryItemDto> summary;
        IReadOnlyList<ReportSummaryItemDto> failureBreakdown = Array.Empty<ReportSummaryItemDto>();
        var effectiveItemsIncluded = request.ItemsIncluded;

        if (request.Family == ReportFamily.ElementList)
        {
            var elementRows = _elementProvider.BuildRows(
                request.TemplateName,
                request.SourceElements);

            rows = elementRows.Select(ToReportRow).ToList();
            EnsureSelectedColumnValues(rows, columns);
            summary = BuildElementSummary(request.TemplateName, elementRows);
        }
        else
        {
            var liftingRows = _liftingProvider.BuildRows(
                request.TemplateName,
                request.ItemsIncluded,
                request.LiftingResults);

            rows = liftingRows.Select(ToReportRow).ToList();
            if (!request.Metadata.HasGaRun && rows.Count == 0)
                rows = [BuildNoGaRunPlaceholderRow(columns)];

            if (request.TemplateName.Equals(ReportTemplateCatalog.GaResultReport, StringComparison.OrdinalIgnoreCase))
                RemoveForbiddenUserFacingValues(rows);

            EnsureSelectedColumnValues(rows, columns);

            if (request.Metadata.HasGaRun)
            {
                summary = BuildLiftingSummary(liftingRows, request.CraneSetups);
                failureBreakdown = BuildLiftingFailureBreakdown(liftingRows);
            }
            else
            {
                summary = BuildNoGaRunSummary();
            }
        }

        ApplySequentialNumbers(rows);

        var metadata = new ReportMetadataDto
        {
            ReportName = request.TemplateName,
            ItemsIncluded = effectiveItemsIncluded,
            ProjectName = request.Metadata.ProjectName,
            ReportDate = request.Metadata.ReportDate,
            CompanyName = request.Metadata.CompanyName,
            CompanyLogoPath = request.Metadata.CompanyLogoPath,
            CreatedBy = request.Metadata.CreatedBy,
            IssuedTo = request.Metadata.IssuedTo,
            Revision = request.Metadata.Revision,
            HasGaRun = request.Metadata.HasGaRun,
            GaRunStatusText = request.Metadata.GaRunStatusText,
            GaWarningText = request.Metadata.GaWarningText,
            CraneInfo = request.Metadata.CraneInfo,
            LiftingAccessories = request.Metadata.LiftingAccessories,
            SummaryItems = summary,
            FailureBreakdownItems = failureBreakdown,
            GeneralNotes = request.Metadata.GeneralNotes
        };

        return Task.FromResult(CreateDocument(
            request.Family,
            metadata,
            columns,
            rows,
            request.Family == ReportFamily.LiftingResult
                ? request.CraneSetups
                : Array.Empty<CraneSetupReportRowDto>()));
    }

    private static ReportDocumentDto CreateDocument(
        ReportFamily family,
        ReportMetadataDto metadata,
        IReadOnlyList<ReportColumnDefinition> columns,
        IReadOnlyList<ReportDataRowDto> rows,
        IReadOnlyList<CraneSetupReportRowDto> craneSetups)
    {
        var generatedAtUtc = metadata.ReportDate.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(metadata.ReportDate)
            : new DateTimeOffset(metadata.ReportDate.ToUniversalTime(), TimeSpan.Zero);
        var sourceSignature = ComputeSourceSignature(metadata, columns, rows, craneSetups);

        return new ReportDocumentDto
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            ReportBuildSessionId = Guid.NewGuid().ToString("N"),
            ReportReference = BuildReportReference(family, metadata, generatedAtUtc, sourceSignature),
            GeneratedAtUtc = generatedAtUtc,
            InputSignature = ComputeHash($"input|{family}|{metadata.ReportName}|{metadata.ItemsIncluded}|{metadata.ProjectName}|{metadata.GaRunStatusText}|{columns.Count}|{rows.Count}|{craneSetups.Count}"),
            SourceSignature = sourceSignature,
            InternalFamily = family,
            Metadata = metadata,
            Columns = columns,
            Rows = rows,
            CraneSetups = craneSetups,
            PreferA3Landscape = columns.Count > 10,
            TextAlignment = columns.FirstOrDefault()?.Alignment ?? "Left",
            TextSizePt = ParseTextSize(columns.FirstOrDefault()?.TextSize)
        };
    }

    private static ReportColumnDefinition NormalizeSelectedColumn(
        ReportFamily family,
        ReportColumnDefinition selectedColumn)
    {
        var catalogColumn = ReportTemplateCatalog.GetAvailableColumns(family)
            .Concat(ReportTemplateCatalog.GetBuiltInTemplates(family).SelectMany(template => template.Columns))
            .FirstOrDefault(column => column.Key.Equals(selectedColumn.Key, StringComparison.OrdinalIgnoreCase));

        if (catalogColumn == null)
            return selectedColumn;

        return new ReportColumnDefinition
        {
            Key = catalogColumn.Key,
            Header = catalogColumn.Header,
            ValueKind = catalogColumn.ValueKind,
            EvidenceScope = catalogColumn.EvidenceScope,
            SupportedFamilies = catalogColumn.SupportedFamilies,
            RequiredCapabilities = catalogColumn.RequiredCapabilities,
            ShowInPreview = catalogColumn.ShowInPreview,
            ShowInPdf = catalogColumn.ShowInPdf,
            ShowInExcel = catalogColumn.ShowInExcel,
            IsSelected = true,
            Order = selectedColumn.Order,
            Format = selectedColumn.Format ?? catalogColumn.Format,
            Alignment = selectedColumn.Alignment ?? catalogColumn.Alignment,
            TextSize = selectedColumn.TextSize ?? catalogColumn.TextSize,
            Category = catalogColumn.Category,
            Unit = selectedColumn.Unit ?? catalogColumn.Unit
        };
    }

    private static string ComputeSourceSignature(
        ReportMetadataDto metadata,
        IReadOnlyList<ReportColumnDefinition> columns,
        IReadOnlyList<ReportDataRowDto> rows,
        IReadOnlyList<CraneSetupReportRowDto> craneSetups)
    {
        var builder = new StringBuilder();
        builder.Append(metadata.ReportName).Append('|')
            .Append(metadata.ItemsIncluded).Append('|')
            .Append(metadata.ProjectName).Append('|')
            .Append(metadata.GaRunStatusText).Append('|')
            .Append(metadata.GaWarningText).Append('|')
            .AppendJoin(',', columns.Select(column => column.Key));

        foreach (var row in rows)
        {
            builder.AppendLine();
            foreach (var column in columns)
            {
                row.Values.TryGetValue(column.Key, out var value);
                builder.Append(column.Key).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';');
            }
        }

        foreach (var setup in craneSetups)
        {
            builder.AppendLine()
                .Append("setup=").Append(setup.SetupNo).Append(',')
                .Append(setup.CraneX.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(setup.CraneY.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(setup.CraneZ.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(setup.LiftedItemsCount);
        }

        return ComputeHash(builder.ToString());
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReportReference(
        ReportFamily family,
        ReportMetadataDto metadata,
        DateTimeOffset generatedAtUtc,
        string sourceSignature)
    {
        var prefix = family == ReportFamily.LiftingResult ? "GAR" : "ELR";
        var timestamp = generatedAtUtc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var identityToken = family == ReportFamily.LiftingResult
            ? ExtractRunToken(metadata.GaRunStatusText)
            : ShortToken(sourceSignature, 8);
        var suffix = ShortToken(sourceSignature, 4);

        return $"{prefix}-{timestamp}-{identityToken}-{suffix}";
    }

    private static string ExtractRunToken(string? gaRunStatusText)
    {
        if (string.IsNullOrWhiteSpace(gaRunStatusText))
            return "NORUN000";

        var runIndex = gaRunStatusText.IndexOf("Run ", StringComparison.OrdinalIgnoreCase);
        var runText = runIndex >= 0
            ? gaRunStatusText[(runIndex + 4)..]
            : gaRunStatusText;

        return ShortToken(runText, 8);
    }

    private static string ShortToken(string? value, int length)
    {
        var token = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Take(length)
            .ToArray());

        return string.IsNullOrWhiteSpace(token)
            ? new string('0', length)
            : token.ToUpperInvariant().PadRight(length, '0')[..length];
    }

    private static double ParseTextSize(string? value)
    {
        if (double.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(parsed, 6, 32);

        return value switch
        {
            "Small" => 7,
            "Large" => 12,
            _ => 10
        };
    }



    private static ReportDataRowDto ToReportRow(ElementReportRowDto row) => new()
    {
        Values =
        {
            ["No"] = row.No,
            ["ElementId"] = row.ElementId,
            ["ElementID"] = row.ElementId,
            ["ElementMark"] = row.ElementMark,
            ["ElementName"] = row.ElementName,
            ["ElementType"] = row.ElementType,
            ["SourceRevitId"] = row.SourceRevitId,
            ["ElementRevitId"] = row.SourceRevitId,
            ["StoreyLevel"] = row.StoreyLevel,
            ["Material"] = row.Material,
            ["MaterialType"] = row.MaterialType,
            ["MaterialGrade"] = row.MaterialGrade,
            ["RawMaterialValue"] = row.RawMaterialValue,
            ["MaterialSource"] = row.MaterialSource,
            ["Length"] = row.Length,
            ["Width"] = row.Width,
            ["Height"] = row.Height,
            ["VolumeM3"] = row.VolumeM3,
            ["VolumeSource"] = row.VolumeSource,
            ["CogX"] = row.CogX,
            ["CogY"] = row.CogY,
            ["CogZ"] = row.CogZ,
            ["AssemblyMemberCount"] = row.AssemblyMemberCount,
            ["TopLevelElevation"] = row.TopLevelElevation,
            ["BottomLevelElevation"] = row.BottomLevelElevation,
            ["WeightTons"] = row.WeightTons,
            ["WeightSource"] = row.WeightSource,
            ["WeightVerified"] = row.WeightVerified,
            ["WeightStatus"] = row.WeightStatus,
            ["WeightWarning"] = row.WeightWarning,
            ["IncludedInCraneOptimization"] = row.IncludedInCraneOptimization,
            ["IncludedInAnalysis"] = row.IncludedInAnalysis,
            ["AnalysisExclusionReason"] = row.AnalysisExclusionReason,
            ["MarkConfidence"] = row.MarkConfidence,
            ["ValidationStatus"] = row.ValidationStatus,
            ["ValidationWarnings"] = row.ValidationWarnings,
            ["WeightTrustLevel"] = row.WeightTrustLevel,
            ["BlockingReasons"] = row.BlockingReasons
        }
    };

    private static ReportDataRowDto ToReportRow(LiftingResultReportRowDto row)
    {
        var values = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase)
        {
            ["No"] = row.No,
            ["ElementMark"] = row.ElementMark,
            ["AssignedSetupNo"] = row.AssignedSetupNo,
            ["OperatingRadiusM"] = row.OperatingRadiusM,
            ["RequiredBoomLengthM"] = row.RequiredBoomLengthM,
            ["RequiredCapacityTons"] = row.RequiredCapacityTons,
            ["AvailableCapacityTons"] = row.AvailableCapacityTons,
            ["LiftStatus"] = row.LiftStatus
        };

        values["CapacityUtilizationPercent"] = row.CapacityUtilizationPercent.HasValue
            ? row.CapacityUtilizationPercent.Value
            : values.TryGetValue("CapacityUtilizationPercent", out var existing) && existing != null
                ? existing
                : "\u2014";

        return new ReportDataRowDto { Values = values };
    }

    private static ReportDataRowDto ToReportRow(ReportDataSourceAuditRowDto row) => new()
    {
        Values =
        {
            ["No"] = row.No,
            ["Area"] = row.Area,
            ["ReportColumn"] = row.ReportColumn,
            ["DisplayedHeader"] = row.DisplayedHeader,
            ["CurrentSource"] = row.CurrentSource,
            ["SourceObjectOrFile"] = row.SourceObjectOrFile,
            ["SourceField"] = row.SourceField,
            ["CurrentAvailability"] = row.CurrentAvailability,
            ["RowsFound"] = row.RowsFound,
            ["WhyItMayShowDash"] = row.WhyItMayShowDash,
            ["RequiredAction"] = row.RequiredAction
        }
    };

    private static ReportMetadataDto CloneMetadata(
        ReportBuildRequestDto request,
        IReadOnlyList<ReportSummaryItemDto> summary) => new()
    {
        ReportName = request.TemplateName,
        ItemsIncluded = request.ItemsIncluded,
        ProjectName = request.Metadata.ProjectName,
        ReportDate = request.Metadata.ReportDate,
        CompanyName = request.Metadata.CompanyName,
        CompanyLogoPath = request.Metadata.CompanyLogoPath,
        CreatedBy = request.Metadata.CreatedBy,
        IssuedTo = request.Metadata.IssuedTo,
        Revision = request.Metadata.Revision,
        HasGaRun = request.Metadata.HasGaRun,
        GaRunStatusText = request.Metadata.GaRunStatusText,
        GaWarningText = request.Metadata.GaWarningText,
        CraneInfo = request.Metadata.CraneInfo,
        LiftingAccessories = request.Metadata.LiftingAccessories,
        SummaryItems = summary,
        FailureBreakdownItems = request.Metadata.FailureBreakdownItems,
        GeneralNotes = request.Metadata.GeneralNotes
    };

    private static IReadOnlyList<ReportSummaryItemDto> BuildAuditSummary(IReadOnlyList<ReportDataRowDto> rows)
    {
        var gaRows = rows.Count(row =>
            row.Values.TryGetValue("Area", out var value)
            && string.Equals(value?.ToString(), "GA / Lifting", StringComparison.OrdinalIgnoreCase));
        var missingRows = rows.Count(row =>
            row.Values.TryGetValue("CurrentAvailability", out var value)
            && (value?.ToString()?.Contains("Missing", StringComparison.OrdinalIgnoreCase) == true
                || value?.ToString()?.Contains("No GA", StringComparison.OrdinalIgnoreCase) == true));

        return
        [
            Summary("Audit Rows", rows.Count.ToString(CultureInfo.InvariantCulture)),
            Summary("GA / Lifting Rows", gaRows.ToString(CultureInfo.InvariantCulture)),
            Summary("Missing Sources", missingRows.ToString(CultureInfo.InvariantCulture))
        ];
    }

    private static void EnsureSelectedColumnValues(
        IReadOnlyList<ReportDataRowDto> rows,
        IReadOnlyList<ReportColumnDefinition> columns)
    {
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                if (!row.Values.TryGetValue(column.Key, out var value) || IsMissingValue(value))
                    row.Values[column.Key] = "\u2014";
            }
        }
    }

    private static bool IsMissingValue(object? value) =>
        value == null
        || value is string text && string.IsNullOrWhiteSpace(text);

    private static void RemoveForbiddenUserFacingValues(IReadOnlyList<ReportDataRowDto> rows)
    {
        foreach (var row in rows)
        {
            foreach (var key in row.Values.Keys.ToList())
            {
                if (ReportTemplateCatalog.IsUserFacingForbiddenColumn(key))
                    row.Values.Remove(key);
            }
        }
    }

    private static ReportDataRowDto BuildNoGaRunPlaceholderRow(IReadOnlyList<ReportColumnDefinition> columns)
    {
        var row = new ReportDataRowDto();
        foreach (var column in columns)
            row.Values[column.Key] = column.Key.Equals("No", StringComparison.OrdinalIgnoreCase) ? 1 : "\u2014";

        return row;
    }

    private static void ApplySequentialNumbers(IReadOnlyList<ReportDataRowDto> rows)
    {
        for (var index = 0; index < rows.Count; index++)
            rows[index].Values["No"] = index + 1;
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildElementSummary(
        string templateName,
        IReadOnlyList<ElementReportRowDto> rows)
    {
        int total = rows.Count;
        int verified = rows.Count(row => row.WeightVerified == "Yes");
        int missing = rows.Count(row => row.WeightStatus == "Missing");
        var summary = new List<ReportSummaryItemDto>
        {
            Summary("Total Items", total.ToString(CultureInfo.InvariantCulture)),
            Summary("Verified Weights", CountPercent(verified, total)),
            Summary("Missing Weights", CountPercent(missing, total))
        };

        return summary;
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildLiftingSummary(
        IReadOnlyList<LiftingResultReportRowDto> rows,
        IReadOnlyList<CraneSetupReportRowDto> craneSetups)
    {
        int total = rows.Count;
        int liftable = rows.Count(row => IsLiftedStatus(row.LiftStatus));
        int notLifted = Math.Max(0, total - liftable);
        var utilizationValues = rows
            .Select(row => row.CapacityUtilizationPercent)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        var items = new List<ReportSummaryItemDto>
        {
            Summary("Total Items", total.ToString(CultureInfo.InvariantCulture)),
            Summary("Liftable", CountPercent(liftable, total)),
            Summary("Not Lifted", CountPercent(notLifted, total)),
            Summary("Final Setup Count", craneSetups.Count.ToString(CultureInfo.InvariantCulture))
        };

        if (utilizationValues.Count > 0)
        {
            items.Add(Summary("Average Capacity Utilization", $"{utilizationValues.Average():0.0}%"));
            items.Add(Summary("Maximum Capacity Utilization", $"{utilizationValues.Max():0.0}%"));
        }

        return items;
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildLiftingFailureBreakdown(
        IReadOnlyList<LiftingResultReportRowDto> rows)
    {
        var failures = new (string Label, Func<LiftingResultReportRowDto, bool> Predicate)[]
        {
            ("Out of Radius", row => IsFailureCategory(row, "Out of Radius")),
            ("Overweight", row => IsFailureCategory(row, "Overweight")),
            ("Height Failure", row => IsFailureCategory(row, "Height Failure")),
            ("Missing Load Chart Coverage", row => IsFailureCategory(row, "Missing Load Chart Coverage"))
        };

        var total = rows.Count;
        var items = new List<ReportSummaryItemDto>();
        foreach (var failure in failures)
        {
            var count = rows.Count(failure.Predicate);
            if (count <= 0)
                continue;

            items.Add(Summary(failure.Label, CountPercent(count, total)));
        }

        return items;
    }

    private static bool IsFailureCategory(LiftingResultReportRowDto row, string category)
    {
        if (row.Values.TryGetValue("FailureCategory", out var value)
            && string.Equals(value?.ToString(), category, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return category switch
        {
            "Out of Radius" => row.LiftStatus.Equals("Out Of Radius", StringComparison.OrdinalIgnoreCase),
            "Overweight" => row.LiftStatus.Equals("Overweight", StringComparison.OrdinalIgnoreCase),
            "Height Failure" => row.LiftStatus.Equals("Height Fail", StringComparison.OrdinalIgnoreCase),
            "Missing Load Chart Coverage" => row.LiftStatus.Equals("Missing Load Chart Coverage", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsLiftedStatus(string status) =>
        status.Equals("Liftable", StringComparison.OrdinalIgnoreCase)
        || status.Equals("Lifted", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ReportSummaryItemDto> BuildNoGaRunSummary() =>
    [
        Summary("Total Items", "0"),
        Summary("Liftable", "0"),
        Summary("Not Lifted", "0"),
        Summary("Final Setup Count", "0")
    ];

    private static ReportSummaryItemDto Summary(string label, string value) => new()
    {
        Label = label,
        Value = value
    };

    private static string CountPercent(int count, int total)
    {
        if (total <= 0)
            return count.ToString(CultureInfo.InvariantCulture);

        return $"{count} ({count * 100.0 / total:0.0}%)";
    }
}
