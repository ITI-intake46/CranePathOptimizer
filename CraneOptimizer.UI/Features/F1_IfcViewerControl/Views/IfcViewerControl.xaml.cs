using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using Microsoft.Extensions.DependencyInjection;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Views;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Features.F7_Zones.Interaction;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Resources;
using CraneOptimizer.UI.Shell.Interaction;
using CraneOptimizer.UI.Shell.ViewModels;

using CraneOptimizer.UI.Features.F17_Selection.Interaction;

namespace CraneOptimizer.UI.Features.F1_IfcViewerControl.Views
{
    public partial class IfcViewerControl : UserControl
    {
        private readonly IRenderingService _renderingService = null!;
        private readonly ISceneService _sceneService = null!;
        private readonly ISceneGridSizingService _sceneGridSizingService = null!;
        private readonly IViewportInteractionService _interactionService = null!;
        private readonly IViewportHitTestService _hitTestService = null!;
        private readonly ICameraService _cameraService = null!;
        private readonly IOsnapService _osnapService = null!;
        private readonly OsnapGeometryBridge _osnapBridge = null!;
        private readonly IUnitSettingsService _unitSettingsService = null!;
        private readonly IViewportSelectionService _viewportSelectionService = null!;
        private readonly SelectionHandler _selectionHandler = null!;
        private readonly DrawingViewModel _drawingViewModel = null!;
        private Window? _ownerWindow;
        private MainViewModel? _osnapDxfSource;

        private bool _manualGridSize;
        private double _gridCenterXMeters;
        private double _gridCenterYMeters;
        private double _gridWidthMeters = 50d;
        private double _gridLengthMeters = 50d;
        private string _lastElementSceneKey = string.Empty;
        private bool _pendingWheelClipUpdate;
        private bool _isUpdatingCameraMode;
        private bool _zoneRenderPending;

        private OsnapIndicator? _osnapIndicator;
        private OsnapPointerCoordinator? _osnapCoordinator;
        private CancellationTokenSource? _osnapLifetime;
        private HoverRequestContext? _latestHoverContext;
        private long _osnapCameraVersion = 1;
        private long _osnapDrawingStateVersion = 1;
        private long _osnapRequestId;
        private long _latestHoverRequestId;
        private bool _zoneClickPending;
        private int _loadGeneration;
        private readonly List<(MeasurementVisualData Data, Border Label)> _measurementLabels = new();
        private readonly List<(MeasurementVisualData Data, System.Windows.Shapes.Line Line)> _measurementLines = new();
        private readonly List<(MeasurementVisualData Data, System.Windows.Shapes.Ellipse Start, System.Windows.Shapes.Ellipse End)> _measurementEndpointMarkers = new();
        private System.Windows.Shapes.Line? _measurementPreviewLine;
        private System.Windows.Shapes.Ellipse? _measurementPreviewStartMarker;
        private System.Windows.Shapes.Ellipse? _measurementPreviewEndMarker;
        private FrameworkElement? _measurementPickMarker;
        private static readonly double MeasurementLineThickness = Math.Clamp(2.25d, 1.5d, 3d);
        private const double MeasurementEndpointSize = 7d;
        private const double MeasurementPickMarkerSize = 16d;

        public IfcViewerControl()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            _renderingService = App.ServiceProvider.GetRequiredService<IRenderingService>();
            _sceneService = App.ServiceProvider.GetRequiredService<ISceneService>();
            _sceneGridSizingService = App.ServiceProvider.GetRequiredService<ISceneGridSizingService>();
            _interactionService = App.ServiceProvider.GetRequiredService<IViewportInteractionService>();
            _hitTestService = App.ServiceProvider.GetRequiredService<IViewportHitTestService>();
            _cameraService = App.ServiceProvider.GetRequiredService<ICameraService>();
            _osnapService = App.ServiceProvider.GetRequiredService<IOsnapService>();
            _osnapBridge = App.ServiceProvider.GetRequiredService<OsnapGeometryBridge>();
            _unitSettingsService = App.ServiceProvider.GetRequiredService<IUnitSettingsService>();
            _viewportSelectionService = App.ServiceProvider.GetRequiredService<IViewportSelectionService>();
            _selectionHandler = App.ServiceProvider.GetRequiredService<SelectionHandler>();
            _drawingViewModel = App.ServiceProvider.GetRequiredService<DrawingViewModel>();

            _osnapIndicator = new OsnapIndicator(OsnapOverlayCanvas);

            _renderingService.Initialize(IfcViewer);

            // Wire the 2D rubber-band overlay canvas to the selection handler.
            // Must be done after InitializeComponent() so SelectionCanvas is created.
            _selectionHandler.SetOverlayCanvas(SelectionCanvas);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            int loadGeneration =
                Interlocked.Increment(ref _loadGeneration);
            InitializeOsnapPointerPipeline();
            _ownerWindow = Window.GetWindow(this);
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown -= OwnerWindow_PreviewKeyDown;
                _ownerWindow.PreviewKeyDown += OwnerWindow_PreviewKeyDown;
            }

            _sceneService.ElementsChanged -= OnElementsChanged;
            _sceneService.ElementsChanged += OnElementsChanged;
            _sceneService.ZonesChanged -= ScheduleRenderZones;
            _sceneService.ZonesChanged += ScheduleRenderZones;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneSelected += OnZoneSelected;
            ThemeService.Instance.ThemeChanged -= OnThemeChanged;
            ThemeService.Instance.ThemeChanged += OnThemeChanged;
            IfcViewer.CameraChanged -= IfcViewer_CameraChanged;
            IfcViewer.CameraChanged += IfcViewer_CameraChanged;
            IfcViewer.SizeChanged -= IfcViewer_SizeChanged;
            IfcViewer.SizeChanged += IfcViewer_SizeChanged;
            _osnapService.MasterEnabledChanged -= OnOsnapMasterEnabledChanged;
            _osnapService.MasterEnabledChanged += OnOsnapMasterEnabledChanged;
            _osnapService.GeometryVersionChanged -= OnOsnapGeometryVersionChanged;
            _osnapService.GeometryVersionChanged += OnOsnapGeometryVersionChanged;
            _osnapService.SettingsVersionChanged -= OnOsnapSettingsVersionChanged;
            _osnapService.SettingsVersionChanged += OnOsnapSettingsVersionChanged;
            _drawingViewModel.PropertyChanged -=
                DrawingViewModel_PropertyChanged;
            _drawingViewModel.PropertyChanged +=
                DrawingViewModel_PropertyChanged;
            _drawingViewModel.MeasurementLabelsChanged -= DrawingViewModel_MeasurementLabelsChanged;
            _drawingViewModel.MeasurementLabelsChanged += DrawingViewModel_MeasurementLabelsChanged;
            _drawingViewModel.MeasurementPreviewChanged -= DrawingViewModel_MeasurementPreviewChanged;
            _drawingViewModel.MeasurementPreviewChanged += DrawingViewModel_MeasurementPreviewChanged;
            RebuildMeasurementLabels();

