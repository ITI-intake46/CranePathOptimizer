using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
using CraneOptimizer.UI.Shell.Interaction;
using CraneOptimizer.UI.Shell.ViewModels;

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
        private Window? _ownerWindow;
        private MainViewModel? _osnapDxfSource;

        private bool _manualGridSize;
        private double _gridCenterXMeters;
        private double _gridCenterYMeters;
        private double _gridWidthMeters = 50d;
        private double _gridLengthMeters = 50d;
        private string _lastElementSceneKey = string.Empty;
        private bool _pendingWheelClipUpdate;

        private OsnapIndicator? _osnapIndicator;

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

            OsnapPanel.DataContext = _osnapVm;
            _osnapIndicator = new OsnapIndicator(OsnapOverlayCanvas);

            _renderingService.Initialize(IfcViewer);
            _osnapBridge.AttachViewport(IfcViewer);

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
            _unitSettingsService.SettingsChanged += OnUnitSettingsChanged;

            if (DataContext is MainViewModel vm)
            {
                _interactionService.ResetToDefaultMode(new DefaultSelectionMode(
                    vm,
                    _hitTestService,
                    () => _cameraService.UpdateClipping(_sceneService.GetElements())));
                vm.Dxf.Initialize(IfcViewer);
                AttachDxfSnapBridge(vm);
            }

            SetGridText(50d, 50d);
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
            _unitSettingsService.SettingsChanged -= OnUnitSettingsChanged;
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
                SetGridText(widthMeters, lengthMeters);
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

            _interactionService.HandleMouseMove(viewport, e);
        }

        private static bool IsCameraOrDragMouseMove(MouseEventArgs e)
            => e.LeftButton == MouseButtonState.Pressed ||
               e.MiddleButton == MouseButtonState.Pressed ||
               e.RightButton == MouseButtonState.Pressed;

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HelixViewport3D viewport)
                _interactionService.HandleMouseDown(viewport, e);
        }

        private void IfcViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is HelixViewport3D viewport)
                _interactionService.HandleMouseUp(viewport, e);
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
        {
            var elements = _sceneService.GetElements();
            if (elements.Count == 0)
                _cameraService.ResetToDefaultView();
            else
                _cameraService.ZoomToElements(elements);
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

        private void ApplyGridSize_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadGridSizeMeters(out var widthMeters, out var lengthMeters))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusText = "Grid size must be positive numbers in meters.";
                }
                return;
            }

            _manualGridSize = true;
            _renderingService.SetGridSize(
                widthMeters,
                lengthMeters,
                _gridCenterXMeters,
                _gridCenterYMeters);
            SetGridText(widthMeters, lengthMeters);

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.StatusText = $"Grid size set to {FormatGridLength(widthMeters)} x {FormatGridLength(lengthMeters)}.";
            }
        }

        private void AutoGridSize_Click(object sender, RoutedEventArgs e)
        {
            _manualGridSize = false;
            OnElementsChanged(_sceneService.GetElements());

            if (DataContext is MainViewModel vm)
            {
                vm.StatusText = "Grid size set from building extents.";
            }
        }

        private bool TryReadGridSizeMeters(out double widthMeters, out double lengthMeters)
        {
            var hasWidth = double.TryParse(GridSizeXTextBox.Text, out widthMeters);
            var hasLength = double.TryParse(GridSizeYTextBox.Text, out lengthMeters);

            if (hasWidth)
                widthMeters = UnitSystem.ToMeters(widthMeters, _unitSettingsService.Current.DisplayLengthUnit);
            if (hasLength)
                lengthMeters = UnitSystem.ToMeters(lengthMeters, _unitSettingsService.Current.DisplayLengthUnit);

            return hasWidth && hasLength && widthMeters > 0 && lengthMeters > 0;
        }

        private void SetGridText(double widthMeters, double lengthMeters)
        {
            _gridWidthMeters = widthMeters;
            _gridLengthMeters = lengthMeters;
            var settings = _unitSettingsService.Current;
            GridUnitLabel.Text = $"GRID {UnitSystem.LengthSymbol(settings.DisplayLengthUnit)}";
            GridSizeXTextBox.ToolTip = $"Grid size on X axis in {UnitSystem.LengthSymbol(settings.DisplayLengthUnit)}";
            GridSizeYTextBox.ToolTip = $"Grid size on Y axis in {UnitSystem.LengthSymbol(settings.DisplayLengthUnit)}";
            GridSizeXTextBox.Text = UnitSystem.FromMeters(widthMeters, settings.DisplayLengthUnit).ToString("0.###");
            GridSizeYTextBox.Text = UnitSystem.FromMeters(lengthMeters, settings.DisplayLengthUnit).ToString("0.###");
        }

        private string FormatGridLength(double meters)
            => UnitSystem.FormatLength(meters, _unitSettingsService.Current);

        private void OnUnitSettingsChanged(object? sender, UnitSettings settings)
        {
            SetGridText(_gridWidthMeters, _gridLengthMeters);
        }

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
