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

        // â”€â”€ Back to startup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                    "â€¢ Yes â€” go back (your project stays open in the background)\n" +
                    "â€¢ No  â€” stay in the project",
                    "Return to Home",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            // Hide this window (don't close â€” the session is still in memory)
            Hide();

            // Show the startup screen; pass the active session so it can show a "Return" button
            _nav.NavigateToStartup(session);
        }

        // â”€â”€ Stub handlers (wired in XAML) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void SaveProject_Click(object sender, RoutedEventArgs e) { }
        private void UndoBtn_Click(object sender, RoutedEventArgs e) { }
        private void RedoBtn_Click(object sender, RoutedEventArgs e) { }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { }
        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e) { }
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }
        private void OpenAiAssistTab()
        {
            AiAssistTab.Visibility = Visibility.Visible;
            RightWorkspaceTabs.SelectedItem = AiAssistTab;
            ShowAiCompanionButton.Visibility = Visibility.Collapsed;
        }

        private void CloseAiAssistTab_Click(object sender, RoutedEventArgs e)
        {
            AiAssistTab.Visibility = Visibility.Collapsed;
            RightWorkspaceTabs.SelectedIndex = 0;
        }

        private void AiCompanionPanel_CloseRequested(object sender, RoutedEventArgs e)
        {
            CloseAiAssistTab_Click(sender, e);
        }

        private void AiCompanionStatusStrip_AssistRequested(object sender, RoutedEventArgs e)
        {
            OpenAiAssistTab();
        }

        private void AiCompanionStatusStrip_HideRequested(object sender, RoutedEventArgs e)
        {
            AiCompanionStrip.Visibility = Visibility.Collapsed;
            ShowAiCompanionButton.Visibility = Visibility.Visible;
        }

        private void ShowAiCompanionButton_Click(object sender, RoutedEventArgs e)
        {
            AiCompanionStrip.Visibility = Visibility.Visible;
            OpenAiAssistTab();
        }
    }
}
