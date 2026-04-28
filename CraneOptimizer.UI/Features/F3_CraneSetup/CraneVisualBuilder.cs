using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    /// <summary>
    /// Builds a true-to-scale 3D crane model using real manufacturer dimensions.
    /// Coordinate system: X = forward, Y = lateral, Z = up.
    /// All dimensions are in metres, read directly from the Crane entity.
    /// </summary>
    public class CraneVisualBuilder : IVisualBuilder<Crane>
    {
        // ── Realistic crane colour palette ──────────────────────────────────
        private static readonly Color ChassisColor      = Color.FromRgb(65, 65, 65);
        private static readonly Color TrackColor        = Color.FromRgb(45, 45, 45);
        private static readonly Color OutriggerBeamColor= Color.FromRgb(80, 80, 80);
        private static readonly Color OutriggerPadColor = Color.FromRgb(110, 110, 110);
        private static readonly Color SlewRingColor     = Color.FromRgb(90, 90, 90);
        private static readonly Color SuperColor        = Color.FromRgb(230, 175, 45);   // Industry yellow
        private static readonly Color CwColor           = Color.FromRgb(35, 35, 35);
        private static readonly Color CabGlassColor     = Color.FromRgb(155, 200, 235);
        private static readonly Color CabFrameColor     = Color.FromRgb(55, 55, 55);
        private static readonly Color BoomColor         = Color.FromRgb(245, 210, 50);
        private static readonly Color SheaveColor       = Color.FromRgb(100, 100, 100);
        private static readonly Color RopeColor         = Color.FromRgb(60, 60, 60);

        private const double BoomElevationDeg = 55.0;   // Resting boom angle

        private readonly Action<ModelVisual3D>? _onBuilt;

        public CraneVisualBuilder() { }
        public CraneVisualBuilder(Action<ModelVisual3D> onBuilt) { _onBuilt = onBuilt; }

        // ====================================================================
        // PUBLIC ENTRY POINT
        // ====================================================================
        public Visual3D Build(Crane crane)
        {
            var group = new Model3DGroup();
            var visual = new ModelVisual3D
            {
                Content = group,
                Transform = new TranslateTransform3D(0, 0, 0)
            };

            // ── Real dimensions with engineering fallbacks ──────────────────
            double bodyL = crane.VehicleLength > 0 ? crane.VehicleLength : 12.0;
            double bodyW = crane.VehicleWidth  > 0 ? crane.VehicleWidth  : 2.75;
            double bodyH = crane.VehicleHeight > 0 ? crane.VehicleHeight : 3.5;

            var spec = crane.Specifications.FirstOrDefault();

            // ── 1. CARRIER / UNDERCARRIAGE ──────────────────────────────────
            AddCarrier(group, crane.CraneType, bodyL, bodyW, bodyH);

            // ── 2. OUTRIGGERS ───────────────────────────────────────────────
            if (crane.OutriggerLength.HasValue && crane.OutriggerWidth.HasValue)
                AddOutriggers(group, bodyL, bodyW, bodyH,
                              crane.OutriggerLength.Value, crane.OutriggerWidth.Value);

            if (spec != null)
            {
                double ringH     = 0.3;
                double platZ     = bodyH;                        // top of chassis
                double superL    = bodyL * 0.42;                 // superstructure length
                double superW    = bodyW * 0.92;                 // superstructure width
                double superH    = bodyH * 0.50;                 // superstructure height
                double superTopZ = platZ + ringH + superH;       // top of superstructure

                // ── 3. SLEWING RING ─────────────────────────────────────────
                AddSlewRing(group, platZ, bodyW, ringH);

                // ── 4. SUPERSTRUCTURE (engine house) ────────────────────────
                AddSuperstructure(group, superL, superW, superH, platZ + ringH);

                // ── 5. COUNTERWEIGHT ────────────────────────────────────────
                double tailSwing = spec.MachineRotation ?? (superL * 0.85);
                AddCounterweight(group, spec.CounterWeight, tailSwing,
                                 superW, platZ + ringH, superH);

                // ── 6. OPERATOR CAB ─────────────────────────────────────────
                AddCab(group, superL, superW, platZ + ringH, superH);

                // ── 7. A-FRAME (GANTRY) ─────────────────────────────────────
                AddAFrame(group, superL, superW, superTopZ);

                // ── 8. BOOM ASSEMBLY ────────────────────────────────────────
                double pivotX = superL * 0.35;
                double pivotZ = superTopZ + 0.2;
                AddBoom(group, spec, pivotX, pivotZ);

                // ── 9. BOOM-TIP SHEAVE + HOOK ROPE ──────────────────────────
                AddHookRope(group, spec, pivotX, pivotZ);
            }

            _onBuilt?.Invoke(visual);
            return visual;
        }

        // ====================================================================
        // 1. CARRIER
        // ====================================================================
        private void AddCarrier(Model3DGroup g, CraneType type, double L, double W, double H)
        {
            if (type == CraneType.Crawler)
            {
                // Two crawler tracks
                double trackW  = W * 0.22;
                double trackH  = H * 0.35;
                double trackL  = L * 1.05;
                double spacing = (W - trackW) / 2.0;

                // Left track
                g.Children.Add(Box(0, -spacing, trackH / 2.0, trackL, trackW, trackH, TrackColor));
                // Right track
                g.Children.Add(Box(0,  spacing, trackH / 2.0, trackL, trackW, trackH, TrackColor));
                // Track pads / grousers (visual texture via thinner overlay)
                g.Children.Add(Box(0, -spacing, trackH + 0.05, trackL * 0.98, trackW * 0.85, 0.1, ChassisColor));
                g.Children.Add(Box(0,  spacing, trackH + 0.05, trackL * 0.98, trackW * 0.85, 0.1, ChassisColor));
                // Car body (sits between the tracks)
                double carbodyH = H - trackH;
                g.Children.Add(Box(0, 0, trackH + carbodyH / 2.0, L * 0.80, W * 0.55, carbodyH, ChassisColor));
            }
            else
            {
                // Mobile crane: solid chassis
                g.Children.Add(Box(0, 0, H / 2.0, L, W, H, ChassisColor));

                // Wheel axles (simplified as cylinders underneath)
                int axleCount = Math.Max(2, (int)(L / 3.0));
                double axleSpacing = (L * 0.75) / Math.Max(1, axleCount - 1);
                double axleStartX  = -L * 0.375;
                double wheelR      = H * 0.28;

                for (int i = 0; i < axleCount; i++)
                {
                    double ax = axleStartX + i * axleSpacing;
                    // Left wheel
                    g.Children.Add(Cyl(
                        new Vector3((float)ax, (float)(-W / 2.0 - 0.15), (float)wheelR),
                        new Vector3((float)ax, (float)(-W / 2.0 + 0.15), (float)wheelR),
                        (float)(wheelR * 2), TrackColor));
                    // Right wheel
                    g.Children.Add(Cyl(
                        new Vector3((float)ax, (float)(W / 2.0 - 0.15), (float)wheelR),
                        new Vector3((float)ax, (float)(W / 2.0 + 0.15), (float)wheelR),
                        (float)(wheelR * 2), TrackColor));
                }
            }
        }

        // ====================================================================
        // 2. OUTRIGGERS
        // ====================================================================
        private void AddOutriggers(Model3DGroup g, double bodyL, double bodyW, double bodyH,
                                   double oSpanL, double oSpanW)
        {
            double beamH  = 0.25;
            double beamW  = 0.20;
            double padSz  = Math.Max(oSpanL * 0.12, 0.6);
            double padH   = 0.15;
            double halfX   = oSpanW / 2.0;
            double halfY   = oSpanL / 2.0;
            double beamZ   = bodyH * 0.15;

            // Beam reaching from body corner to pad centre
            double[] cornersX = { -bodyL * 0.40, bodyL * 0.40, -bodyL * 0.40, bodyL * 0.40 };
            double[] padsX    = { -halfX,         halfX,        -halfX,         halfX        };
            double[] padsY    = { -halfY,        -halfY,         halfY,         halfY        };

            for (int i = 0; i < 4; i++)
            {
                double cx = (cornersX[i] + padsX[i]) / 2.0;
                double cy = padsY[i] / 2.0;
                double bLen = Math.Sqrt(Math.Pow(padsX[i] - cornersX[i], 2)
                                      + Math.Pow(padsY[i], 2));

                // Lateral outrigger beam
                g.Children.Add(Box(padsX[i] / 2.0, padsY[i] / 2.0, beamZ,
                                   Math.Abs(padsX[i]), beamW, beamH, OutriggerBeamColor));
                // Longitudinal stub (from body to lateral beam)
                g.Children.Add(Box(cornersX[i], padsY[i] / 2.0, beamZ,
                                   beamW, Math.Abs(padsY[i]), beamH, OutriggerBeamColor));
                // Pad
                g.Children.Add(Box(padsX[i], padsY[i], padH / 2.0,
                                   padSz, padSz, padH, OutriggerPadColor));
            }
        }

        // ====================================================================
        // 3. SLEWING RING
        // ====================================================================
        private void AddSlewRing(Model3DGroup g, double z, double bodyW, double ringH)
        {
            double dia = bodyW * 0.85;
            g.Children.Add(Cyl(
                new Vector3(0, 0, (float)z),
                new Vector3(0, 0, (float)(z + ringH)),
                (float)dia, SlewRingColor));
        }

        // ====================================================================
        // 4. SUPERSTRUCTURE
        // ====================================================================
        private void AddSuperstructure(Model3DGroup g, double sL, double sW, double sH, double baseZ)
        {
            // Engine house body — offset rearward
            double cx = -sL * 0.15;
            g.Children.Add(Box(cx, 0, baseZ + sH / 2.0, sL, sW, sH, SuperColor));

            // Engine cover / exhaust stack (small detail)
            g.Children.Add(Box(cx - sL * 0.25, 0, baseZ + sH + 0.15, sL * 0.3, sW * 0.4, 0.3, ChassisColor));
        }

        // ====================================================================
        // 5. COUNTERWEIGHT
        // ====================================================================
        private void AddCounterweight(Model3DGroup g, double cwTonnes, double tailSwing,
                                       double superW, double baseZ, double superH)
        {
            if (cwTonnes <= 0) cwTonnes = 10;

            // Derive block size from mass (assuming ~6 t/m³ steel/concrete mix)
            double vol   = cwTonnes / 6.0;
            double scale = Math.Pow(vol, 1.0 / 3.0);
            double cwW   = Math.Min(scale * 1.6, superW * 0.95);
            double cwD   = scale * 0.85;
            double cwH   = scale * 0.9;

            double cx = -tailSwing + cwD / 2.0;
            double cz = baseZ + superH - cwH * 0.1;  // sits flush on super top

            g.Children.Add(Box(cx, 0, cz + cwH / 2.0, cwD, cwW, cwH, CwColor));
            // Stack line detail
            g.Children.Add(Box(cx, 0, cz + cwH + 0.04, cwD * 0.95, cwW * 0.95, 0.08, SlewRingColor));
        }

        // ====================================================================
        // 6. OPERATOR CAB
        // ====================================================================
        private void AddCab(Model3DGroup g, double superL, double superW, double baseZ, double superH)
        {
            double cabW = Math.Min(1.2, superW * 0.35);
            double cabL = Math.Min(1.8, superL * 0.40);
            double cabH = Math.Min(2.0, superH * 1.1);
            double cabX = superL * 0.25;
            double cabY = superW / 2.0 - cabW / 2.0 - 0.05;

            // Cab frame
            g.Children.Add(Box(cabX, cabY, baseZ + superH + cabH / 2.0,
                               cabL, cabW, cabH, CabFrameColor));
            // Glass windows (inset panel on each side)
            double glassInset = 0.06;
            g.Children.Add(Box(cabX, cabY, baseZ + superH + cabH * 0.55,
                               cabL - glassInset * 2, cabW - glassInset * 2,
                               cabH * 0.6, CabGlassColor));
        }

        // ====================================================================
        // 7. A-FRAME / GANTRY
        // ====================================================================
        private void AddAFrame(Model3DGroup g, double superL, double superW, double topZ)
        {
            double legW = 0.15;
            double gantryH = superL * 0.45;
            double footSpread = superW * 0.35;
            double topSpread  = 0.15;

            // Left leg
            g.Children.Add(TiltedStrut(
                -superL * 0.20, -footSpread, topZ,
                -superL * 0.20, -topSpread,  topZ + gantryH,
                legW, SlewRingColor));
            // Right leg
            g.Children.Add(TiltedStrut(
                -superL * 0.20,  footSpread, topZ,
                -superL * 0.20,  topSpread,  topZ + gantryH,
                legW, SlewRingColor));
            // Top cross-bar
            g.Children.Add(Box(-superL * 0.20, 0, topZ + gantryH,
                               legW, topSpread * 2.5, legW, SlewRingColor));
        }

        // ====================================================================
        // 8. BOOM
        // ====================================================================
        private void AddBoom(Model3DGroup g, CraneSpecification spec,
                             double pivotX, double pivotZ)
        {
            double boomLen = spec.MaxBoomLength > 0 ? spec.MaxBoomLength : 30;
            double minBoom = spec.MinBoomLength > 0 ? spec.MinBoomLength : boomLen * 0.35;

            // Three telescoping sections with decreasing cross-section
            int sections = 3;
            double[] lengths = new double[sections];
            double[] widths  = new double[sections];
            double[] heights = new double[sections];

            lengths[0] = minBoom;
            lengths[1] = (boomLen - minBoom) * 0.5;
            lengths[2] = (boomLen - minBoom) * 0.5;
            if (lengths[1] <= 0) { lengths[1] = 0; lengths[2] = 0; sections = 1; }

            // Cross-sections taper from base to tip
            double baseCSW = Math.Max(1.0, Math.Min(1.4, boomLen * 0.022));
            double baseCSH = baseCSW * 0.85;
            widths[0]  = baseCSW;        heights[0] = baseCSH;
            widths[1]  = baseCSW * 0.72; heights[1] = baseCSH * 0.72;
            widths[2]  = baseCSW * 0.48; heights[2] = baseCSH * 0.48;

            var boomTransform = new Transform3DGroup();
            boomTransform.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, -1, 0), BoomElevationDeg)));
            boomTransform.Children.Add(new TranslateTransform3D(pivotX, 0, pivotZ));

            // Build boom foot pivot cylinder
            var pivot = Cyl(
                new Vector3(-0.3f, 0, 0), new Vector3(0.3f, 0, 0),
                (float)(baseCSH * 1.1), SlewRingColor);
            pivot.Transform = boomTransform;
            g.Children.Add(pivot);

            double runningX = 0;
            for (int i = 0; i < sections; i++)
            {
                if (lengths[i] <= 0) continue;
                double cx = runningX + lengths[i] / 2.0;

                // Main section shell
                var section = Box(cx, 0, 0, lengths[i], widths[i], heights[i], BoomColor);
                section.Transform = boomTransform;
                g.Children.Add(section);

                // Inner cutout to suggest hollow lattice/telescopic structure
                if (lengths[i] > 2)
                {
                    var inner = Box(cx, 0, 0,
                                   lengths[i] - 0.4, widths[i] * 0.6, heights[i] * 0.6,
                                   Color.FromRgb(200, 170, 40));
                    inner.Transform = boomTransform;
                    g.Children.Add(inner);
                }

                runningX += lengths[i];
            }

            // Boom-tip sheave block
            var sheave = Box(runningX, 0, 0, 0.5, widths[0] * 0.6, heights[0] * 0.7, SheaveColor);
            sheave.Transform = boomTransform;
            g.Children.Add(sheave);
        }

        // ====================================================================
        // 9. HOOK ROPE
        // ====================================================================
        private void AddHookRope(Model3DGroup g, CraneSpecification spec,
                                  double pivotX, double pivotZ)
        {
            double boomLen = spec.MaxBoomLength > 0 ? spec.MaxBoomLength : 30;
            double angleRad = BoomElevationDeg * Math.PI / 180.0;

            // Boom-tip position in world coords
            double tipX = pivotX + boomLen * Math.Cos(angleRad);
            double tipZ = pivotZ + boomLen * Math.Sin(angleRad);

            // Rope hangs straight down from tip to just above ground
            double ropeBottom = 1.0;
            g.Children.Add(Cyl(
                new Vector3((float)tipX, 0, (float)tipZ),
                new Vector3((float)tipX, 0, (float)ropeBottom),
                0.06f, RopeColor));

            // Hook block
            g.Children.Add(Box(tipX, 0, ropeBottom - 0.3, 0.4, 0.3, 0.6, SheaveColor));
        }

        // ====================================================================
        // PRIMITIVE HELPERS
        // ====================================================================
        private GeometryModel3D Box(double cx, double cy, double cz,
                                    double sx, double sy, double sz, Color color)
        {
            var mb = new MeshBuilder(true, true);
            mb.AddBox(new Vector3((float)cx, (float)cy, (float)cz),
                      (float)sx, (float)sy, (float)sz);
            return ToModel(mb, color);
        }

        private GeometryModel3D Cyl(Vector3 p1, Vector3 p2, float diameter, Color color)
        {
            var mb = new MeshBuilder(true, true);
            mb.AddCylinder(p1, p2, diameter, 24);
            return ToModel(mb, color);
        }

        /// <summary>
        /// Builds a thin strut between two 3D points (used for A-frame legs).
        /// </summary>
        private GeometryModel3D TiltedStrut(
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            double thickness, Color color)
        {
            var mb = new MeshBuilder(true, true);
            mb.AddCylinder(
                new Vector3((float)x1, (float)y1, (float)z1),
                new Vector3((float)x2, (float)y2, (float)z2),
                (float)thickness, 8);
            return ToModel(mb, color);
        }

        private GeometryModel3D ToModel(MeshBuilder mb, Color color)
        {
            var mesh = mb.ToMesh();
            var wpf = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions       = new Point3DCollection(mesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(mesh.TriangleIndices),
                Normals         = new Vector3DCollection(mesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };
            var mat = new DiffuseMaterial(new SolidColorBrush(color));
            return new GeometryModel3D { Geometry = wpf, Material = mat, BackMaterial = mat };
        }
    }
}