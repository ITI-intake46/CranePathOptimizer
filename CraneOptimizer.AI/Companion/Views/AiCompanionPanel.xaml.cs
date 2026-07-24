using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Host;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.Companion.Views
{
    public partial class AiCompanionPanel : UserControl
    {
        private static readonly string[] SectionNames = { "Chat", "Mission", "BIM", "Optimize", "Report", "AI Studio" };

        private AiCompanionViewModel? _viewModel;
        private string? _pendingSectionName;

        public AiCompanionPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
                return;

            var serviceProvider = AiServiceLocator.ServiceProvider;
            if (serviceProvider == null)
                return;

            _viewModel = serviceProvider.GetRequiredService<AiCompanionViewModel>();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ComposerFocusRequested += OnComposerFocusRequested;
            _viewModel.ConversationMessages.CollectionChanged += OnConversationMessagesChanged;
            foreach (var message in _viewModel.ConversationMessages)
                message.PropertyChanged += OnConversationMessagePropertyChanged;

            DataContext = _viewModel;

            if (!string.IsNullOrWhiteSpace(_pendingSectionName))
            {
                var pendingSection = _pendingSectionName;
                _pendingSectionName = null;
                OpenSection(pendingSection);
            }
            else
            {
                ScrollBodyToHome();
            }
        }

        public void OpenSection(string sectionName)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OpenSection(sectionName));
                return;
            }

            var normalized = SectionNames.FirstOrDefault(
                section => string.Equals(section, sectionName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = "AI Studio";

            if (_viewModel == null)
            {
                _pendingSectionName = normalized;
                return;
            }

            if (_viewModel.SelectCommandCenterTabCommand.CanExecute(normalized))
                _viewModel.SelectCommandCenterTabCommand.Execute(normalized);

            ScrollBodyToHome();
        }

        private void BodyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            var offset = scrollViewer.VerticalOffset - (e.Delta / 4.0d);
            scrollViewer.ScrollToVerticalOffset(Math.Max(0d, Math.Min(scrollViewer.ScrollableHeight, offset)));
            e.Handled = true;
        }

        private void SectionNavigation_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    MoveSectionSelection(-1);
                    break;
                case Key.Right:
                case Key.Down:
                    MoveSectionSelection(1);
                    break;
                case Key.Home:
                    SelectSection(0);
                    break;
                case Key.End:
                    SelectSection(SectionNames.Length - 1);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        private void MoveSectionSelection(int delta)
        {
            if (_viewModel == null)
                return;

            var currentIndex = Array.FindIndex(
                SectionNames,
                section => string.Equals(section, _viewModel.SelectedCommandCenterTab, StringComparison.OrdinalIgnoreCase));

            if (currentIndex < 0)
                currentIndex = 0;

            SelectSection(Math.Clamp(currentIndex + delta, 0, SectionNames.Length - 1));
        }

        private void SelectSection(int index)
        {
            if (_viewModel == null || index < 0 || index >= SectionNames.Length)
                return;

            var section = SectionNames[index];
            if (_viewModel.SelectCommandCenterTabCommand.CanExecute(section))
                _viewModel.SelectCommandCenterTabCommand.Execute(section);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiCompanionViewModel.SelectedCommandCenterTab))
            {
                if (string.Equals(_viewModel?.SelectedCommandCenterTab, "Chat", StringComparison.OrdinalIgnoreCase))
                {
                    ScrollBodyToEnd();
                    return;
                }

                ScrollBodyToHome();
            }
        }

        private void OnComposerFocusRequested(object? sender, EventArgs e)
        {
            FocusComposer();
        }

        private void OnConversationMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (AiCompanionChatMessageViewModel message in e.OldItems)
                    message.PropertyChanged -= OnConversationMessagePropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (AiCompanionChatMessageViewModel message in e.NewItems)
                    message.PropertyChanged += OnConversationMessagePropertyChanged;
            }

            if (e.NewItems != null
                && e.NewItems.Cast<AiCompanionChatMessageViewModel>().Any(message => !message.IsReadinessIntro))
            {
                ScrollBodyToEnd();
            }
        }

        private void OnConversationMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not AiCompanionChatMessageViewModel { IsReadinessIntro: false })
                return;

            if (e.PropertyName is nameof(AiCompanionChatMessageViewModel.Body)
                or nameof(AiCompanionChatMessageViewModel.Timestamp))
            {
                ScrollBodyToEnd();
            }
        }

        private void ScrollBodyToHome()
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    BodyScrollViewer.UpdateLayout();
                    BodyScrollViewer.ScrollToHome();
                }),
                DispatcherPriority.ContextIdle);
        }

        private void ScrollBodyToEnd()
        {
            QueueScrollToEnd(DispatcherPriority.Background);
            QueueScrollToEnd(DispatcherPriority.ContextIdle);
            QueueScrollToEnd(DispatcherPriority.ApplicationIdle);
        }

        private void QueueScrollToEnd(DispatcherPriority priority)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    BodyScrollViewer.UpdateLayout();
                    BodyScrollViewer.ScrollToEnd();
                }),
                priority);
        }

        private void FocusComposer()
        {
            QueueFocusComposer(DispatcherPriority.Input);
            QueueFocusComposer(DispatcherPriority.ContextIdle);
        }

        private void QueueFocusComposer(DispatcherPriority priority)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!IsVisible || QuestionTextBox == null)
                        return;

                    QuestionTextBox.UpdateLayout();
                    QuestionTextBox.Focus();
                    Keyboard.Focus(QuestionTextBox);
                    QuestionTextBox.CaretIndex = QuestionTextBox.Text?.Length ?? 0;
                    QuestionTextBox.ScrollToEnd();
                }),
                priority);
        }

        private void ComposerResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_viewModel == null)
                return;

            _viewModel.ComposerHeight = Math.Max(36d, Math.Min(120d, _viewModel.ComposerHeight - e.VerticalChange));
        }

        private void QuestionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                return;

            if (_viewModel?.AskToggleCommand.CanExecute(null) == true)
                _viewModel.AskToggleCommand.Execute(null);

            e.Handled = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.ComposerFocusRequested -= OnComposerFocusRequested;
                _viewModel.ConversationMessages.CollectionChanged -= OnConversationMessagesChanged;
                foreach (var message in _viewModel.ConversationMessages)
                    message.PropertyChanged -= OnConversationMessagePropertyChanged;
                _viewModel.Dispose();
            }

            _viewModel = null;
            DataContext = null;
        }
    }
}
