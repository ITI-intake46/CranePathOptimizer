using System.ComponentModel;
using System.IO;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
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
        private readonly IReadOnlyList<IAiScreenContextContributor> _contributors;
        private MainViewModel? _shell;
        private bool _disposed;
        private long _modelRevision;
        private DateTimeOffset? _modelLoadedAtUtc;

        public AiHostContextBridge()
            : this(Array.Empty<IAiScreenContextContributor>())
        {
        }

        public AiHostContextBridge(IEnumerable<IAiScreenContextContributor> contributors)
        {
            _contributors = contributors?.ToList() ?? new List<IAiScreenContextContributor>();
        }

        public event EventHandler? ContextChanged;

        public void Attach(MainViewModel shell)
        {
            if (shell == null) throw new ArgumentNullException(nameof(shell));
            if (ReferenceEquals(_shell, shell))
                return;

            Detach();
            _shell = shell;
            IncrementModelRevision(shell.Scene.GetElements().Count > 0 ? "attached to existing model" : "attached to shell");
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
            var elements = shell.Scene.GetElements();
            var zones = shell.Scene.GetZones();
            var cranes = shell.Scene.GetCranes();
            var hasResult = shell.CranePlacement.HasResult && result != null;
            var activeLoadChartEvidenceCount = cranes.Count(crane => crane.ActiveSpecification != null || crane.CurrentCapacity.HasValue || crane.CurrentRadius.HasValue);
            if (activeLoadChartEvidenceCount == 0 && hasResult && result!.MaxCapacity > 0 && result.MaxRadius > 0)
                activeLoadChartEvidenceCount = 1;
            var hasLoadChartEvidence = activeLoadChartEvidenceCount > 0;
            var allowedZoneCount = zones.Count(zone => zone.Type == ZoneType.Allowed);
            var forbiddenZoneCount = zones.Count(zone => zone.Type == ZoneType.Forbidden);
            var allowedZoneArea = zones.Where(zone => zone.Type == ZoneType.Allowed).Sum(zone => zone.Area);
            var forbiddenZoneArea = zones.Where(zone => zone.Type == ZoneType.Forbidden).Sum(zone => zone.Area);
            var elementTableTotal = shell.ElementManagement.Elements.Count;
            var elementTableVisible = shell.ElementManagement.FilteredElements.Count;
            var modelDisplayName = BuildModelDisplayName(shell, elements.Count);
            var modelFingerprint = BuildModelFingerprint(shell, elements, elementTableTotal);

            return new AiWorkflowContextSnapshot
            {
                ProjectKey = GetProjectKey(shell),
                ProjectDisplayName = GetProjectDisplayName(shell),
                HasProject = shell.CurrentProjectId.HasValue,
                CurrentProjectName = shell.CurrentProjectName ?? string.Empty,
                ModelRevision = _modelRevision,
                ModelFingerprint = modelFingerprint,
                ModelDisplayName = modelDisplayName,
                ModelLoadedAt = _modelLoadedAtUtc,
                ElementCount = elements.Count,
                ElementTableTotalCount = elementTableTotal,
                ElementTableVisibleCount = elementTableVisible,
                ZoneCount = zones.Count,
                AllowedZoneCount = allowedZoneCount,
                ForbiddenZoneCount = forbiddenZoneCount,
                AllowedZoneArea = allowedZoneArea,
                ForbiddenZoneArea = forbiddenZoneArea,
                CraneCount = cranes.Count,
                LoadChartEvidenceCount = activeLoadChartEvidenceCount,
                HasLoadChartEvidence = hasLoadChartEvidence,
                BuildingArea = shell.BuildingArea,
                BuildingAreaUnit = shell.AreaUnit,
                BuildingHeight = shell.BuildingHeight,
                BuildingLengthUnit = shell.SelectedUnit,
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
                    : new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = BuildScreenContexts(shell, _modelRevision, modelFingerprint, elements.Count, elementTableTotal, elementTableVisible)
            };
        }


        private IReadOnlyList<AiScreenContextSnapshot> BuildScreenContexts(
            MainViewModel shell,
            long modelRevision,
            string modelFingerprint,
            int sceneBimObjectCount,
            int elementTableTotal,
            int elementTableVisible)
        {
            if (_contributors.Count == 0)
                return Array.Empty<AiScreenContextSnapshot>();

            var snapshots = new List<AiScreenContextSnapshot>();
            foreach (var contributor in _contributors)
            {
                try
                {
                    var snapshot = contributor.BuildSnapshot(shell);
                    if (snapshot != null)
                        snapshots.Add(EnrichWithHostSnapshotFacts(snapshot, modelRevision, modelFingerprint, sceneBimObjectCount, elementTableTotal, elementTableVisible));
                }
                catch (Exception ex)
                {
                    snapshots.Add(new AiScreenContextSnapshot
                    {
                        ScreenName = contributor.ScreenName,
                        Stage = "Context unavailable",
                        Summary = $"AI context contributor failed: {ex.Message}",
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "AiContextContributorFailure",
                                Severity = "Info",
                                Area = "AI Context",
                                Evidence = ex.Message,
                                Recommendation = "Use the visible UI and deterministic diagnostics; AI context for this screen is incomplete.",
                                NextClick = contributor.ScreenName,
                                Confidence = "High"
                            }
                        }
                    });
                }
            }

            return snapshots;
        }

        // Narrow host-adapter responsibility: publish source-of-truth model identity/count
        // facts into the AI-owned fact-key contract. Interpretation and answer policy
        // stay inside CraneOptimizer.AI.
        private static AiScreenContextSnapshot EnrichWithHostSnapshotFacts(
            AiScreenContextSnapshot snapshot,
            long modelRevision,
            string modelFingerprint,
            int sceneBimObjectCount,
            int elementTableTotal,
            int elementTableVisible)
        {
            var facts = new Dictionary<string, string>(snapshot.Facts ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                [AiWorkflowFactKeys.HostModelRevision] = modelRevision.ToString(),
                [AiWorkflowFactKeys.HostModelFingerprint] = modelFingerprint ?? string.Empty,
                [AiWorkflowFactKeys.SceneBimObjectCount] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.ImportedSceneBimObjectCount] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.ElementTableItems] = elementTableTotal.ToString(),
                [AiWorkflowFactKeys.VisibleElementTableItems] = elementTableVisible.ToString()
            };

            return new AiScreenContextSnapshot
            {
                ScreenName = snapshot.ScreenName,
                Stage = snapshot.Stage,
                Summary = snapshot.Summary,
                Metrics = snapshot.Metrics,
                Issues = snapshot.Issues,
                ActionHints = snapshot.ActionHints,
                Facts = facts
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

        private static string BuildModelDisplayName(MainViewModel shell, int elementCount)
        {
            if (elementCount <= 0)
                return "No IFC model loaded";

            if (!string.IsNullOrWhiteSpace(shell.BuildingName)
                && !shell.BuildingName.Equals("No Building Loaded", StringComparison.OrdinalIgnoreCase))
            {
                return shell.BuildingName.Trim();
            }

            return string.IsNullOrWhiteSpace(shell.CurrentProjectName) ? "Imported IFC model" : shell.CurrentProjectName.Trim();
        }

        private string BuildModelFingerprint(MainViewModel shell, IReadOnlyList<PrecastElement> elements, int tableTotal)
        {
            if (elements.Count == 0)
                return $"model:none:rev:{_modelRevision}";

            var projectPart = shell.CurrentProjectId?.ToString("N")
                ?? NormalizeFingerprintPart(shell.CurrentProjectName);
            var ifcPart = NormalizeFingerprintPart(Path.GetFileName(shell.CurrentProjectIfcPath ?? string.Empty));
            var firstId = NormalizeFingerprintPart(elements.FirstOrDefault()?.ElementID);
            var lastId = NormalizeFingerprintPart(elements.LastOrDefault()?.ElementID);
            var building = NormalizeFingerprintPart(shell.BuildingName);
            var zones = shell.Scene.GetZones();
            var cranes = shell.Scene.GetCranes();
            var activeLoadCharts = cranes.Count(crane => crane.ActiveSpecification != null || crane.CurrentCapacity.HasValue || crane.CurrentRadius.HasValue);

            return $"model:rev:{_modelRevision}:project:{projectPart}:ifc:{ifcPart}:building:{building}:elements:{elements.Count}:tabletotal:{tableTotal}:zones:{zones.Count}:cranes:{cranes.Count}:loadcharts:{activeLoadCharts}:first:{firstId}:last:{lastId}";
        }

        private static string NormalizeFingerprintPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = value.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var normalized = new string(chars).Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "none" : normalized;
        }

        private void IncrementModelRevision(string reason)
        {
            _modelRevision++;
            _modelLoadedAtUtc = DateTimeOffset.UtcNow;
        }

        private void Subscribe(MainViewModel shell)
        {
            shell.PropertyChanged += OnShellPropertyChanged;
            shell.Scene.ElementsChanged += OnSceneElementsChanged;
            shell.Scene.ElementsSelectionChanged += OnSceneSelectionChanged;
            shell.Scene.ZoneAdded += OnSceneZoneChanged;
            shell.Scene.ZoneRemoved += OnSceneZoneChanged;
            shell.Scene.ZoneUpdated += OnSceneZoneChanged;
            shell.Scene.ZonesCleared += OnNonModelSceneChanged;
            shell.Scene.CraneAdded += OnSceneCraneChanged;
            shell.Scene.CraneRemoved += OnSceneCraneChanged;
            shell.Scene.CraneMoved += OnSceneCraneChanged;
            shell.Scene.CraneRotated += OnSceneCraneChanged;
            shell.Scene.CraneUpdated += OnSceneCraneChanged;
            shell.Scene.CranesCleared += OnNonModelSceneChanged;
            shell.CranePlacement.PropertyChanged += OnCranePlacementPropertyChanged;
        }

        private void Detach()
        {
            var shell = _shell;
            if (shell == null)
                return;

            shell.PropertyChanged -= OnShellPropertyChanged;
            shell.Scene.ElementsChanged -= OnSceneElementsChanged;
            shell.Scene.ElementsSelectionChanged -= OnSceneSelectionChanged;
            shell.Scene.ZoneAdded -= OnSceneZoneChanged;
            shell.Scene.ZoneRemoved -= OnSceneZoneChanged;
            shell.Scene.ZoneUpdated -= OnSceneZoneChanged;
            shell.Scene.ZonesCleared -= OnNonModelSceneChanged;
            shell.Scene.CraneAdded -= OnSceneCraneChanged;
            shell.Scene.CraneRemoved -= OnSceneCraneChanged;
            shell.Scene.CraneMoved -= OnSceneCraneChanged;
            shell.Scene.CraneRotated -= OnSceneCraneChanged;
            shell.Scene.CraneUpdated -= OnSceneCraneChanged;
            shell.Scene.CranesCleared -= OnNonModelSceneChanged;
            shell.CranePlacement.PropertyChanged -= OnCranePlacementPropertyChanged;
            _shell = null;
        }

        private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.CurrentProjectId)
                or nameof(MainViewModel.CurrentProjectName)
                or nameof(MainViewModel.CurrentProjectIfcPath)
                or nameof(MainViewModel.BuildingName))
            {
                IncrementModelRevision(e.PropertyName ?? "project/model identity changed");
                RaiseContextChanged();
                return;
            }

            if (e.PropertyName is nameof(MainViewModel.StatusText))
                RaiseContextChanged();
        }

        private void OnCranePlacementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CranePlacementViewModel.Result) or nameof(CranePlacementViewModel.HasResult) or nameof(CranePlacementViewModel.SelectedPosition) or nameof(CranePlacementViewModel.CurrentGAViewMode))
                RaiseContextChanged();
        }

        private void OnNonModelSceneChanged() => RaiseContextChanged();

        private void OnSceneElementsChanged(List<PrecastElement> _)
        {
            IncrementModelRevision("IFC/model elements changed");
            RaiseContextChanged();
        }

        private void OnSceneSelectionChanged(List<PrecastElement> _) => RaiseContextChanged();
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
