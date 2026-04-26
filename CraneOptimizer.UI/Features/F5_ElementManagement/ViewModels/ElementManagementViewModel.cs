using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using System.Collections.ObjectModel;
using System;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase, IDisposable
    {
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private bool _disposed;

        public ElementManagementViewModel(
            ISceneService scene,
            IRenderingService render)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _render = render ?? throw new ArgumentNullException(nameof(render));

            SelectElementCommand = new RelayCommand<PrecastElement>(SelectElement);

            _scene.ElementsChanged += OnElementsChanged;
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

                OnElementSelected(_selectedElement);
            }
        }

        public System.Windows.Input.ICommand SelectElementCommand { get; }

        private void SelectElement(PrecastElement? element)
        {
            SelectedElement = element;
        }

        private void OnElementSelected(PrecastElement? element)
        {
            if (element == null)
            {
                _render.ResetHighlight();
                return;
            }

            _render.Highlight(element);
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
