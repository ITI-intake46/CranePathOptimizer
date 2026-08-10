using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Host;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.Companion.Views
{
    public partial class AiCompanionPanel : UserControl
    {
        private const double CollapsedSectionRailWidth = 12d;
        private const double ExpandedSectionRailWidth = 30d;
        private const double LatestMessageThreshold = 32d;
        private const int SectionRailWheelDeltaPerStep = 120;
        private static readonly Duration SectionRailAnimationDuration = new(TimeSpan.FromMilliseconds(120));
        private static readonly string[] SectionNames = { "Chat", "Mission", "BIM", "Optimize", "Report", "AI Studio" };

        public static readonly DependencyProperty IsSectionRailExpandedProperty = DependencyProperty.Register(
            nameof(IsSectionRailExpanded),
            typeof(bool),
            typeof(AiCompanionPanel),
            new PropertyMetadata(false, OnSectionRailExpandedChanged));

        public static readonly DependencyProperty ShowHeaderProperty = DependencyProperty.Register(
            nameof(ShowHeader),
            typeof(bool),
            typeof(AiCompanionPanel),
            new PropertyMetadata(true));

        public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AiCompanionPanel));

        private readonly Dictionary<string, double> _sectionScrollOffsets = new(StringComparer.OrdinalIgnoreCase);
        private AiCompanionViewModel? _viewModel;
        private IAiCopilotSettingsService? _settingsService;
        private string? _pendingSectionName;
        private AiCompanionChatMessageViewModel? _pendingPromptAnchor;
        private string _activeSectionName = "Chat";
        private bool _isRestoringScrollPosition;
        private bool _followLatestContent;
        private int _sectionRailWheelDelta;
        private bool _focusRailSelectionAfterChange;

        public bool IsSectionRailExpanded
        {
            get => (bool)GetValue(IsSectionRailExpandedProperty);
            set => SetValue(IsSectionRailExpandedProperty, value);
        }

        public bool ShowHeader
        {
            get => (bool)GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public event RoutedEventHandler CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

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

            _settingsService = serviceProvider.GetService<IAiCopilotSettingsService>();
            if (_settingsService != null)
                IsSectionRailExpanded = _settingsService.Current.IsSectionRailExpanded;

            _viewModel = serviceProvider.GetRequiredService<AiCompanionViewModel>();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ComposerFocusRequested += OnComposerFocusRequested;
            _viewModel.ConversationMessages.CollectionChanged += OnConversationMessagesChanged;
            foreach (var message in _viewModel.ConversationMessages)
                message.PropertyChanged += OnConversationMessagePropertyChanged;

            DataContext = _viewModel;
            _activeSectionName = _viewModel.SelectedCommandCenterTab;
            UpdateSectionRailState(animate: false);
            UpdateSectionRailSelectionState(_activeSectionName, focusSelection: false);

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
        }

        private void BodyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            var offset = scrollViewer.VerticalOffset - (e.Delta / 4.0d);
            scrollViewer.ScrollToVerticalOffset(Math.Max(0d, Math.Min(scrollViewer.ScrollableHeight, offset)));
            e.Handled = true;
        }

        private void BodyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isRestoringScrollPosition)
                return;

            _sectionScrollOffsets[_activeSectionName] = BodyScrollViewer.VerticalOffset;
            _followLatestContent = IsNearLatestMessage();
            UpdateJumpToLatestVisibility();
        }

        private void JumpToLatestButton_Click(object sender, RoutedEventArgs e)
        {
            _followLatestContent = true;
            ScrollBodyToEnd(force: true);
            e.Handled = true;
        }

        private static void OnSectionRailExpandedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is AiCompanionPanel panel)
                panel.UpdateSectionRailState(animate: panel.IsLoaded);
        }

        private void SectionRailToggleButton_Click(object sender, RoutedEventArgs e)
        {
            SetSectionRailExpanded(!IsSectionRailExpanded, persist: true);
            e.Handled = true;
        }

        private void SetSectionRailExpanded(bool isExpanded, bool persist)
        {
            IsSectionRailExpanded = isExpanded;

            if (!persist || _settingsService == null)
                return;

            _settingsService.Update(settings => settings.IsSectionRailExpanded = isExpanded);
        }

        private void UpdateSectionRailState(bool animate)
        {
            if (SectionRail == null || SectionRailToggleButton == null)
                return;

            var targetWidth = IsSectionRailExpanded
                ? ExpandedSectionRailWidth
                : CollapsedSectionRailWidth;

            SectionRailToggleButton.ToolTip = IsSectionRailExpanded
                ? "Collapse to the slim section spine"
                : "Expand full vertical section labels";
            SectionRailToggleGlyph.Text = IsSectionRailExpanded ? "›" : "‹";
            System.Windows.Automation.AutomationProperties.SetName(
                SectionRailToggleButton,
                IsSectionRailExpanded ? "Collapse AI section rail" : "Expand AI section rail");

            if (!animate || !IsSectionRailExpanded)
            {
                SectionRail.BeginAnimation(FrameworkElement.WidthProperty, null);
                SectionRail.Width = targetWidth;
                return;
            }

            var expectedExpandedState = IsSectionRailExpanded;
            var currentWidth = SectionRail.ActualWidth > 0d
                ? SectionRail.ActualWidth
                : SectionRail.Width;
            var animation = new DoubleAnimation
            {
                From = currentWidth,
                To = targetWidth,
                Duration = SectionRailAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (_, _) =>
            {
                if (IsSectionRailExpanded != expectedExpandedState)
                    return;

                SectionRail.BeginAnimation(FrameworkElement.WidthProperty, null);
                SectionRail.Width = targetWidth;
            };

            SectionRail.BeginAnimation(
                FrameworkElement.WidthProperty,
                animation,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void SectionRail_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
                return;

            if (_sectionRailWheelDelta != 0
                && Math.Sign(_sectionRailWheelDelta) != Math.Sign(e.Delta))
            {
                _sectionRailWheelDelta = 0;
            }

            _sectionRailWheelDelta += e.Delta;
            while (Math.Abs(_sectionRailWheelDelta) >= SectionRailWheelDeltaPerStep)
            {
                var direction = _sectionRailWheelDelta < 0 ? 1 : -1;
                MoveSectionSelection(direction, focusSelection: false);
                _sectionRailWheelDelta -= Math.Sign(_sectionRailWheelDelta) * SectionRailWheelDeltaPerStep;
            }

            e.Handled = true;
        }

        private void SectionRail_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    SetSectionRailExpanded(true, persist: true);
                    break;
                case Key.Right:
                    SetSectionRailExpanded(false, persist: true);
                    break;
                case Key.Up:
                    MoveSectionSelection(-1, focusSelection: true);
                    break;
                case Key.Down:
                    MoveSectionSelection(1, focusSelection: true);
                    break;
                case Key.Home:
                    SelectSection(0, focusSelection: true);
                    break;
                case Key.End:
                    SelectSection(SectionNames.Length - 1, focusSelection: true);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        private void MoveSectionSelection(int delta, bool focusSelection)
        {
            if (_viewModel == null)
                return;

            var currentIndex = Array.FindIndex(
                SectionNames,
                section => string.Equals(section, _viewModel.SelectedCommandCenterTab, StringComparison.OrdinalIgnoreCase));

            if (currentIndex < 0)
                currentIndex = 0;

            SelectSection(
                Math.Clamp(currentIndex + delta, 0, SectionNames.Length - 1),
                focusSelection);
        }

        private void SelectSection(int index, bool focusSelection)
        {
            if (_viewModel == null || index < 0 || index >= SectionNames.Length)
                return;

            var section = SectionNames[index];
            _focusRailSelectionAfterChange = focusSelection;

            if (string.Equals(_viewModel.SelectedCommandCenterTab, section, StringComparison.OrdinalIgnoreCase))
            {
                UpdateSectionRailSelectionState(section, focusSelection);
                _focusRailSelectionAfterChange = false;
                return;
            }

            if (_viewModel.SelectCommandCenterTabCommand.CanExecute(section))
            {
                _viewModel.SelectCommandCenterTabCommand.Execute(section);
                return;
            }

            _focusRailSelectionAfterChange = false;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AiCompanionViewModel.SelectedCommandCenterTab) || _viewModel == null)
                return;

            _sectionScrollOffsets[_activeSectionName] = BodyScrollViewer.VerticalOffset;
            _activeSectionName = _viewModel.SelectedCommandCenterTab;
            var focusSelection = _focusRailSelectionAfterChange;
            _focusRailSelectionAfterChange = false;
            UpdateSectionRailSelectionState(_activeSectionName, focusSelection);
            RestoreSectionScrollPosition(_activeSectionName);
        }

        private void UpdateSectionRailSelectionState(string sectionName, bool focusSelection)
        {
            foreach (var (name, button) in GetSectionRailButtons())
            {
                var isSelected = string.Equals(name, sectionName, StringComparison.OrdinalIgnoreCase);
                System.Windows.Automation.AutomationProperties.SetItemStatus(
                    button,
                    isSelected ? "Selected" : "Not selected");

                if (isSelected && focusSelection)
                    button.Focus();
            }
        }

        private IEnumerable<(string Name, RadioButton Button)> GetSectionRailButtons()
        {
            yield return ("Chat", ChatSectionButton);
            yield return ("Mission", MissionSectionButton);
            yield return ("BIM", BimSectionButton);
            yield return ("Optimize", OptimizeSectionButton);
            yield return ("Report", ReportSectionButton);
            yield return ("AI Studio", AiStudioSectionButton);
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

            AiCompanionChatMessageViewModel? newestUserMessage = null;
            AiCompanionChatMessageViewModel? newestAssistantMessage = null;
            if (e.NewItems != null)
            {
                foreach (AiCompanionChatMessageViewModel message in e.NewItems)
                {
                    message.PropertyChanged += OnConversationMessagePropertyChanged;
                    if (message.IsReadinessIntro)
                        continue;

                    if (message.IsUserMessage)
                        newestUserMessage = message;
                    else
                        newestAssistantMessage = message;
                }
            }

            if (newestUserMessage != null)
            {
                _followLatestContent = false;
                _pendingPromptAnchor = newestUserMessage;
                ScrollMessageToStart(newestUserMessage);
            }
            else if (newestAssistantMessage != null)
            {
                _followLatestContent = false;
                var anchor = _pendingPromptAnchor ?? newestAssistantMessage;
                _pendingPromptAnchor = null;
                ScrollMessageToStart(anchor);
            }
            else
            {
                QueueUpdateJumpToLatestVisibility();
            }
        }

        private void OnConversationMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not AiCompanionChatMessageViewModel { IsReadinessIntro: false })
                return;

            if (e.PropertyName is nameof(AiCompanionChatMessageViewModel.Body)
                or nameof(AiCompanionChatMessageViewModel.DisplayBody)
                or nameof(AiCompanionChatMessageViewModel.IsDetailsExpanded))
            {
                if (_followLatestContent)
                    ScrollBodyToEnd();
                else
                    QueueUpdateJumpToLatestVisibility();
            }
        }

        private void ScrollBodyToHome()
        {
            RestoreScrollOffset(0d, DispatcherPriority.ContextIdle);
        }

        private void RestoreSectionScrollPosition(string sectionName)
        {
            var offset = _sectionScrollOffsets.TryGetValue(sectionName, out var savedOffset)
                ? savedOffset
                : 0d;
            RestoreScrollOffset(offset, DispatcherPriority.ContextIdle);
        }

        private void RestoreScrollOffset(double offset, DispatcherPriority priority)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _isRestoringScrollPosition = true;
                    try
                    {
                        BodyScrollViewer.UpdateLayout();
                        BodyScrollViewer.ScrollToVerticalOffset(Math.Max(0d, Math.Min(offset, BodyScrollViewer.ScrollableHeight)));
                        _sectionScrollOffsets[_activeSectionName] = BodyScrollViewer.VerticalOffset;
                    }
                    finally
                    {
                        _isRestoringScrollPosition = false;
                    }

                    UpdateJumpToLatestVisibility();
                }),
                priority);
        }

        private void ScrollMessageToStart(AiCompanionChatMessageViewModel message)
        {
            QueueScrollMessageToStart(message, DispatcherPriority.Loaded);
            QueueScrollMessageToStart(message, DispatcherPriority.ContextIdle);
        }

        private void QueueScrollMessageToStart(AiCompanionChatMessageViewModel message, DispatcherPriority priority)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!string.Equals(_activeSectionName, "Chat", StringComparison.OrdinalIgnoreCase))
                        return;

                    BodyScrollViewer.UpdateLayout();
                    if (ConversationItemsControl.ItemContainerGenerator.ContainerFromItem(message) is not FrameworkElement container)
                        return;

                    try
                    {
                        var position = container.TransformToAncestor(BodyScrollViewer).Transform(new Point(0d, 0d));
                        var targetOffset = BodyScrollViewer.VerticalOffset + position.Y - 4d;
                        RestoreScrollOffset(targetOffset, DispatcherPriority.Send);
                    }
                    catch (InvalidOperationException)
                    {
                        // The item can be detached briefly while templates switch. The queued second pass retries.
                    }
                }),
                priority);
        }

        private void ScrollBodyToEnd(bool force = false)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!force && !_followLatestContent)
                        return;

                    _isRestoringScrollPosition = true;
                    try
                    {
                        BodyScrollViewer.UpdateLayout();
                        BodyScrollViewer.ScrollToEnd();
                        _sectionScrollOffsets[_activeSectionName] = BodyScrollViewer.VerticalOffset;
                    }
                    finally
                    {
                        _isRestoringScrollPosition = false;
                    }

                    UpdateJumpToLatestVisibility();
                }),
                DispatcherPriority.ContextIdle);
        }

        private void QueueUpdateJumpToLatestVisibility()
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    BodyScrollViewer.UpdateLayout();
                    UpdateJumpToLatestVisibility();
                }),
                DispatcherPriority.ContextIdle);
        }

        private bool IsNearLatestMessage()
        {
            var distanceFromEnd = BodyScrollViewer.ScrollableHeight - BodyScrollViewer.VerticalOffset;
            return distanceFromEnd <= LatestMessageThreshold;
        }

        private void UpdateJumpToLatestVisibility()
        {
            if (JumpToLatestButton == null || _viewModel == null)
                return;

            var isChat = string.Equals(_activeSectionName, "Chat", StringComparison.OrdinalIgnoreCase);
            var hasConversation = _viewModel.ConversationMessages.Any(message => !message.IsReadinessIntro);
            JumpToLatestButton.Visibility = isChat && hasConversation && !IsNearLatestMessage()
                ? Visibility.Visible
                : Visibility.Collapsed;
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

            _viewModel.ComposerHeight = Math.Max(34d, Math.Min(120d, _viewModel.ComposerHeight - e.VerticalChange));
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
            _settingsService = null;
            _sectionScrollOffsets.Clear();
            _pendingPromptAnchor = null;
            _activeSectionName = "Chat";
            _followLatestContent = false;
            DataContext = null;
        }
    }
}
