using CraneOptimizer.AI.Host;
using CraneOptimizer.UI.AIIntegration;
using CraneOptimizer.UI.Core.Helpers;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.F10_ProjectManagement.Views;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IProjectNavigationService _nav;
        private readonly IUiInteractionContextService _uiContext;
        private readonly IProjectHistoryService _history;

        public MainWindow(
            MainViewModel vm,
            IProjectNavigationService nav,
            AiHostContextBridge aiContextBridge,
            IUiInteractionContextService uiContext,
            OsnapPanelViewModel osnapViewModel,
            IProjectHistoryService history)
        {
            InitializeComponent();
            DataContext = vm;
            ShellOsnapPanel.DataContext = osnapViewModel;
            _nav = nav;
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _uiContext.ActionRequested += OnAiActionRequested;
            _history.StateChanged += OnHistoryStateChanged;
            Closed += OnClosed;
            aiContextBridge.Attach(vm);
            AddHandler(
                HomeTabView.ProjectSettingsRequestedEvent,
                new RoutedEventHandler(OnProjectSettingsRequested));
            MaximizeTaskbarHelper.Attach(this, MaximizeButton);
            OnHistoryStateChanged();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            // Apply initial maximize after source is initialized
            Loaded += (_, _) => MaximizeTaskbarHelper.ToggleMaximize(this, MaximizeButton);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _uiContext.ActionRequested -= OnAiActionRequested;
            _history.StateChanged -= OnHistoryStateChanged;
            PreviewKeyDown -= MainWindow_PreviewKeyDown;
            PreviewMouseDown -= MainWindow_PreviewMouseDown;
        }

        private void OnAiActionRequested(object? sender, AiUiActionRequestedEventArgs e)
        {
            _ = Dispatcher.InvokeAsync(StopMeasurementIfActive);

            if (!string.Equals(e.ActionType, "NavigateTab", StringComparison.OrdinalIgnoreCase)
                || !IsLegacyAiWorkspaceTarget(e.Target))
            {
                return;
            }

            _ = Dispatcher.InvokeAsync(OpenAiAssistStudio);
        }

        private static bool IsLegacyAiWorkspaceTarget(string? target)
        {
            var normalized = (target ?? string.Empty)
                .Trim()
                .Replace('_', ' ')
                .Replace('-', ' ')
                .ToUpperInvariant();

            return normalized.Contains("COPILOT", StringComparison.Ordinal)
                || normalized.Contains("AI STUDIO", StringComparison.Ordinal)
                || normalized.Contains("AI ASSIST", StringComparison.Ordinal)
                || normalized.Contains("AIADVISOR", StringComparison.Ordinal)
                || normalized.Equals("AI", StringComparison.Ordinal)
                || normalized.Equals("STUDIO", StringComparison.Ordinal);
        }

        private void OpenAiAssistStudio()
        {
            OpenAiAssistTab();
            AiAssistPanel.OpenSection("AI Studio");
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
        private void UndoBtn_Click(object sender, RoutedEventArgs e) => _history.Undo();
        private void RedoBtn_Click(object sender, RoutedEventArgs e) => _history.Redo();
        private void ClearHistory_Click(object sender, RoutedEventArgs e) => _history.Clear();

        private void OnHistoryStateChanged()
        {
            UndoButton.IsEnabled = _history.CanUndo;
            RedoButton.IsEnabled = _history.CanRedo;
            if (DataContext is MainViewModel viewModel)
                viewModel.HistoryBreadcrumb = _history.Breadcrumb;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            StopMeasurementIfActive();

            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            if (e.Key == Key.Z && _history.CanUndo)
            {
                _history.Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y && _history.CanRedo)
            {
                _history.Redo();
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel { Drawing.IsMeasuring: true })
                return;

            bool isMeasurementPointPlacement =
                e.ChangedButton == MouseButton.Left &&
                FindVisualAncestor<HelixViewport3D>(
                    e.OriginalSource as DependencyObject) != null;
            if (!isMeasurementPointPlacement)
                StopMeasurementIfActive();
        }

        private void StopMeasurementIfActive()
        {
            if (DataContext is MainViewModel { Drawing.IsMeasuring: true } viewModel)
                viewModel.Drawing.StopMeasureMode();
        }

        private static T? FindVisualAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                    return match;

                current = current is Visual || current is Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private void RenderQualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: ComboBoxItem item })
                IfcViewerHost.ApplyRenderQualitySelection(item.Tag?.ToString());
        }

        private void OpenAiAssistTab()
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.SelectedRightWorkspaceTab = RightWorkspaceTab.AiAssistant;

            ShowAiCompanionButton.Visibility = Visibility.Collapsed;
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
