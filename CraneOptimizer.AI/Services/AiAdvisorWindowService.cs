using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.ViewModels;
using CraneOptimizer.AI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.Services
{
    public sealed class AiAdvisorWindowService : IAiAdvisorWindowService
    {
        private readonly IServiceProvider _serviceProvider;
        private AiAdvisorWindow? _window;

        public AiAdvisorWindowService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Show()
        {
            if (_window != null)
            {
                if (_window.WindowState == System.Windows.WindowState.Minimized)
                    _window.WindowState = System.Windows.WindowState.Normal;

                _window.Activate();
                _window.Focus();
                return;
            }

            var viewModel = _serviceProvider.GetRequiredService<AiAdvisorViewModel>();
            var settings = _serviceProvider.GetRequiredService<IAiCopilotSettingsService>();
            viewModel.AnalyzeCurrentResult();

            _window = new AiAdvisorWindow(viewModel, settings);
            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner != null && !ReferenceEquals(owner, _window))
                _window.Owner = owner;
            _window.Closed += (_, _) => _window = null;
            _window.Loaded += async (_, _) => await viewModel.CheckOllamaAsync().ConfigureAwait(true);
            _window.Show();
        }
    }
}
