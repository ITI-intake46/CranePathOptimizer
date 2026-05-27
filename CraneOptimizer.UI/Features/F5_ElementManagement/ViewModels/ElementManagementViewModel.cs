using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F5_ElementManagement.Services;
using CraneOptimizer.UI.Features.F16_Filter.DTOs;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F16_Filter.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using CraneOptimizer.UI.Features.F5_ElementManagement.DTOs;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase
    {
        private readonly ISceneService _scene;
        private readonly ISceneGridSizingService _gridSizingService;
        private readonly IElementVisibilityFilterService _visibilityFilterService;
        private readonly IElementLiftStateService _liftStateService;
        private readonly IUnitSettingsService _unitSettingsService;
        private string _selectedElementName = "No element selected";
        private PrecastElement? _selectedElement;
        private double _minWeight;
        private double _maxWeight = 50000;
        private List<string> _selectedTypes = new();
        private List<string> _selectedMaterials = new();
        private List<string> _selectedStoreys = new();
        private HashSet<string>? _includedElementIds;
        private string _sortBy = "Name";
        private bool _sortDescending;
        private string _searchText = "";
        private string _propertySearchText = "";
        private bool _showOnlySelected;
        private bool _isSyncingSelectionFromScene;

        public ElementManagementViewModel(
            ISceneService scene,
            IElementLiftStateService liftStateService,
            ISceneGridSizingService gridSizing,
            IElementVisibilityFilterService visibilityFilterService,
            IUnitSettingsService unitSettingsService)
        {
            _scene = scene;
            _gridSizingService = gridSizing;
            _visibilityFilterService = visibilityFilterService;
            _liftStateService = liftStateService;
            _unitSettingsService = unitSettingsService;
            _scene.ElementsChanged += OnElementsChanged;
            _scene.ElementsSelectionChanged += OnElementsSelectionChanged;
            _liftStateService.StateChanged += OnLiftStateChanged;
            _unitSettingsService.SettingsChanged += OnUnitSettingsChanged;
            ShowFilterDialogCommand = new RelayCommand(ShowFilterDialog);
            ApplyFilterCommand = new RelayCommand(ApplyFilters);
        }

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<PropertyItem> ElementProperties { get; } = new();

        public (double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) GridSize =>
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

        public void ApplyFilterCriteria(FilterCriteriaDto criteria)
        {
            _selectedTypes = criteria.SelectedTypes ?? new List<string>();
            _selectedMaterials = criteria.SelectedMaterials ?? new List<string>();
            _selectedStoreys = criteria.SelectedStoreys ?? new List<string>();
            _includedElementIds = criteria.IncludedElementIds == null
                ? null
                : new HashSet<string>(criteria.IncludedElementIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
            _sortBy = string.IsNullOrWhiteSpace(criteria.SortBy) ? "Name" : criteria.SortBy;
            _sortDescending = criteria.SortDescending;
            MinWeight = criteria.MinWeight;
            MaxWeight = criteria.MaxWeight;
            ApplySearchFilter();
        }

        public void LoadElementProperties()
        {
            ElementProperties.Clear();

            if (SelectedElement == null)
                return;

            var panelDto = BuildElementPanelDto(SelectedElement);
            var groups = BuildPropertyGroups(panelDto);
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

        private void OnLiftStateChanged() => LoadElementProperties();

        private void ShowFilterDialog() => new ElementFilterDialog(this).ShowDialog();

        private void ApplyFilters() => ApplySearchFilter();

        private void ApplySearchFilter()
        {
            var view = CollectionViewSource.GetDefaultView(FilteredElements);
            view.Filter = obj =>
            {
                var element = obj as PrecastElement;
                if (element == null) return false;

                // Weight filter
                var weight = ElementWeightCalculator.GetWeightKg(element);
                if (weight < MinWeight || weight > MaxWeight) return false;

                // Type filter
                if (_selectedTypes.Count > 0 && !_selectedTypes.Contains(element.ElementType))
                    return false;

                if (_selectedMaterials.Count > 0 && !_selectedMaterials.Contains(NormalizeMaterial(element.ElementMaterial)))
                    return false;

                if (_selectedStoreys.Count > 0 && !_selectedStoreys.Contains(_visibilityFilterService.GetStorey(element)))
                    return false;

                if (_includedElementIds != null
                    && (!string.IsNullOrWhiteSpace(element.ElementID)
                        ? !_includedElementIds.Contains(element.ElementID)
                        : true))
                    return false;

                // Search filter
                if (!string.IsNullOrEmpty(SearchText))
                {
                    var displayName = GetPrecastDisplayName(element);
                    if (!displayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                        !(element.ElementID ?? string.Empty).Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                // Selected only
                if (ShowOnlySelected && !_scene.GetSelectedElements().Contains(element))
                    return false;

                return true;
            };

            view.Refresh();
            ApplySort(view);
            OnPropertyChanged(nameof(FilterStatus));
        }

        private void ApplySort(ICollectionView view)
        {
            if (view is not ListCollectionView listView)
                return;

            listView.CustomSort = new ElementListSortComparer(_sortBy, _sortDescending, _visibilityFilterService);
            listView.Refresh();
        }

        private ElementPanelDto BuildElementPanelDto(PrecastElement element)
        {
            var liftState = _liftStateService.GetState(element.ElementID);

            return new ElementPanelDto(
                BuildLiftStateDto(liftState),
                GetElementID(element),
                GetPrecastDisplayName(element),
                CleanElementType(element.ElementType),
                element.ElementMaterial ?? "Concrete",
                element.ElementLength,
                element.ElementWidth,
                element.ElementHeight,
                GetWeightDisplay(element),
                element.ElementVolume,
                element.ElementCOG.X,
                element.ElementCOG.Y,
                element.ElementCOG.Z);
        }

        private ElementPanelLiftStateDto BuildLiftStateDto(ElementLiftStateDto? liftState)
        {
            return new ElementPanelLiftStateDto(
                GetLiftStateDisplay(liftState),
                GetCranePositionDisplay(liftState),
                liftState?.Radius,
                liftState?.RequiredCapacity);
        }

        private List<PropertyItem> BuildPropertyGroups(ElementPanelDto panel)
        {
            return new List<PropertyItem>
            {
                CreateLiftStateGroup(panel.LiftState),

                // Identity (Expanded)
                CreateGroup("Identity", true,
                    CreateProperty("ID", panel.Id, ""),
                    CreateProperty("Name", panel.Name, ""),
                    CreateProperty("Type", panel.Type, ""),
                    CreateProperty("Material", panel.Material, "")),

                // Geometry (Expanded)
                CreateGroup("Geometry", true,
                    CreateDisplayProperty("Length", FormatLength(panel.Length)),
                    CreateDisplayProperty("Width", FormatLength(panel.Width)),
                    CreateDisplayProperty("Height", FormatLength(panel.Height)),
                    CreateDisplayProperty("Weight", panel.Weight),
                    CreateDisplayProperty("Volume", FormatVolume(panel.Volume))),

                // Location (Expanded)
                CreateGroup("Location", true,
                    CreateDisplayProperty("X (COG)", FormatLength(panel.CenterX)),
                    CreateDisplayProperty("Y (COG)", FormatLength(panel.CenterY)),
                    CreateDisplayProperty("Z (COG)", FormatLength(panel.CenterZ)))
            };
        }

        private PropertyItem CreateLiftStateGroup(ElementPanelLiftStateDto liftState)
        {
            return CreateGroup("Lift State", true,
                CreateDisplayProperty("State", liftState.State),
                CreateDisplayProperty("Crane Position", liftState.CranePosition),
                CreateDisplayProperty("Radius", FormatLength(liftState.Radius)),
                CreateDisplayProperty("Required Capacity", FormatMass(liftState.RequiredCapacity)));
        }

        private string GetLiftStateDisplay(ElementLiftStateDto? liftState)
        {
            if (!_liftStateService.HasCurrentState)
                return "Not evaluated";

            if (liftState == null)
                return "No result for selected GA point";

            return liftState.State;
        }

        private string GetCranePositionDisplay(ElementLiftStateDto? liftState)
        {
            return liftState?.CraneX == null || liftState.CraneY == null
                ? "-"
                : $"X {FormatLength(liftState.CraneX)}, Y {FormatLength(liftState.CraneY)}";
        }

        // Revit Integer ID
        private static string GetElementID(PrecastElement e) =>
            !string.IsNullOrEmpty(e.ElementID) ? e.ElementID : "-";

        private static string GetPrecastDisplayName(PrecastElement e)
        {
            return CleanElementName(ElementDisplayNameConverter.GetDisplayName(e));
        }

        private static string CleanElementName(string? rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "-";
            
            return rawName.Replace("- Assembly", "")
                         .Replace(" Assembly", "")
                         .Replace(" : ", " - ")
                         .Trim();
        }

        private static string CleanElementType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "Element";
            
            var cleaned = type.Replace("Ifc", "").Replace("StandardCase", "")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "Element";

            if (cleaned.Equals("ElementAssembly", StringComparison.OrdinalIgnoreCase))
                return "Assembly";

            return cleaned;
        }

        private string GetWeightDisplay(PrecastElement e)
        {
            var weight = ElementWeightCalculator.GetWeightTons(e);
            return weight > 0d
                ? ElementWeightCalculator.FormatWeight(weight, _unitSettingsService.Current, ElementWeightCalculator.IsEstimated(e), CultureInfo.CurrentCulture)
                : "-";
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

        private static PropertyItem CreateDisplayProperty(string name, string? value)
        {
            return new PropertyItem
            {
                Name = name,
                Value = value ?? string.Empty,
                Unit = string.Empty
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

        private static bool Matches(string? value, string filter) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static string GetElementDisplayName(PrecastElement element)
        {
            return ElementDisplayNameConverter.GetDisplayName(element);
        }

        private string FormatLength(double? value)
        {
            return value.HasValue
                ? UnitSystem.FormatLength(value.Value, _unitSettingsService.Current, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private string FormatVolume(double? value)
        {
            if (!value.HasValue) return string.Empty;
            
            var settings = _unitSettingsService.Current;
            var vol = UnitSystem.FromMeters(UnitSystem.FromMeters(UnitSystem.FromMeters(value.Value, settings.DisplayLengthUnit), settings.DisplayLengthUnit), settings.DisplayLengthUnit);
            var symbol = UnitSystem.LengthSymbol(settings.DisplayLengthUnit);
            
            return $"{vol.ToString("N" + Math.Clamp(settings.DecimalPlaces, 0, 6), CultureInfo.CurrentCulture)} {symbol}\u00b3";
        }

        private string FormatMass(double? value)
        {
            return value.HasValue
                ? UnitSystem.FormatMass(value.Value, _unitSettingsService.Current, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private void OnUnitSettingsChanged(object? sender, UnitSettings settings)
        {
            LoadElementProperties();
            OnPropertyChanged(nameof(GridSize));
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

        private int GetVisibleElementCount()
        {
            var view = CollectionViewSource.GetDefaultView(FilteredElements);
            return view?.Cast<object>().Count() ?? FilteredElements.Count;
        }

        private static string NormalizeMaterial(string? material) =>
            string.IsNullOrWhiteSpace(material) ? "Unspecified" : material.Trim();
    }

}
