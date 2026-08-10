using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using System.Diagnostics;


using CraneOptimizer.UI.Core.Configuration;
namespace CraneOptimizer.UI.Core.Services
{
    public sealed class SceneRenderingManager : ISceneRenderingManager
    {
        private readonly ISceneService _scene;
        private readonly IRenderingService _rendering;
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ICraneValidationService _craneValidation;
        private readonly IElementVisibilityFilterService _filterService;
        private readonly IRenderDiagnosticsStore _diagnosticsStore;
        private readonly OptimizationResultViewportState _optimizationResultViewportState = new();
        private readonly List<PrecastElement> _sceneElements = new();
        private Dictionary<string, List<System.Windows.Media.Media3D.GeometryModel3D>> _renderedElementModels = new(StringComparer.Ordinal);
        // Persistent builder so per-crane MobileCraneVisual3D and the
        // INotifyPropertyChanged -> Rebuild plumbing survive across renders.
        private readonly CraneVisualBuilder _craneBuilder;
        private SceneRenderingContext? _context;
        private bool _disposed;
        private bool _showCraneRadius = true;
        private bool _isSyncingSceneElements;

        public SceneRenderingManager(
            ISceneService scene,
            IRenderingService rendering,
            IIfcReaderRepository ifcReader,
            ICraneValidationService craneValidation,
            CraneVisualBuilder craneBuilder,
            IElementVisibilityFilterService filterService,
            IRenderDiagnosticsStore diagnosticsStore)
        {
            _scene = scene;
            _rendering = rendering;
            _ifcReader = ifcReader;
            _craneValidation = craneValidation;
            _craneBuilder = craneBuilder;
            _filterService = filterService;
            _diagnosticsStore = diagnosticsStore;


            _scene.ElementsChanged += OnElementsChanged;
            _scene.ElementsSelectionChanged += OnElementsSelectionChanged;
            _filterService.StateChanged += OnFilterStateChanged;
            _scene.CraneAdded += OnCraneChanged;
            _scene.CraneMoved += OnCraneChanged;
            _scene.CraneUpdated += OnCraneChanged;
            _scene.CraneRemoved += OnCraneChanged;
            _scene.CranesCleared += OnCranesCleared;
            _scene.ZonesChanged += OnZonesChanged;
        }

        public event Action<IReadOnlyList<PrecastElement>>? ElementsChanged;
        public event Action? ElementsCleared;

        public bool ShowCraneRadius
        {
            get => _showCraneRadius;
            set
            {
                if (_showCraneRadius == value)
                    return;

                _showCraneRadius = value;
                ReRenderCranes();
            }
        }

        public void Configure(SceneRenderingContext context)
        {
            _context = context;
        }

        public void ReRenderElements()
        {
            if (_context == null)
                return;

            _rendering.ClearLayer("Elements");

            var visibleElements = _filterService.VisibleElements;


            if (_sceneElements.Count == 0 || visibleElements.Count == 0)
            {
                _renderedElementModels.Clear();
                return;
            }

            if (AppFeatureFlags.EnableProjectPerformanceCapture) Debug.WriteLine("Rendering Elements: " + visibleElements.Count);

            // â”€â”€ Diagnostics: pre-render snapshot â”€â”€
            var sw = AppFeatureFlags.EnableProjectPerformanceCapture ? System.Diagnostics.Stopwatch.StartNew() : null;
            var memBefore = AppFeatureFlags.EnableProjectPerformanceCapture ? GetPrivateMemoryMB() : 0.0;

            var liftResults = _context.GetLiftResults();
            bool hasFinalAssignments = liftResults != null && liftResults.Any(r => r.IsFinalAssigned);
            string selectedPointId = liftResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.CandidatePointId))?.CandidatePointId ?? string.Empty;

            var viewMode = _context.GetGAViewMode?.Invoke() ?? GAViewMode.FinalAssignment;

