using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ReportValidator : IReportValidator
{
    public ReportValidationResult Validate(ReportDocument document)
    {
        var result = new ReportValidationResult();

        if (document == null)
        {
            result.Errors.Add("Report document is null.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(document.ReportId))
            result.Errors.Add("ReportId is required.");

        if (string.IsNullOrWhiteSpace(document.Title))
            result.Errors.Add("Report title is required.");

        if (document.Sections.Count == 0)
            result.Errors.Add("Report must contain at least one section.");

        foreach (var section in document.Sections)
        {
            foreach (var table in section.Tables)
            {
                ValidateTable(table, result);
            }
        }

        return result;
    }

    private static void ValidateTable(ReportTable table, ReportValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(table.Name))
            result.Errors.Add("Report table name is required.");

        if (table.Columns.Count == 0)
            result.Errors.Add($"Table '{table.Name}' must contain at least one column.");

        var duplicateKeys = table.Columns
            .GroupBy(column => column.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicateKey in duplicateKeys)
            result.Errors.Add($"Table '{table.Name}' contains duplicate column key '{duplicateKey}'.");

        foreach (var column in table.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Key))
                result.Errors.Add($"Table '{table.Name}' contains a column with empty key.");

            if (string.IsNullOrWhiteSpace(column.Header))
                result.Errors.Add($"Table '{table.Name}' contains a column with empty header.");
        }

        if (table.Rows.Count == 0)
            result.Warnings.Add($"Table '{table.Name}' has no rows.");

        foreach (var row in table.Rows)
        {
            foreach (var column in table.Columns)
            {
                if (!row.Values.ContainsKey(column.Key))
                {
                    result.Warnings.Add(
                        $"Table '{table.Name}' has a row missing value for column '{column.Key}'.");
                }
            }
        }
    }
}