            if (DataContext is MainViewModel vm)
            {
                _interactionService.ResetToDefaultMode(new DefaultSelectionMode(
                    vm,
                    _hitTestService,
                    _viewportSelectionService,
                    () => _cameraService.UpdateClipping(_sceneService.GetElements())));
                await vm.Dxf.InitializeAsync(IfcViewer);
                if (!IsLoaded ||
                    loadGeneration !=
                        Volatile.Read(ref _loadGeneration))
                {
                    return;
                }

                AttachDxfSnapBridge(vm);
            }

            ScheduleRenderZones();
            SetGridState(50d, 50d);
            var elements = _sceneService.GetElements();
            if (elements.Count == 0)
                _cameraService.ResetToDefaultView();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Interlocked.Increment(ref _loadGeneration);
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown -= OwnerWindow_PreviewKeyDown;
                _ownerWindow = null;
            }

            _sceneService.ElementsChanged -= OnElementsChanged;
            _sceneService.ZonesChanged -= ScheduleRenderZones;
            _sceneService.ZoneSelected -= OnZoneSelected;
            ThemeService.Instance.ThemeChanged -= OnThemeChanged;
            IfcViewer.CameraChanged -= IfcViewer_CameraChanged;
            IfcViewer.SizeChanged -= IfcViewer_SizeChanged;
            _osnapService.MasterEnabledChanged -= OnOsnapMasterEnabledChanged;
            _osnapService.GeometryVersionChanged -= OnOsnapGeometryVersionChanged;
            _osnapService.SettingsVersionChanged -= OnOsnapSettingsVersionChanged;
            _drawingViewModel.PropertyChanged -=
                DrawingViewModel_PropertyChanged;
            _drawingViewModel.MeasurementLabelsChanged -= DrawingViewModel_MeasurementLabelsChanged;
            _drawingViewModel.MeasurementPreviewChanged -= DrawingViewModel_MeasurementPreviewChanged;
            MeasurementOverlayCanvas.Children.Clear();
            _measurementLabels.Clear();
            _measurementLines.Clear();
            _measurementEndpointMarkers.Clear();
            _measurementPreviewLine = null;
            _measurementPreviewStartMarker = null;
            _measurementPreviewEndMarker = null;
            _measurementPickMarker = null;
            DetachDxfSnapBridge();
            DisposeOsnapPointerPipeline();
        }

        private void AttachDxfSnapBridge(MainViewModel vm)
        {
            if (!ReferenceEquals(_osnapDxfSource, vm))
            {
                DetachDxfSnapBridge();
                _osnapDxfSource = vm;
                vm.Dxf.DxfLoaded += OnDxfLoaded;
                vm.Dxf.DxfRemoved += OnDxfRemoved;
            }

            foreach (var source in vm.Dxf.LoadedDxfs)
            {
                if (source.Uploader?.IsLoaded == true)
                    OnDxfLoaded(source.Uploader);
            }
        }

        private void DetachDxfSnapBridge()
        {
            if (_osnapDxfSource == null)
                return;

            foreach (var source in _osnapDxfSource.Dxf.LoadedDxfs)
            {
                if (source.Uploader == null)
                    continue;

                source.Uploader.SetZoneContrast(
                    Array.Empty<GreenAreaZone>(),
                    Colors.White);
                _osnapBridge.RemoveDxf(source.Uploader);
            }

            _osnapDxfSource.Dxf.DxfLoaded -= OnDxfLoaded;
            _osnapDxfSource.Dxf.DxfRemoved -= OnDxfRemoved;
            _osnapDxfSource = null;
        }

        private void OnDxfLoaded(DxfUploader uploader)
        {
            _osnapBridge.RefreshDxf(uploader);
            ApplyDxfZoneContrast(uploader, _sceneService.GetZones());
        }

        private void OnDxfRemoved(DxfUploader uploader)
        {
            uploader.SetZoneContrast(
                Array.Empty<GreenAreaZone>(),
                Colors.White);
            _osnapBridge.RemoveDxf(uploader);
        }

        private void OnThemeChanged(AppTheme _) => RefreshDxfZoneContrast();

        private void RefreshDxfZoneContrast(
            IReadOnlyList<GreenAreaZone>? zones = null)
        {
            if (_osnapDxfSource == null)
                return;

            IReadOnlyList<GreenAreaZone> activeZones =
                zones ?? _sceneService.GetZones();
            foreach (var source in _osnapDxfSource.Dxf.LoadedDxfs)
            {
                if (source.Uploader != null)
                    ApplyDxfZoneContrast(source.Uploader, activeZones);
            }
        }

        private static void ApplyDxfZoneContrast(
            DxfUploader uploader,
            IReadOnlyList<GreenAreaZone> zones)
        {
            Color contrastColor = ThemeService.Instance.CurrentTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
            uploader.SetZoneContrast(zones, contrastColor);
        }

        private void OwnerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnKeyDown(IfcViewer, e);
            if (e.Handled)
                return;

            _interactionService.HandleKeyDown(IfcViewer, e);
        }

        private void OnZoneSelected(GreenAreaZone? zone) => ScheduleRenderZones();

        private void ScheduleRenderZones()
        {
            if (_zoneRenderPending)
                return;

            _zoneRenderPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _zoneRenderPending = false;
                if (IsLoaded)
                    RenderZones();
            }));
        }

        private void RenderZones()
        {
            List<GreenAreaZone> zones = _sceneService.GetZones();
            RefreshDxfZoneContrast(zones);
            if (zones.Count == 0)
            {
                _renderingService.ClearLayer("Zones");
                return;
            }

            _renderingService.Render(zones, new ZoneVisualBuilder(_sceneService), "Zones");
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            if (!_manualGridSize)
            {
                var (widthMeters, lengthMeters, centerXMeters, centerYMeters) =
                    _sceneGridSizingService.CalculateGridSizeMeters(elements);
                _gridCenterXMeters = centerXMeters;
                _gridCenterYMeters = centerYMeters;
                SetGridState(widthMeters, lengthMeters);
                _renderingService.SetGridSize(
                    widthMeters,
                    lengthMeters,
                    _gridCenterXMeters,
                    _gridCenterYMeters);
            }

            _cameraService.UpdateClipping(elements);
            if (elements.Count == 0)
            {
                _lastElementSceneKey = string.Empty;
                _cameraService.ResetToDefaultView();
                return;
            }

            var elementSceneKey = BuildElementSceneKey(elements);
            if (elementSceneKey != _lastElementSceneKey)
                _lastElementSceneKey = elementSceneKey;
        }

        private void IfcViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HelixViewport3D viewport)
            {
                if (_selectionHandler.IsClickOnViewCubeOrOverlay(viewport, e))
                {
                    if (_drawingViewModel.IsMeasuring)
                        _drawingViewModel.StopMeasureMode();
                    return;
                }

                UpdateSelectionHandlerEnabled();
                _selectionHandler.OnPreviewMouseDown(viewport, e);
            }

            if (e.ChangedButton != MouseButton.Middle || e.ClickCount != 2)
                return;

            _cameraService.ZoomToFrontView(_sceneService.GetElements(), animate: true);
            e.Handled = true;
        }

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not HelixViewport3D viewport)
                return;

            Point screen = e.GetPosition(viewport);
            bool capturedShift =
                (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            DomainPoint3D? rawToolPoint = null;
            if (_drawingViewModel.IsDrawing &&
                TryResolveRawDrawingPoint(
                    viewport,
                    screen,
                    out DomainPoint3D point))
            {
                rawToolPoint = point;
            }
            else if (_drawingViewModel.IsMeasuring &&
                     DrawingInteractionMode.TryResolveMeasurementPoint(
                         viewport,
                         screen,
                         out Point3D measurementPoint))
            {
                rawToolPoint = ToDomainPoint(measurementPoint);
            }

            if (IsCameraOrDragMouseMove(e))
            {
                InvalidateOsnapHover();
            }
            else if (TryCreateOsnapQuery(
                         viewport,
                         screen,
                         isClick: false,
                         out OsnapQuery query) &&
                     _osnapCoordinator != null)
            {
                _latestHoverRequestId = query.RequestId;
                _latestHoverContext = new HoverRequestContext(
                    query.RequestId,
                    rawToolPoint,
                    capturedShift);
                try
                {
                    _osnapCoordinator.SubmitHover(query);
                }
                catch (ObjectDisposedException)
                {
                    InvalidateOsnapHover();
                }
            }
            else
            {
                InvalidateOsnapHover();
                if ((_drawingViewModel.IsDrawing ||
                     _drawingViewModel.IsMeasuring) &&
                    rawToolPoint != null)
                {
                    _drawingViewModel.SetResolvedHoverPoint(
                        rawToolPoint,
                        capturedShift,
                        acceptedSnap: null);
                }
            }

            if (DataContext is MainViewModel vm)
            {
                if (_drawingViewModel.IsDrawing && rawToolPoint != null)
                {
                    vm.SetCoordinates(
                        rawToolPoint.X,
                        rawToolPoint.Y,
                        rawToolPoint.Z,
                        isReferencePlane: true);
                }
                else if (TryResolveMouseCoordinatePoint(
                             viewport,
                             screen,
                             out Point3D worldPt,
                             out bool isReferencePlane))
                {
                    vm.SetCoordinates(worldPt.X, worldPt.Y, worldPt.Z, isReferencePlane);
                }
            }

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnMouseMove(viewport, e);
            if (!_drawingViewModel.IsDrawing)
                _interactionService.HandleMouseMove(viewport, e);
        }

        private void IfcViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not HelixViewport3D viewport) return;
            if (_selectionHandler.IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnPreviewMouseMove(viewport, e);
        }

        private void IfcViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not HelixViewport3D viewport) return;
            if (_selectionHandler.IsClickOnViewCubeOrOverlay(viewport, e))
                return;

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnPreviewMouseUp(viewport, e);
        }

        private static bool IsCameraOrDragMouseMove(MouseEventArgs e)
            => e.LeftButton == MouseButtonState.Pressed ||
               e.MiddleButton == MouseButtonState.Pressed ||
               e.RightButton == MouseButtonState.Pressed;

        private bool TryResolveMouseCoordinatePoint(
            HelixViewport3D viewport,
            Point screen,
            out Point3D worldPoint,
            out bool isReferencePlane)
        {
            if (TryGetGeometryCoordinatePoint(viewport, screen, out worldPoint))
            {
                isReferencePlane = false;
                return true;
            }

            isReferencePlane = true;
            return _hitTestService.TryGetWorldPoint(viewport, screen, out worldPoint);
        }

        private static bool TryGetGeometryCoordinatePoint(
            HelixViewport3D viewport,
            Point screen,
            out Point3D worldPoint)
        {
            worldPoint = default;

            try
            {
                Point innerPoint = ToInnerViewportPoint(viewport, screen);
                foreach (PointHitResult hit in viewport.Viewport.FindHits(innerPoint))
                {
                    if (hit.Visual == null ||
                        hit.Mesh == null ||
                        hit.RayHit is not RayMeshGeometry3DHitTestResult ||
                        !IsCoordinateGeometryVisual(hit.Visual) ||
                        !IsFinite(hit.Position))
                    {
                        continue;
                    }

                    worldPoint = hit.Position;
                    return true;
                }
            }
            catch
            {
                // Coordinate display falls back to the active reference plane.
            }

            return false;
        }

        private static bool IsCoordinateGeometryVisual(Visual3D visual)
        {
            while (visual != null)
            {
                object? tag = visual.GetValue(FrameworkElement.TagProperty);
                if (tag is PrecastElement || tag is SceneCrane || tag is GreenAreaZone)
                    return true;

                if (tag is string)
                    return false;

                visual = VisualTreeHelper.GetParent(visual) as Visual3D;
            }

            return false;
        }

        private static Point ToInnerViewportPoint(HelixViewport3D viewport, Point helixPoint)
        {
            try
            {
                return viewport.TranslatePoint(helixPoint, viewport.Viewport);
            }
            catch
            {
                return helixPoint;
            }
        }

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) &&
               double.IsFinite(point.Y) &&
               double.IsFinite(point.Z);

        private void InitializeOsnapPointerPipeline()
        {
            DisposeOsnapPointerPipeline();

            _osnapLifetime = new CancellationTokenSource();
            _osnapCoordinator = new OsnapPointerCoordinator(
                _osnapService,
                TimeSpan.FromMilliseconds(20));
            _osnapCoordinator.HoverCompleted +=
                OsnapCoordinator_HoverCompleted;
            _osnapCoordinator.Faulted +=
                OsnapCoordinator_Faulted;
            Interlocked.Increment(ref _osnapCameraVersion);
            Interlocked.Increment(
                ref _osnapDrawingStateVersion);
            InvalidateOsnapHover();
        }

        private void DisposeOsnapPointerPipeline()
        {
            CancellationTokenSource? lifetime = _osnapLifetime;
            OsnapPointerCoordinator? coordinator = _osnapCoordinator;
            _osnapLifetime = null;
            _osnapCoordinator = null;
            _latestHoverContext = null;
            _latestHoverRequestId = 0;
            _zoneClickPending = false;

            lifetime?.Cancel();
            if (coordinator != null)
            {
                coordinator.HoverCompleted -=
                    OsnapCoordinator_HoverCompleted;
                coordinator.Faulted -=
                    OsnapCoordinator_Faulted;
                coordinator.Dispose();
            }

            lifetime?.Dispose();
            _osnapIndicator?.Hide();
        }

        private void AdvanceOsnapCameraVersion()
        {
            Interlocked.Increment(ref _osnapCameraVersion);
            InvalidateOsnapHover();
        }

        private void InvalidateOsnapHover()
        {
            _latestHoverContext = null;
            _latestHoverRequestId = 0;
            _osnapCoordinator?.InvalidateHover();
            _osnapIndicator?.Hide();
        }

        private void OnOsnapMasterEnabledChanged(bool _)
            => InvalidateOsnapStateFromAnyThread();

        private void OnOsnapGeometryVersionChanged(long _)
            => InvalidateOsnapStateFromAnyThread();

        private void OnOsnapSettingsVersionChanged(long _)
            => InvalidateOsnapStateFromAnyThread();

        private void DrawingViewModel_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DrawingViewModel.IsMeasuring))
            {
                UpdateMeasurementOverlayPositions();
            }

            if (e.PropertyName == nameof(DrawingViewModel.IsDrawing) ||
                e.PropertyName == nameof(DrawingViewModel.IsMeasuring) ||
                e.PropertyName == nameof(DrawingViewModel.PointCount))
            {
                Interlocked.Increment(
                    ref _osnapDrawingStateVersion);
                InvalidateOsnapHover();
            }
        }

        private void InvalidateOsnapStateFromAnyThread()
        {
            if (Dispatcher.CheckAccess())
            {
                InvalidateOsnapHover();
                return;
            }

            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (_osnapLifetime != null)
                        InvalidateOsnapHover();
                }));
        }

        private void OsnapCoordinator_HoverCompleted(
            OsnapQueryCompletion completion)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => ApplyHoverCompletion(completion)));
        }

        private void OsnapCoordinator_Faulted(Exception exception)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (_osnapLifetime != null)
                        InvalidateOsnapHover();
                }));
        }

        private void ApplyHoverCompletion(
            OsnapQueryCompletion completion)
        {
            HoverRequestContext? context = _latestHoverContext;
            if (_osnapLifetime == null ||
                context == null ||
                completion.Query.RequestId !=
                    _latestHoverRequestId ||
                context.RequestId != completion.Query.RequestId ||
                !IsSnapshotCurrent(completion.Query.Viewport))
            {
                return;
            }

            OsnapResult? accepted = completion.SnapResult;
            _osnapIndicator?.Update(accepted);

            if (!_drawingViewModel.IsDrawing &&
                !_drawingViewModel.IsMeasuring)
                return;

            DomainPoint3D? rawPoint = context.RawPoint;
            if (rawPoint == null && accepted != null)
                rawPoint = ToDomainPoint(accepted.WorldPoint);
            if (rawPoint != null)
            {
                _drawingViewModel.SetResolvedHoverPoint(
                    rawPoint,
                    context.CapturedShift,
                    accepted);
            }
        }

        private async Task ResolveAndCommitToolClickAsync(
            HelixViewport3D viewport,
            Point screen,
            bool capturedShift,
            bool isDrawingClick)
        {
            CancellationTokenSource? lifetime = _osnapLifetime;
            OsnapPointerCoordinator? coordinator = _osnapCoordinator;
            if (lifetime == null || coordinator == null)
                return;

            CancellationToken cancellationToken = lifetime.Token;
            long clickCameraVersion =
                Interlocked.Read(ref _osnapCameraVersion);
            long clickDrawingStateVersion =
                Interlocked.Read(ref _osnapDrawingStateVersion);
            long clickDrawingSessionVersion =
                _drawingViewModel.DrawingSessionVersion;
            DomainPoint3D? rawPoint;
            if (isDrawingClick)
            {
                rawPoint = TryResolveRawDrawingPoint(
                    viewport,
                    screen,
                    out DomainPoint3D resolvedRaw)
                    ? resolvedRaw
                    : null;
            }
            else
            {
                rawPoint = DrawingInteractionMode.TryResolveMeasurementPoint(
                    viewport,
                    screen,
                    out Point3D resolvedRaw)
                    ? ToDomainPoint(resolvedRaw)
                    : null;
            }
            const int maximumAttempts = 2;
            for (int attempt = 0;
                 attempt < maximumAttempts;
                 attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsOriginalToolClickStateCurrent(
                        isDrawingClick,
                        clickCameraVersion,
                        clickDrawingStateVersion,
                        clickDrawingSessionVersion))
                {
                    return;
                }

                if (!TryCreateOsnapQuery(
                        viewport,
                        screen,
                        isClick: true,
                        out OsnapQuery query))
                {
                    _osnapIndicator?.Hide();
                    CommitRawToolClickIfCurrent(
                        rawPoint,
                        capturedShift,
                        isDrawingClick,
                        clickCameraVersion,
                        clickDrawingStateVersion,
                        clickDrawingSessionVersion);

                    return;
                }

                OsnapQueryCompletion completion;
                try
                {
                    completion = await coordinator.ResolveClickAsync(
                        query,
                        hoverCursorTolerancePixels: 3.0,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    // A settings/master epoch changed. Recapture once so
                    // disabling snap falls back unsnapped and reconfiguration
                    // can resolve against the new immutable state.
                    continue;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _osnapIndicator?.Hide();
                    System.Diagnostics.Debug.WriteLine(
                        $"[OSnap] Viewport tool click resolution failed: " +
                        exception.Message);
                    CommitRawToolClickIfCurrent(
                        rawPoint,
                        capturedShift,
                        isDrawingClick,
                        clickCameraVersion,
                        clickDrawingStateVersion,
                        clickDrawingSessionVersion);
                    return;
                }

                if (!IsOriginalToolClickStateCurrent(
                        isDrawingClick,
                        clickCameraVersion,
                        clickDrawingStateVersion,
                        clickDrawingSessionVersion))
                {
                    return;
                }

                if (!IsSnapshotCurrent(completion.Query.Viewport))
                    continue;

                OsnapResult? accepted = completion.SnapResult;
                if (rawPoint == null && accepted != null)
                    rawPoint = ToDomainPoint(accepted.WorldPoint);
                if (rawPoint == null)
                {
                    _osnapIndicator?.Hide();
                    return;
                }

                _osnapIndicator?.Update(accepted);
                if (isDrawingClick)
                {
                    _drawingViewModel.CommitResolvedPoint(
                        rawPoint,
                        capturedShift,
                        accepted);
                }
                else
                {
                    _drawingViewModel.CommitResolvedMeasurementPoint(
                        rawPoint,
                        accepted);
                }
                return;
            }

            // Repeated source/settings publication prevented a stable snap
            // result. Commit only the raw point captured by this exact click,
            // and only if its camera and drawing session never changed.
            _osnapIndicator?.Hide();
            CommitRawToolClickIfCurrent(
                rawPoint,
                capturedShift,
                isDrawingClick,
                clickCameraVersion,
                clickDrawingStateVersion,
                clickDrawingSessionVersion);
        }

        private bool IsOriginalToolClickStateCurrent(
            bool isDrawingClick,
            long cameraVersion,
            long drawingStateVersion,
            long drawingSessionVersion) =>
            _osnapLifetime is { IsCancellationRequested: false } &&
            cameraVersion ==
                Interlocked.Read(ref _osnapCameraVersion) &&
            drawingStateVersion ==
                Interlocked.Read(ref _osnapDrawingStateVersion) &&
            (isDrawingClick
                ? _drawingViewModel.IsDrawing &&
                  drawingSessionVersion ==
                      _drawingViewModel.DrawingSessionVersion
                : _drawingViewModel.IsMeasuring);

        private void CommitRawToolClickIfCurrent(
            DomainPoint3D? rawPoint,
            bool capturedShift,
            bool isDrawingClick,
            long cameraVersion,
            long drawingStateVersion,
            long drawingSessionVersion)
        {
            if (rawPoint == null ||
                !IsOriginalToolClickStateCurrent(
                    isDrawingClick,
                    cameraVersion,
                    drawingStateVersion,
                    drawingSessionVersion))
            {
                return;
            }

            if (isDrawingClick)
            {
                _drawingViewModel.CommitResolvedPoint(
                    rawPoint,
                    capturedShift,
                    acceptedSnap: null);
            }
            else
            {
                _drawingViewModel.CommitResolvedMeasurementPoint(
                    rawPoint,
                    acceptedSnap: null);
            }
        }

        private bool TryCreateOsnapQuery(
            HelixViewport3D viewport,
            Point cursor,
            bool isClick,
            out OsnapQuery query)
        {
            query = null!;
            OsnapModeMask enabledModes = _osnapService.EnabledModes;
            if (enabledModes == OsnapModeMask.None ||
                viewport.Camera is not ProjectionCamera camera)
            {
                return false;
            }

            OsnapCameraType cameraType;
            double fieldOfViewOrWidth;
            if (camera is PerspectiveCamera perspective)
            {
                cameraType = OsnapCameraType.Perspective;
                fieldOfViewOrWidth = perspective.FieldOfView;
            }
            else if (camera is OrthographicCamera orthographic)
            {
                cameraType = OsnapCameraType.Orthographic;
                fieldOfViewOrWidth = orthographic.Width;
            }
            else
            {
                return false;
            }

            double viewportWidth = viewport.Viewport.ActualWidth;
            double viewportHeight = viewport.Viewport.ActualHeight;
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                viewportWidth = viewport.ActualWidth;
                viewportHeight = viewport.ActualHeight;
            }

            Point screenOffset;
            try
            {
                screenOffset = viewport.Viewport.TranslatePoint(
                    new Point(0, 0),
                    viewport);
            }
            catch
            {
                screenOffset = default;
            }

            DpiScale dpi = VisualTreeHelper.GetDpi(viewport);
            OsnapGeometryVersionState geometry =
                _osnapService.GeometryVersions;
            long settingsVersion = _osnapService.SettingsVersion;
            OsnapSettings settings = _osnapService.Settings;
            double snapRadius = settings.SnapRadiusPx;
            int intersectionLimit =
                settings.MaxEdgesForIntersection;
            bool snapToDxf = settings.SnapToDxf;
            bool snapToIfc = settings.SnapToIfc;

            if (settingsVersion != _osnapService.SettingsVersion ||
                geometry != _osnapService.GeometryVersions ||
                enabledModes != _osnapService.EnabledModes)
            {
                return false;
            }

            var snapshot = new OsnapViewportSnapshot(
                cameraType,
                camera.Position,
                camera.LookDirection,
                camera.UpDirection,
                fieldOfViewOrWidth,
                viewportWidth,
                viewportHeight,
                screenOffset.X,
                screenOffset.Y,
                dpi.DpiScaleX,
                dpi.DpiScaleY,
                Interlocked.Read(ref _osnapCameraVersion),
                geometry.Aggregate,
                geometry.Sources,
                settingsVersion,
                Interlocked.Read(
                    ref _osnapDrawingStateVersion),
                cursor,
                DrawingReferencePlane,
                enabledModes,
                snapRadius,
                intersectionLimit,
                snapToDxf,
                snapToIfc);
            if (!snapshot.IsValid)
                return false;

            query = new OsnapQuery(
                Interlocked.Increment(ref _osnapRequestId),
                isClick,
                snapshot);
            return true;
        }

        private bool IsSnapshotCurrent(
            OsnapViewportSnapshot snapshot)
        {
            if (_osnapLifetime == null ||
                _osnapLifetime.IsCancellationRequested ||
                snapshot.CameraVersion !=
                    Interlocked.Read(ref _osnapCameraVersion) ||
                snapshot.DrawingSessionVersion !=
                    Interlocked.Read(
                        ref _osnapDrawingStateVersion) ||
                snapshot.SettingsVersion !=
                    _osnapService.SettingsVersion ||
                snapshot.EnabledModes !=
                    _osnapService.EnabledModes)
            {
                return false;
            }

            OsnapGeometryVersionState geometry =
                _osnapService.GeometryVersions;
            return snapshot.GeometryVersion ==
                       geometry.Aggregate &&
                   snapshot.SourceVersions ==
                       geometry.Sources;
        }

        private bool TryResolveRawDrawingPoint(
            HelixViewport3D viewport,
            Point screen,
            out DomainPoint3D point)
        {
            point = null!;
            try
            {
                Point inner = ToInnerViewportPoint(viewport, screen);
                if (!Viewport3DHelper.Point2DtoPoint3D(
                        viewport.Viewport,
                        inner,
                        out Point3D near,
                        out Point3D far))
                {
                    return false;
                }

                Vector3D direction = far - near;
                Vector3D normal = DrawingReferencePlane.Normal;
                double denominator =
                    Vector3D.DotProduct(direction, normal);
                if (Math.Abs(denominator) <= 1e-12)
                    return false;

                double distance =
                    Vector3D.DotProduct(
                        DrawingReferencePlane.Origin - near,
                        normal) /
                    denominator;
                if (!double.IsFinite(distance) || distance <= 0)
                    return false;

                Point3D world = near + direction * distance;
                if (!IsFinite(world))
                    return false;

                point = ToDomainPoint(world);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static DomainPoint3D ToDomainPoint(Point3D point)
            => new(point.X, point.Y, point.Z);

        private static readonly OsnapReferencePlaneSnapshot
            DrawingReferencePlane = new(
                new Point3D(0, 0, 0),
                new Vector3D(0, 0, 1));

        private async void IfcViewer_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not HelixViewport3D viewport)
                return;

            bool isDrawingClick = _drawingViewModel.IsDrawing;
            if ((isDrawingClick || _drawingViewModel.IsMeasuring) &&
                e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                if (isDrawingClick && e.ClickCount > 1)
                {
                    _drawingViewModel.HandleDoubleClick();
                    return;
                }

                if (_zoneClickPending)
                    return;

                long drawingSessionVersion =
                    _drawingViewModel.DrawingSessionVersion;
                if (isDrawingClick &&
                    !_drawingViewModel.BeginPointResolution(
                        drawingSessionVersion))
                {
                    return;
                }

                _zoneClickPending = true;
                Point screen = e.GetPosition(viewport);
                bool capturedShift =
                    (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                try
                {
                    await ResolveAndCommitToolClickAsync(
                        viewport,
                        screen,
                        capturedShift,
                        isDrawingClick);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected when input, camera, settings,
                    // geometry, or the drawing session is superseded.
                }
                catch (ObjectDisposedException)
                {
                    // The control or coordinator was unloaded mid-click.
                }
                catch (Exception exception)
                {
                    _osnapIndicator?.Hide();
                    System.Diagnostics.Debug.WriteLine(
                        $"[OSnap] Viewport tool click resolution failed: " +
                        exception.Message);
                }
                finally
                {
                    _zoneClickPending = false;
                    if (isDrawingClick)
                    {
                        _drawingViewModel.CompletePointResolution(
                            drawingSessionVersion);
                    }
                }

                return;
            }

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnMouseDown(viewport, e);
            _interactionService.HandleMouseDown(viewport, e);
        }

        private void IfcViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is HelixViewport3D viewport)
            {
                UpdateSelectionHandlerEnabled();
                _selectionHandler.OnMouseUp(viewport, e);
                if (e.Handled)
                    return;

                _interactionService.HandleMouseUp(viewport, e);
            }
        }

        private void UpdateSelectionHandlerEnabled()
        {
            if (DataContext is not MainViewModel vm)
            {
                _selectionHandler.IsEnabled = false;
                return;
            }

            _selectionHandler.IsEnabled =
                _interactionService.CurrentMode is DefaultSelectionMode &&
                !vm.IfcViewer.IsPlacementMode &&
                !vm.CranePlacement.IsPlacing &&
                !vm.IsPickingOrigin &&
                !vm.Drawing.IsDrawing &&
                !vm.Drawing.IsMeasuring;
        }

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _cameraService.UpdateClipping(_sceneService.GetElements());

            if (sender is HelixViewport3D viewport && !_pendingWheelClipUpdate)
            {
                _pendingWheelClipUpdate = true;
                viewport.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() =>
                    {
                        _pendingWheelClipUpdate = false;
                        _cameraService.UpdateClipping(_sceneService.GetElements());
                    }));
            }
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
            => ResetView();

        public void ResetView()
        {
            var elements = _sceneService.GetElements();
            if (elements.Count == 0)
                _cameraService.ResetToDefaultView();
            else
                _cameraService.ZoomToElements(elements);
        }

        private void TopView_Click(object sender, RoutedEventArgs e)
        {
            var elements = _sceneService.GetElements();
            _cameraService.ZoomToTopView(elements, animate: true);
        }

        private void DrawingViewModel_MeasurementLabelsChanged(object? sender, EventArgs e)
            => RebuildMeasurementLabels();

        private void DrawingViewModel_MeasurementPreviewChanged(object? sender, EventArgs e)
            => UpdateMeasurementTransientVisuals();

        private void IfcViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdvanceOsnapCameraVersion();
            UpdateMeasurementOverlayPositions();
        }

        private void RebuildMeasurementLabels()
        {
            MeasurementOverlayCanvas.Children.Clear();
            _measurementLabels.Clear();
            _measurementLines.Clear();
            _measurementEndpointMarkers.Clear();

            _measurementPreviewLine = CreateMeasurementLine();
            MeasurementOverlayCanvas.Children.Add(_measurementPreviewLine);
            _measurementPreviewStartMarker = CreateMeasurementEndpointMarker();
            MeasurementOverlayCanvas.Children.Add(_measurementPreviewStartMarker);
            _measurementPreviewEndMarker = CreateMeasurementEndpointMarker();
            MeasurementOverlayCanvas.Children.Add(_measurementPreviewEndMarker);

            foreach (MeasurementVisualData measurement in _drawingViewModel.MeasurementSegments)
            {
                System.Windows.Shapes.Line line = CreateMeasurementLine();
                MeasurementOverlayCanvas.Children.Add(line);
                _measurementLines.Add((measurement, line));

                System.Windows.Shapes.Ellipse startMarker =
                    CreateMeasurementEndpointMarker();
                System.Windows.Shapes.Ellipse endMarker =
                    CreateMeasurementEndpointMarker();
                MeasurementOverlayCanvas.Children.Add(startMarker);
                MeasurementOverlayCanvas.Children.Add(endMarker);
                _measurementEndpointMarkers.Add(
                    (measurement, startMarker, endMarker));

                var text = new TextBlock
                {
                    Text = measurement.LabelText,
                    FontSize = (double)FindResource("FontSizeXS"),
                    FontWeight = FontWeights.SemiBold,
                };
                text.SetResourceReference(
                    TextBlock.FontFamilyProperty,
                    "FontPrimary");
                text.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "TextPrimaryBrush");
                var label = new Border
                {
                    Child = text,
                    Padding = new Thickness(4, 1, 4, 1),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    Visibility = Visibility.Collapsed
                };
                label.SetResourceReference(
                    Border.BackgroundProperty,
                    "SurfaceElevatedBrush");
                label.SetResourceReference(
                    Border.BorderBrushProperty,
                    "PrimaryBrush");

                MeasurementOverlayCanvas.Children.Add(label);
                _measurementLabels.Add((measurement, label));
            }

            _measurementPickMarker = CreateMeasurementPickMarker();
            MeasurementOverlayCanvas.Children.Add(_measurementPickMarker);

            UpdateMeasurementOverlayPositions();
        }

        private static System.Windows.Shapes.Line CreateMeasurementLine()
        {
            var line = new System.Windows.Shapes.Line
            {
                StrokeThickness = MeasurementLineThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Visibility = Visibility.Collapsed
            };
            line.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "PrimaryBrush");
            return line;
        }

        private static System.Windows.Shapes.Ellipse CreateMeasurementEndpointMarker()
        {
            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = MeasurementEndpointSize,
                Height = MeasurementEndpointSize,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Visibility = Visibility.Collapsed
            };
            marker.SetResourceReference(
                System.Windows.Shapes.Shape.FillProperty,
                "PrimaryBrush");
            return marker;
        }

        private static FrameworkElement CreateMeasurementPickMarker()
        {
            var marker = new Grid
            {
                Width = MeasurementPickMarkerSize,
                Height = MeasurementPickMarkerSize,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            var horizontal = new System.Windows.Shapes.Line
            {
                X1 = 1,
                Y1 = MeasurementPickMarkerSize / 2d,
                X2 = MeasurementPickMarkerSize - 1,
                Y2 = MeasurementPickMarkerSize / 2d,
                StrokeThickness = 1.25d,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            horizontal.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "PrimaryBrush");
            marker.Children.Add(horizontal);

            var vertical = new System.Windows.Shapes.Line
            {
                X1 = MeasurementPickMarkerSize / 2d,
                Y1 = 1,
                X2 = MeasurementPickMarkerSize / 2d,
                Y2 = MeasurementPickMarkerSize - 1,
                StrokeThickness = 1.25d,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            vertical.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "PrimaryBrush");
            marker.Children.Add(vertical);

            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = 7d,
                Height = 7d,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                StrokeThickness = 1.25d
            };
            ring.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "PrimaryBrush");
            marker.Children.Add(ring);

            var center = new System.Windows.Shapes.Ellipse
            {
                Width = 2.5d,
                Height = 2.5d,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            center.SetResourceReference(
                System.Windows.Shapes.Shape.FillProperty,
                "PrimaryBrush");
            marker.Children.Add(center);

            return marker;
        }

        private void UpdateMeasurementOverlayPositions()
        {
            UpdateMeasurementLinePositions();
            UpdateMeasurementEndpointPositions();
            UpdateMeasurementLabelPositions();
            UpdateMeasurementPickMarker();
        }

        private void UpdateMeasurementLinePositions()
        {
            UpdateMeasurementPreviewLine();
            foreach ((MeasurementVisualData data, System.Windows.Shapes.Line line) in _measurementLines)
                UpdateMeasurementLine(line, data.Start, data.End);
        }

        private void UpdateMeasurementEndpointPositions()
        {
            UpdateMeasurementPreviewMarkers();
            foreach ((MeasurementVisualData data, System.Windows.Shapes.Ellipse start, System.Windows.Shapes.Ellipse end) in _measurementEndpointMarkers)
            {
                UpdateMeasurementMarker(start, data.Start);
                UpdateMeasurementMarker(end, data.End);
            }
        }

        private void UpdateMeasurementTransientVisuals()
        {
            UpdateMeasurementPreviewLine();
            UpdateMeasurementPreviewMarkers();
            UpdateMeasurementPickMarker();
        }

        private void UpdateMeasurementPreviewLine()
        {
            if (_measurementPreviewLine == null)
                return;

            MeasurementLineData? preview = _drawingViewModel.MeasurementPreview;
            if (preview == null)
            {
                _measurementPreviewLine.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateMeasurementLine(
                _measurementPreviewLine,
                preview.Start,
                preview.End);
        }

        private void UpdateMeasurementPreviewMarkers()
        {
            if (_measurementPreviewStartMarker == null ||
                _measurementPreviewEndMarker == null)
            {
                return;
            }

            MeasurementLineData? preview = _drawingViewModel.MeasurementPreview;
            if (preview == null)
            {
                _measurementPreviewStartMarker.Visibility = Visibility.Collapsed;
                _measurementPreviewEndMarker.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateMeasurementMarker(_measurementPreviewStartMarker, preview.Start);
            double dx = preview.End.X - preview.Start.X;
            double dy = preview.End.Y - preview.Start.Y;
            double dz = preview.End.Z - preview.Start.Z;
            if (dx * dx + dy * dy + dz * dz <= 1e-12d)
            {
                _measurementPreviewEndMarker.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateMeasurementMarker(_measurementPreviewEndMarker, preview.End);
        }

        private void UpdateMeasurementPickMarker()
        {
            if (_measurementPickMarker == null)
                return;

            DomainPoint3D? point = _drawingViewModel.MeasurementMarkerPoint;
            if (!_drawingViewModel.IsMeasuring || point == null)
            {
                _measurementPickMarker.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateMeasurementMarker(_measurementPickMarker, point);
        }

        private void UpdateMeasurementMarker(
            FrameworkElement marker,
            DomainPoint3D point)
        {
            if (!TryProjectToOverlay(
                    IfcViewer,
                    new Point3D(point.X, point.Y, point.Z),
                    out Point screen,
                    out double depth) ||
                depth <= 0)
            {
                marker.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.SetLeft(marker, screen.X - marker.Width / 2d);
            Canvas.SetTop(marker, screen.Y - marker.Height / 2d);
            marker.Visibility = Visibility.Visible;
        }

        private void UpdateMeasurementLine(
            System.Windows.Shapes.Line line,
            DomainPoint3D start,
            DomainPoint3D end)
        {
            if (!TryProjectToOverlay(
                    IfcViewer,
                    new Point3D(start.X, start.Y, start.Z),
                    out Point screenStart,
                    out _) ||
                !TryProjectToOverlay(
                    IfcViewer,
                    new Point3D(end.X, end.Y, end.Z),
                    out Point screenEnd,
                    out _))
            {
                line.Visibility = Visibility.Collapsed;
                return;
            }

            line.X1 = screenStart.X;
            line.Y1 = screenStart.Y;
            line.X2 = screenEnd.X;
            line.Y2 = screenEnd.Y;
            line.Visibility = Visibility.Visible;
        }

        private void UpdateMeasurementLabelPositions()
        {
            double viewportWidth = IfcViewer.ActualWidth;
            double viewportHeight = IfcViewer.ActualHeight;
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                foreach ((_, Border label) in _measurementLabels)
                    label.Visibility = Visibility.Collapsed;
                return;
            }

            foreach ((MeasurementVisualData data, Border label) in _measurementLabels)
            {
                var anchor = new Point3D(
                    (data.Start.X + data.End.X) / 2.0,
                    (data.Start.Y + data.End.Y) / 2.0,
                    (data.Start.Z + data.End.Z) / 2.0);

                if (!TryProjectToOverlay(
                        IfcViewer,
                        anchor,
                        out Point screen,
                        out double depth) ||
                    depth <= 0 ||
                    screen.X < 0 ||
                    screen.Y < 0 ||
                    screen.X > viewportWidth ||
                    screen.Y > viewportHeight)
                {
                    label.Visibility = Visibility.Collapsed;
                    continue;
                }

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size labelSize = label.DesiredSize;
                double left = Math.Clamp(
                    screen.X - labelSize.Width / 2.0,
                    0,
                    Math.Max(0, viewportWidth - labelSize.Width));
                double top = Math.Clamp(
                    screen.Y - labelSize.Height - 6,
                    0,
                    Math.Max(0, viewportHeight - labelSize.Height));

                Canvas.SetLeft(label, left);
                Canvas.SetTop(label, top);
                label.Visibility = Visibility.Visible;
            }
        }

        private static bool TryProjectToOverlay(
            HelixViewport3D viewport,
            Point3D world,
            out Point screen,
            out double depth)
        {
            screen = default;
            depth = default;
            if (viewport.Camera is not ProjectionCamera camera ||
                !IsFinite(world) ||
                camera.LookDirection.LengthSquared < 1e-12)
            {
                return false;
            }

            Vector3D forward = camera.LookDirection;
            forward.Normalize();
            depth = Vector3D.DotProduct(
                world - camera.Position,
                forward);
            if (!double.IsFinite(depth) || depth <= 0)
                return false;

            try
            {
                Point inner = Viewport3DHelper.Point3DtoPoint2D(
                    viewport.Viewport,
                    world);
                screen = viewport.Viewport.TranslatePoint(
                    inner,
                    viewport);
                return double.IsFinite(screen.X) &&
                       double.IsFinite(screen.Y);
            }
            catch
            {
                return false;
            }
        }

        private void IfcViewer_CameraChanged(object sender, RoutedEventArgs e)
        {
            AdvanceOsnapCameraVersion();
            UpdateMeasurementOverlayPositions();

            if (_isUpdatingCameraMode)
                return;

            if (sender is not HelixViewport3D viewport)
                return;

            var camera = viewport.Camera;
            if (camera == null)
                return;

            var look = camera.LookDirection;
            var isNearVertical = Math.Abs(look.X) < 1e-2 && Math.Abs(look.Y) < 1e-2;

            if (isNearVertical)
            {
                if (!_cameraService.IsTopPlanViewActive)
                {
                    _isUpdatingCameraMode = true;
                    try
                    {
                        var elements = _sceneService.GetElements();
                        _cameraService.ZoomToTopView(elements, animate: true);
                    }
                    finally
                    {
                        _isUpdatingCameraMode = false;
                    }
                }
            }
            else
            {
                if (_cameraService.IsTopPlanViewActive)
                {
                    _isUpdatingCameraMode = true;
                    try
                    {
                        _cameraService.IsTopPlanViewActive = false;
                        viewport.Orthographic = false;

                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] Exited Top Plan View");
                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] Position: ({camera.Position.X:F3}, {camera.Position.Y:F3}, {camera.Position.Z:F3})");
                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] LookDirection: ({camera.LookDirection.X:F3}, {camera.LookDirection.Y:F3}, {camera.LookDirection.Z:F3})");
                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] UpDirection: ({camera.UpDirection.X:F3}, {camera.UpDirection.Y:F3}, {camera.UpDirection.Z:F3})");
                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] ProjectionMode: Perspective");
                        System.Diagnostics.Debug.WriteLine($"[Camera Diagnosis] IsTopPlanViewActive: False");
                    }
                    finally
                    {
                        _isUpdatingCameraMode = false;
                    }
                }
            }
        }

        public void ApplyRenderQualitySelection(string? qualityTag)
        {
            var selectedQuality = qualityTag switch
            {
                "High" => ElementVisualBuilder.MeshQuality.High,
                "Low" => ElementVisualBuilder.MeshQuality.Low,
                _ => ElementVisualBuilder.MeshQuality.Medium
            };

            var qualityChanged = ElementVisualBuilder.CurrentQuality != selectedQuality;
            ElementVisualBuilder.CurrentQuality = selectedQuality;

            if (DataContext is not MainViewModel vm)
                return;

            vm.RenderQualityLabel = GetRenderQualityDisplayName(selectedQuality);

            if (!qualityChanged)
                return;

            ElementVisualBuilder.ClearMeshCache("render quality changed");
            vm.ReRenderElements();
            _cameraService.UpdateClipping(_sceneService.GetElements());
            vm.StatusText = $"Render quality: {GetRenderQualityDescription(selectedQuality)}";
        }

        private static string GetRenderQualityDisplayName(ElementVisualBuilder.MeshQuality quality)
        {
            return quality switch
            {
                ElementVisualBuilder.MeshQuality.High => "High",
                ElementVisualBuilder.MeshQuality.Low => "Low",
                _ => "Medium"
            };
        }

        private static string GetRenderQualityDescription(ElementVisualBuilder.MeshQuality quality)
        {
            return quality switch
            {
                ElementVisualBuilder.MeshQuality.High => "High - detailed IFC meshes",
                ElementVisualBuilder.MeshQuality.Low => "Low - batched bounding boxes",
                _ => "Medium - simplified IFC meshes"
            };
        }

        public double CurrentGridWidthMeters => _gridWidthMeters;

        public double CurrentGridLengthMeters => _gridLengthMeters;

        public string CurrentGridWidthDisplayText => FormatGridDisplayValue(_gridWidthMeters);

        public string CurrentGridLengthDisplayText => FormatGridDisplayValue(_gridLengthMeters);

        public string CurrentGridUnitSymbol => UnitSystem.LengthSymbol(_unitSettingsService.Current.DisplayLengthUnit);

        public bool IsGridVisible => _renderingService.IsGridVisible;

        public bool ApplyManualGridSize(string widthText, string lengthText)
        {
            if (!TryReadGridSizeMeters(widthText, lengthText, out var widthMeters, out var lengthMeters))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusText = "Grid size must be positive finite numbers.";
                }
                return false;
            }

            _manualGridSize = true;
            _renderingService.SetGridSize(
                widthMeters,
                lengthMeters,
                _gridCenterXMeters,
                _gridCenterYMeters);
            SetGridState(widthMeters, lengthMeters);

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.StatusText = $"Grid size set to {FormatGridLength(widthMeters)} x {FormatGridLength(lengthMeters)}.";
            }

            return true;
        }

        public void AutoSizeGrid()
        {
            _manualGridSize = false;
            OnElementsChanged(_sceneService.GetElements());

            if (DataContext is MainViewModel vm)
            {
                vm.StatusText = "Grid size set from building extents.";
            }
        }

        public void SetGridVisible(bool isVisible)
            => _renderingService.SetGridVisible(isVisible);

        private bool TryReadGridSizeMeters(
            string widthText,
            string lengthText,
            out double widthMeters,
            out double lengthMeters)
        {
            var hasWidth = double.TryParse(widthText, out widthMeters);
            var hasLength = double.TryParse(lengthText, out lengthMeters);

            if (hasWidth)
                widthMeters = UnitSystem.ToMeters(widthMeters, _unitSettingsService.Current.DisplayLengthUnit);
            if (hasLength)
                lengthMeters = UnitSystem.ToMeters(lengthMeters, _unitSettingsService.Current.DisplayLengthUnit);

            return hasWidth && hasLength &&
                   double.IsFinite(widthMeters) && double.IsFinite(lengthMeters) &&
                   widthMeters > 0 && lengthMeters > 0;
        }

        private void SetGridState(double widthMeters, double lengthMeters)
        {
            _gridWidthMeters = widthMeters;
            _gridLengthMeters = lengthMeters;
        }

        private string FormatGridDisplayValue(double meters)
            => UnitSystem.FromMeters(meters, _unitSettingsService.Current.DisplayLengthUnit).ToString("0.###");

        private string FormatGridLength(double meters)
            => UnitSystem.FormatLength(meters, _unitSettingsService.Current);

        private static string BuildElementSceneKey(IReadOnlyList<PrecastElement> elements)
        {
            unchecked
            {
                var hash = 17;
                foreach (var element in elements)
                {
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(element.ElementID ?? string.Empty);
                    hash = (hash * 31) + element.ElementCOG.X.GetHashCode();
                    hash = (hash * 31) + element.ElementCOG.Y.GetHashCode();
                    hash = (hash * 31) + element.ElementCOG.Z.GetHashCode();
                    hash = (hash * 31) + (element.BBoxLength ?? 0d).GetHashCode();
                    hash = (hash * 31) + (element.BBoxWidth ?? 0d).GetHashCode();
                    hash = (hash * 31) + (element.BBoxHeight ?? 0d).GetHashCode();
                }

                return $"{elements.Count}:{hash}";
            }
        }

        private sealed record HoverRequestContext(
            long RequestId,
            DomainPoint3D? RawPoint,
            bool CapturedShift);
    }
}
