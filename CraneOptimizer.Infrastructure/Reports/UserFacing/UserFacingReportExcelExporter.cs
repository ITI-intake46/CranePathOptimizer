using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;

namespace CraneOptimizer.Infrastructure.Reports.UserFacing;

public sealed class UserFacingReportExcelExporter : IReportExcelExporter
{
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
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            using var archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
            AddEntry(archive, "[Content_Types].xml", BuildContentTypes(report));
            AddEntry(archive, "_rels/.rels", RootRelationships);
            AddEntry(archive, "xl/workbook.xml", BuildWorkbook(report));
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(report));
            AddEntry(archive, "xl/styles.xml", BuildStylesXml());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildReportInfoSheet(report));
            AddEntry(archive, "xl/worksheets/sheet2.xml", BuildReportDataSheet(report));

            var sheetIndex = 3;
            if (report.CraneSetups.Count > 0)
            {
                AddEntry(archive, $"xl/worksheets/sheet{sheetIndex}.xml", BuildCraneSetupsSheet(report));
                sheetIndex++;
            }

            if (!string.IsNullOrWhiteSpace(report.Metadata.GeneralNotes))
                AddEntry(archive, $"xl/worksheets/sheet{sheetIndex}.xml", BuildNotesSheet(report));
        }, cancellationToken);
    }

    private static void AddEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }

    private static string BuildContentTypes(ReportDocumentDto report)
    {
        var sheetCount = 2 + (report.CraneSetups.Count > 0 ? 1 : 0) + (!string.IsNullOrWhiteSpace(report.Metadata.GeneralNotes) ? 1 : 0);
        var overrides = new StringBuilder();
        for (int i = 1; i <= sheetCount; i++)
        {
            overrides.Append($"""
                <Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                """);
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
              {overrides}
            </Types>
            """;
    }

    private static string BuildWorkbook(ReportDocumentDto report)
    {
        var sheets = new List<string>
        {
            """<sheet name="Report Info" sheetId="1" r:id="rId1"/>""",
            """<sheet name="Report Data" sheetId="2" r:id="rId2"/>"""
        };

        var nextId = 3;
        if (report.CraneSetups.Count > 0)
            sheets.Add($"""<sheet name="Crane Setups" sheetId="{nextId}" r:id="rId{nextId++}"/>""");

        if (!string.IsNullOrWhiteSpace(report.Metadata.GeneralNotes))
            sheets.Add($"""<sheet name="Notes" sheetId="{nextId}" r:id="rId{nextId}"/>""");

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                {string.Join(Environment.NewLine, sheets)}
              </sheets>
            </workbook>
            """;
    }

    private static string BuildWorkbookRelationships(ReportDocumentDto report)
    {
        int sheetCount = 2 + (report.CraneSetups.Count > 0 ? 1 : 0) + (!string.IsNullOrWhiteSpace(report.Metadata.GeneralNotes) ? 1 : 0);
        var relationships = new StringBuilder();
        for (int i = 1; i <= sheetCount; i++)
            relationships.AppendLine($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");

        relationships.AppendLine($"""<Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              {relationships}
            </Relationships>
            """;
    }

    private static string BuildReportInfoSheet(ReportDocumentDto report)
    {
        var rows = new List<IReadOnlyList<CellValue>>
        {
            Row("Company Name", report.Metadata.CompanyName),
            Row("Report Name", report.Metadata.ReportName),
            Row("Items Included", report.Metadata.ItemsIncluded),
            Row("Project", report.Metadata.ProjectName),
            Row("Generated On", report.Metadata.ReportDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(report.Metadata.GaRunStatusText))
            rows.Add(Row("GA Run", report.Metadata.GaRunStatusText));

        if (!string.IsNullOrWhiteSpace(report.Metadata.GaWarningText))
            rows.Add(Row("GA Warning", report.Metadata.GaWarningText));

        rows.AddRange([
            Row("Created By", report.Metadata.CreatedBy),
            Row("Issued To", report.Metadata.IssuedTo),
            Row("Revision", report.Metadata.Revision),
            Row("", ""),
            Row("Summary", "")
        ]);

        rows.AddRange(report.Metadata.SummaryItems.Select(item => Row(item.Label, item.Value)));

        if (report.Metadata.CraneInfo != null)
        {
            var crane = report.Metadata.CraneInfo;
            rows.Add(Row("", ""));
            rows.Add(Row("Crane Information", ""));
            rows.Add(Row("Crane", crane.CraneName));
            rows.Add(Row("Manufacturer", crane.Manufacturer));
            rows.Add(Row("Load Chart", crane.LoadChartName));
            rows.Add(Row("Working Radius", crane.WorkingRadiusRangeText));
            rows.Add(Row("Boom Length", crane.BoomLengthRangeText));
            rows.Add(Row("Maximum Capacity", crane.MaximumCapacityTons.HasValue ? $"{crane.MaximumCapacityTons.Value:0.##} tons" : null));
            rows.Add(Row("Capacity Factor", crane.CapacityFactor.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        if (report.Metadata.LiftingAccessories != null)
        {
            rows.Add(Row("", ""));
            rows.Add(Row("Lifting Accessories", ""));
            rows.Add(Row("Total Additional Weight", $"{report.Metadata.LiftingAccessories.TotalAdditionalWeightTons:0.00} tons"));
        }

        return BuildSheet(rows, freezeHeader: false, autoFilter: false);
    }

    private static string BuildReportDataSheet(ReportDocumentDto report)
    {
        var rows = new List<IReadOnlyList<CellValue>>
        {
            report.Columns.Select(column => CellValue.ForText(column.Header, style: HeaderStyleFor(column))).ToList()
        };

        foreach (var reportRow in report.Rows)
        {
            rows.Add(report.Columns
                .Select(column =>
                {
                    reportRow.Values.TryGetValue(column.Key, out var value);
                    return ToCell(value, column);
                })
                .ToList());
        }

        return BuildSheet(
            rows,
            freezeHeader: true,
            autoFilter: true,
            columnWidths: report.Columns.Select(ResolveColumnWidth).ToList());
    }

    private static string BuildCraneSetupsSheet(ReportDocumentDto report)
    {
        var rows = new List<IReadOnlyList<CellValue>>
        {
            new[]
            {
                CellValue.ForText("Setup No.", 1),
                CellValue.ForText("Crane X", 1),
                CellValue.ForText("Crane Y", 1),
                CellValue.ForText("Crane Z", 1),
                CellValue.ForText("Lifted Items Count", 1),
                CellValue.ForText("Lifted Items Percentage", 1)
            }
        };

        rows.AddRange(report.CraneSetups.Select(setup => new[]
        {
            CellValue.ForNumber(setup.SetupNo, 2),
            CellValue.ForNumber(setup.CraneX, 2),
            CellValue.ForNumber(setup.CraneY, 2),
            CellValue.ForNumber(setup.CraneZ, 2),
            CellValue.ForNumber(setup.LiftedItemsCount, 2),
            CellValue.ForNumber(setup.LiftedItemsPercentage / 100.0, 3)
        }));

        return BuildSheet(rows, freezeHeader: true, autoFilter: true);
    }

    private static string BuildNotesSheet(ReportDocumentDto report) =>
        BuildSheet(
            [Row("General Notes", ""), Row(report.Metadata.GeneralNotes, "")],
            freezeHeader: false,
            autoFilter: false,
            columnWidths: [60, 18]);

    private static string BuildSheet(
        IReadOnlyList<IReadOnlyList<CellValue>> rows,
        bool freezeHeader,
        bool autoFilter,
        IReadOnlyList<double>? columnWidths = null)
    {
        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        if (freezeHeader)
        {
            xml.AppendLine("""
                <sheetViews>
                  <sheetView workbookViewId="0" rightToLeft="0">
                    <pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>
                  </sheetView>
                </sheetViews>
                """);
        }
        else
        {
            xml.AppendLine("""
                <sheetViews>
                  <sheetView workbookViewId="0" rightToLeft="0"/>
                </sheetViews>
                """);
        }

        xml.AppendLine("""<cols>""");
        int maxColumns = rows.Count == 0 ? 1 : rows.Max(row => row.Count);
        for (int i = 1; i <= maxColumns; i++)
        {
            var width = columnWidths != null && i <= columnWidths.Count
                ? columnWidths[i - 1]
                : 22;
            xml.AppendLine($"""<col min="{i}" max="{i}" width="{width.ToString("0.##", CultureInfo.InvariantCulture)}" customWidth="1"/>""");
        }
        xml.AppendLine("""</cols>""");

        xml.AppendLine("<sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            xml.AppendLine($"""<row r="{r + 1}">""");
            for (int c = 0; c < rows[r].Count; c++)
                xml.Append(BuildCell(r + 1, c + 1, rows[r][c]));
            xml.AppendLine("</row>");
        }
        xml.AppendLine("</sheetData>");

        if (autoFilter && rows.Count > 0 && maxColumns > 0)
            xml.AppendLine($"""<autoFilter ref="A1:{ColumnName(maxColumns)}{Math.Max(1, rows.Count)}"/>""");

        xml.AppendLine("</worksheet>");
        return xml.ToString();
    }

    private static string BuildCell(int row, int column, CellValue value)
    {
        var reference = $"{ColumnName(column)}{row}";
        var style = value.Style > 0 ? $" s=\"{value.Style}\"" : string.Empty;

        if (value.IsNumber)
            return $"""<c r="{reference}"{style}><v>{Convert.ToString(value.Number, CultureInfo.InvariantCulture)}</v></c>""";

        return $"""<c r="{reference}" t="inlineStr"{style}><is><t>{Escape(value.Text)}</t></is></c>""";
    }

    private static CellValue ToCell(object? value, ReportColumnDefinition column)
    {
        if (value is null)
            return CellValue.ForText(column.ValueKind == ReportColumnValueKind.Percentage ? "N/A" : string.Empty);

        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                return CellValue.ForText("N/A");

            return column.ValueKind switch
            {
                ReportColumnValueKind.Percentage => CellValue.ForNumber(d / 100.0, PercentStyleFor(column)),
                ReportColumnValueKind.Tons when column.Unit == "kg" => CellValue.ForNumber(d * 1000.0, WholeNumberStyleFor(column)),
                ReportColumnValueKind.Meters when column.Unit == "mm" => CellValue.ForNumber(d * 1000.0, WholeNumberStyleFor(column)),
                ReportColumnValueKind.Number or ReportColumnValueKind.Tons or ReportColumnValueKind.Meters => CellValue.ForNumber(d, NumberStyleFor(column)),
                _ => CellValue.ForText(d.ToString("0.##", CultureInfo.InvariantCulture), TextStyleFor(column))
            };
        }

        if (value is int i)
            return CellValue.ForNumber(i, NumberStyleFor(column));

        return CellValue.ForText(value.ToString() ?? string.Empty, TextStyleFor(column));
    }

    private static double ResolveColumnWidth(ReportColumnDefinition column)
    {
        if (column.Key.Equals("No", StringComparison.OrdinalIgnoreCase))
            return 8;

        if (column.Key.Contains("Mark", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            return 42;
        }

        if (column.Key.Contains("Warning", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Reason", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Action", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Candidates", StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        if (column.Key.Contains("Optimization", StringComparison.OrdinalIgnoreCase))
            return 34;

        if (column.Key.Contains("BoomLength", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Boom Length", StringComparison.OrdinalIgnoreCase))
        {
            return 28;
        }

        if (column.Key.Contains("CapacityUtilization", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Capacity Utilization", StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        if (column.Key.Contains("Capacity", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Margin", StringComparison.OrdinalIgnoreCase))
        {
            return 24;
        }

        if (column.Key.Contains("Source", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Revit", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("ElementId", StringComparison.OrdinalIgnoreCase))
        {
            return 24;
        }

        return column.ValueKind switch
        {
            ReportColumnValueKind.Status or ReportColumnValueKind.YesNo => 22,
            ReportColumnValueKind.Percentage => 22,
            ReportColumnValueKind.Number or ReportColumnValueKind.Meters or ReportColumnValueKind.Tons => 18,
            _ => 24
        };
    }

    private static int HeaderStyleFor(ReportColumnDefinition column) => StyleBaseFor(column) + 0;
    private static int TextStyleFor(ReportColumnDefinition column) => StyleBaseFor(column) + 1;
    private static int NumberStyleFor(ReportColumnDefinition column) => StyleBaseFor(column) + 2;
    private static int PercentStyleFor(ReportColumnDefinition column) => StyleBaseFor(column) + 3;
    private static int WholeNumberStyleFor(ReportColumnDefinition column) => StyleBaseFor(column) + 4;

    private static int StyleBaseFor(ReportColumnDefinition column)
    {
        var size = (int)Math.Round(ParseTextSize(column.TextSize));
        return 4 + (((size - 6) * 3) + AlignmentIndex(column.Alignment)) * 5;
    }

    private static int AlignmentIndex(string? alignment) => alignment switch
    {
        "Center" => 1,
        "Right" => 2,
        _ => 0
    };

    private static string AlignmentName(int alignmentIndex) => alignmentIndex switch
    {
        1 => "center",
        2 => "right",
        _ => "left"
    };

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

    private static IReadOnlyList<CellValue> Row(string? left, string? right) =>
        [CellValue.ForText(left ?? string.Empty), CellValue.ForText(right ?? string.Empty)];

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }

        return name;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sanitized = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsOpenXmlTextCharacter(rune.Value))
                sanitized.Append(rune);
        }

        return SecurityElement.Escape(sanitized.ToString()) ?? string.Empty;
    }

    private static bool IsOpenXmlTextCharacter(int codePoint) =>
        codePoint is 0x9 or 0xA or 0xD ||
        codePoint is >= 0x20 and <= 0xD7FF ||
        codePoint is >= 0xE000 and <= 0xFFFD ||
        codePoint is >= 0x10000 and <= 0x10FFFF;

    private sealed record CellValue(string Text, double Number, bool IsNumber, int Style)
    {
        public static CellValue ForText(string text, int style = 0) => new(text, 0, false, style);
        public static CellValue ForNumber(double number, int style = 0) => new(string.Empty, number, true, style);
    }

    private const string RootRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string BuildStylesXml()
    {
        var fonts = new StringBuilder();
        fonts.AppendLine("""<font><sz val="11"/><name val="Exo 2"/></font>""");
        fonts.AppendLine("""<font><b/><sz val="11"/><name val="Exo 2"/></font>""");
        for (int size = 6; size <= 32; size++)
        {
            fonts.AppendLine($"""<font><b/><sz val="{size}"/><name val="Exo 2"/></font>""");
            fonts.AppendLine($"""<font><sz val="{size}"/><name val="Exo 2"/></font>""");
        }

        var cellXfs = new StringBuilder();
        cellXfs.AppendLine("""<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>""");
        cellXfs.AppendLine("""<xf numFmtId="0" fontId="1" fillId="1" borderId="0" xfId="0" applyFont="1" applyFill="1"/>""");
        cellXfs.AppendLine("""<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>""");
        cellXfs.AppendLine("""<xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>""");

        for (int size = 6; size <= 32; size++)
        {
            var fontOffset = 2 + (size - 6) * 2;
            var boldFontId = fontOffset;
            var fontId = fontOffset + 1;

            for (int alignmentIndex = 0; alignmentIndex < 3; alignmentIndex++)
            {
                var alignment = AlignmentName(alignmentIndex);
                cellXfs.AppendLine($"""<xf numFmtId="0" fontId="{boldFontId}" fillId="1" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="{alignment}"/></xf>""");
                cellXfs.AppendLine($"""<xf numFmtId="0" fontId="{fontId}" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="{alignment}"/></xf>""");
                cellXfs.AppendLine($"""<xf numFmtId="164" fontId="{fontId}" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1" applyAlignment="1"><alignment horizontal="{alignment}"/></xf>""");
                cellXfs.AppendLine($"""<xf numFmtId="165" fontId="{fontId}" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1" applyAlignment="1"><alignment horizontal="{alignment}"/></xf>""");
                cellXfs.AppendLine($"""<xf numFmtId="166" fontId="{fontId}" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1" applyAlignment="1"><alignment horizontal="{alignment}"/></xf>""");
            }
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <numFmts count="3">
                <numFmt numFmtId="164" formatCode="0.00"/>
                <numFmt numFmtId="165" formatCode="0.0%"/>
                <numFmt numFmtId="166" formatCode="0"/>
              </numFmts>
              <fonts count="{2 + 27 * 2}">
                {fonts}
              </fonts>
              <fills count="2">
                <fill><patternFill patternType="none"/></fill>
                <fill><patternFill patternType="solid"><fgColor rgb="FFD9D9D9"/><bgColor indexed="64"/></patternFill></fill>
              </fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="{4 + 27 * 3 * 5}">
                {cellXfs}
              </cellXfs>
            </styleSheet>
            """;
    }
}
