using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using System.Diagnostics;

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
        private readonly List<PrecastElement> _sceneElements = new();
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
            _filterService.StateChanged += OnFilterStateChanged;
            _scene.CraneAdded += OnCraneChanged;
            _scene.CraneMoved += OnCraneChanged;
            _scene.CraneUpdated += OnCraneChanged;
            _scene.CraneRemoved += OnCraneChanged;
            _scene.CranesCleared += OnCranesCleared;
            _scene.ZoneAdded += OnZoneChanged;
            _scene.ZoneUpdated += OnZoneChanged;
            _scene.ZoneRemoved += OnZoneChanged;
            _scene.ZonesCleared += ReRenderCranes;
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
                return;

            Debug.WriteLine("Rendering Elements: " + visibleElements.Count);

            // ── Diagnostics: pre-render snapshot ──
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var memBefore = GetPrivateMemoryMB();

            var liftResults = _context.GetLiftResults();
            bool hasFinalAssignments = liftResults != null && liftResults.Any(r => r.IsFinalAssignedToPoint);
            string selectedPointId = liftResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.CandidatePointId))?.CandidatePointId ?? string.Empty;

            var statusMap = liftResults
                ?.Where(result => !string.IsNullOrWhiteSpace(result.ElementId))
                .GroupBy(result => result.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var item = group.FirstOrDefault(r => r.CandidatePointId == selectedPointId)
                                   ?? group.FirstOrDefault(r => r.IsFinalAssignedToPoint)
                                   ?? group.Last();

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
                    },
                    StringComparer.OrdinalIgnoreCase);

            var selectedElementId = _context.GetSelectedElementId();
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

                    return (element, status, element.ElementID == selectedElementId);
                }),
                builder,
                "Elements");

            sw.Stop();
            var memAfter = GetPrivateMemoryMB();

            // ── Diagnostics: build stats ──
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
                RenderDurationMs = sw.Elapsed.TotalMilliseconds,
                ReRenderDurationMs = sw.Elapsed.TotalMilliseconds,
                MemoryBeforeRenderMB = memBefore,
                MemoryAfterRenderMB = memAfter,
                TimestampUtc = DateTime.UtcNow
            };

            _diagnosticsStore.AddStats(stats);

            Debug.WriteLine(
                $"[RenderStats] Quality={stats.RenderQuality} Elements={stats.TotalElements} " +
                $"Visible={stats.VisibleElements} Skipped={stats.IncludeInRenderSkippedCount} " +
                $"BatchedGroups={stats.BatchedGroupCount} HitBoxes={stats.HitTestBoxCount} " +
                $"RenderGeomModels={stats.RenderGeometryModel3DCount} HitTestGeomModels={stats.HitTestGeometryModel3DCount} " +
                $"HQCacheHits={stats.HighQualityMeshCacheHitCount} HQCacheMisses={stats.HighQualityMeshCacheMissCount} " +
                $"HQMeshesCreated={stats.HighQualityMeshesCreatedCount} HQCacheSize={stats.HighQualityCacheCount} " +
                $"DurationMs={stats.RenderDurationMs:F1} MemBefore={memBefore:F1}MB MemAfter={memAfter:F1}MB");
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
            Debug.WriteLine("Scene ElementsChanged: " + elements.Count);

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

        private void OnFilterStateChanged()
        {
            if (!_isSyncingSceneElements)
                ReRenderElements();
        }

        private void OnCraneChanged(SceneCrane _) => ReRenderCranes();
        private void OnZoneChanged(GreenAreaZone _) => ReRenderCranes();

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
            _filterService.StateChanged -= OnFilterStateChanged;
            _scene.CraneAdded -= OnCraneChanged;
            _scene.CraneMoved -= OnCraneChanged;
            _scene.CraneUpdated -= OnCraneChanged;
            _scene.CraneRemoved -= OnCraneChanged;
            _scene.CranesCleared -= OnCranesCleared;
            _scene.ZoneAdded -= OnZoneChanged;
            _scene.ZoneUpdated -= OnZoneChanged;
            _scene.ZoneRemoved -= OnZoneChanged;
            _scene.ZonesCleared -= ReRenderCranes;
        }
    }
}
