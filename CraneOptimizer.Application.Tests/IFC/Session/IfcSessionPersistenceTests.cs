using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Text.Json;
using CraneOptimizer.Application.Services;

namespace CraneOptimizer.Application.Tests
{
    public class IfcSessionPersistenceTests : IDisposable
    {
        private readonly string _tempFolder;

        public IfcSessionPersistenceTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "CraneOptimizerTests_" + Guid.NewGuid().ToString("N"));
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
        public void TestMapperRoundTrip()
        {
            // Arrange
            var original = new PrecastElement
            {
                ElementID = "123-ABC-456",
                ElementRevitId = "RevitTag_999",
                ElementMark = "COL-01",
                ElementType = "IfcColumn",
                ElementLength = 5.2,
                ElementWidth = 0.4,
                ElementHeight = 0.4,
                ElementMaterial = "Concrete C40",
                ElementWT = 2.5,
                ElementVolume = 1.0,
                ElementCOG = new DomainPoint3D(10, 20, 30),
                BBoxLength = 5.2,
                BBoxWidth = 0.4,
                BBoxHeight = 0.4,

                // Phase 2A/0 Normalization fields
                RawIfcType = "IfcColumnStandardCase",
                ResolvedCategory = "Column",
                Role = IfcElementRole.LiftableElement,
                ParentGlobalId = "PARENT-ID-111",
                ChildGlobalIds = new List<string> { "CHILD-A", "CHILD-B" },
                VisibleGeometryOwnerGlobalIds = new List<string> { "GEOM-OWNER-1" },
                HiddenInternalGeometryOwnerGlobalIds = new List<string> { "HIDDEN-1", "HIDDEN-2" },
                IncludeInRender = true,
                IncludeInElementTree = true,
                IncludeInGa = true,
                WeightTons = 2.5,
                WeightSource = "VolumeEstimation",
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                VolumeM3 = 1.0,
                COG_M = new DomainPoint3D(10, 20, 30),
                BoundingBox_M = new DomainPoint3D(5.2, 0.4, 0.4),
                ExtractionWarnings = new List<string> { "Suspiciously low weight", "Multiple materials found" },
                NormalizationDecisionReason = "Matches column rule perfectly"
            };

            // Act
            var entity = PrecastElementMapper.ToEntity(original, projectInfoId: 1, ifcFileId: 2);
            var restored = PrecastElementMapper.ToDomain(entity);

            // Assert
            Assert.Equal(original.ElementID, restored.ElementID);
            Assert.Equal(original.ElementRevitId, restored.ElementRevitId);
            Assert.Equal(original.ElementMark, restored.ElementMark);
            Assert.Equal(original.ElementType, restored.ElementType);
            Assert.Equal(original.ElementLength, restored.ElementLength);
            Assert.Equal(original.ElementWidth, restored.ElementWidth);
            Assert.Equal(original.ElementHeight, restored.ElementHeight);
            Assert.Equal(original.ElementMaterial, restored.ElementMaterial);
            Assert.Equal(original.ElementWT, restored.ElementWT);
            Assert.Equal(original.ElementVolume, restored.ElementVolume);

            // Coordinates
            Assert.Equal(original.ElementCOG.X, restored.ElementCOG.X, 3);
            Assert.Equal(original.ElementCOG.Y, restored.ElementCOG.Y, 3);
            Assert.Equal(original.ElementCOG.Z, restored.ElementCOG.Z, 3);

            // Phase 2A Normalization survival
            Assert.Equal(original.RawIfcType, restored.RawIfcType);
            Assert.Equal(original.ResolvedCategory, restored.ResolvedCategory);
            Assert.Equal(original.Role, restored.Role);
            Assert.Equal(original.ParentGlobalId, restored.ParentGlobalId);
            Assert.Equal(original.ChildGlobalIds, restored.ChildGlobalIds);
            Assert.Equal(original.VisibleGeometryOwnerGlobalIds, restored.VisibleGeometryOwnerGlobalIds);
            Assert.Equal(original.HiddenInternalGeometryOwnerGlobalIds, restored.HiddenInternalGeometryOwnerGlobalIds);
            Assert.Equal(original.IncludeInRender, restored.IncludeInRender);
            Assert.Equal(original.IncludeInElementTree, restored.IncludeInElementTree);
            Assert.Equal(original.IncludeInGa, restored.IncludeInGa);
            Assert.Equal(original.WeightTons, restored.WeightTons);
            Assert.Equal(original.WeightSource, restored.WeightSource);
            Assert.Equal(original.WeightTrustLevel, restored.WeightTrustLevel);
            Assert.Equal(original.VolumeM3, restored.VolumeM3);
            Assert.Equal(original.NormalizationDecisionReason, restored.NormalizationDecisionReason);
            Assert.Equal(original.ExtractionWarnings, restored.ExtractionWarnings);

            // Bounding box size recreated symmetrically
            Assert.Equal(original.BoundingBox_M.X, restored.BoundingBox_M.X, 3);
            Assert.Equal(original.BoundingBox_M.Y, restored.BoundingBox_M.Y, 3);
            Assert.Equal(original.BoundingBox_M.Z, restored.BoundingBox_M.Z, 3);
        }

