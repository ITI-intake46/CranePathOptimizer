using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ZoneModule;
using WPF_IFC_test1.ZoneModule.Models; // ← ADDED: CraneZone and ZoneType live here

namespace WPF_IFC_test1.Main_Application_UserControl
{
    public partial class ZoneListPanel : UserControl
    {
        public event Action<CraneZone> ZoneSelected;
        public event Action<CraneZone> ZoneDeleteRequested;
        public event Action ExportRequested;

        private ZoneManager _zoneManager;

        public ZoneListPanel() => InitializeComponent();

        public void SetZoneManager(ZoneManager zm)
        {
            _zoneManager = zm;
            if (zm == null) return;

            LvZones.ItemsSource = zm.Zones;
            zm.Zones.CollectionChanged += (s, e) => UpdateStats();

            // ← FIXED: ZoneManager uses ZonesChanged (Action), not PropertyChanged (INotifyPropertyChanged)
            zm.ZonesChanged += () => UpdateStats();

            // ← FIXED: ZoneManager uses ZoneSelected event, not a SelectedZone property
            zm.ZoneSelected += (zone) =>
            {
                if (zone != null)
                    LvZones.SelectedItem = zone;
            };

            // ── Spec §5.4: refresh area labels when unit changes ──────────────
            UnitSystem.Changed += () => Dispatcher.Invoke(() =>
            {
                // Force WPF to re-evaluate converters by cycling the DataContext
                var src = LvZones.ItemsSource;
                LvZones.ItemsSource = null;
                LvZones.ItemsSource = src;
                UpdateStats();
            });

            UpdateStats();
        }

        private void UpdateStats()
        {
            if (_zoneManager == null) return;

            int total = _zoneManager.Zones.Count;

            // ← FIXED: CraneZone has no IsSiteBoundary property — check ZoneType instead
            int green = _zoneManager.Zones.Count(z => z.ZoneType == ZoneType.Allowed);
            int red = _zoneManager.Zones.Count(z => z.ZoneType == ZoneType.Forbidden);
            bool hasBound = _zoneManager.HasSiteBoundary;

            TxtZoneCount.Text = $"({total})";
            TxtStats.Text = $"Site boundary: {(hasBound ? "✓" : "None")}  |  Green: {green}  |  Red: {red}";
        }

        private void LvZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var zone = LvZones.SelectedItem as CraneZone;

            // ← FIXED: No SelectedZone property on ZoneManager — use SelectZone() method instead
            if (zone != null)
                _zoneManager?.SelectZone(zone);

            ZoneSelected?.Invoke(zone);
        }

        private void LvZones_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click opens zone edit in properties panel (handled by MainWindow)
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var zone = LvZones.SelectedItem as CraneZone;
            if (zone == null) return;

            var r = MessageBox.Show($"Delete zone '{zone.DisplayLabel}'?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (r == MessageBoxResult.Yes)
            {
                // ← FIXED: Method is called RemoveZone, not DeleteZone
                _zoneManager?.RemoveZone(zone);
                ZoneDeleteRequested?.Invoke(zone);
                UpdateStats();
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e) => ExportRequested?.Invoke();
    }
}