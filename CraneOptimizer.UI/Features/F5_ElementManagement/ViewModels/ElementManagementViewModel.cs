using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F5_ElementManagement.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase, IDisposable
    {
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private readonly ISelectionService _selectionService;
        private readonly IHighlightService _highlightService;
        private bool _disposed;

        public ElementManagementViewModel(
            ISceneService scene,
            IRenderingService render,
            ISelectionService selectionService,
            IHighlightService highlightService)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _render = render ?? throw new ArgumentNullException(nameof(render));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _highlightService = highlightService ?? throw new ArgumentNullException(nameof(highlightService));

            SelectElementCommand = new RelayCommand<PrecastElement>(SelectElement);

            _scene.ElementsChanged += OnElementsChanged;

            _selectionService.SelectionChanged += element =>
            {
                var dispatcher = System.Windows.Application.Current.Dispatcher;

                if (dispatcher.CheckAccess())
                    SelectedElement = element;
                else
                    dispatcher.InvokeAsync(() => SelectedElement = element);
            };
        }

        // ─────────────────────────────
        // Collections
        // ─────────────────────────────
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();

        // ─────────────────────────────
        // Event to Shell
        // ─────────────────────────────
        public event Action<int>? ElementsLoaded;

        private PrecastElement? _selectedElement;
        public PrecastElement? SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (_selectedElement == value) return;
                _selectedElement = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ElementProperties));
            }
        }

        public bool HasSelection => _selectedElement is not null;

        public IReadOnlyList<PropertyRow> ElementProperties =>
            _selectedElement is null
                ? Array.Empty<PropertyRow>()
                : BuildRows(_selectedElement);

        private static IReadOnlyList<PropertyRow> BuildRows(PrecastElement e) => new[]
        {
            new PropertyRow("ID",     e.ElementID ?? "N/A", "Element"),
            new PropertyRow("Type",   e.ElementType ?? "N/A", "Element"),
            new PropertyRow("Weight", $"{(e.ElementWT ?? 0):N0} kg", "Element"),
            new PropertyRow("Volume", $"{(e.ElementVolume ?? 0):F2} m³", "Element"),
            new PropertyRow("X",      $"{e.ElementCOG.X:F3} m", "Position"),
            new PropertyRow("Y",      $"{e.ElementCOG.Y:F3} m", "Position"),
            new PropertyRow("Z",      $"{e.ElementCOG.Z:F3} m", "Position"),
        };

        public ICommand SelectElementCommand { get; }

        public ICommand FocusElementCommand => new RelayCommand(
            () => _highlightService.RequestMode(HighlightMode.Focus),
            () => HasSelection);

        public ICommand IsolateElementCommand => new RelayCommand(
            () => _highlightService.RequestMode(HighlightMode.Isolation),
            () => HasSelection);

        public ICommand ResetViewCommand => new RelayCommand(
            () =>
            {
                _selectionService.Select(null);
                _highlightService.RequestMode(HighlightMode.Normal);
            });

        private void SelectElement(PrecastElement? element)
        {
            _selectionService.Select(element);
        }

        // ─────────────────────────────
        // Scene sync
        // ─────────────────────────────
        private void OnElementsChanged(List<PrecastElement> elements)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
            {
                UpdateElements(elements);
            }
            else
            {
                dispatcher.InvokeAsync(() => UpdateElements(elements));
            }
        }

        private void UpdateElements(List<PrecastElement> elements)
        {
            Elements.Clear();
            FilteredElements.Clear();

            foreach (var e in elements)
            {
                Elements.Add(e);
                FilteredElements.Add(e);
            }

            _render.Render(
                elements.Select(e => (e, (LiftStatus?)null)),
                new ElementVisualBuilder(),
                "Elements"
            );

            ElementsLoaded?.Invoke(elements.Count);
        }

        // ─────────────────────────────
        // Dispose
        // ─────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _scene.ElementsChanged -= OnElementsChanged;
        }
    }
}
