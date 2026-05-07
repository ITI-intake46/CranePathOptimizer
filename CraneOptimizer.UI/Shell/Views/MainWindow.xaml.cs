using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
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
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }
    }
}
