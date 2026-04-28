using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public sealed class GAResultsWindowViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly CranePlacementResult _result;
        private List<PrecastElement> _cachedElements = new();
        private CranePositionResult? _selectedPoint;
        private bool _isLoaded;
        private bool _isClosing;

        public GAResultsWindowViewModel(
            CranePlacementResult result,
            IRenderingService renderingService,
            ISceneService sceneService)
        {
            _result = result;
            _renderingService = renderingService;
            _sceneService = sceneService;
        }

        public IReadOnlyList<CranePositionResult> Positions => _result.Positions;
        public double FitnessScore => _result.FitnessScore;
        public double CoverageRatio => _result.CoverageRatio;

        public CranePositionResult? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (ReferenceEquals(_selectedPoint, value))
                    return;

                _selectedPoint = value;
                OnPropertyChanged();

                if (!_isLoaded || _isClosing)
                    return;

                RenderSelectedPoint();
            }
        }

        public void OnLoaded()
        {
            if (_isLoaded)
                return;

            _cachedElements = _sceneService.GetElements() ?? new List<PrecastElement>();
            _isLoaded = true;

            if (SelectedPoint == null && _result.Positions.Any())
                SelectedPoint = _result.Positions[0];
        }

        public void OnClosing()
        {
            _isClosing = true;
        }

        private void RenderSelectedPoint()
        {
            SelectedPointRenderer.Render(_renderingService, _cachedElements, SelectedPoint);
        }
    }
}
