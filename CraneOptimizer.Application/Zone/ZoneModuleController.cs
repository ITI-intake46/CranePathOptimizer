// FILE: ZoneModule/ZoneModuleController.cs  (UPDATED)
// CHANGES vs original:
//   + UndoRedoManager injected into Handler
//   + Public UndoManager property for QAT toolbar binding
//   + Wire DrawingOptionsBar to Handler events
//   + Wire RevitDrawPanel to Handler
//   + Wire UndoRedoToolbar UndoActionRequested / RedoActionRequested
//   + OsnapIndicator overlay canvas support
//   + Intersection and Perpendicular snaps now available
//   + All original functionality preserved

using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WPF_IFC_test1.ZoneModule.DXF;
using WPF_IFC_test1.ZoneModule.Drawing;
using WPF_IFC_test1.ZoneModule.Models;
using WPF_IFC_test1.ZoneModule.Persistence;
using WPF_IFC_test1.ZoneModule.Rendering;
using WPF_IFC_test1.ZoneModule.UI;


namespace WPF_IFC_test1.ZoneModule
{
    public class ZoneModuleController
    {
        // ── Core components ───────────────────────────────────────────────────
        public ZoneManager Manager { get; }
        public ZoneDrawingHandler Handler { get; }
        public UndoRedoManager UndoMgr { get; }

        private readonly ZoneRenderer _renderer;
        private readonly GridRenderer _grid;
        private readonly DxfUploader _dxf;
        private readonly HelixViewport3D _viewport;
        private readonly System.Windows.Controls.TextBlock _statusBar;

        // ── Optional UI panels (set by caller) ────────────────────────────────
        private DrawingOptionsBar _optionsBar;
        private RevitDrawPanel _drawPanel;
        private UndoRedoToolbar _undoRedoToolbar;

        // ── Camera state ──────────────────────────────────────────────────────
        private Point3D _savedCamPos;
        private Vector3D _savedCamLook, _savedCamUp;
        private double _savedCamFov;
        private bool _camWasPerspective;
        private bool _inPlanView;
        private double _extent = 200.0;

        /// <summary>
        /// Called whenever EnterPlanView or ReturnTo3DView replaces the camera object.
        /// Subscriber (MainWindow) should re-wire OnCameraChanged to the new camera.
        /// </summary>
        public Action<System.Windows.Media.Media3D.Camera> OnCameraReplaced { get; set; }

        private CraneZone _selectedZone;

        // ── Constructor ───────────────────────────────────────────────────────

        public ZoneModuleController(
            HelixViewport3D viewport,
            System.Windows.Controls.TextBlock statusBar,
            ZoneModuleListPanel zoneListPanel = null)
        {
            _viewport = viewport;
            _statusBar = statusBar;

            UndoMgr = new UndoRedoManager();
            Manager = new ZoneManager();
            _renderer = new ZoneRenderer(viewport);
            _grid = new GridRenderer(viewport);
            _dxf = new DxfUploader(viewport);
            Handler = new ZoneDrawingHandler(viewport, Manager, _renderer, UndoMgr);

            Manager.ZonesChanged += OnZonesChanged;
            Handler.StatusChanged += msg => _statusBar.Text = msg;
            Handler.CursorWorldPositionChanged += (x, y)
                => _statusBar.Text = $"X: {x:F2}m  Y: {y:F2}m | {Handler.ActiveMode}";

            zoneListPanel?.Initialize(Manager);

            _grid.Show(1.0);
        }

        // ── Panel wiring (call after constructor) ─────────────────────────────

        public void AttachDrawPanel(RevitDrawPanel panel)
        {
            _drawPanel = panel;
            panel.Initialize(Handler);
            panel.OsnapToggled += (type, enabled)
                => Handler.Osnap.SetSnapEnabled(type, enabled);
        }

