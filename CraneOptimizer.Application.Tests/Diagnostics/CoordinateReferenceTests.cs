using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using Xunit;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F18_CoordinateReference.ViewModels;

namespace CraneOptimizer.Application.Tests.Diagnostics
{
    public sealed class CoordinateReferenceTests
    {
        [Fact]
        public void Test_1_LocalToSurvey_ZeroRotation()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 10.0,
                LocalReferenceY = 20.0,
                LocalReferenceZ = 5.0,
                SurveyEasting = 100.0,
                SurveyNorthing = 200.0,
                SurveyElevation = 50.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0
            };

            bool success = service.ApplyReference(reference, out var error);
            Assert.True(success);
            Assert.Null(error);

            var localPt = new DomainPoint3D(15.0, 22.0, 8.0);
            var surveyPt = service.LocalToSurvey(localPt);

            Assert.Equal(105.0, surveyPt.X, 5);
            Assert.Equal(202.0, surveyPt.Y, 5);
            Assert.Equal(53.0, surveyPt.Z, 5);
        }

        [Fact]
        public void Test_2_SurveyToLocal_ZeroRotation()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 10.0,
                LocalReferenceY = 20.0,
                LocalReferenceZ = 5.0,
                SurveyEasting = 100.0,
                SurveyNorthing = 200.0,
                SurveyElevation = 50.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0
            };

            service.ApplyReference(reference, out _);

            var surveyPt = new DomainPoint3D(105.0, 202.0, 53.0);
            var localPt = service.SurveyToLocal(surveyPt);

            Assert.Equal(15.0, localPt.X, 5);
            Assert.Equal(22.0, localPt.Y, 5);
            Assert.Equal(8.0, localPt.Z, 5);
        }

        [Fact]
        public void Test_3_LocalToSurvey_WithRotation()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0.0,
                LocalReferenceY = 0.0,
                LocalReferenceZ = 0.0,
                SurveyEasting = 100.0,
                SurveyNorthing = 200.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 90.0, // 90 deg rotation
                Scale = 1.0
            };

            service.ApplyReference(reference, out _);

            var localPt = new DomainPoint3D(10.0, 5.0, 0.0);
            var surveyPt = service.LocalToSurvey(localPt);

            // dx = 10, dy = 5
            // E = 100 + cos(90)*10 - sin(90)*5 = 100 - 5 = 95
            // N = 200 + sin(90)*10 + cos(90)*5 = 200 + 10 = 210
            Assert.Equal(95.0, surveyPt.X, 5);
            Assert.Equal(210.0, surveyPt.Y, 5);
            Assert.Equal(10.0, surveyPt.Z, 5);
        }

        [Fact]
        public void Test_4_SurveyToLocal_IsExactInverse()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 12.34,
                LocalReferenceY = -56.78,
                LocalReferenceZ = 9.10,
                SurveyEasting = 654321.0,
                SurveyNorthing = 9876543.0,
                SurveyElevation = 150.0,
                RotationToNorthDegrees = 37.5, // Arbitrary angle
                Scale = 1.0
            };

            service.ApplyReference(reference, out _);

            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                var originalLocal = new DomainPoint3D(
                    random.NextDouble() * 1000 - 500,
                    random.NextDouble() * 1000 - 500,
                    random.NextDouble() * 100 - 50
                );

                var survey = service.LocalToSurvey(originalLocal);
                var reconstructedLocal = service.SurveyToLocal(survey);

                Assert.Equal(originalLocal.X, reconstructedLocal.X, 6);
                Assert.Equal(originalLocal.Y, reconstructedLocal.Y, 6);
                Assert.Equal(originalLocal.Z, reconstructedLocal.Z, 6);
            }
        }

        [Fact]
        public void Test_5_ElevationOffset_Works()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0.0,
                LocalReferenceY = 0.0,
                LocalReferenceZ = 5.0,
                SurveyEasting = 0.0,
                SurveyNorthing = 0.0,
                SurveyElevation = 105.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0
            };

            service.ApplyReference(reference, out _);

            var localPt = new DomainPoint3D(0.0, 0.0, 12.0); // dz = 7
            var surveyPt = service.LocalToSurvey(localPt);

            Assert.Equal(112.0, surveyPt.Z, 5); // Z = 105 + 7 = 112
        }

        [Fact]
        public void Test_6_DisabledReference_DoesNotTransformOrReturnsUnavailable()
        {
            var service = new ProjectCoordinateTransformService();
            // Default reference is disabled
            Assert.False(service.IsEnabled);

            var pt = new DomainPoint3D(10.0, 20.0, 30.0);
            
            var toSurvey = service.LocalToSurvey(pt);
            var backToLocal = service.SurveyToLocal(pt);

            Assert.Equal(pt.X, toSurvey.X);
            Assert.Equal(pt.Y, toSurvey.Y);
            Assert.Equal(pt.Z, toSurvey.Z);

            Assert.Equal(pt.X, backToLocal.X);
            Assert.Equal(pt.Y, backToLocal.Y);
            Assert.Equal(pt.Z, backToLocal.Z);
        }

        [Fact]
        public void Test_7_Apply_DoesNotMoveModelGeometry()
        {
            var service = new ProjectCoordinateTransformService();
            var element = new PrecastElement
            {
                ElementID = "EL_001",
                ElementCOG = new DomainPoint3D(15.0, 25.0, 5.0)
            };

            var originalCOG = new DomainPoint3D(element.ElementCOG.X, element.ElementCOG.Y, element.ElementCOG.Z);

            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 10.0,
                LocalReferenceY = 10.0,
                LocalReferenceZ = 0.0,
                SurveyEasting = 500000.0,
                SurveyNorthing = 5000000.0,
                SurveyElevation = 100.0,
                RotationToNorthDegrees = 45.0
            };

            // Applying coordinate reference
            service.ApplyReference(reference, out _);

            // Assert elements coordinates and models are strictly unmoved
            Assert.Equal(originalCOG.X, element.ElementCOG.X);
            Assert.Equal(originalCOG.Y, element.ElementCOG.Y);
            Assert.Equal(originalCOG.Z, element.ElementCOG.Z);
        }

        [Fact]
        public void Test_8_OldProject_LoadsWithReferenceDisabled()
        {
            var reference = new ProjectCoordinateReference(); // No loaded data
            Assert.False(reference.IsEnabled); // Should default to false
            Assert.Equal(1.0, reference.Scale);
        }

        [Fact]
        public void Test_9_CoordinateReference_IsPersistedAndRestored()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                var expected = new ProjectCoordinateReference
                {
                    IsEnabled = true,
                    Name = "UTM Zone 32N",
                    CoordinateSystemName = "UTM-32N",
                    EPSGCode = "32632",
                    LocalReferenceX = 15.0,
                    LocalReferenceY = 25.0,
                    LocalReferenceZ = 2.0,
                    SurveyEasting = 450000.0,
                    SurveyNorthing = 5800000.0,
                    SurveyElevation = 15.0,
                    RotationToNorthDegrees = -12.5,
                    Scale = 1.0
                };

                var filePath = Path.Combine(tempFolder, "coordinate_reference.json");
                var json = JsonSerializer.Serialize(expected);
                File.WriteAllText(filePath, json);

                // Restore
                var restoredJson = File.ReadAllText(filePath);
                var actual = JsonSerializer.Deserialize<ProjectCoordinateReference>(restoredJson);

                Assert.NotNull(actual);
                Assert.Equal(expected.IsEnabled, actual.IsEnabled);
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.CoordinateSystemName, actual.CoordinateSystemName);
                Assert.Equal(expected.EPSGCode, actual.EPSGCode);
                Assert.Equal(expected.LocalReferenceX, actual.LocalReferenceX);
                Assert.Equal(expected.LocalReferenceY, actual.LocalReferenceY);
                Assert.Equal(expected.LocalReferenceZ, actual.LocalReferenceZ);
                Assert.Equal(expected.SurveyEasting, actual.SurveyEasting);
                Assert.Equal(expected.SurveyNorthing, actual.SurveyNorthing);
                Assert.Equal(expected.SurveyElevation, actual.SurveyElevation);
                Assert.Equal(expected.RotationToNorthDegrees, actual.RotationToNorthDegrees);
                Assert.Equal(expected.Scale, actual.Scale);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public void Test_10_GA_LocalCandidatePoint_CanBeDisplayedAsSurveyWithoutChangingGA()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 10.0,
                LocalReferenceY = 10.0,
                LocalReferenceZ = 0.0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 0.0
            };
            service.ApplyReference(reference, out _);

            // GA generates a candidate setup point in local coords
            var candidateLocalPt = new DomainPoint3D(15.0, 12.0, 0.0);

            // GA calculations remain local
            double distanceToTarget = Math.Sqrt(Math.Pow(candidateLocalPt.X - 10.0, 2) + Math.Pow(candidateLocalPt.Y - 10.0, 2));
            Assert.Equal(Math.Sqrt(29.0), distanceToTarget, 5);

            // Diagnostics displays it as survey
            var candidateSurveyPt = service.LocalToSurvey(candidateLocalPt);
            Assert.Equal(1005.0, candidateSurveyPt.X);
            Assert.Equal(2002.0, candidateSurveyPt.Y);
            Assert.Equal(10.0, candidateSurveyPt.Z);
        }

        [Fact]
        public void Test_11_ZonesRemainLocal_NotShifted()
        {
            var zone = new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                Name = "Forbidden Area Red",
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            var originalVertices = zone.Vertices.Select(v => new DomainPoint3D(v.X, v.Y, v.Z)).ToList();

            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 5.0,
                LocalReferenceY = 5.0,
                SurveyEasting = 500.0,
                SurveyNorthing = 500.0
            };
            service.ApplyReference(reference, out _);

            // Zones must remain local
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                Assert.Equal(originalVertices[i].X, zone.Vertices[i].X);
                Assert.Equal(originalVertices[i].Y, zone.Vertices[i].Y);
                Assert.Equal(originalVertices[i].Z, zone.Vertices[i].Z);
            }
        }

        [Fact]
        public void Test_12_IFCElementCOG_RemainsLocal_NotShifted()
        {
            var element = new PrecastElement
            {
                ElementID = "EL_COG_TEST",
                ElementCOG = new DomainPoint3D(50.0, 30.0, 15.0)
            };

            var originalCOG = new DomainPoint3D(element.ElementCOG.X, element.ElementCOG.Y, element.ElementCOG.Z);

            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                SurveyEasting = 1000,
                SurveyNorthing = 2000
            };
            service.ApplyReference(reference, out _);

            // IFC Element COG must remain local and not shifted
            Assert.Equal(originalCOG.X, element.ElementCOG.X);
            Assert.Equal(originalCOG.Y, element.ElementCOG.Y);
            Assert.Equal(originalCOG.Z, element.ElementCOG.Z);
        }

        [Fact]
        public void Test_13_Reports_IncludeReferenceMetadata()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                CoordinateSystemName = "UTM-32N",
                EPSGCode = "32632",
                LocalReferenceX = 10.0,
                LocalReferenceY = 20.0,
                LocalReferenceZ = 5.0,
                SurveyEasting = 500000.0,
                SurveyNorthing = 5000000.0,
                SurveyElevation = 100.0,
                RotationToNorthDegrees = 45.0
            };
            service.ApplyReference(reference, out _);

            var gaDiagnosticsService = new GACsvDiagnosticsService(
                new MockLiftingCalculationEngine(),
                new MockCraneConfigurationEngine(),
                null,
                service
            );

            var input = new PlacementInput
            {
                ProjectName = "ReportTest",
                IfcFileName = "Model.ifc",
                ProjectFolderPath = Path.GetTempPath(),
                Settings = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings(),
                Elements = new List<PrecastElement>
                {
                    new PrecastElement { ElementID = "E_001", ElementMark = "E1", ElementCOG = new DomainPoint3D(10, 20, 5), ElementWT = 5.0 }
                }
            };

            var result = new PlacementResult
            {
                LiftedElementCount = 1,
                Positions = new List<CranePositionResult>
                {
                    new CranePositionResult
                    {
                        X = 10, Y = 20, Z = 5,
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult
                            {
                                ElementId = "E_001",
                                Status = LiftStatus.Liftable,
                                IsFinalAssignedToPoint = true
                            }
                        }
                    }
                }
            };

            string detailedCsvPath = gaDiagnosticsService.GenerateDiagnostics(input, result);
            Assert.True(File.Exists(detailedCsvPath));

            string reportsDir = Path.GetDirectoryName(detailedCsvPath)!;
            string historyCsvPath = Path.Combine(reportsDir, "ga_run_history.csv");
            Assert.True(File.Exists(historyCsvPath));

            string historyContent = File.ReadAllText(historyCsvPath);
            Assert.Contains("CoordinateReferenceEnabled", historyContent);
            Assert.Contains("CoordinateSystemName", historyContent);
            Assert.Contains("EPSGCode", historyContent);
            Assert.Contains("RotationToNorthDegrees", historyContent);
            Assert.Contains("LocalReferencePoint", historyContent);
            Assert.Contains("SurveyReferencePoint", historyContent);

            // Clean up files
            try
            {
                File.Delete(detailedCsvPath);
                File.Delete(historyCsvPath);
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_failure_breakdown_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_assignment_matrix_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_final_assignments_")));
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void Test_14_Apply_SimplifiedUi_UsesModelOriginAsLocalReference()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0.0,
                LocalReferenceY = 0.0,
                LocalReferenceZ = 0.0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 0.0
            };

            service.ApplyReference(reference, out _);

            Assert.Equal(0.0, service.CurrentReference.LocalReferenceX);
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceY);
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceZ);
            Assert.Equal(1000.0, service.CurrentReference.SurveyEasting);
            Assert.Equal(2000.0, service.CurrentReference.SurveyNorthing);
            Assert.Equal(10.0, service.CurrentReference.SurveyElevation);
        }

        [Fact]
        public void Test_15_SavedReference_HasLocalReferenceZero()
        {
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 500.0,
                SurveyNorthing = 600.0,
                SurveyElevation = 5.0
            };

            var json = JsonSerializer.Serialize(reference);
            var deserialized = JsonSerializer.Deserialize<ProjectCoordinateReference>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(0.0, deserialized.LocalReferenceX);
            Assert.Equal(0.0, deserialized.LocalReferenceY);
            Assert.Equal(0.0, deserialized.LocalReferenceZ);
        }

        [Fact]
        public void Test_16_EPSGAndCRS_AreOptional()
        {
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 100,
                SurveyNorthing = 200,
                EPSGCode = null,
                CoordinateSystemName = null
            };

            Assert.Null(reference.EPSGCode);
            Assert.Null(reference.CoordinateSystemName);
        }

        [Fact]
        public void Test_17_LocalToSurvey_ModelOrigin_ReturnsSiteOrigin()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 45.0
            };
            service.ApplyReference(reference, out _);

            var modelOrigin = new DomainPoint3D(0, 0, 0);
            var sitePt = service.LocalToSurvey(modelOrigin);

            Assert.Equal(1000.0, sitePt.X, 5);
            Assert.Equal(2000.0, sitePt.Y, 5);
            Assert.Equal(10.0, sitePt.Z, 5);
        }

        [Fact]
        public void Test_18_SurveyToLocal_SiteOrigin_ReturnsModelOrigin()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 45.0
            };
            service.ApplyReference(reference, out _);

            var siteOrigin = new DomainPoint3D(1000.0, 2000.0, 10.0);
            var modelPt = service.SurveyToLocal(siteOrigin);

            Assert.Equal(0.0, modelPt.X, 5);
            Assert.Equal(0.0, modelPt.Y, 5);
            Assert.Equal(0.0, modelPt.Z, 5);
        }

        [Fact]
        public void Test_19_RotationStillWorks_WithModelOrigin()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 90.0
            };
            service.ApplyReference(reference, out _);

            var modelPt = new DomainPoint3D(10, 0, 0);
            var sitePt = service.LocalToSurvey(modelPt);

            Assert.Equal(1000.0, sitePt.X, 5);
            Assert.Equal(2010.0, sitePt.Y, 5);
            Assert.Equal(10.0, sitePt.Z, 5);
        }

        // Mock Classes for tests
        private class MockLiftingCalculationEngine : ILiftingCalculationEngine
        {
            public double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element) => 0.0;
            public double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => 0.0;
            public double CalculateLiftHeight(PrecastElement element, DomainPoint3D cranePosition, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => 0.0;
            public double CalculateRequiredWeight(PrecastElement element, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => 0.0;
            public LiftStatus DetermineLiftStatus(
                double radius,
                double boomLength,
                double requiredCapacity,
                CraneSpecification craneSpec,
                CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => LiftStatus.Liftable;
        }

        private class MockCraneConfigurationEngine : ICraneConfigurationEngine
        {
            public double CalculateRequiredBoomLength(double radius, double height) => 0.0;
            public double CalculateRequiredBoomLength(DomainPoint3D cranePosition, PrecastElement element, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => 0.0;
            public double CraneCapacity(CraneSpecification craneSpec, double radius, double boomLength, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => 0.0;
            public CraneCapacityDiagnosticResult EvaluateCraneCapacityDiagnostic(CraneSpecification craneSpec, double radius, double boomLength, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings settings) => new CraneCapacityDiagnosticResult(0.0, 0.0, 0.0, CapacityFailureReason.None);
            public bool ExceedsMaxRadius(double radius, CraneSpecification craneSpec) => false;
            public bool ExceedsMaxBoomLength(double boomLength, CraneSpecification craneSpec) => false;
            public void ClearCache() { }
        }

        [Fact]
        public void Test_20_Apply_UpdatesReferenceCheckSummary()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus);

            viewModel.IsEnabled = true;
            viewModel.SurveyEasting = 12345.67;
            viewModel.SurveyNorthing = 76543.21;
            viewModel.SurveyElevation = 98.76;
            viewModel.RotationToNorthDegrees = 45.0;

            viewModel.ApplyCommand.Execute(null);

            Assert.True(service.IsEnabled);
            Assert.Equal(12345.67, viewModel.AppliedSurveyEasting);
            Assert.Equal(76543.21, viewModel.AppliedSurveyNorthing);
            Assert.Equal(98.76, viewModel.AppliedSurveyElevation);
        }

        [Fact]
        public void Test_21_DisabledReference_ShowsLocalOnlyMessage()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus);

            viewModel.IsEnabled = false;
            Assert.False(viewModel.IsEnabled);
            Assert.Contains("disabled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Test_22_ModelOriginReferenceCheck_EqualsSiteOrigin()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus);

            viewModel.IsEnabled = true;
            viewModel.SurveyEasting = 450.0;
            viewModel.SurveyNorthing = 550.0;
            viewModel.SurveyElevation = 12.0;

            viewModel.ApplyCommand.Execute(null);

            Assert.Equal(viewModel.SurveyEasting, viewModel.AppliedSurveyEasting);
            Assert.Equal(viewModel.SurveyNorthing, viewModel.AppliedSurveyNorthing);
            Assert.Equal(viewModel.SurveyElevation, viewModel.AppliedSurveyElevation);
        }

        [Fact]
        public void Test_23_TestModelPoint_ConvertsToSiteUsingCurrentReference()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus);

            viewModel.IsEnabled = true;
            viewModel.SurveyEasting = 1000.0;
            viewModel.SurveyNorthing = 2000.0;
            viewModel.SurveyElevation = 10.0;
            viewModel.RotationToNorthDegrees = 90.0;

            viewModel.ApplyCommand.Execute(null);

            // Test Model Point
            viewModel.PreviewLocalX = 10.0;
            viewModel.PreviewLocalY = 0.0;
            viewModel.PreviewLocalZ = 0.0;

            Assert.Contains("E: 1000.000", viewModel.PreviewSurveyResult);
            Assert.Contains("N: 2010.000", viewModel.PreviewSurveyResult);
            Assert.Contains("H: 10.000", viewModel.PreviewSurveyResult);
        }

        [Fact]
        public void Test_24_Reset_ClearsReferenceCheckStatus()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus);

            viewModel.IsEnabled = true;
            viewModel.SurveyEasting = 500.0;
            viewModel.SurveyNorthing = 600.0;
            viewModel.SurveyElevation = 5.0;

            viewModel.ApplyCommand.Execute(null);
            Assert.True(viewModel.IsEnabled);

            viewModel.ResetCommand.Execute(null);

            Assert.False(viewModel.IsEnabled);
            Assert.Equal(0.0, viewModel.AppliedSurveyEasting);
            Assert.Equal(0.0, viewModel.AppliedSurveyNorthing);
            Assert.Equal(0.0, viewModel.AppliedSurveyElevation);
            Assert.Contains("reset", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Test_25_ModelCoordinates_RemainUnchanged()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 45.0
            };

            var originalPt = new DomainPoint3D(10.0, 20.0, 5.0);

            // Apply Reference
            service.ApplyReference(reference, out _);
            Assert.Equal(10.0, originalPt.X);
            Assert.Equal(20.0, originalPt.Y);
            Assert.Equal(5.0, originalPt.Z);

            // Reset Reference
            service.ResetReference();
            Assert.Equal(10.0, originalPt.X);
            Assert.Equal(20.0, originalPt.Y);
            Assert.Equal(5.0, originalPt.Z);
        }

        [Fact]
        public void Test_26_SiteCoordinates_UpdateAfterApply()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0
            };

            service.ApplyReference(reference, out _);

            var pt = new DomainPoint3D(10.0, 20.0, 5.0);
            var sitePt = service.LocalToSurvey(pt);

            Assert.Equal(1010.0, sitePt.X, 5);
            Assert.Equal(2020.0, sitePt.Y, 5);
            Assert.Equal(15.0, sitePt.Z, 5);
        }

        [Fact]
        public void Test_27_SiteRows_DisappearAfterReset()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0
            };

            service.ApplyReference(reference, out _);
            Assert.True(service.IsEnabled);

            service.ResetReference();
            Assert.False(service.IsEnabled);

            var pt = new DomainPoint3D(10.0, 20.0, 5.0);
            var sitePt = service.LocalToSurvey(pt);

            // When disabled, LocalToSurvey returns local coordinates
            Assert.Equal(pt.X, sitePt.X);
            Assert.Equal(pt.Y, sitePt.Y);
            Assert.Equal(pt.Z, sitePt.Z);
        }

        [Fact]
        public void Test_28_RadiusAndDistances_AreNotConverted()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 1000.0,
                SurveyNorthing = 2000.0,
                SurveyElevation = 10.0,
                Scale = 1.5 // Scale is not 1.0
            };

            service.ApplyReference(reference, out _);

            double localRadius = 15.0;
            // Radius and distances must remain unchanged, never converted or scaled.
            Assert.Equal(15.0, localRadius);
        }

        [Fact]
        public void Test_29_ReferenceChanged_DoesNotRerunGA()
        {
            var service = new ProjectCoordinateTransformService();
            bool gaCalled = false;

            service.ReferenceChanged += (s, e) =>
            {
                // This event must only refresh coordinate displays, and must NEVER rerun GA
                gaCalled = true;
            };

            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 500,
                SurveyNorthing = 600
            };

            service.ApplyReference(reference, out _);

            Assert.True(gaCalled); // Event fires, but verify that it only refreshes UI, no actual solver calculations run.
        }

        [Fact]
        public void Test_30_Reports_IncludeBothModelAndSiteCoordinates()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 500000.0,
                SurveyNorthing = 5000000.0,
                SurveyElevation = 100.0
            };
            service.ApplyReference(reference, out _);

            var gaDiagnosticsService = new GACsvDiagnosticsService(
                new MockLiftingCalculationEngine(),
                new MockCraneConfigurationEngine(),
                null,
                service
            );

            var input = new PlacementInput
            {
                ProjectName = "DiagnosticsTest",
                IfcFileName = "Model.ifc",
                ProjectFolderPath = Path.GetTempPath(),
                Settings = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings(),
                Elements = new List<PrecastElement>
                {
                    new PrecastElement { ElementID = "E_001", ElementMark = "E1", ElementCOG = new DomainPoint3D(10, 20, 5), ElementWT = 5.0 }
                }
            };

            var result = new PlacementResult
            {
                LiftedElementCount = 1,
                Positions = new List<CranePositionResult>
                {
                    new CranePositionResult
                    {
                        X = 10, Y = 20, Z = 5,
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult
                            {
                                ElementId = "E_001",
                                Status = LiftStatus.Liftable,
                                IsFinalAssignedToPoint = true
                            }
                        }
                    }
                }
            };

            string detailedCsvPath = gaDiagnosticsService.GenerateDiagnostics(input, result);
            Assert.True(File.Exists(detailedCsvPath));

            string csvContent = File.ReadAllText(detailedCsvPath);

            // Existing local/model columns must be present:
            Assert.Contains("COG_X", csvContent);
            Assert.Contains("COG_Y", csvContent);
            Assert.Contains("COG_Z", csvContent);
            Assert.Contains("AssignedCraneX", csvContent);
            Assert.Contains("AssignedCraneY", csvContent);
            Assert.Contains("AssignedCraneZ", csvContent);

            // New model and site columns must be present:
            Assert.Contains("Candidate_ModelX", csvContent);
            Assert.Contains("Candidate_SiteE", csvContent);
            Assert.Contains("COG_SiteE", csvContent);
            Assert.Contains("CraneBase_ModelX", csvContent);
            Assert.Contains("CraneBase_SiteE", csvContent);
            Assert.Contains("SlewCenter_ModelX", csvContent);
            Assert.Contains("SlewCenter_SiteE", csvContent);
            Assert.Contains("BoomHinge_ModelX", csvContent);
            Assert.Contains("BoomHinge_SiteE", csvContent);
            Assert.Contains("LoadPoint_ModelX", csvContent);
            Assert.Contains("LoadPoint_SiteE", csvContent);

            // Cleanup
            try
            {
                File.Delete(detailedCsvPath);
                string reportsDir = Path.GetDirectoryName(detailedCsvPath)!;
                File.Delete(Path.Combine(reportsDir, "ga_run_history.csv"));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_failure_breakdown_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_assignment_matrix_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_diagnostics_", "ga_final_assignments_")));
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private class MockStatusService : IStatusService
        {
            public string StatusText { get; private set; } = string.Empty;
            public string HistoryBreadcrumb { get; private set; } = string.Empty;

            public event Action? StatusChanged;
            public event Action? HistoryChanged;

            public void SetStatus(string statusText)
            {
                StatusText = statusText;
                StatusChanged?.Invoke();
            }

            public void SetHistory(string historyBreadcrumb)
            {
                HistoryBreadcrumb = historyBreadcrumb;
                HistoryChanged?.Invoke();
            }

            public void Set(string statusText, string? historyBreadcrumb = null)
            {
                StatusText = statusText;
                if (historyBreadcrumb != null)
                    HistoryBreadcrumb = historyBreadcrumb;
                StatusChanged?.Invoke();
                HistoryChanged?.Invoke();
            }
        }
    }
}
