using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public interface IReportBuilderDialogService
{
    Task ShowReportBuilderAsync(
        ReportFamily family,
        string templateName,
        string itemsIncluded,
        GaReportRunSelection? runSelection = null);
}
