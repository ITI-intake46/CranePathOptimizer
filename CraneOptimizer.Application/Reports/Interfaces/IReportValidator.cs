using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

public interface IReportValidator
{
    ReportValidationResult Validate(ReportDocument document);
}