        [Fact]
        public async Task TestSavingAndRestoringIfcMetadata()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            // Create a fake IFC file on disk to simulate copying
            var modelFolder = Path.Combine(_tempFolder, "Models", "IFC");
            Directory.CreateDirectory(modelFolder);
            var fakeIfcPath = Path.Combine(_tempFolder, "source.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "FAKE IFC FILE CONTENT");

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            var originalElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "GID-1",
                    ElementMark = "COL-1",
                    ElementType = "IfcColumn",
                    ElementMaterial = "Concrete",
                    Role = IfcElementRole.LiftableElement,
                    WeightTons = 3.5,
                    WeightSource = "Specified",
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    IncludeInRender = true,
                    IncludeInGa = true,
                    COG_M = new DomainPoint3D(1, 2, 3),
                    BoundingBox_M = new DomainPoint3D(2, 2, 4)
                }
            };

            var state = new ProjectStateSnapshot
            {
                IfcFile = new ProjectIfcFileState
                {
                    AbsolutePath = fakeIfcPath,
                    FileName = "source.ifc",
                    Role = "Primary",
                    InsertionOffset = new DomainPoint3D(10, 0, 0),
                    RotationDegrees = 0,
                    Scale = 1
                },
                IfcElements = originalElements
            };

            var service = new ProjectStateService();

            // Act: Save
            await service.SaveProjectStateAsync(session, state);

            // Assert: verify written in database
            await using (var context = CreatedProjectDbContextFactory.Create(_tempFolder))
            {
                var file = await context.IfcFiles.FirstOrDefaultAsync();
                Assert.NotNull(file);
                Assert.Equal("source.ifc", file.FileName);
                Assert.True(file.IsCacheValid);
                Assert.Equal(1, file.ElementCount);

                var elements = await context.IfcElements.ToListAsync();
                Assert.Single(elements);
                Assert.Equal("GID-1", elements[0].GlobalId);
                Assert.Equal("Specified", elements[0].WeightSource);
                Assert.Equal("ExplicitQuantity", elements[0].WeightTrustLevel);
            }

            // Act: Restore
            var restoredState = await service.LoadProjectStateAsync(session);

            // Assert
            Assert.NotNull(restoredState.IfcFile);
            Assert.Single(restoredState.IfcElements);
            var restoredEl = restoredState.IfcElements[0];
            Assert.Equal("GID-1", restoredEl.ElementID);
            Assert.Equal(IfcElementRole.LiftableElement, restoredEl.Role);
            Assert.Equal(3.5, restoredEl.WeightTons);
            Assert.Equal("Specified", restoredEl.WeightSource);
            Assert.Equal(IfcWeightTrustLevel.ExplicitQuantity, restoredEl.WeightTrustLevel);
            Assert.True(restoredEl.IncludeInRender);
            Assert.True(restoredEl.IncludeInGa);
            Assert.Equal(1.0, restoredEl.COG_M.X);
            Assert.Equal(2.0, restoredEl.BoundingBox_M.X);
        }

        [Fact]
        public async Task TestInvalidCacheFallsBackToFullExtraction()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            var fakeIfcPath = Path.Combine(_tempFolder, "source.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "OLD CONTENT");

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            var service = new ProjectStateService();

            var originalElements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "GID-1", ElementType = "Column" }
            };

            var state = new ProjectStateSnapshot
            {
                IfcFile = new ProjectIfcFileState
                {
                    AbsolutePath = fakeIfcPath,
                    FileName = "source.ifc",
                    InsertionOffset = new DomainPoint3D(0, 0, 0)
                },
                IfcElements = originalElements
            };

            // Save first to get valid cache metadata
            await service.SaveProjectStateAsync(session, state);

            // Act: Modifying file on disk (changes size and write time) to invalidate cache
            var copiedIfcPath = Path.Combine(_tempFolder, "Models", "IFC", "source.ifc");
            await File.WriteAllTextAsync(copiedIfcPath, "NEW CONTENT WITH DIFFERENT SIZE AND WRITE TIME!");
            File.SetLastWriteTimeUtc(copiedIfcPath, DateTime.UtcNow.AddMinutes(5));

            // Load
            var restoredState = await service.LoadProjectStateAsync(session);

            // Assert: Must fallback (elements list is empty)
            Assert.Empty(restoredState.IfcElements);
        }



        [Fact]
        public async Task TestMigrationDoesNotCreateGlobalOrDeferredTables()
        {
            // Arrange & Act
            await using (var context = CreatedProjectDbContextFactory.Create(_tempFolder))
            {
                await context.Database.MigrateAsync();

                // Assert
                var model = context.Model;

                // 1. Ensure our targeted entity tables exist
                var projectInfoTable = model.FindEntityType(typeof(ProjectInfoEntity));
                Assert.NotNull(projectInfoTable);
                var ifcFileTable = model.FindEntityType(typeof(ProjectIfcFileEntity));
                Assert.NotNull(ifcFileTable);
                var ifcElementTable = model.FindEntityType(typeof(ProjectIfcElementEntity));
                Assert.NotNull(ifcElementTable);

                // 2. Ensure deferred/global tables are ignored/ignored or not present in DB
                // E.g. we must not have ProjectSettingEntity, ProjectGeometryCacheEntity, etc. mapped as active DbSets in DB
                var settingEntity = model.FindEntityType("CraneOptimizer.Domain.CreatedProject.ProjectSettingEntity");
                Assert.Null(settingEntity);

                var geometryCacheEntity = model.FindEntityType("CraneOptimizer.Domain.CreatedProject.IfcFiles.ProjectGeometryCacheEntity");
                Assert.Null(geometryCacheEntity);

                var elementResultEntity = model.FindEntityType("CraneOptimizer.Domain.CreatedProject.OptimizationFiles.ProjectOptimizationElementResultEntity");
                Assert.Null(elementResultEntity);
            }
        }

        [Fact]
        public async Task TestSaveAndLoadCoordinateReference_SQLite()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            var transformService = new ProjectCoordinateTransformService();
            var expectedRef = new ProjectCoordinateReference
            {
                IsEnabled = true,
                Name = "Site CRS",
                CoordinateSystemName = "UTM-32N",
                EPSGCode = "32632",
                LocalReferenceX = 12.0,
                LocalReferenceY = 34.0,
                LocalReferenceZ = 5.0,
                SurveyEasting = 500000.0,
                SurveyNorthing = 6000000.0,
                SurveyElevation = 100.0,
                RotationToNorthDegrees = 45.0,
                Scale = 1.05,
                Source = "Manual"
            };

            transformService.ApplyReference(expectedRef, out _);

            var service = new ProjectStateService(transformService);
            var state = new ProjectStateSnapshot();

            // Act: Save
            await service.SaveProjectStateAsync(session, state);

            // Assert: verify written in database
            await using (var context = CreatedProjectDbContextFactory.Create(_tempFolder))
            {
                var dbRef = await context.CoordinateReferences.FirstOrDefaultAsync();
                Assert.NotNull(dbRef);
                Assert.Equal(expectedRef.IsEnabled, dbRef.IsEnabled);
                Assert.Equal(expectedRef.Name, dbRef.Name);
                Assert.Equal(expectedRef.CoordinateSystemName, dbRef.CoordinateSystemName);
                Assert.Equal(expectedRef.EPSGCode, dbRef.EPSGCode);
                Assert.Equal(expectedRef.LocalReferenceX, dbRef.LocalReferenceX);
                Assert.Equal(expectedRef.LocalReferenceY, dbRef.LocalReferenceY);
                Assert.Equal(expectedRef.LocalReferenceZ, dbRef.LocalReferenceZ);
                Assert.Equal(expectedRef.SurveyEasting, dbRef.SurveyEasting);
                Assert.Equal(expectedRef.SurveyNorthing, dbRef.SurveyNorthing);
                Assert.Equal(expectedRef.SurveyElevation, dbRef.SurveyElevation);
                Assert.Equal(expectedRef.RotationToNorthDegrees, dbRef.RotationToNorthDegrees);
                Assert.Equal(expectedRef.Scale, dbRef.Scale);
                Assert.Equal(expectedRef.Source, dbRef.Source);
            }

            // Reset service reference
            transformService.ResetReference();
            Assert.False(transformService.IsEnabled);

            // Act: Load
            await service.LoadProjectStateAsync(session);

            // Assert: verify applied to service
            var actualRef = transformService.CurrentReference;
            Assert.True(transformService.IsEnabled);
            Assert.Equal(expectedRef.Name, actualRef.Name);
            Assert.Equal(expectedRef.CoordinateSystemName, actualRef.CoordinateSystemName);
            Assert.Equal(expectedRef.EPSGCode, actualRef.EPSGCode);
            Assert.Equal(expectedRef.LocalReferenceX, actualRef.LocalReferenceX);
            Assert.Equal(expectedRef.LocalReferenceY, actualRef.LocalReferenceY);
            Assert.Equal(expectedRef.LocalReferenceZ, actualRef.LocalReferenceZ);
            Assert.Equal(expectedRef.SurveyEasting, actualRef.SurveyEasting);
            Assert.Equal(expectedRef.SurveyNorthing, actualRef.SurveyNorthing);
            Assert.Equal(expectedRef.SurveyElevation, actualRef.SurveyElevation);
            Assert.Equal(expectedRef.RotationToNorthDegrees, actualRef.RotationToNorthDegrees);
            Assert.Equal(expectedRef.Scale, actualRef.Scale);
            Assert.Equal(expectedRef.Source, actualRef.Source);
        }

        [Fact]
        public async Task TestLoadCoordinateReference_FallbackToJson()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            // Create JSON file manually
            var expectedRef = new ProjectCoordinateReference
            {
                IsEnabled = true,
                Name = "JSON CRS",
                LocalReferenceX = 5.0,
                SurveyEasting = 2000.0,
                SurveyNorthing = 3000.0,
                Scale = 1.0
            };
            var refFilePath = Path.Combine(_tempFolder, "coordinate_reference.json");
            var json = JsonSerializer.Serialize(expectedRef);
            await File.WriteAllTextAsync(refFilePath, json);

            var transformService = new ProjectCoordinateTransformService();
            var service = new ProjectStateService(transformService);

            // Act: Load
            await service.LoadProjectStateAsync(session);

            // Assert: verify applied from JSON
            var actualRef = transformService.CurrentReference;
            Assert.True(transformService.IsEnabled);
            Assert.Equal(expectedRef.Name, actualRef.Name);
            Assert.Equal(expectedRef.LocalReferenceX, actualRef.LocalReferenceX);
            Assert.Equal(expectedRef.SurveyEasting, actualRef.SurveyEasting);
            Assert.Equal(expectedRef.SurveyNorthing, actualRef.SurveyNorthing);
        }

        [Fact]
        public async Task TestLoadCoordinateReference_EmptyProjectNoFailure()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            var transformService = new ProjectCoordinateTransformService();
            var service = new ProjectStateService(transformService);

            // Act: Load
            await service.LoadProjectStateAsync(session);

            // Assert: Should not fail and should remain disabled
            Assert.False(transformService.IsEnabled);
        }

        [Fact]
        public async Task TestSaveAndLoad_DoesNotModifyElementCoordinates()
        {
            // Arrange
            var dbPath = _tempFolder;
            await using (var context = CreatedProjectDbContextFactory.Create(dbPath))
            {
                await context.Database.MigrateAsync();
                var projectInfo = new ProjectInfoEntity { Name = "Test Project" };
                await context.ProjectInfo.AddAsync(projectInfo);
                await context.SaveChangesAsync();
            }

            // Create a fake IFC file on disk to simulate copying
            var modelFolder = Path.Combine(_tempFolder, "Models", "IFC");
            Directory.CreateDirectory(modelFolder);
            var fakeIfcPath = Path.Combine(_tempFolder, "source.ifc");
            await File.WriteAllTextAsync(fakeIfcPath, "FAKE IFC FILE CONTENT");

            var session = new ProjectSession
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Test Project",
                ProjectFolderPath = _tempFolder,
                ProjectInfoId = 1
            };

            var originalElements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL_TEST",
                    COG_M = new DomainPoint3D(10.0, 20.0, 30.0),
                    ElementCOG = new DomainPoint3D(10.0, 20.0, 30.0)
                }
            };

            // Set up a transformed coordinate system with active offsets
            var transformService = new ProjectCoordinateTransformService();
            transformService.ApplyReference(new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0
            }, out _);

            var service = new ProjectStateService(transformService);
            var state = new ProjectStateSnapshot
            {
                IfcFile = new ProjectIfcFileState
                {
                    AbsolutePath = fakeIfcPath,
                    FileName = "source.ifc",
                    Role = "Primary",
                    InsertionOffset = new DomainPoint3D(0, 0, 0),
                    RotationDegrees = 0,
                    Scale = 1
                },
                IfcElements = originalElements
            };

            // Act: Save
            await service.SaveProjectStateAsync(session, state);

            // Assert: verify coordinates in SQLite remain 10.0, 20.0, 30.0
            await using (var context = CreatedProjectDbContextFactory.Create(_tempFolder))
            {
                var el = await context.IfcElements.FirstOrDefaultAsync();
                Assert.NotNull(el);
                Assert.Equal(10.0, el.COG_X);
                Assert.Equal(20.0, el.COG_Y);
                Assert.Equal(30.0, el.COG_Z);
            }
        }

        // =========================================================================
        // Manual Stub Classes for testing
        // =========================================================================

        public class StubIfcReaderRepository : IIfcReaderRepository
        {
            public bool ReadElementsSimpleCalled { get; set; }
            public bool ReadElementsWithProgressCalled { get; set; }
            public bool InitializeSessionForRenderingCalled { get; set; }

            public List<PrecastElement> ReadElements(string ifcPath)
            {
                ReadElementsSimpleCalled = true;
                return new List<PrecastElement>();
            }

            public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options)
            {
                ReadElementsSimpleCalled = true;
                return new List<PrecastElement>();
            }

            public List<PrecastElement> ReadElements(string path, IProgress<double> progress)
            {
                ReadElementsWithProgressCalled = true;
                return new List<PrecastElement>();
            }

            public List<PrecastElement> ReadElements(
                string path,
                IProgress<double> progress,
                IfcReadingOptions? options,
                CancellationToken cancellationToken = default)
            {
                ReadElementsWithProgressCalled = true;
                return new List<PrecastElement>();
            }

            public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default)
            {
                InitializeSessionForRenderingCalled = true;
                return Task.CompletedTask;
            }

            public double ModelLengthFactor => 1.0;
            public double? ModelGroundElevation => 0.0;

            public IfcGeoreferencingResult? GetGeoreferencingData() => null;

            public void ClearData() { }
            public void Dispose() { }
        }

        public class StubIfcSceneOperationService : IIfcSceneOperationService
        {
            public bool ReplaceIfcModelCalled { get; set; }
            public List<PrecastElement> LastReplacedElements { get; set; } = new();

            public List<PrecastElement> GetElements() => new();

            public List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset)
            {
                return elements.ToList();
            }

            public List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements)
            {
                ReplaceIfcModelCalled = true;
                LastReplacedElements = elements.ToList();
                return elements.ToList();
            }

            public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements) => new();
            public List<PrecastElement> MoveIfcModel(DomainPoint3D delta) => new();
            public void DeleteIfcModel(bool clearReaderData = true) { }
            public bool HasIfcModel => false;
        }

        public class StubSceneService : ISceneService
        {
            public List<PrecastElement> GetElements() => new();
            public void SetElements(List<PrecastElement> elements) { }
            public void ClearElements() { }

            public List<PrecastElement> GetSelectedElements() => new();
            public void SetSelectedElements(List<PrecastElement> elements) { }
            public void ClearSelectedElements() { }

            public event Action<List<PrecastElement>>? ElementsChanged;
            public event Action<List<PrecastElement>>? ElementsSelectionChanged;

            public List<GreenAreaZone> GetZones() => new();
            public void AddZone(GreenAreaZone zone) { }
            public void RemoveZone(GreenAreaZone zone) { }
            public void RemoveSelectedZone() { }
            public void ClearZones() { }
            public void UpdateZone(GreenAreaZone zone) { }

            public GreenAreaZone? SelectedZone => null;
            public void SetSelectedZone(GreenAreaZone? zone) { }
            public void ClearZoneSelection() { }

            public event Action<GreenAreaZone>? ZoneAdded;
            public event Action<GreenAreaZone>? ZoneRemoved;
            public event Action<GreenAreaZone>? ZoneUpdated;
            public event Action? ZonesCleared;
            public event Action<GreenAreaZone?>? ZoneSelected;

            public List<SceneCrane> GetCranes() => new();
            public void AddCrane(SceneCrane crane) { }
            public void RemoveCrane(SceneCrane crane) { }
            public void ClearCranes() { }

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
    }
}
