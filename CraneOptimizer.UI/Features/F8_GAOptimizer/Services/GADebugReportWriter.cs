using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System;
using System.Collections.Generic;
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
                "Weight Raw",
                "COG X",
                "COG Y",
                "COG Z",
                "Radius m",
                "Boom m",
                "Required Capacity Raw",
                "Available Capacity Chart",
                "Status",
                "Newly Covered At Point"
            }));

            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int pointIndex = 0; pointIndex < result.Positions.Count; pointIndex++)
            {
                var position = result.Positions[pointIndex];

                foreach (var element in position.ElementResults)
                {
                    bool newlyCovered = element.Status == LiftStatus.Liftable
                        && covered.Add(element.ElementId);

                    writer.WriteLine(string.Join(",", new[]
                    {
                        Format(pointIndex + 1),
                        Format(position.X),
                        Format(position.Y),
                        Escape(element.ElementId),
                        Escape(element.ElementMark),
                        Escape(element.ElementType),
                        Format(element.Weight),
                        Format(element.CogX),
                        Format(element.CogY),
                        Format(element.CogZ),
                        Format(element.Radius),
                        Format(element.BoomLength),
                        Format(element.RequiredCapacity),
                        Format(element.AvailableCapacity),
                        Escape(element.Status.ToString()),
                        newlyCovered ? "Yes" : "No"
                    }));
                }
            }

            return path;
        }

        private static string Format(int value) => value.ToString(Invariant);

        private static string Format(double value) => value.ToString("0.###", Invariant);

        private static string Format(double? value) =>
            value.HasValue ? Format(value.Value) : string.Empty;

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
