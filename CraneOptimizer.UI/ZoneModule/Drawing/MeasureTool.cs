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
    //  MeasureTool
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Self-contained measure tool.  Zero dependency on MainWindow.
    ///
    /// Usage pattern (wired in Session 6 / MainWindow):
    ///
    ///   var _measure = new MeasureTool();
    ///   _measure.AttachViewport(IfcViewer);
    ///   _measure.AttachOverlay(Overlay);
    ///   _measure.AttachCommandLine(CmdLine);
    ///
    ///   // Start measuring (e.g. from ribbon button or "M" command):
    ///   _measure.Start();
    ///
    ///   // Listen for results:
    ///   _measure.MeasureCompleted += (metres, label) =>
    ///       StatusText.Text = $"Distance: {label}";
    ///
    ///   // Cancel from Escape key:
    ///   _measure.Cancel();
    ///
    /// ── How it works ───────────────────────────────────────────────────────
    ///
    ///   Start()
    ///     → Overlay.EnableHitTest()       — overlay captures mouse
    ///     → CommandLine.SetMode(Measure)
    ///     → CommandLine.SetPrompt("Specify first point:")
    ///     → Subscribes to Overlay mouse events
    ///
    ///   First click
    ///     → Records _pointA (screen)
    ///     → Records _worldA (3D) via viewport unproject
    ///     → CommandLine.SetPrompt("Specify second point:")
    ///     → MouseMove → rubber-band line on Overlay
    ///
    ///   Second click
    ///     → Computes world distance
    ///     → Overlay.ShowMeasureLabel(mid, formattedDistance)
    ///     → Fires MeasureCompleted
    ///     → Resets for next measurement (stays in Measure mode)
    ///
    ///   Cancel()
    ///     → Overlay.ClearMeasure()
    ///     → Overlay.DisableHitTest()
    ///     → CommandLine.ClearAll()
    ///     → Unsubscribes mouse events
    ///
    /// ── Plan-view vs 3D ────────────────────────────────────────────────────
    ///
    ///   In plan view (top-down orthographic) the tool uses the viewport's
    ///   camera scale to convert screen pixels → world metres.
    ///
    ///   In 3D perspective mode the tool unprojects both click points using
    ///   HelixToolkit's FindNearestPoint, then measures the 3D Euclidean
    ///   distance.  If no geometry is hit, it falls back to the ground plane
    ///   (Z = 0) intersection.
    ///
    /// </summary>
    public class MeasureTool
    {
        // ── Attached services ─────────────────────────────────────────────────
        private HelixViewport3D _viewport;
        private ViewportOverlayCanvas _overlay;
        private CommandLineBar _cmdLine;

        // ── Tool state ────────────────────────────────────────────────────────
        private bool _isActive = false;
        private bool _hasPointA = false;
        private Point _screenA;          // screen coordinate of first click
        private Point3D _worldA;          // 3D world coordinate of first click

        // ── Persistent result tracking (for screen-space update on camera move) ──
        private bool _hasResult = false;
        private Point _lastScreenA, _lastScreenB;
        private Point3D _lastWorldA, _lastWorldB;

        // ── OSnap integration ────────────────────────────────────────────────
        private OsnapSystem _osnap;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after the second point is placed.
        /// <para><c>metres</c>  — raw distance in metres.</para>
        /// <para><c>label</c>   — formatted string via UnitSystem.Format().</para>
        /// </summary>
        public event Action<double, string> MeasureCompleted;

        /// <summary>Fired when the tool enters active mode (Start called).</summary>
        public event Action MeasureStarted;

        /// <summary>Fired when the tool exits (Cancel or Escape).</summary>
        public event Action MeasureCancelled;

        /// <summary>Fired on mouse move when a snap is detected. Wire to OsnapIndicator.Update().</summary>
        public event Action<OsnapType?, Point3D?> OsnapSnapped;

        // ══════════════════════════════════════════════════════════════════════
        //  ATTACH
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Attach the HelixViewport3D this tool captures clicks on.</summary>
        public void AttachViewport(HelixViewport3D viewport)
        {
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }

        /// <summary>Attach the transparent overlay canvas for drawing measure geometry.</summary>
        public void AttachOverlay(ViewportOverlayCanvas overlay)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        /// <summary>Attach the command line bar for prompts and results (optional).</summary>
        public void AttachCommandLine(CommandLineBar cmdLine)
        {
            _cmdLine = cmdLine;
        }

        /// <summary>Attach the OSnap system for snapping measure points to geometry.</summary>
        public void AttachOsnap(OsnapSystem osnap)
        {
            _osnap = osnap;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enters measure mode — enables overlay hit-test and subscribes to mouse events.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public void Start()
        {
            if (_viewport == null || _overlay == null) return;
            if (_isActive) return; // already active — idempotent

            _isActive = true;
            _hasPointA = false;

            // Subscribe to VIEWPORT mouse events directly — do NOT use overlay.EnableHitTest()
            // because that puts a transparent layer on top and blocks ZoneDrawingHandler clicks.
            _viewport.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
            _viewport.MouseMove += Overlay_MouseMove;
            _viewport.MouseRightButtonDown += Overlay_MouseRightButtonDown;

            // Update command line
            _cmdLine?.SetMode(CommandMode.Measure);
            _cmdLine?.SetPrompt("Specify first point:");
            _cmdLine?.FocusInput();

            MeasureStarted?.Invoke();
        }

        /// <summary>
        /// Cancels the measure tool — clears overlay, disables hit-test, unsubscribes events.
        /// </summary>
        public void Cancel()
        {
            if (!_isActive) return;

            _isActive = false;
            _hasPointA = false;
            _hasResult = false;

            // Unsubscribe from viewport
            if (_viewport != null)
            {
                _viewport.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
                _viewport.MouseMove -= Overlay_MouseMove;
                _viewport.MouseRightButtonDown -= Overlay_MouseRightButtonDown;
            }

            if (_overlay != null)
            {
                _overlay.ClearMeasure();
            }

            // Reset command line
            _cmdLine?.ClearAll();

            MeasureCancelled?.Invoke();
        }

        /// <summary>Returns true if the tool is currently active.</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Re-projects the last measurement result from 3D world to current screen
        /// coordinates. Call from OnCameraChanged so the measure line stays accurate
        /// when the user pans/zooms/rotates.
        /// </summary>
        public void UpdateScreenPositions()
        {
            // BUG 4 FIX: Don't gate on _hasResult — check whether we have valid world
            // coordinates to re-project. _hasResult was being cleared by right-click,
            // permanently killing re-projection even though visuals were still on screen.
            if (!_isActive || _viewport == null || _overlay == null) return;
            if (!_hasResult) return;  // no completed measurement to update
            try
            {
                var spA = HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(
                    _viewport.Viewport, _lastWorldA);
                var spB = HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(
                    _viewport.Viewport, _lastWorldB);
                if (double.IsNaN(spA.X) || double.IsNaN(spB.X)) return;

                _overlay.ClearMeasure();
                _overlay.DrawMeasureLine(spA, spB);

                var mid = new Point((spA.X + spB.X) / 2.0, (spA.Y + spB.Y) / 2.0);
                double metres = ComputeDistance(_lastWorldA, _lastWorldB);
                _overlay.ShowMeasureLabel(mid, UnitSystem.Format(metres));

                // BUG 4 FIX: Also update stored screen coords so rubber-band stays consistent
                _lastScreenA = spA;
                _lastScreenB = spB;
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MOUSE HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isActive) return;

            Point screenPt = e.GetPosition(_viewport);

            if (!_hasPointA)
            {
                // ── First click: record point A ─────────────────────────────
                // BUG FIX: Clear any previous measure result (line + label) so
                // old "11.20 m" doesn't persist on screen forever.
                _overlay.ClearMeasure();
                _overlay.ClearPreview();
                _hasResult = false;  // BUG 4 FIX: clear previous result when starting new measurement

                _screenA = screenPt;
                _worldA = UnprojectPoint(screenPt);

                // Apply OSnap if available
                // BUG 1 FIX: Snap from the raw unprojected world point — the snap
                // indicator was already showing the correct snapped position during
                // MouseMove, so we must apply the same snap here on click so the
                // actual stored _worldA lands on the snapped geometry.
                if (_osnap != null)
                {
                    var snapped = _osnap.Snap(_worldA, new List<Point3D>(), _viewport);
                    if (snapped.HasValue)
                    {
                        _worldA = snapped.Value;
                        // Update _screenA to the snapped screen position for consistency
                        try
                        {
                            var snappedScreen = Viewport3DHelper.Point3DtoPoint2D(
                                _viewport.Viewport, _worldA);
                            if (!double.IsNaN(snappedScreen.X))
                                _screenA = snappedScreen;
                        }
                        catch { }
                    }
                    OsnapSnapped?.Invoke(_osnap.LastSnapType, _osnap.LastSnapPoint);
                }

                _hasPointA = true;

                // Draw a small dot marker at point A
                _overlay.AddPreviewVertex(screenPt, ZonePreviewType.SiteBoundary);

                _cmdLine?.SetPrompt("Specify second point (or ESC to cancel):");
                _cmdLine?.Log("Point A set.");
            }
            else
            {
                // ── Second click: compute and show result ───────────────────
                Point screenB = screenPt;
                Point3D worldB = UnprojectPoint(screenPt);

                // Apply OSnap if available
                if (_osnap != null)
                {
                    var snapped = _osnap.Snap(worldB, new List<Point3D> { _worldA }, _viewport);
                    if (snapped.HasValue)
                        worldB = snapped.Value;
                    OsnapSnapped?.Invoke(_osnap.LastSnapType, _osnap.LastSnapPoint);
                }

                // BUG 4 FIX: Re-project _worldA to get current screen position
                // (user may have panned between first and second click)
                Point currentScreenA = HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(
                    _viewport.Viewport, _worldA);
                if (double.IsNaN(currentScreenA.X)) currentScreenA = _screenA; // fallback

                double metres = ComputeDistance(_worldA, worldB);
                string label = UnitSystem.Format(metres);

                // Clear the rubber-band, then draw the final measure line
                _overlay.ClearMeasure();
                _overlay.DrawMeasureLine(currentScreenA, screenB);

                // Show the distance label at the midpoint
                var mid = new Point((currentScreenA.X + screenB.X) / 2.0,
                                    (currentScreenA.Y + screenB.Y) / 2.0);
                _overlay.ShowMeasureLabel(mid, label);

                // Store for screen-space update on camera move
                _lastScreenA = currentScreenA;
                _lastScreenB = screenB;
                _lastWorldA = _worldA;
                _lastWorldB = worldB;
                _hasResult = true;

                _cmdLine?.ShowResult($"Distance: {label}");
                _cmdLine?.Log($"Measured: {label}");

                MeasureCompleted?.Invoke(metres, label);

                // Reset for next measurement — keep measure mode active
                _hasPointA = false;
                _overlay.ClearPreview(); // clear vertex dots only; keep measure line
                _cmdLine?.SetPrompt("Specify first point (result above, or ESC to exit):");
            }

            e.Handled = true;
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isActive) return;

            Point cursor = e.GetPosition(_viewport);
            Point3D worldCursor = UnprojectPoint(cursor);

            // ── SNAP: run for BOTH pre-first-click and post-first-click hover ──
            // BUG 1 FIX: Previously this entire method returned early when !_hasPointA,
            // so the OsnapIndicator never fired and the first click always landed on the
            // raw unprojected point with no snap applied.
            if (_osnap != null)
            {
                // Pass _worldA as context only when we already have point A
                var contextPoints = _hasPointA
                    ? new List<Point3D> { _worldA }
                    : new List<Point3D>();

                var snapped = _osnap.Snap(worldCursor, contextPoints, _viewport);
                if (snapped.HasValue)
                {
                    worldCursor = snapped.Value;
                    // Re-project snapped world point back to screen coordinates
                    try
                    {
                        var snappedScreen = Viewport3DHelper.Point3DtoPoint2D(
                            _viewport.Viewport, worldCursor);
                        if (!double.IsNaN(snappedScreen.X))
                            cursor = snappedScreen;
                    }
                    catch { }
                }
                OsnapSnapped?.Invoke(_osnap.LastSnapType, _osnap.LastSnapPoint);
            }

            // ── If waiting for first point: only show snap indicator, no rubber band ──
            if (!_hasPointA)
            {
                // Update command line to show snap hint if any snap is active
                if (_osnap?.LastSnapType != null && _cmdLine != null)
                    _cmdLine.SetPrompt($"Specify first point: [{_osnap.LastSnapType}]");
                return;
            }

            // ── Post-first-click: draw rubber-band line and live distance ──
            // BUG 4 FIX: Re-project _worldA each frame instead of using stale _screenA
            Point currentScreenA = _screenA;
            try
            {
                var projected = HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(
                    _viewport.Viewport, _worldA);
                if (!double.IsNaN(projected.X))
                    currentScreenA = projected;
            }
            catch { }

            _overlay.UpdateMeasureRubberBand(currentScreenA, cursor);

            // Show live distance in command line prompt
            if (_cmdLine != null)
            {
                double liveDist = ComputeDistance(_worldA, worldCursor);
                _cmdLine.SetPrompt($"Specify second point: {UnitSystem.Format(liveDist)}");
            }
        }

        private void Overlay_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click cancels current measurement attempt (but keeps tool active)
            if (_hasPointA)
            {
                _hasPointA = false;
                // BUG 4 FIX: Do NOT clear _hasResult here — the last completed measurement
                // should stay visible and trackable until a new measurement starts or tool
                // is cancelled. _hasResult is cleared in the first-click handler when
                // starting a new measurement, and in Cancel() when exiting the tool.
                _overlay.ClearMeasure();
                _overlay.ClearPreview();
                _cmdLine?.SetPrompt("Specify first point:");
                _cmdLine?.Log("Cancelled. Specify first point.");
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GEOMETRY HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Unprojects a screen point to a 3D world coordinate.
        ///
        /// Strategy:
        ///   1. Try HelixToolkit's FindNearestPoint on existing geometry.
        ///   2. If nothing is hit, intersect with the Z=0 ground plane.
        ///   3. If that also fails (edge of view), return a point directly
        ///      in front of the camera at a fixed depth.
        /// </summary>
        private Point3D UnprojectPoint(Point screenPt)
        {
            if (_viewport == null) return new Point3D(0, 0, 0);

            // Strategy 1: raycast onto existing geometry
            try
            {
                var hitResult = _viewport.FindNearestPoint(screenPt);
                if (hitResult.HasValue)
                    return hitResult.Value;
            }
            catch { /* HelixToolkit may throw if no geometry present */ }

            // Strategy 2: intersect with ground plane Z = 0
            // Uses Viewport3DHelper.Point2DtoPoint3D to get the near/far ray points
            try
            {
                Point3D nearPt, farPt;
                bool ok = HelixToolkit.Wpf.Viewport3DHelper.Point2DtoPoint3D(
                    _viewport.Viewport, screenPt, out nearPt, out farPt);

                if (ok)
                {
                    // Ray direction from near to far
                    var dir = farPt - nearPt;
                    double dirZ = dir.Z;

                    // Intersect with Z = 0 plane
                    // nearPt.Z + t * dirZ = 0  →  t = -nearPt.Z / dirZ
                    if (Math.Abs(dirZ) > 1e-9)
                    {
                        double t = -nearPt.Z / dirZ;
                        if (t > 0)
                            return nearPt + t * dir;
                    }
                }
            }
            catch { }

            // Strategy 3: fixed depth fallback
            return new Point3D(screenPt.X * 0.01, screenPt.Y * 0.01, 0);
        }

        /// <summary>Returns the Euclidean 3D distance in metres between two world points.</summary>
        private static double ComputeDistance(Point3D a, Point3D b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double dz = b.Z - a.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}