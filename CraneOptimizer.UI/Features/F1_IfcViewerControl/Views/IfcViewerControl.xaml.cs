using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Microsoft.Extensions.DependencyInjection;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Views;
using CraneOptimizer.UI.Features.F5_ElementManagement.Services;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.Interaction;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.Features.F1_IfcViewerControl.Views
{
    public partial class IfcViewerControl : UserControl
    {
        private readonly IRenderingService _renderingService = null!;
        private readonly ISceneService _sceneService = null!;
        private readonly IElementGridSizingService _elementGridSizingService = null!;
        private readonly IViewportInteractionService _interactionService = null!;
        private readonly IViewportHitTestService _hitTestService = null!;
        private readonly ICameraService _cameraService = null!;
        private readonly IOsnapService _osnapService = null!;
        private readonly OsnapPanelViewModel _osnapVm = null!;
        private readonly OsnapGeometryBridge _osnapBridge = null!;
        private Window? _ownerWindow;

        private bool _manualGridSize;
        private double _gridCenterXMillimeters;
        private double _gridCenterYMillimeters;
        private string _lastElementSceneKey = string.Empty;

        private OsnapIndicator? _osnapIndicator;

        public IfcViewerControl()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            _renderingService = App.ServiceProvider.GetRequiredService<IRenderingService>();
            _sceneService = App.ServiceProvider.GetRequiredService<ISceneService>();
            _elementGridSizingService = App.ServiceProvider.GetRequiredService<IElementGridSizingService>();
            _interactionService = App.ServiceProvider.GetRequiredService<IViewportInteractionService>();
            _hitTestService = App.ServiceProvider.GetRequiredService<IViewportHitTestService>();
            _cameraService = App.ServiceProvider.GetRequiredService<ICameraService>();
            _osnapService = App.ServiceProvider.GetRequiredService<IOsnapService>();
            _osnapVm = App.ServiceProvider.GetRequiredService<OsnapPanelViewModel>();
            _osnapBridge = App.ServiceProvider.GetRequiredService<OsnapGeometryBridge>();

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

            if (DataContext is MainViewModel vm)
            {
                _interactionService.CurrentMode = new InteractionRouter(
                    vm,
                    _hitTestService,
                    () => _cameraService.UpdateClipping(_sceneService.GetElements()));
                vm.Dxf.Initialize(IfcViewer);
            }

            SetGridText(50d, 50d);
            _cameraService.UpdateClipping(_sceneService.GetElements());
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
                var (widthMeters, lengthMeters, centerXMillimeters, centerYMillimeters) =
                    _elementGridSizingService.CalculateGridSizeMeters(elements);
                _gridCenterXMillimeters = centerXMillimeters;
                _gridCenterYMillimeters = centerYMillimeters;
                SetGridText(widthMeters, lengthMeters);
                _renderingService.SetGridSize(
                    widthMeters * 1000.0,
                    lengthMeters * 1000.0,
                    _gridCenterXMillimeters,
                    _gridCenterYMillimeters);
            }

            _cameraService.UpdateClipping(elements);
            if (elements.Count == 0)
            {
                _lastElementSceneKey = string.Empty;
                return;
            }

            var elementSceneKey = BuildElementSceneKey(elements);
            if (elementSceneKey != _lastElementSceneKey)
            {
                _lastElementSceneKey = elementSceneKey;
                _cameraService.ZoomExtents();
            }
        }

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not HelixViewport3D viewport) return;

            var screen = e.GetPosition(viewport);

            if (_osnapIndicator != null)
            {
                var result = _osnapService.Snap(screen, viewport);
                _osnapIndicator.Update(result);
            }

            _interactionService.HandleMouseMove(viewport, e);
        }

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
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            _cameraService.ResetCamera();
            _cameraService.UpdateClipping(_sceneService.GetElements());
        }

        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
        {
            RenderQualityPopup.IsOpen = true;
        }

        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (rbRenderHigh.IsChecked == true)
            {
                ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.High;
                vm.RenderQualityLabel = "HIGH";
            }
            else
            {
                ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Standard;
                vm.RenderQualityLabel = "STD";
            }

            vm.ReRenderElements();
            _cameraService.UpdateClipping(_sceneService.GetElements());
            vm.StatusText = $"Render quality: {vm.RenderQualityLabel}";
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
                widthMeters * 1000.0,
                lengthMeters * 1000.0,
                _gridCenterXMillimeters,
                _gridCenterYMillimeters);

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.StatusText = $"Grid size set to {widthMeters:F0}m x {lengthMeters:F0}m.";
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

            return hasWidth && hasLength && widthMeters > 0 && lengthMeters > 0;
        }

        private void SetGridText(double widthMeters, double lengthMeters)
        {
            GridSizeXTextBox.Text = widthMeters.ToString("0");
            GridSizeYTextBox.Text = lengthMeters.ToString("0");
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
