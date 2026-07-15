using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Host;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.Companion.Views
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

            var serviceProvider = AiServiceLocator.ServiceProvider;
            if (serviceProvider == null)
                return;

            _viewModel = serviceProvider.GetRequiredService<AiCompanionViewModel>();
            DataContext = _viewModel;
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
