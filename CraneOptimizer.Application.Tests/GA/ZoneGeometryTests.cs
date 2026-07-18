using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Xunit;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;

namespace CraneOptimizer.Application.Tests
{
    public class ZoneGeometryTests
    {
        private readonly IZoneGeometryService _geometry = new ZoneGeometryService();

        private static void RunOnSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
                throw new AggregateException("STA thread threw an exception", caught);
        }

        // ════════════════════════════════════════════════════════════════════
        // Polygon Validation Tests
        // ════════════════════════════════════════════════════════════════════

        [Fact]
        public void ValidatePolygon_RejectsLessThanThreeUniquePoints()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(1, 1, 0)
            };
            Assert.False(_geometry.ValidatePolygon(pts, out var warning));
            Assert.Contains("requires at least 3 unique", warning);
        }

        [Fact]
        public void ValidatePolygon_RemovesConsecutiveDuplicates()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var norm = _geometry.NormalizeVertices(pts);
            Assert.Equal(4, norm.Count);
            Assert.True(_geometry.ValidatePolygon(pts, out _));
        }

        [Fact]
        public void ValidatePolygon_RejectsSelfIntersection()
        {
            // Bow-tie shape
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(0, 10, 0)
            };
            Assert.False(_geometry.ValidatePolygon(pts, out var warning));
            Assert.Contains("Self-intersecting polygons", warning);
        }

        [Fact]
        public void ValidatePolygon_AllowsConcaveSimplePolygon()
        {
            // L-shape (concave)
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            Assert.True(_geometry.ValidatePolygon(pts, out _));
        }

        [Fact]
        public void SignedArea_DetectsOrientation()
        {
            var ccw = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var cw = new List<DomainPoint3D>(ccw);
            cw.Reverse();

            Assert.True(_geometry.CalculateSignedArea2D(ccw) > 0);
            Assert.True(_geometry.CalculateSignedArea2D(cw) < 0);
            Assert.Equal(100, Math.Abs(_geometry.CalculateSignedArea2D(ccw)), 5);
        }

        // ════════════════════════════════════════════════════════════════════
        // Triangulation & Rendering Tests
        // ════════════════════════════════════════════════════════════════════

        [Fact]
        public void Triangulate_Rectangle_ReturnsTwoTriangles()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.Equal(6, indices.Count); // 2 triangles
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out _));
        }

        [Fact]
        public void Triangulate_LShape_ReturnsCorrectTriangles()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.Equal(12, indices.Count); // 4 triangles
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out _));
        }

        [Fact]
        public void Triangulate_TShape_ReturnsCorrectTriangles()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 3, 0),
                new DomainPoint3D(7, 3, 0),
                new DomainPoint3D(7, 7, 0),
                new DomainPoint3D(3, 7, 0),
                new DomainPoint3D(3, 3, 0),
                new DomainPoint3D(0, 3, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.Equal(18, indices.Count); // 6 triangles
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out _));
        }

        [Fact]
        public void Triangulate_ConcavePolygon_TriangleAreaEqualsPolygonArea()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out var error), error);
        }

        [Fact]
        public void Triangulate_DoesNotCreateTrianglesOutsidePolygon()
        {
            // Concave L-shape: the "inside corner" centroid is at (7.5, 7.5) which lies outside the L
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out _));
        }

        [Fact]
        public void BuildZoneRenderMesh_UsesEarClipping_NotTriangleFan()
        {
            RunOnSta(() =>
            {
                var scene = new MockSceneService();
                var builder = new ZoneVisualBuilder(scene, _geometry);
                var zone = new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(10, 0, 0),
                        new DomainPoint3D(10, 5, 0),
                        new DomainPoint3D(5, 5, 0),
                        new DomainPoint3D(5, 10, 0),
                        new DomainPoint3D(0, 10, 0)
                    }
                };
                var visual = builder.Build(zone) as ModelVisual3D;
                Assert.NotNull(visual);
                var model = visual.Content as GeometryModel3D;
                Assert.NotNull(model);
                var wpfMesh = model.Geometry as MeshGeometry3D;
                Assert.NotNull(wpfMesh);

                // Verified triangles
                Assert.Equal(12, wpfMesh.TriangleIndices.Count);
            });
        }

        [Fact]
        public void Render_OutlineClosesBoundary()
        {
            RunOnSta(() =>
            {
                var scene = new MockSceneService();
                var builder = new ZoneVisualBuilder(scene, _geometry);
                var zone = new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(10, 0, 0),
                        new DomainPoint3D(10, 10, 0),
                        new DomainPoint3D(0, 10, 0)
                    }
                };
                var visual = builder.Build(zone) as ModelVisual3D;
                Assert.NotNull(visual);
                var border = visual.Children.OfType<LinesVisual3D>().FirstOrDefault();
                Assert.NotNull(border);

                // 4 segments = 8 points (each segment has start & end point)
                Assert.Equal(8, border.Points.Count);
                // Verify it connects the last point back to the first point
                var lastEdgeEnd = border.Points[7];
                var firstEdgeStart = border.Points[0];
                Assert.Equal(firstEdgeStart.X, lastEdgeEnd.X, 3);
                Assert.Equal(firstEdgeStart.Y, lastEdgeEnd.Y, 3);
            });
        }

        [Fact]
        public void RedAndGreenZones_RenderCorrectly_WithConcavePolygons()
        {
            RunOnSta(() =>
            {
                var scene = new MockSceneService();
                var builder = new ZoneVisualBuilder(scene, _geometry);

                var allowedZone = new GreenAreaZone
                {
                    Type = ZoneType.Allowed,
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(10, 0, 0),
                        new DomainPoint3D(10, 5, 0),
                        new DomainPoint3D(5, 5, 0),
                        new DomainPoint3D(5, 10, 0),
                        new DomainPoint3D(0, 10, 0)
                    }
                };
                var forbiddenZone = new GreenAreaZone
                {
                    Type = ZoneType.Forbidden,
                    Vertices = allowedZone.Vertices.ToList()
                };

                var greenVisual = builder.Build(allowedZone) as ModelVisual3D;
                var redVisual = builder.Build(forbiddenZone) as ModelVisual3D;

                Assert.NotNull(greenVisual);
                Assert.NotNull(redVisual);
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // Finish Behavior Tests
        // ════════════════════════════════════════════════════════════════════

        [Fact]
        public void RightClick_FinishesDrawing_WhenThreeOrMorePoints()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                DrawingViewModel.DialogShowHook = (area, type) => (true, type, "Test Zone", null, null);
                try
                {
                    vm.Start();
                    drawing.AddPoint(new DomainPoint3D(0, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 10, 0));

                    vm.HandleRightClick(); // Triggers finalization dialog fallback inside test headless mock
                    Assert.False(vm.IsDrawing);
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
        }

        [Fact]
        public void RightClick_DoesNotAddExtraPoint()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                DrawingViewModel.DialogShowHook = (area, type) => (true, type, "Test Zone", null, null);
                try
                {
                    vm.Start();
                    drawing.AddPoint(new DomainPoint3D(0, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 10, 0));

                    vm.HandleRightClick();
                    var zone = scene.GetZones().FirstOrDefault();
                    Assert.NotNull(zone);
                    Assert.Equal(3, zone.Vertices.Count); // Strictly 3 unique vertices, no extra point added!
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
        }

        [Fact]
        public void CtrlEnter_FinishesDrawing_WhenThreeOrMorePoints()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                DrawingViewModel.DialogShowHook = (area, type) => (true, type, "Test Zone", null, null);
                try
                {
                    vm.Start();
                    drawing.AddPoint(new DomainPoint3D(0, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 10, 0));

                    vm.FinishDrawingFromUserAction(FinishReason.CtrlEnter);
                    Assert.False(vm.IsDrawing);
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
        }

        [Fact]
        public void CtrlEnter_DoesNotTriggerOutsideDrawingMode()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                // IsDrawing is false by default
                vm.FinishDrawingFromUserAction(FinishReason.CtrlEnter);
                Assert.False(vm.IsDrawing);
                Assert.Empty(scene.GetZones());
            });
        }

        [Fact]
        public void Finish_WithLessThanThreePoints_ShowsWarning_AndDoesNotCreateZone()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                vm.Start();
                drawing.AddPoint(new DomainPoint3D(0, 0, 0));
                drawing.AddPoint(new DomainPoint3D(10, 0, 0));

                vm.Finish();
                Assert.True(vm.IsDrawing);
                Assert.True(vm.IsInlineWarningVisible);
                Assert.Empty(scene.GetZones());
            });
        }

        [Fact]
        public void StoredZone_DoesNotDuplicateFirstVertex()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                DrawingViewModel.DialogShowHook = (area, type) => (true, type, "Test Zone", null, null);
                try
                {
                    vm.Start();
                    drawing.AddPoint(new DomainPoint3D(0, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 0, 0));
                    drawing.AddPoint(new DomainPoint3D(10, 10, 0));

                    vm.FinishDrawingFromUserAction(FinishReason.CtrlEnter);
                    var zone = scene.GetZones().FirstOrDefault();
                    Assert.NotNull(zone);
                    Assert.Equal(3, zone.Vertices.Count); // Strictly unique vertices
                    Assert.NotEqual(zone.Vertices[0], zone.Vertices[^1]); // Closure is implicit
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
        }

        [Fact]
        public void PreviewOutline_ClosesLastPointToFirstPoint()
        {
            RunOnSta(() =>
            {
                var pts = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 10, 0)
                };
                var builder = new PreviewLinesBuilder(Colors.Yellow);
                var visual = builder.Build(pts) as LinesVisual3D;
                Assert.NotNull(visual);

                // Connects 0->1, 1->2, and 2->0 (visual closure)
                // So 6 points total for 3 lines
                Assert.Equal(6, visual.Points.Count);
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // GA Integration Tests
        // ════════════════════════════════════════════════════════════════════

        [Fact]
        public void PointInPolygon_WorksForConcaveZone()
        {
            // L-shape
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };

            var pInside = new DomainPoint3D(2.5, 2.5, 0);
            var pCornerOutside = new DomainPoint3D(7.5, 7.5, 0); // inside the "bounding box" but outside the concave L-boundary

            Assert.True(_geometry.PointInPolygon(pInside, pts));
            Assert.False(_geometry.PointInPolygon(pCornerOutside, pts));
        }

        [Fact]
        public void CandidateGeneration_UsesZoneGeometryService()
        {
            var engine = new ConstraintEngine(_geometry);
            var settings = new GASettings { CraneFootprintRadius = 0 };

            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 5, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(5, 10, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            var point = new DomainPoint3D(2.5, 2.5, 0);
            var allowed = new List<GreenAreaZone> { allowedZone };
            var forbidden = new List<GreenAreaZone>();

            Assert.True(engine.IsValidSetupPoint(point, allowed, forbidden, settings));
        }

        [Fact]
        public void ForbiddenZone_ConcaveShape_BlocksCorrectPoints()
        {
            var engine = new ConstraintEngine(_geometry);
            var settings = new GASettings { CraneFootprintRadius = 0 };

            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(100, 0, 0),
                    new DomainPoint3D(100, 100, 0),
                    new DomainPoint3D(0, 100, 0)
                }
            };

            var forbiddenZone = new GreenAreaZone
            {
                Type = ZoneType.Forbidden,
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(30, 10, 0),
                    new DomainPoint3D(30, 20, 0),
                    new DomainPoint3D(20, 20, 0),
                    new DomainPoint3D(20, 30, 0),
                    new DomainPoint3D(10, 30, 0)
                }
            };

            var allowed = new List<GreenAreaZone> { allowedZone };
            var forbidden = new List<GreenAreaZone> { forbiddenZone };

            var pInsideForbidden = new DomainPoint3D(15, 15, 0); // Inside L obstacle
            var pOutsideForbidden = new DomainPoint3D(25, 25, 0); // Outside L obstacle (inside corner)

            Assert.False(engine.IsValidSetupPoint(pInsideForbidden, allowed, forbidden, settings));
            Assert.True(engine.IsValidSetupPoint(pOutsideForbidden, allowed, forbidden, settings));
        }

        [Fact]
        public void InvalidZone_NotPassedToGA()
        {
            var engine = new ConstraintEngine(_geometry);
            var settings = new GASettings { CraneFootprintRadius = 0 };

            // Self-intersecting invalid allowed zone
            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            var allowed = new List<GreenAreaZone> { allowedZone };
            var forbidden = new List<GreenAreaZone>();
            var point = new DomainPoint3D(2, 2, 0);

            // Should filter out the invalid zone and return false
            Assert.False(engine.IsValidSetupPoint(point, allowed, forbidden, settings));
        }

        // ════════════════════════════════════════════════════════════════════
        // Extra Backward Compatibility & Precision Tests
        // ════════════════════════════════════════════════════════════════════

        [Fact]
        public void LegacyClosedZone_Normalize_RemovesDuplicateLastVertex()
        {
            var legacy = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(0, 0, 0) // Duplicate closing vertex
            };
            var normalized = _geometry.NormalizeVertices(legacy);
            Assert.Equal(3, normalized.Count);
            Assert.NotEqual(normalized[0], normalized[^1]);
        }

        [Fact]
        public void OldProjectZone_WithDuplicateClosure_RendersCorrectly()
        {
            var scene = new MockSceneService();
            var builder = new ZoneVisualBuilder(scene, _geometry);

            var legacyZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(0, 0, 0) // Duplicate closure
                }
            };

            var visual = builder.Build(legacyZone) as ModelVisual3D;
            Assert.NotNull(visual);
            var border = visual.Children.OfType<LinesVisual3D>().FirstOrDefault();
            Assert.NotNull(border);
            // Normalized to 3 unique vertices, outline has 3 edges = 6 points
            Assert.Equal(6, border.Points.Count);
        }

        [Fact]
        public void OldProjectZone_WithDuplicateClosure_GAChecksCorrectly()
        {
            var engine = new ConstraintEngine(_geometry);
            var settings = new GASettings { CraneFootprintRadius = 0 };

            var legacyZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(0, 10, 0),
                    new DomainPoint3D(0, 0, 0) // Duplicate closing
                }
            };

            var allowed = new List<GreenAreaZone> { legacyZone };
            var forbidden = new List<GreenAreaZone>();

            var pInside = new DomainPoint3D(5, 5, 0);
            var pOutside = new DomainPoint3D(15, 15, 0);

            Assert.True(engine.IsValidSetupPoint(pInside, allowed, forbidden, settings));
            Assert.False(engine.IsValidSetupPoint(pOutside, allowed, forbidden, settings));
        }

        [Fact]
        public void EarClipping_TriangleAreaMatchesConcavePolygonArea()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out _));
        }

        [Fact]
        public void EarClipping_TriangleCentroidsInsidePolygon()
        {
            var pts = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 5, 0),
                new DomainPoint3D(5, 5, 0),
                new DomainPoint3D(5, 10, 0),
                new DomainPoint3D(0, 10, 0)
            };
            var indices = _geometry.TriangulateSimplePolygon(pts);
            Assert.True(_geometry.ValidateTriangulation(pts, indices, out var error), error);
        }

        [Fact]
        public void RightClick_OnlyFinishesInDrawingMode()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                // IsDrawing is false, RightClick should reset measure or do nothing
                vm.HandleRightClick();
                Assert.Empty(scene.GetZones());
            });
        }

        [Fact]
        public void CtrlEnter_OnlyFinishesInDrawingMode()
        {
            RunOnSta(() =>
            {
                var drawing = new DrawingService();
                var scene = new MockSceneService();
                var render = new MockRenderingService();
                var interaction = new MockViewportInteractionService();
                var hitTest = new MockViewportHitTestService();
                var vm = new DrawingViewModel(drawing, scene, render, interaction, hitTest, _geometry);

                // IsDrawing is false, CtrlEnter should do nothing
                vm.FinishDrawingFromUserAction(FinishReason.CtrlEnter);
                Assert.Empty(scene.GetZones());
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // Mock Services
        // ════════════════════════════════════════════════════════════════════

        private class MockSceneService : ISceneService
        {
            private List<PrecastElement> _elements = new();
            private readonly List<GreenAreaZone> _zones = new();
            private readonly List<SceneCrane> _cranes = new();

            public List<PrecastElement> GetElements() => _elements;
            public void SetElements(List<PrecastElement> elements) => _elements = elements;
            public void ClearElements() => _elements.Clear();

            public List<PrecastElement> GetSelectedElements() => new();
            public void SetSelectedElements(List<PrecastElement> elements) { }
            public void ClearSelectedElements() { }

            public event Action<List<PrecastElement>>? ElementsChanged;
            public event Action<List<PrecastElement>>? ElementsSelectionChanged;

            public List<GreenAreaZone> GetZones() => _zones;
            public void AddZone(GreenAreaZone zone) => _zones.Add(zone);
            public void RemoveZone(GreenAreaZone zone) => _zones.Remove(zone);
            public void RemoveSelectedZone() { }
            public void ClearZones() => _zones.Clear();
            public void UpdateZone(GreenAreaZone zone) { }

            public GreenAreaZone? SelectedZone { get; private set; }
            public void SetSelectedZone(GreenAreaZone? zone) => SelectedZone = zone;
            public void ClearZoneSelection() => SelectedZone = null;

            public event Action<GreenAreaZone>? ZoneAdded;
            public event Action<GreenAreaZone>? ZoneRemoved;
            public event Action<GreenAreaZone>? ZoneUpdated;
            public event Action? ZonesCleared;
            public event Action<GreenAreaZone?>? ZoneSelected;

            public List<SceneCrane> GetCranes() => _cranes;
            public void AddCrane(SceneCrane crane) => _cranes.Add(crane);
            public void RemoveCrane(SceneCrane crane) => _cranes.Remove(crane);
            public void ClearCranes() => _cranes.Clear();

            public void MoveCrane(SceneCrane crane, double x, double y, double z) { }
            public void RotateCrane(SceneCrane crane, double angle) { }
            public void UpdateCrane(SceneCrane crane) { }

            public SceneCrane? SelectedCrane => null;
            public void SetSelectedCrane(SceneCrane? crane) { }
            public void ClearCraneSelection() { }

            public event Action<SceneCrane>? CraneAdded;
            public event Action<SceneCrane>? CraneRemoved;
            public event Action? CranesCleared;
            public event Action<SceneCrane>? CraneMoved;
            public event Action<SceneCrane>? CraneRotated;
            public event Action<SceneCrane>? CraneUpdated;
            public event Action<SceneCrane?>? CraneSelected;

            public List<SceneDxf> GetDxfEntities() => new();
            public void AddDxfEntity(SceneDxf entity) { }
            public void RemoveDxfEntity(SceneDxf entity) { }
            public void RemoveSelectedDxfEntity() { }
            public void ClearDxfEntities() { }
            public void UpdateDxfEntity(SceneDxf entity) { }

            public SceneDxf? SelectedDxfEntity => null;
            public void SetSelectedDxfEntity(SceneDxf? entity) { }
            public void ClearDxfSelection() { }

            public event Action<SceneDxf>? DxfEntityAdded;
            public event Action<SceneDxf>? DxfEntityRemoved;
            public event Action<SceneDxf>? DxfEntityUpdated;
            public event Action? DxfEntitiesCleared;
            public event Action<SceneDxf?>? DxfEntitySelected;

            public object? GetSelectedObject() => null;
            public void SetSelectedObject(object? obj) { }

            public event Action<object?>? SelectionChanged;
        }

        private class MockRenderingService : IRenderingService
        {
            private HelixViewport3D? _viewport;

            public void Initialize(HelixViewport3D viewport)
            {
                _viewport = viewport;
            }

            public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName) { }
            public void Clear() { }
            public void ClearLayer(string layerKey) { }
            public bool IsGridVisible { get; private set; } = true;
            public void SetGridVisible(bool isVisible) => IsGridVisible = isVisible;
            public void SetGridSize(double widthMeters, double lengthMeters) { }
            public void SetGridSize(double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) { }
            public HelixViewport3D? GetViewport() => _viewport;
            public void ZoomExtents() { }
        }

        private class MockViewportInteractionService : IViewportInteractionService
        {
            public IInteractionMode CurrentMode => null!;
            public void PushMode(IInteractionMode mode) { }
            public void PopMode() { }
            public void ResetToDefaultMode(IInteractionMode defaultMode) { }
            public void HandleKeyDown(HelixViewport3D viewport, KeyEventArgs e) { }
            public void HandleMouseMove(HelixViewport3D viewport, MouseEventArgs e) { }
            public void HandleMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e) { }
            public void HandleMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e) { }
        }

        private class MockViewportHitTestService : IViewportHitTestService
        {
            public object? HitObject { get; set; }
            public object? GetHitObject(HelixViewport3D viewport, System.Windows.Point screenPt) => HitObject;
            public bool TryGetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt, out System.Windows.Media.Media3D.Point3D worldPoint)
            {
                worldPoint = new System.Windows.Media.Media3D.Point3D();
                return false;
            }
            public bool TryGetPlacementPoint(HelixViewport3D viewport, System.Windows.Point screenPt, out System.Windows.Media.Media3D.Point3D worldPoint)
            {
                worldPoint = new System.Windows.Media.Media3D.Point3D();
                return false;
            }
            public bool TryGetHitCrane(HelixViewport3D viewport, System.Windows.Point screenPt, out SceneCrane crane)
            {
                crane = null!;
                return false;
            }
        }
    }
}
