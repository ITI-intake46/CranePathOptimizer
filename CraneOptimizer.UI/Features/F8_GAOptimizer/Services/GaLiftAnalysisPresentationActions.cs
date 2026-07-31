using System.Diagnostics;
using System.IO;
using CraneOptimizer.Application.GAAnalysis;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public interface IGaLiftAnalysisPresentationActions
{
    Task<bool> TryOpenSummaryFolderAsync(GaLiftAnalysisResult analysis);
    bool TryCopySummary(string summary);
}

public sealed class GaLiftAnalysisPresentationActions : IGaLiftAnalysisPresentationActions
{
    private readonly IGaLiftAnalysisPathResolver _pathResolver;

    public GaLiftAnalysisPresentationActions(IGaLiftAnalysisPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public Task<bool> TryOpenSummaryFolderAsync(GaLiftAnalysisResult analysis)
    {
        return Task.Run(() =>
        {
            try
            {
                var paths = _pathResolver.Resolve(analysis.ProjectGuid, analysis.OptimizationRunId);
                if (!Directory.Exists(paths.RunDirectory))
                    return false;
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                };
                startInfo.ArgumentList.Add(paths.RunDirectory);
                return Process.Start(startInfo) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Lift Summary Open Folder Failed] {ex}");
                return false;
            }
        });
    }

    public bool TryCopySummary(string summary)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(summary))
                return false;
            System.Windows.Clipboard.SetText(summary);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Lift Summary Copy Failed] {ex}");
            return false;
        }
    }
}
