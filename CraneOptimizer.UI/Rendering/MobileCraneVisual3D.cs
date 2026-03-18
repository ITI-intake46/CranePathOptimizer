using HelixToolkit.Wpf;
using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public class MobileCraneVisual3D : ModelVisual3D, ICraneVisual
    {
        private MobileCraneParameters _params;

        private static readonly Material YellowMat = MaterialHelper.CreateMaterial(Color.FromRgb(255, 190, 0));
        private static readonly Material OrangePadMat = MaterialHelper.CreateMaterial(Color.FromRgb(255, 140, 0));
        private static readonly Material DarkGrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(50, 50, 50));
        private static readonly Material LightGrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(130, 130, 130));
        private static readonly Material RedMat = MaterialHelper.CreateMaterial(Color.FromRgb(200, 50, 50));
        private static readonly Material WhiteStripeMat = MaterialHelper.CreateMaterial(Color.FromRgb(240, 240, 240));
        private static readonly Material BlackMat = MaterialHelper.CreateMaterial(Color.FromRgb(25, 25, 25));
        private static readonly Material WindowMat = MaterialHelper.CreateMaterial(Color.FromRgb(80, 120, 160));
        private static readonly Material BlueMat = MaterialHelper.CreateMaterial(Color.FromArgb(80, 52, 152, 219));
        private static readonly Material ExclusionMat = MaterialHelper.CreateMaterial(Color.FromArgb(100, 255, 120, 0));

        public CraneParametersBase Parameters => _params;
        public MobileCraneParameters MobileParams => _params;

        public MobileCraneVisual3D(MobileCraneParameters parameters)
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

            if (_params.ShowOutriggerPads)
                group.Children.Add(BuildOutriggerPads());

            group.Children.Add(BuildOutriggerLegs());
            group.Children.Add(BuildCarrier());

            double turntableZ = _params.CarrierHeight;
            group.Children.Add(BuildTurntable(turntableZ));

            var superstructure = BuildSuperstructure(turntableZ);
            superstructure.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), _params.SlewAngle),
                new Point3D(0, 0, 0));
            group.Children.Add(superstructure);

            if (_params.ShowSwingRadius)
            {
                group.Children.Add(new GeometryModel3D(
                    MeshHelper.CreatePipe(
                        new Point3D(0, 0, 0.05),
                        new Point3D(0, 0, 0.15),
                        _params.HookRadius - 0.5,
                        _params.HookRadius + 0.5, 48),
                    BlueMat));
            }

            // ── Exclusion zone ring (orange) ─────────────────────────────────
            if (_params.ExclusionRadiusM > 0)
            {
                double halfW = _params.OutriggerSpanM / 2.0;
                double halfB = _params.SpanToSpanM    / 2.0;
                double excR  = Math.Max(halfW, halfB) + _params.ExclusionRadiusM;
                group.Children.Add(new GeometryModel3D(
                    MeshHelper.CreatePipe(
                        new Point3D(0, 0, 0.02),
                        new Point3D(0, 0, 0.08),
                        Math.Max(0.2, excR - 0.4),
                        excR + 0.4, 48),
                    ExclusionMat));
            }

            // BodyAngle rotates the whole crane body (carrier + outriggers + superstructure)
            // around the crane's own Z axis, then translates to world position.
            // This is used by the "Swap W ↔ L" button to physically orient the crane 90°.
            var transformGroup = new Transform3DGroup();
            if (_params.BodyAngle != 0)
            {
                transformGroup.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), _params.BodyAngle),
                    new Point3D(0, 0, 0)));
            }
            transformGroup.Children.Add(new TranslateTransform3D(
                _params.PositionX, _params.PositionY, _params.PositionZ));
            group.Transform = transformGroup;

            return group;
        }

        #region Outriggers

        private Model3DGroup BuildOutriggerPads()
        {
            var padsGroup = new Model3DGroup();
            double halfW = _params.OutriggerSpanM / 2.0;   // pad-to-pad side spread (X)
            double halfB = _params.SpanToSpanM    / 2.0;   // front-to-rear spread   (Y)
            double padSize = _params.OutriggerPadSize;
            double padThickness = 0.12;

            var positions = new[]
            {
                new Point3D( halfW,  halfB, padThickness / 2),
                new Point3D( halfW, -halfB, padThickness / 2),
                new Point3D(-halfW,  halfB, padThickness / 2),
                new Point3D(-halfW, -halfB, padThickness / 2)
            };

            foreach (var pos in positions)
            {
                padsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(pos, padSize, padSize, padThickness),
                    OrangePadMat));

                double bw = 0.08;
                padsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(
                        new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2 + 0.02),
                        padSize + bw, padSize + bw, 0.02),
                    BlackMat));
            }
            return padsGroup;
        }

        private Model3DGroup BuildOutriggerLegs()
        {
            var legsGroup = new Model3DGroup();
            double halfW = _params.OutriggerSpanM / 2.0;   // side spread (X)
            double halfB = _params.SpanToSpanM    / 2.0;   // front-rear spread (Y)
            // Use the same effective body width as BuildCarrier so legs align with the body
            double effectiveBodyWidth = _params.TravelWidthM > 0
                ? Math.Max(1.2, _params.TravelWidthM - 1.35)
                : _params.CarrierWidth;
            double carrierHalfWidth = effectiveBodyWidth / 2 + 0.3;
            double carrierHalfLength = _params.CarrierLength / 4;
            double legTopZ = _params.CarrierHeight + 0.2;
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

                legsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(carrier, beamEnd, 0.18, 8), YellowMat));

                legsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateBox(carrier, 0.6, 0.6, 0.5), YellowMat));

                legsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(beamEnd, pad, 0.15, 12), LightGrayMat));

                legsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z + 0.1),
                        new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z - 0.4),
                        0.22, 12),
                    YellowMat));

                legsGroup.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(
                        pad,
                        new Point3D(pad.X, pad.Y, pad.Z - 0.08),
                        0.35, 12),
                    DarkGrayMat));
            }
            return legsGroup;
        }

        #endregion

        #region Carrier

        private Model3DGroup BuildCarrier()
        {
            var g = new Model3DGroup();
            double length = _params.CarrierLength;
            double height = _params.CarrierHeight;

            // When TravelWidthM is set (> 0) the user wants the total visual carrier
            // width — body + tires on both sides — to equal that value.
            // Wheel protrusion per side: wheelWidth/2 + 0.1 (gap) + wheelWidth + 0.05 (hub) = 0.675 m
            // → bodyWidth = travelWidth − 2 × 0.675 = travelWidth − 1.35
            double width = _params.TravelWidthM > 0
                ? Math.Max(1.2, _params.TravelWidthM - 1.35)
                : _params.CarrierWidth;

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(0, 0, height / 2), length, width, height),
                DarkGrayMat));

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(0, width / 2 + 0.05, height / 2), length - 1, 0.1, height - 0.2),
                YellowMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(0, -width / 2 - 0.05, height / 2), length - 1, 0.1, height - 0.2),
                YellowMat));

            double wheelRadius = 0.45;
            double wheelWidth = 0.35;
            double wheelY = width / 2 + wheelWidth / 2 + 0.1;
            double[] wheelXs = { length / 2 - 1, length / 2 - 2.2, -length / 2 + 1.5, -length / 2 + 2.7 };

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

        #endregion

        #region Turntable & Superstructure

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

        #endregion

        #region Boom

        private Model3DGroup BuildBoom(double baseZ)
        {
            var g = new Model3DGroup();
            double boomAngleRad = _params.BoomAngle * Math.PI / 180;
            var pivot = new Point3D(0.5, 0, baseZ + 0.5);
            double boomEndX = pivot.X + _params.BoomLength * Math.Cos(boomAngleRad);
            double boomEndZ = pivot.Z + _params.BoomLength * Math.Sin(boomAngleRad);
            var boomEnd = new Point3D(boomEndX, 0, boomEndZ);

            int sections = _params.BoomSections;
            for (int i = 0; i < sections; i++)
            {
                double t1 = (double)i / sections;
                double t2 = (double)(i + 1) / sections;
                double avgWidth = _params.BoomBaseWidth * (1 - ((t1 + t2) / 2) * 0.6);

                var sStart = new Point3D(
                    pivot.X + t1 * _params.BoomLength * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t1 * _params.BoomLength * Math.Sin(boomAngleRad));
                var sEnd = new Point3D(
                    pivot.X + t2 * _params.BoomLength * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t2 * _params.BoomLength * Math.Sin(boomAngleRad));

                g.Children.Add(new GeometryModel3D(
                    MeshHelper.CreateCylinder(sStart, sEnd, avgWidth, 8), YellowMat));

                if (i > 0)
                {
                    g.Children.Add(new GeometryModel3D(
                        MeshHelper.CreateCylinder(
                            new Point3D(sStart.X - 0.1 * Math.Cos(boomAngleRad), 0, sStart.Z - 0.1 * Math.Sin(boomAngleRad)),
                            new Point3D(sStart.X + 0.1 * Math.Cos(boomAngleRad), 0, sStart.Z + 0.1 * Math.Sin(boomAngleRad)),
                            avgWidth + 0.05, 12),
                        LightGrayMat));
                }
            }

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(boomEnd, 0.6, 0.5, 0.8), YellowMat));

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.35, boomEnd.Z),
                    new Point3D(boomEnd.X, 0.35, boomEnd.Z),
                    0.25, 16),
                LightGrayMat));

            // Luffing cylinder
            var cylBase = new Point3D(pivot.X - 1.5, 0, baseZ - 0.3);
            double cylLen = _params.BoomLength * 0.35;
            double cylAngle = boomAngleRad + 0.3;
            var cylEnd = new Point3D(
                cylBase.X + cylLen * Math.Cos(cylAngle), 0,
                cylBase.Z + cylLen * Math.Sin(cylAngle));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(cylBase, cylEnd, 0.15, 12), LightGrayMat));
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    cylBase,
                    new Point3D(cylBase.X + 0.3, 0, cylBase.Z + 0.2),
                    0.2, 12),
                YellowMat));

            // Hoisting cable
            double hookZ = boomEnd.Z - _params.CableLength;
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, boomEnd.Z - 0.3),
                    new Point3D(boomEnd.X, 0, hookZ),
                    0.04, 6),
                BlackMat));

            // Hook block
            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateBox(new Point3D(boomEnd.X, 0, hookZ - 0.4), 0.7, 0.5, 1.0), RedMat));

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, -0.3, hookZ - 0.2),
                    new Point3D(boomEnd.X, 0.3, hookZ - 0.2),
                    0.18, 12),
                LightGrayMat));

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateCylinder(
                    new Point3D(boomEnd.X, 0, hookZ - 0.9),
                    new Point3D(boomEnd.X, 0, hookZ - 1.5),
                    0.1, 8),
                LightGrayMat));

            g.Children.Add(new GeometryModel3D(
                MeshHelper.CreateSphere(
                    new Point3D(boomEnd.X + 0.15, 0, hookZ - 1.6), 0.15, 8, 8),
                LightGrayMat));

            return g;
        }

        #endregion

        public void Dispose()
        {
            if (_params != null)
                _params.PropertyChanged -= OnParameterChanged;
        }
    }
}