            var statusMap = liftResults
                ?.Where(result => !string.IsNullOrWhiteSpace(result.ElementId))
                .GroupBy(result => result.ElementId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var itemForSelected = group.FirstOrDefault(r => r.CandidatePointId == selectedPointId);
                        var item = itemForSelected
                                   ?? group.FirstOrDefault(r => r.IsFinalAssignedToPoint)
                                   ?? group.Last();

                        if (viewMode == GAViewMode.SelectedPointFeasibility)
                        {
                            return (LiftStatus?)(itemForSelected?.Status ?? item.Status);
                        }
                        else // FinalAssignment or AllFeasibleDebug
                        {
                            if (hasFinalAssignments)
                            {
                                if (item.IsFinalAssignedToPoint || (!string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId == selectedPointId))
                                {
                                    return (LiftStatus?)item.Status;
                                }
                                else if (item.IsFinalAssigned && !string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId != selectedPointId)
                                {
                                    return null; // Dimmed/Blueprint Gray
                                }
                                else if (!item.IsFinalAssigned)
                                {
                                    return (LiftStatus?)item.BestFailedLiftStatus;
                                }
                                else
                                {
                                    return null; // Safe neutral fallback
                                }
                            }
                            return (LiftStatus?)item.Status;
                        }
                    },
                    StringComparer.Ordinal);

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            if (_context.GetSelectedElementIds != null)
            {
                var ids = _context.GetSelectedElementIds();
                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        if (id != null)
                            selectedIds.Add(id);
                    }
                }
            }
            else
            {
                var singleId = _context.GetSelectedElementId();
                if (singleId != null)
                    selectedIds.Add(singleId);
            }

            var origin = _context.GetOriginPoint();
            var placementOffset = _context.GetPlacementOffset();

            var builder = new ElementVisualBuilder(_ifcReader, origin, placementOffset);
            builder.ResetCounters();

            _rendering.Render(
                visibleElements.Select(element =>
                {
                    LiftStatus? status = null;
                    if (statusMap != null && statusMap.TryGetValue(element.ElementID, out var liftStatus))
                        status = liftStatus;

                    bool isSelected = selectedIds.Contains(element.ElementID);
                    return (element, status, isSelected);
                }),
                builder,
                "Elements");

            _renderedElementModels = builder.ElementModelRegistry;

            // Apply exact visual states (including AssignedToOtherPoint) immediately after rendering
            UpdateElementMaterialsCore(
                liftResults,
                releaseOptimizationHighlight: false,
                reRenderIfMissing: false);

            sw?.Stop();
            var memAfter = AppFeatureFlags.EnableProjectPerformanceCapture ? GetPrivateMemoryMB() : 0.0;
            if (AppFeatureFlags.EnableProjectPerformanceCapture)
            {

            // â”€â”€ Diagnostics: build stats â”€â”€
            var sessionService = _ifcReader as CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository;
            var stats = new CraneOptimizer.Domain.Diagnostics.RenderPerformanceStats
            {
                SessionId = sessionService?.CurrentSessionId,
                FileName = sessionService?.CurrentFileIdentity,
                RenderQuality = ElementVisualBuilder.CurrentQuality.ToString(),
                TotalElements = builder.DiagTotalElements,
                VisibleElements = builder.DiagVisibleElements,
                IncludeInRenderSkippedCount = builder.DiagIncludeInRenderSkipped,
                ShapeInstanceCount = builder.DiagShapeInstances,
                MeshesCreatedCount = builder.DiagMeshesCreated,
                MeshCacheHitCount = builder.DiagMeshCacheHits,
                MeshCacheMissCount = builder.DiagMeshCacheMisses,
                HighQualityMeshCacheHitCount = builder.DiagHighQualityMeshCacheHits,
                HighQualityMeshCacheMissCount = builder.DiagHighQualityMeshCacheMisses,
                HighQualityMeshesCreatedCount = builder.DiagHighQualityMeshesCreated,
                HighQualityCacheCount = ElementVisualBuilder.HighQualityMeshCacheCount,
                GeometryModel3DCount = builder.DiagGeometryModel3DCount,
                ModelVisual3DCount = builder.DiagModelVisual3DCount,
                MaterialCreatedCount = builder.DiagMaterialCreated,
                FallbackBoxCount = builder.DiagFallbackBoxCount,
                RealMeshElementCount = builder.DiagRealMeshElements,
                BatchedGroupCount = builder.DiagBatchedGroups,
                BatchedElementCount = builder.DiagBatchedElements,
                HitTestBoxCount = builder.DiagHitTestBoxes,
                RenderGeometryModel3DCount = builder.DiagRenderGeometryModel3DCount,
                HitTestGeometryModel3DCount = builder.DiagHitTestGeometryModel3DCount,
                TotalGeometryModel3DCount = builder.DiagTotalGeometryModel3DCount,
                RenderDurationMs = sw?.Elapsed.TotalMilliseconds ?? 0.0,
                ReRenderDurationMs = sw?.Elapsed.TotalMilliseconds ?? 0.0,
                MemoryBeforeRenderMB = memBefore,
                MemoryAfterRenderMB = memAfter,
                TimestampUtc = DateTime.UtcNow
            };

            _diagnosticsStore.AddStats(stats);

            if (AppFeatureFlags.EnableProjectPerformanceCapture)
            {
                Debug.WriteLine(
                    $"[RenderStats] Quality={stats.RenderQuality} Elements={stats.TotalElements} " +
                $"Visible={stats.VisibleElements} Skipped={stats.IncludeInRenderSkippedCount} " +
                $"BatchedGroups={stats.BatchedGroupCount} HitBoxes={stats.HitTestBoxCount} " +
                $"RenderGeomModels={stats.RenderGeometryModel3DCount} HitTestGeomModels={stats.HitTestGeometryModel3DCount} " +
                $"MeshCacheHits={stats.MeshCacheHitCount} MeshCacheMisses={stats.MeshCacheMissCount} MeshCacheHitRatio={stats.MeshCacheHitRatio:P1} " +
                $"HQCacheHits={stats.HighQualityMeshCacheHitCount} HQCacheMisses={stats.HighQualityMeshCacheMissCount} HQCacheHitRatio={stats.HighQualityMeshCacheHitRatio:P1} " +
                $"HQMeshesCreated={stats.HighQualityMeshesCreatedCount} HQCacheSize={stats.HighQualityCacheCount} " +
                $"DurationMs={stats.RenderDurationMs:F1} MemBefore={memBefore:F1}MB MemAfter={memAfter:F1}MB");
            }
        }

        }
        public void UpdateElementMaterials(System.Collections.Generic.IReadOnlyList<CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.ElementLiftResult>? results = null)
        {
            UpdateElementMaterialsCore(
                results,
                releaseOptimizationHighlight: results != null,
                reRenderIfMissing: true);
        }

        public void ApplyOptimizationResultHighlight(
            IEnumerable<string> liftableElementIds,
            IEnumerable<string> notLiftableElementIds)
        {
            ArgumentNullException.ThrowIfNull(liftableElementIds);
            ArgumentNullException.ThrowIfNull(notLiftableElementIds);

            string[] liftable = liftableElementIds.ToArray();
            string[] notLiftable = notLiftableElementIds.ToArray();
            _optimizationResultViewportState.Apply(liftable, notLiftable);
            LogMissingOptimizationResultIds(liftable.Concat(notLiftable));
            UpdateElementMaterialsCore(
                null,
                releaseOptimizationHighlight: false,
                reRenderIfMissing: false);
        }

        public void ClearOptimizationResultHighlight()
        {
            _optimizationResultViewportState.Clear();
            UpdateElementMaterialsCore(
                null,
                releaseOptimizationHighlight: false,
                reRenderIfMissing: false);
        }

        private void UpdateElementMaterialsCore(
            System.Collections.Generic.IReadOnlyList<CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.ElementLiftResult>? results,
            bool releaseOptimizationHighlight,
            bool reRenderIfMissing)
        {
            if (_context == null)
                return;

            if (releaseOptimizationHighlight)
                _optimizationResultViewportState.Release();

            if (_renderedElementModels.Count == 0 || _sceneElements.Count == 0)
            {
                if (reRenderIfMissing)
                    ReRenderElements();
                return;
            }

            var liftResults = results ?? _context.GetLiftResults();
            bool hasFinalAssignments = liftResults != null && liftResults.Any(r => r.IsFinalAssigned);
            string selectedPointId = liftResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.CandidatePointId))?.CandidatePointId ?? string.Empty;

            var viewMode = _context.GetGAViewMode?.Invoke() ?? GAViewMode.FinalAssignment;

            var statusMap = new Dictionary<string, LiftStatus?>(StringComparer.Ordinal);
            if (liftResults != null)
            {
                var groupedResults = liftResults
                    .Where(result => !string.IsNullOrWhiteSpace(result.ElementId))
                    .GroupBy(result => result.ElementId, StringComparer.Ordinal);

                foreach (var group in groupedResults)
                {
                    var itemForSelected = group.FirstOrDefault(r => r.CandidatePointId == selectedPointId);
                    var item = itemForSelected
                               ?? group.FirstOrDefault(r => r.IsFinalAssignedToPoint)
                               ?? group.Last();

                    LiftStatus? resolvedStatus;
                    if (viewMode == GAViewMode.SelectedPointFeasibility)
                    {
                        resolvedStatus = (LiftStatus?)(itemForSelected?.Status ?? item.Status);
                    }
                    else // FinalAssignment or AllFeasibleDebug
                    {
                        if (hasFinalAssignments)
                        {
                            if (item.IsFinalAssignedToPoint || (!string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId == selectedPointId))
                            {
                                resolvedStatus = (LiftStatus?)item.Status;
                            }
                            else if (item.IsFinalAssigned && !string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId != selectedPointId)
                            {
                                resolvedStatus = null; // Dimmed/Blueprint Gray
                            }
                            else if (!item.IsFinalAssigned)
                            {
                                resolvedStatus = (LiftStatus?)item.BestFailedLiftStatus;
                            }
                            else
                            {
                                resolvedStatus = null; // Safe neutral fallback
                            }
                        }
                        else
                        {
                            resolvedStatus = (LiftStatus?)item.Status;
                        }
                    }

                    statusMap[group.Key] = resolvedStatus;

                    foreach (var res in group)
                    {
                        if (res.IsAssemblyGroup && res.AssemblyMemberIds != null)
                        {
                            foreach (var memberId in res.AssemblyMemberIds)
                            {
                                if (!string.IsNullOrWhiteSpace(memberId))
                                {
                                    statusMap[memberId] = resolvedStatus;
                                }
                            }
                        }
                    }
                }
            }

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            if (_context.GetSelectedElementIds != null)
            {
                var ids = _context.GetSelectedElementIds();
                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        if (id != null)
                            selectedIds.Add(id);
                    }
                }
            }
            else
            {
                var singleId = _context.GetSelectedElementId();
                if (singleId != null)
                    selectedIds.Add(singleId);
            }

            foreach (var element in _sceneElements)
            {
                if (!_renderedElementModels.TryGetValue(element.ElementID, out var models))
                    continue;

                bool isSelected = selectedIds.Contains(element.ElementID);
                ElementVisualizationStatus visualStatus;

                if (statusMap != null && statusMap.TryGetValue(element.ElementID, out var liftStatus))
                {
                    if (liftStatus == null)
                    {
                        bool isFinalAssignmentMode = (liftResults != null && viewMode == GAViewMode.FinalAssignment);
                        visualStatus = isFinalAssignmentMode
                            ? (isSelected ? ElementVisualizationStatus.Selected : ElementVisualizationStatus.AssignedToOtherPoint)
                            : ElementVisualizationStatus.Default;
                    }
                    else
                    {
                        visualStatus = ElementMaterialProvider.GetStatus(element, liftStatus, isSelected);
                    }
                }
                else
                {
                    visualStatus = ElementMaterialProvider.GetStatus(element, null, isSelected);
                }

                visualStatus = _optimizationResultViewportState.Resolve(
                    element.ElementID,
                    isSelected,
                    visualStatus);

                var material = ElementMaterialProvider.GetMaterial(visualStatus, ElementVisualBuilder.CurrentQuality);

                foreach (var model in models)
                {
                    model.Material = material;
                    model.BackMaterial = material;
                }
            }
        }

        private void LogMissingOptimizationResultIds(IEnumerable<string> requestedIds)
        {
            HashSet<string> sceneIds = _sceneElements
                .Select(element => element.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            string[] missingIds = requestedIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && !sceneIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (missingIds.Length > 0)
            {
                string sample = string.Join(", ", missingIds.Take(10));
                string suffix = missingIds.Length > 10 ? ", ..." : string.Empty;
                Debug.WriteLine(
                    $"[GA Result Viewport] Skipped {missingIds.Length} result element ID(s) not present in the current scene: {sample}{suffix}");
            }
        }

        private static double GetPrivateMemoryMB()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / (1024.0 * 1024.0);
            }
            catch
            {
                return GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            }
        }

        public void ReRenderCranes()
        {
            if (_context == null)
                return;

            var cranes = _scene.GetCranes();
            var zones = _context.GetZones().ToList();

            foreach (var crane in cranes)
                crane.IsValidPlacement = _craneValidation.ValidatePlacement(crane, zones);

            _rendering.Render(cranes, _craneBuilder, "Cranes");

            if (ShowCraneRadius)
                _rendering.Render(cranes, new CraneRadiusBuilder(), "CraneRadius");
            else
                _rendering.ClearLayer("CraneRadius");
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            if (AppFeatureFlags.EnableProjectPerformanceCapture) Debug.WriteLine("Scene ElementsChanged: " + elements.Count);

            _sceneElements.Clear();
            _sceneElements.AddRange(elements);
            _isSyncingSceneElements = true;
            try
            {
                _filterService.SetElements(_sceneElements);
            }
            finally
            {
                _isSyncingSceneElements = false;
            }

            if (_sceneElements.Count == 0)
            {
                _rendering.ClearLayer("Elements");
                ElementsCleared?.Invoke();
                return;
            }

            ElementsChanged?.Invoke(_sceneElements);
            ReRenderElements();
        }

        private void OnElementsSelectionChanged(List<PrecastElement> selected)
        {
            UpdateElementMaterials();
        }

        private void OnFilterStateChanged()
        {

            if (!_isSyncingSceneElements)
                ReRenderElements();
        }

        private void OnCraneChanged(SceneCrane _) => ReRenderCranes();
        private void OnZonesChanged()
        {
            if (_scene.GetCranes().Count > 0)
                ReRenderCranes();
        }

        private void OnCranesCleared()
        {
            _rendering.ClearLayer("Cranes");
            _rendering.ClearLayer("CraneRadius");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _scene.ElementsChanged -= OnElementsChanged;
            _scene.ElementsSelectionChanged -= OnElementsSelectionChanged;
            _filterService.StateChanged -= OnFilterStateChanged;
            _scene.CraneAdded -= OnCraneChanged;
            _scene.CraneMoved -= OnCraneChanged;
            _scene.CraneUpdated -= OnCraneChanged;
            _scene.CraneRemoved -= OnCraneChanged;
            _scene.CranesCleared -= OnCranesCleared;
            _scene.ZonesChanged -= OnZonesChanged;
        }
    }
}
