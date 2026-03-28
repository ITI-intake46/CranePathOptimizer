using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementFilterDialog : Window
    {
        public ElementFilterDialog()
        {
            InitializeComponent();
            TxtSubtitle.Text = "Apply filters to narrow the element list in the main view.";
            UpdatePreviewCount();
        }

        // ── Search ────────────────────────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreviewCount();

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
        private void OnlySelected_Changed(object sender, RoutedEventArgs e) => UpdatePreviewCount();

        // ── Footer ────────────────────────────────────────────────────────
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text        = string.Empty;
            TxtWeightMin.Text     = string.Empty;
            TxtWeightMax.Text     = string.Empty;
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

        private void UpdatePreviewCount()
        {
            if (TxtPreviewCount != null)
                TxtPreviewCount.Text = "Showing all elements";
        }
    }

    public interface ICheckable { bool IsChecked { get; set; } }
}
