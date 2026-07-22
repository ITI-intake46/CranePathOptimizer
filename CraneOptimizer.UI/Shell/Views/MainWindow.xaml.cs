using CraneOptimizer.UI.AIIntegration;
using CraneOptimizer.UI.Core.Helpers;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.F10_ProjectManagement.Views;
using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private const double CranePropertiesMinWidth = 330;
        private const double CranePropertiesMaxWidth = 380;
        private const double CranePropertiesDefaultWidth = 360;
        private const double AiAssistMinWidth = 400;
        private const double AiAssistPreferredWidth = 400;
        private const double AiAssistMaxWidth = 480;

        private readonly IProjectNavigationService _nav;
        private GridLength _rightWorkspaceWidthBeforeAi = new(CranePropertiesDefaultWidth);
        private bool _isAiAssistWidthActive;

        public MainWindow(MainViewModel vm, IProjectNavigationService nav, AiHostContextBridge aiContextBridge)
        {
            InitializeComponent();
            DataContext = vm;
            _nav = nav;
            aiContextBridge.Attach(vm);
            AddHandler(
                HomeTabView.ProjectSettingsRequestedEvent,
                new RoutedEventHandler(OnProjectSettingsRequested));
            MaximizeTaskbarHelper.Attach(this, MaximizeButton);
            // Apply initial maximize after source is initialized
            Loaded += (_, _) => MaximizeTaskbarHelper.ToggleMaximize(this, MaximizeButton);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
            => MaximizeTaskbarHelper.ToggleMaximize(this, MaximizeButton);

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private void OnProjectSettingsRequested(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (DataContext is not MainViewModel viewModel)
                return;

            var settingsWindow = new ProjectSettingsWindow(viewModel, IfcViewerHost)
            {
                Owner = this
            };

            settingsWindow.ShowDialog();
        }

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
            ApplyAiAssistWorkspaceWidth();
            ShowAiCompanionButton.Visibility = Visibility.Collapsed;
        }

        private void CloseAiAssistTab_Click(object sender, RoutedEventArgs e)
        {
            AiAssistTab.Visibility = Visibility.Collapsed;
            RightWorkspaceTabs.SelectedIndex = 0;
            RestoreCranePropertiesWorkspaceWidth();
        }

        private void RightWorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || !ReferenceEquals(e.Source, RightWorkspaceTabs))
                return;

            if (AiAssistTab.Visibility == Visibility.Visible
                && ReferenceEquals(RightWorkspaceTabs.SelectedItem, AiAssistTab))
            {
                ApplyAiAssistWorkspaceWidth();
                return;
            }

            RestoreCranePropertiesWorkspaceWidth();
        }

        private void ApplyAiAssistWorkspaceWidth()
        {
            if (!_isAiAssistWidthActive)
            {
                var currentWidth = RightWorkspaceColumn.ActualWidth > 0
                    ? RightWorkspaceColumn.ActualWidth
                    : CranePropertiesDefaultWidth;

                _rightWorkspaceWidthBeforeAi = new GridLength(
                    Math.Clamp(currentWidth, CranePropertiesMinWidth, CranePropertiesMaxWidth));
            }

            RightWorkspaceColumn.MinWidth = AiAssistMinWidth;
            RightWorkspaceColumn.MaxWidth = AiAssistMaxWidth;

            var requestedWidth = Math.Max(RightWorkspaceColumn.ActualWidth, AiAssistPreferredWidth);
            RightWorkspaceColumn.Width = new GridLength(
                Math.Clamp(requestedWidth, AiAssistMinWidth, AiAssistMaxWidth));
            _isAiAssistWidthActive = true;
        }

        private void RestoreCranePropertiesWorkspaceWidth()
        {
            if (!_isAiAssistWidthActive)
                return;

            RightWorkspaceColumn.MinWidth = CranePropertiesMinWidth;
            RightWorkspaceColumn.MaxWidth = CranePropertiesMaxWidth;

            var restoredWidth = _rightWorkspaceWidthBeforeAi.IsAbsolute
                ? _rightWorkspaceWidthBeforeAi.Value
                : CranePropertiesDefaultWidth;
            RightWorkspaceColumn.Width = new GridLength(
                Math.Clamp(restoredWidth, CranePropertiesMinWidth, CranePropertiesMaxWidth));
            _isAiAssistWidthActive = false;
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
