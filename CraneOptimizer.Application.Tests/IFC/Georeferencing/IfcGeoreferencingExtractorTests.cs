using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.MeasureResource;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.Application.Tests.IFC.Georeferencing
{
    public sealed class IfcGeoreferencingExtractorTests
    {
        [Fact]
        public void Test_Extract_NullModel_ReturnsNull()
        {
            var extractor = new IfcGeoreferencingExtractor();
            var result = extractor.Extract(null!);
            Assert.Null(result);
        }

        [Fact]
        public void Test_Extract_NonXbimModel_ThrowsArgumentException()
        {
            var extractor = new IfcGeoreferencingExtractor();
            Assert.Throws<ArgumentException>(() => extractor.Extract(new object()));
        }

        [Fact]
        public void Test_Extract_EmptyModel_ReturnsNull()
        {
            var extractor = new IfcGeoreferencingExtractor();
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                var result = extractor.Extract(model);
                Assert.Null(result);
            }
        }

        [Fact]
        public void Test_Extract_Ifc4_ProjectedCRS_And_MapConversion()
        {
            var extractor = new IfcGeoreferencingExtractor();
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Georeferencing"))
                {
                    var mapConversion = model.Instances.New<IfcMapConversion>();
                    mapConversion.Eastings = 123456.78;
                    mapConversion.Northings = 987654.32;
                    mapConversion.OrthogonalHeight = 12.34;
                    mapConversion.XAxisAbscissa = 0.8660254; // cos(30 deg)
                    mapConversion.XAxisOrdinate = 0.5;       // sin(30 deg)
                    mapConversion.Scale = 1.00003;

                    var crs = model.Instances.New<IfcProjectedCRS>();
                    crs.Name = "EPSG:32637";
                    crs.Description = "WGS 84 / UTM zone 37N";

                    mapConversion.TargetCRS = crs;

                    txn.Commit();
                }

                var result = extractor.Extract(model);
                Assert.NotNull(result);
                Assert.True(result.HasMapConversion);
                Assert.Equal(123456.78, result.Eastings);
                Assert.Equal(987654.32, result.Northings);
                Assert.Equal(12.34, result.OrthogonalHeight);
                Assert.Equal(0.8660254, result.XAxisAbscissa);
                Assert.Equal(0.5, result.XAxisOrdinate);
                Assert.Equal(1.00003, result.Scale);
                Assert.Equal("EPSG:32637", result.CrsName);
                Assert.Equal("WGS 84 / UTM zone 37N", result.CrsDescription);
                Assert.Equal("32637", result.EpsgCode);
            }
        }

        [Fact]
        public void Test_Extract_MapConversion_Only_NoCRS()
        {
            var extractor = new IfcGeoreferencingExtractor();
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create MapConversion"))
                {
                    var mapConversion = model.Instances.New<IfcMapConversion>();
                    mapConversion.Eastings = 500.0;
                    mapConversion.Northings = 600.0;
                    mapConversion.OrthogonalHeight = 10.0;

                    txn.Commit();
                }

                var result = extractor.Extract(model);
                Assert.NotNull(result);
                Assert.True(result.HasMapConversion);
                Assert.Equal(500.0, result.Eastings);
                Assert.Equal(600.0, result.Northings);
                Assert.Equal(10.0, result.OrthogonalHeight);
                Assert.Null(result.CrsName);
                Assert.Null(result.CrsDescription);
                Assert.Null(result.EpsgCode);
            }
        }

        [Fact]
        public void Test_Extract_IfcSite_LatLon_Conversion()
        {
            var extractor = new IfcGeoreferencingExtractor();
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Site"))
                {
                    var site = model.Instances.New<IfcSite>();
                    site.RefElevation = 45.2;

                    // DMS format: 30 deg, 15 min, 20 sec, 0 millionths = 30.25555556 deg
                    var lat = new IfcCompoundPlaneAngleMeasure();
                    IfcCompoundPlaneAngleMeasure.Add(ref lat, 30);
                    IfcCompoundPlaneAngleMeasure.Add(ref lat, 15);
                    IfcCompoundPlaneAngleMeasure.Add(ref lat, 20);
                    IfcCompoundPlaneAngleMeasure.Add(ref lat, 0);

                    // DMS format: -45 deg, 30 min, 0 sec, 0 millionths = -45.5 deg
                    var lon = new IfcCompoundPlaneAngleMeasure();
                    IfcCompoundPlaneAngleMeasure.Add(ref lon, -45);
                    IfcCompoundPlaneAngleMeasure.Add(ref lon, 30);
                    IfcCompoundPlaneAngleMeasure.Add(ref lon, 0);
                    IfcCompoundPlaneAngleMeasure.Add(ref lon, 0);

                    site.RefLatitude = lat;
                    site.RefLongitude = lon;

                    txn.Commit();
                }

                var result = extractor.Extract(model);
                Assert.NotNull(result);
                Assert.False(result.HasMapConversion);
                Assert.True(result.HasSiteLatLon);
                Assert.Equal(45.2, result.SiteElevation);
                Assert.NotNull(result.SiteLatitude);
                Assert.NotNull(result.SiteLongitude);
                Assert.Equal(30.25555556, result.SiteLatitude.Value, 5);
                Assert.Equal(-45.5, result.SiteLongitude.Value, 5);
            }
        }

        [Fact]
        public void Test_Rotation_Angles()
        {
            var extractor = new IfcGeoreferencingExtractor();

            // Test 0 degrees (xAxisAbscissa=1, xAxisOrdinate=0)
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Zero rotation"))
                {
                    var mapConversion = model.Instances.New<IfcMapConversion>();
                    mapConversion.XAxisAbscissa = 1.0;
                    mapConversion.XAxisOrdinate = 0.0;
                    txn.Commit();
                }
                var result = extractor.Extract(model);
                Assert.NotNull(result);
                double rotationDegrees = GetRotationDegrees(result);
                Assert.Equal(0.0, rotationDegrees, 5);
            }

            // Test 90 degrees (xAxisAbscissa=0, xAxisOrdinate=1)
            using (var model = IfcStore.Create(XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("90 rotation"))
                {
                    var mapConversion = model.Instances.New<IfcMapConversion>();
                    mapConversion.XAxisAbscissa = 0.0;
                    mapConversion.XAxisOrdinate = 1.0;
                    txn.Commit();
                }
                var result = extractor.Extract(model);
                Assert.NotNull(result);
                double rotationDegrees = GetRotationDegrees(result);
                Assert.Equal(90.0, rotationDegrees, 5);
            }
        }

        [Fact]
        public void Test_ManualReferenceProtection_InTryApply()
        {
            // Set up a mock IfcReaderRepository that returns a georeferencing result
            var mockIfcReader = new StubIfcReaderRepositoryForIntegration
            {
                GeoreferencingToReturn = new IfcGeoreferencingResult(
                    HasMapConversion: true,
                    Eastings: 1000.0,
                    Northings: 2000.0,
                    OrthogonalHeight: 300.0,
                    XAxisAbscissa: 1.0,
                    XAxisOrdinate: 0.0,
                    Scale: 1.0,
                    CrsName: "EPSG:32637",
                    CrsDescription: "CRS Desc",
                    EpsgCode: "32637",
                    HasSiteLatLon: false,
                    SiteLatitude: null,
                    SiteLongitude: null,
                    SiteElevation: null
                )
            };

            // Set up ProjectCoordinateTransformService with a manual reference
            var transformService = new ProjectCoordinateTransformService();
            var manualReference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 555.5,
                SurveyNorthing = 666.6,
                Source = "ManualSinglePoint"
            };
            transformService.ApplyReference(manualReference, out _);

            // Construct view model
            var viewModel = new IfcViewerViewModel(
                mockIfcReader,
                new StubSceneService(),
                null!,
                null!,
                null!,
                null!,
                null!,
                new StubIfcSceneOperationService(),
                null!,
                null!,
                null!,
                transformService
            );

            // Trigger the private TryApplyIfcGeoreferencing method via reflection
            var method = typeof(IfcViewerViewModel).GetMethod("TryApplyIfcGeoreferencing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);

            method.Invoke(viewModel, null);

            // Verify the reference is STILL the manual reference (not overwritten)
            var currentRef = transformService.CurrentReference;
            Assert.NotNull(currentRef);
            Assert.Equal("ManualSinglePoint", currentRef.Source);
            Assert.Equal(555.5, currentRef.SurveyEasting);
            Assert.Equal(666.6, currentRef.SurveyNorthing);
        }

        [Fact]
        public void Test_AutoPopulateDisabledOrEmptyReference_InTryApply()
        {
            var mockIfcReader = new StubIfcReaderRepositoryForIntegration
            {
                GeoreferencingToReturn = new IfcGeoreferencingResult(
                    HasMapConversion: true,
                    Eastings: 1000.0,
                    Northings: 2000.0,
                    OrthogonalHeight: 300.0,
                    XAxisAbscissa: 1.0,
                    XAxisOrdinate: 0.0,
                    Scale: 1.0,
                    CrsName: "EPSG:32637",
                    CrsDescription: "CRS Desc",
                    EpsgCode: "32637",
                    HasSiteLatLon: false,
                    SiteLatitude: null,
                    SiteLongitude: null,
                    SiteElevation: null
                )
            };

            var transformService = new ProjectCoordinateTransformService();
            // Default reference is empty/disabled

            var viewModel = new IfcViewerViewModel(
                mockIfcReader,
                new StubSceneService(),
                null!,
                null!,
                null!,
                null!,
                null!,
                new StubIfcSceneOperationService(),
                null!,
                null!,
                null!,
                transformService
            );

            var method = typeof(IfcViewerViewModel).GetMethod("TryApplyIfcGeoreferencing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);

            method.Invoke(viewModel, null);

            // Verify reference has been updated with IFC values
            var currentRef = transformService.CurrentReference;
            Assert.NotNull(currentRef);
            Assert.Equal("IfcMapConversion", currentRef.Source);
            Assert.True(currentRef.IsEnabled);
            Assert.Equal(1000.0, currentRef.SurveyEasting);
            Assert.Equal(2000.0, currentRef.SurveyNorthing);
            Assert.Equal(300.0, currentRef.SurveyElevation);
            Assert.Equal("EPSG:32637", currentRef.CoordinateSystemName);
            Assert.Equal("32637", currentRef.EPSGCode);
        }

        private static double GetRotationDegrees(IfcGeoreferencingResult result)
        {
            double rotationToNorthDegrees = 0.0;
            if (result.XAxisAbscissa.HasValue && result.XAxisOrdinate.HasValue)
            {
                double abscissa = result.XAxisAbscissa.Value;
                double ordinate = result.XAxisOrdinate.Value;
                if (abscissa != 0.0 || ordinate != 0.0)
                {
                    double angleRad = Math.Atan2(ordinate, abscissa);
                    rotationToNorthDegrees = angleRad * 180.0 / Math.PI;
                }
            }
            return rotationToNorthDegrees;
        }

        // =========================================================================
        // Mocks for VM Integration testing
        // =========================================================================

        private class StubIfcReaderRepositoryForIntegration : IIfcReaderRepository
        {
            public IfcGeoreferencingResult? GeoreferencingToReturn { get; set; }

            public List<PrecastElement> ReadElements(string ifcPath) => new();
            public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options) => new();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress) => new();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress, IfcReadingOptions? options, CancellationToken cancellationToken = default) => new();

            public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public double ModelLengthFactor => 1.0;
            public double? ModelGroundElevation => 0.0;

            public IfcGeoreferencingResult? GetGeoreferencingData() => GeoreferencingToReturn;

            public void ClearData() { }
            public void Dispose() { }
        }

        private class StubSceneService : ISceneService
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

            public GreenAreaZone? SelectedZone => null;
            public void SetSelectedZone(GreenAreaZone? zone) { }
            public void ClearZoneSelection() { }

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

        private class StubIfcSceneOperationService : IIfcSceneOperationService
        {
            public bool HasIfcModel => false;
            public List<PrecastElement> GetElements() => new();
            public List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset) => new();
            public List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements) => new();
            public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements) => new();
            public List<PrecastElement> MoveIfcModel(DomainPoint3D delta) => new();
            public void DeleteIfcModel(bool clearReaderData = true) { }
        }
    }
}
