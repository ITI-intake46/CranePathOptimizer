using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using Microsoft.Extensions.DependencyInjection;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Views;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
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
        private readonly OsnapPanelViewModel _osnapVm = null!;
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

        private OsnapIndicator? _osnapIndicator;
        private readonly List<(MeasurementVisualData Data, Border Label)> _measurementLabels = new();

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
            _osnapVm = App.ServiceProvider.GetRequiredService<OsnapPanelViewModel>();
            _osnapBridge = App.ServiceProvider.GetRequiredService<OsnapGeometryBridge>();
            _unitSettingsService = App.ServiceProvider.GetRequiredService<IUnitSettingsService>();
            _viewportSelectionService = App.ServiceProvider.GetRequiredService<IViewportSelectionService>();
            _selectionHandler = App.ServiceProvider.GetRequiredService<SelectionHandler>();
            _drawingViewModel = App.ServiceProvider.GetRequiredService<DrawingViewModel>();

            OsnapPanel.DataContext = _osnapVm;
            _osnapIndicator = new OsnapIndicator(OsnapOverlayCanvas);

            _renderingService.Initialize(IfcViewer);
            _osnapBridge.AttachViewport(IfcViewer);

            // Wire the 2D rubber-band overlay canvas to the selection handler.
            // Must be done after InitializeComponent() so SelectionCanvas is created.
            _selectionHandler.SetOverlayCanvas(SelectionCanvas);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ownerWindow = Window.GetWindow(this);
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown -= OwnerWindow_PreviewKeyDown;
                _ownerWindow.PreviewKeyDown += OwnerWindow_PreviewKeyDown;
            }

            _sceneService.ElementsChanged += OnElementsChanged;
            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneSelected += OnZoneSelected;
            _sceneService.ZoneRemoved += OnZoneChanged;
            _sceneService.ZoneUpdated += OnZoneChanged;
            _sceneService.ZonesCleared += OnZonesCleared;
            IfcViewer.CameraChanged += IfcViewer_CameraChanged;
            IfcViewer.SizeChanged += IfcViewer_SizeChanged;
            _drawingViewModel.MeasurementLabelsChanged += DrawingViewModel_MeasurementLabelsChanged;
            RebuildMeasurementLabels();

            if (DataContext is MainViewModel vm)
            {
                _interactionService.ResetToDefaultMode(new DefaultSelectionMode(
                    vm,
                    _hitTestService,
                    _viewportSelectionService,
                    () => _cameraService.UpdateClipping(_sceneService.GetElements())));
                vm.Dxf.Initialize(IfcViewer);
                AttachDxfSnapBridge(vm);
            }

            SetGridState(50d, 50d);
            var elements = _sceneService.GetElements();
            if (elements.Count == 0)
                _cameraService.ResetToDefaultView();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown -= OwnerWindow_PreviewKeyDown;
                _ownerWindow = null;
            }

            _sceneService.ElementsChanged -= OnElementsChanged;
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneRemoved -= OnZoneChanged;
            _sceneService.ZoneUpdated -= OnZoneChanged;
            _sceneService.ZonesCleared -= OnZonesCleared;
            IfcViewer.CameraChanged -= IfcViewer_CameraChanged;
            IfcViewer.SizeChanged -= IfcViewer_SizeChanged;
            _drawingViewModel.MeasurementLabelsChanged -= DrawingViewModel_MeasurementLabelsChanged;
            MeasurementOverlayCanvas.Children.Clear();
            _measurementLabels.Clear();
            DetachDxfSnapBridge();
        }

        private void AttachDxfSnapBridge(MainViewModel vm)
        {
            if (!ReferenceEquals(_osnapDxfSource, vm))
            {
                DetachDxfSnapBridge();
                _osnapDxfSource = vm;
                vm.Dxf.DxfLoaded += _osnapBridge.RefreshDxf;
            }

            if (vm.Dxf.CurrentUploader?.IsLoaded == true)
                _osnapBridge.RefreshDxf(vm.Dxf.CurrentUploader);
        }

        private void DetachDxfSnapBridge()
        {
            if (_osnapDxfSource == null)
                return;

            _osnapDxfSource.Dxf.DxfLoaded -= _osnapBridge.RefreshDxf;
            _osnapDxfSource = null;
        }

        private void OwnerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnKeyDown(IfcViewer, e);
            if (e.Handled)
                return;

            _interactionService.HandleKeyDown(IfcViewer, e);
        }

        private void OnZoneAdded(GreenAreaZone zone) => RenderZones();
        private void OnZoneSelected(GreenAreaZone? zone) => RenderZones();
        private void OnZoneChanged(GreenAreaZone zone) => RenderZones();
        private void OnZonesCleared() => RenderZones();

        private void RenderZones()
        {
            _renderingService.Render(_sceneService.GetZones(), new ZoneVisualBuilder(_sceneService), "Zones");
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
                    return;

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
            if (sender is not HelixViewport3D viewport) return;

            var screen = e.GetPosition(viewport);

            if (_osnapIndicator != null)
            {
                if (IsCameraOrDragMouseMove(e))
                {
                    _osnapIndicator.Hide();
                }
                else
                {
                    var result = _osnapService.Snap(screen, viewport);
                    _osnapIndicator.Update(result);
                }
            }

            if (DataContext is MainViewModel vm)
            {
                if (_hitTestService.TryGetWorldPoint(viewport, screen, out var worldPt))
                {
                    vm.SetCoordinates(worldPt.X, worldPt.Y);
                }
            }

            UpdateSelectionHandlerEnabled();
            _selectionHandler.OnMouseMove(viewport, e);
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

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HelixViewport3D viewport)
            {
                UpdateSelectionHandlerEnabled();
                _selectionHandler.OnMouseDown(viewport, e);
                _interactionService.HandleMouseDown(viewport, e);
            }
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

        private void IfcViewer_SizeChanged(object sender, SizeChangedEventArgs e)
            => UpdateMeasurementLabelPositions();

        private void RebuildMeasurementLabels()
        {
            MeasurementOverlayCanvas.Children.Clear();
            _measurementLabels.Clear();

            foreach (MeasurementVisualData measurement in _drawingViewModel.MeasurementSegments)
            {
                var text = new TextBlock
                {
                    Text = measurement.LabelText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.WhiteSmoke
                };
                var label = new Border
                {
                    Child = text,
                    Padding = new Thickness(5, 2.5, 5, 2.5),
                    Background = new SolidColorBrush(Color.FromArgb(205, 28, 30, 33)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(140, 145, 155, 163)),
                    BorderThickness = new Thickness(0.6),
                    CornerRadius = new CornerRadius(2),
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    Visibility = Visibility.Collapsed
                };

                MeasurementOverlayCanvas.Children.Add(label);
                _measurementLabels.Add((measurement, label));
            }

            UpdateMeasurementLabelPositions();
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

                if (!OsnapProjection.TryGetCameraDepth(IfcViewer, anchor, out double depth) ||
                    depth <= 0 ||
                    !OsnapProjection.TryProject(IfcViewer, anchor, out Point screen) ||
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
                    screen.Y - labelSize.Height - 10,
                    0,
                    Math.Max(0, viewportHeight - labelSize.Height));

                Canvas.SetLeft(label, left);
                Canvas.SetTop(label, top);
                label.Visibility = Visibility.Visible;
            }
        }

        private void IfcViewer_CameraChanged(object sender, RoutedEventArgs e)
        {
            UpdateMeasurementLabelPositions();

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

        private void RenderQualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RenderQualityCombo.SelectedItem is not ComboBoxItem item)
                return;

            var selectedQuality = item.Tag?.ToString() switch
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
    }
}
