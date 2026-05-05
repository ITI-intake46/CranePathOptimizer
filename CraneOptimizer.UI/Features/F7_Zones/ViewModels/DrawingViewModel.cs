using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F7_Zones.Views;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public enum DrawingMode { None, Allowed, Forbidden }

    public class DrawingViewModel : ViewModelBase
    {
        // ── Services ─────────────────────────────────────────────────────────
        private readonly DrawingService _drawing;
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly ZoneListViewModel _zoneList;

        // ── Timers ───────────────────────────────────────────────────────────
        private readonly DispatcherTimer _inlineWarningTimer;
        private readonly DispatcherTimer _escConfirmTimer;
        private readonly DispatcherTimer _deleteAllConfirmTimer;

        // ── Drawing state ────────────────────────────────────────────────────
        private DomainPoint3D? _currentHover;
        private bool _escPendingConfirmation;
        private bool _deleteAllPendingConfirmation;
        private ZoneType _currentType = ZoneType.Allowed;

        // ── Measurement state ────────────────────────────────────────────────
        private readonly List<DomainPoint3D> _measurePoints = new();
        private readonly List<MeasurementVisualData> _measureSegments = new();
        private DomainPoint3D? _measureHoverPoint;
        private double _measureTotalDistance;

        // ── Snap ─────────────────────────────────────────────────────────────
        private readonly SnapSystem _snap = new();
        private bool _isGridEnabled; // off by default; point snap + ortho always active

        // ── Layer names ──────────────────────────────────────────────────────
        private const string PreviewLayer = "Preview";
        private const string MeasurePreviewLayer = "MeasurePreview";
        private const string MeasureFinalLayer = "MeasureFinal";
        private const string SnapIndicatorLayer = "SnapIndicator";

        // ── Geometry constants ───────────────────────────────────────────────
        private const double PointEqualityTolerance = 0.001;
        private const double SnapToFirstThresholdPercent = 0.005;

        // ════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════
        public DrawingViewModel(
            DrawingService drawing,
            ISceneService scene,
            IRenderingService render,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest)
        {
            _drawing = drawing;
            _scene = scene;
            _render = render;
            _interaction = interaction;
            _hitTest = hitTest;
            _zoneList = new ZoneListViewModel(scene);

            // Commands
            StartAllowedCommand = new RelayCommand(StartAllowed);
            StartForbiddenCommand = new RelayCommand(StartForbidden);
            FinishCommand = new RelayCommand(Finish, CanFinish);
            CancelCommand = new RelayCommand(Cancel, () => IsDrawing);
            UndoCommand = new RelayCommand(Undo, CanUndo);
            StartMeasureCommand = new RelayCommand(StartMeasure, CanStartMeasure);
            DeleteSelectedZoneCommand = new RelayCommand(DeleteSelectedZone, CanDeleteSelectedZone);
            DeleteZoneCommand = DeleteSelectedZoneCommand;
            DeleteAllCommand = new RelayCommand(DeleteAllZones, CanDeleteAllZones);

            // Timers
            _inlineWarningTimer = new DispatcherTimer();
            _inlineWarningTimer.Tick += InlineWarningTimer_Tick;

            _escConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _escConfirmTimer.Tick += EscConfirmTimer_Tick;

            _deleteAllConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _deleteAllConfirmTimer.Tick += DeleteAllConfirmTimer_Tick;

            RefreshHintText();
        }

        // ════════════════════════════════════════════════════════════════════
        // Bindable properties
        // ════════════════════════════════════════════════════════════════════

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            private set
            {
                _isDrawing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHintVisible));
                ViewportCursor = _isDrawing ? Cursors.Cross : Cursors.Arrow;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int PointCount => _drawing.GetPoints().Count;
        public bool IsHintVisible => IsDrawing;

        private string _hintText = string.Empty;
        public string HintText
        {
            get => _hintText;
            private set { _hintText = value; OnPropertyChanged(); }
        }

        private string _inlineWarning = string.Empty;
        public string InlineWarning
        {
            get => _inlineWarning;
            private set { _inlineWarning = value; OnPropertyChanged(); }
        }

        private bool _isInlineWarningVisible;
        public bool IsInlineWarningVisible
        {
            get => _isInlineWarningVisible;
            private set { _isInlineWarningVisible = value; OnPropertyChanged(); }
        }

        private Cursor _viewportCursor = Cursors.Arrow;
        public Cursor ViewportCursor
        {
            get => _viewportCursor;
            private set { _viewportCursor = value; OnPropertyChanged(); }
        }

        private DrawingMode _activeDrawingMode = DrawingMode.None;
        public DrawingMode ActiveDrawingMode
        {
            get => _activeDrawingMode;
            private set { _activeDrawingMode = value; OnPropertyChanged(); }
        }

        private bool _isMeasuring;
        public bool IsMeasuring
        {
            get => _isMeasuring;
            private set
            {
                _isMeasuring = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMeasureOverlayVisible));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _measureText = "📏 Click first point to start measuring";
        public string MeasureText
        {
            get => _measureText;
            private set { _measureText = value; OnPropertyChanged(); }
        }

        public bool IsMeasureOverlayVisible => IsMeasuring;

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand StartAllowedCommand { get; }
        public ICommand StartForbiddenCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand StartMeasureCommand { get; }
        public ICommand DeleteZoneCommand { get; }
        public ICommand DeleteSelectedZoneCommand { get; }
        public ICommand DeleteAllCommand { get; }

        public ZoneListViewModel ZoneList => _zoneList;

        // ════════════════════════════════════════════════════════════════════
        // Public API — called from code-behind (thin forwarding only)
        // ════════════════════════════════════════════════════════════════════

        // ── Internal SHIFT detection ──────────────────────────────────────────
        // Read directly from Keyboard so neither MainWindow nor the tab view need
        // to be changed to pass this state.  This is safe on the UI thread.
        private static bool IsShiftDown =>
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        /// <summary>Entry point for left-click from the viewport.</summary>
        public void HandleViewportLeftClick(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            bool shift = shiftDown || IsShiftDown;
            if (IsDrawing) { AddPoint(rawPoint, shift); return; }
            if (IsMeasuring) { HandleMeasureClick(rawPoint, shift); }
        }

        /// <summary>Entry point for right-click from the viewport.</summary>
        public void HandleRightClick()
        {
            if (IsMeasuring) { ResetMeasurement(); return; }
            if (IsDrawing) { Finish(); }
        }

        /// <summary>Entry point for double-click (left) from the viewport.</summary>
        public void HandleDoubleClick() => Finish();

        /// <summary>
        /// Update the rubber-band preview while the cursor moves during drawing
        /// AND handle measure hover — DrawingInteractionMode calls this for all MouseMove events,
        /// so both modes are served from the same entry point.
        /// </summary>
        public void SetHoverPoint(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            bool shift = shiftDown || IsShiftDown;

            if (IsDrawing)
            {
                _snap.UpdateScale(GetSceneSize());
                var snap = _snap.Snap(rawPoint, GetAllSnapTargets(), LastDrawnPoint(), shift, _isGridEnabled);
                _currentHover = snap.Point;
                RenderPreview();
                RenderSnapIndicator(snap);
            }

            if (IsMeasuring)
            {
                SetMeasureHoverPoint(rawPoint, shift);
            }
        }

        /// <summary>Update the measurement preview line while cursor moves during measuring.</summary>
        public void SetMeasureHoverPoint(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            if (!IsMeasuring) return;

            bool shift = shiftDown || IsShiftDown;
            _snap.UpdateScale(GetSceneSize());
            var lastPt = _measurePoints.Count > 0 ? _measurePoints[^1] : null;
            var snap = _snap.Snap(rawPoint, GetAllSnapTargets(), lastPt, shift, _isGridEnabled);

            _measureHoverPoint = snap.Point;
            RenderMeasurePreview();
            if (!IsDrawing) RenderSnapIndicator(snap); // avoid double-render when both active
        }

        /// <summary>Key-down forwarded from PreviewKeyDown in the View.</summary>
        public void HandleKeyDown(KeyEventArgs e)
        {
            // ── Delete key (not drawing) ──────────────────────────────────
            if (e.Key == Key.Delete && !IsDrawing && !IsMeasuring)
            {
                if (DeleteSelectedZoneCommand.CanExecute(null))
                {
                    DeleteSelectedZoneCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // ── ESC while measuring ───────────────────────────────────────
            if (e.Key == Key.Escape && IsMeasuring)
            {
                StopMeasureMode();
                e.Handled = true;
                return;
            }

            // ── C while measuring → reset segments ────────────────────────
            if (e.Key == Key.C && IsMeasuring)
            {
                ResetMeasurement();
                e.Handled = true;
                return;
            }

            // ── Nothing else applies outside drawing ──────────────────────
            if (!IsDrawing) return;

            if (e.Key == Key.Escape)
            {
                HandleEscape();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                UndoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C)
            {
                Finish();
                e.Handled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Drawing — core methods
        // ════════════════════════════════════════════════════════════════════

        public void Start()
        {
            StopMeasureMode();
            _interaction.PushMode(new Interaction.DrawingInteractionMode(this, _hitTest, _interaction));
            _drawing.Start();
            _currentHover = null;
            _render.ClearLayer(PreviewLayer);
            _render.ClearLayer(SnapIndicatorLayer);
            IsDrawing = true;
            _escPendingConfirmation = false;
            _escConfirmTimer.Stop();
            HideInlineWarning();
            RefreshHintText();
            OnPropertyChanged(nameof(PointCount));
            CommandManager.InvalidateRequerySuggested();
        }

        private void StartAllowed()
        {
            _currentType = ZoneType.Allowed;
            ActiveDrawingMode = DrawingMode.Allowed;
            Start();
        }

        private void StartForbidden()
        {
            _currentType = ZoneType.Forbidden;
            ActiveDrawingMode = DrawingMode.Forbidden;
            Start();
        }

        private void AddPoint(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            if (!IsDrawing) return;

            _snap.UpdateScale(GetSceneSize());
            var lastPt = LastDrawnPoint();
            var snap = _snap.Snap(rawPoint, GetAllSnapTargets(), lastPt, shiftDown, _isGridEnabled);
            var point = snap.Point;

            if (!_drawing.TryAddPoint(point))
                return;

            _currentHover = null;
            _render.ClearLayer(SnapIndicatorLayer);
            OnPointCollectionChanged();
            RenderPreview();
        }

        public void Finish()
        {
            if (!IsDrawing) return;
            if (PointCount < 3)
            {
                ShowInlineWarning("⚠ At least 3 points required to create a zone", TimeSpan.FromSeconds(3));
                return;
            }

            var rawPoints = _drawing.GetPoints();
            var polygonPoints = rawPoints.Select(p => new DomainPoint3D(p.X, p.Y, p.Z)).ToList();
            EnsureClosed(polygonPoints);
            var areaM2 = CalculatePolygonArea(polygonPoints);

            var dialog = new ZoneFinalizationDialog(
                areaM2,
                _currentType == ZoneType.Allowed ? "Allowed" : "Forbidden")
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                Cancel();
                return;
            }

            rawPoints.Clear();
            rawPoints.AddRange(polygonPoints);
            var zone = _drawing.BuildZone();
            if (zone != null)
            {
                zone.Type = string.Equals(dialog.ZoneType, "Forbidden",
                                StringComparison.OrdinalIgnoreCase)
                            ? ZoneType.Forbidden
                            : ZoneType.Allowed;
                zone.Area = areaM2;
                zone.Name = string.IsNullOrWhiteSpace(dialog.ZoneName)
                            ? $"{zone.Type} Zone {_scene.GetZones().Count + 1}"
                            : dialog.ZoneName;
                _scene.AddZone(zone);
                _zoneList.AddOrUpdateFromZone(zone, dialog.MinHeightValue, dialog.MaxHeightValue);
            }

            EndDrawingSession();
        }

        public void Cancel()
        {
            if (!IsDrawing) return;
            _currentHover = null;
            _escPendingConfirmation = false;
            _escConfirmTimer.Stop();
            EndDrawingSession();
        }

        public void Undo()
        {
            if (!IsDrawing) return;
            var pts = _drawing.GetPoints();
            if (pts.Count == 0)
            {
                ShowInlineWarning("⚠ Nothing to undo", TimeSpan.FromSeconds(2));
                return;
            }
            pts.RemoveAt(pts.Count - 1);
            OnPointCollectionChanged();
            RenderPreview();
        }

        // ════════════════════════════════════════════════════════════════════
        // Measure — multi-segment
        // ════════════════════════════════════════════════════════════════════

        private void StartMeasure()
        {
            if (IsDrawing) Cancel();

            _interaction.PushMode(new Interaction.DrawingInteractionMode(this, _hitTest, _interaction));
            IsMeasuring = true;
            ResetMeasurement();
        }

        private void StopMeasureMode()
        {
            if (IsMeasuring) _interaction.PopMode();
            IsMeasuring = false;
            ResetMeasurement();
        }

        public void ResetMeasurement()
        {
            _measurePoints.Clear();
            _measureSegments.Clear();
            _measureTotalDistance = 0.0;
            _measureHoverPoint = null;
            MeasureText = "📏 Click first point to start measuring";
            _render.ClearLayer(MeasurePreviewLayer);
            _render.ClearLayer(MeasureFinalLayer);
            _render.ClearLayer(SnapIndicatorLayer);
        }

        private void HandleMeasureClick(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            _snap.UpdateScale(GetSceneSize());
            var lastPt = _measurePoints.Count > 0 ? _measurePoints[^1] : null;
            var snap = _snap.Snap(rawPoint, GetAllSnapTargets(), lastPt, shiftDown, _isGridEnabled);
            var point = snap.Point;

            // First point — anchor only
            if (_measurePoints.Count == 0)
            {
                _measurePoints.Add(point);
                MeasureText = "📏 Point A set — click next point | Right-click to reset";
                return;
            }

            // Subsequent points — accumulate segment
            var prev = _measurePoints[^1];
            var dx = point.X - prev.X;
            var dy = point.Y - prev.Y;
            var dz = point.Z - prev.Z;
            var dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

            _measureTotalDistance += dist;
            _measureSegments.Add(new MeasurementVisualData(prev, point, dist));
            _measurePoints.Add(point);

            int segCount = _measureSegments.Count;
            MeasureText = segCount == 1
                ? $"📏 {dist:F3} m | Click for more segments | Right-click to reset"
                : $"📏 Seg {segCount}: {dist:F3} m | Total: {_measureTotalDistance:F3} m | Right-click to reset";

            RenderAllMeasureSegments();
        }

        private void RenderMeasurePreview()
        {
            if (_measurePoints.Count != 1 || _measureHoverPoint == null)
            {
                _render.ClearLayer(MeasurePreviewLayer);
                return;
            }

            _render.Render(
                new[] { new MeasurementLineData(_measurePoints[0], _measureHoverPoint) },
                new MeasurementPreviewBuilder(),
                MeasurePreviewLayer);
        }

        private void RenderAllMeasureSegments()
        {
            _render.ClearLayer(MeasureFinalLayer);
            if (_measureSegments.Count > 0)
            {
                _render.Render(
                    _measureSegments,
                    new MeasurementFinalBuilder(),
                    MeasureFinalLayer);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Snap indicator
        // ════════════════════════════════════════════════════════════════════

        private void RenderSnapIndicator(SnapResult snap)
        {
            if (!snap.Snapped)
            {
                _render.ClearLayer(SnapIndicatorLayer);
                return;
            }

            _render.Render(
                new[] { new SnapIndicatorData(snap.Point, snap.Type) },
                new SnapIndicatorBuilder(),
                SnapIndicatorLayer);
        }

        // ════════════════════════════════════════════════════════════════════
        // Drawing preview
        // ════════════════════════════════════════════════════════════════════

        private void RenderPreview()
        {
            var previewPoints = new List<DomainPoint3D>(_drawing.GetPoints());
            if (_currentHover != null) previewPoints.Add(_currentHover);

            if (previewPoints.Count < 2)
            {
                _render.ClearLayer(PreviewLayer);
                return;
            }

            var color = IsHoverNearFirstPoint() ? Colors.Cyan : Colors.Yellow;

            _render.Render(
                new[] { previewPoints },
                new PreviewLinesBuilder(color),
                PreviewLayer);
        }

        // ════════════════════════════════════════════════════════════════════
        // Delete / manage zones
        // ════════════════════════════════════════════════════════════════════

        private bool CanFinish() => IsDrawing && PointCount >= 3;
        private bool CanUndo() => IsDrawing && PointCount > 0;
        private bool CanStartMeasure() => !IsDrawing;
        private bool CanDeleteSelectedZone() =>
            !IsDrawing && !IsMeasuring && (_zoneList.SelectedZone?.Zone != null || _scene.SelectedZone != null);
        private bool CanDeleteAllZones() => !IsDrawing && !IsMeasuring && _zoneList.ZoneCount > 0;

        private void DeleteSelectedZone()
        {
            var selected = _zoneList.SelectedZone?.Zone ?? _scene.SelectedZone;
            if (selected != null) _scene.RemoveZone(selected);
        }

        private void DeleteAllZones()
        {
            if (!_deleteAllPendingConfirmation)
            {
                _deleteAllPendingConfirmation = true;
                _deleteAllConfirmTimer.Stop();
                _deleteAllConfirmTimer.Start();
                ShowInlineWarning("⚠ Press Delete All again to confirm", TimeSpan.FromSeconds(3));
                return;
            }

            _deleteAllPendingConfirmation = false;
            _deleteAllConfirmTimer.Stop();
            _scene.ClearZones();
            HideInlineWarning();
        }

        // ════════════════════════════════════════════════════════════════════
        // ESC handling (double-press confirmation when ≥3 points)
        // ════════════════════════════════════════════════════════════════════

        private void HandleEscape()
        {
            if (PointCount >= 3)
            {
                if (_escPendingConfirmation)
                {
                    Cancel();
                    return;
                }

                _escPendingConfirmation = true;
                ShowInlineWarning(
                    $"⚠ Press ESC again to discard ({PointCount} points will be lost)",
                    TimeSpan.FromSeconds(2));
                _escConfirmTimer.Stop();
                _escConfirmTimer.Start();
                return;
            }

            Cancel();
        }

        // ════════════════════════════════════════════════════════════════════
        // Session helpers
        // ════════════════════════════════════════════════════════════════════

        private void EndDrawingSession()
        {
            if (IsDrawing) _interaction.PopMode();
            _drawing.Start();
            _render.ClearLayer(PreviewLayer);
            _render.ClearLayer(SnapIndicatorLayer);
            IsDrawing = false;
            ActiveDrawingMode = DrawingMode.None;
            _currentHover = null;
            OnPointCollectionChanged();
        }

        private void OnPointCollectionChanged()
        {
            _escPendingConfirmation = false;
            _escConfirmTimer.Stop();
            OnPropertyChanged(nameof(PointCount));
            RefreshHintText();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshHintText()
        {
            HintText = PointCount == 0
                ? "📍 Left click: add point  |  ESC: cancel  |  SHIFT: ortho lock"
                : $"📍 Points: {PointCount}  |  Right-click / C: finish  |  ESC: cancel  |  Ctrl+Z: undo  |  SHIFT: ortho lock";
        }

        // ════════════════════════════════════════════════════════════════════
        // Snap target collection
        // ════════════════════════════════════════════════════════════════════

        private IEnumerable<DomainPoint3D> GetAllSnapTargets()
        {
            // Existing zone vertices
            foreach (var zone in _scene.GetZones())
                foreach (var v in zone.Vertices)
                    yield return v;

            // Current measure points
            foreach (var mp in _measurePoints)
                yield return mp;

            // Current drawing points (excluding the very last to allow undo)
            var drawPts = _drawing.GetPoints();
            for (int i = 0; i < drawPts.Count - 1; i++)
                yield return drawPts[i];

            // First drawing point (to enable close-polygon snap)
            if (drawPts.Count >= 3)
                yield return drawPts[0];
        }

        // ════════════════════════════════════════════════════════════════════
        // Geometry helpers
        // ════════════════════════════════════════════════════════════════════

        private DomainPoint3D? LastDrawnPoint()
        {
            var pts = _drawing.GetPoints();
            return pts.Count > 0 ? pts[^1] : null;
        }

        private bool IsHoverNearFirstPoint()
        {
            var points = _drawing.GetPoints();
            if (_currentHover == null || points.Count == 0) return false;

            var first = points[0];
            var h = _currentHover;
            var dx = first.X - h.X;
            var dy = first.Y - h.Y;
            var dz = first.Z - h.Z;
            var dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return dist <= GetSceneSize() * SnapToFirstThresholdPercent;
        }

        private double GetSceneSize()
        {
            var elements = _scene.GetElements();
            if (elements.Count == 0) return 10.0;

            var minX = elements.Min(e => e.ElementCOG.X);
            var maxX = elements.Max(e => e.ElementCOG.X);
            var minY = elements.Min(e => e.ElementCOG.Y);
            var maxY = elements.Max(e => e.ElementCOG.Y);
            var minZ = elements.Min(e => e.ElementCOG.Z);
            var maxZ = elements.Max(e => e.ElementCOG.Z);

            return System.Math.Max(
                1.0,
                System.Math.Max(maxX - minX, System.Math.Max(maxY - minY, maxZ - minZ)));
        }

        private static void EnsureClosed(List<DomainPoint3D> points)
        {
            if (points.Count < 3) return;
            var first = points[0];
            var last = points[^1];
            if (System.Math.Abs(first.X - last.X) > PointEqualityTolerance ||
                System.Math.Abs(first.Y - last.Y) > PointEqualityTolerance ||
                System.Math.Abs(first.Z - last.Z) > PointEqualityTolerance)
            {
                points.Add(new DomainPoint3D(first.X, first.Y, first.Z));
            }
        }

        private static double CalculatePolygonArea(List<DomainPoint3D> points)
        {
            if (points.Count < 4) return 0; // closed polygon = first point repeated
            double sum = 0;
            for (int i = 0; i < points.Count - 1; i++)
                sum += (points[i].X * points[i + 1].Y) - (points[i + 1].X * points[i].Y);
            return System.Math.Abs(sum) * 0.5;
        }

        // ════════════════════════════════════════════════════════════════════
        // Inline warning
        // ════════════════════════════════════════════════════════════════════

        private void ShowInlineWarning(string message, TimeSpan duration)
        {
            InlineWarning = message;
            IsInlineWarningVisible = true;
            _inlineWarningTimer.Stop();
            _inlineWarningTimer.Interval = duration;
            _inlineWarningTimer.Start();
        }

        private void HideInlineWarning()
        {
            _inlineWarningTimer.Stop();
            InlineWarning = string.Empty;
            IsInlineWarningVisible = false;
        }

        // ── Timer ticks ───────────────────────────────────────────────────────
        private void InlineWarningTimer_Tick(object? sender, EventArgs e) => HideInlineWarning();
        private void EscConfirmTimer_Tick(object? sender, EventArgs e) { _escConfirmTimer.Stop(); _escPendingConfirmation = false; }
        private void DeleteAllConfirmTimer_Tick(object? sender, EventArgs e) { _deleteAllConfirmTimer.Stop(); _deleteAllPendingConfirmation = false; }
    }
}