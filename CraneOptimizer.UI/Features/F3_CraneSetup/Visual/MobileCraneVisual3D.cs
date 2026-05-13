using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using HelixToolkit.Wpf;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Visual
{
    internal sealed class MobileCraneVisual3D : ModelVisual3D, ICraneVisual
    {
        private const double SceneUnitsPerMeter = 1000.0;
        private readonly MobileCraneParameters _params;

        private static Material CreatePhongMaterial(Color color, Color specularColor, double specularPower = 60.0)
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(specularColor), specularPower));
            return group;
        }

        private static readonly Material YellowMat      = CreatePhongMaterial(Color.FromRgb(255, 190, 0), Colors.White, 70);
        private static readonly Material OrangePadMat   = CreatePhongMaterial(Color.FromRgb(214, 138, 86), Colors.DarkGray, 30);
        private static readonly Material DarkGrayMat    = CreatePhongMaterial(Color.FromRgb(50,  50,  50), Colors.White, 40);
        private static readonly Material LightGrayMat   = CreatePhongMaterial(Color.FromRgb(150, 150, 150), Colors.White, 80);
        private static readonly Material RedMat         = CreatePhongMaterial(Color.FromRgb(200, 50,  50), Colors.White, 60);
        private static readonly Material WhiteStripeMat = CreatePhongMaterial(Color.FromRgb(240, 240, 240), Colors.White, 50);
        private static readonly Material BlackMat       = CreatePhongMaterial(Color.FromRgb(25,  25,  25), Colors.DimGray, 20);
        private static readonly Material WindowMat      = CreatePhongMaterial(Color.FromRgb(80,  120, 160), Colors.White, 100);
        private static readonly Material SwingMat       = MaterialHelper.CreateMaterial(Color.FromArgb(80, 52, 152, 219));
        private static readonly Material ExclusionMat   = MaterialHelper.CreateMaterial(Color.FromArgb(100, 214, 138, 86));

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
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(pos, padSize, padSize, padThickness), OrangePadMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2.0 + 0.02),
                        padSize + 0.08, padSize + 0.08, 0.02), BlackMat));
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
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(carrier, beamEnd, 0.18, 8), YellowMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(carrier, 0.6, 0.6, 0.5), YellowMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(beamEnd, pad, 0.15, 12), LightGrayMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z + 0.1),
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z - 0.4),
                        0.22, 12), YellowMat));
                g.Children.Add(new GeometryModel3D(
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

            // Body
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(0, 0, height / 2), length, width, height),
                YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(0, 0, height + 0.15), length * 0.6, width * 0.8, 0.3),
                YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(length / 2 - 0.5, 0, height + 0.25), 1.5, width * 0.6, 0.5),
                YellowMat));

            // White stripes on sides
            for (int side = -1; side <= 1; side += 2)
            {
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(
                        new Point3D(0, side * (width / 2 + 0.05), height * 0.6),
                        length - 1, 0.1, height - 0.2),
                    WhiteStripeMat));
            }


            // Wheels — fixed proportions matching reference
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
                        wheelRadius, 16), BlackMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, -wheelY, wheelRadius),
                        new Point3D(wx, -wheelY - wheelWidth, wheelRadius),
                        wheelRadius, 16), BlackMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, wheelY + wheelWidth, wheelRadius),
                        new Point3D(wx, wheelY + wheelWidth + 0.05, wheelRadius),
                        wheelRadius * 0.4, 12), LightGrayMat));
                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(wx, -wheelY - wheelWidth, wheelRadius),
                        new Point3D(wx, -wheelY - wheelWidth - 0.05, wheelRadius),
                        wheelRadius * 0.4, 12), LightGrayMat));
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

                var sStart = new Point3D(
                    pivot.X + t1 * boomLen * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t1 * boomLen * Math.Sin(boomAngleRad));
                var sEnd = new Point3D(
                    pivot.X + t2 * boomLen * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t2 * boomLen * Math.Sin(boomAngleRad));

                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(sStart, sEnd, avgWidth, 8), YellowMat));

                if (i > 0)
                {
                    g.Children.Add(new GeometryModel3D(
                        MeshHelper.CreateCylinder(
                            new Point3D(sStart.X - 0.1 * Math.Cos(boomAngleRad), 0,
                                        sStart.Z - 0.1 * Math.Sin(boomAngleRad)),
                            new Point3D(sStart.X + 0.1 * Math.Cos(boomAngleRad), 0,
                                        sStart.Z + 0.1 * Math.Sin(boomAngleRad)),
                            avgWidth + 0.05, 12),
                        LightGrayMat));
                }
            }

            // Boom head
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(boomEnd, 0.6, 0.5, 0.8), YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.35, boomEnd.Z),
                    new Point3D(boomEnd.X,  0.35, boomEnd.Z),
                    0.25, 16), LightGrayMat));



            // Hook and cable
            double hookZ = boomEnd.Z - Math.Max(_params.CableLength, 1.0);
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, boomEnd.Z - 0.3),
                    new Point3D(boomEnd.X, 0, hookZ),
                    0.04, 6), BlackMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(boomEnd.X, 0, hookZ - 0.4), 0.7, 0.5, 1.0), RedMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.3, hookZ - 0.2),
                    new Point3D(boomEnd.X,  0.3, hookZ - 0.2),
                    0.18, 12), LightGrayMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, hookZ - 0.9),
                    new Point3D(boomEnd.X, 0, hookZ - 1.5),
                    0.1, 8), LightGrayMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateSphere(
                    new Point3D(boomEnd.X + 0.15, 0, hookZ - 1.6), 0.15, 8, 8), LightGrayMat));

            return g;
        }
    }
}
