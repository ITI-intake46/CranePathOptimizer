using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Companion.ViewModels;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Configuration;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Services;
using CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Workflow;
using CraneOptimizer.UI.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Companion.Views
{
    public partial class AiCompanionPanel : UserControl
    {
        public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AiCompanionPanel));

        private AiCompanionViewModel? _viewModel;

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

            if (FindInheritedMainViewModel() is not { } mainViewModel)
                return;

            var advisor = App.ServiceProvider.GetRequiredService<AiAdvisorViewModel>();
            var windowService = App.ServiceProvider.GetRequiredService<IAiAdvisorWindowService>();
            var uiContext = App.ServiceProvider.GetRequiredService<IUiInteractionContextService>();
            var settings = App.ServiceProvider.GetRequiredService<IAiCopilotSettingsService>();
            var workflow = App.ServiceProvider.GetRequiredService<IWorkflowReadinessService>();

            _viewModel = new AiCompanionViewModel(mainViewModel, advisor, windowService, uiContext, settings, workflow);
            _viewModel.ConversationMessages.CollectionChanged += OnConversationMessagesChanged;
            foreach (var message in _viewModel.ConversationMessages)
                message.PropertyChanged += OnConversationMessagePropertyChanged;

            DataContext = _viewModel;
            ScrollBodyToHome();
        }

        private MainViewModel? FindInheritedMainViewModel()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is FrameworkElement element && element.DataContext is MainViewModel vm)
                    return vm;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
            e.Handled = true;
        }

        private void BodyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // Keep the docked assistant on one predictable scroll surface: the body between header and composer.
            var offset = scrollViewer.VerticalOffset - (e.Delta / 4.0d);
            scrollViewer.ScrollToVerticalOffset(Math.Max(0d, Math.Min(scrollViewer.ScrollableHeight, offset)));
            e.Handled = true;
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

            if (e.NewItems != null && e.NewItems.Cast<AiCompanionChatMessageViewModel>().Any(message => !message.IsReadinessIntro))
                ScrollBodyToEnd();
        }

        private void OnConversationMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not AiCompanionChatMessageViewModel { IsReadinessIntro: false })
                return;

            if (e.PropertyName is nameof(AiCompanionChatMessageViewModel.Body) or nameof(AiCompanionChatMessageViewModel.Timestamp))
                ScrollBodyToEnd();
        }

        private void ScrollBodyToHome()
        {
            Dispatcher.BeginInvoke(new Action(() => BodyScrollViewer.ScrollToHome()));
        }

        private void ScrollBodyToEnd()
        {
            Dispatcher.BeginInvoke(new Action(() => BodyScrollViewer.ScrollToEnd()));
        }


        private void ComposerResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_viewModel == null)
                return;

            _viewModel.ComposerHeight = Math.Max(32d, Math.Min(120d, _viewModel.ComposerHeight - e.VerticalChange));
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
