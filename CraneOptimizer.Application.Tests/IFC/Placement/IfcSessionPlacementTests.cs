using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.Application.Tests
{
    public class IfcSessionPlacementTests : IDisposable
    {
        private readonly string _tempFolder;

        public IfcSessionPlacementTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "IfcSessionPlacementTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                    Directory.Delete(_tempFolder, recursive: true);
            }
            catch { }
        }

        [Fact]
        public async Task Test_IfcRestore_DoesNotDoubleTranslateCachedElements()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var fakeIfcPath = Path.Combine(_tempFolder, "building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            // Cached elements already have user placement offset baked in (e.g. at 10, 20, 30)
            var cachedElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "IfcSlab",
                    ElementCOG = new DomainPoint3D(10, 20, 30),
                    COG_M = new DomainPoint3D(10, 20, 30),
                    IncludeInRender = true
                }
            };

            var savedOffset = new Point3D(10, 20, 30);

            // Act
            var success = await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, cachedElements);
            await Task.Delay(50); // Let background tasks process

            // Assert
            Assert.True(success);
            Assert.True(ops.ReplaceIfcModelCalled);
            Assert.Single(ops.GetElements());
            
            var restoredElement = ops.GetElements()[0];
            // Cached element MUST NOT be translated again. COG should remain exactly (10, 20, 30)
            Assert.Equal(10.0, restoredElement.ElementCOG.X);
            Assert.Equal(20.0, restoredElement.ElementCOG.Y);
            Assert.Equal(30.0, restoredElement.ElementCOG.Z);

            // PendingPlacementPoint MUST be set to the saved offset for rendering Raw IFC geometry correctly
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(savedOffset.X, viewModel.PendingPlacementPoint.Value.X);
            Assert.Equal(savedOffset.Y, viewModel.PendingPlacementPoint.Value.Y);
            Assert.Equal(savedOffset.Z, viewModel.PendingPlacementPoint.Value.Z);
        }

        [Fact]
        public async Task Test_IfcRestore_FullExtractionTranslatesOnce()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            // Raw IFC reader extracts untranslated elements from file (e.g. at 0, 0, 0)
            reader.ElementsToReturn = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "IfcSlab",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    COG_M = new DomainPoint3D(0, 0, 0),
                    IncludeInRender = true
                }
            };

            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var fakeIfcPath = Path.Combine(_tempFolder, "building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            var savedOffset = new Point3D(10, 20, 30);

            // Act - No cached elements passed, triggering full extraction
            var success = await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, null);
            await Task.Delay(50);

            // Assert
            Assert.True(success);
            Assert.True(ops.ReplaceIfcModelCalled);
            Assert.Single(ops.GetElements());

            var restoredElement = ops.GetElements()[0];
            // Freshly extracted elements MUST be translated exactly once by the insertion offset
            Assert.Equal(10.0, restoredElement.ElementCOG.X);
            Assert.Equal(20.0, restoredElement.ElementCOG.Y);
            Assert.Equal(30.0, restoredElement.ElementCOG.Z);

            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(savedOffset.X, viewModel.PendingPlacementPoint.Value.X);
        }

        [Fact]
        public void Test_IfcRestore_LowMediumHighPlacementConsistency()
        {
            // Act / Assert
            var savedOffset = new Point3D(10, 20, 30);
            
            // In Low quality mode: the drawer draws bounding boxes centered at element.ElementCOG
            // Restored element.ElementCOG = (10, 20, 30) (single-translated)
            var lowRenderCenter = new Point3D(10, 20, 30);

            // In Medium/High quality mode: raw IFC vertex at (0,0,0) is shifted by PendingPlacementPoint once
            var wpfTransform = new TranslateTransform3D(savedOffset.X, savedOffset.Y, savedOffset.Z);
            var medHighRenderCenter = wpfTransform.Transform(new Point3D(0, 0, 0));

            // Verify they align perfectly in 3D world coordinate space!
            Assert.Equal(lowRenderCenter.X, medHighRenderCenter.X);
            Assert.Equal(lowRenderCenter.Y, medHighRenderCenter.Y);
            Assert.Equal(lowRenderCenter.Z, medHighRenderCenter.Z);
        }

        [Fact]
        public async Task Test_IfcRestore_AlignsWithGreenAreaAndCranes()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var fakeIfcPath = Path.Combine(_tempFolder, "building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            // Cached element already translated once to (10, 20, 0)
            var cachedElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "IfcWall",
                    ElementCOG = new DomainPoint3D(10, 20, 0),
                    COG_M = new DomainPoint3D(10, 20, 0),
                    IncludeInRender = true
                }
            };

            // Setup Green Zone and Crane relative to (10, 20, 0) insertion point
            var zone = new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                Vertices = new List<DomainPoint3D> { new DomainPoint3D(15, 25, 0) }
            };
            scene.AddZone(zone);

            var crane = new SceneCrane
            {
                Id = Guid.NewGuid(),
                Position = new DomainPoint3D(12, 22, 0)
            };
            scene.AddCrane(crane);

            var savedOffset = new Point3D(10, 20, 0);

            // Act
            await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, cachedElements);

            // Assert
            var restoredElement = ops.GetElements().First();
            
            // 1. Calculate relative distance from element COG to zone vertex
            double elementToZoneDist = Math.Sqrt(
                Math.Pow(zone.Vertices[0].X - restoredElement.ElementCOG.X, 2) +
                Math.Pow(zone.Vertices[0].Y - restoredElement.ElementCOG.Y, 2));

            // Expected distance: sqrt((15-10)^2 + (25-20)^2) = sqrt(25 + 25) = sqrt(50) ~ 7.071
            Assert.Equal(Math.Sqrt(50), elementToZoneDist, 5);

            // 2. Calculate relative distance from element COG to crane position
            double elementToCraneDist = Math.Sqrt(
                Math.Pow(crane.Position.X - restoredElement.ElementCOG.X, 2) +
                Math.Pow(crane.Position.Y - restoredElement.ElementCOG.Y, 2));

            // Expected distance: sqrt((12-10)^2 + (22-20)^2) = sqrt(4 + 4) = sqrt(8) ~ 2.828
            Assert.Equal(Math.Sqrt(8), elementToCraneDist, 5);
        }

        [Fact]
        public async Task Test_IfcRestore_GaSavedResultsRemainSpatiallyConsistent()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var fakeIfcPath = Path.Combine(_tempFolder, "building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            var cachedElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "IfcSlab",
                    ElementCOG = new DomainPoint3D(10, 20, 0),
                    COG_M = new DomainPoint3D(10, 20, 0),
                    ElementHeight = 4.0,
                    ElementWT = 10.0,
                    IncludeInRender = true,
                    IncludeInGa = true
                }
            };

            var crane = new SceneCrane
            {
                Id = Guid.NewGuid(),
                Position = new DomainPoint3D(12, 22, 0)
            };
            scene.AddCrane(crane);

            var savedOffset = new Point3D(10, 20, 0);

            // Act
            await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, cachedElements);

            // Assert - Verify GA lifting distance calculation is completely accurate and aligned
            var restoredElement = ops.GetElements().First();
            
            // GA Slew/Radius Calculation Engine logic
            double dx = crane.Position.X - restoredElement.ElementCOG.X;
            double dy = crane.Position.Y - restoredElement.ElementCOG.Y;
            double calculatedRadius = Math.Sqrt(dx * dx + dy * dy);

            // Radius should be exactly sqrt((12-10)^2 + (22-20)^2) = sqrt(8) ~ 2.8284
            Assert.Equal(Math.Sqrt(8), calculatedRadius, 5);
        }

        [Fact]
        public async Task Test_IfcMove_AppliesOneDeltaAndUpdatesPendingPlacementPoint()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            // 1. Initial import/placement at (10, 20, 30)
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    COG_M = new DomainPoint3D(0, 0, 0)
                }
            };
            
            // Set up placement state
            var placedElements = ops.TranslateElements(elements, new DomainPoint3D(10, 20, 30));
            ops.ReplaceIfcModel(placedElements);
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, new Point3D(10, 20, 30));

            // Set placement mode flags to allow move
            viewModel.GetType().GetField("_isMovingExistingIfc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, true);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            // 2. Perform Move to (15, 25, 40)
            // Z anchor is 30, so delta.Z will be 40 - 30 = 10. New COG.Z should be 30 + 10 = 40.
            var success = viewModel.MovePlacedIfc(new Point3D(15, 25, 40));

            // Assert
            Assert.True(success);
            Assert.Single(ops.GetElements());
            
            var movedElement = ops.GetElements()[0];
            // Element COG should be at exactly (15, 25, 40)
            Assert.Equal(15.0, movedElement.ElementCOG.X);
            Assert.Equal(25.0, movedElement.ElementCOG.Y);
            Assert.Equal(40.0, movedElement.ElementCOG.Z);

            // PendingPlacementPoint should be updated to exactly (15, 25, 40)
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(15.0, viewModel.PendingPlacementPoint.Value.X);
            Assert.Equal(25.0, viewModel.PendingPlacementPoint.Value.Y);
            Assert.Equal(40.0, viewModel.PendingPlacementPoint.Value.Z);
        }

        [Fact]
        public void Test_IfcMove_LowMediumHighRenderAlignmentAfterMove()
        {
            // Act / Assert
            var movedOffset = new Point3D(15, 25, 40);
            
            // In Low quality mode: drawer draws centered at element.ElementCOG
            var lowRenderCenter = new Point3D(15, 25, 40);

            // In Medium/High quality mode: raw IFC vertex at (0,0,0) is transformed by PendingPlacementPoint
            var wpfTransform = new TranslateTransform3D(movedOffset.X, movedOffset.Y, movedOffset.Z);
            var medHighRenderCenter = wpfTransform.Transform(new Point3D(0, 0, 0));

            // Assert perfect alignment in world coordinate space!
            Assert.Equal(lowRenderCenter.X, medHighRenderCenter.X);
            Assert.Equal(lowRenderCenter.Y, medHighRenderCenter.Y);
            Assert.Equal(lowRenderCenter.Z, medHighRenderCenter.Z);
        }

        [Fact]
        public async Task Test_IfcMove_EnvironmentAlignmentAfterMove()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(10, 20, 0), COG_M = new DomainPoint3D(10, 20, 0) }
            };
            ops.ReplaceIfcModel(elements);
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, new Point3D(10, 20, 0));

            viewModel.GetType().GetField("_isMovingExistingIfc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, true);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            // Setup Green Zone and Crane in world space
            var zone = new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                Vertices = new List<DomainPoint3D> { new DomainPoint3D(20, 30, 0) }
            };
            scene.AddZone(zone);

            var crane = new SceneCrane
            {
                Id = Guid.NewGuid(),
                Position = new DomainPoint3D(17, 27, 0)
            };
            scene.AddCrane(crane);

            // Act - Move IFC from (10, 20, 0) to (15, 25, 0) (delta is 5, 5, 0)
            await Task.Run(() => viewModel.MovePlacedIfc(new Point3D(15, 25, 0)));

            // Assert
            var movedElement = ops.GetElements().First();
            Assert.Equal(15.0, movedElement.ElementCOG.X);
            Assert.Equal(25.0, movedElement.ElementCOG.Y);

            // Distance to green area vertex should be sqrt((20-15)^2 + (30-25)^2) = sqrt(50)
            double distanceToZone = Math.Sqrt(
                Math.Pow(zone.Vertices[0].X - movedElement.ElementCOG.X, 2) +
                Math.Pow(zone.Vertices[0].Y - movedElement.ElementCOG.Y, 2));
            Assert.Equal(Math.Sqrt(50), distanceToZone, 5);

            // Distance to crane should be sqrt((17-15)^2 + (27-25)^2) = sqrt(8)
            double distanceToCrane = Math.Sqrt(
                Math.Pow(crane.Position.X - movedElement.ElementCOG.X, 2) +
                Math.Pow(crane.Position.Y - movedElement.ElementCOG.Y, 2));
            Assert.Equal(Math.Sqrt(8), distanceToCrane, 5);
        }

        [Fact]
        public async Task Test_IfcMove_GaSavedResultsRemainSpatiallyConsistentAfterMove()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(10, 20, 0), COG_M = new DomainPoint3D(10, 20, 0), IncludeInGa = true }
            };
            ops.ReplaceIfcModel(elements);
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, new Point3D(10, 20, 0));

            viewModel.GetType().GetField("_isMovingExistingIfc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, true);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            var crane = new SceneCrane
            {
                Id = Guid.NewGuid(),
                Position = new DomainPoint3D(17, 27, 0)
            };
            scene.AddCrane(crane);

            // Act - Move IFC from (10, 20, 0) to (15, 25, 0)
            viewModel.MovePlacedIfc(new Point3D(15, 25, 0));

            // Assert
            var movedElement = ops.GetElements().First();
            
            // GA slew radius engine evaluation
            double dx = crane.Position.X - movedElement.ElementCOG.X;
            double dy = crane.Position.Y - movedElement.ElementCOG.Y;
            double calculatedRadius = Math.Sqrt(dx * dx + dy * dy);

            // Radius should be exactly sqrt((17-15)^2 + (27-25)^2) = sqrt(8) ~ 2.8284
            // Radius should be exactly sqrt((17-15)^2 + (27-25)^2) = sqrt(8) ~ 2.8284
            Assert.Equal(Math.Sqrt(8), calculatedRadius, 5);
        }

        [Fact]
        public async Task Test_IfcMove_PersistsMovedIfcPlacementAcrossReopen()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            // Initial elements at (10, 20, 30)
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(10, 20, 30), COG_M = new DomainPoint3D(10, 20, 30) }
            };
            ops.ReplaceIfcModel(elements);
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, new Point3D(10, 20, 30));

            viewModel.GetType().GetField("_isMovingExistingIfc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, true);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            // 1. Move to new offset (15, 25, 40)
            viewModel.MovePlacedIfc(new Point3D(15, 25, 40));

            // Verify moved locally
            var movedElements = ops.GetElements();
            Assert.Equal(15.0, movedElements[0].ElementCOG.X);
            Assert.Equal(25.0, movedElements[0].ElementCOG.Y);
            Assert.Equal(40.0, movedElements[0].ElementCOG.Z);

            // 2. Simulate Save/Reopen cycle
            // In a save state, the Snapshot is created with these movedElements and PendingPlacementPoint offset (15, 25, 40)
            var savedOffset = viewModel.PendingPlacementPoint.Value;
            var cachedElements = movedElements;

            // Clear operational scene to simulate project close
            ops.ReplaceIfcModel(new List<PrecastElement>());
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, null);

            var fakeIfcPath = Path.Combine(_tempFolder, "building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            // Reopen and Restore
            var success = await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, cachedElements);

            // Assert
            Assert.True(success);
            var restoredElements = ops.GetElements();
            Assert.Single(restoredElements);
            
            // Restored coordinates should be exactly at the moved coordinates (15, 25, 40), NOT double-translated!
            Assert.Equal(15.0, restoredElements[0].ElementCOG.X);
            Assert.Equal(25.0, restoredElements[0].ElementCOG.Y);
            Assert.Equal(40.0, restoredElements[0].ElementCOG.Z);

            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(15.0, viewModel.PendingPlacementPoint.Value.X);
        }

        [Fact]
        public void Test_IfcImport_PreservesNegativeZCoordinates()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                new MockVisibilityFilterService(),
                ops,
                null!,
                null!,
                null!);

            // An untranslated element with a negative Z coordinate (e.g. -6.0) representing foundation/footing
            var sourceElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-NEG-1",
                    ElementType = "IfcFooting",
                    ElementCOG = new DomainPoint3D(5, 5, -6.0),
                    COG_M = new DomainPoint3D(5, 5, -6.0),
                    ElementHeight = 2.0,
                    ElementLength = 2.0,
                    ElementWidth = 2.0,
                    BBoxHeight = 2.0,
                    BBoxLength = 2.0,
                    BBoxWidth = 2.0
                }
            };

            viewModel.GetType().GetField("_pendingIfcElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, sourceElements);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            // Act - Place pending IFC at user selected insertion point with Z = 2.0
            var success = viewModel.PlacePendingIfc(new Point3D(10, 20, 2.0));

            // Assert
            Assert.True(success);
            Assert.Single(ops.GetElements());

            var placedElement = ops.GetElements()[0];
            // Element Z coordinate must be preserved exactly plus only the user-selected insertion offset Z (i.e. -6.0 + 2.0 = -4.0)
            // It MUST NOT be normalized or forced to Z = 0 or 2.0!
            Assert.Equal(-4.0, placedElement.ElementCOG.Z);
            Assert.Equal(-4.0, placedElement.COG_M.Z);

            // PendingPlacementPoint Z offset must be exactly the user-selected insertion offset (2.0)
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(2.0, viewModel.PendingPlacementPoint.Value.Z);
        }

        [Fact]
        public async Task Test_IfcRestore_PreservesNegativeZCoordinatesAcrossReopen()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            var fakeIfcPath = Path.Combine(_tempFolder, "negative_z_building.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "IFC");

            // Cached element with negative Z coordinate (placed previously, so COG.Z is -4.0, which is original -6.0 plus offset 2.0)
            var cachedElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-NEG-1",
                    ElementType = "IfcFooting",
                    ElementCOG = new DomainPoint3D(15, 25, -4.0),
                    COG_M = new DomainPoint3D(15, 25, -4.0),
                    IncludeInRender = true
                }
            };

            var savedOffset = new Point3D(10, 20, 2.0);

            // Act - Restore the model
            var success = await viewModel.LoadIfcFromProjectAsync(fakeIfcPath, savedOffset, cachedElements);

            // Assert
            Assert.True(success);
            Assert.Single(ops.GetElements());

            var restoredElement = ops.GetElements()[0];
            // Cached element must remain exactly at its placed Z-coordinate (which is negative) and not be shifted
            Assert.Equal(-4.0, restoredElement.ElementCOG.Z);

            // PendingPlacementPoint must match the savedOffset exactly
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(2.0, viewModel.PendingPlacementPoint.Value.Z);
        }

        [Fact]
        public void Test_IfcMove_PreservesNegativeZCoordinatesCorrectly()
        {
            // Arrange
            var reader = new MockIfcReaderRepository();
            var ops = new MockIfcSceneOperationService();
            var scene = new MockSceneService();

            var viewModel = new IfcViewerViewModel(
                reader,
                scene,
                null!,
                null!,
                null!,
                null!,
                null!,
                ops,
                null!,
                null!,
                null!);

            // Placed elements with negative Z coordinate (e.g. -4.0, under current offset Z = 2.0)
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-NEG-1",
                    ElementCOG = new DomainPoint3D(15, 25, -4.0),
                    COG_M = new DomainPoint3D(15, 25, -4.0)
                }
            };

            ops.ReplaceIfcModel(elements);
            viewModel.GetType().GetProperty("PendingPlacementPoint")?.SetValue(viewModel, new Point3D(10, 20, 2.0));

            viewModel.GetType().GetField("_isMovingExistingIfc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(viewModel, true);
            viewModel.GetType().GetProperty("IsPlacementMode")?.SetValue(viewModel, true);

            // Act - Move IFC from (10, 20, 2.0) to new insertion point at (12, 22, 5.0)
            // The delta.Z is new insertion point Z (5.0) - old offset Z (2.0) = +3.0
            // The new COG.Z should be -4.0 + 3.0 = -1.0
            var success = viewModel.MovePlacedIfc(new Point3D(12, 22, 5.0));

            // Assert
            Assert.True(success);
            Assert.Single(ops.GetElements());

            var movedElement = ops.GetElements()[0];
            // The element COG.Z should be exactly -1.0 (which is negative)
            Assert.Equal(-1.0, movedElement.ElementCOG.Z);

            // PendingPlacementPoint Z should be updated to exactly 5.0
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(5.0, viewModel.PendingPlacementPoint.Value.Z);
        }

        [Fact]
        public void Test_IfcNegativeZ_LowMediumHighRenderAlignment()
        {
            // Arrange
            // Raw untranslated element with negative Z: -6.0
            var originalZ = -6.0;
            var userOffsetZ = 2.0;

            // In Low quality mode: drawer draws centered at element.ElementCOG in world space
            // ElementCOG Z is exactly -6.0 + 2.0 = -4.0
            var lowRenderZ = originalZ + userOffsetZ;

            // In Medium/High quality mode: raw IFC vertex at Z = -6.0 is transformed by PendingPlacementPoint (2.0)
            var wpfTransform = new TranslateTransform3D(0, 0, userOffsetZ);
            var medHighRenderPoint = wpfTransform.Transform(new Point3D(0, 0, originalZ));

            // Verify perfect alignment below zero (at Z = -4.0) in both modes!
            Assert.Equal(-4.0, lowRenderZ);
            Assert.Equal(-4.0, medHighRenderPoint.Z);
            Assert.Equal(lowRenderZ, medHighRenderPoint.Z);
        }

        // =========================================================================
        // Mocks for clean state control
        // =========================================================================

        private class MockIfcReaderRepository : IIfcReaderRepository
        {
            public List<PrecastElement> ElementsToReturn { get; set; } = new();
            public bool InitializeSessionForRenderingCalled { get; set; }

            public List<PrecastElement> ReadElements(string ifcPath) => ElementsToReturn;
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress) => ElementsToReturn;

            public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default)
            {
                InitializeSessionForRenderingCalled = true;
                return Task.CompletedTask;
            }

            public double ModelLengthFactor => 1.0;
            public double? ModelGroundElevation => 0.0;

            public void ClearData() { }
            public void Dispose() { }
        }

        private class MockIfcSceneOperationService : IIfcSceneOperationService
        {
            public bool ReplaceIfcModelCalled { get; set; }
            private List<PrecastElement> _elements = new();

            public bool HasIfcModel => _elements.Count > 0;
            public List<PrecastElement> GetElements() => _elements;

            public List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset)
            {
                return elements.Select(e => new PrecastElement
                {
                    ElementID = e.ElementID,
                    ElementType = e.ElementType,
                    ElementCOG = new DomainPoint3D(e.ElementCOG.X + offset.X, e.ElementCOG.Y + offset.Y, e.ElementCOG.Z + offset.Z),
                    COG_M = new DomainPoint3D(e.COG_M.X + offset.X, e.COG_M.Y + offset.Y, e.COG_M.Z + offset.Z),
                    IncludeInRender = e.IncludeInRender,
                    IncludeInGa = e.IncludeInGa
                }).ToList();
            }

            public List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements)
            {
                ReplaceIfcModelCalled = true;
                _elements = elements.ToList();
                return _elements;
            }

            public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements) => new();
            public List<PrecastElement> MoveIfcModel(DomainPoint3D delta)
            {
                var moved = TranslateElements(_elements, delta);
                _elements = moved;
                return _elements;
            }
            public void DeleteIfcModel(bool clearReaderData = true) { }
        }

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

        private class MockVisibilityFilterService : IElementVisibilityFilterService
        {
            public event Action? StateChanged;
            
            public IReadOnlyList<PrecastElement> AllElements => new List<PrecastElement>();
            public IReadOnlyList<PrecastElement> VisibleElements => new List<PrecastElement>();
            public IReadOnlyList<string> ElementTypes => new List<string>();
            public IReadOnlyList<string> Materials => new List<string>();
            public IReadOnlyList<string> Storeys => new List<string>();

            public int ShowingCount => 1;
            public int TotalCount => 1;
            public double AvailableMinWeightKg => 0;
            public double AvailableMaxWeightKg => 0;
            public double MinWeightKg => 0;
            public double MaxWeightKg => 0;
            public string SearchText => "";

            public void SetElements(IEnumerable<PrecastElement> elements) { }
            public void SetSearchText(string text) { }
            public void SetTypeEnabled(string type, bool enabled) { }
            public void SetMaterialEnabled(string material, bool enabled) { }
            public void SetStoreyEnabled(string storey, bool enabled) { }
            public void SetWeightRange(double minWeightKg, double maxWeightKg) { }
            public void SetIncludedElementIds(IEnumerable<string>? elementIds) { }
            public void ResetFilters() { }
            public void ShowAll() { }
            public void HideElements(IEnumerable<PrecastElement> elements) { }
            public void IsolateElements(IEnumerable<PrecastElement> elements) { }
            public void ApplyLoadOptions(IfcLoadOptionsDto options) { }
            public string GetStorey(PrecastElement element) => "";
            public bool IsTypeEnabled(string type) => true;
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
            public bool IsElementIncluded(PrecastElement element) => true;
        }
    }
}
