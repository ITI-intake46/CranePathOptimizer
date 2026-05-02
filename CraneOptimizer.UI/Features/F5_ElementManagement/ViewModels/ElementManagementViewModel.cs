using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase
    {
        private readonly ISceneService _scene;
        private string _selectedElementName = "No element selected";
        private PrecastElement? _selectedElement;
 
        public ElementManagementViewModel(ISceneService scene)
        {
            _scene = scene;
            _scene.ElementsChanged += OnElementsChanged;
            _scene.ElementsSelectionChanged += OnElementsSelectionChanged;
        }
 
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
 
        public string SelectedElementName
        {
            get => _selectedElementName;
            set
            {
                _selectedElementName = value;
                OnPropertyChanged();
            }
        }
 
        public PrecastElement? SelectedElement
        {
            get => _selectedElement;
            set
            {
                _selectedElement = value;
                OnPropertyChanged();
                SelectedElementName = value?.ElementID ?? "No element selected";
            }
        }
 
        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Elements.Clear();
            FilteredElements.Clear();
 
            foreach (var e in elements)
            {
                Elements.Add(e);
                FilteredElements.Add(e);
            }
 
            if (elements.Count == 0)
            {
                SelectedElement = null;
            }
        }
 
        private void OnElementsSelectionChanged(List<PrecastElement> selectedElements)
        {
            SelectedElement = selectedElements.FirstOrDefault();
        }
 
        public void SelectElement(PrecastElement element)
        {
            _scene.SetSelectedElements(new List<PrecastElement> { element });
        }
    }
}
