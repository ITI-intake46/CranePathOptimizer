using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.ViewModels;

namespace WPF_IFC_test1.Dialogs
{
    // ── Small VM for type/material checkbox rows ──────────────────────────────
    public class CheckableItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string TypeName { get; set; }
        public string DisplayName { get; set; }
        public int Count { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public partial class ElementFilterDialog : Window
    {
        // ── State ─────────────────────────────────────────────────────────────
        private readonly MainViewModel _vm;
        private List<CheckableItem> _typeItems = new();
        private List<CheckableItem> _materialItems = new();
        private bool _suppressUpdate = true;  // stays true until RestoreFromFilter sets it false

        /// <summary>The filter that was applied when the user clicked Apply.</summary>
        public ElementFilter ResultFilter { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        public ElementFilterDialog(MainViewModel vm, ElementFilter currentFilter)
        {
            _vm = vm;           // Must be set BEFORE InitializeComponent so that
                                // the SelectionChanged event fired by IsSelected="True"
                                // on CmbHierarchyMode doesn't crash with a null _vm.
            InitializeComponent();

            PopulateSubtitle();
            PopulateTypes(currentFilter);
            PopulateMaterials(currentFilter);
            PopulateWeightRange(currentFilter);
            PopulateHeightRange(currentFilter);
            RestoreFromFilter(currentFilter);
            UpdateResultCount();
            BuildHierarchyTree("Type");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Populate helpers
        // ─────────────────────────────────────────────────────────────────────

        private void PopulateSubtitle()
        {
            int total = _vm.TotalCount;
            TxtSubtitle.Text = total > 0
                ? $"{total} elements loaded — use filters below to narrow the list"
                : "No elements loaded";
        }

        private void PopulateTypes(ElementFilter f)
        {
            _typeItems = _vm.GetAvailableTypes()
                .Select(t => new CheckableItem
                {
                    TypeName = t,
                    DisplayName = FormatTypeName(t),
                    Count = _vm.Elements.Count(e => e.Type == t),
                    IsChecked = f.AllowedTypes == null || f.AllowedTypes.Contains(t)
                }).ToList();

            TypesList.ItemsSource = _typeItems;
        }

        private void PopulateMaterials(ElementFilter f)
        {
            _materialItems = _vm.GetAvailableMaterials()
                .Select(m => new CheckableItem
                {
                    TypeName = m,
                    DisplayName = $"{m}",
                    Count = _vm.Elements.Count(e => e.Material == m),
                    IsChecked = f.AllowedMaterials == null || f.AllowedMaterials.Contains(m)
                }).ToList();

            if (_materialItems.Count == 0)
            {
                TxtNoMaterials.Visibility = Visibility.Visible;
                MaterialsList.Visibility = Visibility.Collapsed;
            }
            else
            {
                MaterialsList.ItemsSource = _materialItems;
            }
        }

        private void PopulateWeightRange(ElementFilter f)
        {
            double maxW = Math.Ceiling(_vm.GetMaxWeightKg());
            TxtWeightMin.Text = f.MinWeightKg.HasValue ? f.MinWeightKg.Value.ToString("F0") : "0";
            TxtWeightMax.Text = f.MaxWeightKg.HasValue ? f.MaxWeightKg.Value.ToString("F0")
                                                        : maxW > 0 ? maxW.ToString("F0") : "";
            TxtWeightRange.Text = maxW > 0 ? $"Model range: 0 – {maxW:F0} kg" : "No weight data";
        }

        private void PopulateHeightRange(ElementFilter f)
        {
            double maxH = Math.Ceiling(_vm.GetMaxHeightM() * 10) / 10;
            TxtHeightMin.Text = f.MinHeightM.HasValue ? f.MinHeightM.Value.ToString("F2") : "0";
            TxtHeightMax.Text = f.MaxHeightM.HasValue ? f.MaxHeightM.Value.ToString("F2")
                                                       : maxH > 0 ? maxH.ToString("F2") : "";
        }

        private void RestoreFromFilter(ElementFilter f)
        {
            _suppressUpdate = true;

            // Search
            TxtSearch.Text = f.SearchText ?? "";

            // Sort
            var sortMap = new Dictionary<ElementSortField, int>
            {
                { ElementSortField.Name,       0 }, { ElementSortField.Type,      1 },
                { ElementSortField.WeightAsc,  2 }, { ElementSortField.WeightDesc, 3 },
                { ElementSortField.HeightAsc,  4 }, { ElementSortField.HeightDesc, 5 },
                { ElementSortField.Mark,       6 }
            };
            CmbSortField.SelectedIndex = sortMap.TryGetValue(f.SortField, out int idx) ? idx : 0;
            ChkSortDesc.IsChecked = f.SortDescending;

            // Group
            switch (f.GroupBy)
            {
                case ElementGroupBy.Type: RbGroupType.IsChecked = true; break;
                case ElementGroupBy.Material: RbGroupMaterial.IsChecked = true; break;
                case ElementGroupBy.WeightCategory: RbGroupWeight.IsChecked = true; break;
                default: RbGroupNone.IsChecked = true; break;
            }

            // Only selected
            ChkOnlySelected.IsChecked = f.OnlySelected;

            _suppressUpdate = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build current filter from UI state
        // ─────────────────────────────────────────────────────────────────────

        private ElementFilter BuildFilter()
        {
            var f = new ElementFilter();

            // Search
            f.SearchText = TxtSearch.Text?.Trim() ?? "";

            // Types
            var uncheckedTypes = _typeItems.Where(t => !t.IsChecked).Select(t => t.TypeName).ToHashSet();
            if (uncheckedTypes.Count > 0)
                f.AllowedTypes = _typeItems.Where(t => t.IsChecked).Select(t => t.TypeName).ToHashSet();

            // Materials
            var uncheckedMats = _materialItems.Where(m => !m.IsChecked).Select(m => m.TypeName).ToHashSet();
            if (uncheckedMats.Count > 0)
                f.AllowedMaterials = _materialItems.Where(m => m.IsChecked).Select(m => m.TypeName).ToHashSet();

            // Weight
            if (double.TryParse(TxtWeightMin.Text, out double wMin) && wMin > 0) f.MinWeightKg = wMin;
            if (double.TryParse(TxtWeightMax.Text, out double wMax) && wMax > 0) f.MaxWeightKg = wMax;

            // Height
            if (double.TryParse(TxtHeightMin.Text, out double hMin) && hMin > 0) f.MinHeightM = hMin;
            if (double.TryParse(TxtHeightMax.Text, out double hMax) && hMax > 0) f.MaxHeightM = hMax;

            // Only selected
            f.OnlySelected = ChkOnlySelected.IsChecked == true;

            // Sort
            var sortTag = (CmbSortField.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Name";
            f.SortField = sortTag switch
            {
                "Type" => ElementSortField.Type,
                "WeightAsc" => ElementSortField.WeightAsc,
                "WeightDesc" => ElementSortField.WeightDesc,
                "HeightAsc" => ElementSortField.HeightAsc,
                "HeightDesc" => ElementSortField.HeightDesc,
                "Mark" => ElementSortField.Mark,
                _ => ElementSortField.Name
            };
            f.SortDescending = ChkSortDesc.IsChecked == true;

            // Group
            if (RbGroupType.IsChecked == true) f.GroupBy = ElementGroupBy.Type;
            else if (RbGroupMaterial.IsChecked == true) f.GroupBy = ElementGroupBy.Material;
            else if (RbGroupWeight.IsChecked == true) f.GroupBy = ElementGroupBy.WeightCategory;
            else f.GroupBy = ElementGroupBy.None;

            return f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Live result count preview
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateResultCount()
        {
            if (_vm == null) return;
            var f = BuildFilter();

            // Count matching without actually applying to VM
            var q = _vm.Elements.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(f.SearchText))
            {
                var qs = f.SearchText.ToLowerInvariant();
                q = q.Where(e =>
                    (e.DisplayName?.ToLowerInvariant().Contains(qs) == true) ||
                    (e.Type?.ToLowerInvariant().Contains(qs) == true) ||
                    (e.Mark?.ToLowerInvariant().Contains(qs) == true) ||
                    (e.Material?.ToLowerInvariant().Contains(qs) == true));
            }
            if (f.AllowedTypes != null) q = q.Where(e => e.Type != null && f.AllowedTypes.Contains(e.Type));
            if (f.AllowedMaterials != null) q = q.Where(e => e.Material != null && f.AllowedMaterials.Contains(e.Material));
            if (f.MinWeightKg.HasValue) q = q.Where(e => e.Weight >= f.MinWeightKg);
            if (f.MaxWeightKg.HasValue) q = q.Where(e => e.Weight <= f.MaxWeightKg);
            if (f.MinHeightM.HasValue) q = q.Where(e => e.Height >= f.MinHeightM);
            if (f.MaxHeightM.HasValue) q = q.Where(e => e.Height <= f.MaxHeightM);
            if (f.OnlySelected) q = q.Where(e => e.IsSelected);

            int count = q.Count();
            int total = _vm.TotalCount;

            TxtResultCount.Text = $"{count} elements";
            TxtResultOf.Text = count == total ? " (all)" : $" of {total}";

            UpdateTagStrip(f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Active filter tag chips
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateTagStrip(ElementFilter f)
        {
            TagsPanel.Children.Clear();

            if (!string.IsNullOrWhiteSpace(f.SearchText))
                AddTag($"🔍 \"{f.SearchText}\"", "#007ACC");

            if (f.AllowedTypes != null)
                AddTag($"Type: {f.AllowedTypes.Count} selected", "#9B59B6");

            if (f.AllowedMaterials != null)
                AddTag($"Material: {f.AllowedMaterials.Count} selected", "#1ABC9C");

            if (f.MinWeightKg.HasValue || f.MaxWeightKg.HasValue)
            {
                string wLabel = $"Weight: {f.MinWeightKg?.ToString("F0") ?? "0"} – {f.MaxWeightKg?.ToString("F0") ?? "∞"} kg";
                AddTag(wLabel, "#E67E22");
            }

            if (f.MinHeightM.HasValue || f.MaxHeightM.HasValue)
            {
                string hLabel = $"Height: {f.MinHeightM?.ToString("F2") ?? "0"} – {f.MaxHeightM?.ToString("F2") ?? "∞"} m";
                AddTag(hLabel, "#27AE60");
            }

            if (f.OnlySelected)
                AddTag("Selected only", "#E74C3C");

            if (f.SortField != ElementSortField.Name || f.SortDescending)
            {
                string sortLabel = $"Sort: {(CmbSortField.SelectedItem as ComboBoxItem)?.Content}";
                AddTag(sortLabel, "#555");
            }

            if (f.GroupBy != ElementGroupBy.None)
                AddTag($"Group: {f.GroupBy}", "#555");
        }

        private void AddTag(string text, string colorHex)
        {
            var tag = new Border { Style = (Style)FindResource("FilterTag") };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            tag.Child = sp;
            TagsPanel.Children.Add(tag);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hierarchy tree builder
        // ─────────────────────────────────────────────────────────────────────

        private void BuildHierarchyTree(string mode)
        {
            // Guard: HierarchyTree may not exist yet if called during InitializeComponent
            // (SelectionChanged fires on CmbHierarchyMode before the TreeView is instantiated)
            if (HierarchyTree == null) return;
            HierarchyTree.Items.Clear();
            if (_vm == null || _vm.Elements.Count == 0) return;

            IEnumerable<IGrouping<string, WPF_IFC_test1.Models.IfcElementModel>> groups;

            switch (mode)
            {
                case "Material":
                    groups = _vm.Elements
                        .GroupBy(e => string.IsNullOrEmpty(e.Material) ? "(No Material)" : e.Material)
                        .OrderBy(g => g.Key);
                    break;
                case "Weight":
                    groups = _vm.Elements
                        .GroupBy(e => e.Weight switch
                        {
                            null => "Unknown",
                            double w when w < 500 => "🟢  Light  (< 500 kg)",
                            double w when w < 5000 => "🟡  Medium  (500 – 5 000 kg)",
                            _ => "🔴  Heavy  (> 5 000 kg)"
                        })
                        .OrderBy(g => g.Key);
                    break;
                default: // Type
                    groups = _vm.Elements
                        .GroupBy(e => string.IsNullOrEmpty(e.Type) ? "(Unknown Type)" : FormatTypeName(e.Type))
                        .OrderBy(g => g.Key);
                    break;
            }

            foreach (var grp in groups)
            {
                var parentItem = new TreeViewItem
                {
                    Header = BuildGroupHeader(grp.Key, grp.Count()),
                    IsExpanded = true,
                    Tag = grp.Key
                };

                foreach (var el in grp.OrderBy(e => e.DisplayName))
                {
                    var childItem = new TreeViewItem
                    {
                        Header = BuildElementHeader(el),
                        Tag = el
                    };
                    parentItem.Items.Add(childItem);
                }

                HierarchyTree.Items.Add(parentItem);
            }
        }

        private FrameworkElement BuildGroupHeader(string key, int count)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = key,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = count.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            return sp;
        }

        private FrameworkElement BuildElementHeader(WPF_IFC_test1.Models.IfcElementModel el)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };

            // Colour dot
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            try
            {
                dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(el.ElementColor ?? "#AAAAAA"));
            }
            catch { dot.Fill = Brushes.Gray; }
            sp.Children.Add(dot);

            // Name
            sp.Children.Add(new TextBlock
            {
                Text = el.DisplayName,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Weight pill
            if (el.Weight.HasValue)
            {
                sp.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = $"{el.Weight.Value:F0} kg",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                        FontSize = 10
                    }
                });
            }

            return sp;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helper
        // ─────────────────────────────────────────────────────────────────────

        private static string FormatTypeName(string t)
            => t?.Replace("Ifc", "").Replace("IfcBuilding", "") ?? t;

        // ─────────────────────────────────────────────────────────────────────
        //  UI event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void TypeFilter_Changed(object s, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void MaterialFilter_Changed(object s, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void WeightRange_Changed(object s, TextChangedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void HeightRange_Changed(object s, TextChangedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void OnlySelected_Changed(object s, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void Sort_Changed(object s, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        private void Group_Changed(object s, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdateResultCount();
        }

        // Type select/clear all
        private void BtnSelectAllTypes_Click(object s, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            foreach (var t in _typeItems) t.IsChecked = true;
            _suppressUpdate = false;
            UpdateResultCount();
        }

        private void BtnClearAllTypes_Click(object s, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            foreach (var t in _typeItems) t.IsChecked = false;
            _suppressUpdate = false;
            UpdateResultCount();
        }

        // Material select/clear all
        private void BtnSelectAllMaterials_Click(object s, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            foreach (var m in _materialItems) m.IsChecked = true;
            _suppressUpdate = false;
            UpdateResultCount();
        }

        private void BtnClearAllMaterials_Click(object s, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            foreach (var m in _materialItems) m.IsChecked = false;
            _suppressUpdate = false;
            UpdateResultCount();
        }

        // Search clear
        private void BtnClearSearch_Click(object s, RoutedEventArgs e)
        {
            TxtSearch.Clear();
        }

        // Reset all
        private void BtnResetAll_Click(object s, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            TxtSearch.Clear();
            foreach (var t in _typeItems) t.IsChecked = true;
            foreach (var m in _materialItems) m.IsChecked = true;
            TxtWeightMin.Text = "0";
            TxtWeightMax.Text = _vm.GetMaxWeightKg() > 0 ? Math.Ceiling(_vm.GetMaxWeightKg()).ToString("F0") : "";
            TxtHeightMin.Text = "0";
            TxtHeightMax.Text = _vm.GetMaxHeightM() > 0 ? (Math.Ceiling(_vm.GetMaxHeightM() * 10) / 10).ToString("F2") : "";
            ChkOnlySelected.IsChecked = false;
            CmbSortField.SelectedIndex = 0;
            ChkSortDesc.IsChecked = false;
            RbGroupNone.IsChecked = true;
            _suppressUpdate = false;
            UpdateResultCount();
        }

        // Hierarchy
        private void HierarchyMode_Changed(object s, SelectionChangedEventArgs e)
        {
            var mode = (CmbHierarchyMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Type";
            BuildHierarchyTree(mode);
        }

        private void BtnExpandAll_Click(object s, RoutedEventArgs e)
        {
            foreach (TreeViewItem item in HierarchyTree.Items)
                item.IsExpanded = true;
        }

        private void BtnCollapseAll_Click(object s, RoutedEventArgs e)
        {
            foreach (TreeViewItem item in HierarchyTree.Items)
                item.IsExpanded = false;
        }

        private void HierarchyTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // When a leaf element is selected in the tree, highlight it in the search field
            if ((HierarchyTree.SelectedItem as TreeViewItem)?.Tag is WPF_IFC_test1.Models.IfcElementModel el)
            {
                TxtSearch.Text = el.DisplayName;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dialog result buttons
        // ─────────────────────────────────────────────────────────────────────

        private void BtnApply_Click(object s, RoutedEventArgs e)
        {
            ResultFilter = BuildFilter();
            DialogResult = true;
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}