using System.Collections.Concurrent;
using System.IO;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public enum GaLiftAnalysisJobStatus
{
    Queued,
    LoadingCatalog,
    Analyzing,
    Saving,
    Completed,
    Failed,
    Cancelled
}

public sealed class GaLiftAnalysisJobState
{
    internal GaLiftAnalysisJobState(string key, GaResultSnapshotDto snapshot, bool forceRebuild)
    {
        Key = key;
        Snapshot = snapshot;
        ForceRebuild = forceRebuild;
    }

    internal CancellationTokenSource Cancellation { get; } = new();
    internal GaResultSnapshotDto Snapshot { get; }
    internal bool ForceRebuild { get; }
    public string Key { get; }
    public GaLiftAnalysisJobStatus Status { get; internal set; } = GaLiftAnalysisJobStatus.Queued;
    public int ProgressPercent { get; internal set; }
    public string StatusText { get; internal set; } = "Queued";
    public GaLiftAnalysisResult? Result { get; internal set; }
    public string? ErrorMessage { get; internal set; }
    public Task Completion { get; internal set; } = Task.CompletedTask;
    public bool IsTerminal => Status is GaLiftAnalysisJobStatus.Completed
        or GaLiftAnalysisJobStatus.Failed
        or GaLiftAnalysisJobStatus.Cancelled;
}

public interface IGaLiftAnalysisJobCoordinator
{
    event EventHandler<GaLiftAnalysisJobState>? JobChanged;
    GaLiftAnalysisJobState StartOrGet(GaResultSnapshotDto snapshot);
    GaLiftAnalysisJobState Refresh(GaResultSnapshotDto snapshot);
    GaLiftAnalysisJobState Restart(GaResultSnapshotDto snapshot);
    void Cancel(string projectGuid, string optimizationRunId);
    void CancelAll();
}

