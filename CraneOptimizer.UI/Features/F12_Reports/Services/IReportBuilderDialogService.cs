using CraneOptimizer.Application.Reports.Definitions;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public interface IReportBuilderDialogService
{
    void ShowReportBuilder(ReportFamily family, string templateName, string itemsIncluded);
}
