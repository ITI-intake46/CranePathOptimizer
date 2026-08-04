using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using System.Windows;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAResultsWindowService : IGAResultsWindowService
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly IOptimizationViewportHighlightService _viewportHighlightService;
        private GAResultsWindow? _activeWindow;

        public GAResultsWindowService(
            IRenderingService renderingService,
            ISceneService sceneService,
            IProjectCoordinateTransformService transformService,
            IOptimizationViewportHighlightService viewportHighlightService)
        {
            _renderingService = renderingService;
            _sceneService = sceneService;
            _transformService = transformService;
            _viewportHighlightService = viewportHighlightService;
        }

        public void Show(
            CranePlacementResult result, 
            Action<CranePositionResult?> selectedPointChanged,
            Action<GAViewMode> viewModeChanged,
            GAViewMode initialViewMode)
        {
            if (result == null || result.Positions == null || result.Positions.Count == 0)
                return;

            if (_activeWindow != null)
            {
                ActivateWindow(_activeWindow);
                return;
            }

            GAResultsWindowViewModel viewModel = new(
                result,
                _renderingService,
                _sceneService,
                selectedPointChanged,
                viewModeChanged,
                initialViewMode,
                _transformService,
                _viewportHighlightService);
            GAResultsWindow window = new(viewModel);
            _activeWindow = window;
            window.Closed += OnWindowClosed;

            if (System.Windows.Application.Current?.MainWindow != null)
                window.Owner = System.Windows.Application.Current.MainWindow;

            window.Show();
            ActivateWindow(window);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is GAResultsWindow window)
                window.Closed -= OnWindowClosed;

            if (ReferenceEquals(_activeWindow, sender))
                _activeWindow = null;
        }

        private static void ActivateWindow(GAResultsWindow window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            window.Focus();
        }
    }
}
