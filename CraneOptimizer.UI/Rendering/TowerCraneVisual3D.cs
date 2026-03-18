using HelixToolkit.Wpf;
using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public class TowerCraneVisual3D : ModelVisual3D, ICraneVisual
    {
        private TowerCraneParameters _params;

        private static readonly Material YellowMat = MaterialHelper.CreateMaterial(Color.FromRgb(255, 200, 0));
        private static readonly Material GrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(80, 80, 80));
        private static readonly Material LightGrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(140, 140, 140));
        private static readonly Material RedMat = MaterialHelper.CreateMaterial(Color.FromRgb(220, 60, 60));
        private static readonly Material BlueMat = MaterialHelper.CreateMaterial(Color.FromArgb(80, 52, 152, 219));
        private static readonly Material ExclusionMat = MaterialHelper.CreateMaterial(Color.FromArgb(100, 255, 120, 0));

        public CraneParametersBase Parameters => _params;
        public TowerCraneParameters TowerParams => _params;

        public TowerCraneVisual3D(TowerCraneParameters parameters)
        {
            _params = parameters;
            _params.PropertyChanged += OnParameterChanged;
            Rebuild();
        }

        private void OnParameterChanged(object sender, PropertyChangedEventArgs e) => Rebuild();

        public void Rebuild() => Content = BuildCraneGeometry();

        private Model3DGroup BuildCraneGeometry()
        {
            var group = new Model3DGroup();
            if (!_params.IsVisible) return group;

            // === BASE PLATFORM ===
            group.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(new Point3D(0, 0, 0), new Point3D(0, 0, 2), _params.BaseRadius, 24),
                GrayMat));

            // === TOWER ===
            double towerWidth = 1.8;
            group.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(
                    new Point3D(0, 0, 2 + _params.TowerHeight / 2),
                    towerWidth, towerWidth, _params.TowerHeight),
                YellowMat));

            // Tower lattice vertical edges
            double edgeWidth = 0.15;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    double x = i * (towerWidth / 2 - edgeWidth);
                    double y = j * (towerWidth / 2 - edgeWidth);
                    group.Children.Add(new GeometryModel3D(
                        MeshHelper.CreateCylinder(
                            new Point3D(x, y, 2),
                            new Point3D(x, y, 2 + _params.TowerHeight),
                            edgeWidth, 8),
                        YellowMat));
                }
            }

            // === SLEWING PLATFORM ===
            double topZ = 2 + _params.TowerHeight;
            group.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(new Point3D(0, 0, topZ), new Point3D(0, 0, topZ + 1.5), 2.2, 20),
                GrayMat));

            // === CABIN ===
            group.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(1.8, 0, topZ + 2.5), 2.5, 2.2, 2.5),
                YellowMat));

            // Cabin windows
            var windowMat = MaterialHelper.CreateMaterial(Color.FromRgb(100, 150, 200));
            group.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(3.1, 0, topZ + 2.8), 0.1, 1.6, 1.2),
                windowMat));

            // === BOOM GROUP (rotates with slew) ===
            var boomGroup = BuildBoomGroup(topZ + 1.5);
            boomGroup.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), _params.SlewAngle),
                new Point3D(0, 0, 0));
            group.Children.Add(boomGroup);

            // === SWING RADIUS INDICATOR ===
            if (_params.ShowSwingRadius)
            {
                group.Children.Add(new GeometryModel3D(
                    MeshHelper.CreatePipe(
                        new Point3D(0, 0, 0.1),
                        new Point3D(0, 0, 0.2),
                        _params.BoomLength - 0.5,
                        _params.BoomLength + 0.5, 48),
                    BlueMat));
            }

            // === EXCLUSION ZONE RING (orange) ===
            if (_params.ExclusionRadiusM > 0)
            {
                double excR = _params.ExclusionRadiusM;
                group.Children.Add(new GeometryModel3D(
                    MeshHelper.CreatePipe(
                        new Point3D(0, 0, 0.02),
                        new Point3D(0, 0, 0.08),
                        Math.Max(0.2, excR - 0.4),
                        excR + 0.4, 48),
                    ExclusionMat));
            }

            group.Transform = new TranslateTransform3D(
                _params.PositionX, _params.PositionY, _params.PositionZ);

            return group;
        }

        private Model3DGroup BuildBoomGroup(double baseZ)
        {
            var boomGroup = new Model3DGroup();
            double boomAngleRad = _params.BoomAngle * Math.PI / 180;
            double boomEndZ = _params.BoomLength * Math.Tan(boomAngleRad);

            // === MAIN BOOM (jib) ===
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ),
                    new Point3D(_params.BoomLength, 0, baseZ + boomEndZ),
                    0.5, 12),
                YellowMat));

            // Boom tip marker
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateSphere(
                    new Point3D(_params.BoomLength, 0, baseZ + boomEndZ), 0.8, 12, 12),
                RedMat));

            // === COUNTER-JIB ===
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ),
                    new Point3D(-_params.CounterJibLength, 0, baseZ + 2),
                    0.4, 8),
                YellowMat));

            // Counterweight
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(
                    new Point3D(-_params.CounterJibLength + 2, 0, baseZ), 4, 2, 2),
                GrayMat));

            // === A-FRAME ===
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ),
                    new Point3D(0, 0, baseZ + 8),
                    0.25, 8),
                YellowMat));

            // === CABLES ===
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ + 8),
                    new Point3D(_params.BoomLength, 0, baseZ + boomEndZ),
                    0.06, 6),
                LightGrayMat));

            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ + 8),
                    new Point3D(-_params.CounterJibLength, 0, baseZ + 2),
                    0.06, 6),
                LightGrayMat));

            // === HOISTING CABLE ===
            double hookZ = baseZ + boomEndZ - _params.CableLength;
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(_params.BoomLength, 0, baseZ + boomEndZ),
                    new Point3D(_params.BoomLength, 0, hookZ),
                    0.05, 6),
                LightGrayMat));

            // === HOOK BLOCK ===
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(
                    new Point3D(_params.BoomLength, 0, hookZ - 0.4), 0.8, 0.8, 1.0),
                RedMat));

            // Hook
            boomGroup.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(_params.BoomLength, 0, hookZ - 1),
                    new Point3D(_params.BoomLength, 0, hookZ - 1.8),
                    0.12, 6),
                LightGrayMat));

            return boomGroup;
        }

        public void Dispose()
        {
            if (_params != null)
                _params.PropertyChanged -= OnParameterChanged;
        }
    }
}