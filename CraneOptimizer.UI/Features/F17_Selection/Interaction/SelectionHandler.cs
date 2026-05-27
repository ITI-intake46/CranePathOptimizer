using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F17_Selection.Services;
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

        public bool IsClickOnViewCubeOrOverlay(HelixViewport3D viewport, RoutedEventArgs e)
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

            if (e is MouseEventArgs mouseEventArgs)
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

            if (IsClickOnViewCubeOrOverlay(viewport, e))
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

            _clickArmed = false;
            _dragArmed = false;
        }

        public void OnPreviewMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (!IsEnabled || e.LeftButton != MouseButtonState.Pressed || !_dragArmed)
                return;

            if (IsClickOnViewCubeOrOverlay(viewport, e))
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
                    _dragCurrent = e.GetPosition(viewport);
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
            var hits = GetElementsInRect(viewport, rect, isCrossing);

            // Temporary debug logging
            System.Diagnostics.Debug.WriteLine($"[Box Selection Audit]");
            System.Diagnostics.Debug.WriteLine($"  DragStart: ({_dragStart.X:F1}, {_dragStart.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"  DragEnd: ({_dragCurrent.X:F1}, {_dragCurrent.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"  IsCrossing: {isCrossing}");
            System.Diagnostics.Debug.WriteLine($"  CandidateElementsCount: {_filterService.VisibleElements.Count()}");
            System.Diagnostics.Debug.WriteLine($"  MatchedElementsCount: {hits.Count}");
            System.Diagnostics.Debug.WriteLine($"  SelectedGlobalIds: {string.Join(", ", hits.Select(h => h.ElementID))}");

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

            foreach (var projection in GetElementProjections(viewport))
            {
                bool matched;
                if (crossing)
                {
                    // Crossing selection: intersects bounds, or any sample is inside
                    matched = screenRect.IntersectsWith(projection.ScreenBounds) ||
                              projection.ScreenSamples.Any(screenRect.Contains);
                }
                else
                {
                    // Window selection: fully contained bounds and all samples are inside
                    matched = BoundaryBoxFullyContained(screenRect, projection.ScreenBounds) &&
                              projection.ScreenSamples.All(screenRect.Contains);
                }

                if (matched && seen.Add(projection.Element))
                {
                    result.Add(projection.Element);
                }
            }

            return result;
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
            var projections = new List<ElementProjection>();
            var seen = new HashSet<PrecastElement>(ReferenceEqualityComparer.Instance);

            foreach (var child in viewport.Children.OfType<ModelVisual3D>())
                CollectElementProjections(viewport, child, Transform3D.Identity, projections, seen);

            foreach (var element in _filterService.VisibleElements)
            {
                if (TryProjectElementBox(viewport, element, out var screenBounds, out var samples, out var depth) &&
                    seen.Add(element))
                {
                    projections.Add(new ElementProjection(element, screenBounds, samples, depth));
                }
            }

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
                Point screenPoint;

                try
                {
                    screenPoint = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, worldPoint);
                }
                catch
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

            screenBounds = InflateTinyRect(rect.Value);
            samples = projectedPoints.ToArray();
            depth = depthSum / projectedCount;
            return true;
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

        private readonly record struct ElementProjection(
            PrecastElement Element,
            Rect ScreenBounds,
            Point[] ScreenSamples,
            double Depth);
    }
}
