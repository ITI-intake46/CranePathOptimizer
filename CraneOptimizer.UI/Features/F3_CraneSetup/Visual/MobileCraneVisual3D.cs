using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using HelixToolkit.Wpf;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Visual
{
    internal sealed class MobileCraneVisual3D : ModelVisual3D, ICraneVisual
    {
        private const double SceneUnitsPerMeter = 1.0;
        private readonly MobileCraneParameters _params;

        private static Material CreatePhongMaterial(
            Color diffuseColor,
            double ambientStrength,
            Color specularColor,
            double specularStrength,
            double specularPower)
        {
            var diffuseBrush = new SolidColorBrush(diffuseColor);
            diffuseBrush.Freeze();

            var specularBrush = new SolidColorBrush(WithAlpha(specularColor, specularStrength));
            specularBrush.Freeze();

            // WPF combines DiffuseMaterial, AmbientLight/AmbientColor and SpecularMaterial
            // into a Phong-style response driven by the normals generated in MeshHelper.
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(diffuseBrush)
            {
                AmbientColor = ScaleRgb(diffuseColor, ambientStrength)
            });
            group.Children.Add(new SpecularMaterial(specularBrush, specularPower));
            group.Freeze();
            return group;
        }

        private static readonly Material YellowMat      = CreatePhongMaterial(Color.FromRgb(245, 185, 26), 0.34, Color.FromRgb(255, 245, 210), 0.56, 88);
        private static readonly Material OrangePadMat   = CreatePhongMaterial(Color.FromRgb(204, 126, 70), 0.30, Color.FromRgb(245, 220, 190), 0.28, 38);
        private static readonly Material DarkGrayMat    = CreatePhongMaterial(Color.FromRgb(44, 48, 52), 0.28, Color.FromRgb(225, 230, 235), 0.42, 72);
        private static readonly Material LightGrayMat   = CreatePhongMaterial(Color.FromRgb(160, 166, 170), 0.32, Color.FromRgb(255, 255, 255), 0.72, 118);
        private static readonly Material RedMat         = CreatePhongMaterial(Color.FromRgb(195, 45, 42), 0.28, Color.FromRgb(255, 230, 220), 0.42, 72);
        private static readonly Material WhiteStripeMat = CreatePhongMaterial(Color.FromRgb(238, 238, 230), 0.45, Color.FromRgb(255, 255, 255), 0.38, 70);
        private static readonly Material BlackMat       = CreatePhongMaterial(Color.FromRgb(22, 22, 22), 0.22, Color.FromRgb(170, 170, 170), 0.18, 28);
        private static readonly Material WindowMat      = CreatePhongMaterial(Color.FromRgb(65, 105, 145), 0.24, Color.FromRgb(235, 250, 255), 0.92, 142);
        private static readonly Material SteelMat       = CreatePhongMaterial(Color.FromRgb(105, 112, 118), 0.32, Color.FromRgb(255, 255, 255), 0.78, 126);
        private static readonly Material RubberMat      = CreatePhongMaterial(Color.FromRgb(12, 12, 12), 0.18, Color.FromRgb(120, 120, 120), 0.12, 22);
        private static readonly Material AmberMat       = CreatePhongMaterial(Color.FromRgb(255, 142, 36), 0.45, Color.FromRgb(255, 240, 210), 0.34, 48);
        private static readonly Material SwingMat       = MaterialHelper.CreateMaterial(Color.FromArgb(80, 52, 152, 219));
        private static readonly Material ExclusionMat   = MaterialHelper.CreateMaterial(Color.FromArgb(100, 214, 138, 86));

        private static Color ScaleRgb(Color color, double strength)
        {
            strength = Math.Clamp(strength, 0.0, 1.0);
            return Color.FromRgb(
                ToByte(color.R * strength),
                ToByte(color.G * strength),
                ToByte(color.B * strength));
        }

        private static Color WithAlpha(Color color, double alpha)
            => Color.FromArgb(ToByte(255 * Math.Clamp(alpha, 0.0, 1.0)), color.R, color.G, color.B);

        private static byte ToByte(double value)
            => (byte)Math.Clamp(Math.Round(value), 0, 255);

        private static GeometryModel3D CreateModel(MeshGeometry3D mesh, Material material)
            => new(mesh, material) { BackMaterial = material };

        private static GeometryModel3D CreateBoomBox(
            Point3D pivot,
            double startDistance,
            double endDistance,
            double width,
            double height,
            double boomAngleDegrees,
            Material material)
        {
            double length = Math.Max(0.01, endDistance - startDistance);
            var center = new Point3D(pivot.X + startDistance + length / 2.0, 0, pivot.Z);
            double chamfer = Math.Min(width, height) * 0.18;
            var model = CreateModel(MeshHelper.CreateChamferedBox(center, length, width, height, chamfer), material);
            model.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), -boomAngleDegrees),
                pivot);
            return model;
        }

        private static void AddCylinderChain(
            Model3DGroup group,
            IReadOnlyList<Point3D> points,
            double radius,
            Material material)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                group.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(points[i], points[i + 1], radius, 18),
                    material));
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                group.Children.Add(CreateModel(
                    MeshHelper.CreateSphere(points[i], radius * 1.04, 12, 8),
                    material));
            }
        }

        public CraneParametersBase Parameters => _params;
        public MobileCraneParameters MobileParams => _params;

        public MobileCraneVisual3D(MobileCraneParameters parameters)
        {
            _params = parameters;
            _params.PropertyChanged += OnParameterChanged;
            Rebuild();
        }

        private void OnParameterChanged(object? sender, PropertyChangedEventArgs e) => Rebuild();

        public void Rebuild() => Content = BuildCraneGeometry();

        public void Dispose() => _params.PropertyChanged -= OnParameterChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Top-level builder
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildCraneGeometry()
        {
            var group = new Model3DGroup();
            if (!_params.IsVisible)
                return group;

            if (_params.ShowOutriggerPads)
                group.Children.Add(BuildOutriggerPads());

            group.Children.Add(BuildOutriggerLegs());

            // Carrier
            group.Children.Add(BuildCarrier());

            // Turntable
            double turntableZ = _params.CarrierHeight;
            group.Children.Add(BuildTurntable(turntableZ));

            // Superstructure + boom — rotated around Z by SlewAngle
            var superstructure = BuildSuperstructure(turntableZ);
            superstructure.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), _params.SlewAngle),
                new Point3D(0, 0, 0));
            group.Children.Add(superstructure);

            if (_params.ShowSwingRadius)
                group.Children.Add(BuildSwingRadiusRing());

            if (_params.ExclusionRadiusM > 0)
                group.Children.Add(BuildExclusionRing());

            var transformGroup = new Transform3DGroup();
            if (_params.BodyAngle != 0)
            {
                transformGroup.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), _params.BodyAngle),
                    new Point3D(0, 0, 0)));
            }

            transformGroup.Children.Add(new ScaleTransform3D(
                SceneUnitsPerMeter,
                SceneUnitsPerMeter,
                SceneUnitsPerMeter));

            transformGroup.Children.Add(new TranslateTransform3D(
                _params.PositionX, _params.PositionY, _params.PositionZ));
            group.Transform = transformGroup;

            return group;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Radius overlays
        // ─────────────────────────────────────────────────────────────────────

        private GeometryModel3D BuildSwingRadiusRing()
        {
            double r = Math.Max(1.0, _params.HookRadius);
            return new GeometryModel3D(
                MeshHelper.CreatePipe(
                    new Point3D(0, 0, 0.05),
                    new Point3D(0, 0, 0.15),
                    Math.Max(0.2, r - 0.5), r + 0.5, 48),
                SwingMat);
        }

        private GeometryModel3D BuildExclusionRing()
        {
            double halfW = _params.OutriggerSpanM / 2.0;
            double halfB = _params.SpanToSpanM / 2.0;
            double r = Math.Max(halfW, halfB) + _params.ExclusionRadiusM;
            return new GeometryModel3D(
                MeshHelper.CreatePipe(
                    new Point3D(0, 0, 0.02),
                    new Point3D(0, 0, 0.08),
                    Math.Max(0.2, r - 0.4), r + 0.4, 48),
                ExclusionMat);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Outrigger pads
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildOutriggerPads()
        {
            var g = new Model3DGroup();
            double halfW = _params.OutriggerSpanM / 2.0;
            double halfB = _params.SpanToSpanM / 2.0;
            double padSize = _params.OutriggerPadSize;
            const double padThickness = 0.12;

            var positions = new[]
            {
                new Point3D( halfW,  halfB, padThickness / 2.0),
                new Point3D( halfW, -halfB, padThickness / 2.0),
                new Point3D(-halfW,  halfB, padThickness / 2.0),
                new Point3D(-halfW, -halfB, padThickness / 2.0)
            };

            foreach (var pos in positions)
            {
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(pos, padSize, padSize, padThickness), OrangePadMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2.0 + 0.02),
                        padSize + 0.08, padSize + 0.08, 0.02), BlackMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2.0 + 0.03),
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2.0 + 0.08),
                        padSize * 0.24, 32),
                    SteelMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2.0 + 0.1),
                        padSize * 0.5, 0.08, 0.06),
                    DarkGrayMat));
            }

            return g;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Outrigger legs
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildOutriggerLegs()
        {
            var g = new Model3DGroup();
            double halfW = _params.OutriggerSpanM / 2.0;
            double halfB = _params.SpanToSpanM / 2.0;
            double effectiveBodyWidth = _params.TravelWidthM > 0
                ? Math.Max(1.2, _params.TravelWidthM - 1.35)
                : _params.CarrierWidth;
            double carrierHalfWidth  = effectiveBodyWidth / 2.0 + 0.3;
            double carrierHalfLength = _params.CarrierLength / 4.0;
            double legTopZ    = _params.CarrierHeight + 0.2;
            double legBottomZ = 0.2;

            var data = new[]
            {
                (c: new Point3D( carrierHalfLength,  carrierHalfWidth, legTopZ),
                 p: new Point3D( halfW,  halfB, legBottomZ)),
                (c: new Point3D( carrierHalfLength, -carrierHalfWidth, legTopZ),
                 p: new Point3D( halfW, -halfB, legBottomZ)),
                (c: new Point3D(-carrierHalfLength,  carrierHalfWidth, legTopZ),
                 p: new Point3D(-halfW,  halfB, legBottomZ)),
                (c: new Point3D(-carrierHalfLength, -carrierHalfWidth, legTopZ),
                 p: new Point3D(-halfW, -halfB, legBottomZ))
            };

            foreach (var (carrier, pad) in data)
            {
                var beamEnd = new Point3D(pad.X, pad.Y, carrier.Z);
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(carrier, beamEnd, 0.18, 8), YellowMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        new Point3D(carrier.X, carrier.Y, carrier.Z + 0.08),
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z + 0.08),
                        0.08, 16), SteelMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(carrier, 0.7, 0.7, 0.55), YellowMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(beamEnd, 0.55, 0.55, 0.45), YellowMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(beamEnd, pad, 0.15, 12), LightGrayMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z + 0.1),
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z - 0.4),
                        0.22, 12), YellowMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        pad, new Point3D(pad.X, pad.Y, pad.Z - 0.08),
                        0.35, 12), DarkGrayMat));
            }

            return g;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Carrier
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildCarrier()
        {
            var g = new Model3DGroup();
            double length = _params.CarrierLength;
            double width = _params.TravelWidthM > 0
                ? Math.Max(1.2, _params.TravelWidthM - 1.35)
                : _params.CarrierWidth;
            double height = _params.CarrierHeight;

            // Body surfaces are chamfered inside the same overall footprint so the carrier reads as heavy steel, not stacked blocks.
            g.Children.Add(CreateModel(
                MeshHelper.CreateChamferedBox(new Point3D(0, 0, height / 2), length, width, height, Math.Min(width, height) * 0.18),
                YellowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateChamferedBox(new Point3D(0, 0, height + 0.14), length * 0.58, width * 0.72, 0.28, 0.08),
                YellowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateChamferedBox(new Point3D(length / 2 - 0.58, 0, height + 0.22), 1.34, width * 0.54, 0.44, 0.12),
                YellowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(0, 0, 0.3), length * 0.92, width * 0.78, 0.36),
                DarkGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(length / 2 - 0.18, 0, height + 0.48), 0.08, width * 0.5, 0.34),
                WindowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(length / 2 - 0.85, width * 0.31, height + 0.48), 0.72, 0.06, 0.34),
                WindowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(length / 2 - 0.85, -width * 0.31, height + 0.48), 0.72, 0.06, 0.34),
                WindowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(length / 2 + 0.02, 0, 0.72), 0.08, width * 0.72, 0.42),
                DarkGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(-length / 2 - 0.02, 0, 0.7), 0.08, width * 0.72, 0.38),
                DarkGrayMat));

            // White stripes on sides
            for (int side = -1; side <= 1; side += 2)
            {
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(
                        new Point3D(0, side * (width / 2 + 0.05), height * 0.6),
                        length - 1, 0.1, height - 0.2),
                    WhiteStripeMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        new Point3D(-length * 0.42, side * (width / 2 + 0.055), height * 0.9),
                        new Point3D(length * 0.42, side * (width / 2 + 0.055), height * 0.9),
                        0.045, 16),
                    YellowMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        new Point3D(-length * 0.43, side * (width / 2 + 0.06), height * 0.22),
                        new Point3D(length * 0.43, side * (width / 2 + 0.06), height * 0.22),
                        0.04, 16),
                    DarkGrayMat));
            }


            // Wheels — fixed proportions matching reference
            double lampX = length / 2 + 0.055;
            foreach (double lampY in new[] { -width * 0.24, width * 0.24 })
            {
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(new Point3D(lampX, lampY, 0.98), 0.04, 0.18, 0.12),
                    WhiteStripeMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateBox(new Point3D(-lampX, lampY, 0.92), 0.04, 0.16, 0.1),
                    RedMat));
            }

            double wheelRadius = 0.45;
            double wheelWidth  = 0.35;
            double wheelY = width / 2 + wheelWidth / 2 + 0.1;
            double[] wheelXs = { length / 2 - 1.0, length / 2 - 2.2, -length / 2 + 1.5, -length / 2 + 2.7 };

            foreach (double wx in wheelXs)
            {
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, wheelY, wheelRadius),
                        new Point3D(wx, wheelY + wheelWidth, wheelRadius),
                        wheelRadius, 24), RubberMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, -wheelY, wheelRadius),
                        new Point3D(wx, -wheelY - wheelWidth, wheelRadius),
                        wheelRadius, 24), RubberMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, wheelY + wheelWidth, wheelRadius),
                        new Point3D(wx, wheelY + wheelWidth + 0.05, wheelRadius),
                        wheelRadius * 0.38, 20), LightGrayMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, -wheelY - wheelWidth, wheelRadius),
                        new Point3D(wx, -wheelY - wheelWidth - 0.05, wheelRadius),
                        wheelRadius * 0.38, 20), LightGrayMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateChamferedBox(new Point3D(wx, wheelY - wheelWidth * 0.2, wheelRadius + 0.44), 1.02, 0.18, 0.22, 0.05),
                    DarkGrayMat));
                g.Children.Add(CreateModel(
                    MeshHelper.CreateChamferedBox(new Point3D(wx, -wheelY + wheelWidth * 0.2, wheelRadius + 0.44), 1.02, 0.18, 0.22, 0.05),
                    DarkGrayMat));
            }

            return g;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Turntable
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildTurntable(double baseZ)
        {
            var g = new Model3DGroup();
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreatePipe(
                    new Point3D(0, 0, baseZ),
                    new Point3D(0, 0, baseZ + 0.3),
                    1.2, 1.6, 24),
                DarkGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(0, 0, baseZ + 0.05),
                    new Point3D(0, 0, baseZ + 0.42),
                    1.08, 40),
                SteelMat));
            for (int i = 0; i < 12; i++)
            {
                double a = 2 * Math.PI * i / 12.0;
                var bolt = new Point3D(1.43 * Math.Cos(a), 1.43 * Math.Sin(a), baseZ + 0.36);
                g.Children.Add(CreateModel(
                    MeshHelper.CreateCylinder(
                        bolt,
                        new Point3D(bolt.X, bolt.Y, bolt.Z + 0.06),
                        0.055, 10),
                    LightGrayMat));
            }
            return g;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Superstructure
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildSuperstructure(double baseZ)
        {
            var g = new Model3DGroup();
            double superZ = baseZ + 0.3;

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-0.5, 0, superZ + 0.5), 3, 2.5, 1), YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-1.5, 0, superZ + 1.6), 2.2, 2.2, 2.2), YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-0.35, 0, superZ + 1.8), 0.08, 1.4, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-1.5, 1.15, superZ + 1.8), 1.2, 0.08, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-1.5, -1.15, superZ + 1.8), 1.2, 0.08, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-2.8, 0, superZ + 0.8), 2, 2.2, 1.4), DarkGrayMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(-2.5, 0, superZ + 2), 1.5, 1.8, 1), YellowMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(-3.28, 0, superZ + 0.95), 0.18, 2.26, 1.1),
                LightGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(-3.0, 0, superZ + 1.45), 1.55, 2.12, 0.12),
                SteelMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(0.35, -1.42, superZ + 1.45),
                    new Point3D(0.35, 1.42, superZ + 1.45),
                    0.22, 32),
                SteelMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(-2.95, -0.82, superZ + 2.5),
                    new Point3D(-2.95, -0.82, superZ + 3.25),
                    0.09, 18),
                DarkGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(-1.2, 0, superZ + 2.63),
                    new Point3D(-1.2, 0, superZ + 2.78),
                    0.12, 20),
                AmberMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(-0.36, 0, superZ + 2.36), 0.1, 1.36, 0.18),
                DarkGrayMat));

            g.Children.Add(BuildBoom(superZ + 1));
            return g;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Boom
        // ─────────────────────────────────────────────────────────────────────

        private Model3DGroup BuildBoom(double baseZ)
        {
            var g = new Model3DGroup();
            double boomAngleRad = _params.BoomAngle * Math.PI / 180.0;
            var pivot = new Point3D(0.5, 0, baseZ + 0.5);

            double boomLen = Math.Clamp(_params.BoomLength, _params.MinBoomLength, _params.MaxBoomLength);
            double boomEndX = pivot.X + boomLen * Math.Cos(boomAngleRad);
            double boomEndZ = pivot.Z + boomLen * Math.Sin(boomAngleRad);
            var boomEnd = new Point3D(boomEndX, 0, boomEndZ);

            int sections = Math.Max(1, _params.BoomSections);
            double boomBase = _params.BoomBaseWidth > 0 ? _params.BoomBaseWidth : 0.8;

            for (int i = 0; i < sections; i++)
            {
                double t1 = (double)i / sections;
                double t2 = (double)(i + 1) / sections;
                double avgWidth = boomBase * (1 - ((t1 + t2) / 2.0) * 0.6);

                g.Children.Add(CreateBoomBox(
                    pivot,
                    t1 * boomLen,
                    t2 * boomLen,
                    Math.Max(0.22, avgWidth * 0.72),
                    Math.Max(0.20, avgWidth * 0.48),
                    _params.BoomAngle,
                    YellowMat));
                g.Children.Add(CreateBoomBox(
                    pivot,
                    t1 * boomLen + (t2 - t1) * boomLen * 0.08,
                    t2 * boomLen - (t2 - t1) * boomLen * 0.08,
                    Math.Max(0.18, avgWidth * 0.58),
                    0.028,
                    _params.BoomAngle,
                    DarkGrayMat));

                if (i > 0)
                {
                    g.Children.Add(CreateBoomBox(
                        pivot,
                        t1 * boomLen - 0.08,
                        t1 * boomLen + 0.08,
                        Math.Max(0.26, avgWidth * 0.8),
                        Math.Max(0.23, avgWidth * 0.56),
                        _params.BoomAngle,
                        SteelMat));
                }
            }

            // Boom head
            g.Children.Add(CreateModel(
                MeshHelper.CreateChamferedBox(boomEnd, 0.6, 0.42, 0.58, 0.08), YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.35, boomEnd.Z),
                    new Point3D(boomEnd.X,  0.35, boomEnd.Z),
                    0.25, 16), LightGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X + 0.18, -0.28, boomEnd.Z - 0.05),
                    new Point3D(boomEnd.X + 0.18, 0.28, boomEnd.Z - 0.05),
                    0.18, 24),
                DarkGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateBox(new Point3D(boomEnd.X + 0.22, 0, boomEnd.Z - 0.42), 0.12, 0.68, 0.22),
                SteelMat));



            // Hook and cable
            double hookZ = boomEnd.Z - Math.Max(_params.CableLength, 1.0);
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, boomEnd.Z - 0.3),
                    new Point3D(boomEnd.X, 0, hookZ),
                    0.04, 6), BlackMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateChamferedBox(new Point3D(boomEnd.X, 0, hookZ - 0.34), 0.52, 0.42, 0.66, 0.08), RedMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.26, hookZ - 0.16),
                    new Point3D(boomEnd.X, 0.26, hookZ - 0.16),
                    0.15, 20), LightGrayMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.2, hookZ - 0.66),
                    new Point3D(boomEnd.X, 0.2, hookZ - 0.66),
                    0.11, 18), SteelMat));
            g.Children.Add(CreateModel(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, hookZ - 0.66),
                    new Point3D(boomEnd.X, 0, hookZ - 1.05),
                    0.07, 16), SteelMat));

            var hookPoints = new List<Point3D>();
            var hookCenter = new Point3D(boomEnd.X + 0.21, 0, hookZ - 1.2);
            const double hookRadius = 0.25;
            for (int i = 0; i <= 10; i++)
            {
                double angle = (198 + i * 24) * Math.PI / 180.0;
                hookPoints.Add(new Point3D(
                    hookCenter.X + hookRadius * Math.Cos(angle),
                    0,
                    hookCenter.Z + hookRadius * Math.Sin(angle)));
            }

            hookPoints.Insert(0, new Point3D(boomEnd.X, 0, hookZ - 1.05));
            hookPoints.Add(new Point3D(boomEnd.X + 0.34, 0, hookZ - 1.03));
            AddCylinderChain(g, hookPoints, 0.065, SteelMat);

            return g;
        }
    }
}