        public void AttachOptionsBar(DrawingOptionsBar bar)
        {
            _optionsBar = bar;
            Handler.OptionsBar = bar;

            Handler.DrawingStarted += (zt, tool) =>
                bar.Dispatcher.Invoke(() => bar.ShowForDraw(zt, tool));

            Handler.DrawingEnded += () =>
                bar.Dispatcher.Invoke(() => bar.HideBar());

            bar.FinishRequested += () => Handler.TryFinalizePolygon();
            bar.CancelRequested += () => Handler.CancelDraw();
            bar.OpacityChanged += opacity => _dxf.Opacity = opacity / 100.0;
        }

        public void AttachUndoRedoToolbar(UndoRedoToolbar toolbar)
        {
            _undoRedoToolbar = toolbar;
            toolbar.Initialize(UndoMgr);
            toolbar.UndoActionRequested += () => Handler.Undo();
            toolbar.RedoActionRequested += () => Handler.Redo();
        }

        // ── Action dispatcher ─────────────────────────────────────────────────

        public void HandleZoneAction(string action, object parameter = null)
        {
            switch (action)
            {
                case "DrawSiteBoundary": Handler.StartDraw(ZoneType.SiteBoundary); break;
                case "DrawAllowed": Handler.StartDraw(ZoneType.Allowed); break;
                case "DrawForbidden": Handler.StartDraw(ZoneType.Forbidden); break;

                case "SetToolPolyline": Handler.SetTool(ShapeTool.Polyline); break;
                case "SetToolRectangle": Handler.SetTool(ShapeTool.Rectangle); break;
                case "SetToolCircle": Handler.SetTool(ShapeTool.Circle); break;
                case "SetToolArc": Handler.SetTool(ShapeTool.Arc); break;

                case "SetModeDimension": Handler.SetMode(DrawMode.Dimension); break;
                case "SetModeSelect": Handler.SetMode(DrawMode.Select); break;

                case "FinishZone": Handler.TryFinalizePolygon(); break;
                case "UndoVertex": Handler.UndoLastVertex(); break;
                case "Undo": Handler.Undo(); break;
                case "Redo": Handler.Redo(); break;
                case "CancelDraw": Handler.CancelDraw(); break;

                case "DeleteZone": DeleteSelectedZone(); break;
                case "DeleteAllZones": DeleteAllZones(); break;
                case "ExportZones": ExportZones(); break;

                case "LoadDxf": LoadDxf(); break;
                case "AlignDxf": AlignDxf(); break;
                case "ToggleDxf": ToggleDxfVisibility(); break;
                case "ToggleGrid": ToggleGrid(); break;
                case "ToggleFlatZones": ToggleFlatMode(); break;
                case "SetGridSpacing" when parameter is double s:
                    SetGridSpacing(s); break;

                case "ConfirmRectangle" when parameter is WPF_IFC_test1.ZoneModule.Drawing.RectangleShapeData rd:
                    ConfirmShapeAsZone(rd.Corners, rd.ZoneType); break;

                case "ConfirmCircle" when parameter is WPF_IFC_test1.ZoneModule.Drawing.CircleShapeData cd:
                    ConfirmShapeAsZone(BuildCirclePolygon(cd.Center, cd.Radius), cd.ZoneType); break;

                case "ConfirmArc" when parameter is WPF_IFC_test1.ZoneModule.Drawing.ArcShapeData ad:
                    ConfirmShapeAsZone(BuildArcPolygon(ad), ad.ZoneType); break;

                case "ConfirmPickObjects" when parameter is WPF_IFC_test1.ZoneModule.Drawing.PickObjectsShapeData pd:
                    ConfirmShapeAsZone(pd.Vertices, pd.ZoneType); break;
            }
        }

        public void HandleKeyDown(KeyEventArgs e) => Handler.HandleKeyDown(e);

        // ── Plan View ─────────────────────────────────────────────────────────

