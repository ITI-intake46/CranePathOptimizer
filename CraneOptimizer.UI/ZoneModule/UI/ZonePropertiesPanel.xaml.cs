// FILE: ZoneModule/UI/ZonePropertiesPanel.xaml.cs  (NEW FILE — does not exist yet)
// This is the Select Mode properties panel from spec §7.3:
//   "Clicking a zone in Select Mode opens a properties panel with editable
//    Min Height, Max Height, zone type toggle, name field, and notes.
//    All changes update the 3D visualization immediately in real time."

using System;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.ZoneModule.Models;
using WPF_IFC_test1.ZoneModule.Rendering;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class ZonePropertiesPanel : UserControl
    {
        private CraneZone _zone;
        private ZoneRenderer _renderer;
        private ZoneManager _manager;

        // Raised when changes are committed so the parent can refresh
        public event Action<CraneZone> ZoneUpdated;

        public ZonePropertiesPanel()
        {
            InitializeComponent();
            SetVisible(false);
        }

        public void Initialize(ZoneManager manager, ZoneRenderer renderer)
        {
            _manager = manager;
            _renderer = renderer;

            // Auto-show when a zone is selected
            _manager.ZoneSelected += zone =>
            {
                Dispatcher.Invoke(() => ShowZone(zone));
            };
        }

        // ── Show / hide ───────────────────────────────────────────────────────

        public void ShowZone(CraneZone zone)
        {
            _zone = zone;
            if (zone == null) { SetVisible(false); return; }

            // Populate fields
            TxtName.Text = zone.Name ?? "";
            TxtMinHeight.Text = zone.MinHeight.ToString("F1");
            TxtMaxHeight.Text = zone.MaxHeight.ToString("F1");
            TxtNotes.Text = zone.Notes ?? "";
            ChkVisible.IsChecked = zone.IsVisible;

            RadioAllowed.IsChecked = zone.ZoneType == ZoneType.Allowed;
            RadioForbidden.IsChecked = zone.ZoneType == ZoneType.Forbidden;

            // Site boundary: show info, no editing of type or heights
            bool isBoundary = zone.IsSiteBoundary;
            RadioAllowed.IsEnabled = !isBoundary;
            RadioForbidden.IsEnabled = !isBoundary;
            TxtMinHeight.IsEnabled = !isBoundary;
            TxtMaxHeight.IsEnabled = !isBoundary;

            LblZoneId.Content = $"ID: {zone.Id[..8]}…";
            LblArea.Content = $"Area: {zone.FootprintArea:F1} m²";
            LblCreated.Content = $"Created: {zone.CreatedAt:yyyy-MM-dd HH:mm}";

            SetVisible(true);
        }

        public void Clear()
        {
            _zone = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Live update handlers ──────────────────────────────────────────────

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_zone == null) return;
            _zone.Name = TxtName.Text.Trim();
            // DisplayLabel auto-updates via INotifyPropertyChanged on CraneZone
        }

        private void ZoneType_Changed(object sender, RoutedEventArgs e)
        {
            if (_zone == null || _zone.IsSiteBoundary) return;
            var newType = RadioAllowed.IsChecked == true
                ? ZoneType.Allowed
                : ZoneType.Forbidden;

            if (_zone.ZoneType == newType) return;
            _zone.ZoneType = newType;
            _renderer?.RefreshZone(_zone); // Re-render with new color
        }

        private void TxtMinHeight_LostFocus(object sender, RoutedEventArgs e)
            => TryApplyHeight();

        private void TxtMaxHeight_LostFocus(object sender, RoutedEventArgs e)
            => TryApplyHeight();

        private void TryApplyHeight()
        {
            if (_zone == null) return;
            if (!double.TryParse(TxtMinHeight.Text, out double minH) ||
                !double.TryParse(TxtMaxHeight.Text, out double maxH))
            {
                HeightError.Text = "⚠ Enter valid numbers";
                return;
            }
            if (minH < 0) { HeightError.Text = "⚠ Min height cannot be negative"; return; }
            if (maxH <= minH) { HeightError.Text = "⚠ Max height must exceed min"; return; }

            HeightError.Text = "";
            _zone.MinHeight = minH;
            _zone.MaxHeight = maxH;
            _renderer?.RefreshZone(_zone); // Re-render prism with new height
        }

        private void TxtNotes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_zone == null) return;
            _zone.Notes = TxtNotes.Text;
        }

        private void ChkVisible_Changed(object sender, RoutedEventArgs e)
        {
            if (_zone == null) return;
            _zone.IsVisible = ChkVisible.IsChecked == true;
            _renderer?.UpdateZoneVisibility(_zone);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            TryApplyHeight();
            if (HeightError.Text == "")
            {
                _manager?.NotifyZonesChanged();
                ZoneUpdated?.Invoke(_zone);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_zone == null) return;
            if (MessageBox.Show($"Delete '{_zone.DisplayLabel}'?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _renderer?.RemoveZone(_zone);
            _manager?.RemoveZone(_zone);
            Clear();
        }
    }
}