using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.Application.Tests.DXF
{
    public sealed class DxfZoneImportServiceTests
    {
        private readonly DxfZoneImportService _service = new(new ZoneGeometryService());

        [Fact]
        public void ClosedLwPolylineStoredAsPolyline2D_ReturnsValidCandidate()
        {
            var doc = CreateDocumentWithPolyline("Zones", true, Square());

            var candidates = _service.GetCandidates(doc, "Zones", Matrix3D.Identity);

            var candidate = Assert.Single(candidates);
            Assert.True(candidate.IsValid);
            Assert.Empty(candidate.Issues);
            Assert.Equal(4, candidate.Vertices.Count);
            Assert.Equal(100, candidate.Area, 3);
        }

        [Fact]
        public void OpenPolyline2D_ReturnsInvalidCandidate()
        {
            var doc = CreateDocumentWithPolyline("Zones", false, Square());

            var candidate = Assert.Single(_service.GetCandidates(doc, "Zones", Matrix3D.Identity));

            Assert.False(candidate.IsValid);
            Assert.Contains(candidate.Issues, issue => issue.Contains("open", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DuplicateClosingVertex_IsNormalizedAway()
        {
            var doc = CreateDocumentWithPolyline("Zones", true, new[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            });

            var candidate = Assert.Single(_service.GetCandidates(doc, "Zones", Matrix3D.Identity));

            Assert.True(candidate.IsValid);
            Assert.Equal(4, candidate.Vertices.Count);
            Assert.NotEqual(candidate.Vertices[0], candidate.Vertices[^1]);
        }

        [Fact]
        public void SelfIntersectingPolyline_IsRejected()
        {
            var doc = CreateDocumentWithPolyline("Zones", true, new[]
            {
                new Vector2(0, 0),
                new Vector2(10, 10),
                new Vector2(10, 0),
                new Vector2(0, 10)
            });

            var candidate = Assert.Single(_service.GetCandidates(doc, "Zones", Matrix3D.Identity));

            Assert.False(candidate.IsValid);
            Assert.Contains(candidate.Issues, issue => issue.Contains("Self-intersecting", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LayerSummaries_CountEntitiesValidAndInvalidShapes()
        {
            var doc = new DxfDocument();
            var layer = new Layer("Zones") { Color = AciColor.Red };
            doc.Layers.Add(layer);

            var closed = new Polyline2D(Square(), true) { Layer = layer };
            var open = new Polyline2D(Square(), false) { Layer = layer };
            var line = new Line(new Vector2(0, 0), new Vector2(1, 1)) { Layer = layer };
            doc.Entities.Add(closed);
            doc.Entities.Add(open);
            doc.Entities.Add(line);

            var summary = Assert.Single(
                _service.GetLayerSummaries(doc, Matrix3D.Identity),
                layer => layer.Name == "Zones");

            Assert.Equal("Zones", summary.Name);
            Assert.Equal("#FF0000", summary.ColorHex);
            Assert.Equal(3, summary.TotalEntityCount);
            Assert.Equal(1, summary.ValidClosedCount);
            Assert.Equal(1, summary.InvalidCount);
        }

        [Fact]
        public void Candidates_ApplyTransformToVertices()
        {
            var doc = CreateDocumentWithPolyline("Zones", true, Square());
            var transform = Matrix3D.Identity;
            transform.Translate(new Vector3D(5, 7, 2));

            var candidate = Assert.Single(_service.GetCandidates(doc, "Zones", transform));

            Assert.True(candidate.IsValid);
            AssertPoint(candidate.Vertices[0], 5, 7, 2);
            AssertPoint(candidate.Vertices[1], 15, 7, 2);
        }

        [Fact]
        public void ClosedLineLoop_ReturnsValidCandidate()
        {
            var doc = new DxfDocument();
            var layer = new Layer("1");
            doc.Layers.Add(layer);

            doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)) { Layer = layer });
            doc.Entities.Add(new Line(new Vector2(10, 0), new Vector2(10, 10)) { Layer = layer });
            doc.Entities.Add(new Line(new Vector2(10, 10), new Vector2(0, 10)) { Layer = layer });
            doc.Entities.Add(new Line(new Vector2(0, 10), new Vector2(0, 0)) { Layer = layer });

            var candidate = Assert.Single(_service.GetCandidates(doc, "1", Matrix3D.Identity));

            Assert.True(candidate.IsValid);
            Assert.Equal("LineLoop", candidate.SourceEntityType);
            Assert.Equal(4, candidate.Vertices.Count);
            Assert.Equal(100, candidate.Area, 3);
        }

        [Fact]
        public void ClosedCircle_ReturnsValidCandidate()
        {
            var doc = new DxfDocument();
            var layer = new Layer("Curves");
            doc.Layers.Add(layer);
            doc.Entities.Add(new Circle(new Vector3(0, 0, 0), 10) { Layer = layer });

            var candidate = Assert.Single(_service.GetCandidates(doc, "Curves", Matrix3D.Identity));

            Assert.True(candidate.IsValid);
            Assert.Equal("Circle", candidate.SourceEntityType);
            Assert.True(candidate.Vertices.Count > 16);
            Assert.Equal(Math.PI * 100, candidate.Area, 0);
        }

        [Fact]
        public void FullEllipse_ReturnsValidCandidate()
        {
            var doc = new DxfDocument();
            var layer = new Layer("Curves");
            doc.Layers.Add(layer);
            doc.Entities.Add(new Ellipse(new Vector3(0, 0, 0), 20, 10) { Layer = layer });

            var candidate = Assert.Single(_service.GetCandidates(doc, "Curves", Matrix3D.Identity));

            Assert.True(candidate.IsValid);
            Assert.Equal("Ellipse", candidate.SourceEntityType);
            Assert.True(candidate.Vertices.Count > 16);
            Assert.Equal(Math.PI * 50, candidate.Area, 0);
        }

        [Fact]
        public void CurvedClosedPolyline_ReturnsValidCandidate()
        {
            var doc = new DxfDocument();
            var layer = new Layer("Curves");
            doc.Layers.Add(layer);

            var polyline = new Polyline2D(Square(), true) { Layer = layer };
            polyline.Vertexes[0].Bulge = 0.41421356237;
            doc.Entities.Add(polyline);

            var candidate = Assert.Single(_service.GetCandidates(doc, "Curves", Matrix3D.Identity));

            Assert.True(candidate.IsValid);
            Assert.Equal("Polyline2D", candidate.SourceEntityType);
            Assert.True(candidate.Vertices.Count > 4);
            Assert.Empty(candidate.Issues);
        }

        private static DxfDocument CreateDocumentWithPolyline(string layerName, bool isClosed, IEnumerable<Vector2> points)
        {
            var doc = new DxfDocument();
            var layer = new Layer(layerName);
            doc.Layers.Add(layer);
            doc.Entities.Add(new Polyline2D(points, isClosed) { Layer = layer });
            return doc;
        }

        private static IReadOnlyList<Vector2> Square() =>
            new[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

        private static void AssertPoint(DomainPoint3D point, double x, double y, double z)
        {
            Assert.Equal(x, point.X, 6);
            Assert.Equal(y, point.Y, 6);
            Assert.Equal(z, point.Z, 6);
        }
    }
}
