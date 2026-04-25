using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;

            DataContext = vm;

            IfcViewer.Initialize(renderingService, sceneService);

            //#region DXF
            //// Wire the DXF uploader to the same Helix viewport.
            //// Mirrors the IRenderingService pattern  must be done after InitializeComponent.
            //vm.Dxf.Initialize(IfcViewer);
            //#endregion
        }

        private MainViewModel? VM => DataContext as MainViewModel;



        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }



        #region Window Chrome + UI Events (required by XAML)

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BackToWelcome_Click(object sender, RoutedEventArgs e) { }
        private void SaveProject_Click(object sender, RoutedEventArgs e) { }
        private void UndoBtn_Click(object sender, RoutedEventArgs e) { }
        private void RedoBtn_Click(object sender, RoutedEventArgs e) { }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { }
        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e) { }
        private void ElementItem_Click(object sender, MouseButtonEventArgs e) { }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            //IfcViewer?.CameraController?.ResetCamera();
        }

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) { }

        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
        {
            RenderQualityPopup.IsOpen = true;
        }

        private void RenderQuality_Changed(object sender, RoutedEventArgs e) { }

        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
        {
            OsnapPopup.IsOpen = true;
        }

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e) { }
        private void StatusOsnap_Click(object sender, RoutedEventArgs e) { }

        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }

        #endregion


    }
}
