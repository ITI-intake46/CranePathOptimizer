using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F5_ElementManagement.Services;
using CraneOptimizer.UI.Features.F5_ElementManagement.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase
    {
        private readonly ISceneService _scene;
        private readonly IElementGridSizingService _gridSizingService;
        private string _selectedElementName = "No element selected";
        private PrecastElement? _selectedElement;
        private double _minWeight;
        private double _maxWeight = 50000;
        private List<string> _selectedTypes = new();
        private string _searchText = "";
        private string _propertySearchText = "";
        private bool _showOnlySelected;
        private bool _isSyncingSelectionFromScene;

        public ElementManagementViewModel(ISceneService scene, IElementGridSizingService gridSizing)
        {
            _scene = scene;
            _gridSizingService = gridSizing;
            _scene.ElementsChanged += OnElementsChanged;
            _scene.ElementsSelectionChanged += OnElementsSelectionChanged;
            ShowFilterDialogCommand = new RelayCommand(ShowFilterDialog);
            ApplyFilterCommand = new RelayCommand(ApplyFilters);
        }

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<PropertyItem> ElementProperties { get; } = new();

        public (double widthMeters, double lengthMeters, double centerXMillimeters, double centerYMillimeters) GridSize =>
            _gridSizingService.CalculateGridSizeMeters(Elements.ToList());

        public ICommand ShowFilterDialogCommand { get; }
        public ICommand ApplyFilterCommand { get; }

        public string FilterStatus => $"Showing {GetVisibleElementCount()} of {Elements.Count} elements";

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
                if (ReferenceEquals(_selectedElement, value))
                    return;

                _selectedElement = value;
                OnPropertyChanged();
                SelectedElementName = value != null ? GetElementDisplayName(value) : "No element selected";
                LoadElementProperties();

                if (!_isSyncingSelectionFromScene && value != null)
                {
                    _scene.SetSelectedElements(new List<PrecastElement> { value });
                }
            }
        }

        public double MinWeight
        {
            get => _minWeight;
            set
            {
                _minWeight = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public double MaxWeight
        {
            get => _maxWeight;
            set
            {
                _maxWeight = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplySearchFilter();
            }
        }

        public string PropertySearchText
        {
            get => _propertySearchText;
            set
            {
                _propertySearchText = value;
                OnPropertyChanged();
                LoadElementProperties();
            }
        }

        public bool ShowOnlySelected
        {
            get => _showOnlySelected;
            set
            {
                _showOnlySelected = value;
                OnPropertyChanged();
                ApplySearchFilter();
            }
        }

        public void ApplyFilterCriteria(FilterCriteria criteria)
        {
            _selectedTypes = criteria.SelectedTypes ?? new List<string>();
            MinWeight = criteria.MinWeight;
            MaxWeight = criteria.MaxWeight;
            ApplySearchFilter();
        }

        public void LoadElementProperties()
        {
            ElementProperties.Clear();

            if (SelectedElement == null)
                return;

            var groups = BuildPropertyGroups(SelectedElement);
            var filter = PropertySearchText?.Trim();

            foreach (var group in groups)
            {
                var filteredGroup = FilterGroup(group, filter);
                if (filteredGroup != null)
                    ElementProperties.Add(filteredGroup);
            }
        }

        public void SelectElement(PrecastElement element)
        {
            _scene.SetSelectedElements(new List<PrecastElement> { element });
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

            ApplySearchFilter();

            if (elements.Count == 0)
            {
                SelectedElement = null;
            }
            else if (SelectedElement != null && !elements.Contains(SelectedElement))
            {
                SelectedElement = elements.FirstOrDefault();
            }

            OnPropertyChanged(nameof(GridSize));
            OnPropertyChanged(nameof(FilterStatus));
        }

        private void OnElementsSelectionChanged(List<PrecastElement> selectedElements)
        {
            try
            {
                _isSyncingSelectionFromScene = true;
                SelectedElement = selectedElements.FirstOrDefault();
            }
            finally
            {
                _isSyncingSelectionFromScene = false;
            }

            if (ShowOnlySelected)
            {
                ApplySearchFilter();
            }
        }

        private void ShowFilterDialog() => new ElementFilterDialog(this).ShowDialog();

        private void ApplyFilters() => ApplySearchFilter();

        private void ApplySearchFilter()
        {
            var view = CollectionViewSource.GetDefaultView(FilteredElements);
            view.Filter = obj =>
            {
                var element = obj as PrecastElement;
                if (element == null) return false;

                var weight = element.ElementWT ?? 0;
                if (weight < MinWeight || weight > MaxWeight) return false;

                if (_selectedTypes.Count > 0 && !_selectedTypes.Contains(element.ElementType)) return false;

                if (!string.IsNullOrEmpty(SearchText) &&
                    !Matches(element.ElementID, SearchText) &&
                    !Matches(GetElementDisplayName(element), SearchText))
                {
                    return false;
                }

                if (ShowOnlySelected && !_scene.GetSelectedElements().Contains(element)) return false;

                return true;
            };

            view.Refresh();
            OnPropertyChanged(nameof(FilterStatus));
        }

        private static List<PropertyItem> BuildPropertyGroups(PrecastElement element)
        {
            return new List<PropertyItem>
            {
                CreateGroup("Identity", true,
                    CreateProperty("Name", GetElementDisplayName(element), ""),
                    CreateProperty("Object Type", GetStringProperty(element, "ObjectType") ?? element.ElementType, ""),
                    CreateProperty("Material", element.ElementMaterial, "")),

                CreateGroup("Geometry", true,
                    CreateProperty("Length", element.ElementLength, "m"),
                    CreateProperty("Width", element.ElementWidth, "m"),
                    CreateProperty("Height", element.ElementHeight, "m"),
                    CreateProperty("Weight", GetWeightDisplayValue(element), ""),
                    CreateProperty("Volume", element.ElementVolume, "m3")),

                CreateGroup("Location", true,
                    CreateProperty("X (COG)", element.ElementCOG.X, "mm"),
                    CreateProperty("Y (COG)", element.ElementCOG.Y, "mm"),
                    CreateProperty("Z (COG)", element.ElementCOG.Z, "mm"))
            };
        }

        private static PropertyItem CreateGroup(string category, bool isExpanded, params PropertyItem[] children)
        {
            var group = new PropertyItem
            {
                Name = category,
                Category = category,
                IsExpanded = isExpanded
            };

            foreach (var child in children.Where(c => !string.IsNullOrWhiteSpace(c.Value)))
            {
                child.Category = category;
                group.Children.Add(child);
            }

            return group;
        }

        private static PropertyItem CreateProperty(string name, object? value, string unit)
        {
            var formattedValue = FormatValue(value);

            return new PropertyItem
            {
                Name = name,
                Value = string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(formattedValue)
                    ? formattedValue
                    : $"{formattedValue} {unit}",
                Unit = unit
            };
        }

        private static PropertyItem? FilterGroup(PropertyItem group, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return group;

            var groupMatches = Matches(group.Name, filter);
            var filtered = new PropertyItem
            {
                Name = group.Name,
                Category = group.Category,
                IsExpanded = true
            };

            foreach (var child in group.Children)
            {
                if (groupMatches || Matches(child.Name, filter) || Matches(child.Value, filter))
                    filtered.Children.Add(child);
            }

            return filtered.Children.Count > 0 ? filtered : null;
        }

        private static bool MatchesElement(PrecastElement element, string filter)
        {
            return Matches(GetElementDisplayName(element), filter) ||
                   Matches(element.ElementID, filter) ||
                   Matches(element.ElementType, filter) ||
                   Matches(element.ElementMaterial, filter);
        }

        private static bool Matches(string? value, string filter) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static string GetElementDisplayName(PrecastElement element)
        {
            return ElementDisplayNameConverter.GetDisplayName(element);
        }

        private static string GetWeightDisplayValue(PrecastElement element)
        {
            if (element.ElementWT.HasValue && element.ElementWT.Value > 0)
                return $"{element.ElementWT.Value:N0} kg";

            if (element.ElementVolume.HasValue && element.ElementVolume.Value > 0)
                return $"{element.ElementVolume.Value * 2500:N0} kg";

            return string.Empty;
        }

        private static string? GetStringProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return property?.GetValue(source)?.ToString();
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                double d => d.ToString("N2", CultureInfo.CurrentCulture),
                float f => f.ToString("N2", CultureInfo.CurrentCulture),
                decimal m => m.ToString("N2", CultureInfo.CurrentCulture),
                bool b => b ? "Yes" : "No",
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string FormatPoint(PrecastElement element)
        {
            return string.Create(
                CultureInfo.CurrentCulture,
                $"{element.ElementCOG.X:N2}, {element.ElementCOG.Y:N2}, {element.ElementCOG.Z:N2}");
        }

        private int GetVisibleElementCount()
        {
            var view = CollectionViewSource.GetDefaultView(FilteredElements);
            return view?.Cast<object>().Count() ?? FilteredElements.Count;
        }
    }

    public class FilterCriteria
    {
        public double MinWeight { get; set; } = 0;
        public double MaxWeight { get; set; } = 50000;
        public List<string> SelectedTypes { get; set; } = new();
    }
}
