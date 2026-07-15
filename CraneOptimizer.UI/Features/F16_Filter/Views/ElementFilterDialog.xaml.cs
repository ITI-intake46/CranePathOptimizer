using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F16_Filter.ViewModels;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F16_Filter.Views
{
    public partial class ElementFilterDialog : Window
    {
        private readonly IElementVisibilityFilterService? _visibilityFilter;
        private readonly ISceneService? _scene;
        private readonly IViewportSelectionService? _selectionService;
        private bool _isSyncingControls;
        private List<FilterDialogOption> _allTypeOptions = new();
        private List<FilterDialogOption> _allMaterialOptions = new();
        private List<FilterDialogOption> _allStoreyOptions = new();
        private List<PrecastElement> _allSceneElements = new();
        private List<PrecastElement> _selectedScopeElements = new();
        private List<PrecastElement> _allElements = new();
        private readonly HashSet<string> _checkedElementIds = new(StringComparer.Ordinal);
        private ObservableCollection<FilterHierarchyNode> _hierarchyRoots = new();
        private bool _isSyncingHierarchy;
        private FilterScope _filterScope;
        private bool _isInitializing = true;
        private bool _isIsolated;
        private static readonly string[] StructuralTypeKeywords =
        [
            "IfcColumn", "IfcBeam", "IfcSlab", "IfcWall", "IfcStair",
            "IfcRoof", "IfcMember", "IfcPlate", "IfcFooting", "IfcPile", "IfcRamp"
        ];

        private enum FilterScope
        {
            All,
            Structural,
            Selected
        }

        public ElementFilterDialog()
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<IViewportSelectionService>();
            Owner = System.Windows.Application.Current.MainWindow;
            HookSelectionEvents();
            InitializeDialogElements(_visibilityFilter?.AllElements ?? Array.Empty<PrecastElement>());
        }

        public ElementFilterDialog(ElementManagementViewModel viewModel)
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<IViewportSelectionService>();
            Owner = System.Windows.Application.Current.MainWindow;
            HookSelectionEvents();
            DataContext = viewModel;

            InitializeDialogElements(viewModel.Elements, searchText: viewModel.SearchText);
        }

        public ElementFilterDialog(IReadOnlyCollection<PrecastElement> selectedElements)
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<IViewportSelectionService>();
            Owner = System.Windows.Application.Current.MainWindow;
            HookSelectionEvents();
            InitializeDialogElements(
                _visibilityFilter?.AllElements ?? Array.Empty<PrecastElement>(),
                selectedElements);
        }

        private void HookSelectionEvents()
        {
            if (_selectionService != null)
                _selectionService.SelectionChanged += OnSelectionChanged;

            Closed += ElementFilterDialog_Closed;
        }

        private void ElementFilterDialog_Closed(object? sender, EventArgs e)
        {
            if (_selectionService != null)
                _selectionService.SelectionChanged -= OnSelectionChanged;

            Closed -= ElementFilterDialog_Closed;
        }

        private void InitializeDialogElements(
            IEnumerable<PrecastElement> elements,
            IEnumerable<PrecastElement>? selectedElements = null,
            string? searchText = null)
        {
            _isInitializing = true;
            try
            {
                var gaElements = elements
                    .Where(e => e.Role == IfcElementRole.LiftAssembly && e.IncludeInGa)
                    .ToList();

                _allSceneElements = gaElements;
                _selectedScopeElements = ResolveSelectedScopeElements(
                    selectedElements ?? GetLiveSelectedElements());
                _filterScope = _selectedScopeElements.Count > 0
                    ? FilterScope.Selected
                    : FilterScope.All;
                InitializeCheckedElementIds();

                if (searchText != null)
                    TxtSearch.Text = searchText;

                RebuildScopeFilter();
            }
            finally
            {
                _isInitializing = false;
            }

            UpdatePreviewCount();
            UpdateVisibilityActionButtons();
        }

        private List<PrecastElement> ResolveSelectedScopeElements(IEnumerable<PrecastElement> selectedElements)
        {
            var selected = selectedElements.ToList();
            var selectedIds = selected
                .Select(element => element.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            if (selectedIds.Count == 0)
                return selected;

            var resolved = _allSceneElements
                .Where(element =>
                    !string.IsNullOrWhiteSpace(element.ElementID)
                    && selectedIds.Contains(element.ElementID))
                .ToList();

            return resolved.Count > 0 ? resolved : selected;
        }

        private void InitializeCheckedElementIds()
        {
            _checkedElementIds.Clear();
            var initialTargets = _selectedScopeElements.Count > 0
                ? _selectedScopeElements
                : _allSceneElements;

            foreach (var element in initialTargets)
            {
                if (!string.IsNullOrWhiteSpace(element.ElementID))
                    _checkedElementIds.Add(element.ElementID);
            }
        }

        private IEnumerable<PrecastElement> GetLiveSelectedElements() =>
            _scene?.GetSelectedElements() ?? Enumerable.Empty<PrecastElement>();

        private void OnSelectionChanged(IReadOnlyList<Core.Models.SelectionTarget> _)
        {
            var wasSelectedScope = _filterScope == FilterScope.Selected;
            var liveSelection = ResolveSelectedScopeElements(GetLiveSelectedElements());
            _selectedScopeElements = liveSelection;

            if (wasSelectedScope && _selectedScopeElements.Count == 0)
                _filterScope = FilterScope.All;

            if (wasSelectedScope)
                RebuildScopeFilter();
            else
                UpdateScopeButtons();
        }

        private void BtnScopeAll_Click(object sender, RoutedEventArgs e) =>
            SwitchElementScope(FilterScope.All);

        private void BtnScopeStructural_Click(object sender, RoutedEventArgs e) =>
            SwitchElementScope(FilterScope.Structural);

        private void BtnScopeSelected_Click(object sender, RoutedEventArgs e) =>
            SwitchElementScope(FilterScope.Selected);

        private void SwitchElementScope(FilterScope scope)
        {
            if (scope == FilterScope.Selected && _selectedScopeElements.Count == 0)
                return;

            if (_filterScope == scope)
                return;

            _filterScope = scope;
            RebuildScopeFilter();
        }

        private void RebuildScopeFilter()
        {
            _allElements = _filterScope switch
            {
                FilterScope.Selected => _selectedScopeElements.ToList(),
                FilterScope.Structural => _allSceneElements.Where(IsStructuralElement).ToList(),
                _ => _allSceneElements.ToList()
            };
            LoadTypeOptions(_allElements);
            LoadMaterialOptions(_allElements);
            LoadStoreyOptions(_allElements);
            UpdateWeightControlsForCurrentScope();
            ApplyHierarchyFilters();
            UpdateRangeChips();
            UpdateScopeButtons();
        }

        private static bool IsStructuralElement(PrecastElement element)
        {
            // RawIfcType holds the original IFC type name e.g. "IfcColumn"
            var rawType = element.RawIfcType ?? string.Empty;
            // ElementType holds the resolved display category e.g. "Column"
            var displayType = element.ElementType ?? string.Empty;

            return StructuralTypeKeywords.Any(keyword =>
                // Match raw IFC type directly (e.g. "IfcColumn" == "IfcColumn")
                rawType.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                // Match display type with full keyword (in case ElementType still has "Ifc" prefix)
                || displayType.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                // Match display type after stripping "Ifc" prefix: "IfcColumn" → "Column" == "Column"
                || (keyword.Length > 3
                    && keyword.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase)
                    && displayType.Contains(keyword[3..], StringComparison.OrdinalIgnoreCase)));
        }

        private void UpdateScopeButtons()
        {
            if (BtnScopeAll == null || BtnScopeStructural == null || BtnScopeSelected == null)
                return;

            var primaryStyle = TryFindResource("ButtonPrimary") as Style;
            var secondaryStyle = TryFindResource("ButtonSecondary") as Style;

            BtnScopeAll.Style = _filterScope == FilterScope.All ? primaryStyle : secondaryStyle;
            BtnScopeStructural.Style = _filterScope == FilterScope.Structural ? primaryStyle : secondaryStyle;
            BtnScopeSelected.Style = _filterScope == FilterScope.Selected ? primaryStyle : secondaryStyle;
            BtnScopeSelected.IsEnabled = _selectedScopeElements.Count > 0;
            BtnScopeSelected.Content = _selectedScopeElements.Count > 0
                ? $"Selected ({_selectedScopeElements.Count:N0})"
                : "Selected";

            TxtSubtitle.Text = _filterScope switch
            {
                FilterScope.Selected =>
                    $"Filtering only the {_selectedScopeElements.Count:N0} selected element{(_selectedScopeElements.Count == 1 ? string.Empty : "s")}.",
                FilterScope.Structural => "Filtering structural IFC elements.",
                _ => "Choose IFC types, materials, levels, and weight range to hide or show elements in the 3D view."
            };
        }

        private void UpdateVisibilityActionButtons()
        {
            var checkedElements = GetCheckedElements();
            var hasCheckedElements = checkedElements.Count > 0;

            // Hide / Show Checked toggle
            // Rule: all checked hidden → "Show Checked" (active/terracotta); otherwise → "Hide Checked" (secondary)
            var allCheckedHidden = hasCheckedElements
                && checkedElements.All(e => _visibilityFilter?.IsElementHidden(e) == true);

            if (BtnHideChecked != null)
            {
                BtnHideChecked.IsEnabled = hasCheckedElements;
                BtnHideChecked.Tag     = allCheckedHidden ? "True" : "False";  // drives terracotta via ActionToggleButtonStyle
                BtnHideChecked.Content = allCheckedHidden ? "Show Checked" : "Hide Checked";
                BtnHideChecked.ToolTip = allCheckedHidden
                    ? "All checked elements are hidden. Click to show them."
                    : "Hide all checked elements, including those outside the current hierarchy filter.";
            }

            // Isolate / End Isolation toggle
            if (BtnIsolateChecked != null)
            {
                BtnIsolateChecked.IsEnabled = hasCheckedElements || _isIsolated;
                BtnIsolateChecked.Tag     = _isIsolated ? "True" : "False";  // drives terracotta
                BtnIsolateChecked.Content = _isIsolated ? "End Isolation" : "Isolate Checked";
                BtnIsolateChecked.ToolTip = _isIsolated
                    ? "Click to exit isolation and restore all element visibility."
                    : "Show only the checked elements in the 3D view.";
            }

            if (TxtCheckedCount != null)
                TxtCheckedCount.Text = $"{_checkedElementIds.Count:N0} checked";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyHierarchyFilters();
        private void FilterOption_Changed(object sender, RoutedEventArgs e) => ApplyHierarchyFilters();
        private void HierarchyOption_Changed(object sender, RoutedEventArgs e) => OnHierarchyOptionChanged(sender);
        private void OptionSearch_Changed(object sender, TextChangedEventArgs e) => RefreshOptionViews();
        private void HierarchyMode_Changed(object sender, SelectionChangedEventArgs e) => ApplyHierarchyFilters();
        private void Sort_Changed(object sender, RoutedEventArgs e) => ApplyHierarchyFilters();
        private void Sort_Changed(object sender, SelectionChangedEventArgs e) => ApplyHierarchyFilters();
        private void BtnExpandHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(true);
        private void BtnCollapseHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(false);

        private void BtnSelectAllTypes_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allTypeOptions, true); ApplyHierarchyFilters(); }
        private void BtnClearAllTypes_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allTypeOptions, false); ApplyHierarchyFilters(); }
        private void BtnSelectAllMaterials_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allMaterialOptions, true); ApplyHierarchyFilters(); }
        private void BtnClearAllMaterials_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allMaterialOptions, false); ApplyHierarchyFilters(); }
        private void BtnSelectAllStoreys_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allStoreyOptions, true); ApplyHierarchyFilters(); }
        private void BtnClearAllStoreys_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allStoreyOptions, false); ApplyHierarchyFilters(); }
        private void BtnInvertTypes_Click(object sender, RoutedEventArgs e) { InvertChecked(_allTypeOptions); ApplyHierarchyFilters(); }

        private void WeightRange_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingControls)
                return;

            _isSyncingControls = true;
            var min = ParseDouble(TxtWeightMin.Text, SliderWeightMin.Minimum);
            var max = ParseDouble(TxtWeightMax.Text, SliderWeightMax.Maximum);
            if (min > max)
                max = min;

            SliderWeightMin.Value = Clamp(min, SliderWeightMin.Minimum, SliderWeightMin.Maximum);
            SliderWeightMax.Value = Clamp(max, SliderWeightMax.Minimum, SliderWeightMax.Maximum);
            _isSyncingControls = false;
            UpdateRangeChips();
            ApplyHierarchyFilters();
        }

        private void SliderWeightMin_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingControls || TxtWeightMin == null)
                return;

            _isSyncingControls = true;
            var value = Math.Min(e.NewValue, SliderWeightMax.Value);
            if (!value.Equals(e.NewValue))
                SliderWeightMin.Value = value;
            TxtWeightMin.Text = value.ToString("0", CultureInfo.CurrentCulture);
            _isSyncingControls = false;
            UpdateRangeChips();
            UpdateWeightRangeSliderVisual();
            ApplyHierarchyFilters();
        }

        private void SliderWeightMax_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingControls || TxtWeightMax == null)
                return;

            _isSyncingControls = true;
            var value = Math.Max(e.NewValue, SliderWeightMin.Value);
            if (!value.Equals(e.NewValue))
                SliderWeightMax.Value = value;
            TxtWeightMax.Text = value.ToString("0", CultureInfo.CurrentCulture);
            _isSyncingControls = false;
            UpdateRangeChips();
            UpdateWeightRangeSliderVisual();
            ApplyHierarchyFilters();
        }

        private void WeightRangeSlider_SizeChanged(object sender, SizeChangedEventArgs e) =>
            UpdateWeightRangeSliderVisual();

        private void ThumbWeightMin_DragDelta(object sender, DragDeltaEventArgs e) =>
            SetWeightRangeThumbFromDelta(ThumbWeightMin, e.HorizontalChange, isMinimumThumb: true);

        private void ThumbWeightMax_DragDelta(object sender, DragDeltaEventArgs e) =>
            SetWeightRangeThumbFromDelta(ThumbWeightMax, e.HorizontalChange, isMinimumThumb: false);

        private void WeightRangeSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isSyncingControls || SliderWeightMin == null || SliderWeightMax == null || WeightRangeSlider == null)
                return;

            var point = e.GetPosition(WeightRangeSlider);
            var value = GetWeightValueFromTrackX(point.X);
            if (Math.Abs(value - SliderWeightMin.Value) <= Math.Abs(value - SliderWeightMax.Value))
                SliderWeightMin.Value = Math.Min(value, SliderWeightMax.Value);
            else
                SliderWeightMax.Value = Math.Max(value, SliderWeightMin.Value);

            e.Handled = true;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _filterScope = FilterScope.All;

            _checkedElementIds.Clear();
            foreach (var element in _allSceneElements)
            {
                if (!string.IsNullOrWhiteSpace(element.ElementID))
                    _checkedElementIds.Add(element.ElementID);
            }

            if (TxtSearch != null)
                TxtSearch.Text = string.Empty;

            // Rebuilds _allElements, type/material/storey option lists, and weight slider bounds
            // for the All scope. Does NOT touch 3D visibility.
            RebuildScopeFilter();

            // Snap weight controls back to full available bounds AFTER the scope rebuild
            // has refreshed the slider Min/Max values for the new element set.
            ResetWeightRangeToAvailableBounds();

            ApplyHierarchyFilters();
            UpdateVisibilityActionButtons();
        }

        /// <summary>
        /// Resets the weight range slider and text boxes to the full available min/max
        /// without triggering redundant hierarchy refreshes.
        /// Does NOT touch 3D visibility.
        /// </summary>
        private void ResetWeightRangeToAvailableBounds()
        {
            if (SliderWeightMin == null || SliderWeightMax == null ||
                TxtWeightMin == null   || TxtWeightMax == null)
                return;

            _isSyncingControls = true;

            SliderWeightMin.Value = SliderWeightMin.Minimum;
            SliderWeightMax.Value = SliderWeightMax.Maximum;

            TxtWeightMin.Text = SliderWeightMin.Minimum.ToString("0", CultureInfo.CurrentCulture);
            TxtWeightMax.Text = SliderWeightMax.Maximum.ToString("0", CultureInfo.CurrentCulture);

            _isSyncingControls = false;

            UpdateRangeChips();
            UpdateWeightRangeSliderVisual();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // BtnShowChecked has been removed from XAML (merged into BtnHideChecked toggle).
        // This handler is kept only so existing callers do not cause a compile error.
        private void BtnShowChecked_Click(object sender, RoutedEventArgs e) { }

        private void BtnHideChecked_Click(object sender, RoutedEventArgs e)
        {
            var elements = GetCheckedElements();
            if (elements.Count == 0)
                return;

            // Exit isolation cleanly before applying hide/show
            if (_isIsolated)
            {
                _isIsolated = false;
                _visibilityFilter?.ShowAll();
            }

            // Toggle: all checked already hidden → show them; otherwise → hide them
            var allCheckedHidden = elements.All(e => _visibilityFilter?.IsElementHidden(e) == true);
            if (allCheckedHidden)
                _visibilityFilter?.ShowElements(elements);
            else
                _visibilityFilter?.HideElements(elements);

            ApplyHierarchyFilters();
            UpdateVisibilityActionButtons();
        }

        private void BtnIsolateChecked_Click(object sender, RoutedEventArgs e)
        {
            var elements = GetCheckedElements();

            if (_isIsolated)
            {
                // End Isolation — restore all visibility
                _isIsolated = false;
                _visibilityFilter?.ShowAll();
            }
            else
            {
                if (elements.Count == 0)
                    return;

                _isIsolated = true;
                _visibilityFilter?.IsolateElements(elements);
            }

            ApplyHierarchyFilters();
            UpdateVisibilityActionButtons();
        }

        private void BtnShowAll_Click(object sender, RoutedEventArgs e)
        {
            _isIsolated = false;
            _visibilityFilter?.ShowAll();
            ApplyHierarchyFilters();
            UpdateVisibilityActionButtons();
        }

        private List<PrecastElement> GetCheckedElements()
        {
            return _allSceneElements
                .Where(element =>
                    !string.IsNullOrWhiteSpace(element.ElementID)
                    && _checkedElementIds.Contains(element.ElementID))
                .ToList();
        }

        private void UpdateRangeChips()
        {
            if (TxtWeightChip != null)
                TxtWeightChip.Text = $"Weight: {ParseDouble(TxtWeightMin?.Text ?? string.Empty, 0):N0} - {ParseDouble(TxtWeightMax?.Text ?? string.Empty, 0):N0} kg";

            if (TxtWeightRangeSummary != null)
                TxtWeightRangeSummary.Text = $"Selected: {ParseDouble(TxtWeightMin?.Text ?? string.Empty, 0):N0} - {ParseDouble(TxtWeightMax?.Text ?? string.Empty, 0):N0} kg";

            if (TxtAvailableWeightRange != null)
                TxtAvailableWeightRange.Text = $"Available: {SliderWeightMin?.Minimum ?? 0:N0} - {SliderWeightMax?.Maximum ?? 0:N0} kg";

            if (TxtHeightChip == null)
                return;

            var heights = _allElements
                .Select(e => e.ElementHeight ?? e.BBoxHeight ?? 0d)
                .Where(height => height > 0)
                .ToList();
            var min = heights.Count == 0 ? 0 : heights.Min();
            var max = heights.Count == 0 ? 0 : heights.Max();
            TxtHeightChip.Text = $"Height: {min:0.##} - {max:0.##} m";
        }

        private void ApplyHierarchyFilters()
        {
            if (HierarchyTree == null)
                return;

            BuildHierarchy(GetElementsMatchingCurrentFilters(includeWeightRange: true));
            UpdatePreviewCount();
            UpdateVisibilityActionButtons();
            UpdateScopeButtons();
        }

        private void UpdateWeightControlsForCurrentScope()
        {
            var minSlider = SliderWeightMin;
            var maxSlider = SliderWeightMax;
            var minTextBox = TxtWeightMin;
            var maxTextBox = TxtWeightMax;

            if (_isSyncingControls
                || minSlider == null
                || maxSlider == null
                || minTextBox == null
                || maxTextBox == null)
                return;

            var oldMin = minSlider.Minimum;
            var oldMax = maxSlider.Maximum;
            var currentMin = ParseDouble(minTextBox.Text, oldMin);
            var currentMax = ParseDouble(maxTextBox.Text, oldMax);
            var weights = _allElements.Select(ElementWeightCalculator.GetWeightKg).ToList();
            var newMin = weights.Count == 0 ? 0d : Math.Floor(weights.Min());
            var newMax = weights.Count == 0 ? 0d : Math.Ceiling(weights.Max());

            if (newMax < newMin)
                newMax = newMin;

            var minWasPinned = currentMin <= oldMin + 0.001d;
            var maxWasPinned = currentMax >= oldMax - 0.001d;

            currentMin = minWasPinned ? newMin : Clamp(currentMin, newMin, newMax);
            currentMax = maxWasPinned ? newMax : Clamp(currentMax, newMin, newMax);
            if (currentMin > currentMax)
                currentMin = currentMax;

            _isSyncingControls = true;
            minSlider.Minimum = newMin;
            minSlider.Maximum = newMax;
            maxSlider.Minimum = newMin;
            maxSlider.Maximum = newMax;
            minSlider.Value = currentMin;
            maxSlider.Value = currentMax;
            minTextBox.Text = currentMin.ToString("0", CultureInfo.CurrentCulture);
            maxTextBox.Text = currentMax.ToString("0", CultureInfo.CurrentCulture);
            _isSyncingControls = false;

            UpdateRangeChips();
            UpdateWeightRangeSliderVisual();
        }

        private void SetWeightRangeThumbFromDelta(Thumb thumb, double horizontalChange, bool isMinimumThumb)
        {
            if (_isSyncingControls || SliderWeightMin == null || SliderWeightMax == null)
                return;

            var thumbCenter = Canvas.GetLeft(thumb) + (thumb.ActualWidth / 2d) + horizontalChange;
            var value = GetWeightValueFromTrackX(thumbCenter);

            if (isMinimumThumb)
                SliderWeightMin.Value = Math.Min(value, SliderWeightMax.Value);
            else
                SliderWeightMax.Value = Math.Max(value, SliderWeightMin.Value);
        }

        private double GetWeightValueFromTrackX(double x)
        {
            if (WeightRangeSlider == null || SliderWeightMin == null || SliderWeightMax == null)
                return 0d;

            var thumbWidth = Math.Max(ThumbWeightMin?.ActualWidth ?? 14d, 14d);
            var trackLeft = thumbWidth / 2d;
            var trackWidth = Math.Max(1d, WeightRangeSlider.ActualWidth - thumbWidth);
            var ratio = Clamp((x - trackLeft) / trackWidth, 0d, 1d);

            return SliderWeightMin.Minimum
                + ((SliderWeightMin.Maximum - SliderWeightMin.Minimum) * ratio);
        }

        private void UpdateWeightRangeSliderVisual()
        {
            if (WeightRangeSlider == null
                || WeightRangeTrackBase == null
                || WeightRangeTrackSelection == null
                || ThumbWeightMin == null
                || ThumbWeightMax == null
                || SliderWeightMin == null
                || SliderWeightMax == null)
                return;

            var thumbWidth = Math.Max(ThumbWeightMin.ActualWidth, 14d);
            var trackLeft = thumbWidth / 2d;
            var trackWidth = Math.Max(0d, WeightRangeSlider.ActualWidth - thumbWidth);

            WeightRangeTrackBase.Width = trackWidth;
            Canvas.SetLeft(WeightRangeTrackBase, trackLeft);

            var range = SliderWeightMin.Maximum - SliderWeightMin.Minimum;
            var minRatio = range <= 0d ? 0d : (SliderWeightMin.Value - SliderWeightMin.Minimum) / range;
            var maxRatio = range <= 0d ? 0d : (SliderWeightMax.Value - SliderWeightMax.Minimum) / range;
            var minX = trackLeft + (Clamp(minRatio, 0d, 1d) * trackWidth);
            var maxX = trackLeft + (Clamp(maxRatio, 0d, 1d) * trackWidth);

            Canvas.SetLeft(WeightRangeTrackSelection, minX);
            WeightRangeTrackSelection.Width = Math.Max(0d, maxX - minX);
            Canvas.SetLeft(ThumbWeightMin, minX - (thumbWidth / 2d));
            Canvas.SetLeft(ThumbWeightMax, maxX - (thumbWidth / 2d));
        }

        private void BuildHierarchy(IEnumerable<PrecastElement>? elements = null)
        {
            if (HierarchyTree == null)
                return;

            var hierarchyElements = elements?.ToList() ?? _allElements;
            var mode = (CboHierarchyBy?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Type - Element";
            Func<PrecastElement, string> groupKey = mode.StartsWith("Material", StringComparison.OrdinalIgnoreCase)
                ? e => string.IsNullOrWhiteSpace(e.ElementMaterial) ? "N/A" : e.ElementMaterial.Trim()
                : mode.StartsWith("Storey", StringComparison.OrdinalIgnoreCase)
                    ? e => _visibilityFilter?.GetStorey(e) ?? "Unassigned"
                    : e => string.IsNullOrWhiteSpace(e.ElementType) ? "Unknown type" : e.ElementType.Trim();
            var elementSort = GetElementSortKey();
            var sortDescending = RbSortDescending?.IsChecked == true;

            _isSyncingHierarchy = true;
            _hierarchyRoots = new ObservableCollection<FilterHierarchyNode>(
                hierarchyElements
                    .GroupBy(groupKey, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key)
                    .Select(group =>
                    {
                        var parent = FilterHierarchyNode.CreateGroup(group.Key);
                        var orderedElements = sortDescending
                            ? group.OrderByDescending(elementSort).ThenBy(ElementDisplayNameConverter.GetDisplayName)
                            : group.OrderBy(elementSort).ThenBy(ElementDisplayNameConverter.GetDisplayName);

                        foreach (var element in orderedElements)
                        {
                            var weightKg = ElementWeightCalculator.GetWeightKg(element);
                            var weightTons = ElementWeightCalculator.GetWeightTons(element);
                            parent.Children.Add(FilterHierarchyNode.CreateElement(
                                element,
                                ElementDisplayNameConverter.GetDisplayName(element),
                                ElementWeightCalculator.FormatWeight(weightTons, ElementWeightCalculator.IsEstimated(element)),
                                weightKg,
                                element.ElementMaterial,
                                !string.IsNullOrWhiteSpace(element.ElementID)
                                    && _checkedElementIds.Contains(element.ElementID),
                                _visibilityFilter?.IsElementHidden(element) ?? false,
                                parent));
                        }

                        parent.RefreshFromChildren();
                        parent.IsExpanded = parent.Children.Count <= 40;
                        return parent;
                    }));
            HierarchyTree.ItemsSource = _hierarchyRoots;
            _isSyncingHierarchy = false;
        }

        private void OnHierarchyOptionChanged(object sender)
        {
            if (_isSyncingHierarchy)
                return;

            if ((sender as FrameworkElement)?.DataContext is not FilterHierarchyNode node)
                return;

            _isSyncingHierarchy = true;
            if (node.Children.Count > 0)
            {
                node.SetChildrenChecked(node.IsChecked);
                foreach (var child in node.Children)
                    UpdateCheckedElementId(child.Element, child.IsChecked);
            }
            else
            {
                UpdateCheckedElementId(node.Element, node.IsChecked);
            }

            node.Parent?.RefreshFromChildren();
            _isSyncingHierarchy = false;
            UpdateVisibilityActionButtons();
        }

        private void UpdateCheckedElementId(PrecastElement? element, bool isChecked)
        {
            if (element == null || string.IsNullOrWhiteSpace(element.ElementID))
                return;

            if (isChecked)
                _checkedElementIds.Add(element.ElementID);
            else
                _checkedElementIds.Remove(element.ElementID);
        }

        private void SetHierarchyExpanded(bool isExpanded)
        {
            foreach (var node in _hierarchyRoots)
                node.SetExpandedRecursive(isExpanded);
        }

        private string GetSortBy() =>
            (CboSortBy?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Name";

        private Func<PrecastElement, object> GetElementSortKey()
        {
            return GetSortBy() switch
            {
                "Type" => e => e.ElementType ?? string.Empty,
                "Weight" => e => ElementWeightCalculator.GetWeightKg(e),
                "Height" => e => e.ElementHeight ?? e.BBoxHeight ?? 0d,
                "Material" => e => e.ElementMaterial ?? string.Empty,
                "Storey / Level" => e => _visibilityFilter?.GetStorey(e) ?? "Unassigned",
                _ => e => ElementDisplayNameConverter.GetDisplayName(e)
            };
        }

        private static void SetAllChecked(IEnumerable<FilterDialogOption> options, bool isChecked)
        {
            foreach (var option in options)
                option.IsChecked = isChecked;
        }

        private static void InvertChecked(IEnumerable<FilterDialogOption> options)
        {
            foreach (var option in options)
                option.IsChecked = !option.IsChecked;
        }

        private void LoadTypeOptions(IEnumerable<PrecastElement> elements)
        {
            _allTypeOptions = elements
                .Select(e => string.IsNullOrWhiteSpace(e.ElementType) ? "Unknown type" : e.ElementType.Trim())
                .GroupBy(type => type, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new FilterDialogOption(g.Key, g.Count(), IsTypeChecked(g.Key)))
                .ToList();
            RefreshOptionViews();
        }

        private void LoadMaterialOptions(IEnumerable<PrecastElement> elements)
        {
            _allMaterialOptions = elements
                .Select(e => string.IsNullOrWhiteSpace(e.ElementMaterial) ? "N/A" : e.ElementMaterial.Trim())
                .GroupBy(material => material, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new FilterDialogOption(g.Key, g.Count(), IsMaterialChecked(g.Key)))
                .ToList();
            RefreshOptionViews();
        }

        private void LoadStoreyOptions(IEnumerable<PrecastElement> elements)
        {
            _allStoreyOptions = elements
                .Select(e => _visibilityFilter?.GetStorey(e) ?? "Unassigned")
                .GroupBy(storey => storey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new FilterDialogOption(g.Key, g.Count(), IsStoreyChecked(g.Key)))
                .ToList();
            RefreshOptionViews();
        }

        private void RefreshOptionViews()
        {
            UpdatePreviewCount();
        }

        private static List<FilterDialogOption> FilterOptions(IEnumerable<FilterDialogOption> options, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return options.ToList();

            return options
                .Where(option => option.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private bool IsTypeChecked(string type) => _visibilityFilter?.IsTypeEnabled(type) ?? true;
        private bool IsMaterialChecked(string material) => _visibilityFilter?.IsMaterialEnabled(material) ?? true;
        private bool IsStoreyChecked(string storey) => _visibilityFilter?.IsStoreyEnabled(storey) ?? true;

        private List<string> GetSelectedTypes() => GetSelected(_allTypeOptions);
        private List<string> GetSelectedMaterials() => GetSelected(_allMaterialOptions);
        private List<string> GetSelectedStoreys() => GetSelected(_allStoreyOptions);

        private static List<string> GetSelected(IEnumerable<FilterDialogOption> options)
        {
            return options
                .Where(t => t.IsChecked)
                .Select(t => t.DisplayName)
                .ToList();
        }

        private static double ParseDouble(string text, double fallback)
        {
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value)
                ? value
                : fallback;
        }

        private static double Clamp(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));

        private void UpdatePreviewCount()
        {
            if (TxtPreviewCount == null)
                return;

            var total = _allElements.Count;
            var label = _filterScope == FilterScope.Selected ? "selected" : "elements";
            TxtPreviewCount.Text = $"Showing {CalculatePreviewCount()} of {total} {label}";
        }

        private int CalculatePreviewCount()
        {
            return GetElementsMatchingCurrentFilters(includeWeightRange: true).Count();
        }

        // ── Filter category helpers ──────────────────────────────────────────────
        // OrdinalIgnoreCase is intentional here: these match human-readable display
        // labels (type names, material names, storey names), not IFC GlobalIds.

        private HashSet<string> GetCheckedTypeNames() =>
            _allTypeOptions
                .Where(o => o.IsChecked)
                .Select(o => o.DisplayName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> GetCheckedMaterialNames() =>
            _allMaterialOptions
                .Where(o => o.IsChecked)
                .Select(o => o.DisplayName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> GetCheckedStoreyNames() =>
            _allStoreyOptions
                .Where(o => o.IsChecked)
                .Select(o => o.DisplayName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private List<PrecastElement> GetElementsMatchingCurrentFilters(bool includeWeightRange)
        {
            var minWeight = ParseDouble(TxtWeightMin?.Text ?? string.Empty, 0);
            var maxWeight = ParseDouble(TxtWeightMax?.Text ?? string.Empty, double.MaxValue);
            var search = TxtSearch?.Text?.Trim() ?? string.Empty;

            // Build filter sets once — avoids re-evaluating LINQ inside Where predicate
            var checkedTypes     = GetCheckedTypeNames();
            var checkedMaterials = GetCheckedMaterialNames();
            var checkedStoreys   = GetCheckedStoreyNames();

            return _allElements.Where(element =>
            {
                var weight = ElementWeightCalculator.GetWeightKg(element);

                // Normalise to the same labels used when building the option lists
                var type = string.IsNullOrWhiteSpace(element.ElementType)
                    ? "Unknown type"
                    : element.ElementType.Trim();

                var material = string.IsNullOrWhiteSpace(element.ElementMaterial)
                    ? "N/A"
                    : element.ElementMaterial.Trim();

                var storey = _visibilityFilter?.GetStorey(element) ?? "Unassigned";

                return (!includeWeightRange || (weight >= minWeight && weight <= maxWeight))
                    && (checkedTypes.Count == 0     || checkedTypes.Contains(type))
                    && (checkedMaterials.Count == 0 || checkedMaterials.Contains(material))
                    && (checkedStoreys.Count == 0   || checkedStoreys.Contains(storey))
                    && MatchesSearch(element, search);
            }).ToList();
        }

        private static bool MatchesSearch(PrecastElement element, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            var name = ElementDisplayNameConverter.GetDisplayName(element);
            return Contains(name, search)
                || Contains(element.ElementType, search)
                || Contains(element.ElementMark, search)
                || Contains(element.ElementMaterial, search)
                || Contains(element.ElementID, search)
                || Contains(element.ElementRevitId, search);
        }

        private static bool Contains(string? value, string search) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

}
