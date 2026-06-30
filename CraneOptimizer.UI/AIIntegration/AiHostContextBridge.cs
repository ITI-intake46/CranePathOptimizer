using System.ComponentModel;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration
{
    /// <summary>
    /// UI-side adapter that translates live shell/GA state into AI-layer DTOs.
    /// This keeps CraneOptimizer.AI isolated from CraneOptimizer.UI view models.
    /// </summary>
    public sealed class AiHostContextBridge : IAiOptimizationContextProvider, IAiWorkflowContextProvider, IDisposable
    {
        private MainViewModel? _shell;
        private bool _disposed;

        public event EventHandler? ContextChanged;

        public void Attach(MainViewModel shell)
        {
            if (shell == null) throw new ArgumentNullException(nameof(shell));
            if (ReferenceEquals(_shell, shell))
                return;

            Detach();
            _shell = shell;
            Subscribe(shell);
            RaiseContextChanged();
        }

        public AiOptimizationContextDto GetCurrentOptimizationContext()
        {
            var shell = _shell;
            if (shell?.CranePlacement.Result == null)
            {
                return new AiOptimizationContextDto
                {
                    HasOptimizationResult = false,
                    ProjectName = shell?.CurrentProjectName ?? string.Empty
                };
            }

            var result = shell.CranePlacement.Result;
            var stats = result.Statistics ?? new LiftStatistics();
            var total = result.TotalElementCount > 0
                ? result.TotalElementCount
                : stats.Liftable + stats.OutOfRadius + stats.Overweight + stats.HeightFail + stats.SpecialCase + stats.NotCharted;

            var lifted = result.LiftedElementCount > 0 ? result.LiftedElementCount : stats.Liftable;

            return new AiOptimizationContextDto
            {
                HasOptimizationResult = true,
                ProjectName = shell.CurrentProjectName ?? string.Empty,
                CraneName = result.CraneName ?? string.Empty,
                CraneModelOrSpecification = result.CraneModelOrSpecification ?? string.Empty,
                TotalElementCount = total,
                LiftedElementCount = lifted,
                PositionCount = result.Positions.Count,
                CoverageRatio = result.CoverageRatio,
                FitnessScore = result.FitnessScore,
                MaxCraneRadius = result.MaxRadius,
                MaxCraneCapacity = result.MaxCapacity,
                SafetyFactor = result.SafetyFactor,
                DeratingRatio = result.DeratingRatio,
                AdditionalWeight = result.AdditionalWeight,
                WireLength = result.WireLength,
                DiagnosticsPath = result.DebugReportPath,
                FailureCounts = new FailureCountsDto
                {
                    Liftable = stats.Liftable,
                    OutOfRadius = stats.OutOfRadius,
                    Overweight = stats.Overweight,
                    HeightFail = stats.HeightFail,
                    SpecialCase = stats.SpecialCase,
                    NotCharted = stats.NotCharted
                },
                Reachability = result.ReachabilityDiagnostics == null
                    ? null
                    : new ReachabilitySnapshotDto
                    {
                        TotalCandidatePoints = result.ReachabilityDiagnostics.TotalCandidatePoints,
                        ValidCandidatePoints = result.ReachabilityDiagnostics.ValidCandidatePoints,
                        RejectedCandidatePoints = result.ReachabilityDiagnostics.RejectedCandidatePoints,
                        ZeroCoveragePoints = result.ReachabilityDiagnostics.ZeroCoveragePoints,
                        HotSpotCount = result.ReachabilityDiagnostics.HotSpotCount,
                        MaxHotSpotCoverage = result.ReachabilityDiagnostics.MaxHotSpotCoverage,
                        AverageHotSpotCoverage = result.ReachabilityDiagnostics.AverageHotSpotCoverage,
                        ReachabilityMean = result.ReachabilityDiagnostics.ReachabilityMean,
                        ReachabilityStdDev = result.ReachabilityDiagnostics.ReachabilityStdDev
                    }
            };
        }

        public AiWorkflowContextSnapshot GetSnapshot()
        {
            var shell = _shell;
            if (shell == null)
            {
                return new AiWorkflowContextSnapshot
                {
                    ProjectKey = "global",
                    ProjectDisplayName = "Global",
                    HasProject = false,
                    CurrentProjectName = string.Empty,
                    OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false }
                };
            }

            var result = shell.CranePlacement.Result;
            var cranes = shell.Scene.GetCranes();
            var hasResult = shell.CranePlacement.HasResult && result != null;
            var hasLoadChartEvidence = cranes.Any(crane => crane.ActiveSpecification != null || crane.CurrentCapacity.HasValue || crane.CurrentRadius.HasValue)
                || (hasResult && result!.MaxCapacity > 0 && result.MaxRadius > 0);

            return new AiWorkflowContextSnapshot
            {
                ProjectKey = GetProjectKey(shell),
                ProjectDisplayName = GetProjectDisplayName(shell),
                HasProject = shell.CurrentProjectId.HasValue,
                CurrentProjectName = shell.CurrentProjectName ?? string.Empty,
                ElementCount = shell.Scene.GetElements().Count,
                ZoneCount = shell.Scene.GetZones().Count,
                CraneCount = cranes.Count,
                HasLoadChartEvidence = hasLoadChartEvidence,
                OptimizationResult = hasResult
                    ? new AiOptimizationResultSnapshot
                    {
                        HasResult = true,
                        CoverageRatio = result!.CoverageRatio,
                        LiftedElementCount = result.LiftedElementCount,
                        TotalElementCount = result.TotalElementCount,
                        PositionCount = result.Positions.Count,
                        DiagnosticsPath = result.DebugReportPath,
                        MaxCapacity = result.MaxCapacity,
                        MaxRadius = result.MaxRadius
                    }
                    : new AiOptimizationResultSnapshot { HasResult = false }
            };
        }

        private static string GetProjectKey(MainViewModel shell)
        {
            if (shell.CurrentProjectId.HasValue)
                return "project:" + shell.CurrentProjectId.Value;

            return string.IsNullOrWhiteSpace(shell.CurrentProjectName)
                ? "global"
                : "project-name:" + shell.CurrentProjectName.Trim().ToLowerInvariant();
        }

        private static string GetProjectDisplayName(MainViewModel shell)
        {
            return string.IsNullOrWhiteSpace(shell.CurrentProjectName) ? "Global" : shell.CurrentProjectName.Trim();
        }

        private void Subscribe(MainViewModel shell)
        {
            shell.PropertyChanged += OnShellPropertyChanged;
            shell.Scene.ElementsChanged += OnSceneElementsChanged;
            shell.Scene.ElementsSelectionChanged += OnSceneElementsChanged;
            shell.Scene.ZoneAdded += OnSceneZoneChanged;
            shell.Scene.ZoneRemoved += OnSceneZoneChanged;
            shell.Scene.ZoneUpdated += OnSceneZoneChanged;
            shell.Scene.ZonesCleared += OnSceneChanged;
            shell.Scene.CraneAdded += OnSceneCraneChanged;
            shell.Scene.CraneRemoved += OnSceneCraneChanged;
            shell.Scene.CraneMoved += OnSceneCraneChanged;
            shell.Scene.CraneRotated += OnSceneCraneChanged;
            shell.Scene.CraneUpdated += OnSceneCraneChanged;
            shell.Scene.CranesCleared += OnSceneChanged;
            shell.CranePlacement.PropertyChanged += OnCranePlacementPropertyChanged;
        }

        private void Detach()
        {
            var shell = _shell;
            if (shell == null)
                return;

            shell.PropertyChanged -= OnShellPropertyChanged;
            shell.Scene.ElementsChanged -= OnSceneElementsChanged;
            shell.Scene.ElementsSelectionChanged -= OnSceneElementsChanged;
            shell.Scene.ZoneAdded -= OnSceneZoneChanged;
            shell.Scene.ZoneRemoved -= OnSceneZoneChanged;
            shell.Scene.ZoneUpdated -= OnSceneZoneChanged;
            shell.Scene.ZonesCleared -= OnSceneChanged;
            shell.Scene.CraneAdded -= OnSceneCraneChanged;
            shell.Scene.CraneRemoved -= OnSceneCraneChanged;
            shell.Scene.CraneMoved -= OnSceneCraneChanged;
            shell.Scene.CraneRotated -= OnSceneCraneChanged;
            shell.Scene.CraneUpdated -= OnSceneCraneChanged;
            shell.Scene.CranesCleared -= OnSceneChanged;
            shell.CranePlacement.PropertyChanged -= OnCranePlacementPropertyChanged;
            _shell = null;
        }

        private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.CurrentProjectId)
                or nameof(MainViewModel.CurrentProjectName)
                or nameof(MainViewModel.StatusText))
                RaiseContextChanged();
        }

        private void OnCranePlacementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CranePlacementViewModel.Result) or nameof(CranePlacementViewModel.HasResult))
                RaiseContextChanged();
        }

        private void OnSceneChanged() => RaiseContextChanged();
        private void OnSceneElementsChanged(List<PrecastElement> _) => RaiseContextChanged();
        private void OnSceneZoneChanged(GreenAreaZone _) => RaiseContextChanged();
        private void OnSceneCraneChanged(SceneCrane _) => RaiseContextChanged();

        private void RaiseContextChanged() => ContextChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Detach();
        }
    }
}
