using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementFilterDialog : Window
    {
        private ElementManagementViewModel? _viewModel;

        public ElementFilterDialog()
        {
            InitializeComponent();
            TxtSubtitle.Text = "Apply filters to narrow the element list in the main view.";
            UpdatePreviewCount();
        }

        public ElementFilterDialog(ElementManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            TxtSubtitle.Text = "Apply filters to narrow the element list in the main view.";
            TxtSearch.Text = viewModel.SearchText;
            TxtWeightMin.Text = viewModel.MinWeight.ToString("0.##", CultureInfo.CurrentCulture);
            TxtWeightMax.Text = viewModel.MaxWeight.ToString("0.##", CultureInfo.CurrentCulture);
            SliderWeightMax.Value = viewModel.MaxWeight;
            ChkOnlySelected.IsChecked = viewModel.ShowOnlySelected;
            LoadTypeOptions(viewModel);
            UpdatePreviewCount();
        }

        // ── Search ────────────────────────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.SearchText = TxtSearch.Text;

            UpdatePreviewCount();
        }

        // ── Type filter ───────────────────────────────────────────────────
        private void BtnSelectAllTypes_Click(object sender, RoutedEventArgs e) => SetAllChecked(TypesList, true);
        private void BtnClearAllTypes_Click(object sender, RoutedEventArgs e)  => SetAllChecked(TypesList, false);

        // ── Weight ────────────────────────────────────────────────────────
        private void WeightRange_Changed(object sender, TextChangedEventArgs e) => UpdatePreviewCount();

        private void SliderWeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtWeightMax != null)
                TxtWeightMax.Text = ((int)e.NewValue).ToString();
            UpdatePreviewCount();
        }

        // ── Height ────────────────────────────────────────────────────────
        private void HeightRange_Changed(object sender, TextChangedEventArgs e) => UpdatePreviewCount();

        private void SliderHeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtHeightMax != null)
                TxtHeightMax.Text = ((int)e.NewValue).ToString();
            UpdatePreviewCount();
        }

        // ── Material ──────────────────────────────────────────────────────
        private void BtnSelectAllMaterials_Click(object sender, RoutedEventArgs e) => SetAllChecked(MaterialsList, true);
        private void BtnClearAllMaterials_Click(object sender, RoutedEventArgs e)  => SetAllChecked(MaterialsList, false);

        // ── Options ───────────────────────────────────────────────────────
        private void Options_Changed(object sender, RoutedEventArgs e)     => UpdatePreviewCount();
        private void OnlySelected_Changed(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.ShowOnlySelected = ChkOnlySelected.IsChecked == true;

            UpdatePreviewCount();
        }

        // ── Footer ────────────────────────────────────────────────────────
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text        = string.Empty;
            TxtWeightMin.Text     = "0";
            TxtWeightMax.Text     = "50000";
            TxtHeightMin.Text     = string.Empty;
            TxtHeightMax.Text     = string.Empty;
            SliderWeightMax.Value = SliderWeightMax.Maximum;
            SliderHeightMax.Value = SliderHeightMax.Maximum;
            SetAllChecked(TypesList,     true);
            SetAllChecked(MaterialsList, true);
            TagsPanel.Children.Clear();
            TxtActiveFilters.Text = string.Empty;
            UpdatePreviewCount();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                var criteria = new FilterCriteria
                {
                    MinWeight = ParseDouble(TxtWeightMin.Text, 0),
                    MaxWeight = ParseDouble(TxtWeightMax.Text, 50000),
                    SelectedTypes = GetSelectedTypes()
                };

                _viewModel.ApplyFilterCriteria(criteria);
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static void SetAllChecked(ItemsControl list, bool isChecked)
        {
            foreach (var item in list.Items)
                if (item is ICheckable c) c.IsChecked = isChecked;
        }

        private void LoadTypeOptions(ElementManagementViewModel viewModel)
        {
            TypesList.ItemsSource = viewModel.Elements
                .Where(e => !string.IsNullOrWhiteSpace(e.ElementType))
                .GroupBy(e => e.ElementType)
                .OrderBy(g => g.Key)
                .Select(g => new TypeFilterOption
                {
                    DisplayName = g.Key,
                    Count = g.Count(),
                    IsChecked = true
                })
                .ToList();
        }

        private List<string> GetSelectedTypes()
        {
            return TypesList.Items
                .OfType<TypeFilterOption>()
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

        private void UpdatePreviewCount()
        {
            if (TxtPreviewCount != null)
                TxtPreviewCount.Text = _viewModel != null
                    ? $"Showing {_viewModel.FilterStatus.Replace("Showing ", string.Empty)}"
                    : "Showing all elements";
        }
    }

    public interface ICheckable { bool IsChecked { get; set; } }

    public class TypeFilterOption : ICheckable
    {
        public string DisplayName { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsChecked { get; set; }
    }
}
