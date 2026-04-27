using System.Windows;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    /// <summary>
    /// Interaction logic for GAResultsWindow.xaml
    /// </summary>
    public partial class GAResultsWindow : Window
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private List<PrecastElement> _cachedElements = new();
        private bool _isClosing;

        public static readonly DependencyProperty SelectedPointProperty =
            DependencyProperty.Register(
                nameof(SelectedPoint),
                typeof(CranePositionResult),
                typeof(GAResultsWindow),
                new PropertyMetadata(null, OnSelectedPointChanged));

        public CranePositionResult? SelectedPoint
        {
            get => (CranePositionResult?)GetValue(SelectedPointProperty);
            set => SetValue(SelectedPointProperty, value);
        }

        public GAResultsWindow(
            IRenderingService renderingService,
            ISceneService sceneService)
        {
            _renderingService = renderingService;
            _sceneService = sceneService;

            InitializeComponent();
            Loaded += OnLoaded;
        }

        private static void OnSelectedPointChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not GAResultsWindow window)
                return;

            if (Equals(e.OldValue, e.NewValue))
                return;

            if (!window.IsLoaded || window._isClosing)
                return;

            window.RenderSelectedPoint(e.NewValue as CranePositionResult);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _cachedElements = _sceneService.GetElements() ?? new List<PrecastElement>();

            if (SelectedPoint != null)
                return;

            if (DataContext is CranePlacementResult result && result.Positions.Count > 0)
                SelectedPoint = result.Positions[0];
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            base.OnClosing(e);
        }

        private void RenderSelectedPoint(CranePositionResult? point)
        {
            if (!IsLoaded || _isClosing)
                return;

            SelectedPointRenderer.Render(_renderingService, _cachedElements, point);
        }
    }
}
