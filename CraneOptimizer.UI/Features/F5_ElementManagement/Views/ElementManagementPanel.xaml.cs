using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementManagementPanel : UserControl
    {
        private readonly ISceneService? _sceneService;
        private bool _isSyncingGridSelection;

        public ElementManagementPanel()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            _sceneService = App.ServiceProvider.GetRequiredService<ISceneService>();
            var vm = App.ServiceProvider.GetRequiredService<ElementManagementViewModel>();
            DataContext = vm;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private ElementManagementViewModel? VM => DataContext as ElementManagementViewModel;

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_sceneService == null)
                return;

            _sceneService.ElementsSelectionChanged -= OnSceneElementsSelectionChanged;
            _sceneService.ElementsSelectionChanged += OnSceneElementsSelectionChanged;
            _sceneService.ElementsChanged -= OnSceneElementsChanged;
            _sceneService.ElementsChanged += OnSceneElementsChanged;
            SyncGridSelection(_sceneService.GetSelectedElements());
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_sceneService == null)
                return;

            _sceneService.ElementsSelectionChanged -= OnSceneElementsSelectionChanged;
            _sceneService.ElementsChanged -= OnSceneElementsChanged;
        }

        private void ElementsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sceneService == null || _isSyncingGridSelection)
                return;

            var selected = ElementsGrid.SelectedItems
                .OfType<PrecastElement>()
                .ToList();

            if (IsSelectionChangeFromViewRefresh(e))
            {
                Dispatcher.InvokeAsync(
                    () => SyncGridSelection(_sceneService.GetSelectedElements()),
                    DispatcherPriority.Background);
                return;
            }

            _sceneService.SetSelectedElements(selected);
        }

        private bool IsSelectionChangeFromViewRefresh(SelectionChangedEventArgs e)
        {
            if (_sceneService == null || e.RemovedItems.Count == 0 || IsUserChangingGridSelection())
                return false;

            var sceneSelected = new HashSet<PrecastElement>(
                _sceneService.GetSelectedElements(),
                ReferenceEqualityComparer.Instance);

            return sceneSelected.Count > 0
                && e.RemovedItems.OfType<PrecastElement>().Any(sceneSelected.Contains);
        }

        private bool IsUserChangingGridSelection()
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed || ElementsGrid.IsKeyboardFocusWithin)
                return true;

            return Keyboard.FocusedElement is DependencyObject focusedElement
                && IsDescendantOfGrid(focusedElement);
        }

        private bool IsDescendantOfGrid(DependencyObject element)
        {
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, ElementsGrid))
                    return true;
            }

            return false;
        }

        private void OnSceneElementsChanged(List<PrecastElement> _)
        {
            if (_sceneService == null)
                return;

            SyncGridSelection(_sceneService.GetSelectedElements());
        }

        private void OnSceneElementsSelectionChanged(List<PrecastElement> selectedElements)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(
                    () => SyncGridSelection(selectedElements),
                    DispatcherPriority.Background);
                return;
            }

            SyncGridSelection(selectedElements);
        }

        private void SyncGridSelection(IReadOnlyCollection<PrecastElement> selectedElements)
        {
            _isSyncingGridSelection = true;
            try
            {
                ElementsGrid.SelectedItems.Clear();

                if (selectedElements.Count == 0)
                    return;

                var selectedSet = new HashSet<PrecastElement>(
                    selectedElements,
                    ReferenceEqualityComparer.Instance);

                foreach (var item in ElementsGrid.Items.OfType<PrecastElement>())
                {
                    if (selectedSet.Contains(item))
                        ElementsGrid.SelectedItems.Add(item);
                }
            }
            finally
            {
                _isSyncingGridSelection = false;
            }
        }
    }
}
