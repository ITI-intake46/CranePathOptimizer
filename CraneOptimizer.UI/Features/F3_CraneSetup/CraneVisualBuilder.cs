using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System;
using System.Linq;
using CraneOptimizer.UI.Core.Models;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    public class CraneVisualBuilder : IVisualBuilder<SceneCrane>
    {
        private static readonly Material YellowMat = MaterialHelper.CreateMaterial(Color.FromRgb(255, 190, 0));
        private static readonly Material OrangePadMat = MaterialHelper.CreateMaterial(Color.FromRgb(214, 138, 86));
        private static readonly Material DarkGrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(50, 50, 50));
        private static readonly Material LightGrayMat = MaterialHelper.CreateMaterial(Color.FromRgb(130, 130, 130));
        private static readonly Material RedMat = MaterialHelper.CreateMaterial(Color.FromRgb(200, 50, 50));
        private static readonly Material WhiteStripeMat = MaterialHelper.CreateMaterial(Color.FromRgb(240, 240, 240));
        private static readonly Material BlackMat = MaterialHelper.CreateMaterial(Color.FromRgb(25, 25, 25));
        private static readonly Material WindowMat = MaterialHelper.CreateMaterial(Color.FromRgb(80, 120, 160));
        private static readonly Material ExclusionMat = MaterialHelper.CreateMaterial(Color.FromArgb(100, 214, 138, 86));

        private readonly Action<ModelVisual3D>? _onBuilt;

        public CraneVisualBuilder() { }
        public CraneVisualBuilder(Action<ModelVisual3D> onBuilt) { _onBuilt = onBuilt; }

        public Visual3D Build(Crane crane)
        {
            var group = new Model3DGroup();
            var visual = new ModelVisual3D
            {
                Content = group,
                Transform = new TranslateTransform3D(0, 0, 0)
            };

            // Setup default specs if missing
            var spec = crane.Specifications?.FirstOrDefault();
            if (spec == null)
            {
                spec = new CraneSpecification
                {
                    MaxBoomLength = 40.0,
                    MinBoomLength = 15.0,
                    MachineRotation = (crane.VehicleLength > 0 ? crane.VehicleLength : 12.0) * 0.45,
                    CounterWeight = 20.0
                };
            }

            double length = crane.VehicleLength > 0 ? crane.VehicleLength : 12.0;
            double width = crane.VehicleWidth > 0 ? crane.VehicleWidth : 2.75;
            double height = crane.VehicleHeight > 0 ? crane.VehicleHeight : 2.0;
            
            double outriggerSpanL = crane.OutriggerLength ?? 8.0;
            double outriggerSpanW = crane.OutriggerWidth ?? 8.0;

            group.Children.Add(BuildOutriggerPads(outriggerSpanL, outriggerSpanW));
            group.Children.Add(BuildOutriggerLegs(length, width, height, outriggerSpanL, outriggerSpanW));
            group.Children.Add(BuildCarrier(length, width, height));

            double turntableZ = height;
            group.Children.Add(BuildTurntable(turntableZ));

            var superstructure = BuildSuperstructure(turntableZ, spec);
            // Rotate the superstructure if needed. Here we default to 0.
            superstructure.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0),
                new Point3D(0, 0, 0));
            group.Children.Add(superstructure);

            _onBuilt?.Invoke(visual);
            return visual;
        }

        private Model3DGroup BuildOutriggerPads(double spanL, double spanW)
        {
            var padsGroup = new Model3DGroup();
            double halfW = spanW / 2.0;
            double halfB = spanL / 2.0;
            double padSize = Math.Max(spanL * 0.1, 0.6);
            double padThickness = 0.12;

            var positions = new[]
            {
                new Point3D( halfB,  halfW, padThickness / 2),
                new Point3D( halfB, -halfW, padThickness / 2),
                new Point3D(-halfB,  halfW, padThickness / 2),
                new Point3D(-halfB, -halfW, padThickness / 2)
            };

            foreach (var pos in positions)
            {
                padsGroup.Children.Add(new GeometryModel3D(CreateBox(pos, padSize, padSize, padThickness), OrangePadMat));

                double bw = 0.08;
                padsGroup.Children.Add(new GeometryModel3D(
                    CreateBox(new Point3D(pos.X, pos.Y, pos.Z + padThickness / 2 + 0.02), padSize + bw, padSize + bw, 0.02),
                    BlackMat));
            }
            return padsGroup;
        }

        private Model3DGroup BuildOutriggerLegs(double bodyL, double bodyW, double bodyH, double spanL, double spanW)
        {
            var legsGroup = new Model3DGroup();
            double halfW = spanW / 2.0;
            double halfB = spanL / 2.0;
            
            double carrierHalfWidth = bodyW / 2 + 0.3;
            double carrierHalfLength = bodyL / 4;
            double legTopZ = bodyH + 0.2;
            double legBottomZ = 0.2;

            var data = new[]
            {
                (c: new Point3D( carrierHalfLength,  carrierHalfWidth, legTopZ), p: new Point3D( halfB,  halfW, legBottomZ)),
                (c: new Point3D( carrierHalfLength, -carrierHalfWidth, legTopZ), p: new Point3D( halfB, -halfW, legBottomZ)),
                (c: new Point3D(-carrierHalfLength,  carrierHalfWidth, legTopZ), p: new Point3D(-halfB,  halfW, legBottomZ)),
                (c: new Point3D(-carrierHalfLength, -carrierHalfWidth, legTopZ), p: new Point3D(-halfB, -halfW, legBottomZ))
            };

            foreach (var (carrier, pad) in data)
            {
                var beamEnd = new Point3D(pad.X, pad.Y, carrier.Z);

                legsGroup.Children.Add(new GeometryModel3D(CreateCylinder(carrier, beamEnd, 0.18, 8), YellowMat));
                legsGroup.Children.Add(new GeometryModel3D(CreateBox(carrier, 0.6, 0.6, 0.5), YellowMat));
                legsGroup.Children.Add(new GeometryModel3D(CreateCylinder(beamEnd, pad, 0.15, 12), LightGrayMat));
                
                legsGroup.Children.Add(new GeometryModel3D(
                    CreateCylinder(new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z + 0.1), new Point3D(beamEnd.X, beamEnd.Y, beamEnd.Z - 0.4), 0.22, 12),
                    YellowMat));
                
                legsGroup.Children.Add(new GeometryModel3D(
                    CreateCylinder(pad, new Point3D(pad.X, pad.Y, pad.Z - 0.08), 0.35, 12),
                    DarkGrayMat));
            }
            return legsGroup;
        }

        private Model3DGroup BuildCarrier(double length, double width, double height)
        {
            var g = new Model3DGroup();

            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(0, 0, height / 2), length, width, height), YellowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(0, 0, height + 0.15), length * 0.6, width * 0.8, 0.3), YellowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(length / 2 - 0.5, 0, height + 0.25), 1.5, width * 0.6, 0.5), YellowMat));

            for (int side = -1; side <= 1; side += 2)
            {
                g.Children.Add(new GeometryModel3D(
                    CreateBox(new Point3D(0, side * (width / 2 + 0.05), height * 0.6), length - 1, 0.1, height - 0.2),
                    WhiteStripeMat));
            }

            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(0, width / 2 + 0.05, height / 2), length - 1, 0.1, height - 0.2), YellowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(0, -width / 2 - 0.05, height / 2), length - 1, 0.1, height - 0.2), YellowMat));

            double wheelRadius = 0.45;
            double wheelWidth = 0.35;
            double wheelY = width / 2 + wheelWidth / 2 + 0.1;
            double[] wheelXs = { length / 2 - 1, length / 2 - 2.2, -length / 2 + 1.5, -length / 2 + 2.7 };

            foreach (double wx in wheelXs)
            {
                g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(wx, wheelY, wheelRadius), new Point3D(wx, wheelY + wheelWidth, wheelRadius), wheelRadius, 16), BlackMat));
                g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(wx, -wheelY, wheelRadius), new Point3D(wx, -wheelY - wheelWidth, wheelRadius), wheelRadius, 16), BlackMat));

                g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(wx, wheelY + wheelWidth, wheelRadius), new Point3D(wx, wheelY + wheelWidth + 0.05, wheelRadius), wheelRadius * 0.4, 12), LightGrayMat));
                g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(wx, -wheelY - wheelWidth, wheelRadius), new Point3D(wx, -wheelY - wheelWidth - 0.05, wheelRadius), wheelRadius * 0.4, 12), LightGrayMat));
            }
            return g;
        }

        private Model3DGroup BuildTurntable(double baseZ)
        {
            var g = new Model3DGroup();
            g.Children.Add(new GeometryModel3D(
                CreatePipe(new Point3D(0, 0, baseZ), new Point3D(0, 0, baseZ + 0.3), 1.2, 1.6, 24),
                DarkGrayMat));
            return g;
        }

        private Model3DGroup BuildSuperstructure(double baseZ, CraneSpecification spec)
        {
            var g = new Model3DGroup();
            double superZ = baseZ + 0.3;

            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-0.5, 0, superZ + 0.5), 3, 2.5, 1), YellowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-1.5, 0, superZ + 1.6), 2.2, 2.2, 2.2), YellowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-0.35, 0, superZ + 1.8), 0.08, 1.4, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-1.5, 1.15, superZ + 1.8), 1.2, 0.08, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-1.5, -1.15, superZ + 1.8), 1.2, 0.08, 1.0), WindowMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-2.8, 0, superZ + 0.8), 2, 2.2, 1.4), DarkGrayMat));
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(-2.5, 0, superZ + 2), 1.5, 1.8, 1), YellowMat));

            g.Children.Add(BuildBoom(superZ + 1, spec));
            return g;
        }

        private Model3DGroup BuildBoom(double baseZ, CraneSpecification spec)
        {
            var g = new Model3DGroup();
            double boomAngleRad = 55.0 * Math.PI / 180; // Hardcoded angle for visual representation
            var pivot = new Point3D(0.5, 0, baseZ + 0.5);
            
            double boomLen = spec.MaxBoomLength > 0 ? spec.MaxBoomLength : 30.0;
            double boomEndX = pivot.X + boomLen * Math.Cos(boomAngleRad);
            double boomEndZ = pivot.Z + boomLen * Math.Sin(boomAngleRad);
            var boomEnd = new Point3D(boomEndX, 0, boomEndZ);

            int sections = 4;
            double baseWidth = 1.2;

            for (int i = 0; i < sections; i++)
            {
                double t1 = (double)i / sections;
                double t2 = (double)(i + 1) / sections;
                double avgWidth = baseWidth * (1 - ((t1 + t2) / 2) * 0.6);

                var sStart = new Point3D(
                    pivot.X + t1 * boomLen * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t1 * boomLen * Math.Sin(boomAngleRad));
                var sEnd = new Point3D(
                    pivot.X + t2 * boomLen * Math.Cos(boomAngleRad), 0,
                    pivot.Z + t2 * boomLen * Math.Sin(boomAngleRad));

                g.Children.Add(new GeometryModel3D(CreateCylinder(sStart, sEnd, avgWidth, 8), YellowMat));

                if (i > 0)
                {
                    g.Children.Add(new GeometryModel3D(
                        CreateCylinder(
                            new Point3D(sStart.X - 0.1 * Math.Cos(boomAngleRad), 0, sStart.Z - 0.1 * Math.Sin(boomAngleRad)),
                            new Point3D(sStart.X + 0.1 * Math.Cos(boomAngleRad), 0, sStart.Z + 0.1 * Math.Sin(boomAngleRad)),
                            avgWidth + 0.05, 12),
                        LightGrayMat));
                }
            }

            // Boom Tip
            g.Children.Add(new GeometryModel3D(CreateBox(boomEnd, 0.6, 0.5, 0.8), YellowMat));

            g.Children.Add(new GeometryModel3D(
                CreateCylinder(new Point3D(boomEnd.X, -0.35, boomEnd.Z), new Point3D(boomEnd.X, 0.35, boomEnd.Z), 0.25, 16),
                LightGrayMat));

            // Luffing Cylinder
            var cylBase = new Point3D(pivot.X - 1.5, 0, baseZ - 0.3);
            double cylLen = boomLen * 0.35;
            double cylAngle = boomAngleRad + 0.3;
            var cylEnd = new Point3D(
                cylBase.X + cylLen * Math.Cos(cylAngle), 0,
                cylBase.Z + cylLen * Math.Sin(cylAngle));
                
            g.Children.Add(new GeometryModel3D(CreateCylinder(cylBase, cylEnd, 0.15, 12), LightGrayMat));
            g.Children.Add(new GeometryModel3D(CreateCylinder(cylBase, new Point3D(cylBase.X + 0.3, 0, cylBase.Z + 0.2), 0.2, 12), YellowMat));

            // Hook Rope
            double hookZ = boomEnd.Z - boomLen * 0.8;
            if (hookZ < 0) hookZ = 1.0;

            g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(boomEnd.X, 0, boomEnd.Z - 0.3), new Point3D(boomEnd.X, 0, hookZ), 0.04, 6), BlackMat));
            
            // Hook Block
            g.Children.Add(new GeometryModel3D(CreateBox(new Point3D(boomEnd.X, 0, hookZ - 0.4), 0.7, 0.5, 1.0), RedMat));
            
            g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(boomEnd.X, -0.3, hookZ - 0.2), new Point3D(boomEnd.X, 0.3, hookZ - 0.2), 0.18, 12), LightGrayMat));
            g.Children.Add(new GeometryModel3D(CreateCylinder(new Point3D(boomEnd.X, 0, hookZ - 0.9), new Point3D(boomEnd.X, 0, hookZ - 1.5), 0.1, 8), LightGrayMat));

            // Small hook sphere
            var mbSphere = new HelixToolkit.Geometry.MeshBuilder(false, false);
            mbSphere.AddSphere(new System.Numerics.Vector3((float)(boomEnd.X + 0.15), 0, (float)(hookZ - 1.6)), 0.15f, 8, 8);
            var helixSphere = mbSphere.ToMesh();
            var wpfSphere = new MeshGeometry3D
            {
                Positions = new Point3DCollection(helixSphere.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixSphere.TriangleIndices)
            };
            g.Children.Add(new GeometryModel3D(wpfSphere, LightGrayMat));

            return g;
        }

        // ====================================================================
        // PRIMITIVE HELPERS
        // ====================================================================

        private MeshGeometry3D CreateBox(Point3D center, double xLength, double yLength, double zLength)
        {
            var mb = new HelixToolkit.Geometry.MeshBuilder(false, false);
            mb.AddBox(new System.Numerics.Vector3((float)center.X, (float)center.Y, (float)center.Z), (float)xLength, (float)yLength, (float)zLength);
            var helixMesh = mb.ToMesh();
            return new MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };
        }

        private MeshGeometry3D CreateCylinder(Point3D p1, Point3D p2, double diameter, int thetaDiv)
        {
            var mb = new HelixToolkit.Geometry.MeshBuilder(false, false);
            mb.AddCylinder(
                new System.Numerics.Vector3((float)p1.X, (float)p1.Y, (float)p1.Z), 
                new System.Numerics.Vector3((float)p2.X, (float)p2.Y, (float)p2.Z), 
                (float)diameter, thetaDiv);
            var helixMesh = mb.ToMesh();
            return new MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };
        }

        private MeshGeometry3D CreatePipe(Point3D p1, Point3D p2, double innerDiameter, double outerDiameter, int thetaDiv)
        {
            var mb = new HelixToolkit.Geometry.MeshBuilder(false, false);
            mb.AddCylinder(
                new System.Numerics.Vector3((float)p1.X, (float)p1.Y, (float)p1.Z),
                new System.Numerics.Vector3((float)p2.X, (float)p2.Y, (float)p2.Z),
                (float)outerDiameter, thetaDiv);
            var helixMesh = mb.ToMesh();
            return new MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };
        }
        public Visual3D Build(SceneCrane item)
        {
            var group = new ModelVisual3D();

            var vehicleLength = item.CraneDefinition.VehicleLength > 0 ? item.CraneDefinition.VehicleLength : 12.0;
            var vehicleWidth = item.CraneDefinition.VehicleWidth > 0 ? item.CraneDefinition.VehicleWidth : 2.75;
            var vehicleHeight = item.CraneDefinition.VehicleHeight > 0 ? item.CraneDefinition.VehicleHeight : 2.0;

            var centerX = item.Position.X;
            var centerY = item.Position.Y;
            var baseZ = item.Position.Z;

            var bodyColor = item.IsValidPlacement ? Color.FromRgb(255, 215, 0) : Color.FromRgb(220, 53, 69);

            var truck = new BoxVisual3D
            {
                Width = vehicleWidth,
                Length = vehicleLength,
                Height = vehicleHeight,
                Fill = new SolidColorBrush(bodyColor),
                Center = new Point3D(centerX, centerY, baseZ + vehicleHeight / 2.0)
            };

            truck.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), item.Rotation),
                new Point3D(centerX, centerY, baseZ));

            var pivot = item.GetBoomPivotWorld();
            var tip = item.GetBoomTipWorld();

            var boom = new PipeVisual3D
            {
                Point1 = new Point3D(pivot.X, pivot.Y, pivot.Z),
                Point2 = new Point3D(tip.X, tip.Y, tip.Z),
                Diameter = 0.25,
                Fill = Brushes.Goldenrod
            };

            var pivotMarker = new SphereVisual3D
            {
                Center = new Point3D(pivot.X, pivot.Y, pivot.Z),
                Radius = 0.2,
                Fill = Brushes.OrangeRed
            };

            truck.SetValue(System.Windows.FrameworkElement.TagProperty, item);
            boom.SetValue(System.Windows.FrameworkElement.TagProperty, item);
            pivotMarker.SetValue(System.Windows.FrameworkElement.TagProperty, item);

            group.Children.Add(truck);
            group.Children.Add(boom);
            group.Children.Add(pivotMarker);
            group.SetValue(System.Windows.FrameworkElement.TagProperty, item);

            _onBuilt?.Invoke(group);

            return group;
        }
    }
}