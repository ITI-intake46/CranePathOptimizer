using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAResultsWindowService : IGAResultsWindowService
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;

        public GAResultsWindowService(
            IRenderingService renderingService,
            ISceneService sceneService)
        {
            _renderingService = renderingService;
            _sceneService = sceneService;
        }

        public void Show(CranePlacementResult result)
        {
            var window = new GAResultsWindow(_renderingService, _sceneService)
            {
                DataContext = result
            };

            if (System.Windows.Application.Current?.MainWindow != null)
                window.Owner = System.Windows.Application.Current.MainWindow;

            window.ShowDialog();
        }
    }
}
