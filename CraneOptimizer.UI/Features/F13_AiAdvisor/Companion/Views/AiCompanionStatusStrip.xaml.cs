using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Companion.ViewModels;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Configuration;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Services;
using CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Workflow;
using CraneOptimizer.UI.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Companion.Views
{
    public partial class AiCompanionStatusStrip : UserControl
    {
        public static readonly RoutedEvent AssistRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(AssistRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AiCompanionStatusStrip));

        public static readonly RoutedEvent HideRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(HideRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AiCompanionStatusStrip));

        private AiCompanionViewModel? _viewModel;

        public AiCompanionStatusStrip()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event RoutedEventHandler AssistRequested
        {
            add => AddHandler(AssistRequestedEvent, value);
            remove => RemoveHandler(AssistRequestedEvent, value);
        }

        public event RoutedEventHandler HideRequested
        {
            add => AddHandler(HideRequestedEvent, value);
            remove => RemoveHandler(HideRequestedEvent, value);
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
            DataContext = _viewModel;
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

        private void AssistButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshContext();
            RaiseEvent(new RoutedEventArgs(AssistRequestedEvent, this));
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(HideRequestedEvent, this));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Dispose();
            _viewModel = null;
            DataContext = null;
        }
    }
}