        public void EnterPlanView()
        {
            if (_inPlanView) return;

            if (_viewport.Camera is PerspectiveCamera persp)
            {
                _savedCamPos = persp.Position;
                _savedCamLook = persp.LookDirection;
                _savedCamUp = persp.UpDirection;
                _savedCamFov = persp.FieldOfView;
                _camWasPerspective = true;
            }
            else if (_viewport.Camera is OrthographicCamera orth)
            {
                _savedCamPos = orth.Position;
                _savedCamLook = orth.LookDirection;
                _savedCamUp = orth.UpDirection;
                _camWasPerspective = false;
            }

            _inPlanView = true;
            _viewport.IsRotationEnabled = false;

            ComputeSceneAabb(out Point3D sceneCenter, out double sceneSize);
            _extent = Math.Max(sceneSize, 100.0);

            double elevation = sceneCenter.Z + Math.Max(_extent * 1.5, 500.0);
            var targetPos = new Point3D(sceneCenter.X, sceneCenter.Y, elevation);
            var targetLook = new Vector3D(0, 0, -1);
            var targetUp = new Vector3D(0, 1, 0);

            AnimateCameraTo(targetPos, targetLook, targetUp, durationMs: 400, onComplete: () =>
            {
                // FIX: Use scene XY span (not diagonal) for ortho width with 40% margin
                // Do NOT call ZoomExtents — it fits to all visuals INCLUDING the grid,
                // which causes the view to zoom out to show the entire grid.
                double orthoWidth = _extent * 2.8;
                _viewport.Camera = new OrthographicCamera
                {
                    Position = targetPos,
                    LookDirection = targetLook,
                    UpDirection = targetUp,
                    Width = orthoWidth,
                    NearPlaneDistance = 0.001,
                    FarPlaneDistance = 1_000_000
                };
                OnCameraReplaced?.Invoke(_viewport.Camera);
                _grid.Show(1.0);
                _statusBar.Text = "📐 Plan View — Zones tab to draw";
            });
        }

        public void ReturnTo3DView()
        {
            Handler.CancelDraw();
            _inPlanView = false;
            _viewport.IsRotationEnabled = true;

            if (_camWasPerspective)
            {
                _viewport.Camera = new PerspectiveCamera
                {
                    Position = _savedCamPos,
                    LookDirection = _savedCamLook,
                    UpDirection = _savedCamUp,
                    FieldOfView = _savedCamFov > 0 ? _savedCamFov : 45
                };
                OnCameraReplaced?.Invoke(_viewport.Camera);
            }

            _statusBar.Text = "3D View";
        }

        /// <summary>Detach all static event subscriptions. Call on window close.</summary>
        public void Detach()
        {
            _grid.Detach();
        }

