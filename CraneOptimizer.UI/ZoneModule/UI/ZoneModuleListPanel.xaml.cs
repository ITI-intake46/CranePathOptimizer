using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WPF_IFC_test1.ZoneModule;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class ZoneModuleListPanel : UserControl
    {
        public event Action<CraneZone> ZoneSelected;
        public event Action<CraneZone> ZoneDeleteRequested;
        public event Action DeleteAllRequested;
        public event Action ExportRequested;

        private ZoneManager _zoneManager;
        private string _activeFilter = "All";

        // ?? Drag-reorder state ?????????????????????????????????????????????????
        private Point _dragStartPoint;
        private CraneZone _draggedZone;
        private bool _isDragging = false;

        public ZoneModuleListPanel()
        {
            InitializeComponent();

            // Re-run UpdateStats whenever the user changes units so area label refreshes
            UnitSystem.Changed += () => Dispatcher.Invoke(UpdateStats);
        }

        // ?? Initialization ????????????????????????????????????????????????????

        public void Initialize(ZoneManager zm) => SetZoneManager(zm);

        public void SetZoneManager(ZoneManager zm)
        {
            _zoneManager = zm;
            if (zm == null) return;

            zm.Zones.CollectionChanged += (s, e) => Dispatcher.Invoke(() => { ApplyFilter(); UpdateStats(); });
            zm.ZonesChanged += () => Dispatcher.Invoke(() => { ApplyFilter(); UpdateStats(); });
            zm.ZoneSelected += zone => Dispatcher.Invoke(() =>
            {
                if (zone != null && LvZones.Items.Contains(zone))
                    LvZones.SelectedItem = zone;
            });

            ApplyFilter();
            UpdateStats();
        }

        // ?? Filter ????????????????????????????????????????????????????????????

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (RbBoundary?.IsChecked == true) _activeFilter = "Boundary";
            else if (RbAllowed?.IsChecked == true) _activeFilter = "Allowed";
            else if (RbForbidden?.IsChecked == true) _activeFilter = "Forbidden";
            else _activeFilter = "All";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_zoneManager == null) return;
            IEnumerable<CraneZone> src = _zoneManager.Zones;
            src = _activeFilter switch
            {
                "Boundary" => src.Where(z => z.IsSiteBoundary),
                "Allowed" => src.Where(z => z.ZoneType == ZoneType.Allowed && !z.IsSiteBoundary),
                "Forbidden" => src.Where(z => z.ZoneType == ZoneType.Forbidden && !z.IsSiteBoundary),
                _ => src
            };
            LvZones.ItemsSource = src.ToList();
        }

        // ?? Statistics ????????????????????????????????????????????????????????

        private void UpdateStats()
        {
            if (_zoneManager == null) return;

            int total = _zoneManager.Zones.Count;
            int boundary = _zoneManager.Zones.Count(z => z.IsSiteBoundary);
            int allowed = _zoneManager.Zones.Count(z => z.ZoneType == ZoneType.Allowed && !z.IsSiteBoundary);
            int forbidden = _zoneManager.Zones.Count(z => z.ZoneType == ZoneType.Forbidden && !z.IsSiteBoundary);

            TxtTotalCount.Text = $"({total})";
            TxtTotalCount2.Text = total.ToString();
            TxtBoundaryCount.Text = boundary.ToString();
            TxtAllowedCount.Text = allowed.ToString();
            TxtForbiddenCount.Text = forbidden.ToString();

            // Site area
            double area = _zoneManager.TotalSiteArea;
            TxtSiteArea.Text = area > 0 ? UnitSystem.FormatArea(area) : "";

            // Forbidden ground coverage %
            double pct = _zoneManager.ForbiddenGroundCoverage();
            TxtForbiddenPct.Text = area > 0 ? $"{pct:F1}%" : "?";
        }

        // ?? Selection ?????????????????????????????????????????????????????????

        private void LvZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var zone = LvZones.SelectedItem as CraneZone;
            if (zone != null) _zoneManager?.SelectZone(zone);
            ZoneSelected?.Invoke(zone);
        }

        private void LvZones_DoubleClick(object sender, MouseButtonEventArgs e) { }

        // ?? Visibility toggle all ?????????????????????????????????????????????

        private void ChkShowAll_Changed(object sender, RoutedEventArgs e)
        {
            if (_zoneManager == null) return;
            bool show = ChkShowAll.IsChecked == true;
            foreach (var z in _zoneManager.Zones) z.IsVisible = show;
        }

        // ?? Delete ????????????????????????????????????????????????????????????

        private void BtnItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CraneZone zone) ConfirmDelete(zone);
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var zone = LvZones.SelectedItem as CraneZone;
            if (zone == null)
            {
                MessageBox.Show("Select a zone first.", "Delete Zone", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ConfirmDelete(zone);
        }

        private void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (_zoneManager == null || _zoneManager.Zones.Count == 0) return;
            if (MessageBox.Show($"Delete all {_zoneManager.Zones.Count} zone(s)?", "Clear All",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var z in _zoneManager.Zones.ToList()) _zoneManager.RemoveZone(z);
                DeleteAllRequested?.Invoke();
                UpdateStats();
            }
        }

        private void ConfirmDelete(CraneZone zone)
        {
            if (MessageBox.Show($"Delete zone '{zone.DisplayLabel}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _zoneManager?.RemoveZone(zone);
                ZoneDeleteRequested?.Invoke(zone);
                ApplyFilter();
                UpdateStats();
            }
        }

        // ?? Export ????????????????????????????????????????????????????????????

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            if (_zoneManager == null) return;
            var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "zones.json" };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, _zoneManager.ExportToJson());
                ExportRequested?.Invoke();
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_zoneManager == null) return;
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "zones.csv" };
            if (dlg.ShowDialog() == true)
                File.WriteAllText(dlg.FileName, _zoneManager.ExportToCsv());
        }

        // ?? Drag-to-reorder ???????????????????????????????????????????????????

        private void LvZones_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedZone = null;
            _isDragging = false;
        }

        private void LvZones_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            var delta = e.GetPosition(null) - _dragStartPoint;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            // Find which item is under the cursor
            var item = GetListViewItemAt(e.GetPosition(LvZones));
            if (item == null) return;

            _draggedZone = item.DataContext as CraneZone;
            if (_draggedZone == null) return;

            _isDragging = true;
            DragDrop.DoDragDrop(LvZones, _draggedZone, DragDropEffects.Move);
            _isDragging = false;
        }

        private void LvZones_Drop(object sender, DragEventArgs e)
        {
            if (_draggedZone == null || _zoneManager == null) return;

            var target = GetListViewItemAt(e.GetPosition(LvZones));
            if (target == null) return;

            var targetZone = target.DataContext as CraneZone;
            if (targetZone == null || targetZone == _draggedZone) return;

            int fromIdx = _zoneManager.Zones.IndexOf(_draggedZone);
            int toIdx = _zoneManager.Zones.IndexOf(targetZone);
            if (fromIdx < 0 || toIdx < 0) return;

            _zoneManager.MoveZone(fromIdx, toIdx);
            ApplyFilter();
            _draggedZone = null;
        }

        private ListViewItem GetListViewItemAt(Point position)
        {
            var element = LvZones.InputHitTest(position) as DependencyObject;
            while (element != null)
            {
                if (element is ListViewItem lvi) return lvi;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }
            return null;
        }
    }
}