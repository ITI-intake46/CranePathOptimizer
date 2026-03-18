using System;
using System.Windows;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.DXF;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class DxfAlignmentDialog : Window
    {
        private readonly DxfUploader _uploader;

        public Matrix3D ResultTransform { get; private set; } = Matrix3D.Identity;

        public DxfAlignmentDialog(DxfUploader uploader)
        {
            InitializeComponent();
            _uploader = uploader;
        }

        // ── Method A: Point Matching ──────────────────────────────────────────

        private void ApplyPointMatch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double dxfX = double.Parse(DxfPointX.Text);
                double dxfY = double.Parse(DxfPointY.Text);
                double ifcX = double.Parse(IfcPointX.Text);
                double ifcY = double.Parse(IfcPointY.Text);

                // Pure translation: move DXF origin so that (dxfX,dxfY) lands on (ifcX,ifcY)
                double tx = ifcX - dxfX;
                double ty = ifcY - dxfY;

                var transform = Matrix3D.Identity;
                transform.Translate(new Vector3D(tx, ty, 0));

                ApplyTransform(transform);
                PointMatchStatus.Text = $"✓ Offset: ({tx:F2}, {ty:F2})";
            }
            catch (Exception ex)
            {
                PointMatchStatus.Text = $"⚠ {ex.Message}";
                PointMatchStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        // ── Method B: Manual Transform ────────────────────────────────────────

        private void ApplyManualTransform_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double tx  = double.Parse(TranslateX.Text);
                double ty  = double.Parse(TranslateY.Text);
                double sc  = double.Parse(Scale.Text);
                double rot = double.Parse(Rotation.Text) * Math.PI / 180.0;

                // Build 4x4 matrix: scale, then rotate about Z, then translate
                double cos = Math.Cos(rot), sin = Math.Sin(rot);

                var m = new Matrix3D(
                    sc * cos, -sc * sin, 0, 0,
                    sc * sin,  sc * cos, 0, 0,
                    0,         0,        1, 0,
                    tx,        ty,       0, 1
                );

                ApplyTransform(m);
                ManualStatus.Text = $"✓ Applied (tx={tx:F1}, ty={ty:F1}, rot={double.Parse(Rotation.Text):F1}°)";
            }
            catch (Exception ex)
            {
                ManualStatus.Text = $"⚠ {ex.Message}";
                ManualStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private void ApplyTransform(Matrix3D transform)
        {
            ResultTransform = transform;
            _uploader.AlignmentTransform = transform;
            _uploader.ReApplyTransform();
        }

        private void ResetTransform_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransform(Matrix3D.Identity);
            PointMatchStatus.Text = "";
            ManualStatus.Text     = "✓ Reset to identity";
            TranslateX.Text = "0.0"; TranslateY.Text = "0.0";
            Scale.Text = "1.0"; Rotation.Text = "0.0";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