        private void AnimateCameraTo(
            Point3D targetPos, Vector3D targetLook, Vector3D targetUp,
            int durationMs, Action onComplete)
        {
            Point3D startPos;
            Vector3D startLook, startUp;

            if (_viewport.Camera is PerspectiveCamera pc)
            { startPos = pc.Position; startLook = pc.LookDirection; startUp = pc.UpDirection; }
            else { onComplete?.Invoke(); return; }

            var start = DateTime.UtcNow;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

            timer.Tick += (s, e) =>
            {
                double t = Math.Min((DateTime.UtcNow - start).TotalMilliseconds / durationMs, 1.0);
                double smooth = t * t * (3 - 2 * t);

                if (_viewport.Camera is PerspectiveCamera ac)
                {
                    ac.Position = new Point3D(
                        Lerp(startPos.X, targetPos.X, smooth),
                        Lerp(startPos.Y, targetPos.Y, smooth),
                        Lerp(startPos.Z, targetPos.Z, smooth));
                    ac.LookDirection = new Vector3D(
                        Lerp(startLook.X, targetLook.X, smooth),
                        Lerp(startLook.Y, targetLook.Y, smooth),
                        Lerp(startLook.Z, targetLook.Z, smooth));
                    ac.UpDirection = new Vector3D(
                        Lerp(startUp.X, targetUp.X, smooth),
                        Lerp(startUp.Y, targetUp.Y, smooth),
                        Lerp(startUp.Z, targetUp.Z, smooth));
                }

                if (t >= 1.0) { timer.Stop(); onComplete?.Invoke(); }
            };
            timer.Start();
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // ── DXF & Grid ────────────────────────────────────────────────────────

        public void LoadDxf()
        {
            var dlg = new OpenFileDialog { Filter = "DXF files (*.dxf)|*.dxf" };
            if (dlg.ShowDialog() != true) return;

            _statusBar.Text = $"Loading DXF: {Path.GetFileName(dlg.FileName)}...";
            bool success;
            try { success = _dxf.LoadDxf(dlg.FileName); }
            catch (Exception ex) { ShowDxfError(ex.Message); return; }

            if (success)
            {
                Handler.Osnap.DxfSnapPoints.Clear();
                Handler.Osnap.DxfSnapPoints.AddRange(_dxf.SnapPoints);
                Handler.SetMode(DrawMode.Select);
                _statusBar.Text = $"✓ DXF loaded: {Path.GetFileName(dlg.FileName)} ({_dxf.SnapPoints.Count} snap points)";

                if (_dxf.SnapPoints.Count == 0 && !string.IsNullOrEmpty(_dxf.LastLoadDiagnostic))
                    MessageBox.Show(_dxf.LastLoadDiagnostic, "DXF Loaded — No Geometry",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                AlignDxf();
            }
            else
            {
                ShowDxfError(_dxf.LastLoadDiagnostic);
            }
        }

        private void ShowDxfError(string detail)
        {
            _statusBar.Text = "⚠ DXF load failed";
            MessageBox.Show(
                $"Could not load DXF file.\n\n{detail}\n\n" +
                "Supported: AutoCAD 2010+ (AC1024+)\n" +
                "Supported entities: Line, Polyline, Circle, Arc, Spline\n\n" +
                "To fix: Save As → AutoCAD 2010 DXF in AutoCAD/QCAD.",
                "DXF Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void AlignDxf()
        {
            if (!_dxf.IsLoaded) { _statusBar.Text = "⚠ Load a DXF first."; return; }
            var dialog = new DxfAlignmentDialog(_dxf);
            try
            {
                foreach (Window w in Application.Current.Windows)
                    if (w is MainWindow mainWin && mainWin.IsLoaded && mainWin.IsVisible)
                    { dialog.Owner = mainWin; break; }
            }
            catch { }
            dialog.ShowDialog();
        }

        public void ToggleDxfVisibility() => _dxf.ToggleVisibility();
        public void ToggleGrid() => _grid.Toggle();

        public void UpdateGridForCamera(System.Windows.Media.Media3D.Point3D camPos, double camDist)
            => _grid.UpdateForCamera(camPos, camDist);

        public void UpdateGridForOrthographicCamera(System.Windows.Media.Media3D.Point3D camPos, double orthoWidth)
            => _grid.UpdateForCamera(camPos, orthoWidth * 0.6);

        public void AttachGridOverlay(ViewportOverlayCanvas overlay)
            => _grid.AttachOverlay(overlay);

        public void ToggleFlatMode()
        {
            _renderer.FlatMode = !_renderer.FlatMode;
            foreach (var zone in Manager.Zones) _renderer.RenderZone(zone);
            if (Manager.SiteBoundary != null) _renderer.RenderZone(Manager.SiteBoundary);
        }

        public void SetGridSpacing(double meters) => _grid.SetSpacing(meters);

        // ── Project save/load ─────────────────────────────────────────────────

        public void SaveProject(string filePath)
        {
            var data = ProjectPersistence.FromManager(
                Manager, _dxf.LoadedFilePath, MatrixToArray(_dxf.AlignmentTransform));
            ProjectPersistence.Save(filePath, data);
            _statusBar.Text = $"✓ Saved: {Path.GetFileName(filePath)}";
        }

        public void LoadProject(string filePath)
        {
            var data = ProjectPersistence.Load(filePath);
            ProjectPersistence.RestoreToManager(data, Manager);
            _renderer.Clear();
            _renderer.ClearDimensions();
            foreach (var zone in Manager.Zones) _renderer.RenderZone(zone);
            foreach (var ann in Manager.Annotations) _renderer.RenderDimension(ann);

            if (!string.IsNullOrEmpty(data.DxfFilePath) && File.Exists(data.DxfFilePath))
            {
                if (data.AlignmentMatrix != null)
                    _dxf.AlignmentTransform = ArrayToMatrix(data.AlignmentMatrix);
                _dxf.LoadDxf(data.DxfFilePath);
                Handler.Osnap.DxfSnapPoints.Clear();
                Handler.Osnap.DxfSnapPoints.AddRange(_dxf.SnapPoints);
            }

            UndoMgr.Clear();
            _statusBar.Text = $"✓ Project loaded — {Manager.Zones.Count} zones";
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void DeleteSelectedZone()
        {
            _selectedZone = Manager.SelectedZone;
            if (_selectedZone == null) return;
            if (MessageBox.Show($"Delete zone '{_selectedZone.DisplayLabel}'?", "Confirm",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _renderer.RemoveZone(_selectedZone);
            Manager.RemoveZone(_selectedZone);
            _selectedZone = null;
        }

        private void DeleteAllZones()
        {
            if (MessageBox.Show("Delete ALL zones?", "Confirm",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _renderer.Clear();
            _renderer.ClearDimensions();
            Manager.RemoveAll();
            UndoMgr.Clear();
        }

        private void ExportZones()
        {
            var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "zones.json" };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, Manager.ExportToJson());
            _statusBar.Text = "✓ Zones exported";
        }

        private void OnZonesChanged()
        {
            foreach (var zone in Manager.Zones)
                _renderer.UpdateZoneVisibility(zone);

            // BUG 3 FIX: Removed ZoomExtents(200) — it includes grid geometry and
            // causes the view to zoom out excessively each time zones change.
            _viewport.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { _viewport.InvalidateVisual(); } catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ConfirmShapeAsZone(
            System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D> vertices,
            WPF_IFC_test1.ZoneModule.UI.ZonePreviewType previewType)
        {
            if (vertices == null || vertices.Count < 3) return;

            double area = CalculatePolygonArea(vertices);

            var zoneType = previewType switch
            {
                WPF_IFC_test1.ZoneModule.UI.ZonePreviewType.Forbidden => ZoneType.Forbidden,
                WPF_IFC_test1.ZoneModule.UI.ZonePreviewType.SiteBoundary => ZoneType.SiteBoundary,
                _ => ZoneType.Allowed
            };

            var dialog = new WPF_IFC_test1.ZoneModule.UI.ZoneFinalizationDialog(zoneType, area);

            // BUG FIX: Owner assignment can crash with InvalidOperationException
            // if MainWindow hasn't been shown yet or is in an invalid state.
            // Use safe lookup instead.
            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (w is MainWindow mainWin && mainWin.IsLoaded && mainWin.IsVisible)
                    {
                        dialog.Owner = mainWin;
                        break;
                    }
                }
            }
            catch { /* Owner assignment failed — dialog will still work without it */ }

            bool confirmed = dialog.ShowDialog() == true;

            _viewport.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try { _viewport.InvalidateVisual(); } catch { }
            }));

            if (!confirmed) return;

            var zone = new WPF_IFC_test1.ZoneModule.Models.CraneZone
            {
                Vertices = new System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D>(vertices),
                ZoneType = dialog.SelectedZoneType,
                MinHeight = dialog.MinHeight,
                MaxHeight = dialog.MaxHeight,
                Name = dialog.ZoneName
            };

            try
            {
                if (zone.ZoneType == ZoneType.SiteBoundary)
                    Manager.SetSiteBoundary(zone.Vertices);
                else
                    Manager.AddZone(zone);

                _renderer.RenderZone(zone);

                string undoLabel = zone.ZoneType == ZoneType.SiteBoundary
                    ? "Draw Site Boundary"
                    : $"Draw {zone.ZoneType} zone";

                UndoMgr?.Push(undoLabel,
                    new WPF_IFC_test1.ZoneModule.Drawing.UndoAction
                    {
                        Type = WPF_IFC_test1.ZoneModule.Drawing.UndoType.AddZone,
                        Zone = zone
                    });

                _statusBar.Text = $"✅ {zone.DisplayLabel} created.";
            }
            catch (Exception ex)
            {
                _statusBar.Text = $"⚠ {ex.Message}";
            }
        }

        private static double CalculatePolygonArea(
            System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D> pts)
        {
            double area = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
                area += pts[i].X * pts[(i + 1) % n].Y - pts[(i + 1) % n].X * pts[i].Y;
            return Math.Abs(area) / 2.0;
        }

        private static System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D>
            BuildCirclePolygon(System.Windows.Media.Media3D.Point3D center, double radius)
        {
            const int segments = 36;
            var pts = new System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D>();
            for (int i = 0; i < segments; i++)
            {
                double angle = i * Math.PI * 2.0 / segments;
                pts.Add(new System.Windows.Media.Media3D.Point3D(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle), 0));
            }
            return pts;
        }

        private static System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D>
            BuildArcPolygon(WPF_IFC_test1.ZoneModule.Drawing.ArcShapeData arc)
        {
            const int segments = 48;
            var pts = new System.Collections.Generic.List<System.Windows.Media.Media3D.Point3D>();

            double sa = arc.StartAngle * Math.PI / 180.0;
            double ea = arc.EndAngle * Math.PI / 180.0;

            while (ea < sa) ea += Math.PI * 2;
            if (!arc.IsMinorArc) ea -= Math.PI * 2;

            pts.Add(arc.Center);
            for (int i = 0; i <= segments; i++)
            {
                double t = sa + (ea - sa) * i / segments;
                pts.Add(new System.Windows.Media.Media3D.Point3D(
                    arc.Center.X + arc.Radius * Math.Cos(t),
                    arc.Center.Y + arc.Radius * Math.Sin(t), 0));
            }
            return pts;
        }

        private void ComputeSceneAabb(out Point3D center, out double halfSize)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool any = false;

            void Expand(double x, double y)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                any = true;
            }

            // Include zones
            if (Manager?.Zones != null)
                foreach (var z in Manager.Zones)
                    foreach (var v in z.Vertices)
                        Expand(v.X, v.Y);

            // FIX: Include ALL 3D geometry in the viewport (IFC models etc.)
            // This is critical — without this, plan view has no idea where the building is.
            // BUG 3 FIX: Skip LinesVisual3D (grid lines) and SphereVisual3D (vertex markers)
            // to prevent the grid from inflating the scene bounds → feedback loop.
            foreach (var child in _viewport.Children)
            {
                if (child is HelixToolkit.Wpf.LinesVisual3D) continue;
                if (child is HelixToolkit.Wpf.SphereVisual3D) continue;

                if (child is ModelVisual3D mv && mv.Content != null)
                {
                    try
                    {
                        var bounds = mv.Content.Bounds;
                        if (!bounds.IsEmpty && bounds.SizeX > 0.01)
                        {
                            Expand(bounds.X, bounds.Y);
                            Expand(bounds.X + bounds.SizeX, bounds.Y + bounds.SizeY);
                        }
                    }
                    catch { }
                }
            }

            if (!any || (maxX - minX) < 1.0)
            {
                center = new Point3D(0, 0, 0);
                halfSize = 100.0;
                return;
            }

            center = new Point3D((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);
            halfSize = Math.Max(maxX - minX, maxY - minY) / 2.0;
        }

        private double[][] MatrixToArray(Matrix3D m) => new[]
        {
            new[] { m.M11, m.M12, m.M13, m.M14 },
            new[] { m.M21, m.M22, m.M23, m.M24 },
            new[] { m.M31, m.M32, m.M33, m.M34 },
            new[] { m.OffsetX, m.OffsetY, m.OffsetZ, m.M44 }
        };

        private Matrix3D ArrayToMatrix(double[][] a) => new(
            a[0][0], a[0][1], a[0][2], a[0][3],
            a[1][0], a[1][1], a[1][2], a[1][3],
            a[2][0], a[2][1], a[2][2], a[2][3],
            a[3][0], a[3][1], a[3][2], a[3][3]);
    }
}