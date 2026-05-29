using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace CraneOptimizer.UI.Features.F17_Selection.Interaction
{
    /// <summary>
    /// Always-on element selection for the IFC viewport.
    /// Mouse drag uses only a 2D overlay. Box commit projects existing element
    /// bounds; no geometry is duplicated in the scene.
    /// </summary>
    public sealed class SelectionHandler : IInteractionMode, IDisposable
    {
        public static System.Func<HelixViewport3D, Point3D, Point>? TestProjectHook { get; set; }

        public enum BoxSelectionMode { BoundsOnly, MeshProxy }
        public static BoxSelectionMode SelectionPrecision { get; set; } = BoxSelectionMode.MeshProxy;
        public static bool EnableSelectionDiagnostics { get; set; } = false;

        private const int DefaultMeshProxyTriangles = 32;
        private const int HighPrecisionMeshProxyTriangles = 64;
        private const double WindowSelectionTolerancePx = 2.0;

        private const double DragThresholdPx = 5.0;
        private const double TabNearCursorRadiusPx = 8.0;
        private const double CrossingSampleStepPx = 6.0;
        private const int MaxCrossingSamplePoints = 7000;

        private readonly IViewportSelectionService _selection;
        private readonly IViewportHitTestService _hitTest;
        private readonly HoverHitCache _hoverCache;
        private readonly IElementVisibilityFilterService _filterService;
        private readonly IOriginService _originService;

        private Canvas? _overlayCanvas;
        private Rectangle? _rubberBand;

        private bool _clickArmed;
        private bool _dragArmed;
        private bool _isDragging;
        private Point _dragStart;
        private Point _dragCurrent;
        private bool _lastWasCrossing;

        private List<ElementProjection>? _cachedProjections;
        private ProjectionCacheKey _cachedKey;

        private readonly record struct ProjectionCacheKey(
            Point3D CameraPosition,
            Vector3D CameraLookDirection,
            double ViewportWidth,
            double ViewportHeight,
            int VisibleElementsCount,
            int SpatialRegistryCount,
            BoxSelectionMode Precision);

        public SelectionHandler(
            IViewportSelectionService selection,
            IViewportHitTestService hitTest,
            HoverHitCache hoverCache,
            IElementVisibilityFilterService filterService,
            IOriginService originService)
        {
            _selection = selection;
            _hitTest = hitTest;
            _hoverCache = hoverCache;
            _filterService = filterService;
            _originService = originService;
        }

        public bool IsEnabled { get; set; } = true;

        public void SetOverlayCanvas(Canvas canvas) => _overlayCanvas = canvas;

        public bool IsClickOnViewCubeOrOverlay(HelixViewport3D viewport, RoutedEventArgs e, bool skipFindHits = false)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null)
                return false;

            DependencyObject? current = originalSource;
            while (current != null && current != viewport)
            {
                var typeName = current.GetType().Name;
                if (typeName.Contains("ViewCube") || 
                    typeName.Contains("CoordinateSystem") || 
                    typeName.Contains("Button") || 
                    typeName.Contains("Menu") || 
                    typeName.Contains("ComboBox") || 
                    typeName.Contains("TextBox") ||
                    current is Button || 
                    current is ComboBox || 
                    current is MenuItem ||
                    current is TextBox)
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (!skipFindHits && e is MouseEventArgs mouseEventArgs)
            {
                var pos = mouseEventArgs.GetPosition(viewport);
                try
                {
                    var hits = viewport.Viewport.FindHits(pos);
                    if (hits != null)
                    {
                        foreach (var hit in hits)
                        {
                            var visual = hit.Visual;
                            while (visual != null)
                            {
                                var typeName = visual.GetType().Name;
                                if (typeName.Contains("ViewCube") || 
                                    typeName.Contains("CoordinateSystem") || 
                                    typeName.Contains("CameraDirectVisual") || 
                                    (typeName.Contains("ModelVisual3D") && visual.GetType().FullName != null && visual.GetType().FullName.Contains("HelixToolkit")))
                                {
                                    return true;
                                }
                                visual = VisualTreeHelper.GetParent(visual) as Visual3D;
                            }
                        }
                    }
                }
                catch { }
            }

            return false;
        }

        public void OnPreviewMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            ArmBoxSelectIfEmpty(viewport, e);
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            // Clicks on elements/empty space are already armed/hit-tested in OnPreviewMouseDown.
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (!IsEnabled)
                return;

            if (_isDragging)
                return;

            if (IsClickOnViewCubeOrOverlay(viewport, e, skipFindHits: true))
                return;

            _hoverCache.OnMouseMove(e.GetPosition(viewport));
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.ChangedButton != MouseButton.Left)
                return;

            if (IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            if (_isDragging)
            {
                try
                {
                    var pos = e.GetPosition(viewport);
                    if (pos.X != 0 || pos.Y != 0)
                    {
                        _dragCurrent = pos;
                    }
                    FinishBoxSelect(viewport);
                    e.Handled = true;
                }
                finally
                {
                    ClearRubberBand();
                    _isDragging = false;
                    _dragArmed = false;
                    _clickArmed = false;
                    ReleaseMouseCapture(viewport);
                }
                return;
            }

            _clickArmed = false;
            _dragArmed = false;
        }

        public void OnPreviewMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (!IsEnabled || e.LeftButton != MouseButtonState.Pressed || !_dragArmed)
                return;

            // During active drag, skip expensive ViewCube/FindHits check
            if (!_isDragging && IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            var pos = e.GetPosition(viewport);

            if (!_isDragging)
            {
                if (!HasMovedEnoughToDrag(pos))
                    return;

                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                    _selection.Clear();

                _isDragging = true;
                Mouse.Capture(viewport, CaptureMode.SubTree);
            }

            e.Handled = true;
            _dragCurrent = pos;
            UpdateRubberBand();
        }

        public void OnPreviewMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.ChangedButton != MouseButton.Left || !_dragArmed)
                return;

            if (IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            try
            {
                if (_isDragging)
                {
                    var pos = e.GetPosition(viewport);
                    if (pos.X != 0 || pos.Y != 0)
                    {
                        _dragCurrent = pos;
                    }
                    FinishBoxSelect(viewport);
                    e.Handled = true;
                }
            }
            finally
            {
                ClearRubberBand();
                _isDragging = false;
                _dragArmed = false;
                _clickArmed = false;
                ReleaseMouseCapture(viewport);
            }
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            if (!IsEnabled)
                return;

            if (e.Key == Key.Escape)
            {
                _selection.Clear();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                SelectNextTabCandidate(viewport, e);
            }
        }

        private void ArmBoxSelectIfEmpty(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.ChangedButton != MouseButton.Left)
                return;

            if (IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            _clickArmed = false;
            _dragArmed = false;
            _isDragging = false;

            var pos = e.GetPosition(viewport);
            var rayHits = GetRayHitElements(viewport, pos).ToList();
            if (rayHits.Count > 0)
            {
                _clickArmed = true;
                _dragStart = pos;
                _dragCurrent = pos;
                
                // Fetch the fully sorted and filtered candidate list for hover cache
                var candidates = GetTabCandidateElements(viewport, pos).ToList();
                _hoverCache.SetCandidates(pos, candidates);
                return;
            }

            var hit = _hitTest.GetHitObject(viewport, pos);
            if (hit != null)
            {
                return;
            }

            _dragArmed = true;
            _isDragging = false;
            _dragStart = pos;
            _dragCurrent = pos;
        }

        private class TabCandidate
        {
            public PrecastElement Element { get; }
            public bool IsRayHit { get; }
            public double RayDistance { get; }
            public double ScreenDistanceSq { get; }
            public string GlobalId { get; }

            public TabCandidate(PrecastElement element, bool isRayHit, double rayDistance, double screenDistanceSq, string globalId)
            {
                Element = element;
                IsRayHit = isRayHit;
                RayDistance = rayDistance;
                ScreenDistanceSq = screenDistanceSq;
                GlobalId = globalId ?? string.Empty;
            }
        }

        private void SelectNextTabCandidate(HelixViewport3D viewport, KeyEventArgs e)
        {
            if (!EnsureHoverCandidates(viewport))
                return;

            var current = _selection.GetSelectedTargets().Select(t => t.UnderlyingObject).OfType<PrecastElement>().FirstOrDefault();
            var currentInCandidates = current != null &&
                                      _hoverCache.Candidates.Any(candidate => ReferenceEquals(candidate, current));

            bool isShiftHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            PrecastElement? next;
            if (currentInCandidates)
            {
                _hoverCache.AlignTo(current!);
                next = isShiftHeld ? _hoverCache.CyclePrevious() : _hoverCache.CycleNext();
            }
            else
            {
                next = _hoverCache.CurrentCandidate;
            }

            if (next == null)
                return;

            if (isCtrlHeld)
            {
                _selection.ToggleElement(next);
            }
            else
            {
                _selection.SetElements(new[] { next });
            }
            e.Handled = true;
        }

        private bool EnsureHoverCandidates(HelixViewport3D viewport)
        {
            var currentPosition = Mouse.GetPosition(viewport);
            if (_hoverCache.HasCandidates && IsCacheFreshFor(currentPosition))
                return true;

            _hoverCache.Invalidate();

            var candidates = GetTabCandidateElements(viewport, currentPosition).ToList();
            if (candidates.Count == 0)
                return false;

            _hoverCache.SetCandidates(currentPosition, candidates);
            return true;
        }

        private bool IsCacheFreshFor(Point currentPosition)
        {
            if (!_hoverCache.HasLastCursorPosition)
                return false;

            var last = _hoverCache.LastCursorPosition;
            return Math.Abs(currentPosition.X - last.X) <= HoverHitCache.MovementThresholdPx &&
                   Math.Abs(currentPosition.Y - last.Y) <= HoverHitCache.MovementThresholdPx;
        }

        private IEnumerable<PrecastElement> GetTabCandidateElements(HelixViewport3D viewport, Point pos)
        {
            var rayHits = new Dictionary<PrecastElement, double>(ReferenceEqualityComparer.Instance);
            try
            {
                var hits = viewport.Viewport.FindHits(pos);
                if (hits != null)
                {
                    foreach (var hit in hits)
                    {
                        var element = TryGetElementTag(hit.Visual);
                        if (element != null && _filterService.IsElementIncluded(element))
                        {
                            double dist = hit.Distance;
                            if (!rayHits.TryGetValue(element, out var existingDist) || dist < existingDist)
                            {
                                rayHits[element] = dist;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Tab cycling is optional; failed ray hits simply mean no cycle.
            }

            var nearProjections = new Dictionary<PrecastElement, double>(ReferenceEqualityComparer.Instance);
            var maxDistanceSq = TabNearCursorRadiusPx * TabNearCursorRadiusPx;
            foreach (var projection in GetElementProjections(viewport))
            {
                if (!_filterService.IsElementIncluded(projection.Element))
                    continue;

                var distSq = GetDistanceToProjectionSquared(pos, projection);
                if (distSq <= maxDistanceSq)
                {
                    nearProjections[projection.Element] = distSq;
                }
            }

            var candidates = new List<TabCandidate>();
            var allElements = new HashSet<PrecastElement>(rayHits.Keys.Concat(nearProjections.Keys), ReferenceEqualityComparer.Instance);

            foreach (var element in allElements)
            {
                bool isRayHit = rayHits.TryGetValue(element, out var rayDist);
                double screenDistSq = nearProjections.TryGetValue(element, out var sDistSq) ? sDistSq : 0.0;
                string globalId = element.ElementID ?? string.Empty;

                candidates.Add(new TabCandidate(element, isRayHit, isRayHit ? rayDist : double.MaxValue, screenDistSq, globalId));
            }

            return candidates
                .OrderBy(c => c.IsRayHit ? 0 : 1) // Ray hits first, then near-projections
                .ThenBy(c => c.IsRayHit ? c.RayDistance : c.ScreenDistanceSq) // Closest ray distance for ray hits, closest screen distance for projections
                .ThenBy(c => c.GlobalId) // Global ID tie-breaker
                .Select(c => c.Element);
        }

        private IEnumerable<PrecastElement> GetRayHitElements(HelixViewport3D viewport, Point pos)
        {
            var result = new List<PrecastElement>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);

            try
            {
                var hits = viewport.Viewport.FindHits(pos);
                if (hits != null)
                {
                    foreach (var hit in hits)
                    {
                        var element = TryGetElementTag(hit.Visual);
                        if (element != null && _filterService.IsElementIncluded(element) && seen.Add(element))
                            result.Add(element);
                    }
                }
            }
            catch
            {
                // Tab cycling is optional; failed ray hits simply mean no cycle.
            }

            return result;
        }

        private void FinishBoxSelect(HelixViewport3D viewport)
        {
            var rect = GetDragRect();
            if (rect.Width < 2 || rect.Height < 2)
                return;

            var isCrossing = _dragStart.X > _dragCurrent.X;

            System.Diagnostics.Stopwatch? sw = null;
#if DEBUG
            if (EnableSelectionDiagnostics)
            {
                sw = System.Diagnostics.Stopwatch.StartNew();
            }
#endif

            var hits = GetElementsInRect(viewport, rect, isCrossing);

#if DEBUG
            if (EnableSelectionDiagnostics && sw != null)
            {
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[Box Selection Audit]");
                System.Diagnostics.Debug.WriteLine($"  DragStart: ({_dragStart.X:F1}, {_dragStart.Y:F1})");
                System.Diagnostics.Debug.WriteLine($"  DragEnd: ({_dragCurrent.X:F1}, {_dragCurrent.Y:F1})");
                System.Diagnostics.Debug.WriteLine($"  SelectionRect: {rect}");
                System.Diagnostics.Debug.WriteLine($"  IsCrossing: {isCrossing}");
                System.Diagnostics.Debug.WriteLine($"  SelectionPrecision: {SelectionPrecision}");
                System.Diagnostics.Debug.WriteLine($"  MouseUpEvaluationTimeMs: {sw.Elapsed.TotalMilliseconds:F2}");
                System.Diagnostics.Debug.WriteLine($"  MatchedElementsCount: {hits.Count}");
                System.Diagnostics.Debug.WriteLine($"  SelectedGlobalIds: {string.Join(", ", hits.Select(h => h.ElementID))}");
            }
#endif

            bool isCtrlHeld = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (isCtrlHeld)
            {
                _selection.SelectElements(hits, isCtrlHeld: true);
            }
            else
            {
                _selection.SetElements(hits);
            }
        }

        private List<PrecastElement> GetElementsInRect(HelixViewport3D viewport, Rect screenRect, bool crossing)
        {
            var result = new List<PrecastElement>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);

            var spatialElements = ElementVisualBuilder.GetSpatialHitTestElements();
            var spatialLookup = new Dictionary<string, ElementVisualBuilder.SpatialHitTestElement>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var se in spatialElements)
            {
                if (!string.IsNullOrEmpty(se.ElementId))
                    spatialLookup[se.ElementId] = se;
            }

            int broadCandidateCount = 0;
            int narrowAcceptedCount = 0;

            foreach (var projection in GetElementProjections(viewport))
            {
                // Stage 1: Broad phase — AABB check
                bool broadMatch;
                if (crossing)
                {
                    broadMatch = screenRect.IntersectsWith(projection.ScreenBounds) ||
                                 projection.ScreenSamples.Any(screenRect.Contains);
                }
                else
                {
                    broadMatch = BoundaryBoxFullyContained(screenRect, projection.ScreenBounds) &&
                                 projection.ScreenSamples.All(screenRect.Contains);
                }

                if (!broadMatch)
                    continue;

                broadCandidateCount++;

                // Stage 2: Narrow phase — mesh proxy test
                bool narrowMatch;
                Point[]? meshProxySamples = null;
                Tuple<Point, Point>[]? meshProxyEdges = null;

                if (SelectionPrecision == BoxSelectionMode.MeshProxy &&
                    !string.IsNullOrEmpty(projection.Element.ElementID) &&
                    spatialLookup.TryGetValue(projection.Element.ElementID, out var spatial))
                {
                    int maxTriangles = DefaultMeshProxyTriangles;
                    // Allow 64 only when candidate count is small (e.g. < 10)
                    if (broadCandidateCount < 10)
                    {
                        maxTriangles = HighPrecisionMeshProxyTriangles;
                    }

                    BuildMeshProxy(viewport, spatial, maxTriangles, out meshProxySamples, out meshProxyEdges);

                    if (meshProxySamples != null && meshProxySamples.Length > 0)
                    {
                        narrowMatch = NarrowPhaseTest(screenRect, meshProxySamples, meshProxyEdges, crossing);
                    }
                    else
                    {
                        // Approximate AABB fallback: stricter rules
                        if (crossing)
                        {
                            narrowMatch = projection.ScreenSamples.Any(screenRect.Contains);
                        }
                        else
                        {
                            narrowMatch = true; // Broad-phase window selection is already strict
                        }
                    }
                }
                else
                {
                    // BoundsOnly mode or fallback: AABB-only
                    if (crossing)
                    {
                        narrowMatch = projection.ScreenSamples.Any(screenRect.Contains);
                    }
                    else
                    {
                        narrowMatch = true;
                    }
                }

#if DEBUG
                if (EnableSelectionDiagnostics)
                {
                    var reason = narrowMatch ? "ACCEPTED" : "REJECTED_NARROW";
                    var meshCount = meshProxySamples?.Length ?? 0;
                    var edgeCount = meshProxyEdges?.Length ?? 0;
                    var edgeIntersectResult = (crossing && meshProxyEdges != null && !meshProxySamples.Any(screenRect.Contains)) 
                        ? (narrowMatch ? "EdgeIntersected" : "NoEdgeIntersection")
                        : "N/A";

                    System.Diagnostics.Debug.WriteLine(
                        $"  [{reason}] {projection.Element.ElementID} " +
                        $"Bounds={projection.ScreenBounds} MeshSamples={meshCount} Edges={edgeCount} " +
                        $"EdgeIntersect={edgeIntersectResult}");
                }
#endif

                if (narrowMatch && seen.Add(projection.Element))
                {
                    narrowAcceptedCount++;
                    result.Add(projection.Element);
                }
            }

#if DEBUG
            if (EnableSelectionDiagnostics)
            {
                System.Diagnostics.Debug.WriteLine($"  BroadCandidates: {broadCandidateCount}");
                System.Diagnostics.Debug.WriteLine($"  NarrowAccepted: {narrowAcceptedCount}");
            }
#endif

            return result;
        }

        private static bool NarrowPhaseTest(Rect screenRect, Point[] meshSamples, Tuple<Point, Point>[]? meshEdges, bool crossing)
        {
            if (crossing)
            {
                // Crossing: at least one mesh proxy point inside, OR at least one edge intersects
                if (meshSamples.Any(screenRect.Contains))
                    return true;

                if (meshEdges != null)
                {
                    foreach (var edge in meshEdges)
                    {
                        if (SegmentIntersectsRect(edge.Item1, edge.Item2, screenRect))
                            return true;
                    }
                }

                return false;
            }
            else
            {
                // Window: all mesh proxy samples must be inside the rectangle (with tiny tolerance)
                var tolerantRect = new Rect(
                    screenRect.X - WindowSelectionTolerancePx,
                    screenRect.Y - WindowSelectionTolerancePx,
                    screenRect.Width + WindowSelectionTolerancePx * 2,
                    screenRect.Height + WindowSelectionTolerancePx * 2);

                return meshSamples.All(tolerantRect.Contains);
            }
        }

        private IEnumerable<ElementProjection> GetNearCursorProjections(
            HelixViewport3D viewport,
            Point pos,
            double radius)
        {
            var maxDistanceSq = radius * radius;

            return GetElementProjections(viewport)
                .Select(projection => new
                {
                    Projection = projection,
                    DistanceSq = GetDistanceToProjectionSquared(pos, projection)
                })
                .Where(item => item.DistanceSq <= maxDistanceSq)
                .OrderBy(item => item.DistanceSq)
                .ThenBy(item => item.Projection.Depth)
                .Select(item => item.Projection);
        }

        private List<ElementProjection> GetElementProjections(HelixViewport3D viewport)
        {
            var projCam = viewport.Camera as ProjectionCamera;
            var camPos = projCam?.Position ?? new Point3D();
            var camLook = projCam?.LookDirection ?? new Vector3D(0, 0, -1);
            var width = viewport.ActualWidth;
            var height = viewport.ActualHeight;
            var visibleCount = _filterService.VisibleElements.Count;
            
            // Get spatial registry elements count
            var spatialElements = ElementVisualBuilder.GetSpatialHitTestElements();
            var spatialCount = spatialElements.Count;

            var key = new ProjectionCacheKey(camPos, camLook, width, height, visibleCount, spatialCount, SelectionPrecision);

            if (_cachedProjections != null && _cachedKey.Equals(key))
            {
                return _cachedProjections;
            }

            var projections = new List<ElementProjection>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);

            // Build dictionary once to avoid O(N^2) linear lookup
            var spatialLookup = new Dictionary<string, ElementVisualBuilder.SpatialHitTestElement>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var se in spatialElements)
            {
                if (!string.IsNullOrEmpty(se.ElementId))
                    spatialLookup[se.ElementId] = se;
            }

            foreach (var child in viewport.Children.OfType<ModelVisual3D>())
                CollectElementProjections(viewport, child, Transform3D.Identity, projections, seen);

            foreach (var element in _filterService.VisibleElements)
            {
                if (TryProjectElementBox(viewport, element, spatialLookup, out var screenBounds, out var samples, out var depth) &&
                    seen.Add(element))
                {
                    projections.Add(new ElementProjection(element, screenBounds, samples, depth));
                }
            }

            _cachedProjections = projections;
            _cachedKey = key;

            return projections;
        }

        private void CollectElementProjections(
            HelixViewport3D viewport,
            ModelVisual3D visual,
            Transform3D parentTransform,
            List<ElementProjection> projections,
            HashSet<PrecastElement> seen)
        {
            var visualTransform = Combine(parentTransform, visual.Transform);
            var element = visual.GetValue(FrameworkElement.TagProperty) as PrecastElement;

            if (element != null &&
                _filterService.IsElementIncluded(element) &&
                !seen.Contains(element) &&
                TryProjectVisualBounds(viewport, visual, visualTransform, out var screenBounds, out var samples, out var depth) &&
                seen.Add(element))
            {
                projections.Add(new ElementProjection(element, screenBounds, samples, depth));
            }

            foreach (var child in visual.Children.OfType<ModelVisual3D>())
                CollectElementProjections(viewport, child, visualTransform, projections, seen);
        }

        private static PrecastElement? TryGetElementTag(Visual3D? visual)
        {
            while (visual != null)
            {
                var tag = visual.GetValue(FrameworkElement.TagProperty);
                if (tag is PrecastElement element)
                    return element;

                visual = VisualTreeHelper.GetParent(visual) as Visual3D;
            }

            return null;
        }

        private bool TryProjectVisualBounds(
            HelixViewport3D viewport,
            ModelVisual3D visual,
            Transform3D transform,
            out Rect screenBounds,
            out Point[] samples,
            out double depth)
        {
            screenBounds = Rect.Empty;
            samples = [];
            depth = 0d;

            if (visual.Content == null)
                return false;

            var worldPoints = new List<Point3D>();
            CollectModelBoundsSamplePoints(visual.Content, transform, worldPoints);

            return TryProjectWorldPoints(viewport, worldPoints, out screenBounds, out samples, out depth);
        }

        private static bool IsValidAABB(Point3D min, Point3D max)
        {
            double dx = max.X - min.X;
            double dy = max.Y - min.Y;
            double dz = max.Z - min.Z;

            if (double.IsNaN(dx) || double.IsNaN(dy) || double.IsNaN(dz) ||
                double.IsInfinity(dx) || double.IsInfinity(dy) || double.IsInfinity(dz))
            {
                return false;
            }

            if (dx <= 1e-4 || dy <= 1e-4 || dz <= 1e-4)
            {
                return false;
            }

            if (dx > 150.0 || dy > 150.0 || dz > 150.0)
            {
                return false;
            }

            double volume = dx * dy * dz;
            if (volume > 200000.0)
            {
                return false;
            }

            return true;
        }

        private bool TryProjectElementBox(
            HelixViewport3D viewport,
            PrecastElement element,
            Dictionary<string, ElementVisualBuilder.SpatialHitTestElement> spatialLookup,
            out Rect screenBounds,
            out Point[] samples,
            out double depth)
        {
            // Try to find the element in the cached spatial registry lookup
            ElementVisualBuilder.SpatialHitTestElement? spatialElement = null;
            if (!string.IsNullOrEmpty(element.ElementID))
                spatialLookup.TryGetValue(element.ElementID, out spatialElement);

            if (spatialElement != null)
            {
                if (!spatialElement.IsVisible)
                {
                    screenBounds = Rect.Empty;
                    samples = [];
                    depth = 0d;
                    return false;
                }

                var min = spatialElement.Min;
                var max = spatialElement.Max;
                if (IsValidAABB(min, max))
                {
                    var spatialBounds = new Rect3D(min.X, min.Y, min.Z, max.X - min.X, max.Y - min.Y, max.Z - min.Z);
                    return TryProjectBounds(viewport, spatialBounds, Transform3D.Identity, out screenBounds, out samples, out depth);
                }

                // If present in spatial registry but bounds are invalid/outliers/NaN, reject completely
                screenBounds = Rect.Empty;
                samples = [];
                depth = 0d;
                return false;
            }

            // Fallback to COG-based bounding box
            var cog = element.ElementCOG;
            var origin = _originService.OriginPoint;
            var boundsCenter = new Point3D(
                cog.X - (origin?.X ?? 0d),
                cog.Y - (origin?.Y ?? 0d),
                cog.Z - (origin?.Z ?? 0d));

            var length = Math.Max(element.BBoxLength ?? element.ElementLength ?? 1d, 0.01d);
            var width = Math.Max(element.BBoxWidth ?? element.ElementWidth ?? 1d, 0.01d);
            var height = Math.Max(element.BBoxHeight ?? element.ElementHeight ?? 1d, 0.01d);

            var fallbackMin = new Point3D(boundsCenter.X - length / 2d, boundsCenter.Y - width / 2d, boundsCenter.Z - height / 2d);
            var fallbackMax = new Point3D(boundsCenter.X + length / 2d, boundsCenter.Y + width / 2d, boundsCenter.Z + height / 2d);

            if (!IsValidAABB(fallbackMin, fallbackMax))
            {
                screenBounds = Rect.Empty;
                samples = [];
                depth = 0d;
                return false;
            }

            var fallbackBounds = new Rect3D(fallbackMin.X, fallbackMin.Y, fallbackMin.Z, length, width, height);
            return TryProjectBounds(viewport, fallbackBounds, Transform3D.Identity, out screenBounds, out samples, out depth);
        }

        private static bool TryProjectBounds(
            HelixViewport3D viewport,
            Rect3D bounds,
            Transform3D transform,
            out Rect screenBounds,
            out Point[] samples,
            out double depth)
        {
            screenBounds = Rect.Empty;
            samples = [];
            depth = 0d;

            if (bounds.IsEmpty)
                return false;

            var matrix = transform.Value;
            var worldPoints = GetBoundsSamplePoints(bounds)
                .Select(point => matrix.Transform(point));

            return TryProjectWorldPoints(viewport, worldPoints, out screenBounds, out samples, out depth);
        }

        private static bool TryProjectWorldPoints(
            HelixViewport3D viewport,
            IEnumerable<Point3D> worldPoints,
            out Rect screenBounds,
            out Point[] samples,
            out double depth)
        {
            screenBounds = Rect.Empty;
            samples = [];
            depth = 0d;

            Rect? rect = null;
            var depthSum = 0d;
            var projectedCount = 0;
            var projectedPoints = new List<Point>();

            var projCam = viewport.Camera as ProjectionCamera;
            Vector3D lookDir = new Vector3D(0, 0, -1);
            Point3D camPos = new Point3D(0, 0, 0);
            double nearPlane = 0.05;
            bool hasProjCam = false;

            if (projCam != null)
            {
                camPos = projCam.Position;
                var rawLook = projCam.LookDirection;
                var len = rawLook.Length;
                if (len > 0.0001)
                {
                    lookDir = rawLook / len;
                }

                if (projCam.NearPlaneDistance > 0.0001)
                {
                    nearPlane = projCam.NearPlaneDistance;
                }
                hasProjCam = true;
            }

            const double epsilon = 0.01;
            double depthThreshold = nearPlane + epsilon;

            foreach (var worldPoint in worldPoints)
            {
                if (hasProjCam)
                {
                    var toPoint = worldPoint - camPos;
                    var ptDepth = Vector3D.DotProduct(toPoint, lookDir);
                    if (ptDepth <= depthThreshold)
                    {
                        // Point is behind camera or inside near plane, skip it.
                        continue;
                    }
                }

                Point screenPoint;
                try
                {
                    if (TestProjectHook != null)
                    {
                        screenPoint = TestProjectHook(viewport, worldPoint);
                    }
                    else
                    {
                        var wpfPoint = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, worldPoint);
                        // Explicit coordinate space translation using SafeTranslatePoint helper
                        screenPoint = SafeTranslatePoint(viewport.Viewport, wpfPoint, viewport);
                    }
                }
                catch
                {
                    continue;
                }

                // Sanity check: finite coordinates
                if (double.IsNaN(screenPoint.X) || double.IsInfinity(screenPoint.X) ||
                    double.IsNaN(screenPoint.Y) || double.IsInfinity(screenPoint.Y))
                {
                    continue;
                }

                var pointRect = new Rect(screenPoint, new Size(0, 0));
                rect = rect.HasValue ? Rect.Union(rect.Value, pointRect) : pointRect;
                projectedPoints.Add(screenPoint);
                depthSum += GetCameraDistance(viewport.Camera, worldPoint);
                projectedCount++;
            }

            if (!rect.HasValue || projectedCount == 0)
                return false;

            var finalRect = InflateTinyRect(rect.Value);

            // Sanity check: finite sizes
            if (double.IsNaN(finalRect.Width) || double.IsInfinity(finalRect.Width) ||
                double.IsNaN(finalRect.Height) || double.IsInfinity(finalRect.Height))
            {
                return false;
            }

            // Sanity check: strictly positive width/height
            if (finalRect.Width <= 0.001 || finalRect.Height <= 0.001)
                return false;

            // Strict sanity check: reasonable width/height relative to viewport dimensions
            double viewWidth = viewport.ActualWidth > 0 ? viewport.ActualWidth : 1920;
            double viewHeight = viewport.ActualHeight > 0 ? viewport.ActualHeight : 1080;

            if (finalRect.Width > viewWidth * 4.0 || finalRect.Height > viewHeight * 4.0)
                return false;

            // Strict sanity check: reasonable area relative to viewport area
            var rectArea = finalRect.Width * finalRect.Height;
            var viewArea = viewWidth * viewHeight;
            if (rectArea > viewArea * 8.0)
                return false;

            // Strict sanity check: intersects visible viewport bounds
            var viewportBounds = new Rect(0, 0, viewWidth, viewHeight);
            if (!viewportBounds.IntersectsWith(finalRect))
                return false;

            screenBounds = finalRect;
            samples = [.. projectedPoints];
            depth = depthSum / projectedCount;
            return true;
        }

        private static Point SafeTranslatePoint(UIElement fromElement, Point point, UIElement toElement)
        {
            try
            {
                if (PresentationSource.FromVisual(fromElement) != null)
                {
                    return fromElement.TranslatePoint(point, toElement);
                }
            }
            catch
            {
            }
            return point; // Safe fallback when detached (e.g. in unit tests)
        }

        private static void CollectModelBoundsSamplePoints(
            Model3D model,
            Transform3D parentTransform,
            List<Point3D> worldPoints)
        {
            var modelTransform = Combine(parentTransform, model.Transform);

            if (model is GeometryModel3D geometryModel)
            {
                var bounds = geometryModel.Geometry?.Bounds ?? Rect3D.Empty;
                if (bounds.IsEmpty)
                    return;

                var matrix = modelTransform.Value;
                foreach (var point in GetBoundsSamplePoints(bounds))
                    worldPoints.Add(matrix.Transform(point));

                return;
            }

            if (model is not Model3DGroup group)
                return;

            foreach (var child in group.Children)
                CollectModelBoundsSamplePoints(child, modelTransform, worldPoints);
        }

        private static Rect InflateTinyRect(Rect rect)
        {
            if (rect.Width < 4)
                rect.Inflate((4 - rect.Width) / 2d, 0);
            if (rect.Height < 4)
                rect.Inflate(0, (4 - rect.Height) / 2d);

            return rect;
        }

        private static void BuildMeshProxy(
            HelixViewport3D viewport,
            ElementVisualBuilder.SpatialHitTestElement spatialElement,
            int maxTriangles,
            out Point[]? projectedVertices,
            out Tuple<Point, Point>[]? projectedEdges)
        {
            projectedVertices = null;
            projectedEdges = null;

            if (spatialElement.Meshes == null || spatialElement.Meshes.Count == 0)
                return;

            var projCam = viewport.Camera as ProjectionCamera;
            Vector3D lookDir = new Vector3D(0, 0, -1);
            Point3D camPos = new Point3D(0, 0, 0);
            double nearPlane = 0.05;
            bool hasProjCam = false;

            if (projCam != null)
            {
                camPos = projCam.Position;
                var rawLook = projCam.LookDirection;
                var len = rawLook.Length;
                if (len > 0.0001) lookDir = rawLook / len;
                if (projCam.NearPlaneDistance > 0.0001) nearPlane = projCam.NearPlaneDistance;
                hasProjCam = true;
            }

            double depthThreshold = nearPlane + 0.01;
            var validVertices = new List<Point>();
            var validEdges = new List<Tuple<Point, Point>>();
            int trianglesBudget = maxTriangles;

            foreach (var mesh in spatialElement.Meshes)
            {
                var geometry = mesh.Mesh;
                var transform = mesh.Transform;

                if (trianglesBudget <= 0)
                    break;

                if (geometry == null)
                    continue;

                var positions = geometry.Positions;
                var indices = geometry.TriangleIndices;

                if (positions == null || positions.Count == 0)
                    continue;

                // If triangle indices available, sample triangles for vertices + edges
                if (indices != null && indices.Count >= 3)
                {
                    int triCount = indices.Count / 3;
                    int stride = Math.Max(1, triCount / trianglesBudget);

                    for (int t = 0; t < triCount && trianglesBudget > 0; t += stride)
                    {
                        int i0 = indices[t * 3];
                        int i1 = indices[t * 3 + 1];
                        int i2 = indices[t * 3 + 2];

                        if (i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count)
                            continue;

                        var wp0 = transform.Transform(positions[i0]);
                        var wp1 = transform.Transform(positions[i1]);
                        var wp2 = transform.Transform(positions[i2]);

                        var sp0 = ProjectSinglePoint(viewport, wp0, camPos, lookDir, depthThreshold, hasProjCam);
                        var sp1 = ProjectSinglePoint(viewport, wp1, camPos, lookDir, depthThreshold, hasProjCam);
                        var sp2 = ProjectSinglePoint(viewport, wp2, camPos, lookDir, depthThreshold, hasProjCam);

                        if (sp0.HasValue) validVertices.Add(sp0.Value);
                        if (sp1.HasValue) validVertices.Add(sp1.Value);
                        if (sp2.HasValue) validVertices.Add(sp2.Value);

                        if (sp0.HasValue && sp1.HasValue) validEdges.Add(Tuple.Create(sp0.Value, sp1.Value));
                        if (sp1.HasValue && sp2.HasValue) validEdges.Add(Tuple.Create(sp1.Value, sp2.Value));
                        if (sp2.HasValue && sp0.HasValue) validEdges.Add(Tuple.Create(sp2.Value, sp0.Value));

                        trianglesBudget--;
                    }
                }
                else
                {
                    // No triangle indices — sample vertices only
                    int vertStride = Math.Max(1, positions.Count / (trianglesBudget * 3));
                    for (int i = 0; i < positions.Count && validVertices.Count < trianglesBudget * 3; i += vertStride)
                    {
                        var wp = transform.Transform(positions[i]);
                        var sp = ProjectSinglePoint(viewport, wp, camPos, lookDir, depthThreshold, hasProjCam);
                        if (sp.HasValue)
                            validVertices.Add(sp.Value);
                    }
                    trianglesBudget = 0;
                }
            }

            if (validVertices.Count > 0)
                projectedVertices = validVertices.ToArray();
            if (validEdges.Count > 0)
                projectedEdges = validEdges.ToArray();
        }

        private static Point? ProjectSinglePoint(
            HelixViewport3D viewport,
            Point3D worldPoint,
            Point3D camPos,
            Vector3D lookDir,
            double depthThreshold,
            bool hasProjCam)
        {
            if (hasProjCam)
            {
                var toPoint = worldPoint - camPos;
                var ptDepth = Vector3D.DotProduct(toPoint, lookDir);
                if (ptDepth <= depthThreshold)
                    return null;
            }

            Point screenPoint;
            try
            {
                if (TestProjectHook != null)
                {
                    screenPoint = TestProjectHook(viewport, worldPoint);
                }
                else
                {
                    var wpfPoint = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, worldPoint);
                    screenPoint = SafeTranslatePoint(viewport.Viewport, wpfPoint, viewport);
                }
            }
            catch
            {
                return null;
            }

            if (double.IsNaN(screenPoint.X) || double.IsInfinity(screenPoint.X) ||
                double.IsNaN(screenPoint.Y) || double.IsInfinity(screenPoint.Y))
            {
                return null;
            }

            return screenPoint;
        }

        private static bool SegmentIntersectsRect(Point a, Point b, Rect rect)
        {
            // Check if either endpoint is inside the rect
            if (rect.Contains(a) || rect.Contains(b))
                return true;

            // Check intersection with all 4 edges of the rect
            var tl = new Point(rect.Left, rect.Top);
            var tr = new Point(rect.Right, rect.Top);
            var br = new Point(rect.Right, rect.Bottom);
            var bl = new Point(rect.Left, rect.Bottom);

            return SegmentsIntersect(a, b, tl, tr) ||
                   SegmentsIntersect(a, b, tr, br) ||
                   SegmentsIntersect(a, b, br, bl) ||
                   SegmentsIntersect(a, b, bl, tl);
        }

        private static bool SegmentsIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            double d1 = Cross(p3, p4, p1);
            double d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3);
            double d4 = Cross(p1, p2, p4);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            if (Math.Abs(d1) < 1e-10 && OnSegment(p3, p4, p1)) return true;
            if (Math.Abs(d2) < 1e-10 && OnSegment(p3, p4, p2)) return true;
            if (Math.Abs(d3) < 1e-10 && OnSegment(p1, p2, p3)) return true;
            if (Math.Abs(d4) < 1e-10 && OnSegment(p1, p2, p4)) return true;

            return false;
        }

        private static double Cross(Point o, Point a, Point b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        private static bool OnSegment(Point p, Point q, Point r)
        {
            return r.X >= Math.Min(p.X, q.X) && r.X <= Math.Max(p.X, q.X) &&
                   r.Y >= Math.Min(p.Y, q.Y) && r.Y <= Math.Max(p.Y, q.Y);
        }

        private static Point3D[] GetBoundsSamplePoints(Rect3D bounds)
        {
            var x0 = bounds.X;
            var y0 = bounds.Y;
            var z0 = bounds.Z;
            var x1 = bounds.X + bounds.SizeX;
            var y1 = bounds.Y + bounds.SizeY;
            var z1 = bounds.Z + bounds.SizeZ;
            var xm = bounds.X + bounds.SizeX / 2d;
            var ym = bounds.Y + bounds.SizeY / 2d;
            var zm = bounds.Z + bounds.SizeZ / 2d;

            return
            [
                new Point3D(x0, y0, z0),
                new Point3D(x1, y0, z0),
                new Point3D(x0, y1, z0),
                new Point3D(x1, y1, z0),
                new Point3D(x0, y0, z1),
                new Point3D(x1, y0, z1),
                new Point3D(x0, y1, z1),
                new Point3D(x1, y1, z1),
                new Point3D(xm, ym, zm),
                new Point3D(xm, y0, zm),
                new Point3D(xm, y1, zm),
                new Point3D(x0, ym, zm),
                new Point3D(x1, ym, zm),
                new Point3D(xm, ym, z0),
                new Point3D(xm, ym, z1)
            ];
        }

        private static double GetCameraDistance(Camera? camera, Point3D point)
        {
            if (camera is ProjectionCamera projectionCamera)
                return (point - projectionCamera.Position).LengthSquared;

            return 0d;
        }

        private static Transform3D Combine(Transform3D parent, Transform3D child)
        {
            if (child == Transform3D.Identity || child == null)
                return parent;
            if (parent == Transform3D.Identity)
                return child;

            var group = new Transform3DGroup();
            group.Children.Add(parent);
            group.Children.Add(child);
            return group;
        }

        private static bool IsWindowBoxHit(Rect rect, ElementProjection projection)
        {
            return BoundaryBoxFullyContained(rect, projection.ScreenBounds) &&
                   projection.ScreenSamples.All(rect.Contains);
        }

        private static bool BoundaryBoxFullyContained(Rect selectionRect, Rect elementBounds)
        {
            return elementBounds.Left >= selectionRect.Left &&
                   elementBounds.Top >= selectionRect.Top &&
                   elementBounds.Right <= selectionRect.Right &&
                   elementBounds.Bottom <= selectionRect.Bottom;
        }

        private static double GetDistanceToProjectionSquared(Point point, ElementProjection projection)
        {
            var best = double.PositiveInfinity;

            foreach (var sample in projection.ScreenSamples)
                best = Math.Min(best, DistanceSquared(point, sample));

            for (var i = 0; i < ProjectionEdgeIndices.Length; i += 2)
            {
                var aIndex = ProjectionEdgeIndices[i];
                var bIndex = ProjectionEdgeIndices[i + 1];
                if (aIndex >= projection.ScreenSamples.Length || bIndex >= projection.ScreenSamples.Length)
                    continue;

                best = Math.Min(
                    best,
                    DistanceToSegmentSquared(
                        point,
                        projection.ScreenSamples[aIndex],
                        projection.ScreenSamples[bIndex]));
            }

            return best;
        }

        private static IEnumerable<Point> EnumerateRectSamplePoints(Rect rect, double viewportWidth, double viewportHeight)
        {
            var viewportRect = new Rect(0, 0, Math.Max(0, viewportWidth), Math.Max(0, viewportHeight));
            rect.Intersect(viewportRect);

            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                yield break;

            var area = Math.Max(1d, rect.Width * rect.Height);
            var step = Math.Max(CrossingSampleStepPx, Math.Sqrt(area / MaxCrossingSamplePoints));
            var xs = BuildSampleCoordinates(rect.Left, rect.Right, step);
            var ys = BuildSampleCoordinates(rect.Top, rect.Bottom, step);

            foreach (var y in ys)
            {
                foreach (var x in xs)
                    yield return new Point(x, y);
            }
        }

        private static List<double> BuildSampleCoordinates(double start, double end, double step)
        {
            var values = new List<double> { start };

            for (var value = start + step; value < end; value += step)
                values.Add(value);

            if (end > start)
                values.Add(end);

            return values;
        }

        private static double DistanceSquared(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static double DistanceToSegmentSquared(Point p, Point a, Point b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            if (Math.Abs(dx) < 0.000001d && Math.Abs(dy) < 0.000001d)
                return DistanceSquared(p, a);

            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0d, Math.Min(1d, t));

            var nearest = new Point(a.X + t * dx, a.Y + t * dy);
            return DistanceSquared(p, nearest);
        }

        private static void ReleaseMouseCapture(HelixViewport3D viewport)
        {
            if (Mouse.Captured == viewport)
                Mouse.Capture(null);
        }

        private void UpdateRubberBand()
        {
            if (_overlayCanvas == null)
                return;

            var isCrossing = _dragStart.X > _dragCurrent.X;

            if (_rubberBand == null)
            {
                _rubberBand = new Rectangle
                {
                    StrokeThickness = 2.75,
                    IsHitTestVisible = false
                };
                _overlayCanvas.Children.Add(_rubberBand);
            }

            if (isCrossing != _lastWasCrossing || _rubberBand.Stroke == null)
            {
                _rubberBand.Stroke = isCrossing
                    ? new SolidColorBrush(Color.FromRgb(82, 194, 102))
                    : new SolidColorBrush(Color.FromRgb(66, 133, 244));
                _rubberBand.Fill = isCrossing
                    ? new SolidColorBrush(Color.FromArgb(48, 82, 194, 102))
                    : new SolidColorBrush(Color.FromArgb(48, 66, 133, 244));
                _rubberBand.StrokeDashArray = isCrossing
                    ? new DoubleCollection(new double[] { 4.0, 3.0 })
                    : null;
                _lastWasCrossing = isCrossing;
            }

            var rect = GetDragRect();
            Canvas.SetLeft(_rubberBand, rect.X);
            Canvas.SetTop(_rubberBand, rect.Y);
            _rubberBand.Width = Math.Max(0, rect.Width);
            _rubberBand.Height = Math.Max(0, rect.Height);
        }

        private void ClearRubberBand()
        {
            if (_rubberBand != null && _overlayCanvas != null)
                _overlayCanvas.Children.Remove(_rubberBand);

            _rubberBand = null;
        }

        private bool HasMovedEnoughToDrag(Point current)
        {
            var dx = current.X - _dragStart.X;
            var dy = current.Y - _dragStart.Y;
            return (dx * dx + dy * dy) >= DragThresholdPx * DragThresholdPx;
        }

        private Rect GetDragRect() =>
            new(
                Math.Min(_dragStart.X, _dragCurrent.X),
                Math.Min(_dragStart.Y, _dragCurrent.Y),
                Math.Abs(_dragCurrent.X - _dragStart.X),
                Math.Abs(_dragCurrent.Y - _dragStart.Y));

        public void Dispose()
        {
            ClearRubberBand();
        }

        private static readonly int[] ProjectionEdgeIndices =
        [
            0, 1,
            0, 2,
            1, 3,
            2, 3,
            4, 5,
            4, 6,
            5, 7,
            6, 7,
            0, 4,
            1, 5,
            2, 6,
            3, 7
        ];

        public readonly record struct ElementProjection(
            PrecastElement Element,
            Rect ScreenBounds,
            Point[] ScreenSamples,
            double Depth,
            Point[]? MeshProxySamples = null,
            Tuple<Point, Point>[]? MeshProxyEdges = null);
    }
}
