using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public interface IGaLiftAnalysisWindowService
{
    void Show(GaResultSnapshotDto snapshot);
    void ShowSaved(GaLiftAnalysisResult analysis);
}

public interface IGaLiftAnalysisWindowFactory
{
    GaLiftAnalysisWindow Create(GaResultSnapshotDto snapshot);
    GaLiftAnalysisWindow CreateSaved(GaLiftAnalysisResult analysis);
}

public sealed class GaLiftAnalysisWindowFactory : IGaLiftAnalysisWindowFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GaLiftAnalysisWindowFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public GaLiftAnalysisWindow Create(GaResultSnapshotDto snapshot)
    {
        var window = CreateWindow(out var scope);
        try
        {
            window.Initialize(snapshot);
            window.Closed += (_, _) => scope.Dispose();
            return window;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public GaLiftAnalysisWindow CreateSaved(GaLiftAnalysisResult analysis)
    {
        var window = CreateWindow(out var scope);
        try
        {
            window.InitializeSaved(analysis);
            window.Closed += (_, _) => scope.Dispose();
            return window;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private GaLiftAnalysisWindow CreateWindow(out IServiceScope scope)
    {
        scope = _scopeFactory.CreateScope();
        try
        {
            return scope.ServiceProvider.GetRequiredService<GaLiftAnalysisWindow>();
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}

public sealed class GaLiftAnalysisWindowService : IGaLiftAnalysisWindowService
{
    private readonly IGaLiftAnalysisWindowFactory _windowFactory;

    public GaLiftAnalysisWindowService(IGaLiftAnalysisWindowFactory windowFactory)
    {
        _windowFactory = windowFactory;
    }

    public void Show(GaResultSnapshotDto snapshot)
    {
        var window = _windowFactory.Create(snapshot);
        if (System.Windows.Application.Current?.MainWindow is { } owner && owner != window)
            window.Owner = owner;
        window.Show();
    }

    public void ShowSaved(GaLiftAnalysisResult analysis)
    {
        var window = _windowFactory.CreateSaved(analysis);
        if (System.Windows.Application.Current?.MainWindow is { } owner && owner != window)
            window.Owner = owner;
        window.Show();
    }
}
