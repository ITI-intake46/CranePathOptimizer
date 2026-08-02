using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F7_Zones.Views;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public enum DrawingMode { None, Allowed, Forbidden }

    public enum FinishReason
    {
        RightClick,
        CtrlEnter,
        CKey,
        DoubleClick,
        ToolbarFinishButton
    }

    public class DrawingViewModel : ViewModelBase
    {
        /// <summary>
        /// Testing-only hook to bypass showing the ZoneFinalizationDialog during automated tests.
        /// </summary>
        internal static System.Func<double, string, (bool Success, string ZoneType, string ZoneName, double? MinHeight, double? MaxHeight)>? DialogShowHook { get; set; }

        // ── Services ─────────────────────────────────────────────────────────
        private readonly DrawingService _drawing;
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly ZoneListViewModel _zoneList;
        private readonly IZoneGeometryService _geometryService;
        private readonly IUnitSettingsService? _unitSettings;
        private readonly IOsnapService? _osnapService;

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
        private DomainPoint3D? _measurementMarkerPoint;

        public event EventHandler? MeasurementLabelsChanged;
        public event EventHandler? MeasurementPreviewChanged;

        // ── Snap ─────────────────────────────────────────────────────────────
        private CancellationTokenSource? _transientUpdateCancellation;
        private Task _transientUpdateTask = Task.CompletedTask;
        private ImmutableList<Point3D> _transientSnapVertices =
            ImmutableList<Point3D>.Empty;
        private long _drawingSessionVersion;
        private CancellationTokenSource? _sceneScaleCancellation;
        private int _sceneScaleGeneration;
        private double _sceneSize = 10.0;
        private int _pendingPointResolutionCount;
        private FinishReason? _deferredFinishReason;

        // ── Layer names ──────────────────────────────────────────────────────
        private const string PreviewLayer = "Preview";
        private const string MeasurePreviewLayer = "MeasurePreview";
        private const string MeasureFinalLayer = "MeasureFinal";
        private const string SnapIndicatorLayer = "SnapIndicator";

        // ── Geometry constants ───────────────────────────────────────────────
        private const double PointEqualityTolerance = 0.001;
        private const double SnapToFirstThresholdPercent = 0.005;
        private const double MeasurementDistanceTolerance = 1e-6;

        // ════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════
        public DrawingViewModel(
            DrawingService drawing,
            ISceneService scene,
            IRenderingService render,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            IZoneGeometryService geometryService,
            IUnitSettingsService? unitSettings = null,
            IOsnapService? osnapService = null)
        {
            _drawing = drawing;
            _scene = scene;
            _render = render;
            _interaction = interaction;
            _hitTest = hitTest;
            _geometryService = geometryService;
            _unitSettings = unitSettings;
            _osnapService = osnapService;
            _zoneList = new ZoneListViewModel(scene);

            if (_unitSettings != null)
                _unitSettings.SettingsChanged += OnUnitSettingsChanged;
            if (_osnapService != null)
                _osnapService.MasterEnabledChanged += OnOsnapMasterEnabledChanged;
            _scene.ElementsChanged += OnSceneElementsChanged;
            IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement>
                initialElements = _scene.GetElementSnapshot();
            if (initialElements.Count > 0)
            {
                QueueSceneScaleUpdate(initialElements);
            }

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
        public long DrawingSessionVersion =>
            Interlocked.Read(ref _drawingSessionVersion);
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
        public IReadOnlyList<MeasurementVisualData> MeasurementSegments => _measureSegments;
        public DomainPoint3D? MeasurementMarkerPoint => _measurementMarkerPoint;
        public MeasurementLineData? MeasurementPreview =>
            _measurePoints.Count == 1 && _measureHoverPoint != null
                ? new MeasurementLineData(_measurePoints[0], _measureHoverPoint)
                : null;

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
            if (IsDrawing)
            {
                CommitResolvedPoint(rawPoint, shift, acceptedSnap: null);
                return;
            }
            if (IsMeasuring) { HandleMeasureClick(rawPoint); }
        }

        /// <summary>
        /// Commits the point resolved by the authoritative viewport pipeline.
        /// Modifier state is captured before any await and is never re-read here.
        /// </summary>
        public bool CommitResolvedPoint(
            DomainPoint3D rawPoint,
            bool capturedShift,
            OsnapResult? acceptedSnap)
        {
            if (!IsDrawing)
                return false;

            DomainPoint3D point = ResolveDrawingPoint(
                rawPoint,
                capturedShift,
                acceptedSnap);
            return AddResolvedPoint(point);
        }

        public bool CommitResolvedMeasurementPoint(
            DomainPoint3D rawPoint,
            OsnapResult? acceptedSnap)
        {
            if (!IsMeasuring)
                return false;

            DomainPoint3D point = TryResolveObjectSnapPoint(
                acceptedSnap,
                out DomainPoint3D snappedPoint)
                ? snappedPoint
                : rawPoint;
            HandleMeasureClick(point);
            return true;
        }

        /// <summary>
        /// Pins a drawing session while the authoritative viewport resolves a
        /// click asynchronously. Finish requests are deferred until that click
        /// has either committed or been rejected; cancellation/replacement still
        /// ends the session immediately.
        /// </summary>
        public bool BeginPointResolution(long drawingSessionVersion)
        {
            if (!IsDrawing ||
                drawingSessionVersion != DrawingSessionVersion)
            {
                return false;
            }

            _pendingPointResolutionCount++;
            return true;
        }

        public void CompletePointResolution(long drawingSessionVersion)
        {
            if (!IsDrawing ||
                drawingSessionVersion != DrawingSessionVersion)
            {
                return;
            }

            if (_pendingPointResolutionCount > 0)
                _pendingPointResolutionCount--;

            if (_pendingPointResolutionCount != 0 ||
                !_deferredFinishReason.HasValue)
            {
                return;
            }

            FinishReason reason = _deferredFinishReason.Value;
            _deferredFinishReason = null;
            FinishDrawingFromUserAction(reason);
        }

        /// <summary>Entry point for right-click from the viewport.</summary>
        public void HandleRightClick()
        {
            if (IsMeasuring) return;
            if (IsDrawing) { FinishDrawingFromUserAction(FinishReason.RightClick); }
        }

        /// <summary>Entry point for double-click (left) from the viewport.</summary>
        public void HandleDoubleClick() => FinishDrawingFromUserAction(FinishReason.DoubleClick);

        /// <summary>
        /// Update the rubber-band preview while the cursor moves during drawing
        /// AND handle measure hover — DrawingInteractionMode calls this for all MouseMove events,
        /// so both modes are served from the same entry point.
        /// </summary>
        public void SetHoverPoint(
            DomainPoint3D rawPoint,
            bool shiftDown = false,
            OsnapResult? acceptedSnap = null)
        {
            bool shift = shiftDown || IsShiftDown;
            SetResolvedHoverPoint(rawPoint, shift, acceptedSnap);
        }

        public void SetResolvedHoverPoint(
            DomainPoint3D rawPoint,
            bool capturedShift,
            OsnapResult? acceptedSnap)
        {
            if (IsDrawing)
            {
                _currentHover = ResolveDrawingPoint(
                    rawPoint,
                    capturedShift,
                    acceptedSnap);
                RenderPreview();
            }

            if (IsMeasuring)
            {
                DomainPoint3D point = TryResolveObjectSnapPoint(
                    acceptedSnap,
                    out DomainPoint3D snappedPoint)
                    ? snappedPoint
                    : rawPoint;
                SetMeasureHoverPoint(point, capturedShift);
            }
        }

        /// <summary>Update the measurement preview line while cursor moves during measuring.</summary>
        public void SetMeasureHoverPoint(DomainPoint3D rawPoint, bool shiftDown = false)
        {
            if (!IsMeasuring) return;

            _measureHoverPoint = rawPoint;
            SetMeasurementPickMarker(rawPoint);
            MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearMeasureHoverPoint()
        {
            if (!IsMeasuring) return;

            _measureHoverPoint = null;
            _measurementMarkerPoint = null;
            _render.ClearLayer(MeasurePreviewLayer);
            _render.ClearLayer(SnapIndicatorLayer);
            MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
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

            if (e.Key == Key.Enter &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                FinishDrawingFromUserAction(FinishReason.CtrlEnter);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C)
            {
                FinishDrawingFromUserAction(FinishReason.CKey);
                e.Handled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Drawing — core methods
        // ════════════════════════════════════════════════════════════════════

        public void Start()
        {
            StopMeasureMode();
            if (IsDrawing)
                _interaction.PopMode();
            _interaction.PushMode(new Interaction.DrawingInteractionMode(this, _hitTest, _interaction));
            long sessionVersion =
                Interlocked.Increment(ref _drawingSessionVersion);
            _drawing.Start();
            _transientSnapVertices =
                ImmutableList<Point3D>.Empty;
            _pendingPointResolutionCount = 0;
            _deferredFinishReason = null;
            _currentHover = null;
            _render.ClearLayer(PreviewLayer);
            _render.ClearLayer(SnapIndicatorLayer);
            IsDrawing = true;
            _osnapService?.ClearTransientZoneGeometry(sessionVersion);
            QueueTransientSnapGeometry();
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

        private bool AddResolvedPoint(DomainPoint3D point)
        {
            if (!IsDrawing)
                return false;

            if (!_drawing.TryAddPoint(point))
                return false;

            _transientSnapVertices =
                _transientSnapVertices.Add(
                    new Point3D(point.X, point.Y, point.Z));
            _currentHover = null;
            _render.ClearLayer(SnapIndicatorLayer);
            QueueTransientSnapGeometry();
            OnPointCollectionChanged();
            RenderPreview();
            return true;
        }

        public void Finish() => FinishDrawingFromUserAction(FinishReason.ToolbarFinishButton);

        public void FinishDrawingFromUserAction(FinishReason reason)
        {
            if (!IsDrawing) return;
            if (_pendingPointResolutionCount > 0)
            {
                _deferredFinishReason = reason;
                return;
            }

            var rawPoints = _drawing.GetPoints();
            var uniquePoints = _geometryService.NormalizeVertices(rawPoints);

            if (uniquePoints.Count < 3)
            {
                ShowInlineWarning("⚠ At least 3 unique points required to create a zone", TimeSpan.FromSeconds(3));
                return;
            }

            if (!_geometryService.ValidatePolygon(uniquePoints, out var warning))
            {
                ShowInlineWarning(warning ?? "⚠ Invalid polygon", TimeSpan.FromSeconds(3));
                return;
            }

            double areaM2 = Math.Abs(_geometryService.CalculateSignedArea2D(uniquePoints));

            bool isDialogSuccess = false;
            string dialogZoneType = _currentType == ZoneType.Allowed ? "Allowed" : "Forbidden";
            string dialogZoneName = "";
            double? dialogMinHeight = null;
            double? dialogMaxHeight = null;

            if (DialogShowHook != null)
            {
                var hookRes = DialogShowHook(areaM2, dialogZoneType);
                isDialogSuccess = hookRes.Success;
                dialogZoneType = hookRes.ZoneType;
                dialogZoneName = hookRes.ZoneName;
                dialogMinHeight = hookRes.MinHeight;
                dialogMaxHeight = hookRes.MaxHeight;
            }
            else
            {
                var dialog = new ZoneFinalizationDialog(
                    areaM2,
                    _currentType == ZoneType.Allowed ? "Allowed" : "Forbidden")
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    isDialogSuccess = true;
                    dialogZoneType = dialog.ZoneType;
                    dialogZoneName = dialog.ZoneName;
                    dialogMinHeight = dialog.MinHeightValue;
                    dialogMaxHeight = dialog.MaxHeightValue;
                }
            }

            if (!isDialogSuccess)
            {
                Cancel();
                return;
            }

            // Stored zone vertices must not duplicate the first vertex at the end.
            rawPoints.Clear();
            rawPoints.AddRange(uniquePoints);

            var zone = _drawing.BuildZone();
            if (zone != null)
            {
                zone.Type = string.Equals(dialogZoneType, "Forbidden",
                                StringComparison.OrdinalIgnoreCase)
                            ? ZoneType.Forbidden
                            : ZoneType.Allowed;
                zone.Area = areaM2;
                zone.Name = string.IsNullOrWhiteSpace(dialogZoneName)
                            ? $"{zone.Type} Zone {_scene.GetZones().Count + 1}"
                            : dialogZoneName;
                _scene.AddZone(zone);
                _zoneList.AddOrUpdateFromZone(zone, dialogMinHeight, dialogMaxHeight);
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
            if (_transientSnapVertices.Count > 0)
            {
                _transientSnapVertices =
                    _transientSnapVertices.RemoveAt(
                        _transientSnapVertices.Count - 1);
            }
            QueueTransientSnapGeometry();
            OnPointCollectionChanged();
            RenderPreview();
        }

        // ════════════════════════════════════════════════════════════════════
        // Measure - independent point-to-point distances
        // ════════════════════════════════════════════════════════════════════

        private void StartMeasure()
        {
            if (IsDrawing) Cancel();
            if (IsMeasuring)
                StopMeasureMode();

            _interaction.PushMode(new Interaction.DrawingInteractionMode(this, _hitTest, _interaction));
            IsMeasuring = true;
            ResetMeasurement();
        }

        public void StopMeasureMode()
        {
            if (!IsMeasuring)
                return;

            _interaction.PopMode();
            IsMeasuring = false;
            ResetMeasurement();
        }

        public void ResetMeasurement()
        {
            _measurePoints.Clear();
            _measureSegments.Clear();
            _measureHoverPoint = null;
            _measurementMarkerPoint = null;
            MeasureText = "Click first point to start measuring";
            _render.ClearLayer(MeasurePreviewLayer);
            _render.ClearLayer(MeasureFinalLayer);
            _render.ClearLayer(SnapIndicatorLayer);
            MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
            MeasurementLabelsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HandleMeasureClick(DomainPoint3D point)
        {
            if (_measurePoints.Count == 0)
            {
                _measurePoints.Add(point);
                _measureHoverPoint = point;
                MeasureText = "Point A set - click Point B";
                SetMeasurementPickMarker(point);
                MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var start = _measurePoints[0];
            var dx = point.X - start.X;
            var dy = point.Y - start.Y;
            var dz = point.Z - start.Z;
            var dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (dist <= MeasurementDistanceTolerance)
            {
                MeasureText = "Point B must differ from Point A";
                _measureHoverPoint = point;
                SetMeasurementPickMarker(point);
                MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            string labelText = FormatMeasurementDistance(dist);
            _measureSegments.Add(new MeasurementVisualData(start, point, dist, labelText));
            _measurePoints.Clear();
            _measureHoverPoint = null;
            MeasureText = $"{labelText} - click a new Point A";
            _render.ClearLayer(MeasurePreviewLayer);
            SetMeasurementPickMarker(point);
            MeasurementPreviewChanged?.Invoke(this, EventArgs.Empty);
            MeasurementLabelsChanged?.Invoke(this, EventArgs.Empty);
        }

        private string FormatMeasurementDistance(double distanceMeters)
        {
            UnitSettings settings = _unitSettings?.Current ?? UnitSettings.Default;
            return FormatMeasurementDistance(distanceMeters, settings);
        }

        private static string FormatMeasurementDistance(
            double distanceMeters,
            UnitSettings settings)
        {
            double displayValue = UnitSystem.FromMeters(
                distanceMeters,
                settings.DisplayLengthUnit);
            string numberFormat = settings.DisplayLengthUnit switch
            {
                LengthUnit.Millimeter => "0",
                LengthUnit.Centimeter => "0.#",
                LengthUnit.Meter => "0.000",
                _ => "0.###"
            };
            string valueText = displayValue.ToString(
                numberFormat,
                CultureInfo.CurrentCulture);
            string unitSymbol = UnitSystem.LengthSymbol(settings.DisplayLengthUnit);
            return $"{valueText} {unitSymbol}";
        }

        private void OnUnitSettingsChanged(object? sender, UnitSettings settings)
        {
            for (int index = 0; index < _measureSegments.Count; index++)
            {
                MeasurementVisualData measurement = _measureSegments[index];
                _measureSegments[index] = measurement with
                {
                    LabelText = FormatMeasurementDistance(
                        measurement.Distance,
                        settings)
                };
            }

            if (_measureSegments.Count > 0)
                MeasureText = $"{_measureSegments[^1].LabelText} - click a new Point A";

            MeasurementLabelsChanged?.Invoke(this, EventArgs.Empty);
        }

        // ════════════════════════════════════════════════════════════════════
        // Snap indicator
        // ════════════════════════════════════════════════════════════════════

        private void SetMeasurementPickMarker(DomainPoint3D point)
        {
            _measurementMarkerPoint = point;
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
            !IsDrawing && !IsMeasuring && (_zoneList.SelectedZone?.ZoneData != null || _scene.SelectedZone != null);
        private bool CanDeleteAllZones() => !IsDrawing && !IsMeasuring && _zoneList.ZoneCount > 0;

        private void DeleteSelectedZone()
        {
            var selected = _scene.SelectedZone;
            if (selected == null && _zoneList.SelectedZone?.ZoneData != null)
            {
                selected = _scene.GetZones()
                    .FirstOrDefault(zone => zone.ZoneId == _zoneList.SelectedZone.ZoneData.ZoneId);
            }

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
            _pendingPointResolutionCount = 0;
            _deferredFinishReason = null;
            CancelTransientUpdate();
            long closedSessionVersion =
                Interlocked.Increment(ref _drawingSessionVersion);
            _osnapService?.ClearTransientZoneGeometry(
                closedSessionVersion);
            _drawing.Start();
            _transientSnapVertices =
                ImmutableList<Point3D>.Empty;
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
            return dist <= PreviewClosureThreshold();
        }

        private DomainPoint3D ResolveDrawingPoint(
            DomainPoint3D rawPoint,
            bool capturedShift,
            OsnapResult? acceptedSnap)
        {
            if (TryResolveObjectSnapPoint(
                    acceptedSnap,
                    out DomainPoint3D snappedPoint))
                return snappedPoint;

            if (TryResolveFirstPointClosure(
                    rawPoint,
                    out DomainPoint3D closedPoint))
            {
                return closedPoint;
            }

            DomainPoint3D? lastPoint = LastDrawnPoint();
            if (capturedShift && lastPoint != null)
                return ApplyOrthogonalConstraint(rawPoint, lastPoint);

            return rawPoint;
        }

        private bool TryResolveObjectSnapPoint(
            OsnapResult? acceptedSnap,
            out DomainPoint3D point)
        {
            point = null!;
            if (acceptedSnap == null ||
                !(_osnapService?.Settings.MasterEnabled ?? true))
            {
                return false;
            }

            Point3D snapped = acceptedSnap.WorldPoint;
            point = new DomainPoint3D(
                snapped.X,
                snapped.Y,
                snapped.Z);
            return true;
        }

        private bool TryResolveFirstPointClosure(
            DomainPoint3D rawPoint,
            out DomainPoint3D firstPoint)
        {
            List<DomainPoint3D> points = _drawing.GetPoints();
            if (points.Count < 3)
            {
                firstPoint = rawPoint;
                return false;
            }

            firstPoint = points[0];
            double deltaX = firstPoint.X - rawPoint.X;
            double deltaY = firstPoint.Y - rawPoint.Y;
            double deltaZ = firstPoint.Z - rawPoint.Z;
            double distanceSquared =
                deltaX * deltaX +
                deltaY * deltaY +
                deltaZ * deltaZ;
            double threshold = FirstPointClosureThreshold();
            return distanceSquared <= threshold * threshold;
        }

        private double PreviewClosureThreshold()
        {
            double sceneScale = Volatile.Read(ref _sceneSize);
            return sceneScale * SnapToFirstThresholdPercent;
        }

        private double FirstPointClosureThreshold()
        {
            double sceneScale = Volatile.Read(ref _sceneSize);
            return Math.Max(
                0.25,
                sceneScale * 0.018);
        }

        private void OnSceneElementsChanged(
            List<CraneOptimizer.Domain.Schemas.PrecastElement> elements)
            => QueueSceneScaleUpdate(elements);

        private void QueueSceneScaleUpdate(
            IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements)
        {
            int generation =
                Interlocked.Increment(ref _sceneScaleGeneration);
            var cancellation = new CancellationTokenSource();
            CancellationTokenSource? superseded =
                Interlocked.Exchange(
                    ref _sceneScaleCancellation,
                    cancellation);
            try
            {
                superseded?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _ = UpdateSceneScaleAsync(
                elements,
                generation,
                cancellation);
        }

        private async Task UpdateSceneScaleAsync(
            IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements,
            int generation,
            CancellationTokenSource cancellation)
        {
            try
            {
                double sceneSize = await Task.Run(
                        () => ComputeSceneSize(
                            elements,
                            cancellation.Token),
                        cancellation.Token)
                    .ConfigureAwait(false);
                if (generation ==
                        Volatile.Read(ref _sceneScaleGeneration) &&
                    !cancellation.IsCancellationRequested)
                {
                    Volatile.Write(ref _sceneSize, sceneSize);
                }
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Drawing] Scene scale update failed: " +
                    exception.Message);
            }
            finally
            {
                Interlocked.CompareExchange(
                    ref _sceneScaleCancellation,
                    null,
                    cancellation);
                cancellation.Dispose();
            }
        }

        private static double ComputeSceneSize(
            IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements,
            CancellationToken cancellationToken)
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double maxZ = double.NegativeInfinity;
            for (int index = 0; index < elements.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                DomainPoint3D? center = elements[index].ElementCOG;
                if (center == null)
                    continue;

                minX = Math.Min(minX, center.X);
                minY = Math.Min(minY, center.Y);
                minZ = Math.Min(minZ, center.Z);
                maxX = Math.Max(maxX, center.X);
                maxY = Math.Max(maxY, center.Y);
                maxZ = Math.Max(maxZ, center.Z);
            }

            if (!double.IsFinite(minX))
                return 10.0;

            return Math.Max(
                1.0,
                Math.Max(
                    maxX - minX,
                    Math.Max(maxY - minY, maxZ - minZ)));
        }

        private static DomainPoint3D ApplyOrthogonalConstraint(
            DomainPoint3D rawPoint,
            DomainPoint3D anchor)
        {
            double deltaX = Math.Abs(rawPoint.X - anchor.X);
            double deltaY = Math.Abs(rawPoint.Y - anchor.Y);
            return deltaX >= deltaY
                ? new DomainPoint3D(
                    rawPoint.X,
                    anchor.Y,
                    anchor.Z)
                : new DomainPoint3D(
                    anchor.X,
                    rawPoint.Y,
                    anchor.Z);
        }

        private void QueueTransientSnapGeometry()
        {
            IOsnapService? service = _osnapService;
            if (service == null)
                return;

            CancelTransientUpdate();
            long sessionVersion = DrawingSessionVersion;
            if (!IsDrawing || !service.Settings.MasterEnabled)
            {
                service.ClearTransientZoneGeometry(sessionVersion);
                return;
            }

            ImmutableList<Point3D> snapshot =
                _transientSnapVertices;

            var cancellation = new CancellationTokenSource();
            Interlocked.Exchange(
                ref _transientUpdateCancellation,
                cancellation);
            _transientUpdateTask = PublishTransientSnapGeometryAsync(
                service,
                sessionVersion,
                snapshot,
                cancellation);
        }

        private async Task PublishTransientSnapGeometryAsync(
            IOsnapService service,
            long sessionVersion,
            IReadOnlyList<Point3D> snapshot,
            CancellationTokenSource cancellation)
        {
            try
            {
                await service.SetTransientZoneGeometryAsync(
                        sessionVersion,
                        snapshot,
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSnap] Active-zone index refresh failed: " +
                    exception.Message);
            }
            finally
            {
                Interlocked.CompareExchange(
                    ref _transientUpdateCancellation,
                    null,
                    cancellation);
                cancellation.Dispose();
            }
        }

        private void CancelTransientUpdate()
        {
            CancellationTokenSource? cancellation =
                Interlocked.Exchange(
                    ref _transientUpdateCancellation,
                    null);
            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The completed worker won the exchange and disposed its token.
            }
        }

        private void OnOsnapMasterEnabledChanged(bool enabled)
        {
            Dispatcher? dispatcher =
                System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(
                    DispatcherPriority.DataBind,
                    new Action(
                        () => OnOsnapMasterEnabledChanged(enabled)));
                return;
            }

            if (!IsDrawing)
                return;

            if (enabled)
                QueueTransientSnapGeometry();
            else
            {
                CancelTransientUpdate();
                _osnapService?.ClearTransientZoneGeometry(
                    DrawingSessionVersion);
            }
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
