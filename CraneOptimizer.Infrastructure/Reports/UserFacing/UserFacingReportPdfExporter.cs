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
            Document.Create(container => Compose(container, report, cancellationToken))
                .GeneratePdf(fullPath);
        }, cancellationToken);
    }

    private static void Compose(
        IDocumentContainer container,
        ReportDocumentDto report,
        CancellationToken cancellationToken)
    {
        container.Page(page =>
        {
            ApplyPageDefaults(page, report, landscape: false);
            page.Content().Element(content => ComposeMetadataPage(content, report));
            page.Footer().Element(footer => ComposeFooter(footer, report));
        });

        container.Page(page =>
        {
            ApplyPageDefaults(page, report, landscape: true);
            page.Content().Element(content => ComposeMainTable(content, report, cancellationToken));
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
        var size = report.PreferA3Landscape ? PageSizes.A3 : PageSizes.A4;
        page.Size(landscape ? size.Landscape() : PageSizes.A4);
        page.MarginTop(15, Unit.Millimetre);
        page.MarginBottom(15, Unit.Millimetre);
        page.MarginLeft(12, Unit.Millimetre);
        page.MarginRight(12, Unit.Millimetre);
        page.DefaultTextStyle(style => style.FontFamily("Exo 2").FontSize((float)Math.Clamp(report.TextSizePt, 6, 32)));
    }

    private static void ComposeMetadataPage(IContainer container, ReportDocumentDto report)
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
                    header.Item().Text(string.IsNullOrWhiteSpace(metadata.CompanyName) ? "Mobile Crane Path Optimizer" : metadata.CompanyName)
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Orange.Darken2);
                    header.Item().Text(metadata.ReportName).FontSize(14).SemiBold();
                });
            });

            column.Item().Element(item => ComposeKeyValueBlock(item, "Report Information",
                BuildReportInformationRows(metadata)));

            if (!string.IsNullOrWhiteSpace(metadata.GaWarningText))
            {
                column.Item().PaddingTop(6).Background(Colors.Orange.Lighten4).Padding(6)
                    .Text(metadata.GaWarningText).FontSize(9).FontColor(Colors.Orange.Darken4);
            }

            column.Item().Element(item => ComposeSummary(item, metadata.SummaryItems));

            if (metadata.CraneInfo != null)
            {
                var crane = metadata.CraneInfo;
                column.Item().Element(item => ComposeKeyValueBlock(item, "Crane Information",
                [
                    ("Crane", crane.CraneName),
                    ("Manufacturer", crane.Manufacturer),
                    ("Load Chart", crane.LoadChartName),
                    ("Working Radius", crane.WorkingRadiusRangeText),
                    ("Boom Length", crane.BoomLengthRangeText),
                    ("Maximum Capacity", crane.MaximumCapacityTons.HasValue ? $"{crane.MaximumCapacityTons.Value:0.##} tons" : null),
                    ("Capacity Factor", crane.CapacityFactor.ToString("0.00", CultureInfo.InvariantCulture))
                ]));
            }

            if (metadata.LiftingAccessories != null)
            {
                var accessories = metadata.LiftingAccessories;
                column.Item().Element(item => ComposeKeyValueBlock(item, "Lifting Accessories",
                [
                    ("Hook Block Weight", FormatTons(accessories.HookBlockWeightTons)),
                    ("Rigging Weight", FormatTons(accessories.RiggingWeightTons)),
                    ("Slings Weight", FormatTons(accessories.SlingsWeightTons)),
                    ("Spreader Beam Weight", FormatTons(accessories.SpreaderBeamWeightTons)),
                    ("Additional Weight", FormatTons(accessories.AdditionalWeightTons)),
                    ("Total Additional Weight", $"{accessories.TotalAdditionalWeightTons:0.00} tons")
                ]));
            }

            if (report.CraneSetups.Count > 0)
                column.Item().Element(item => ComposeCraneSetupTable(item, report.CraneSetups));
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

    private static IReadOnlyList<(string Label, string? Value)> BuildReportInformationRows(ReportMetadataDto metadata)
    {
        var rows = new List<(string Label, string? Value)>
        {
            ("Report Name", metadata.ReportName),
            ("Items Included", metadata.ItemsIncluded),
            ("Project", metadata.ProjectName),
            ("Generated On", metadata.ReportDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(metadata.GaRunStatusText))
            rows.Add(("GA Run", metadata.GaRunStatusText));

        rows.Add(("Created By", metadata.CreatedBy));
        rows.Add(("Issued To", metadata.IssuedTo));
        rows.Add(("Revision", metadata.Revision));
        return rows;
    }

    private static void ComposeCraneSetupTable(IContainer container, IReadOnlyList<CraneSetupReportRowDto> setups)
    {
        container.Column(column =>
        {
            column.Item().Text("Crane Setups").FontSize(12).Bold().FontColor(Colors.Orange.Darken2);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(55);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                foreach (var header in new[] { "Setup No.", "Crane X", "Crane Y", "Crane Z", "Lifted Items", "Lifted %" })
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(header).SemiBold();

                foreach (var setup in setups)
                {
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(setup.SetupNo.ToString(CultureInfo.InvariantCulture));
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneX:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneY:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.CraneZ:0.00}");
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(setup.LiftedItemsCount.ToString(CultureInfo.InvariantCulture));
                    table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{setup.LiftedItemsPercentage:0.0}%");
                }
            });
        });
    }

    private static void ComposeMainTable(
        IContainer container,
        ReportDocumentDto report,
        CancellationToken cancellationToken)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in report.Columns)
                    columns.RelativeColumn(GetPdfColumnWeight(column));
            });

            table.Header(header =>
            {
                foreach (var column in report.Columns)
                    header.Cell().Background(Colors.Grey.Lighten3).Border(0.25f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(column.Header).SemiBold().FontSize(GetBodyFontSize(column));
            });

            foreach (var row in report.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var column in report.Columns)
                {
                    row.Values.TryGetValue(column.Key, out var value);
                    var cell = table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                    ApplyAlignment(cell, column).Text(FormatValue(value, column)).FontSize(GetBodyFontSize(column));
                }
            }
        });
    }

    private static float GetBodyFontSize(ReportColumnDefinition column) =>
        (float)ParseTextSize(column.TextSize);

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
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(report.Metadata.CompanyName)
                ? "Mobile Crane Path Optimizer"
                : report.Metadata.CompanyName).FontSize(8);
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

    private static string? FormatTons(double? value) => value.HasValue ? $"{value.Value:0.00} tons" : null;
}
