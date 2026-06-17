using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
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
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Features.F5_ElementManagement.DTOs;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class ElementManagementViewModel : ViewModelBase, IDisposable, IDataErrorInfo
    {
        private const double MinConcreteDensityKgPerM3 = 1000.0;
        private const double MaxConcreteDensityKgPerM3 = 4000.0;

        private readonly ISceneService _scene;
        private readonly ISceneGridSizingService _gridSizingService;
        private readonly IElementVisibilityFilterService _visibilityFilterService;
        private readonly IElementLiftStateService _liftStateService;
        private readonly IUnitSettingsService _unitSettingsService;
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly ICoordinateReadoutFormatter _readoutFormatter;
        private readonly IProjectMaterialSettingsService _materialSettingsService;
        private readonly IProjectWorkspaceService _workspaceService;
        private string _selectedElementName = "No element selected";
        private PrecastElement? _selectedElement;
        private double _concreteDensityKgPerM3 = ProjectMaterialSettingsService.DefaultConcreteDensityKgPerM3;
        private string _densityValidationMessage = string.Empty;
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
            IUnitSettingsService unitSettingsService,
            IProjectCoordinateTransformService transformService,
            ICoordinateReadoutFormatter readoutFormatter,
            IProjectMaterialSettingsService materialSettingsService,
            IProjectWorkspaceService workspaceService)
        {
            _scene = scene;
            _gridSizingService = gridSizing;
            _visibilityFilterService = visibilityFilterService;
            _liftStateService = liftStateService;
            _unitSettingsService = unitSettingsService;
            _transformService = transformService;
            _readoutFormatter = readoutFormatter;
            _materialSettingsService = materialSettingsService;
            _workspaceService = workspaceService;
            _scene.ElementsChanged += OnElementsChanged;
            _scene.ElementsSelectionChanged += OnElementsSelectionChanged;
            _liftStateService.StateChanged += OnLiftStateChanged;
            _unitSettingsService.SettingsChanged += OnUnitSettingsChanged;
            _transformService.ReferenceChanged += OnReferenceChanged;
            ShowFilterDialogCommand = new RelayCommand(ShowFilterDialog);
            ApplyFilterCommand = new RelayCommand(ApplyFilters);
            SaveDensityCommand = new AsyncRelayCommand(SaveDensityAsync);
            _ = LoadDensityAsync();
        }

        public void Dispose()
        {
            _scene.ElementsChanged -= OnElementsChanged;
            _scene.ElementsSelectionChanged -= OnElementsSelectionChanged;
            _liftStateService.StateChanged -= OnLiftStateChanged;
            _unitSettingsService.SettingsChanged -= OnUnitSettingsChanged;
            if (_transformService != null)
            {
                _transformService.ReferenceChanged -= OnReferenceChanged;
            }
        }

        private void OnReferenceChanged(object? sender, EventArgs e)
        {
            LoadElementProperties();
        }

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<PropertyItem> ElementProperties { get; } = new();

        public (double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) GridSize =>
            _gridSizingService.CalculateGridSizeMeters(Elements.ToList());

        public ICommand ShowFilterDialogCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public AsyncRelayCommand SaveDensityCommand { get; }

        public string FilterStatus => $"Showing {GetVisibleElementCount()} of {Elements.Count} elements";

        public string Error => DensityValidationMessage;

        public string this[string columnName] =>
            columnName == nameof(ConcreteDensityKgPerM3)
                ? ValidateConcreteDensity(ConcreteDensityKgPerM3)
                : string.Empty;

        public double ConcreteDensityKgPerM3
        {
            get => _concreteDensityKgPerM3;
            set
            {
                if (Math.Abs(_concreteDensityKgPerM3 - value) < 0.000001)
                    return;

                _concreteDensityKgPerM3 = value;
                DensityValidationMessage = ValidateConcreteDensity(value);
                OnPropertyChanged();

                if (string.IsNullOrWhiteSpace(DensityValidationMessage))
                {
                    ElementWeightCalculator.ConcreteDensityKgPerCubicMeter = value;
                    RecalculateElementWeights();
                }
            }
        }

        public string DensityValidationMessage
        {
            get => _densityValidationMessage;
            private set
            {
                if (_densityValidationMessage == value)
                    return;

                _densityValidationMessage = value;
                OnPropertyChanged();
            }
        }

        private void RecalculateElementWeights()
        {
            foreach (var element in Elements)
            {
                if (!ElementWeightCalculator.IsEstimated(element))
                    continue;

                if (!IsConcreteElement(element))
                    continue;

                var volume = element.VolumeM3 is > 0d
                    ? element.VolumeM3.Value
                    : element.ElementVolume is > 0d
                        ? element.ElementVolume.Value
                        : 0d;

                if (volume <= 0d)
                    continue;

                var calculatedWeight = volume * ConcreteDensityKgPerM3 / 1000d;
                element.ElementWT = calculatedWeight;
                element.WeightTons = calculatedWeight;
            }

            ApplySearchFilter();
            LoadElementProperties();
        }

        private static bool IsConcreteElement(PrecastElement element)
        {
            var source = $"{element.ElementMaterial} {element.ElementType}".ToUpperInvariant();
            if (source.Contains("STEEL") || source.Contains("REBAR") || source.Contains("REINFORC") ||
                source.Contains("ALUMIN") || source.Contains("GLASS") || source.Contains("TIMBER") || source.Contains("WOOD"))
            {
                return false;
            }
            return true;
        }

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
                : new HashSet<string>(criteria.IncludedElementIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
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

            _ = LoadDensityAsync();

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

        private async Task LoadDensityAsync()
        {
            var session = _workspaceService.CurrentSession;
            if (session == null)
            {
                ConcreteDensityKgPerM3 = ProjectMaterialSettingsService.DefaultConcreteDensityKgPerM3;
                return;
            }

            try
            {
                ConcreteDensityKgPerM3 = await _materialSettingsService
                    .GetConcreteDensityKgPerM3Async(session.ProjectId);
            }
            catch (Exception ex)
            {
                DensityValidationMessage = $"Could not load density: {ex.Message}";
            }
        }

        private async Task SaveDensityAsync()
        {
            var validation = ValidateConcreteDensity(ConcreteDensityKgPerM3);
            DensityValidationMessage = validation;
            if (!string.IsNullOrWhiteSpace(validation))
                return;

            var session = _workspaceService.CurrentSession;
            if (session == null)
            {
                DensityValidationMessage = "Open a project before saving density.";
                return;
            }

            try
            {
                await _materialSettingsService
                    .SetConcreteDensityKgPerM3Async(session.ProjectId, ConcreteDensityKgPerM3);

                DensityValidationMessage = string.Empty;
            }
            catch (Exception ex)
            {
                DensityValidationMessage = $"Could not save density: {ex.Message}";
            }
        }

        private static string ValidateConcreteDensity(double density)
        {
            return density is < MinConcreteDensityKgPerM3 or > MaxConcreteDensityKgPerM3
                ? "Density must be between 1000 and 4000 kg/m3"
                : string.Empty;
        }

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

            var displayVolumeM3 =
    element.VolumeM3 is > 0d
        ? element.VolumeM3
        : element.ElementVolume is > 0d
            ? element.ElementVolume
            : element.BBoxLength is > 0d && element.BBoxWidth is > 0d && element.BBoxHeight is > 0d
                ? element.BBoxLength.Value * element.BBoxWidth.Value * element.BBoxHeight.Value
                : null;

            var displayWeightTons =
                element.WeightTons is > 0d
                    ? element.WeightTons.Value
                    : element.ElementWT is > 0d
                        ? element.ElementWT.Value
                        : ElementWeightCalculator.GetWeightTons(element);

            var displayWeight =
                displayWeightTons > 0d
                    ? ElementWeightCalculator.FormatWeight(
                        displayWeightTons,
                        _unitSettingsService.Current,
                        ElementWeightCalculator.IsEstimated(element),
                        CultureInfo.CurrentCulture)
                    : "-";

            return new ElementPanelDto(
                BuildLiftStateDto(liftState),
                GetElementID(element),
                GetPrecastDisplayName(element),
                CleanElementType(element.ElementType),
                string.IsNullOrWhiteSpace(element.ElementMaterial) ? "N/A" : element.ElementMaterial,
                element.ElementLength,
                element.ElementWidth,
                element.ElementHeight,
                displayWeight,
                displayVolumeM3,
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
            var properties = new List<PropertyItem>
            {
                CreateLiftStateGroup(panel.LiftState),

                CreateGroup("Identity", true,
                    CreateProperty("ID", GetReadableElementId(SelectedElement, panel.Id), ""),
                    CreateProperty("Name", panel.Name, ""),
                    CreateProperty("Type", panel.Type, ""),
                    CreateProperty("Material", panel.Material, "")),

                CreateGroup("Geometry", true,
                    CreateDisplayProperty("Length", FormatLength(panel.Length)),
                    CreateDisplayProperty("Width", FormatLength(panel.Width)),
                    CreateDisplayProperty("Height", FormatLength(panel.Height)),
                    CreateDisplayProperty("Weight", panel.Weight),
                    CreateDisplayProperty("Volume", FormatVolume(panel.Volume)))
            };

            var locationItems = new List<PropertyItem>
            {
                CreateDisplayProperty("Local X", FormatLength(panel.CenterX)),
                CreateDisplayProperty("Local Y", FormatLength(panel.CenterY)),
                CreateDisplayProperty("Local Z", FormatLength(panel.CenterZ))
            };

            if (CraneOptimizer.UI.Core.Configuration.AppFeatureFlags.ShowCoordinateReferenceUi && _transformService != null && _transformService.IsEnabled)
            {
                var cogPt = new DomainPoint3D(panel.CenterX, panel.CenterY, panel.CenterZ);
                var sitePt = _transformService.LocalToSurvey(cogPt);
                locationItems.Add(CreateDisplayProperty("Easting", FormatSurveyCoordinate(sitePt.X)));
                locationItems.Add(CreateDisplayProperty("Northing", FormatSurveyCoordinate(sitePt.Y)));
                locationItems.Add(CreateDisplayProperty("Elevation", FormatSurveyCoordinate(sitePt.Z)));
            }
            // Temporarily hidden by product decision. Keep logic intact for future re-enable.
            // else
            // {
            //     locationItems.Add(CreateDisplayProperty("Survey", "Not active / Local Only"));
            // }

            properties.Add(CreateGroup("Location", true, locationItems.ToArray()));

            return properties;
        }

        private static string GetReadableElementId(PrecastElement? element, string fallback)
        {
            if (element == null)
                return IsGenericOrMaterialId(fallback) ? "Unknown" : (string.IsNullOrWhiteSpace(fallback) ? "Unknown" : fallback);

            if (!string.IsNullOrWhiteSpace(element.ElementRevitId) && !IsGenericOrMaterialId(element.ElementRevitId))
                return element.ElementRevitId;

            if (!string.IsNullOrWhiteSpace(element.ElementMark) && !IsGenericOrMaterialId(element.ElementMark))
                return element.ElementMark;

            if (!string.IsNullOrWhiteSpace(element.ElementID) && !IsGenericOrMaterialId(element.ElementID))
                return element.ElementID;

            if (!string.IsNullOrWhiteSpace(fallback) && !IsGenericOrMaterialId(fallback))
                return fallback;

            return "Unknown";
        }

        private static bool IsGenericOrMaterialId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var v = value.Trim().ToUpperInvariant();

            return v == "C20"
                || v == "C25"
                || v == "C30"
                || v == "C35"
                || v == "C40"
                || v == "WALL"
                || v == "COLUMN"
                || v == "BEAM"
                || v == "SLAB"
                || v == "ROOF"
                || v == "UNKNOWN"
                || v.StartsWith("CONCRETE/")
                || v.StartsWith("QDC_CONCRETE")
                || v.Contains("CONCRETE");
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
            if (liftState?.CraneX == null || liftState.CraneY == null)
                return "-";

            var modelStr = $"X {FormatLength(liftState.CraneX)}, Y {FormatLength(liftState.CraneY)}";
            if (_transformService != null && _transformService.IsEnabled)
            {
                var localPt = new DomainPoint3D(liftState.CraneX.Value, liftState.CraneY.Value, 0.0);
                var sitePt = _transformService.LocalToSurvey(localPt);
                var siteStr = $"E {FormatLength(sitePt.X)}, N {FormatLength(sitePt.Y)}";
                return $"{modelStr} | {siteStr}";
            }

            return modelStr;
        }

        private static string GetElementID(PrecastElement e)
        {
            if (!string.IsNullOrWhiteSpace(e.ElementID))
                return e.ElementID;

            if (!string.IsNullOrWhiteSpace(e.ElementRevitId))
                return e.ElementRevitId;

            return "Unknown";
        }

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
            var weight =
                e.WeightTons is > 0d
                    ? e.WeightTons.Value
                    : e.ElementWT is > 0d
                        ? e.ElementWT.Value
                        : ElementWeightCalculator.GetWeightTons(e);

            return weight > 0d
                ? ElementWeightCalculator.FormatWeight(
                    weight,
                    _unitSettingsService.Current,
                    ElementWeightCalculator.IsEstimated(e),
                    CultureInfo.CurrentCulture)
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

        private string FormatSurveyCoordinate(double value)
        {
            return $"{value.ToString("F3", CultureInfo.CurrentCulture)} m";
        }

        private string FormatLength(double? value)
        {
            return value.HasValue
                ? UnitSystem.FormatLength(value.Value, _unitSettingsService.Current, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private string FormatVolume(double? value)
        {
            return value.HasValue
                ? UnitSystem.FormatVolume(value.Value, _unitSettingsService.Current, CultureInfo.CurrentCulture)
                : string.Empty;
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
            CollectionViewSource.GetDefaultView(FilteredElements)?.Refresh();
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
            string.IsNullOrWhiteSpace(material) ? "N/A" : material.Trim();
    }

}
