using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.DTOs;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F16_Filter.ViewModels;
using CraneOptimizer.UI.Features.F17_Selection.Services;
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
        private readonly ElementManagementViewModel? _viewModel;
        private readonly IElementVisibilityFilterService? _visibilityFilter;
        private readonly ISceneService? _scene;
        private readonly ISelectionService? _selectionService;
        private bool _isSyncingControls;
        private List<FilterDialogOption> _allTypeOptions = new();
        private List<FilterDialogOption> _allMaterialOptions = new();
        private List<FilterDialogOption> _allStoreyOptions = new();
        private List<PrecastElement> _allSceneElements = new();
        private List<PrecastElement> _selectedScopeElements = new();
        private List<PrecastElement> _selectionSnapshotElements = new();
        private List<PrecastElement> _allElements = new();
        private ObservableCollection<FilterHierarchyNode> _hierarchyRoots = new();
        private bool _isSyncingHierarchy;
        private bool _showSelectedScope;
        private bool _isInitializing;
        private SelectionVisibilityAction _activeSelectionVisibilityAction;

        private enum SelectionVisibilityAction
        {
            None,
            Isolate,
            Hide
        }

        public ElementFilterDialog()
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<ISelectionService>();
            HookSelectionEvents();
            InitializeDialogElements(_visibilityFilter?.AllElements ?? Array.Empty<PrecastElement>());
        }

        public ElementFilterDialog(ElementManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<ISelectionService>();
            HookSelectionEvents();
            DataContext = viewModel;

            InitializeDialogElements(viewModel.Elements, searchText: viewModel.SearchText);
        }

        /// <summary>
        /// Opens the filter dialog with the Selected scope active when elements are supplied.
        /// </summary>
        public ElementFilterDialog(IReadOnlyCollection<PrecastElement> selectedElements)
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            _selectionService = App.ServiceProvider.GetService<ISelectionService>();
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
                _allSceneElements = elements.ToList();
                _selectedScopeElements = ResolveSelectedScopeElements(selectedElements ?? GetLiveSelectedElements());
                _selectionSnapshotElements = _selectedScopeElements.ToList();
                _showSelectedScope = _selectedScopeElements.Count > 0;

                if (searchText != null)
                    TxtSearch.Text = searchText;

                RebuildScopeFilter();
            }
            finally
            {
                _isInitializing = false;
            }

            UpdatePreviewCount();
            UpdateSelectionActionButtons();
        }

        private List<PrecastElement> ResolveSelectedScopeElements(IEnumerable<PrecastElement> selectedElements)
        {
            var selected = selectedElements.ToList();
            var selectedIds = selected
                .Select(e => e.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedIds.Count == 0)
                return selected;

            var resolved = _allSceneElements
                .Where(e => !string.IsNullOrWhiteSpace(e.ElementID) && selectedIds.Contains(e.ElementID))
                .ToList();

            return resolved.Count > 0 ? resolved : selected;
        }

        private IEnumerable<PrecastElement> GetLiveSelectedElements()
        {
            if (_selectionService?.SelectedElements.Count > 0)
                return _selectionService.SelectedElements;

            return _scene?.GetSelectedElements() ?? Enumerable.Empty<PrecastElement>();
        }

        private IEnumerable<PrecastElement> GetCurrentSelectedElements()
        {
            var live = GetLiveSelectedElements().ToList();
            return live.Count > 0 ? live : _selectionSnapshotElements;
        }

        private List<PrecastElement> GetSelectionActionElements()
        {
            var live = ResolveSelectedScopeElements(GetLiveSelectedElements());
            if (live.Count > 0)
                return live;

            return _activeSelectionVisibilityAction == SelectionVisibilityAction.None
                ? new List<PrecastElement>()
                : _selectionSnapshotElements.ToList();
        }

        private void OnSelectionChanged()
        {
            var liveSelection = ResolveSelectedScopeElements(GetLiveSelectedElements());
            if (liveSelection.Count > 0)
            {
                _selectedScopeElements = liveSelection;
                _selectionSnapshotElements = liveSelection.ToList();
            }
            else if (_activeSelectionVisibilityAction == SelectionVisibilityAction.None)
            {
                _selectedScopeElements.Clear();
                _selectionSnapshotElements.Clear();
            }

            if (_showSelectedScope && _selectedScopeElements.Count == 0)
                _showSelectedScope = false;

            if (_showSelectedScope)
                RebuildScopeFilter();
            else
                UpdateScopeButtons();

            UpdateSelectionActionButtons();
        }

        private void UpdateSelectionActionButtons()
        {
            var hasSelection = GetSelectionActionElements().Count > 0;
            if (!hasSelection)
                _activeSelectionVisibilityAction = SelectionVisibilityAction.None;

            var primaryStyle = TryFindResource("ButtonPrimary") as Style;
            var secondaryStyle = TryFindResource("ButtonSecondary") as Style;

            if (BtnShowSelectedOnly != null)
            {
                BtnShowSelectedOnly.IsEnabled = hasSelection;
                BtnShowSelectedOnly.Style = _activeSelectionVisibilityAction == SelectionVisibilityAction.Isolate
                    ? primaryStyle
                    : secondaryStyle;
            }

            if (BtnHideSelected != null)
            {
                BtnHideSelected.IsEnabled = hasSelection;
                BtnHideSelected.Style = _activeSelectionVisibilityAction == SelectionVisibilityAction.Hide
                    ? primaryStyle
                    : secondaryStyle;
            }
        }

        private void BtnScopeAll_Click(object sender, RoutedEventArgs e) => SwitchElementScope(showSelectedOnly: false);
        private void BtnScopeSelected_Click(object sender, RoutedEventArgs e) => SwitchElementScope(showSelectedOnly: true);

        private void SwitchElementScope(bool showSelectedOnly)
        {
            if (showSelectedOnly && _selectedScopeElements.Count == 0)
                return;

            if (_showSelectedScope == showSelectedOnly)
                return;

            _showSelectedScope = showSelectedOnly;
            RebuildScopeFilter();
        }

        private void RebuildScopeFilter()
        {
            _allElements = (_showSelectedScope ? _selectedScopeElements : _allSceneElements).ToList();
            LoadTypeOptions(_allElements);
            LoadMaterialOptions(_allElements);
            LoadStoreyOptions(_allElements);
            if (_showSelectedScope)
            {
                SetAllChecked(_allTypeOptions, true);
                SetAllChecked(_allMaterialOptions, true);
                SetAllChecked(_allStoreyOptions, true);
            }
            BuildHierarchy();
            if (_showSelectedScope)
                SetHierarchyChecked(true);
            InitializeWeightControls();
            UpdateRangeChips();
            UpdateScopeButtons();
            UpdatePreviewCount();
        }

        private void UpdateScopeButtons()
        {
            if (BtnScopeAll == null || BtnScopeSelected == null)
                return;

            var primaryStyle = TryFindResource("ButtonPrimary") as Style;
            var secondaryStyle = TryFindResource("ButtonSecondary") as Style;

            BtnScopeAll.Style = _showSelectedScope ? secondaryStyle : primaryStyle;
            BtnScopeSelected.Style = _showSelectedScope ? primaryStyle : secondaryStyle;
            BtnScopeSelected.IsEnabled = _selectedScopeElements.Count > 0;
            BtnScopeSelected.Content = _selectedScopeElements.Count > 0
                ? $"Selected ({_selectedScopeElements.Count:N0})"
                : "Selected";

            TxtSubtitle.Text = _showSelectedScope
                ? $"Filtering only the {_selectedScopeElements.Count:N0} selected element{(_selectedScopeElements.Count == 1 ? string.Empty : "s")}."
                : "Choose IFC types, materials, levels, and weight range to hide or show elements in the 3D view.";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => NonWeightFilterChanged();
        private void FilterOption_Changed(object sender, RoutedEventArgs e) => NonWeightFilterChanged();
        private void HierarchyOption_Changed(object sender, RoutedEventArgs e) => OnHierarchyOptionChanged(sender);
        private void AnyFilter_Changed(object sender, RoutedEventArgs e) => FilterChanged();
        private void HierarchyMode_Changed(object sender, SelectionChangedEventArgs e) => BuildHierarchy();
        private void Sort_Changed(object sender, RoutedEventArgs e) => BuildHierarchy();
        private void Sort_Changed(object sender, SelectionChangedEventArgs e) => BuildHierarchy();
        private void BtnExpandHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(true);
        private void BtnCollapseHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(false);

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
            FilterChanged();
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
            FilterChanged();
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
            FilterChanged();
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
            var selectionToRestore = GetCurrentSelectedElements().ToList();

            _isInitializing = true;
            try
            {
                _visibilityFilter?.ResetFilters();
                _activeSelectionVisibilityAction = SelectionVisibilityAction.None;
                _showSelectedScope = false;
                TxtSearch.Text = string.Empty;
                RebuildScopeFilter();
                SetHierarchyChecked(true);
                ApplyToElementList();
            }
            finally
            {
                _isInitializing = false;
            }

            RestoreSelection(selectionToRestore);
            UpdatePreviewCount();
            UpdateSelectionActionButtons();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selectionToRestore = GetCurrentSelectedElements().ToList();
            ApplyToElementList();
            if (_activeSelectionVisibilityAction == SelectionVisibilityAction.None)
                ApplyToViewer();
            RestoreSelection(selectionToRestore);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnIsolateSelected_Click(object sender, RoutedEventArgs e)
        {
            var elements = GetSelectionActionElements();
            if (elements.Count == 0)
                return;

            if (_activeSelectionVisibilityAction == SelectionVisibilityAction.Isolate)
            {
                RestoreCurrentFilterVisibility(elements);
                return;
            }

            if (_activeSelectionVisibilityAction != SelectionVisibilityAction.None)
                ApplyToViewer();

            _selectionSnapshotElements = ResolveSelectedScopeElements(elements);
            _selectedScopeElements = _selectionSnapshotElements.ToList();
            _visibilityFilter?.IsolateElements(_selectionSnapshotElements);
            _activeSelectionVisibilityAction = SelectionVisibilityAction.Isolate;
            RestoreSelection(_selectionSnapshotElements);
            UpdateScopeButtons();
            UpdatePreviewCount();
            UpdateSelectionActionButtons();
        }

        private void BtnHideSelected_Click(object sender, RoutedEventArgs e)
        {
            var elements = GetSelectionActionElements();
            if (elements.Count == 0)
                return;

            if (_activeSelectionVisibilityAction == SelectionVisibilityAction.Hide)
            {
                RestoreCurrentFilterVisibility(elements);
                return;
            }

            if (_activeSelectionVisibilityAction != SelectionVisibilityAction.None)
                ApplyToViewer();

            _selectionSnapshotElements = ResolveSelectedScopeElements(elements);
            _selectedScopeElements = _selectionSnapshotElements.ToList();
            _visibilityFilter?.HideElements(_selectionSnapshotElements);
            _activeSelectionVisibilityAction = SelectionVisibilityAction.Hide;
            RestoreSelection(_selectionSnapshotElements);
            UpdateScopeButtons();
            UpdatePreviewCount();
            UpdateSelectionActionButtons();
        }

        private void RestoreCurrentFilterVisibility(IReadOnlyCollection<PrecastElement> elementsToKeepSelected)
        {
            _activeSelectionVisibilityAction = SelectionVisibilityAction.None;
            ApplyToViewer();
            RestoreSelection(elementsToKeepSelected);
            UpdatePreviewCount();
            UpdateSelectionActionButtons();
        }

        private void RestoreSelection(IEnumerable<PrecastElement> elements)
        {
            var resolved = ResolveSelectedScopeElements(elements);
            _selectionSnapshotElements = resolved.ToList();
            _selectedScopeElements = resolved.ToList();

            if (resolved.Count == 0)
                return;

            if (_selectionService != null)
                _selectionService.Set(resolved);
            else
                _scene?.SetSelectedElements(resolved);

            UpdateScopeButtons();
        }

        private void ApplyToElementList()
        {
            if (_viewModel == null)
                return;

            _viewModel.SearchText = TxtSearch.Text;
            _viewModel.ApplyFilterCriteria(new FilterCriteriaDto
            {
                MinWeight = ParseDouble(TxtWeightMin.Text, 0),
                MaxWeight = ParseDouble(TxtWeightMax.Text, double.MaxValue),
                SelectedTypes = GetSelectedTypes(),
                SelectedMaterials = GetSelectedMaterials(),
                SelectedStoreys = GetSelectedStoreys(),
                IncludedElementIds = GetIncludedElementIds().ToList(),
                SortBy = GetSortBy(),
                SortDescending = RbSortDescending?.IsChecked == true
            });
        }

        private void ApplyToViewer()
        {
            if (_visibilityFilter == null)
                return;

            _visibilityFilter.ResetFilters();

            if (_showSelectedScope)
            {
                _visibilityFilter.SetExcludedElementIds(GetFilteredOutCurrentScopeElementIds());
                return;
            }

            _visibilityFilter.SetSearchText(TxtSearch.Text);
            _visibilityFilter.SetWeightRange(
                ParseDouble(TxtWeightMin.Text, _visibilityFilter.AvailableMinWeightKg),
                ParseDouble(TxtWeightMax.Text, _visibilityFilter.AvailableMaxWeightKg));

            var selectedTypes = new HashSet<string>(GetSelectedTypes(), StringComparer.OrdinalIgnoreCase);
            var selectedMaterials = new HashSet<string>(GetSelectedMaterials(), StringComparer.OrdinalIgnoreCase);
            var selectedStoreys = new HashSet<string>(GetSelectedStoreys(), StringComparer.OrdinalIgnoreCase);

            foreach (var type in _visibilityFilter.ElementTypes)
                _visibilityFilter.SetTypeEnabled(type, selectedTypes.Contains(type));

            foreach (var material in _visibilityFilter.Materials)
                _visibilityFilter.SetMaterialEnabled(material, selectedMaterials.Contains(material));

            foreach (var storey in _visibilityFilter.Storeys)
                _visibilityFilter.SetStoreyEnabled(storey, selectedStoreys.Contains(storey));

            _visibilityFilter.SetExcludedElementIds(GetUncheckedCurrentScopeElementIds());
        }

        private void InitializeWeightControls()
        {
            var (min, max) = CalculateAvailableWeightBoundsForCurrentFilter();
            if (max < min)
                max = min;

            _isSyncingControls = true;
            SliderWeightMin.Minimum = min;
            SliderWeightMin.Maximum = max;
            SliderWeightMax.Minimum = min;
            SliderWeightMax.Maximum = max;
            SliderWeightMin.Value = Clamp(_visibilityFilter?.MinWeightKg ?? min, min, max);
            SliderWeightMax.Value = Clamp(_visibilityFilter?.MaxWeightKg ?? max, min, max);
            TxtWeightMin.Text = SliderWeightMin.Value.ToString("0", CultureInfo.CurrentCulture);
            TxtWeightMax.Text = SliderWeightMax.Value.ToString("0", CultureInfo.CurrentCulture);
            _isSyncingControls = false;
            UpdateWeightRangeSliderVisual();
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

        private void FilterChanged()
        {
            UpdatePreviewCount();

            if (_isInitializing)
                return;

            if (ChkLiveApply?.IsChecked == true)
            {
                var selectionToRestore = GetCurrentSelectedElements().ToList();
                _activeSelectionVisibilityAction = SelectionVisibilityAction.None;
                UpdateSelectionActionButtons();
                ApplyToElementList();
                ApplyToViewer();
                RestoreSelection(selectionToRestore);
            }
        }

        private void NonWeightFilterChanged()
        {
            UpdateWeightControlsForCurrentFilter();
            FilterChanged();
        }

        private void UpdateWeightControlsForCurrentFilter()
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
            var (newMin, newMax) = CalculateAvailableWeightBoundsForCurrentFilter();

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

        private void BuildHierarchy()
        {
            if (HierarchyTree == null)
                return;

            var mode = (CboHierarchyBy?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Type - Element";
            Func<PrecastElement, string> groupKey = mode.StartsWith("Material", StringComparison.OrdinalIgnoreCase)
                ? e => string.IsNullOrWhiteSpace(e.ElementMaterial) ? "Unspecified" : e.ElementMaterial.Trim()
                : mode.StartsWith("Storey", StringComparison.OrdinalIgnoreCase)
                    ? e => _visibilityFilter?.GetStorey(e) ?? "Unassigned"
                    : e => string.IsNullOrWhiteSpace(e.ElementType) ? "Unknown type" : e.ElementType.Trim();
            var elementSort = GetElementSortKey();
            var sortDescending = RbSortDescending?.IsChecked == true;

            _isSyncingHierarchy = true;
            _hierarchyRoots = new ObservableCollection<FilterHierarchyNode>(
                _allElements
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
                                _visibilityFilter?.IsElementIncluded(element) ?? true,
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
                node.SetChildrenChecked(node.IsChecked);
            node.Parent?.RefreshFromChildren();
            _isSyncingHierarchy = false;
            NonWeightFilterChanged();
        }

        private void SetHierarchyElementPredicate(Func<PrecastElement, bool> predicate)
        {
            _isSyncingHierarchy = true;
            foreach (var root in _hierarchyRoots)
            {
                foreach (var child in root.Children)
                    child.IsChecked = child.Element != null && predicate(child.Element);
                root.RefreshFromChildren();
            }
            _isSyncingHierarchy = false;
        }

        private void SetHierarchyExpanded(bool isExpanded)
        {
            foreach (var node in _hierarchyRoots)
                node.SetExpandedRecursive(isExpanded);
        }

        private void SetHierarchyChecked(bool isChecked)
        {
            _isSyncingHierarchy = true;
            foreach (var node in _hierarchyRoots)
            {
                node.IsChecked = isChecked;
                node.SetChildrenChecked(isChecked);
            }
            _isSyncingHierarchy = false;
        }

        private IEnumerable<string> GetIncludedElementIds()
        {
            return _hierarchyRoots.Count == 0
                ? _allElements.Select(e => e.ElementID).Where(id => !string.IsNullOrWhiteSpace(id))
                : _hierarchyRoots
                    .SelectMany(root => root.Children)
                    .Where(node => node.IsChecked && node.Element != null && !string.IsNullOrWhiteSpace(node.Element.ElementID))
                    .Select(node => node.Element!.ElementID);
        }

        private IEnumerable<string> GetUncheckedCurrentScopeElementIds()
        {
            var includedIds = GetIncludedElementIds().ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _allElements
                .Select(e => e.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id) && !includedIds.Contains(id));
        }

        private IEnumerable<string> GetFilteredOutCurrentScopeElementIds()
        {
            var visibleScopeIds = GetElementsMatchingCurrentFilters(includeWeightRange: true)
                .Select(e => e.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _allElements
                .Select(e => e.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id) && !visibleScopeIds.Contains(id));
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
                .Select(e => string.IsNullOrWhiteSpace(e.ElementMaterial) ? "Unspecified" : e.ElementMaterial.Trim())
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

        private (double Min, double Max) CalculateAvailableWeightBoundsForCurrentFilter()
        {
            var weights = GetElementsMatchingCurrentFilters(includeWeightRange: false)
                .Select(ElementWeightCalculator.GetWeightKg)
                .ToList();

            if (weights.Count == 0)
                return (0d, 0d);

            return (Math.Floor(weights.Min()), Math.Ceiling(weights.Max()));
        }

        private void UpdatePreviewCount()
        {
            if (TxtPreviewCount == null)
                return;

            var total = _allElements.Count;
            var label = _showSelectedScope ? "selected" : "elements";
            TxtPreviewCount.Text = $"Showing {CalculatePreviewCount()} of {total} {label}";
        }

        private int CalculatePreviewCount()
        {
            return GetElementsMatchingCurrentFilters(includeWeightRange: true).Count();
        }

        private List<PrecastElement> GetElementsMatchingCurrentFilters(bool includeWeightRange)
        {
            var elements = _allElements.AsEnumerable();
            var selectedTypes = new HashSet<string>(GetSelectedTypes(), StringComparer.OrdinalIgnoreCase);
            var selectedMaterials = new HashSet<string>(GetSelectedMaterials(), StringComparer.OrdinalIgnoreCase);
            var selectedStoreys = new HashSet<string>(GetSelectedStoreys(), StringComparer.OrdinalIgnoreCase);
            var minWeight = ParseDouble(TxtWeightMin?.Text ?? string.Empty, 0);
            var maxWeight = ParseDouble(TxtWeightMax?.Text ?? string.Empty, double.MaxValue);
            var search = TxtSearch?.Text ?? string.Empty;
            var includedIds = GetIncludedElementIds().ToHashSet(StringComparer.OrdinalIgnoreCase);

            return elements.Where(element =>
            {
                var type = string.IsNullOrWhiteSpace(element.ElementType) ? "Unknown type" : element.ElementType.Trim();
                var material = string.IsNullOrWhiteSpace(element.ElementMaterial) ? "Unspecified" : element.ElementMaterial.Trim();
                var storey = _visibilityFilter?.GetStorey(element) ?? "Unassigned";
                var weight = ElementWeightCalculator.GetWeightKg(element);

                return selectedTypes.Contains(type)
                    && selectedMaterials.Contains(material)
                    && selectedStoreys.Contains(storey)
                    && (!includeWeightRange || (weight >= minWeight && weight <= maxWeight))
                    && !string.IsNullOrWhiteSpace(element.ElementID)
                    && includedIds.Contains(element.ElementID)
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
