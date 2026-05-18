using CraneOptimizer.Domain.Common;
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
        private readonly Action<CranePositionResult?> _selectedPointChanged;
        private readonly CranePlacementResult _result;
        private CranePositionResult? _selectedPoint;
        private bool _isLoaded;
        private bool _isClosing;

        public GAResultsWindowViewModel(
            CranePlacementResult result,
            IRenderingService renderingService,
            ISceneService sceneService,
            Action<CranePositionResult?> selectedPointChanged)
        {
            _result = result;
            _renderingService = renderingService;
            _sceneService = sceneService;
            _selectedPointChanged = selectedPointChanged;
        }

        public IReadOnlyList<CranePositionResult> Positions => _result.Positions;
        public double FitnessScore => _result.FitnessScore;
        public double CoverageRatio => _result.CoverageRatio;
        public string TotalLiftedText => $"Total Lifted: {_result.LiftedElementCount} / {_result.TotalElementCount}";

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

            _isLoaded = true;

            if (SelectedPoint == null && _result.Positions.Any())
                SelectedPoint = _result.Positions[0];
        }

        public void OnClosing()
        {
            _isClosing = true;
            SelectedPointRenderer.Clear(_renderingService);
        }

        private void RenderSelectedPoint()
        {
            SelectedPointRenderer.Clear(_renderingService);
            _selectedPointChanged(SelectedPoint);

            if (SelectedPoint != null)
                SelectedPointRenderer.RenderPlacementPoint(
                    _renderingService,
                    new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, 0));

            // Move the existing crane to the selected GA point
            if (SelectedPoint != null)
            {
                var sceneCrane = _sceneService.GetCranes().FirstOrDefault();
                if (sceneCrane != null)
                {
                    sceneCrane.Position = new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, 0);
                    _sceneService.UpdateCrane(sceneCrane);
                }
            }
        }
    }
}
