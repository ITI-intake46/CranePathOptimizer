using System.Globalization;
using System.IO;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CraneOptimizer.Infrastructure.Reports.UserFacing;

public sealed class UserFacingReportPdfExporter : IReportPdfExporter
{
    private const string ProductName = "Crane Optimizer";

    static UserFacingReportPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task ExportAsync(
        ReportDocumentDto report,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tempPath = Path.Combine(
                directory ?? Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

            Document.Create(container => Compose(container, report, cancellationToken))
                .GeneratePdf(tempPath);

            cancellationToken.ThrowIfCancellationRequested();
            var tempInfo = new FileInfo(tempPath);
            if (!tempInfo.Exists || tempInfo.Length == 0)
                throw new IOException($"PDF export did not produce a valid file for document {report.DocumentId}.");

            ReplaceFinalFile(tempPath, fullPath);
        }, cancellationToken);
    }

    private static void ReplaceFinalFile(string tempPath, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        File.Move(tempPath, fullPath);

        var finalInfo = new FileInfo(fullPath);
        if (!finalInfo.Exists || finalInfo.Length == 0)
            throw new IOException($"PDF export final file verification failed: {fullPath}");
    }

    private static void Compose(
        IDocumentContainer container,
        ReportDocumentDto report,
        CancellationToken cancellationToken)
    {
        container.Page(page =>
        {
            ApplyPageDefaults(page, report, landscape: false);
            page.Content().Element(content => ComposeIntroductionPageOne(content, report));
            page.Footer().Element(footer => ComposeFooter(footer, report));
        });

        if (HasIntroductionPageTwo(report))
        {
            container.Page(page =>
            {
                ApplyPageDefaults(page, report, landscape: false);
                page.Content().Element(content => ComposeIntroductionPageTwo(content, report));
                page.Footer().Element(footer => ComposeFooter(footer, report));
            });
        }

        container.Page(page =>
        {
            ApplyPageDefaults(page, report, landscape: true);
            page.Content().Element(content => ComposeMainTables(content, report, cancellationToken));
            page.Footer().Element(footer => ComposeFooter(footer, report));
        });

        if (!string.IsNullOrWhiteSpace(report.Metadata.GeneralNotes))
        {
            container.Page(page =>
            {
                ApplyPageDefaults(page, report, landscape: false);
                page.Content().Column(column =>
                {
                    column.Item().Text("General Notes").FontSize(18).Bold().FontColor(Colors.Orange.Darken2);
                    column.Item().PaddingTop(12).Text(report.Metadata.GeneralNotes).FontSize(10);
                });
                page.Footer().Element(footer => ComposeFooter(footer, report));
            });
        }
    }

    private static void ApplyPageDefaults(PageDescriptor page, ReportDocumentDto report, bool landscape)
    {
        var size = landscape ? ChooseLandscapePageSize(report) : PageSizes.A4;
        page.Size(landscape ? size.Landscape() : PageSizes.A4);
        page.MarginTop(15, Unit.Millimetre);
        page.MarginBottom(15, Unit.Millimetre);
        page.MarginLeft(12, Unit.Millimetre);
        page.MarginRight(12, Unit.Millimetre);
        page.DefaultTextStyle(style => style.FontFamily("Exo 2").FontSize((float)Math.Clamp(report.TextSizePt, 7, 32)));
    }

    private static PageSize ChooseLandscapePageSize(ReportDocumentDto report)
    {
        var totalWeight = report.Columns.Sum(column => GetPdfColumnWeight(column));
        var textScale = Math.Max(1.0, Math.Clamp(report.TextSizePt, 7, 32) / 10.0);
        var requiredWeight = totalWeight * textScale;

        if (requiredWeight <= 12.0 && !report.PreferA3Landscape)
            return PageSizes.A4;
        if (requiredWeight <= 18.0)
            return PageSizes.A3;
        if (requiredWeight <= 28.0)
            return PageSizes.A2;
        if (requiredWeight <= 40.0)
            return PageSizes.A1;

        var customLandscapeWidthMm = (float)Math.Clamp(841 + ((requiredWeight - 40.0) * 24.0), 842.0, 2000.0);
        return new PageSize(594, customLandscapeWidthMm, Unit.Millimetre);
    }

    private static bool HasIntroductionPageTwo(ReportDocumentDto report) =>
        report.Metadata.CraneInfo != null
        || report.Metadata.LiftingAccessories != null
        || report.CraneSetups.Count > 0;

    private static void ComposeIntroductionPageOne(IContainer container, ReportDocumentDto report)
    {
        var metadata = report.Metadata;

        container.Column(column =>
        {
            column.Spacing(12);

            column.Item().Row(row =>
            {
                if (!string.IsNullOrWhiteSpace(metadata.CompanyLogoPath) && File.Exists(metadata.CompanyLogoPath))
                {
                    row.ConstantItem(80).Height(45).Image(metadata.CompanyLogoPath).FitArea();
                }

                row.RelativeItem().Column(header =>
                {
                    header.Item().Text(ProductName)
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Orange.Darken2);
                    header.Item().Text(metadata.ReportName).FontSize(14).SemiBold();
                });
            });

            column.Item().Element(item => ComposeKeyValueBlock(item, "Report Information",
                BuildReportInformationRows(report)));

            if (!string.IsNullOrWhiteSpace(metadata.GaWarningText))
            {
                column.Item().PaddingTop(6).Background(Colors.Orange.Lighten4).Padding(6)
                    .Text(metadata.GaWarningText).FontSize(9).FontColor(Colors.Orange.Darken4);
            }

            column.Item().Element(item => ComposeSummary(item, metadata.SummaryItems));
            if (metadata.FailureBreakdownItems.Count > 0)
                column.Item().ShowEntire().Element(item => ComposeKeyValueBlock(item, "Failure Breakdown",
                    metadata.FailureBreakdownItems.Select(summary => (summary.Label, (string?)summary.Value))));
        });
    }

    private static void ComposeIntroductionPageTwo(IContainer container, ReportDocumentDto report)
    {
        var metadata = report.Metadata;

        container.Column(column =>
        {
            column.Spacing(14);

            if (metadata.CraneInfo != null)
                column.Item().ShowEntire().Element(item => ComposeKeyValueBlock(item, "Crane / Analysis Information", BuildCraneInformationRows(metadata.CraneInfo)));

            if (metadata.LiftingAccessories != null)
                column.Item().ShowEntire().Element(item => ComposeKeyValueBlock(item, "Lifting Accessories", BuildLiftingAccessoriesRows(metadata.LiftingAccessories)));

            column.Item().ShowEntire().Element(ComposeCapacityNote);

            if (report.CraneSetups.Count > 0)
                column.Item().ShowEntire().Element(item => ComposeCraneSetupTable(item, "Final GA Setup Points", report.CraneSetups));
        });
    }

    private static void ComposeKeyValueBlock(
        IContainer container,
        string title,
        IEnumerable<(string Label, string? Value)> values)
    {
        var cleanValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .ToList();

        container.Column(column =>
        {
            column.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(115);
                    columns.RelativeColumn();
                });

                foreach (var value in cleanValues)
                {
                    table.Cell().PaddingVertical(2).Text(value.Label).SemiBold();
                    table.Cell().PaddingVertical(2).Text(value.Value);
                }
            });
        });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyList<ReportSummaryItemDto> summary)
    {
        container.Column(column =>
        {
            column.Item().Text("Summary").FontSize(12).Bold().FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                foreach (var item in summary)
                {
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Label).SemiBold();
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Value);
                }
            });
        });
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildReportInformationRows(ReportDocumentDto report)
    {
        var metadata = report.Metadata;
        var rows = new List<(string Label, string? Value)>
        {
            ("Report Reference", report.ReportReference),
            ("Report Name", metadata.ReportName),
            ("Items Included", metadata.ItemsIncluded),
            ("Project", metadata.ProjectName),
            ("Created On", metadata.ReportDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
        };
        return rows;
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildCraneInformationRows(CraneReportInfoDto crane) =>
    [
        ("Model", FirstNonEmptyNullable(crane.ModelName, crane.CraneModelOrSpecification, crane.CraneName)),
        ("Specification", crane.SpecificationName),
        ("Manufacturer", crane.Manufacturer),
        ("Crane Type", crane.CraneType),
        ("Counterweight", crane.CounterweightTons.HasValue ? $"{crane.CounterweightTons.Value:0.##} tons" : null),
        ("Outrigger Configuration", crane.OutriggerConfiguration),
        ("Working Radius", crane.WorkingRadiusRangeText),
        ("Boom Length", crane.BoomLengthRangeText),
        ("Maximum Capacity", crane.MaximumCapacityTons.HasValue ? $"{crane.MaximumCapacityTons.Value:0.##} tons" : null),
        ("Safety Factor", crane.SafetyFactorText),
        ("Derating Ratio", crane.DeratingRatioText),
        ("Crane Hoist Wire Length", crane.CraneHoistWireLengthText)
    ];

    private static IReadOnlyList<(string Label, string? Value)> BuildLiftingAccessoriesRows(LiftingAccessoriesInfoDto accessories) =>
    [
        ("Wire Rope Length", FormatMetersOrNotProvided(accessories.RiggingWireRopeLengthM)),
        ("Total Lifting Accessories Weight", $"{accessories.VisibleTotalLiftingAccessoriesWeightTons:0.00} tons")
    ];

    private static void ComposeCapacityNote(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Capacity Note").FontSize(12).Bold().FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(6)
                .Text("Adjusted Allowable Capacity includes the selected safety factor and derating ratio.")
                .FontSize(9);
        });
    }

    private static void ComposeCraneSetupTable(IContainer container, string title, IReadOnlyList<CraneSetupReportRowDto> setups)
    {
        var showZone = ShouldShowZone(setups);

        container.Column(column =>
        {
            column.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    if (showZone)
                        columns.RelativeColumn();
                });

                var headers = showZone
                    ? new[] { "Setup Reference", "X (m)", "Y (m)", "Z (m)", "Assigned Lift Count", "Assigned Percentage", "Zone" }
                    : new[] { "Setup Reference", "X (m)", "Y (m)", "Z (m)", "Assigned Lift Count", "Assigned Percentage" };

                foreach (var header in headers)
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(header).SemiBold();

                foreach (var setup in setups)
                {
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(string.IsNullOrWhiteSpace(setup.SetupReference) ? $"Point_{setup.SetupNo}" : setup.SetupReference);
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneX:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneY:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneZ:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(setup.LiftedItemsCount.ToString(CultureInfo.InvariantCulture));
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.LiftedItemsPercentage:0.0}%");
                    if (showZone)
                        table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(string.IsNullOrWhiteSpace(setup.Zone) ? "-" : setup.Zone);
                }
            });
        });
    }

    private static void ComposeMainTables(
        IContainer container,
        ReportDocumentDto report,
        CancellationToken cancellationToken)
    {
        if (report.InternalFamily == ReportFamily.LiftingResult
            && report.Metadata.ReportName.Equals(ReportTemplateCatalog.GaResultReport, StringComparison.OrdinalIgnoreCase))
        {
            ComposeGaResultSections(container, report, cancellationToken);
            return;
        }

        container.Column(column =>
        {
            column.Item().Element(content => ComposeMainTable(content, report.Columns, report.Rows, cancellationToken));
        });
    }

    private static void ComposeGaResultSections(
        IContainer container,
        ReportDocumentDto report,
        CancellationToken cancellationToken)
    {
        container.Column(column =>
        {
            column.Spacing(14);

            foreach (var setup in report.CraneSetups.OrderBy(setup => setup.SetupNo))
            {
                var rows = GetSuccessfulRowsForSetup(report, setup);
                column.Item().ShowEntire().Element(content => ComposeSetupSection(content, setup));
                column.Item().Element(content => ComposeMainTable(content, GetSetupColumns(report), rows, cancellationToken));
            }

            column.Item().PaddingTop(10).Text("Not Lifted / Failed Lift Units")
                .FontSize(14)
                .Bold()
                .FontColor(Colors.Orange.Darken2);
            column.Item().Element(content => ComposeMainTable(content, GetFailedColumns(report), GetFailedRows(report), cancellationToken));
        });
    }

    private static void ComposeSetupSection(IContainer container, CraneSetupReportRowDto setup)
    {
        container.Column(column =>
        {
            column.Item().Text($"Setup {FirstNonEmpty(setup.SetupReference, $"Point_{setup.SetupNo}")}")
                .FontSize(14)
                .Bold()
                .FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(3).Text(
                $"Coordinates: X {setup.CraneX:0.00} m, Y {setup.CraneY:0.00} m, Z {setup.CraneZ:0.00} m").FontSize(9);
            column.Item().Text(
                $"Assigned Lift Units: {setup.LiftedItemsCount} | Assigned Percentage: {setup.LiftedItemsPercentage:0.0}%{FormatSetupZone(setup)}").FontSize(9);
        });
    }

    private static bool ShouldShowZone(IReadOnlyList<CraneSetupReportRowDto> setups) =>
        setups.Any(setup => !string.IsNullOrWhiteSpace(setup.Zone)
            && setup.Zone.Trim() is not "-" and not "\u2014");

    private static string FormatSetupZone(CraneSetupReportRowDto setup) =>
        string.IsNullOrWhiteSpace(setup.Zone) || setup.Zone.Trim() is "-" or "\u2014"
            ? string.Empty
            : $" | Zone: {setup.Zone.Trim()}";

    private static IReadOnlyList<ReportColumnDefinition> GetSetupColumns(ReportDocumentDto report) =>
        SelectColumns(report,
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
            "CapacityUtilizationPercent"
        ]);

    private static IReadOnlyList<ReportColumnDefinition> GetFailedColumns(ReportDocumentDto report) =>
        SelectColumns(report,
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
        ]);

    private static IReadOnlyList<ReportColumnDefinition> SelectColumns(ReportDocumentDto report, IReadOnlyList<string> keys)
    {
        var catalog = ReportTemplateCatalog.GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.GaResultReport)
            .Columns
            .Concat(report.Columns)
            .GroupBy(column => column.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return keys
            .Where(key => !ReportTemplateCatalog.IsUserFacingForbiddenColumn(key))
            .Select((key, index) =>
            {
                if (catalog.TryGetValue(key, out var column))
                {
                    return new ReportColumnDefinition
                    {
                        Key = column.Key,
                        Header = column.Header,
                        ValueKind = column.ValueKind,
                        EvidenceScope = column.EvidenceScope,
                        SupportedFamilies = column.SupportedFamilies,
                        RequiredCapabilities = column.RequiredCapabilities,
                        ShowInPreview = column.ShowInPreview,
                        ShowInPdf = column.ShowInPdf,
                        ShowInExcel = column.ShowInExcel,
                        IsSelected = true,
                        Order = index,
                        Format = column.Format,
                        Alignment = column.Alignment,
                        TextSize = column.TextSize,
                        Category = column.Category,
                        Unit = column.Unit
                    };
                }

                return new ReportColumnDefinition
                {
                    Key = key,
                    Header = key,
                    ValueKind = ReportColumnValueKind.Text,
                    SupportedFamilies = [ReportFamily.LiftingResult],
                    IsSelected = true,
                    Order = index
                };
            })
            .ToList();
    }

    private static IReadOnlyList<ReportDataRowDto> GetSuccessfulRowsForSetup(ReportDocumentDto report, CraneSetupReportRowDto setup) =>
        ApplySectionDisplayNumbers(report.Rows
            .Where(IsSuccessfulLift)
            .Where(row => TryGetSetupNo(row, out var setupNo) && setupNo == setup.SetupNo)
            .ToList());

    private static IReadOnlyList<ReportDataRowDto> GetFailedRows(ReportDocumentDto report) =>
        ApplySectionDisplayNumbers(report.Rows.Where(row => !IsSuccessfulLift(row)).ToList());

    private static IReadOnlyList<ReportDataRowDto> ApplySectionDisplayNumbers(IReadOnlyList<ReportDataRowDto> rows) =>
        rows
            .Select((row, index) =>
            {
                var values = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase)
                {
                    ["No"] = index + 1
                };

                return new ReportDataRowDto { Values = values };
            })
            .ToList();

    private static bool IsSuccessfulLift(ReportDataRowDto row)
    {
        var isLifted = GetText(row, "IsLifted");
        if (isLifted.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            return true;

        var status = GetText(row, "LiftStatus");
        return (status.Equals("Liftable", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Lifted", StringComparison.OrdinalIgnoreCase))
            && TryGetSetupNo(row, out var setupNo)
            && setupNo > 0;
    }

    private static bool TryGetSetupNo(ReportDataRowDto row, out int setupNo)
    {
        setupNo = 0;
        foreach (var key in new[] { "AssignedSetupNo", "AssignedSetupIndex" })
        {
            if (!row.Values.TryGetValue(key, out var value) || value == null)
                continue;

            if (value is int intValue)
            {
                setupNo = intValue;
                return true;
            }

            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out setupNo))
                return true;
        }

        return false;
    }

    private static string GetText(ReportDataRowDto row, string key) =>
        row.Values.TryGetValue(key, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    private static void ComposeMainTable(
        IContainer container,
        IReadOnlyList<ReportColumnDefinition> columns,
        IReadOnlyList<ReportDataRowDto> rows,
        CancellationToken cancellationToken)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                foreach (var column in columns)
                    definition.RelativeColumn(GetPdfColumnWeight(column));
            });

            table.Header(header =>
            {
                foreach (var column in columns)
                    header.Cell().Background(Colors.Grey.Lighten3).Border(0.25f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(column.Header).SemiBold().FontSize(GetBodyFontSize(column));
            });

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var column in columns)
                {
                    row.Values.TryGetValue(column.Key, out var value);
                    var cell = table.Cell().ShowEntire().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                    ApplyAlignment(cell, column).Text(FormatValue(value, column)).FontSize(GetBodyFontSize(column));
                }
            }
        });
    }

    private static float GetBodyFontSize(ReportColumnDefinition column) =>
        (float)Math.Max(7.0, ParseTextSize(column.TextSize));

    private static float GetPdfColumnWeight(ReportColumnDefinition column)
    {
        if (column.Key.Equals("No", StringComparison.OrdinalIgnoreCase))
            return 0.55f;

        if (column.Key.Contains("Mark", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            return 2.4f;
        }

        if (column.Key.Contains("Warning", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Reason", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Action", StringComparison.OrdinalIgnoreCase))
        {
            return 2.1f;
        }

        if (column.Key.Contains("Optimization", StringComparison.OrdinalIgnoreCase))
            return 2.0f;

        if (column.Key.Contains("BoomLength", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Boom Length", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("CapacityUtilization", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Capacity Utilization", StringComparison.OrdinalIgnoreCase))
        {
            return 1.8f;
        }

        if (column.Key.Contains("Capacity", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Margin", StringComparison.OrdinalIgnoreCase))
        {
            return 1.55f;
        }

        return column.ValueKind switch
        {
            ReportColumnValueKind.Status or ReportColumnValueKind.YesNo => 1.4f,
            ReportColumnValueKind.Number or ReportColumnValueKind.Meters or ReportColumnValueKind.Tons or ReportColumnValueKind.Percentage => 1.25f,
            _ => 1.5f
        };
    }

    private static double ParseTextSize(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            return Math.Clamp(parsed, 6, 32);
        }

        return value switch
        {
            "Small" => 7,
            "Large" => 12,
            _ => 10
        };
    }

    private static IContainer ApplyAlignment(IContainer cell, ReportColumnDefinition column)
    {
        return column.ValueKind switch
        {
            _ when column.Alignment?.Equals("Right", StringComparison.OrdinalIgnoreCase) == true => cell.AlignRight(),
            _ when column.Alignment?.Equals("Center", StringComparison.OrdinalIgnoreCase) == true => cell.AlignCenter(),
            _ => cell.AlignLeft()
        };
    }

    private static void ComposeFooter(IContainer container, ReportDocumentDto report)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(ProductName).FontSize(8);
            row.RelativeItem().AlignCenter().Text(report.Metadata.ReportName).FontSize(8);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static string Shorten(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        var text = value.Trim();
        return text.Length <= length ? text : text[..length];
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "-";

    private static string? FirstNonEmptyNullable(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string FormatValue(object? value, ReportColumnDefinition column)
    {
        if (value == null)
            return column.ValueKind == ReportColumnValueKind.Percentage ? "N/A" : string.Empty;

        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                return "N/A";

            var converted = ConvertUnit(d, column);
            return column.ValueKind switch
            {
                ReportColumnValueKind.Percentage => $"{converted:0.0}%",
                ReportColumnValueKind.Tons when column.Unit == "kg" => converted.ToString("0", CultureInfo.InvariantCulture),
                ReportColumnValueKind.Meters when column.Unit == "mm" => converted.ToString("0", CultureInfo.InvariantCulture),
                ReportColumnValueKind.Tons when converted is > 0 and < 0.01 => converted.ToString("0.0000", CultureInfo.InvariantCulture),
                ReportColumnValueKind.Tons or ReportColumnValueKind.Meters or ReportColumnValueKind.Number => converted.ToString("0.00", CultureInfo.InvariantCulture),
                _ => converted.ToString("0.##", CultureInfo.InvariantCulture)
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private static double ConvertUnit(double value, ReportColumnDefinition column)
    {
        if (column.ValueKind == ReportColumnValueKind.Meters && column.Unit == "mm")
            return value * 1000.0;

        if (column.ValueKind == ReportColumnValueKind.Tons && column.Unit == "kg")
            return value * 1000.0;

        return value;
    }

    private static string FormatMetersOrNotProvided(double? value) =>
        value.HasValue ? $"{value.Value:0.00} m" : "Not Provided";
}
