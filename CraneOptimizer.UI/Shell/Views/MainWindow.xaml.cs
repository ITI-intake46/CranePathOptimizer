using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F5_ElementManagement.Services;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Views;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly IElementGridSizingService _elementGridSizingService;
        private readonly IViewportInteractionService _interactionService;
        private bool _manualGridSize;
        private double _gridCenterXMillimeters;
        private double _gridCenterYMillimeters;
        private string _lastElementSceneKey = string.Empty;

        private const double MinNearPlane = 1d;
        private const double FarPlanePaddingFactor = 1.4;

        private const double DefaultGridSizeMeters = 50.0;
        private const double GridPaddingFactor = 1.2;
        private const double GridRoundStepMeters = 5.0;

        private readonly OsnapPanelViewModel _osnapVm;
        private readonly IOsnapService _osnapService;
        private readonly OsnapGeometryBridge _osnapBridge;
        private OsnapIndicator? _osnapIndicator;

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService,
            OsnapPanelViewModel osnapVm,
            IOsnapService osnapService,
            OsnapGeometryBridge osnapBridge,
            CraneSetupViewModel craneSetup,
            IElementGridSizingService elementGridSizingService)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;
            _elementGridSizingService = elementGridSizingService;

            _osnapVm = osnapVm;
            _osnapService = osnapService;
            _osnapBridge = osnapBridge;

            DataContext = vm;
            OsnapPanel.DataContext = osnapVm;
            _osnapIndicator = new OsnapIndicator(OsnapOverlayCanvas);

            var hitTest = new CraneOptimizer.UI.Core.Services.ViewportHitTestService();
            _interactionService = new CraneOptimizer.UI.Core.Services.ViewportInteractionService();
            _interactionService.CurrentMode = new CraneOptimizer.UI.Shell.Interaction.InteractionRouter(vm, hitTest, () => UpdateCameraClipping());

            _renderingService.Initialize(IfcViewer);
            _osnapBridge.AttachViewport(IfcViewer);

            vm.Dxf.Initialize(IfcViewer);
            vm.Dxf.DxfLoaded += uploader => _osnapBridge.RefreshDxf(uploader);

            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneSelected += OnZoneSelected;
            _sceneService.ZoneRemoved += OnZoneChanged;
            _sceneService.ZoneUpdated += OnZoneChanged;
            _sceneService.ZonesCleared += OnZonesCleared;

            _sceneService.ElementsChanged += OnElementsChanged;
            _sceneService.CraneSelected += OnCraneSelected;

            SetGridText(50d, 50d);
            UpdateCameraClipping();
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void OnZoneAdded(GreenAreaZone zone) => RenderZones();
        private void OnZoneSelected(GreenAreaZone? zone) => RenderZones();
        private void OnZoneChanged(GreenAreaZone zone) => RenderZones();
        private void OnZonesCleared() => RenderZones();

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

            UpdateCameraClipping(elements);
            if (elements.Count == 0)
            {
                _lastElementSceneKey = string.Empty;
                return;
            }

            var elementSceneKey = BuildElementSceneKey(elements);
            if (elementSceneKey != _lastElementSceneKey)
            {
                _lastElementSceneKey = elementSceneKey;
                _renderingService.ZoomExtents();
            }
        }

        private void RenderZones()
        {
            _renderingService.Render(_sceneService.GetZones(), new ZoneVisualBuilder(_sceneService), "Zones");
        }

        private void OnCraneSelected(SceneCrane? crane)
        {
            // future UI sync
        }

        protected override void OnClosed(EventArgs e)
        {
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneRemoved -= OnZoneChanged;
            _sceneService.ZoneUpdated -= OnZoneChanged;
            _sceneService.ZonesCleared -= OnZonesCleared;
            _sceneService.ElementsChanged -= OnElementsChanged;
            _sceneService.CraneSelected -= OnCraneSelected;

            base.OnClosed(e);
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

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IfcViewer != null)
                _interactionService.HandleKeyDown(IfcViewer, e);
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private void BackToWelcome_Click(object sender, RoutedEventArgs e) { }
        private void SaveProject_Click(object sender, RoutedEventArgs e) { }
        private void UndoBtn_Click(object sender, RoutedEventArgs e) { }
        private void RedoBtn_Click(object sender, RoutedEventArgs e) { }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { }
        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e) { }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            IfcViewer?.CameraController?.ResetCamera();
            UpdateCameraClipping();
        }

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            UpdateCameraClipping();
        }

        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
        {
            RenderQualityPopup.IsOpen = true;
        }

        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            if (rbRenderHigh.IsChecked == true)
            {
                ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.High;
                VM.RenderQualityLabel = "● HIGH";
            }
            else
            {
                ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Standard;
                VM.RenderQualityLabel = "● STD";
            }

            VM.ReRenderElements();
            UpdateCameraClipping();
            VM.StatusText = $"Render quality: {VM.RenderQualityLabel}";
        }

        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e) { }

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
                _osnapVm.MasterEnabled = tb.IsChecked == true;
        }

        private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string snapName
                && Enum.TryParse<OsnapType>(snapName, out var snapType))
                _osnapVm.SetSnapEnabled(snapType, btn.IsChecked == true);
        }

        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }

        private void ApplyGridSize_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadGridSizeMeters(out var widthMeters, out var lengthMeters))
            {
                VM!.StatusText = "Grid size must be positive numbers in meters.";
                return;
            }

            _manualGridSize = true;
            _renderingService.SetGridSize(
                widthMeters * 1000.0,
                lengthMeters * 1000.0,
                _gridCenterXMillimeters,
                _gridCenterYMillimeters);
            VM!.StatusText = $"Grid size set to {widthMeters:F0}m x {lengthMeters:F0}m.";
        }

        private void AutoGridSize_Click(object sender, RoutedEventArgs e)
        {
            _manualGridSize = false;
            OnElementsChanged(_sceneService.GetElements());
            VM!.StatusText = "Grid size set from building extents.";
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

        private static double CalculateGridAxisMeters(double buildingAxisMeters)
        {
            if (buildingAxisMeters <= DefaultGridSizeMeters)
                return DefaultGridSizeMeters;

            var padded = buildingAxisMeters * GridPaddingFactor;
            return Math.Ceiling(padded / GridRoundStepMeters) * GridRoundStepMeters;
        }

        private void UpdateCameraClipping(IReadOnlyList<PrecastElement>? elements = null)
        {
            var camera = IfcViewer?.Camera as PerspectiveCamera;
            if (camera == null)
                return;

            var source = elements ?? _sceneService.GetElements();
            if (source == null || source.Count == 0)
            {
                camera.NearPlaneDistance = MinNearPlane;
                camera.FarPlaneDistance = 2_000_000;
                return;
            }

            var minX = source.Min(e => e.ElementCOG.X - ((e.BBoxLength ?? 0d) / 2d));
            var maxX = source.Max(e => e.ElementCOG.X + ((e.BBoxLength ?? 0d) / 2d));
            var minY = source.Min(e => e.ElementCOG.Y - ((e.BBoxWidth ?? 0d) / 2d));
            var maxY = source.Max(e => e.ElementCOG.Y + ((e.BBoxWidth ?? 0d) / 2d));
            var minZ = source.Min(e => e.ElementCOG.Z - ((e.BBoxHeight ?? 0d) / 2d));
            var maxZ = source.Max(e => e.ElementCOG.Z + ((e.BBoxHeight ?? 0d) / 2d));

            var center = new Point3D((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
            var dx = maxX - minX;
            var dy = maxY - minY;
            var dz = maxZ - minZ;
            var radius = Math.Max(1000d, Math.Sqrt(dx * dx + dy * dy + dz * dz) * 0.5);

            var cameraDistance = (camera.Position - center).Length;
            camera.NearPlaneDistance = Math.Max(MinNearPlane, Math.Min(radius * 0.02, cameraDistance * 0.1));
            camera.FarPlaneDistance = Math.Max(camera.NearPlaneDistance * 1000, (cameraDistance + radius) * FarPlanePaddingFactor);
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
