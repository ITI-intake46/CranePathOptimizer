using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.DTOs;
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
        private readonly ElementManagementViewModel? _viewModel;
        private readonly IElementVisibilityFilterService? _visibilityFilter;
        private readonly ISceneService? _scene;
        private bool _isSyncingControls;
        private List<FilterDialogOption> _allTypeOptions = new();
        private List<FilterDialogOption> _allMaterialOptions = new();
        private List<FilterDialogOption> _allStoreyOptions = new();
        private List<PrecastElement> _allElements = new();
        private ObservableCollection<FilterHierarchyNode> _hierarchyRoots = new();
        private bool _isSyncingHierarchy;
        private static readonly string[] StructuralTypeKeywords =
        [
            "IfcColumn", "IfcBeam", "IfcSlab", "IfcWall", "IfcStair",
            "IfcRoof", "IfcMember", "IfcPlate", "IfcFooting", "IfcPile", "IfcRamp"
        ];

        public ElementFilterDialog()
        {
            InitializeComponent();
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            TxtSubtitle.Text = "Choose IFC types, materials, levels, and weight range to hide or show elements in the 3D view.";
            _allElements = (_visibilityFilter?.AllElements ?? Array.Empty<PrecastElement>()).ToList();
            LoadTypeOptions(_allElements);
            LoadMaterialOptions(_allElements);
            LoadStoreyOptions(_allElements);
            BuildHierarchy();
            InitializeWeightControls();
            UpdateRangeChips();
            FilterChanged();
        }

        public ElementFilterDialog(ElementManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _visibilityFilter = App.ServiceProvider.GetService<IElementVisibilityFilterService>();
            _scene = App.ServiceProvider.GetService<ISceneService>();
            DataContext = viewModel;

            TxtSubtitle.Text = "Choose IFC types, materials, levels, and weight range to hide or show elements in the 3D view.";
            TxtSearch.Text = viewModel.SearchText;
            _allElements = viewModel.Elements.ToList();

            LoadTypeOptions(viewModel.Elements);
            LoadMaterialOptions(viewModel.Elements);
            LoadStoreyOptions(viewModel.Elements);
            BuildHierarchy();
            InitializeWeightControls();
            UpdateRangeChips();
            FilterChanged();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => NonWeightFilterChanged();
        private void FilterOption_Changed(object sender, RoutedEventArgs e) => NonWeightFilterChanged();
        private void HierarchyOption_Changed(object sender, RoutedEventArgs e) => OnHierarchyOptionChanged(sender);
        private void AnyFilter_Changed(object sender, RoutedEventArgs e) => FilterChanged();
        private void OptionSearch_Changed(object sender, TextChangedEventArgs e) => RefreshOptionViews();
        private void HierarchyMode_Changed(object sender, SelectionChangedEventArgs e) => BuildHierarchy();
        private void Sort_Changed(object sender, RoutedEventArgs e) => BuildHierarchy();
        private void Sort_Changed(object sender, SelectionChangedEventArgs e) => BuildHierarchy();
        private void BtnExpandHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(true);
        private void BtnCollapseHierarchy_Click(object sender, RoutedEventArgs e) => SetHierarchyExpanded(false);

        private void BtnSelectAllTypes_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allTypeOptions, true); FilterChanged(); }
        private void BtnClearAllTypes_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allTypeOptions, false); FilterChanged(); }
        private void BtnSelectAllMaterials_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allMaterialOptions, true); FilterChanged(); }
        private void BtnClearAllMaterials_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allMaterialOptions, false); FilterChanged(); }
        private void BtnSelectAllStoreys_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allStoreyOptions, true); FilterChanged(); }
        private void BtnClearAllStoreys_Click(object sender, RoutedEventArgs e) { SetAllChecked(_allStoreyOptions, false); FilterChanged(); }
        private void BtnInvertTypes_Click(object sender, RoutedEventArgs e) { InvertChecked(_allTypeOptions); FilterChanged(); }

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
            TxtSearch.Text = string.Empty;
            SetAllChecked(_allTypeOptions, true);
            SetAllChecked(_allMaterialOptions, true);
            SetAllChecked(_allStoreyOptions, true);
            SetHierarchyChecked(true);
            InitializeWeightControls();
            UpdateRangeChips();
            FilterChanged();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ApplyToElementList();
            ApplyToViewer();
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
            _visibilityFilter?.IsolateElements(_scene?.GetSelectedElements() ?? new List<PrecastElement>());
            UpdatePreviewCount();
        }

        private void BtnHideSelected_Click(object sender, RoutedEventArgs e)
        {
            _visibilityFilter?.HideElements(_scene?.GetSelectedElements() ?? new List<PrecastElement>());
            UpdatePreviewCount();
        }

        private void BtnShowAll_Click(object sender, RoutedEventArgs e)
        {
            _visibilityFilter?.ShowAll();
            BtnReset_Click(sender, e);
        }

        private void BtnPresetStructural_Click(object sender, RoutedEventArgs e)
        {
            foreach (var option in _allTypeOptions)
                option.IsChecked = StructuralTypeKeywords.Any(keyword => option.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            SetAllChecked(_allMaterialOptions, true);
            SetAllChecked(_allStoreyOptions, true);
            InitializeWeightControls();
            BuildHierarchy();
            SetHierarchyElementPredicate(element =>
                StructuralTypeKeywords.Any(keyword => (element.ElementType ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            NonWeightFilterChanged();
        }

        private void BtnPresetHeavy_Click(object sender, RoutedEventArgs e)
        {
            SetAllChecked(_allTypeOptions, true);
            SetAllChecked(_allMaterialOptions, true);
            SetAllChecked(_allStoreyOptions, true);

            var availableMax = _visibilityFilter?.AvailableMaxWeightKg ?? 0d;
            var heavyThreshold = Math.Max(1000d, availableMax * 0.25d);
            TxtWeightMin.Text = heavyThreshold.ToString("0", CultureInfo.CurrentCulture);
            TxtWeightMax.Text = availableMax.ToString("0", CultureInfo.CurrentCulture);
            SetHierarchyChecked(true);
            FilterChanged();
        }

        private void BtnPresetCurrentSelection_Click(object sender, RoutedEventArgs e)
        {
            var selected = _scene?.GetSelectedElements() ?? new List<PrecastElement>();

            // If nothing selected in 3D, fall back to currently checked hierarchy items
            if (selected.Count == 0)
            {
                var checkedIds = _hierarchyRoots
                    .SelectMany(root => root.Children)
                    .Where(node => node.IsChecked && node.Element != null)
                    .Select(node => node.Element!.ElementID)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (checkedIds.Count == 0)
                    return;

                selected = _allElements
                    .Where(el => !string.IsNullOrWhiteSpace(el.ElementID) && checkedIds.Contains(el.ElementID))
                    .ToList();
            }

            var selectedTypes = selected
                .Select(e => e.ElementType)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedMaterials = selected
                .Select(e => string.IsNullOrWhiteSpace(e.ElementMaterial) ? "Unspecified" : e.ElementMaterial.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedStoreys = selected
                .Select(e => _visibilityFilter?.GetStorey(e) ?? "Unassigned")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var option in _allTypeOptions) option.IsChecked = selectedTypes.Contains(option.DisplayName);
            foreach (var option in _allMaterialOptions) option.IsChecked = selectedMaterials.Contains(option.DisplayName);
            foreach (var option in _allStoreyOptions) option.IsChecked = selectedStoreys.Contains(option.DisplayName);
            var selectedIds = selected
                .Select(e => e.ElementID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            BuildHierarchy();
            SetHierarchyElementPredicate(element => !string.IsNullOrWhiteSpace(element.ElementID) && selectedIds.Contains(element.ElementID));
            NonWeightFilterChanged();
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

            _visibilityFilter.SetIncludedElementIds(GetIncludedElementIds());
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

            if (ChkLiveApply?.IsChecked == true)
            {
                ApplyToElementList();
                ApplyToViewer();
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
            if (_hierarchyRoots.Count == 0)
                return _allElements.Select(e => e.ElementID).Where(id => !string.IsNullOrWhiteSpace(id));

            return _hierarchyRoots
                .SelectMany(root => root.Children)
                .Where(node => node.IsChecked && node.Element != null && !string.IsNullOrWhiteSpace(node.Element.ElementID))
                .Select(node => node.Element!.ElementID);
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

            var total = _viewModel?.Elements.Count ?? _visibilityFilter?.TotalCount ?? 0;
            TxtPreviewCount.Text = $"Showing {CalculatePreviewCount()} of {total} elements";
        }

        private int CalculatePreviewCount()
        {
            return GetElementsMatchingCurrentFilters(includeWeightRange: true).Count();
        }

        private List<PrecastElement> GetElementsMatchingCurrentFilters(bool includeWeightRange)
        {
            var elements = _viewModel?.Elements.AsEnumerable() ?? _visibilityFilter?.AllElements ?? Enumerable.Empty<PrecastElement>();
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
