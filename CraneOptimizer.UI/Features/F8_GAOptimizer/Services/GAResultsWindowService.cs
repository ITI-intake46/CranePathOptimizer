using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAResultsWindowService : IGAResultsWindowService
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly IIfcReaderRepository _ifcReader;

        public GAResultsWindowService(
            IRenderingService renderingService,
            ISceneService sceneService,
            IIfcReaderRepository ifcReader)
        {
            _renderingService = renderingService;
            _sceneService = sceneService;
            _ifcReader = ifcReader;
        }

        public void Show(CranePlacementResult result)
        {
            if (result == null || result.Positions.Count == 0)
                return;

            var viewModel = new GAResultsWindowViewModel(result, _renderingService, _sceneService, _ifcReader);
            var window = new GAResultsWindow(viewModel);

            if (System.Windows.Application.Current?.MainWindow != null)
                window.Owner = System.Windows.Application.Current.MainWindow;

            window.ShowDialog();
        }
    }
}
