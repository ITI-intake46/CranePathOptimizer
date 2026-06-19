using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Application.Reports.Services;

namespace CraneOptimizer.Application.Tests.Reports;

public class ReportValidatorTests
{
    private readonly ReportValidator _validator = new();

    [Fact]
    public void Validate_ReturnsValid_ForValidReport()
    {
        var result = _validator.Validate(CreateValidDocument());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenReportIdMissing()
    {
        var document = CreateValidDocument(reportId: string.Empty);

        var result = _validator.Validate(document);

        Assert.Contains(result.Errors, error => error.Contains("ReportId"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenTitleMissing()
    {
        var document = CreateValidDocument(title: string.Empty);

        var result = _validator.Validate(document);

        Assert.Contains(result.Errors, error => error.Contains("title"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSectionsMissing()
    {
        var document = CreateValidDocument(includeSections: false);

        var result = _validator.Validate(document);

        Assert.Contains(result.Errors, error => error.Contains("at least one section"));
    }

    [Fact]
    public void Validate_ReturnsError_ForDuplicateColumnKeys()
    {
        var document = CreateValidDocument();
        document.Sections[0].Tables[0].Columns.Add(new ReportColumn { Key = "A", Header = "Duplicate" });

        var result = _validator.Validate(document);

        Assert.Contains(result.Errors, error => error.Contains("duplicate column key"));
    }

    [Fact]
    public void Validate_ReturnsWarning_ForEmptyTable()
    {
        var document = CreateValidDocument();
        document.Sections[0].Tables[0].Rows.Clear();

        var result = _validator.Validate(document);

        Assert.Contains(result.Warnings, warning => warning.Contains("has no rows"));
    }

    [Fact]
    public void Validate_ReturnsWarning_ForMissingRowValue()
    {
        var document = CreateValidDocument();
        document.Sections[0].Tables[0].Rows[0].Values.Remove("A");

        var result = _validator.Validate(document);

        Assert.Contains(result.Warnings, warning => warning.Contains("missing value"));
    }

    private static ReportDocument CreateValidDocument(
        string reportId = "report",
        string title = "Report",
        bool includeSections = true)
    {
        var document = new ReportDocument
        {
            ReportId = reportId,
            Title = title
        };

        if (!includeSections)
            return document;

        document.Sections.Add(
            new ReportSection
            {
                Title = "Section",
                Tables =
                {
                    new ReportTable
                    {
                        Name = "table",
                        Title = "Table",
                        Columns =
                        {
                            new ReportColumn { Key = "A", Header = "A" },
                            new ReportColumn { Key = "B", Header = "B" }
                        },
                        Rows =
                        {
                            new ReportRow
                            {
                                Values =
                                {
                                    ["A"] = "a",
                                    ["B"] = "b"
                                }
                            }
                        }
                    }
                }
            });

        return document;
    }
}
