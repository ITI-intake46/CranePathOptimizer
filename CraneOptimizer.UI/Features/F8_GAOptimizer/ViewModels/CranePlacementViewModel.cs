using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public class CranePlacementViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ICraneOptimizerDialogService _optimizerDialogService;
        private readonly IGAResultsWindowService _resultsWindowService;
        private readonly RelayCommand _optimizeCommand;
        private readonly RelayCommand _showResultsCommand;

        private bool _isPlacing;
        private DomainPoint3D? _currentPosition;
        private Crane? _selectedCrane;
        private CranePlacementResult? _result;

        public CranePlacementViewModel(
            IRenderingService renderingService,
            ICraneOptimizerDialogService optimizerDialogService,
            IGAResultsWindowService resultsWindowService)
        {
            _renderingService = renderingService;
            _optimizerDialogService = optimizerDialogService;
            _resultsWindowService = resultsWindowService;

            _optimizeCommand = new RelayCommand(ExecuteOptimize);
            _showResultsCommand = new RelayCommand(OpenResults, CanShowResults);
            StartPlacementCommand = new RelayCommand(StartPlacement);
        }

        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                _isPlacing = value;
                OnPropertyChanged();
            }
        }

        public DomainPoint3D? CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
            }
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
            }
        }

        public CranePlacementResult? Result
        {
            get => _result;
            set
            {
                if (ReferenceEquals(_result, value))
                    return;

                _result = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasResult));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasResult => Result != null && Result.Positions.Count > 0;

        public ICommand OptimizeCommand => _optimizeCommand;
        public ICommand ShowResultsCommand => _showResultsCommand;
        public ICommand StartPlacementCommand { get; }

        public bool RunOptimizationWorkflow()
        {
            var result = _optimizerDialogService.ShowDialog();
            if (result == null || result.Positions.Count == 0)
                return false;

            Result = result;
            return true;
        }

        private void ExecuteOptimize()
        {
            RunOptimizationWorkflow();
        }

        private void StartPlacement()
        {
            IsPlacing = true;
        }

        private bool CanShowResults()
        {
            return HasResult;
        }

        private void OpenResults()
        {
            if (!CanShowResults() || Result == null)
                return;

            _resultsWindowService.Show(Result);
        }

        public void Place(DomainPoint3D point)
        {
            if (!IsPlacing || SelectedCrane == null)
                return;

            CurrentPosition = point;

            SelectedPointRenderer.Clear(_renderingService);
            SelectedPointRenderer.RenderPoint(
                _renderingService,
                point.X,
                point.Y,
                point.Z + 500.0);

            IsPlacing = false;
        }
    }
}
