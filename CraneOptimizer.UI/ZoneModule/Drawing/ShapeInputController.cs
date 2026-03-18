using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ZoneModule.UI;

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ShapeInputController
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State-machine controller for Rectangle, Circle, and Arc input.
    /// Drives CommandLineBar prompts, ViewportOverlayCanvas preview geometry,
    /// and CursorPickStrip method selection.
    ///
    /// ── Usage (Session 6 / MainWindow) ────────────────────────────────────
    ///
    ///   var _shapeCtrl = new ShapeInputController();
    ///   _shapeCtrl.AttachViewport(IfcViewer);
    ///   _shapeCtrl.AttachOverlay(Overlay);
    ///   _shapeCtrl.AttachCommandLine(CmdLine);
    ///   _shapeCtrl.AttachPickStrip(PickStrip);
    ///
    ///   // Start from ribbon buttons:
    ///   _shapeCtrl.StartRectangle(ZonePreviewType.Allowed);
    ///   _shapeCtrl.StartCircle(ZonePreviewType.Forbidden);
    ///   _shapeCtrl.StartArc(ZonePreviewType.SiteBoundary);
    ///
    ///   // Listen for results:
    ///   _shapeCtrl.RectangleConfirmed += data => _zoneController.AddRectangle(data);
    ///   _shapeCtrl.CircleConfirmed    += data => _zoneController.AddCircle(data);
    ///   _shapeCtrl.ArcConfirmed       += data => _zoneController.AddArc(data);
    ///
    ///   // Cancel:
    ///   _shapeCtrl.Cancel();
    ///
    /// ── State flow (Rectangle Method 1 example) ───────────────────────────
    ///
    ///   StartRectangle() → State.ChooseMethod
    ///     CommandLine: "RECTANGLE  >  Choose method: 1=W×H  2=2 corners  3=Side"
    ///     PickStrip shows: [W×H] [2 pts] [Side]
    ///   User presses "1" / types "1" / clicks [W×H]
    ///     → State.Rect_EnterWidth
    ///     CommandLine: "RECTANGLE  >  Enter Width (e.g. 10 or 10m or 33ft):"
    ///   User types "15" + Enter
    ///     → State.Rect_EnterHeight
    ///     CommandLine: "RECTANGLE  >  Enter Height:"
    ///   User types "8" + Enter
    ///     → State.Rect_PickPlacement
    ///     CommandLine: "RECTANGLE  >  Click to place  |  W=15.00 m  H=8.00 m"
    ///     Preview rectangle follows cursor.
    ///   User left-clicks
    ///     → RectangleConfirmed event fired, state reset.
    ///
    /// </summary>
    public class ShapeInputController
    {
        // ── Attached services ─────────────────────────────────────────────────
        private HelixViewport3D _viewport;
        private ViewportOverlayCanvas _overlay;
        private CommandLineBar _cmdLine;
        private CursorPickStrip _pickStrip;

        // ── Events ────────────────────────────────────────────────────────────

        public event Action<RectangleShapeData> RectangleConfirmed;
        public event Action<CircleShapeData> CircleConfirmed;
        public event Action<ArcShapeData> ArcConfirmed;
        public event Action<PickObjectsShapeData> PickObjectsConfirmed;
        public event Action DrawingCancelled;
        public event Action DrawingStarted;

        // ── State machine ─────────────────────────────────────────────────────

        private enum State
        {
            Idle,

            // --- Shared ---
            ChooseMethod,

            // --- Rectangle ---
            Rect_EnterWidth,
            Rect_EnterHeight,
            Rect_PickPlacement,
            Rect_PickCornerA,
            Rect_PickCornerB,
            Rect_PickSideA,
            Rect_PickSideB,
            Rect_EnterSideHeight,

            // --- Circle ---
            Circle_PickCenter,
            Circle_PickRadius,
            Circle_EnterDiameter,
            Circle_Pick3P_P1,
            Circle_Pick3P_P2,
            Circle_Pick3P_P3,

            // --- Arc ---
            Arc_PickStart,
            Arc_PickEnd,
            Arc_EnterRadius,
            Arc_ChooseDirection,
            Arc_Center_PickCenter,
            Arc_Center_PickStart,
            Arc_Center_PickEnd,
            Arc_3P_P1,
            Arc_3P_P2,
            Arc_3P_P3,

            // --- Pick Objects ---
            PickObjects_Picking,
        }

        private State _state = State.Idle;
        private ZonePreviewType _zoneType = ZonePreviewType.Allowed;
        private ShapeKind _shapeKind = ShapeKind.Rectangle;

        private enum ShapeKind { Rectangle, Circle, Arc, PickObjects }

        // ── Temporary state variables ─────────────────────────────────────────
        private double _width, _height, _radius;
        private Point _screenA, _screenB, _screenC;
        private Point3D _worldA, _worldB, _worldC;
        private Point3D _circleCenter;
        private int _arcMethod;  // 1 = start/end/radius, 2 = center/start/end, 3 = 3-point
        private bool _arcIsMinor = true;

        // ══════════════════════════════════════════════════════════════════════
        //  ATTACH
        // ══════════════════════════════════════════════════════════════════════

        public void AttachViewport(HelixViewport3D viewport) => _viewport = viewport;
        public void AttachOverlay(ViewportOverlayCanvas overlay) => _overlay = overlay;
        public void AttachCommandLine(CommandLineBar cmdLine) => _cmdLine = cmdLine;
        public void AttachPickStrip(CursorPickStrip pickStrip) => _pickStrip = pickStrip;

        public bool IsActive => _state != State.Idle;
        public bool IsPickObjectsActive => _state == State.PickObjects_Picking;

        /// <summary>
        /// Updates the zone type while a shape tool is already active.
        /// Called when user switches zone mode (Allowed→Forbidden) mid-draw.
        /// </summary>
        public void UpdateZoneType(ZonePreviewType newType)
        {
            _zoneType = newType;
        }

        // ── Remember last used method per shape (Spec: no need to pick every time) ──
        private string _lastRectMethod = "1"; // default: W×H
        private string _lastCircleMethod = "1"; // default: Center+Radius
        private string _lastArcMethod = "1"; // default: Start/End/Radius

        // ══════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ══════════════════════════════════════════════════════════════════════

        public void StartRectangle(ZonePreviewType zoneType)
        {
            BeginTool(ShapeKind.Rectangle, zoneType);
            // Skip chooser — jump straight to last used method
            SelectRectMethod(_lastRectMethod);
        }

        public void StartCircle(ZonePreviewType zoneType)
        {
            BeginTool(ShapeKind.Circle, zoneType);
            SelectCircleMethod(_lastCircleMethod);
        }

        public void StartArc(ZonePreviewType zoneType)
        {
            BeginTool(ShapeKind.Arc, zoneType);
            SelectArcMethod(_lastArcMethod);
        }

        // ── Pick Objects / Pick Lines fields ────────────────────────────────
        private HelixViewport3D _pickViewport;
        private List<ModelVisual3D> _pickIfcModels;
        private Dictionary<ModelVisual3D, string> _pickVisualToIdMap;
        private readonly List<ModelVisual3D> _pickedVisuals = new();
        private readonly List<System.Windows.Media.Media3D.Material> _pickedOriginalMaterials = new();

        // Pick Lines mode (Revit-style polygon drawing by clicking on IFC geometry)
        private readonly List<Point3D> _pickLinePoints = new();
        private readonly List<Point> _pickLineScreenPoints = new();
        private OsnapSystem _pickOsnap;
        private const double PICK_CLOSE_THRESHOLD_PX = 12.0;

        // Hover edge — the nearest mesh edge under the cursor, updated every mouse-move
        private (Point3D A, Point3D B, Point SA, Point SB)? _hoveredEdge;

        /// <summary>
        /// Start Pick Lines mode — user clicks on/near IFC geometry to add polyline
        /// vertices. Each click adds a point, preview lines shown between vertices.
        /// Close the polygon by clicking near the start point or pressing C.
        /// </summary>
        public void StartPickObjects(
            ZonePreviewType zoneType,
            HelixViewport3D viewport,
            List<ModelVisual3D> ifcModels,
            Dictionary<ModelVisual3D, string> visualToIdMap,
            OsnapSystem osnap = null)
        {
            Cancel(); // cancel any active tool

            _zoneType = zoneType;
            _shapeKind = ShapeKind.PickObjects;
            _pickViewport = viewport;
            _pickIfcModels = ifcModels;
            _pickVisualToIdMap = visualToIdMap;
            _pickOsnap = osnap;
            _pickedVisuals.Clear();
            _pickedOriginalMaterials.Clear();
            _pickLinePoints.Clear();
            _pickLineScreenPoints.Clear();
            _hoveredEdge = null;
            _state = State.PickObjects_Picking;

            _cmdLine?.SetMode(CommandMode.Draw);
            _cmdLine?.SetPrompt("PICK LINES  >  Hover over a building edge to highlight it, then click to pick it  (C = close, Esc = cancel)");
            _cmdLine?.Log("Pick Lines — hover to highlight an edge, click to pick the full edge segment");
            DrawingStarted?.Invoke();

            if (_pickViewport != null)
            {
                _pickViewport.MouseLeftButtonDown += PickObjects_MouseLeftButtonDown;
                _pickViewport.MouseMove += PickLines_MouseMove;
            }
        }

        private void PickObjects_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_state != State.PickObjects_Picking || _pickViewport == null) return;

            var screenPt = e.GetPosition(_pickViewport);

            // Use cached hovered edge (from last mouse-move) or search fresh
            var edgeResult = _hoveredEdge ?? FindNearestEdge(screenPt);

            if (!edgeResult.HasValue)
            {
                _cmdLine?.Log("⚠ No IFC edge found here — move closer to a building edge.");
                e.Handled = true;
                return;
            }

            var (edgeA, edgeB, screenA, screenB) = edgeResult.Value;

            if (_pickLinePoints.Count == 0)
            {
                // First edge — add both endpoints; chain now has 1 segment
                _pickLinePoints.Add(edgeA);
                _pickLinePoints.Add(edgeB);
            }
            else
            {
                // Subsequent edges — figure out which endpoint extends the chain
                Point3D chainEnd = _pickLinePoints[_pickLinePoints.Count - 1];
                bool aIsNear = Distance2D(edgeA, chainEnd) <= Distance2D(edgeB, chainEnd);
                Point3D farPt = aIsNear ? edgeB : edgeA;

                // Auto-close if far endpoint is near the start of the chain
                if (_pickLinePoints.Count >= 3 &&
                    Distance2D(farPt, _pickLinePoints[0]) < ComputeWorldCloseThreshold())
                {
                    FinishPickLines();
                    e.Handled = true;
                    return;
                }

                _pickLinePoints.Add(farPt);
            }

            // Rebuild the full polygon screen preview from world positions
            RedrawPickLinesPreview();

            int n = _pickLinePoints.Count;
            _cmdLine?.SetPrompt($"PICK LINES  >  {n} point{(n == 1 ? "" : "s")}" +
                (n >= 4 ? " — C to close or click near start" : " — keep clicking on edges"));

            e.Handled = true;
        }

        private void PickLines_MouseMove(object sender, MouseEventArgs e)
        {
            if (_state != State.PickObjects_Picking || _pickViewport == null) return;

            var screenPt = e.GetPosition(_pickViewport);

            // Detect and highlight the nearest IFC edge under the cursor
            _hoveredEdge = FindNearestEdge(screenPt);
            if (_hoveredEdge.HasValue)
                _overlay?.DrawHoverEdge(_hoveredEdge.Value.SA, _hoveredEdge.Value.SB);
            else
                _overlay?.ClearHoverEdge();

            if (_pickLinePoints.Count == 0) return;

            // Rubber-band: existing chain + prospective new point (far endpoint of hovered edge)
            var previewPts = new List<Point>();
            foreach (var wp in _pickLinePoints)
            {
                var sp = WorldToScreen(wp);
                if (sp.HasValue) previewPts.Add(sp.Value);
            }

            if (_hoveredEdge.HasValue)
            {
                var (wa, wb, sa, sb) = _hoveredEdge.Value;
                Point3D chainEnd = _pickLinePoints[_pickLinePoints.Count - 1];
                bool aIsNear = Distance2D(wa, chainEnd) <= Distance2D(wb, chainEnd);
                previewPts.Add(aIsNear ? sb : sa); // add screen pos of far endpoint
            }
            else
            {
                previewPts.Add(screenPt);
            }

            if (previewPts.Count >= 2)
                _overlay?.DrawPreviewPolygon(previewPts, _zoneType, closed: false);

            // Re-add vertex dots (cleared by DrawPreviewPolygon)
            foreach (var wp in _pickLinePoints)
            {
                var sp = WorldToScreen(wp);
                if (sp.HasValue) _overlay?.AddPreviewVertex(sp.Value, _zoneType);
            }
        }

        /// <summary>
        /// Computes the close threshold in world units (based on camera zoom).
        /// When clicking within this distance of the first point, the polygon auto-closes.
        /// </summary>
        private double ComputeWorldCloseThreshold()
        {
            try
            {
                if (_pickViewport?.Camera is PerspectiveCamera cam)
                {
                    double dist = cam.LookDirection.Length;
                    double viewH = _pickViewport.ActualHeight > 0 ? _pickViewport.ActualHeight : 600;
                    double halfFovRad = cam.FieldOfView * 0.5 * Math.PI / 180.0;
                    double worldH = 2.0 * dist * Math.Tan(halfFovRad);
                    return Math.Max(0.05, PICK_CLOSE_THRESHOLD_PX * worldH / viewH);
                }
                if (_pickViewport?.Camera is OrthographicCamera orth)
                {
                    double viewH = _pickViewport.ActualHeight > 0 ? _pickViewport.ActualHeight : 600;
                    return Math.Max(0.05, PICK_CLOSE_THRESHOLD_PX * orth.Width / viewH);
                }
            }
            catch { }
            return 0.5;
        }

        // ── Redraw helper ──────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the polygon preview and vertex dots from the current world-space
        /// chain. Called after each click so the preview is always up-to-date.
        /// </summary>
        private void RedrawPickLinesPreview()
        {
            var screenPts = new List<Point>();
            foreach (var wp in _pickLinePoints)
            {
                var sp = WorldToScreen(wp);
                if (sp.HasValue) screenPts.Add(sp.Value);
            }

            if (screenPts.Count >= 2)
                _overlay?.DrawPreviewPolygon(screenPts, _zoneType, closed: false);

            // Re-add vertex dots (DrawPreviewPolygon clears them via ClearPreview)
            foreach (var sp in screenPts)
                _overlay?.AddPreviewVertex(sp, _zoneType);
        }

        // ── Edge picking helpers ───────────────────────────────────────────────

        /// <summary>
        /// Hit-tests the viewport at <paramref name="screenPt"/>, finds the IFC mesh
        /// triangle under the cursor, then returns the triangle edge closest to the
        /// cursor in screen space — together with both world-space endpoints.
        /// Returns null if no IFC geometry is under the cursor.
        /// </summary>
        private (Point3D A, Point3D B, Point SA, Point SB)? FindNearestEdge(Point screenPt)
        {
            try
            {
                var hits = Viewport3DHelper.FindHits(_pickViewport.Viewport, screenPt);
                foreach (var hit in hits)
                {
                    var visual = hit.Visual as ModelVisual3D;
                    if (visual == null) continue;

                    if (FindAncestorInList(visual, _pickIfcModels) == null) continue;

                    // Access the hit triangle's vertex indices
                    var rayHit = hit.RayHit as System.Windows.Media.Media3D.RayMeshGeometry3DHitTestResult;
                    if (rayHit?.MeshHit == null) continue;

                    var mesh = rayHit.MeshHit;
                    if (mesh.Positions == null || mesh.Positions.Count < 3) continue;

                    // Triangle vertices in local mesh space → transform to world space
                    var p0 = TransformVertexToWorld(mesh.Positions[rayHit.VertexIndex1], visual);
                    var p1 = TransformVertexToWorld(mesh.Positions[rayHit.VertexIndex2], visual);
                    var p2 = TransformVertexToWorld(mesh.Positions[rayHit.VertexIndex3], visual);

                    // Project each vertex to screen
                    var s0 = WorldToScreen(p0);
                    var s1 = WorldToScreen(p1);
                    var s2 = WorldToScreen(p2);
                    if (!s0.HasValue || !s1.HasValue || !s2.HasValue) continue;

                    // Return the edge whose screen projection is nearest to the cursor
                    double d01 = DistanceFromPointToSegment(screenPt, s0.Value, s1.Value);
                    double d12 = DistanceFromPointToSegment(screenPt, s1.Value, s2.Value);
                    double d20 = DistanceFromPointToSegment(screenPt, s2.Value, s0.Value);

                    if (d01 <= d12 && d01 <= d20) return (p0, p1, s0.Value, s1.Value);
                    if (d12 <= d01 && d12 <= d20) return (p1, p2, s1.Value, s2.Value);
                    return (p2, p0, s2.Value, s0.Value);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Applies the visual's own Transform3D to convert a mesh-local position to
        /// world space. For IFC models with no transform this is a no-op.
        /// </summary>
        private static Point3D TransformVertexToWorld(Point3D localPt, ModelVisual3D visual)
        {
            var t = visual?.Transform;
            return (t == null || t == System.Windows.Media.Media3D.Transform3D.Identity)
                ? localPt
                : t.Transform(localPt);
        }

        /// <summary>
        /// Minimum distance from point <paramref name="p"/> to the line segment AB
        /// (all in 2-D screen space).
        /// </summary>
        private static double DistanceFromPointToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            double t = lenSq < 1e-9 ? 0 :
                Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
            double px = a.X + t * dx, py = a.Y + t * dy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }

        /// <summary>
        /// Finishes Pick Lines mode. Creates a polygon zone from the drawn vertices.
        /// Called externally when user presses C (Finish Zone).
        /// </summary>
        public void FinishPickObjects()
        {
            if (_state != State.PickObjects_Picking) return;

            // Delegate to the new Pick Lines finalization
            FinishPickLines();
        }

        private void FinishPickLines()
        {
            if (_pickLinePoints.Count < 3)
            {
                _cmdLine?.Log("Need at least 3 points to close a polygon.");
                return;
            }

            var vertices = new List<Point3D>(_pickLinePoints);

            // Calculate polygon area (Shoelace formula)
            double area = 0;
            int n = vertices.Count;
            for (int i = 0; i < n; i++)
                area += vertices[i].X * vertices[(i + 1) % n].Y - vertices[(i + 1) % n].X * vertices[i].Y;
            area = Math.Abs(area) / 2.0;

            var data = new PickObjectsShapeData
            {
                Vertices = vertices,
                ZoneType = _zoneType,
                PickedCount = vertices.Count
            };

            _cmdLine?.ShowResult($"Pick Lines polygon: {vertices.Count} vertices, area = {UnitSystem.Format(area)}m\u00B2");

            // Clean up
            RestorePickedMaterials();
            _overlay?.ClearPreview();
            PickObjectsConfirmed?.Invoke(data);

            if (_pickViewport != null)
            {
                _pickViewport.MouseLeftButtonDown -= PickObjects_MouseLeftButtonDown;
                _pickViewport.MouseMove -= PickLines_MouseMove;
            }

            // Clear extra OSnap points and hover state
            _pickOsnap?.ExtraSnapPoints.Clear();
            _hoveredEdge = null;
            _overlay?.ClearHoverEdge();

            _pickLinePoints.Clear();
            _pickLineScreenPoints.Clear();
            _state = State.Idle;
            _cmdLine?.SetMode(CommandMode.Idle);
            DrawingCancelled?.Invoke(); // signals draw tools can be hidden
        }

        private void RestorePickedMaterials()
        {
            for (int i = 0; i < _pickedVisuals.Count; i++)
            {
                var visual = _pickedVisuals[i];
                var origMat = i < _pickedOriginalMaterials.Count ? _pickedOriginalMaterials[i] : null;

                if (visual.Content is GeometryModel3D gm && origMat != null)
                {
                    gm.Material = origMat;
                }
                else if (visual.Content is Model3DGroup grp && origMat != null)
                {
                    foreach (var child in grp.Children)
                    {
                        if (child is GeometryModel3D childGm)
                            childGm.Material = origMat;
                    }
                }
            }
            _pickedVisuals.Clear();
            _pickedOriginalMaterials.Clear();
        }

        public void Cancel()
        {
            if (_state == State.Idle) return;

            // Clean up PickObjects / Pick Lines mode
            if (_state == State.PickObjects_Picking)
            {
                RestorePickedMaterials();
                if (_pickViewport != null)
                {
                    _pickViewport.MouseLeftButtonDown -= PickObjects_MouseLeftButtonDown;
                    _pickViewport.MouseMove -= PickLines_MouseMove;
                }
                _pickLinePoints.Clear();
                _pickLineScreenPoints.Clear();
                _hoveredEdge = null;
                _overlay?.ClearHoverEdge();
                _pickOsnap?.ExtraSnapPoints.Clear();
            }

            DetachMouseHandlers();
            _overlay?.ClearPreview();
            _overlay?.ClearMeasure();  // BUG FIX: Clear stale W/H dimension labels from rectangle tool
            _cmdLine?.ClearAll();
            _pickStrip?.Hide();

            _state = State.Idle;
            DrawingCancelled?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  COMMAND LINE INPUT (called by CommandLine.ValueEntered /
        //  CommandLine.CommandEntered events — wired in Session 6)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called when the user presses Enter in the CommandLine with a string value.
        /// Handles method selection ("1", "2", "3") and keyword options.
        /// </summary>
        public void HandleCommandEntered(string command)
        {
            if (_state == State.Idle) return;

            string cmd = command.Trim().ToUpperInvariant();

            if (_state == State.ChooseMethod)
            {
                SelectMethod(cmd);
                return;
            }

            // C or CLOSE in Pick Lines mode — finish the polygon
            if (_state == State.PickObjects_Picking && (cmd == "C" || cmd == "CLOSE" || cmd == "FINISH"))
            {
                FinishPickObjects();
                return;
            }

            // Keyword shortcuts in any state
            switch (cmd)
            {
                case "ESC":
                case "CANCEL":
                    Cancel();
                    return;
            }
        }

        /// <summary>
        /// Called when the user presses Enter with a numeric value.
        /// Handles dimension entry (width, height, radius, etc.)
        /// </summary>
        public void HandleValueEntered(double metres)
        {
            if (_state == State.Idle) return;

            switch (_state)
            {
                case State.Rect_EnterWidth:
                    _width = metres;
                    _state = State.Rect_EnterHeight;
                    _cmdLine?.SetPrompt("Enter Height:");  // BUG FIX: Was "Enter Width:" — inconsistent with state name
                    break;

                case State.Rect_EnterHeight:
                    _height = metres;
                    EnterPickPlacement();
                    break;

                case State.Circle_PickRadius:
                    // User typed a radius instead of clicking
                    _radius = metres;
                    ConfirmCircle(_circleCenter, _radius);
                    break;

                case State.Circle_EnterDiameter:
                    _radius = metres / 2.0;
                    ConfirmCircle(_circleCenter, _radius);
                    break;

                case State.Arc_EnterRadius:
                    _radius = metres;
                    // Compute two possible arcs, show choice
                    EnterArcChooseDirection();
                    break;

                case State.Rect_EnterSideHeight:
                    _height = metres;
                    // Confirm rectangle with rotation from _worldA→_worldB side
                    ConfirmRotatedRectangle();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SPACE BAR — confirm at current mouse position
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called when the user presses Space during placement.
        /// Confirms the shape at the current cursor position.
        /// </summary>
        public void HandleSpaceBar()
        {
            if (_state == State.Rect_PickPlacement)
                ConfirmRectAtCurrentCursor();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATE MACHINE — private helpers
        // ══════════════════════════════════════════════════════════════════════

        private void BeginTool(ShapeKind kind, ZonePreviewType zoneType)
        {
            Cancel(); // clean up any prior tool

            _shapeKind = kind;
            _zoneType = zoneType;

            AttachMouseHandlers();
            DrawingStarted?.Invoke();
        }

        private void EnterChooseMethod(string shapeName)
        {
            _state = State.ChooseMethod;

            string methods = shapeName switch
            {
                "RECTANGLE" => "1=W×H  2=2 corners  3=Side",
                "CIRCLE" => "1=Center+Radius  2=Center+Diameter  3=3-point",
                "ARC" => "1=Start/End/Radius  2=Center/Start/End  3=3-point",
                _ => "1  2  3"
            };

            _cmdLine?.SetMode(CommandMode.Draw);
            _cmdLine?.SetPrompt($"Choose method: {methods}");

            // Show pick strip with method buttons
            _pickStrip?.Show(shapeName == "RECTANGLE"
                ? new[] { ("W×H", "1"), ("2 pts", "2"), ("Side", "3") }
                : shapeName == "CIRCLE"
                ? new[] { ("C+R", "1"), ("C+D", "2"), ("3pt", "3") }
                : new[] { ("S/E/R", "1"), ("C/S/E", "2"), ("3pt", "3") });
        }

        private void SelectMethod(string cmd)
        {
            _pickStrip?.Hide();

            switch (_shapeKind)
            {
                case ShapeKind.Rectangle: SelectRectMethod(cmd); break;
                case ShapeKind.Circle: SelectCircleMethod(cmd); break;
                case ShapeKind.Arc: SelectArcMethod(cmd); break;
            }
        }

        // ── RECTANGLE methods ─────────────────────────────────────────────────

        private void SelectRectMethod(string cmd)
        {
            switch (cmd)
            {
                case "1":
                case "W":
                    _lastRectMethod = "1";
                    _state = State.Rect_EnterWidth;
                    _cmdLine?.SetPrompt("Enter Length (e.g. 10 or 10m or 33ft):");
                    break;

                case "2":
                    _lastRectMethod = "2";
                    _state = State.Rect_PickCornerA;
                    _cmdLine?.SetPrompt("Click first corner:");
                    break;

                case "3":
                    _lastRectMethod = "3";
                    _state = State.Rect_PickSideA;
                    _cmdLine?.SetPrompt("Click start of width side:");
                    break;

                default:
                    // Unknown — show the chooser so user can pick
                    EnterChooseMethod("RECTANGLE");
                    break;
            }
        }

        private void EnterPickPlacement()
        {
            _state = State.Rect_PickPlacement;
            string wLabel = UnitSystem.Format(_width);
            string hLabel = UnitSystem.Format(_height);
            _cmdLine?.SetPrompt($"Click to place  |  Length={wLabel}  Width={hLabel}");
            _cmdLine?.Log($"Rectangle: Length={wLabel} × Width={hLabel}. Click to place or press Space.");
        }

        private void ConfirmRectAtCurrentCursor()
        {
            if (_lastCursorWorld == null) return;
            ConfirmRectangle(_lastCursorWorld.Value, _width, _height, 0);
        }

        private void ConfirmRectangle(Point3D center, double width, double height, double rotation)
        {
            _overlay?.ClearPreview();

            // Build corners
            double hw = width / 2.0;
            double hh = height / 2.0;
            var corners = new List<Point3D>
            {
                new(center.X - hw, center.Y + hh, center.Z),
                new(center.X + hw, center.Y + hh, center.Z),
                new(center.X + hw, center.Y - hh, center.Z),
                new(center.X - hw, center.Y - hh, center.Z),
            };

            var data = new RectangleShapeData
            {
                ZoneType = _zoneType,
                Center = center,
                Width = width,
                Height = height,
                Rotation = rotation,
                Corners = corners
            };

            ResetToIdle();
            RectangleConfirmed?.Invoke(data);
        }

        private void ConfirmRotatedRectangle()
        {
            // _worldA and _worldB define the width side
            // _height was entered by user
            double dx = _worldB.X - _worldA.X;
            double dy = _worldB.Y - _worldA.Y;
            double width = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            var center = new Point3D((_worldA.X + _worldB.X) / 2.0,
                                        (_worldA.Y + _worldB.Y) / 2.0,
                                        0);
            ConfirmRectangle(center, width, _height, angle);
        }

        // ── CIRCLE methods ────────────────────────────────────────────────────

        private void SelectCircleMethod(string cmd)
        {
            switch (cmd)
            {
                case "1":
                    _lastCircleMethod = "1";
                    _state = State.Circle_PickCenter;
                    _cmdLine?.SetPrompt("Click center point:");
                    break;

                case "2":
                    _lastCircleMethod = "2";
                    _state = State.Circle_PickCenter;
                    _cmdLine?.SetPrompt("Click center point (diameter method):");
                    _circleDiameterMode = true;
                    break;

                case "3":
                    _lastCircleMethod = "3";
                    _state = State.Circle_Pick3P_P1;
                    _cmdLine?.SetPrompt("Click point 1 of 3:");
                    break;

                default:
                    EnterChooseMethod("CIRCLE");
                    break;
            }
        }

        private bool _circleDiameterMode = false;

        private void ConfirmCircle(Point3D center, double radius)
        {
            _overlay?.ClearPreview();
            var data = new CircleShapeData { ZoneType = _zoneType, Center = center, Radius = radius };
            ResetToIdle();
            CircleConfirmed?.Invoke(data);
        }

        // ── ARC methods ───────────────────────────────────────────────────────

        private void SelectArcMethod(string cmd)
        {
            switch (cmd)
            {
                case "1":
                    _lastArcMethod = "1";
                    _arcMethod = 1;
                    _state = State.Arc_PickStart;
                    _cmdLine?.SetPrompt("Click start point:");
                    break;

                case "2":
                    _lastArcMethod = "2";
                    _arcMethod = 2;
                    _state = State.Arc_Center_PickCenter;
                    _cmdLine?.SetPrompt("Click center:");
                    break;

                case "3":
                    _lastArcMethod = "3";
                    _arcMethod = 3;
                    _state = State.Arc_3P_P1;
                    _cmdLine?.SetPrompt("Click point 1 (start):");
                    break;

                default:
                    EnterChooseMethod("ARC");
                    break;
            }
        }

        private void EnterArcChooseDirection()
        {
            _state = State.Arc_ChooseDirection;
            _cmdLine?.SetPrompt("Click to choose arc direction  [major / minor]");

            // Draw both arcs on overlay as preview
            // Minor arc = shorter path, major = longer
            DrawArcPreview(_worldA, _worldB, _radius, _arcIsMinor);
        }

        private void ConfirmArc(Point3D center, double radius,
                                 double startAngle, double endAngle, bool isMinor)
        {
            _overlay?.ClearPreview();
            var data = new ArcShapeData
            {
                ZoneType = _zoneType,
                Center = center,
                Radius = radius,
                StartAngle = startAngle,
                EndAngle = endAngle,
                IsMinorArc = isMinor
            };
            ResetToIdle();
            ArcConfirmed?.Invoke(data);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MOUSE HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private Point3D? _lastCursorWorld = null;

        private void AttachMouseHandlers()
        {
            if (_viewport == null) return;
            _viewport.MouseLeftButtonDown += Overlay_MouseDown;
            _viewport.MouseMove += Overlay_MouseMove;
        }

        private void DetachMouseHandlers()
        {
            if (_viewport == null) return;
            _viewport.MouseLeftButtonDown -= Overlay_MouseDown;
            _viewport.MouseMove -= Overlay_MouseMove;
        }

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_state == State.Idle) return;

            Point screen = e.GetPosition(_viewport);
            Point3D world = Unproject(screen);
            _lastCursorWorld = world;

            switch (_state)
            {
                // ── RECTANGLE ─────────────────────────────────────────
                case State.Rect_PickPlacement:
                    ConfirmRectangle(world, _width, _height, 0);
                    break;

                case State.Rect_PickCornerA:
                    _screenA = screen; _worldA = world;
                    _state = State.Rect_PickCornerB;
                    _cmdLine?.SetPrompt("Click opposite corner:");
                    break;

                case State.Rect_PickCornerB:
                    _screenB = screen; _worldB = world;
                    double w = Math.Abs(_worldB.X - _worldA.X);
                    double h = Math.Abs(_worldB.Y - _worldA.Y);
                    var c = new Point3D((_worldA.X + _worldB.X) / 2,
                                           (_worldA.Y + _worldB.Y) / 2, 0);
                    ConfirmRectangle(c, w, h, 0);
                    break;

                case State.Rect_PickSideA:
                    _screenA = screen; _worldA = world;
                    _state = State.Rect_PickSideB;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _cmdLine?.SetPrompt("Click end of width side:");
                    break;

                case State.Rect_PickSideB:
                    _screenB = screen; _worldB = world;
                    _state = State.Rect_EnterSideHeight;
                    _cmdLine?.SetPrompt($"Enter Height or click to set:");
                    break;

                // ── CIRCLE ────────────────────────────────────────────
                case State.Circle_PickCenter:
                    _circleCenter = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    if (_circleDiameterMode)
                    {
                        _state = State.Circle_EnterDiameter;
                        _cmdLine?.SetPrompt("Enter diameter OR click diameter endpoint:");
                    }
                    else
                    {
                        _state = State.Circle_PickRadius;
                        _cmdLine?.SetPrompt("Click radius point OR type radius:");
                    }
                    break;

                case State.Circle_PickRadius:
                    {
                        double r = Distance2D(_circleCenter, world);
                        ConfirmCircle(_circleCenter, r);
                        break;
                    }

                case State.Circle_Pick3P_P1:
                    _worldA = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Circle_Pick3P_P2;
                    _cmdLine?.SetPrompt("Click point 2 of 3:");
                    break;

                case State.Circle_Pick3P_P2:
                    _worldB = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Circle_Pick3P_P3;
                    _cmdLine?.SetPrompt("Click point 3 of 3:");
                    break;

                case State.Circle_Pick3P_P3:
                    {
                        _worldC = world;
                        if (CircleFrom3Points(_worldA, _worldB, _worldC,
                                              out Point3D center, out double radius))
                            ConfirmCircle(center, radius);
                        else
                        {
                            _cmdLine?.Log("Points are collinear — cannot define a circle. Try again.");
                            _state = State.Circle_Pick3P_P1;
                            _overlay?.ClearPreview();
                            _cmdLine?.SetPrompt("Click point 1 of 3:");
                        }
                        break;
                    }

                // ── ARC ───────────────────────────────────────────────
                case State.Arc_PickStart:
                    _worldA = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Arc_PickEnd;
                    _cmdLine?.SetPrompt("Click end point:");
                    break;

                case State.Arc_PickEnd:
                    _worldB = world;
                    _state = State.Arc_EnterRadius;
                    _cmdLine?.SetPrompt("Enter radius OR click to set:");
                    break;

                case State.Arc_ChooseDirection:
                    // Toggle between major/minor arc on each click
                    _arcIsMinor = !_arcIsMinor;
                    DrawArcPreview(_worldA, _worldB, _radius, _arcIsMinor);
                    _cmdLine?.SetPrompt(_arcIsMinor
                        ? "Minor arc selected — click to confirm or click again to switch"
                        : "Major arc selected — click to confirm or click again to switch");

                    // Double-click to confirm: handled by checking if same point clicked twice
                    // For simplicity: second click on same side confirms
                    ArcFromStartEnd(_worldA, _worldB, _radius, _arcIsMinor,
                                    out Point3D arcCenter, out double sa, out double ea);
                    ConfirmArc(arcCenter, _radius, sa, ea, _arcIsMinor);
                    break;

                case State.Arc_Center_PickCenter:
                    _circleCenter = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Arc_Center_PickStart;
                    _cmdLine?.SetPrompt("Click start point:");
                    break;

                case State.Arc_Center_PickStart:
                    _worldA = world;
                    _radius = Distance2D(_circleCenter, world);
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Arc_Center_PickEnd;
                    _cmdLine?.SetPrompt("Click end point (arc goes counter-clockwise):");
                    break;

                case State.Arc_Center_PickEnd:
                    {
                        _worldB = world;
                        double startAngle = Math.Atan2(_worldA.Y - _circleCenter.Y,
                                                       _worldA.X - _circleCenter.X) * 180 / Math.PI;
                        double endAngle = Math.Atan2(world.Y - _circleCenter.Y,
                                                     world.X - _circleCenter.X) * 180 / Math.PI;
                        ConfirmArc(_circleCenter, _radius, startAngle, endAngle, true);
                        break;
                    }

                case State.Arc_3P_P1:
                    _worldA = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Arc_3P_P2;
                    _cmdLine?.SetPrompt("Click point 2 (on arc):");
                    break;

                case State.Arc_3P_P2:
                    _worldB = world;
                    _overlay?.AddPreviewVertex(screen, _zoneType);
                    _state = State.Arc_3P_P3;
                    _cmdLine?.SetPrompt("Click point 3 (end):");
                    break;

                case State.Arc_3P_P3:
                    {
                        _worldC = world;
                        if (CircleFrom3Points(_worldA, _worldB, _worldC,
                                              out Point3D center, out double radius))
                        {
                            double startAngle = Math.Atan2(_worldA.Y - center.Y, _worldA.X - center.X) * 180 / Math.PI;
                            double endAngle = Math.Atan2(_worldC.Y - center.Y, _worldC.X - center.X) * 180 / Math.PI;
                            ConfirmArc(center, radius, startAngle, endAngle, true);
                        }
                        else
                        {
                            _cmdLine?.Log("Points are collinear — cannot define arc. Try again.");
                            _state = State.Arc_3P_P1;
                            _overlay?.ClearPreview();
                            _cmdLine?.SetPrompt("Click point 1 (start):");
                        }
                        break;
                    }
            }

            e.Handled = true;
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_state == State.Idle) return;

            Point screen = e.GetPosition(_viewport);
            Point3D world = Unproject(screen);
            _lastCursorWorld = world;

            // Update pick strip position
            _pickStrip?.UpdatePosition(screen);

            // Update overlay preview
            switch (_state)
            {
                case State.Rect_PickPlacement:
                    {
                        _overlay?.ClearPreview();
                        double hw = _width / 2.0;
                        double hh = _height / 2.0;
                        // Convert world rect corners to screen and draw preview
                        // (Simplified: use screen-space rect approximated from world extent)
                        var pts = new[]
                        {
                        WorldToScreen(new Point3D(world.X - hw, world.Y + hh, world.Z)),
                        WorldToScreen(new Point3D(world.X + hw, world.Y + hh, world.Z)),
                        WorldToScreen(new Point3D(world.X + hw, world.Y - hh, world.Z)),
                        WorldToScreen(new Point3D(world.X - hw, world.Y - hh, world.Z)),
                    };
                        if (pts[0].HasValue && pts[1].HasValue && pts[2].HasValue && pts[3].HasValue)
                        {
                            var screenPts = new List<Point>
                        {
                            pts[0].Value, pts[1].Value, pts[2].Value, pts[3].Value
                        };
                            _overlay?.DrawPreviewPolygon(screenPts, _zoneType, closed: true);

                            // W label on bottom edge, H label on right edge
                            var midBottom = new Point((pts[3].Value.X + pts[2].Value.X) / 2,
                                                       (pts[3].Value.Y + pts[2].Value.Y) / 2 + 12);
                            var midRight = new Point((pts[1].Value.X + pts[2].Value.X) / 2 + 12,
                                                       (pts[1].Value.Y + pts[2].Value.Y) / 2);
                            _overlay?.ShowMeasureLabel(midBottom, $"W: {UnitSystem.Format(_width)}");
                            _overlay?.ShowMeasureLabel(midRight, $"H: {UnitSystem.Format(_height)}");
                        }
                        break;
                    }

                case State.Rect_PickCornerB when _state == State.Rect_PickCornerB:
                    {
                        _overlay?.ClearPreview();
                        double w = Math.Abs(world.X - _worldA.X);
                        double h = Math.Abs(world.Y - _worldA.Y);
                        double minX = Math.Min(screen.X, _screenA.X);
                        double minY = Math.Min(screen.Y, _screenA.Y);
                        _overlay?.DrawPreviewRect(
                            new Rect(minX, minY, Math.Abs(screen.X - _screenA.X), Math.Abs(screen.Y - _screenA.Y)),
                            _zoneType, w, h);
                        break;
                    }

                case State.Circle_PickRadius:
                    {
                        double r = Distance2D(_circleCenter, world);
                        var cs = WorldToScreen(_circleCenter);
                        if (cs.HasValue)
                        {
                            double rScreen = Distance2D(cs.Value, screen);
                            _overlay?.ClearPreview();
                            _overlay?.DrawPreviewCircle(cs.Value, rScreen, _zoneType, r);
                        }
                        break;
                    }

                case State.Arc_ChooseDirection:
                    DrawArcPreview(_worldA, _worldB, _radius, _arcIsMinor);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PREVIEW HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void DrawArcPreview(Point3D start, Point3D end, double radius, bool minor)
        {
            if (_overlay == null) return;
            _overlay.ClearPreview();

            ArcFromStartEnd(start, end, radius, minor,
                            out Point3D center, out double sa, out double ea);

            // Sample arc as polygon points
            var pts = new List<Point>();
            int steps = 32;
            double span = minor
                ? NormaliseAngle(ea - sa)
                : 360 - NormaliseAngle(ea - sa);
            for (int i = 0; i <= steps; i++)
            {
                double angle = (sa + (span * i / steps)) * Math.PI / 180.0;
                var worldPt = new Point3D(center.X + radius * Math.Cos(angle),
                                           center.Y + radius * Math.Sin(angle), 0);
                var sp = WorldToScreen(worldPt);
                if (sp.HasValue) pts.Add(sp.Value);
            }
            if (pts.Count > 1)
                _overlay.DrawPreviewPolygon(pts, _zoneType, closed: false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GEOMETRY HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void ResetToIdle()
        {
            _state = State.Idle;
            _circleDiameterMode = false;
            DetachMouseHandlers();
            _overlay?.ClearMeasure();  // BUG FIX: Clear stale W/H dimension labels after confirmation
            _cmdLine?.ClearAll();
            _pickStrip?.Hide();
        }

        private Point3D Unproject(Point screenPt)
        {
            if (_viewport == null) return new Point3D();

            try
            {
                var hit = _viewport.FindNearestPoint(screenPt);
                if (hit.HasValue) return hit.Value;
            }
            catch { }

            try
            {
                bool ok = Viewport3DHelper.Point2DtoPoint3D(
                    _viewport.Viewport, screenPt, out Point3D near, out Point3D far);
                if (ok)
                {
                    var dir = far - near;
                    if (Math.Abs(dir.Z) > 1e-9)
                    {
                        double t = -near.Z / dir.Z;
                        if (t > 0) return near + t * dir;
                    }
                }
            }
            catch { }

            return new Point3D(screenPt.X * 0.01, screenPt.Y * 0.01, 0);
        }

        private Point? WorldToScreen(Point3D world)
        {
            if (_viewport == null) return null;
            try
            {
                var pt2d = Viewport3DHelper.Point3DtoPoint2D(_viewport.Viewport, world);
                return pt2d;
            }
            catch { return null; }
        }

        private static double Distance2D(Point3D a, Point3D b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Distance2D(Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double NormaliseAngle(double deg)
        {
            deg %= 360;
            return deg < 0 ? deg + 360 : deg;
        }

        /// <summary>Compute arc centre from start+end+radius.</summary>
        private static void ArcFromStartEnd(Point3D start, Point3D end, double radius,
                                             bool minor,
                                             out Point3D center, out double startAngle, out double endAngle)
        {
            double mx = (start.X + end.X) / 2.0;
            double my = (start.Y + end.Y) / 2.0;
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);

            if (d < 1e-9 || radius < d / 2.0)
            {
                // Degenerate: return midpoint as center
                center = new Point3D(mx, my, 0);
                startAngle = 0;
                endAngle = 180;
                return;
            }

            double h = Math.Sqrt(radius * radius - (d / 2.0) * (d / 2.0));
            double perpX = -dy / d;
            double perpY = dx / d;
            int sign = minor ? 1 : -1;

            center = new Point3D(mx + sign * h * perpX,
                                 my + sign * h * perpY, 0);

            startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180 / Math.PI;
            endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X) * 180 / Math.PI;
        }

        /// <summary>
        /// BUG 2 FIX: Walk up the visual tree from a hit visual to find an ancestor
        /// that exists in the given list of IFC model visuals.
        /// </summary>
        private static ModelVisual3D FindAncestorInList(
            System.Windows.Media.Media3D.Visual3D visual,
            System.Collections.Generic.List<ModelVisual3D> models)
        {
            if (models == null) return null;
            var current = visual;
            while (current != null)
            {
                if (current is ModelVisual3D mv && models.Contains(mv))
                    return mv;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                          as System.Windows.Media.Media3D.Visual3D;
            }
            return null;
        }

        /// <summary>Compute circumscribed circle through 3 points.</summary>
        private static bool CircleFrom3Points(Point3D p1, Point3D p2, Point3D p3,
                                               out Point3D center, out double radius)
        {
            double ax = p1.X, ay = p1.Y;
            double bx = p2.X, by = p2.Y;
            double cx = p3.X, cy = p3.Y;

            double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(D) < 1e-9)
            {
                center = default;
                radius = 0;
                return false; // collinear
            }

            double ux = ((ax * ax + ay * ay) * (by - cy) +
                         (bx * bx + by * by) * (cy - ay) +
                         (cx * cx + cy * cy) * (ay - by)) / D;
            double uy = ((ax * ax + ay * ay) * (cx - bx) +
                         (bx * bx + by * by) * (ax - cx) +
                         (cx * cx + cy * cy) * (bx - ax)) / D;

            center = new Point3D(ux, uy, 0);
            radius = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
            return true;
        }
    }
}