/// <summary>
/// Supervises analysis tasks and gives every job its own DI scope. The worker catches
/// and publishes all failures, so no background task exception is left unobserved.
/// </summary>
public sealed class GaLiftAnalysisJobCoordinator : IGaLiftAnalysisJobCoordinator, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _jobGate = new();
    private readonly ConcurrentDictionary<string, GaLiftAnalysisJobState> _jobs =
        new(StringComparer.Ordinal);

    public GaLiftAnalysisJobCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public event EventHandler<GaLiftAnalysisJobState>? JobChanged;

    public GaLiftAnalysisJobState StartOrGet(GaResultSnapshotDto snapshot)
    {
        var key = Key(snapshot.Project.ProjectGuid, snapshot.Run.OptimizationRunId);
        lock (_jobGate)
        {
            if (_jobs.TryGetValue(key, out var existing))
                return existing;
            var state = StartNew(key, snapshot, forceRebuild: false);
            _jobs[key] = state;
            return state;
        }
    }

    public GaLiftAnalysisJobState Refresh(GaResultSnapshotDto snapshot)
    {
        var key = Key(snapshot.Project.ProjectGuid, snapshot.Run.OptimizationRunId);
        lock (_jobGate)
        {
            if (_jobs.TryGetValue(key, out var current) && !current.IsTerminal)
                return current;
            _jobs.TryRemove(key, out _);
            var state = StartNew(key, snapshot, forceRebuild: false);
            _jobs[key] = state;
            return state;
        }
    }

    public GaLiftAnalysisJobState Restart(GaResultSnapshotDto snapshot)
    {
        var key = Key(snapshot.Project.ProjectGuid, snapshot.Run.OptimizationRunId);
        lock (_jobGate)
        {
            if (_jobs.TryRemove(key, out var existing))
                existing.Cancellation.Cancel();
            var state = StartNew(key, snapshot, forceRebuild: true);
            _jobs[key] = state;
            return state;
        }
    }

    public void Cancel(string projectGuid, string optimizationRunId)
    {
        if (_jobs.TryGetValue(Key(projectGuid, optimizationRunId), out var state))
            state.Cancellation.Cancel();
    }

    public void CancelAll()
    {
        foreach (var state in _jobs.Values)
            state.Cancellation.Cancel();
    }

    public void Dispose()
    {
        CancelAll();
        _jobs.Clear();
    }

    private GaLiftAnalysisJobState StartNew(
        string key,
        GaResultSnapshotDto snapshot,
        bool forceRebuild)
    {
        var state = new GaLiftAnalysisJobState(key, snapshot, forceRebuild);
        state.Completion = Task.Run(() => RunAsync(state));
        return state;
    }

    private async Task RunAsync(GaLiftAnalysisJobState state)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<IGaCraneCatalogProvider>();
            var analysisService = scope.ServiceProvider.GetRequiredService<IGaLiftAnalysisService>();
            var store = scope.ServiceProvider.GetRequiredService<IGaLiftAnalysisStore>();
            var currency = scope.ServiceProvider.GetRequiredService<IGaAnalysisCurrencyService>();
            var currentContextProvider =
                scope.ServiceProvider.GetRequiredService<IGaAnalysisCurrentContextProvider>();
            var token = state.Cancellation.Token;

            Publish(state, GaLiftAnalysisJobStatus.LoadingCatalog, 5, "Checking for a saved current analysis…");
            var saved = await store.LoadAsync(
                    state.Snapshot.Project.ProjectGuid,
                    state.Snapshot.Run.OptimizationRunId,
                    token)
                .ConfigureAwait(false);
            if (!state.ForceRebuild
                && saved != null
                && saved.AnalysisEngineId == GaLiftAnalysisRules.EngineId
                && saved.AnalysisEngineVersion == GaLiftAnalysisRules.EngineVersion
                && currency.Compare(
                    saved,
                    currentContextProvider.Capture(state.Snapshot)).IsCurrent)
            {
                state.Result = saved;
                Publish(state, GaLiftAnalysisJobStatus.Completed, 100, "Saved current analysis restored");
                return;
            }
            if (saved != null)
            {
                state.Result = saved;
                Publish(
                    state,
                    GaLiftAnalysisJobStatus.LoadingCatalog,
                    8,
                    state.ForceRebuild
                        ? "Current saved analysis loaded; rebuilding…"
                        : "Saved analysis is stale; rebuilding from the selected GA run…");
            }

            Publish(state, GaLiftAnalysisJobStatus.LoadingCatalog, 10, "Loading crane catalog…");
            IReadOnlyList<GaCraneCandidate> candidates;
            string? catalogWarning = null;
            try
            {
                candidates = await catalog
                    .GetCandidatesAsync(state.Snapshot.Crane.CraneSpecificationId, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                candidates = Array.Empty<GaCraneCandidate>();
                catalogWarning = "The crane catalog could not be loaded for this analysis run.";
                System.Diagnostics.Debug.WriteLine($"[Lift Summary Catalog Unavailable] Key={state.Key} Error={ex}");
            }

            Publish(state, GaLiftAnalysisJobStatus.Analyzing, 35, "Evaluating lift evidence and alternatives…");
            var result = await analysisService.AnalyzeAsync(new GaLiftAnalysisRequest
            {
                Snapshot = state.Snapshot,
                Candidates = candidates,
                RunAlternativeScreening = true,
                AlternativeScreeningUnavailableReason = catalogWarning
            }, token).ConfigureAwait(false);

            Publish(state, GaLiftAnalysisJobStatus.Saving, 85, "Saving verified analysis…");
            await store.SaveAsync(result, token).ConfigureAwait(false);
            state.Result = result;
            Publish(state, GaLiftAnalysisJobStatus.Completed, 100, "Analysis complete");
        }
        catch (OperationCanceledException)
        {
            Publish(state, GaLiftAnalysisJobStatus.Cancelled, state.ProgressPercent, "Analysis cancelled");
        }
        catch (Exception ex)
        {
            state.ErrorMessage = "Lift Summary could not be generated.";
            System.Diagnostics.Debug.WriteLine($"[Lift Summary Job Failed] Key={state.Key} Error={ex}");
            Publish(state, GaLiftAnalysisJobStatus.Failed, state.ProgressPercent, "Lift Summary could not be generated.");
        }
    }

    private void Publish(
        GaLiftAnalysisJobState state,
        GaLiftAnalysisJobStatus status,
        int progress,
        string statusText)
    {
        state.Status = status;
        state.ProgressPercent = progress;
        state.StatusText = statusText;
        JobChanged?.Invoke(this, state);
    }

    private static string Key(string projectGuid, string optimizationRunId) =>
        $"{projectGuid}\u001f{optimizationRunId}";
}
