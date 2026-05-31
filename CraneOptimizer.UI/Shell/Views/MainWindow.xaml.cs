using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IProjectNavigationService _nav;

        public MainWindow(MainViewModel vm, IProjectNavigationService nav)
        {
            InitializeComponent();
            DataContext = vm;
            _nav = nav;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        // ── Back to startup ─────────────────────────────────────────────

        private void BackToWelcome_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var session = vm?.CurrentSession;

            if (session != null)
            {
                // Ask the user what to do
                var result = MessageBox.Show(
                    $"You are currently working on \"{session.ProjectName}\".\n\n" +
                    "Would you like to leave and go back to the home screen?\n\n" +
                    "• Yes — go back (your project stays open in the background)\n" +
                    "• No  — stay in the project",
                    "Return to Home",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            // Hide this window (don't close — the session is still in memory)
            Hide();

            // Show the startup screen; pass the active session so it can show a "Return" button
            _nav.NavigateToStartup(session);
        }

        // ── Stub handlers (wired in XAML) ───────────────────────────────

        private void SaveProject_Click(object sender, RoutedEventArgs e) { }
        private void UndoBtn_Click(object sender, RoutedEventArgs e) { }
        private void RedoBtn_Click(object sender, RoutedEventArgs e) { }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { }
        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e) { }
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }
    }
}
