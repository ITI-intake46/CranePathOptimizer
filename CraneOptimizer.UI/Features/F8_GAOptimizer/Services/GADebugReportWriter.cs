using CraneOptimizer.UI.Features.F8_GAOptimizer.Mappers;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    internal static class GADebugReportWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Write(CranePlacementResult result)
        {
            var report = result.ToDebugReportDto();
            var reportDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "GAReports");
            Directory.CreateDirectory(reportDirectory);

            var fileName = $"ga-debug-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var path = Path.Combine(reportDirectory, fileName);

            using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            writer.WriteLine(string.Join(",", new[]
            {
                "Point No",
                "Assigned Point X",
                "Assigned Point Y",
                "Element ID",
                "Element Mark",
                "Element Type",
                "Weight",
                "COG X",
                "COG Y",
                "COG Z",
                "Radius",
                "Boom",
                "Required Capacity",
                "Available Capacity",
                "Status",
                "Newly Covered At Point"
            }));

            foreach (var row in report.Rows)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    Format(row.PointNumber),
                    Format(row.PointX),
                    Format(row.PointY),
                    Escape(row.ElementId),
                    Escape(row.ElementMark),
                    Escape(row.ElementType),
                    Format(row.Weight),
                    Format(row.CogX),
                    Format(row.CogY),
                    Format(row.CogZ),
                    FormatWithUnit(row.Radius, "m"),
                    FormatWithUnit(row.BoomLength, "m"),
                    FormatWithUnit(row.RequiredCapacity, "t"),
                    FormatWithUnit(row.AvailableCapacity, "t"),
                    Escape(row.Status),
                    row.NewlyCovered ? "Yes" : "No"
                }));
            }

            return path;
        }

        private static string Format(int value) => value.ToString(Invariant);

        private static string Format(double value) => value.ToString("0.###", Invariant);

        private static string Format(double? value) =>
            value.HasValue ? Format(value.Value) : string.Empty;

        private static string FormatWithUnit(double value, string unit)
        {
            var text = Format(value);
            return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool mustQuote = value.Contains(',')
                || value.Contains('"')
                || value.Contains('\r')
                || value.Contains('\n');

            if (!mustQuote)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
