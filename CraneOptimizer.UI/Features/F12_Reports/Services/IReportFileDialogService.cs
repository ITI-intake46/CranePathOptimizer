namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public interface IReportFileDialogService
{
    string? BrowseLogo();
    string? SavePdf(string suggestedFileName, string initialDirectory);
    string? SaveExcel(string suggestedFileName, string initialDirectory);
    string? PromptForTemplateName(string defaultName, IEnumerable<string> existingNames);
    void ShowInfo(string message, string title);
    bool Confirm(string message, string title);
}
