using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.Services;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public class CranePlacementViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ICraneOptimizerDialogService _optimizerDialogService;
        private readonly IGAResultsWindowService _resultsWindowService;
        private readonly IGADiagnosticsWindowService _diagnosticsWindowService;
        private readonly IReportBuilderDialogService _reportBuilderDialogService;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly IElementLiftStateService _liftStateService;
        private readonly RelayCommand _optimizeCommand;
        private readonly RelayCommand _showResultsCommand;
        private readonly RelayCommand _showDiagnosticsCommand;
        private readonly RelayCommand _openAllElementsReportCommand;
        private readonly RelayCommand _openLiftingSummaryReportCommand;

        private bool _isPlacing;
        private DomainPoint3D? _currentPosition;
        private Crane? _selectedCrane;
        private CranePlacementResult? _result;

        public CranePlacementViewModel(
            IRenderingService renderingService,
            ICraneOptimizerDialogService optimizerDialogService,
            IGAResultsWindowService resultsWindowService,
            IGADiagnosticsWindowService diagnosticsWindowService,
            IReportBuilderDialogService reportBuilderDialogService,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            IElementLiftStateService liftStateService)
        {
            _renderingService = renderingService;
            _optimizerDialogService = optimizerDialogService;
            _resultsWindowService = resultsWindowService;
            _diagnosticsWindowService = diagnosticsWindowService;
            _reportBuilderDialogService = reportBuilderDialogService;
            _interaction = interaction;
            _hitTest = hitTest;
            _liftStateService = liftStateService;

            _optimizeCommand = new RelayCommand(ExecuteOptimize);
            _showResultsCommand = new RelayCommand(OpenResults, CanShowResults);
            _showDiagnosticsCommand = new RelayCommand(OpenDiagnostics);
            _openAllElementsReportCommand = new RelayCommand(OpenAllElementsReport);
            _openLiftingSummaryReportCommand = new RelayCommand(OpenLiftingSummaryReport, CanShowResults);
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

        private GAViewMode _currentGAViewMode = GAViewMode.FinalAssignment;
        public GAViewMode CurrentGAViewMode
        {
            get => _currentGAViewMode;
            set
            {
                if (_currentGAViewMode == value) return;
                _currentGAViewMode = value;
                OnPropertyChanged();
                RenderElementsWithCurrentStatus?.Invoke();
            }
        }

        private CranePositionResult? _selectedPosition;
        public CranePositionResult? SelectedPosition
        {
            get => _selectedPosition;
            set
            {
                if (ReferenceEquals(_selectedPosition, value))
                    return;

                _selectedPosition = value;
                OnPropertyChanged();
                PublishLiftStates(value);
            }
        }

        public bool HasResult => Result != null && Result.Positions.Count > 0;

        public ICommand OptimizeCommand => _optimizeCommand;
        public ICommand ShowResultsCommand => _showResultsCommand;
        public ICommand ShowDiagnosticsCommand => _showDiagnosticsCommand;
        public ICommand OpenAllElementsReportCommand => _openAllElementsReportCommand;
        public ICommand OpenLiftingSummaryReportCommand => _openLiftingSummaryReportCommand;
        public ICommand StartPlacementCommand { get; }
        public Action<DomainPoint3D>? PlaceCraneInScene { get; set; }
        public Action? RenderElementsWithCurrentStatus { get; set; }

        public bool RunOptimizationWorkflow()
        {
            var outcome = _optimizerDialogService.ShowDialog();
            if (outcome?.Result == null || outcome.Result.Positions.Count == 0)
                return false;

            Result = outcome.Result;
            SelectedPosition = outcome.AppliedPosition ?? outcome.Result.Positions[0];
            RenderElementsWithCurrentStatus?.Invoke();
            return true;
        }

        private void ExecuteOptimize()
        {
            RunOptimizationWorkflow();
        }

        private void StartPlacement()
        {
            // Re-arm placement reliably. If the same mode is already active,
            // keep state but avoid stacking duplicate modes.
            IsPlacing = true;
            if (_interaction.CurrentMode is Interaction.CranePlacementMode)
                return;

            if (PlaceCraneInScene != null)
            {
                _interaction.PushMode(new Interaction.CranePlacementMode(this, _hitTest, _interaction, PlaceCraneInScene));
            }
        }

        private bool CanShowResults()
        {
            return HasResult;
        }

        private void OpenResults()
        {
            if (!CanShowResults() || Result == null)
                return;

            try
            {
                _resultsWindowService.Show(Result, 
                    point =>
                    {
                        SelectedPosition = point;
                        RenderElementsWithCurrentStatus?.Invoke();
                    },
                    viewMode =>
                    {
                        CurrentGAViewMode = viewMode;
                    },
                    CurrentGAViewMode);
            }
            finally
            {
                SelectedPosition = null;
                RenderElementsWithCurrentStatus?.Invoke();
            }
        }

        private void OpenDiagnostics()
        {
            _diagnosticsWindowService.Show(CurrentGAViewMode);
        }

        private void OpenAllElementsReport()
        {
            _reportBuilderDialogService.ShowReportBuilder(
                ReportFamily.ElementList,
                ReportTemplateCatalog.BasicElementList,
                "All Elements");
        }

        private void OpenLiftingSummaryReport()
        {
            if (!CanShowResults())
                return;

            _reportBuilderDialogService.ShowReportBuilder(
                ReportFamily.LiftingResult,
                ReportTemplateCatalog.BasicLiftingResult,
                "Optimization Result");
        }

        private void PublishLiftStates(CranePositionResult? position)
        {
            if (position == null)
            {
                _liftStateService.Clear();
                return;
            }

            _liftStateService.SetStates(position.ElementResults.Select(result => new ElementLiftStateDto(
                result.ElementId,
                GetLiftStateDisplay(result.Status),
                position.X,
                position.Y,
                result.Radius,
                result.RequiredCapacity)));
        }

        private static string GetLiftStateDisplay(LiftStatus status)
        {
            return status switch
            {
                LiftStatus.Liftable => "Liftable",
                LiftStatus.OutOfRadius => "Out of radius",
                LiftStatus.Overweight => "Overweight",
                LiftStatus.HeightFail => "Height fail",
                LiftStatus.SpecialCase => "Special case",
                LiftStatus.NotCharted => "Not charted",
                _ => status.ToString()
            };
        }

        public void Place(DomainPoint3D point)
        {
            if (!IsPlacing || SelectedCrane == null)
                return;

            CurrentPosition = point;

            SelectedPointRenderer.Clear(_renderingService);
            SelectedPointRenderer.RenderPlacementPoint(_renderingService, point);

            IsPlacing = false;
        }
    }
}
