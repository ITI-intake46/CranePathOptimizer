using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class CraneOptimizerDialogService : ICraneOptimizerDialogService
    {
        private readonly ICranePlacementService _placementService;
        private readonly IRenderingService _renderingService;
        private readonly IDialogService _dialogService;
        private readonly ICraneRepository _craneRepository;
        private readonly ISceneService _sceneService;
        private readonly CraneSetupViewModel _craneSetup;
        private readonly IIfcReaderRepository _ifcReader;

        public CraneOptimizerDialogService(
            ICranePlacementService placementService,
            IRenderingService renderingService,
            IDialogService dialogService,
            ICraneRepository craneRepository,
            ISceneService sceneService,
            CraneSetupViewModel craneSetup,
            IIfcReaderRepository ifcReader)
        {
            _placementService = placementService;
            _renderingService = renderingService;
            _dialogService = dialogService;
            _craneRepository = craneRepository;
            _sceneService = sceneService;
            _craneSetup = craneSetup;
            _ifcReader = ifcReader;
        }

        public CraneOptimizerDialogOutcome? ShowDialog()
        {
            var dialog = new CraneOptimizerDialog(
                _placementService,
                _renderingService,
                _dialogService,
                _craneRepository,
                _sceneService,
                _craneSetup,
                _ifcReader);

            if (System.Windows.Application.Current?.MainWindow != null)
                dialog.Owner = System.Windows.Application.Current.MainWindow;

            dialog.ShowDialog();

            if (dialog.LatestResult == null)
                return null;

            return new CraneOptimizerDialogOutcome(
                dialog.LatestResult,
                dialog.AppliedPosition,
                dialog.AppliedCrane);
        }
    }
}
