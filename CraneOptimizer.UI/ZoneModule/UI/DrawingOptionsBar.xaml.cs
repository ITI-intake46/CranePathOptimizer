using System;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.ZoneModule.Models;
using WPF_IFC_test1.ZoneModule.Drawing;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class DrawingOptionsBar : UserControl
    {
        // ── Original events (kept) ────────────────────────────────────────────
        public event Action<ZoneType> ZoneTypeChanged;
        public event Action<bool> SnapChanged;
        public event Action<double> OpacityChanged;
        public event Action FinishZoneRequested;
        public event Action CancelZoneRequested;

        // ── Events expected by ZoneModuleController ───────────────────────────
        public event Action FinishRequested;
        public event Action CancelRequested;

        public ZoneType SelectedZoneType { get; private set; } = ZoneType.Allowed;
        public bool SnapToGrid { get; private set; } = true;
        public double FillOpacity { get; private set; } = 0.40;

        public DrawingOptionsBar() => InitializeComponent();

        // ── Methods expected by ZoneModuleController ──────────────────────────
        public void ShowForDraw(ZoneType zoneType, ShapeTool tool)
        {
            SelectedZoneType = zoneType;
            Visibility = Visibility.Visible;
            SetDrawingActive(true, 0);
        }

        public void HideBar()
        {
            Visibility = Visibility.Collapsed;
            SetDrawingActive(false, 0);
        }

        // ── Original public API (kept) ────────────────────────────────────────
        public void SetDrawingActive(bool active, int pointCount = 0)
        {
            BtnFinishZone.IsEnabled = active && pointCount >= 3;
            BtnCancelZone.IsEnabled = active;
            if (active)
            {
                TxtDrawStatus.Text = pointCount < 3
                    ? $"🖊 {pointCount} point{(pointCount == 1 ? "" : "s")} — need at least 3"
                    : $"🖊 {pointCount} points — double-click or ✔ to finish";
                TxtDrawStatus.FontStyle = FontStyles.Normal;
            }
            else
            {
                TxtDrawStatus.Text = "Click on the canvas to start drawing";
                TxtDrawStatus.FontStyle = FontStyles.Italic;
            }
        }

        public void UpdatePointCount(int count)
        {
            BtnFinishZone.IsEnabled = count >= 3;
            TxtDrawStatus.Text = count < 3
                ? $"🖊 {count} point{(count == 1 ? "" : "s")} — need at least 3"
                : $"🖊 {count} points — double-click or ✔ to finish";
        }

        // ── Handlers ──────────────────────────────────────────────────────────
        private void ZoneType_Checked(object sender, RoutedEventArgs e)
        {
            if (RbSiteBoundary?.IsChecked == true) SelectedZoneType = ZoneType.SiteBoundary;
            else if (RbForbidden?.IsChecked == true) SelectedZoneType = ZoneType.Forbidden;
            else SelectedZoneType = ZoneType.Allowed;
            ZoneTypeChanged?.Invoke(SelectedZoneType);
        }

        private void ChkSnap_Changed(object sender, RoutedEventArgs e)
        {
            SnapToGrid = ChkSnap.IsChecked == true;
            SnapChanged?.Invoke(SnapToGrid);
        }

        private void SliderOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            FillOpacity = e.NewValue / 100.0;
            if (TxtOpacity != null) TxtOpacity.Text = $"{(int)e.NewValue}%";
            OpacityChanged?.Invoke(FillOpacity);
        }

        private void BtnFinishZone_Click(object sender, RoutedEventArgs e)
        {
            FinishRequested?.Invoke();
        }

        private void BtnCancelZone_Click(object sender, RoutedEventArgs e)
        {
            CancelZoneRequested?.Invoke();
            CancelRequested?.Invoke();      // controller alias
        }
    }
}