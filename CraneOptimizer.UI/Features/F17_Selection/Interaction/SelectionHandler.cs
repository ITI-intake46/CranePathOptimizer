using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using HelixToolkit.Wpf;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private const double DragThresholdPx = 5.0;
        private const double TabNearCursorRadiusPx = 8.0;
        private const double CrossingSampleStepPx = 6.0;
        private const int MaxCrossingSamplePoints = 7000;

        public enum BoxSelectionMode
        {
            MeshProxy,
            ViewportHitSampling
        }

        public static BoxSelectionMode SelectionPrecision { get; set; } = BoxSelectionMode.MeshProxy;
        public static bool EnableSelectionDiagnostics { get; set; }
        public static Func<HelixViewport3D, Point3D, Point>? TestProjectHook { get; set; }

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

            return false;
        }

        public void OnPreviewMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            ArmBoxSelectIfEmpty(viewport, e);
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            ArmBoxSelectIfEmpty(viewport, e);
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (!IsEnabled)
                return;

            _hoverCache.OnMouseMove(e.GetPosition(viewport));
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.ChangedButton != MouseButton.Left)
                return;

            if (_isDragging)
            {
                try
                {
                    _dragCurrent = e.GetPosition(viewport);
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

            if (_clickArmed)
            {
                try
                {
                    if (!HasMovedEnoughToDrag(e.GetPosition(viewport)))
                    {
                        PerformClickSelect(viewport, e.GetPosition(viewport));
                        e.Handled = true;
                    }
                }
                finally
                {
                    _clickArmed = false;
                }
                return;
            }

            if (_dragArmed)
            {
                try
                {
                    PerformClickSelect(viewport, e.GetPosition(viewport));
                    e.Handled = true;
                }
                finally
                {
                    _dragArmed = false;
                    _clickArmed = false;
                    ReleaseMouseCapture(viewport);
                }
            }
        }

        public void OnPreviewMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (!IsEnabled || e.LeftButton != MouseButtonState.Pressed || !_dragArmed)
                return;

            var pos = e.GetPosition(viewport);
            e.Handled = true;

            if (!_isDragging)
            {
                if (!HasMovedEnoughToDrag(pos))
                    return;

                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                    _selection.Clear();

                _isDragging = true;
            }

            _dragCurrent = pos;
            UpdateRubberBand();
        }

        public void OnPreviewMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.ChangedButton != MouseButton.Left || !_dragArmed)
                return;

            try
            {
                if (_isDragging)
                {
                    _dragCurrent = e.GetPosition(viewport);
                    FinishBoxSelect(viewport);
                }
                else
                {
                    PerformClickSelect(viewport, e.GetPosition(viewport));
                }

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
                _hoverCache.SetCandidates(pos, rayHits);
                return;
            }

            var hit = _hitTest.GetHitObject(viewport, pos);
            if (hit != null)
                return;

            _dragArmed = true;
            _isDragging = false;
            _dragStart = pos;
            _dragCurrent = pos;
            Mouse.Capture(viewport, CaptureMode.SubTree);
            e.Handled = true;
        }

        private void PerformClickSelect(HelixViewport3D viewport, Point pos)
        {
            var hits = GetRayHitElements(viewport, pos).ToList();
            if (hits.Count > 0)
            {
                var element = hits[0];
                _hoverCache.SetCandidates(pos, hits);

                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    _selection.ToggleElement(element);
                else
                    _selection.SetElements(new[] { element });

                return;
            }

            if (_hitTest.GetHitObject(viewport, pos) == null)
            {
                _selection.Clear();
                _hoverCache.Invalidate();
            }
        }

        private void SelectNextTabCandidate(HelixViewport3D viewport, KeyEventArgs e)
        {
            if (!EnsureHoverCandidates(viewport))
                return;

            var current = _selection.GetSelectedTargets()
                .Select(t => t.UnderlyingObject)
                .OfType<PrecastElement>()
                .FirstOrDefault();

            var currentInCandidates = current != null &&
                                      _hoverCache.Candidates.Any(candidate => ReferenceEquals(candidate, current));

            PrecastElement? next;
            if (currentInCandidates)
            {
                _hoverCache.AlignTo(current!);
                next = _hoverCache.CycleNext();
            }
            else
            {
                next = _hoverCache.CurrentCandidate;
            }

            if (next == null)
                return;

            _selection.SetElements(new[] { next });
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
            var result = new List<PrecastElement>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);

            foreach (var element in GetRayHitElements(viewport, pos))
            {
                if (seen.Add(element))
                    result.Add(element);
            }

            if (result.Count > 1)
                return result;

            foreach (var projection in GetNearCursorProjections(viewport, pos, TabNearCursorRadiusPx))
            {
                if (seen.Add(projection.Element))
                    result.Add(projection.Element);
            }

            return result;
        }

        private IEnumerable<PrecastElement> GetRayHitElements(HelixViewport3D viewport, Point pos)
        {
            var result = new List<PrecastElement>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);
            var viewportPoint = TranslatePoint(viewport, pos, viewport.Viewport);

            try
            {
                foreach (var hit in viewport.Viewport.FindHits(viewportPoint))
                {
                    var element = TryGetElementTag(hit.Visual);
                    if (element != null && _filterService.IsElementIncluded(element) && seen.Add(element))
                        result.Add(element);
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
            var hits = GetElementsInRect(viewport, rect, isCrossing);

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (hits.Count > 0)
                    AddElements(hits);
                return;
            }

            if (hits.Count > 0)
                _selection.SetElements(hits);
            else
                _selection.Clear();
        }

        private void AddElements(IEnumerable<PrecastElement> elements)
        {
            var selected = _selection.GetSelectedTargets()
                .Select(target => target.UnderlyingObject)
                .OfType<PrecastElement>()
                .ToList();
            var seen = new HashSet<string>(
                selected.Select(GetElementKey),
                StringComparer.Ordinal);

            foreach (var element in elements)
            {
                if (seen.Add(GetElementKey(element)))
                    selected.Add(element);
            }

            _selection.SetElements(selected);
        }

        private List<PrecastElement> GetElementsInRect(HelixViewport3D viewport, Rect screenRect, bool crossing)
        {
            var result = new List<PrecastElement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projections = GetElementProjections(viewport);

            foreach (var projection in projections)
            {
                var isHit = crossing
                    ? IsCrossingBoxHit(screenRect, projection)
                    : IsWindowBoxHit(screenRect, projection);

                if (isHit && seen.Add(GetElementKey(projection.Element)))
                    result.Add(projection.Element);
            }

            if (crossing &&
                result.Count == 0 &&
                (SelectionPrecision == BoxSelectionMode.ViewportHitSampling || projections.Count == 0))
            {
                return GetElementsHitBySampledRect(viewport, screenRect);
            }

            return result;
        }

        private List<PrecastElement> GetElementsHitBySampledRect(HelixViewport3D viewport, Rect screenRect)
        {
            var result = new List<PrecastElement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            var viewportWidth = viewport.ActualWidth > 0 ? viewport.ActualWidth : viewport.Width;
            var viewportHeight = viewport.ActualHeight > 0 ? viewport.ActualHeight : viewport.Height;

            foreach (var point in EnumerateRectSamplePoints(screenRect, viewportWidth, viewportHeight))
            {
                var element = GetFrontRayHitElement(viewport, point);
                if (element != null && seen.Add(GetElementKey(element)))
                    result.Add(element);
            }

            return result;
        }

        private PrecastElement? GetFrontRayHitElement(HelixViewport3D viewport, Point pos)
        {
            var viewportPoint = TranslatePoint(viewport, pos, viewport.Viewport);

            try
            {
                foreach (var hit in viewport.Viewport.FindHits(viewportPoint))
                {
                    var element = TryGetElementTag(hit.Visual);
                    if (element != null && _filterService.IsElementIncluded(element))
                        return element;
                }
            }
            catch
            {
                // Fall through to the shared hit-test service.
            }

            var hitObj = _hitTest.GetHitObject(viewport, pos);
            if (TryGetElementFromHitObject(hitObj, out var hitElement) &&
                _filterService.IsElementIncluded(hitElement))
            {
                return hitElement;
            }

            return null;
        }

        private static bool TryGetElementFromHitObject(object? hitObj, out PrecastElement element)
        {
            element = null!;

            if (hitObj is PrecastElement direct)
            {
                element = direct;
                return true;
            }

            return false;
        }

        private static bool IsSameElement(PrecastElement left, PrecastElement right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return !string.IsNullOrWhiteSpace(left.ElementID) &&
                   string.Equals(left.ElementID, right.ElementID, StringComparison.Ordinal);
        }

        private static string GetElementKey(PrecastElement element)
        {
            if (!string.IsNullOrWhiteSpace(element.ElementID))
                return $"id:{element.ElementID.Trim()}";

            return $"ref:{RuntimeHelpers.GetHashCode(element)}";
        }

        private PrecastElement? ResolveVisibleElement(PrecastElement element)
        {
            var visibleElements = _filterService.VisibleElements;
            if (visibleElements.Count == 0)
                return element;

            return visibleElements.FirstOrDefault(visible => IsSameElement(visible, element));
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
            var projections = new Dictionary<string, ElementProjection>(StringComparer.Ordinal);

            foreach (var child in viewport.Children.OfType<ModelVisual3D>())
                CollectElementProjections(viewport, child, Transform3D.Identity, projections);

            foreach (var projection in GetSpatialElementProjections(viewport))
            {
                var key = GetElementKey(projection.Element);
                if (projections.TryGetValue(key, out var existing) && existing.HasMeshGeometry)
                    continue;

                AddOrMergeProjection(projections, projection);
            }

            if (projections.Count > 0)
                return projections.Values.ToList();

            foreach (var element in _filterService.VisibleElements)
            {
                var key = GetElementKey(element);
                if (TryProjectElementBox(viewport, element, out var screenBounds, out var samples, out var depth) &&
                    samples.Length > 0)
                {
                    projections[key] = new ElementProjection(element, screenBounds, samples, depth, [], false);
                }
            }

            return projections.Values.ToList();
        }

        private IEnumerable<ElementProjection> GetSpatialElementProjections(
            HelixViewport3D viewport)
        {
            foreach (var candidate in ElementVisualBuilder.GetSpatialHitTestElements())
            {
                var element = ResolveVisibleElement(candidate.Element);
                if (!candidate.IsVisible ||
                    element == null ||
                    !_filterService.IsElementIncluded(element))
                {
                    continue;
                }

                if (TryProjectSpatialElement(viewport, candidate, element, out var projection))
                    yield return projection;
            }
        }

        private static bool TryProjectSpatialElement(
            HelixViewport3D viewport,
            ElementVisualBuilder.SpatialHitTestElement candidate,
            PrecastElement element,
            out ElementProjection projection)
        {
            projection = default;

            if (candidate.Meshes.Count > 0)
                return TryProjectSpatialMeshes(viewport, candidate, element, out projection);

            var bounds = new Rect3D(
                candidate.Min.X,
                candidate.Min.Y,
                candidate.Min.Z,
                candidate.Max.X - candidate.Min.X,
                candidate.Max.Y - candidate.Min.Y,
                candidate.Max.Z - candidate.Min.Z);

            return TryProjectBounds(viewport, bounds, Transform3D.Identity, out var screenBounds, out var samples, out var depth) &&
                   SetProjection(element, screenBounds, samples, depth, [], false, out projection);
        }

        private static bool TryProjectSpatialMeshes(
            HelixViewport3D viewport,
            ElementVisualBuilder.SpatialHitTestElement candidate,
            PrecastElement element,
            out ElementProjection projection)
        {
            projection = default;

            Rect? rect = null;
            var samples = new List<Point>();
            var triangles = new List<ScreenTriangle>();
            var depthSum = 0d;
            var projectedCount = 0;

            foreach (var (mesh, transform) in candidate.Meshes)
            {
                var positions = mesh.Positions;
                var indices = mesh.TriangleIndices;

                if (positions == null || indices == null || positions.Count == 0 || indices.Count < 3)
                    continue;

                for (var i = 0; i + 2 < indices.Count; i += 3)
                {
                    var p0 = transform.Transform(positions[indices[i]]);
                    var p1 = transform.Transform(positions[indices[i + 1]]);
                    var p2 = transform.Transform(positions[indices[i + 2]]);

                    if (!TryProjectWorldPoint(viewport, p0, out var s0) ||
                        !TryProjectWorldPoint(viewport, p1, out var s1) ||
                        !TryProjectWorldPoint(viewport, p2, out var s2))
                    {
                        return false;
                    }

                    AddProjectedPoint(s0, p0, viewport.Camera, ref rect, samples, ref depthSum, ref projectedCount);
                    AddProjectedPoint(s1, p1, viewport.Camera, ref rect, samples, ref depthSum, ref projectedCount);
                    AddProjectedPoint(s2, p2, viewport.Camera, ref rect, samples, ref depthSum, ref projectedCount);
                    triangles.Add(new ScreenTriangle(s0, s1, s2));
                }
            }

            if (!rect.HasValue || projectedCount == 0)
                return false;

            return SetProjection(
                element,
                rect.Value,
                samples.ToArray(),
                depthSum / projectedCount,
                triangles.ToArray(),
                true,
                out projection);
        }

        private static bool SetProjection(
            PrecastElement element,
            Rect screenBounds,
            Point[] samples,
            double depth,
            ScreenTriangle[] triangles,
            bool hasMeshGeometry,
            out ElementProjection projection)
        {
            projection = default;

            if (screenBounds.IsEmpty || samples.Length == 0)
                return false;

            projection = new ElementProjection(element, screenBounds, samples, depth, triangles, hasMeshGeometry);
            return true;
        }

        private void CollectElementProjections(
            HelixViewport3D viewport,
            ModelVisual3D visual,
            Transform3D parentTransform,
            Dictionary<string, ElementProjection> projections)
        {
            var visualTransform = Combine(parentTransform, visual.Transform);
            var taggedElement = visual.GetValue(FrameworkElement.TagProperty) as PrecastElement;
            var element = taggedElement == null ? null : ResolveVisibleElement(taggedElement);

            if (element != null &&
                _filterService.IsElementIncluded(element) &&
                TryProjectVisualGeometry(viewport, visual, visualTransform, element, out var projection))
            {
                AddOrMergeProjection(projections, projection);
            }

            foreach (var child in visual.Children.OfType<ModelVisual3D>())
                CollectElementProjections(viewport, child, visualTransform, projections);
        }

        private static void AddOrMergeProjection(
            Dictionary<string, ElementProjection> projections,
            ElementProjection projection)
        {
            var key = GetElementKey(projection.Element);
            if (!projections.TryGetValue(key, out var existing))
            {
                projections[key] = projection;
                return;
            }

            if (existing.HasMeshGeometry && !projection.HasMeshGeometry)
                return;

            if (!existing.HasMeshGeometry && projection.HasMeshGeometry)
            {
                projections[key] = projection;
                return;
            }

            var samples = existing.ScreenSamples.Concat(projection.ScreenSamples).ToArray();
            var triangles = existing.ScreenTriangles.Concat(projection.ScreenTriangles).ToArray();
            var weightedDepth =
                (existing.Depth * existing.ScreenSamples.Length +
                 projection.Depth * projection.ScreenSamples.Length) /
                Math.Max(1, samples.Length);

            projections[key] = new ElementProjection(
                existing.Element,
                Rect.Union(existing.ScreenBounds, projection.ScreenBounds),
                samples,
                weightedDepth,
                triangles,
                existing.HasMeshGeometry || projection.HasMeshGeometry);
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

        private static bool TryProjectVisualGeometry(
            HelixViewport3D viewport,
            ModelVisual3D visual,
            Transform3D transform,
            PrecastElement element,
            out ElementProjection projection)
        {
            projection = default;

            if (visual.Content == null)
                return false;

            Rect? screenBounds = null;
            var samples = new List<Point>();
            var triangles = new List<ScreenTriangle>();
            var depthSum = 0d;
            var projectedCount = 0;
            var hasMeshGeometry = false;
            var isComplete = true;

            CollectProjectedModelGeometry(
                viewport,
                visual.Content,
                transform,
                ref screenBounds,
                samples,
                triangles,
                ref depthSum,
                ref projectedCount,
                ref hasMeshGeometry,
                ref isComplete);

            if (!isComplete || !screenBounds.HasValue || projectedCount == 0)
                return false;

            return SetProjection(
                element,
                screenBounds.Value,
                samples.ToArray(),
                depthSum / projectedCount,
                triangles.ToArray(),
                hasMeshGeometry,
                out projection);
        }

        private bool TryProjectElementBox(
            HelixViewport3D viewport,
            PrecastElement element,
            out Rect screenBounds,
            out Point[] samples,
            out double depth)
        {
            var cog = element.ElementCOG;
            var origin = _originService.OriginPoint;
            var boundsCenter = new Point3D(
                cog.X - (origin?.X ?? 0d),
                cog.Y - (origin?.Y ?? 0d),
                cog.Z - (origin?.Z ?? 0d));

            var bounds = new Rect3D(
                boundsCenter.X - Math.Max(element.BBoxLength ?? element.ElementLength ?? 1d, 0.01d) / 2d,
                boundsCenter.Y - Math.Max(element.BBoxWidth ?? element.ElementWidth ?? 1d, 0.01d) / 2d,
                boundsCenter.Z - Math.Max(element.BBoxHeight ?? element.ElementHeight ?? 1d, 0.01d) / 2d,
                Math.Max(element.BBoxLength ?? element.ElementLength ?? 1d, 0.01d),
                Math.Max(element.BBoxWidth ?? element.ElementWidth ?? 1d, 0.01d),
                Math.Max(element.BBoxHeight ?? element.ElementHeight ?? 1d, 0.01d));

            return TryProjectBounds(viewport, bounds, Transform3D.Identity, out screenBounds, out samples, out depth);
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

            foreach (var worldPoint in worldPoints)
            {
                if (!TryProjectWorldPoint(viewport, worldPoint, out var screenPoint))
                    return false;

                AddProjectedPoint(screenPoint, worldPoint, viewport.Camera, ref rect, projectedPoints, ref depthSum, ref projectedCount);
            }

            if (!rect.HasValue || projectedCount == 0)
                return false;

            screenBounds = InflateTinyRect(rect.Value);
            samples = projectedPoints.ToArray();
            depth = depthSum / projectedCount;
            return true;
        }

        private static bool TryProjectWorldPoint(HelixViewport3D viewport, Point3D worldPoint, out Point screenPoint)
        {
            screenPoint = default;

            if (viewport.Camera is ProjectionCamera projectionCamera)
            {
                var lookDirection = projectionCamera.LookDirection;
                if (lookDirection.LengthSquared > 0.00000001d)
                {
                    lookDirection.Normalize();
                    var cameraDepth = Vector3D.DotProduct(worldPoint - projectionCamera.Position, lookDirection);
                    var nearPlane = Math.Max(0d, projectionCamera.NearPlaneDistance);
                    if (cameraDepth <= nearPlane + 0.000001d)
                        return false;
                }
            }

            if (TestProjectHook != null)
            {
                try
                {
                    screenPoint = TestProjectHook(viewport, worldPoint);
                    return IsFinite(screenPoint);
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                var viewportPoint = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, worldPoint);
                screenPoint = TranslatePoint(viewport.Viewport, viewportPoint, viewport);
                return IsFinite(screenPoint);
            }
            catch
            {
                return false;
            }
        }

        private static Point TranslatePoint(UIElement from, Point point, UIElement to)
        {
            try
            {
                return from.TranslatePoint(point, to);
            }
            catch
            {
                return point;
            }
        }

        private static bool IsFinite(Point point)
        {
            return !double.IsNaN(point.X) &&
                   !double.IsNaN(point.Y) &&
                   !double.IsInfinity(point.X) &&
                   !double.IsInfinity(point.Y);
        }

        private static void AddProjectedPoint(
            Point screenPoint,
            Point3D worldPoint,
            Camera? camera,
            ref Rect? rect,
            List<Point> projectedPoints,
            ref double depthSum,
            ref int projectedCount)
        {
            var pointRect = new Rect(screenPoint, new Size(0, 0));
            rect = rect.HasValue ? Rect.Union(rect.Value, pointRect) : pointRect;
            projectedPoints.Add(screenPoint);
            depthSum += GetCameraDistance(camera, worldPoint);
            projectedCount++;
        }

        private static void CollectProjectedModelGeometry(
            HelixViewport3D viewport,
            Model3D model,
            Transform3D parentTransform,
            ref Rect? screenBounds,
            List<Point> samples,
            List<ScreenTriangle> triangles,
            ref double depthSum,
            ref int projectedCount,
            ref bool hasMeshGeometry,
            ref bool isComplete)
        {
            var modelTransform = Combine(parentTransform, model.Transform);

            if (model is GeometryModel3D geometryModel)
            {
                if (geometryModel.Geometry is MeshGeometry3D mesh && mesh.Positions.Count > 0)
                {
                    hasMeshGeometry = true;
                    var matrix = modelTransform.Value;
                    var projectedPositions = new Point[mesh.Positions.Count];

                    for (var i = 0; i < mesh.Positions.Count; i++)
                    {
                        var worldPoint = matrix.Transform(mesh.Positions[i]);
                        if (!TryProjectWorldPoint(viewport, worldPoint, out var screenPoint))
                        {
                            isComplete = false;
                            return;
                        }

                        projectedPositions[i] = screenPoint;
                        AddProjectedPoint(
                            screenPoint,
                            worldPoint,
                            viewport.Camera,
                            ref screenBounds,
                            samples,
                            ref depthSum,
                            ref projectedCount);
                    }

                    var indices = mesh.TriangleIndices;
                    for (var i = 0; i + 2 < indices.Count; i += 3)
                    {
                        var i0 = indices[i];
                        var i1 = indices[i + 1];
                        var i2 = indices[i + 2];
                        if (i0 < 0 || i1 < 0 || i2 < 0 ||
                            i0 >= projectedPositions.Length ||
                            i1 >= projectedPositions.Length ||
                            i2 >= projectedPositions.Length)
                        {
                            continue;
                        }

                        triangles.Add(new ScreenTriangle(
                            projectedPositions[i0],
                            projectedPositions[i1],
                            projectedPositions[i2]));
                    }

                    return;
                }

                var bounds = geometryModel.Geometry?.Bounds ?? Rect3D.Empty;
                if (bounds.IsEmpty)
                    return;

                var boundsMatrix = modelTransform.Value;
                foreach (var localPoint in GetBoundsSamplePoints(bounds))
                {
                    var worldPoint = boundsMatrix.Transform(localPoint);
                    if (!TryProjectWorldPoint(viewport, worldPoint, out var screenPoint))
                    {
                        isComplete = false;
                        return;
                    }

                    AddProjectedPoint(
                        screenPoint,
                        worldPoint,
                        viewport.Camera,
                        ref screenBounds,
                        samples,
                        ref depthSum,
                        ref projectedCount);
                }

                return;
            }

            if (model is not Model3DGroup group)
                return;

            foreach (var child in group.Children)
            {
                CollectProjectedModelGeometry(
                    viewport,
                    child,
                    modelTransform,
                    ref screenBounds,
                    samples,
                    triangles,
                    ref depthSum,
                    ref projectedCount,
                    ref hasMeshGeometry,
                    ref isComplete);

                if (!isComplete)
                    return;
            }
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

        private static bool IsCrossingBoxHit(Rect rect, ElementProjection projection)
        {
            if (projection.ScreenTriangles.Length > 0)
                return projection.ScreenTriangles.Any(triangle => TriangleTouchesRect(triangle, rect));

            return RectsTouchOrOverlap(rect, projection.ScreenBounds);
        }

        private static bool BoundaryBoxFullyContained(Rect selectionRect, Rect elementBounds)
        {
            return elementBounds.Left >= selectionRect.Left &&
                   elementBounds.Top >= selectionRect.Top &&
                   elementBounds.Right <= selectionRect.Right &&
                   elementBounds.Bottom <= selectionRect.Bottom;
        }

        private static bool RectsTouchOrOverlap(Rect selectionRect, Rect elementBounds)
        {
            if (selectionRect.IsEmpty || elementBounds.IsEmpty)
                return false;

            return elementBounds.Right >= selectionRect.Left &&
                   elementBounds.Left <= selectionRect.Right &&
                   elementBounds.Bottom >= selectionRect.Top &&
                   elementBounds.Top <= selectionRect.Bottom;
        }

        private static bool TriangleTouchesRect(ScreenTriangle triangle, Rect rect)
        {
            if (!RectsTouchOrOverlap(rect, GetTriangleBounds(triangle)))
                return false;

            if (rect.Contains(triangle.A) ||
                rect.Contains(triangle.B) ||
                rect.Contains(triangle.C))
            {
                return true;
            }

            var rectCorners = GetRectCorners(rect);
            if (rectCorners.Any(corner => PointInTriangleInclusive(corner, triangle)))
                return true;

            var triangleEdges = new[]
            {
                (triangle.A, triangle.B),
                (triangle.B, triangle.C),
                (triangle.C, triangle.A)
            };
            var rectEdges = new[]
            {
                (rectCorners[0], rectCorners[1]),
                (rectCorners[1], rectCorners[2]),
                (rectCorners[2], rectCorners[3]),
                (rectCorners[3], rectCorners[0])
            };

            return triangleEdges.Any(triangleEdge =>
                rectEdges.Any(rectEdge => SegmentsIntersectInclusive(
                    triangleEdge.Item1,
                    triangleEdge.Item2,
                    rectEdge.Item1,
                    rectEdge.Item2)));
        }

        private static Rect GetTriangleBounds(ScreenTriangle triangle)
        {
            var minX = Math.Min(triangle.A.X, Math.Min(triangle.B.X, triangle.C.X));
            var minY = Math.Min(triangle.A.Y, Math.Min(triangle.B.Y, triangle.C.Y));
            var maxX = Math.Max(triangle.A.X, Math.Max(triangle.B.X, triangle.C.X));
            var maxY = Math.Max(triangle.A.Y, Math.Max(triangle.B.Y, triangle.C.Y));

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        private static Point[] GetRectCorners(Rect rect) =>
        [
            new Point(rect.Left, rect.Top),
            new Point(rect.Right, rect.Top),
            new Point(rect.Right, rect.Bottom),
            new Point(rect.Left, rect.Bottom)
        ];

        private static bool PointInTriangleInclusive(Point point, ScreenTriangle triangle)
        {
            var d1 = Cross(point, triangle.A, triangle.B);
            var d2 = Cross(point, triangle.B, triangle.C);
            var d3 = Cross(point, triangle.C, triangle.A);

            const double epsilon = 0.000001d;
            var hasNegative = d1 < -epsilon || d2 < -epsilon || d3 < -epsilon;
            var hasPositive = d1 > epsilon || d2 > epsilon || d3 > epsilon;

            return !(hasNegative && hasPositive);
        }

        private static bool SegmentsIntersectInclusive(Point a, Point b, Point c, Point d)
        {
            const double epsilon = 0.000001d;
            var d1 = Cross(a, b, c);
            var d2 = Cross(a, b, d);
            var d3 = Cross(c, d, a);
            var d4 = Cross(c, d, b);

            if (((d1 > epsilon && d2 < -epsilon) || (d1 < -epsilon && d2 > epsilon)) &&
                ((d3 > epsilon && d4 < -epsilon) || (d3 < -epsilon && d4 > epsilon)))
            {
                return true;
            }

            return Math.Abs(d1) <= epsilon && PointOnSegment(c, a, b) ||
                   Math.Abs(d2) <= epsilon && PointOnSegment(d, a, b) ||
                   Math.Abs(d3) <= epsilon && PointOnSegment(a, c, d) ||
                   Math.Abs(d4) <= epsilon && PointOnSegment(b, c, d);
        }

        private static bool PointOnSegment(Point point, Point a, Point b)
        {
            const double epsilon = 0.000001d;
            return point.X >= Math.Min(a.X, b.X) - epsilon &&
                   point.X <= Math.Max(a.X, b.X) + epsilon &&
                   point.Y >= Math.Min(a.Y, b.Y) - epsilon &&
                   point.Y <= Math.Max(a.Y, b.Y) + epsilon;
        }

        private static double Cross(Point a, Point b, Point c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

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
            var viewportRect = new Rect(0, 0, Math.Max(1, viewportWidth), Math.Max(1, viewportHeight));
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
            ScreenTriangle[] ScreenTriangles,
            bool HasMeshGeometry);

        public readonly record struct ScreenTriangle(Point A, Point B, Point C);
    }
}
