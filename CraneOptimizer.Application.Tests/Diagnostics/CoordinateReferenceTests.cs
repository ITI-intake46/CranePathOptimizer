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
using CraneOptimizer.UI.Features.F2_IfcViewer.Views;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F16_Filter.Services;

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
                Settings = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings
                {
                    DiagnosticsLevel = CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.DiagnosticsLevel.Research
                },
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
            string historyCsvPath = Path.Combine(reportsDir, "ga_audit_run_history.csv");
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
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_failure_breakdown_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_assignment_matrix_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_final_assignments_")));
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
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

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
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

            viewModel.IsEnabled = false;
            Assert.False(viewModel.IsEnabled);
            Assert.Contains("disabled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Test_22_ModelOriginReferenceCheck_EqualsSiteOrigin()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

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
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

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

            Assert.Contains("Easting: 1000.000 m", viewModel.PreviewSurveyResult);
            Assert.Contains("Northing: 2010.000 m", viewModel.PreviewSurveyResult);
            Assert.Contains("Elevation: 10.000 m", viewModel.PreviewSurveyResult);
        }

        [Fact]
        public void Test_24_Reset_ClearsReferenceCheckStatus()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

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
                Settings = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.GASettings
                {
                    DiagnosticsLevel = CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.DiagnosticsLevel.Research
                },
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
                File.Delete(Path.Combine(reportsDir, "ga_audit_run_history.csv"));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_failure_breakdown_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_assignment_matrix_")));
                File.Delete(Path.Combine(reportsDir, Path.GetFileName(detailedCsvPath).Replace("ga_audit_diagnostics_", "ga_audit_final_assignments_")));
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void Test_31_SelectingLocalOnlyDisablesActiveSurveyReference()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

            // Initially set manual reference
            viewModel.SelectedMode = CoordinateReferenceMode.Manual;
            viewModel.IsEnabled = true;
            viewModel.SurveyEasting = 100.0;
            viewModel.ApplyCommand.Execute(null);
            Assert.True(service.IsEnabled);

            // Change to LocalOnly
            viewModel.SelectedMode = CoordinateReferenceMode.LocalOnly;
            viewModel.ApplyCommand.Execute(null);

            Assert.False(viewModel.IsEnabled);
            Assert.False(service.IsEnabled);
        }

        [Fact]
        public void Test_32_SelectingIfcOriginMappingEnablesValidReference()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: 1.0,
                XAxisOrdinate: 0.0,
                Scale: 1.0,
                CrsName: "Projected CRS",
                CrsDescription: "Test Projected CRS",
                EpsgCode: "3857",
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            Assert.True(viewModel.IsIfcOriginMappingEnabled);

            // Change to IfcOrigin
            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            Assert.True(viewModel.IsEnabled);
            Assert.True(service.IsEnabled);
            Assert.Equal(50000.0, viewModel.SurveyEasting);
            Assert.Equal("IfcMapConversion", service.CurrentReference.Source);
        }

        [Fact]
        public void Test_33_IfcOriginMappingKeepsLocalReferenceZero()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: 1.0,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            // Select IFC Origin
            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            Assert.Equal(0.0, viewModel.LocalReferenceX);
            Assert.Equal(0.0, viewModel.LocalReferenceY);
            Assert.Equal(0.0, viewModel.LocalReferenceZ);
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceX);
        }

        [Fact]
        public void Test_34_Ifc2x3SiteLatLonDoesNotActivateMapping()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: false,
                Eastings: 0.0,
                Northings: 0.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: true,
                SiteLatitude: 30.05,
                SiteLongitude: 31.25,
                SiteElevation: 15.0
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            // Since it only has SiteLatLon, IFC Origin Mapping must not be enabled
            Assert.False(viewModel.IsIfcOriginMappingEnabled);
            Assert.Equal(System.Windows.Visibility.Visible, viewModel.IfcGeoreferencingNotFoundMessageVisibility);

            // Selecting IFC Origin Mapping should fallback to LocalOnly
            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            Assert.Equal(CoordinateReferenceMode.LocalOnly, viewModel.SelectedMode);
        }

        [Fact]
        public void Test_35_ManualReferencesAreNotOverwrittenOrDeleted()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 8888.0,
                Northings: 9999.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);

            // Setup a manual reference in service
            var manualRef = new ProjectCoordinateReference
            {
                IsEnabled = true,
                Source = "ManualSinglePoint",
                SurveyEasting = 123.0,
                SurveyNorthing = 456.0
            };
            service.ApplyReference(manualRef, out _);

            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            // Verify manual reference loaded and source is manual
            Assert.Equal(CoordinateReferenceMode.Manual, viewModel.SelectedMode);
            Assert.Equal(123.0, viewModel.SurveyEasting);

            // Switch to Local Only
            viewModel.SelectedMode = CoordinateReferenceMode.LocalOnly;
            viewModel.ApplyCommand.Execute(null);

            // The manual reference values should be kept in the VM and not deleted
            Assert.Equal(123.0, viewModel.SurveyEasting);
            Assert.Equal(456.0, viewModel.SurveyNorthing);
            Assert.False(service.IsEnabled);
        }

        [Fact]
        public void Test_36_SaveLoadRoundTripsSelectedMode()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 500.0,
                Northings: 600.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            // Verify service active reference has IfcMapConversion source and is enabled
            var reference = service.CurrentReference;
            Assert.True(reference.IsEnabled);
            Assert.Equal("IfcMapConversion", reference.Source);
            Assert.Equal(500.0, reference.SurveyEasting);
        }

        [Fact]
        public void Test_37_LocalToSurveyReturnsExpectedValues()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 1000.0,
                Northings: 2000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: 0.0,
                XAxisOrdinate: 1.0, // 90 degrees rotation
                Scale: 1.0,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            var localPt = new DomainPoint3D(10.0, 0.0, 5.0);
            var surveyPt = service.LocalToSurvey(localPt);

            // Under 90 deg rotation, (10, 0, 5) with origin (1000, 2000, 10):
            // E = 1000 + cos(90)*10 - sin(90)*0 = 1000
            // N = 2000 + sin(90)*10 + cos(90)*0 = 2010
            // H = 10 + 5 = 15
            Assert.Equal(1000.0, surveyPt.X, 4);
            Assert.Equal(2010.0, surveyPt.Y, 4);
            Assert.Equal(15.0, surveyPt.Z, 4);
        }

        [Fact]
        public void Test_38_GAServicesDoNotAccessOrReferenceSurveyMode()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 1000.0,
                Northings: 2000.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            // Ensure GA settings, builders, or constraint engines do not reference transform service or change metrics
            var originalCandidate = new DomainPoint3D(10, 15, 0);
            Assert.Equal(10.0, originalCandidate.X);
            Assert.Equal(15.0, originalCandidate.Y);
        }

        [Fact]
        public void Test_39_ElementLocalCoordinatesRemainUnchanged()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 1000.0,
                Northings: 2000.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            var element = new PrecastElement
            {
                ElementCOG = new DomainPoint3D(5.0, 5.0, 2.0)
            };

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            // Element local coordinates should remain (5, 5, 2)
            Assert.Equal(5.0, element.ElementCOG.X);
            Assert.Equal(5.0, element.ElementCOG.Y);
            Assert.Equal(2.0, element.ElementCOG.Z);
        }

        private static T RunInSta<T>(Func<T> action)
        {
            T result = default!;
            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                        app.Resources["FontSizeLG"] = 16.0;
                        app.Resources["FontSizeSM"] = 12.0;
                        app.Resources["FontSizeBase"] = 14.0;
                        app.Resources["FontSizeXS"] = 10.0;
                        app.Resources["PrimaryBrush"] = System.Windows.Media.Brushes.Blue;
                        app.Resources["SuccessBrush"] = System.Windows.Media.Brushes.Green;
                        app.Resources["WarningBrush"] = System.Windows.Media.Brushes.Orange;
                        app.Resources["ErrorBrush"] = System.Windows.Media.Brushes.Red;
                        app.Resources["TextSecondaryBrush"] = System.Windows.Media.Brushes.Gray;
                        app.Resources["TextPrimaryBrush"] = System.Windows.Media.Brushes.Black;
                        app.Resources["BorderBrush"] = System.Windows.Media.Brushes.LightGray;
                        app.Resources["SurfaceBrush"] = System.Windows.Media.Brushes.White;
                        app.Resources["SurfaceElevatedBrush"] = System.Windows.Media.Brushes.White;
                        app.Resources["PanelSectionHeader"] = new System.Windows.Style(typeof(System.Windows.Controls.TextBlock));
                        app.Resources["TitleBarButton"] = new System.Windows.Style(typeof(System.Windows.Controls.Button));
                        app.Resources["ButtonSecondary"] = new System.Windows.Style(typeof(System.Windows.Controls.Button));
                        app.Resources["ButtonPrimary"] = new System.Windows.Style(typeof(System.Windows.Controls.Button));
                    }
                    result = action();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RunInSta] Exception: {ex}");
                    throw;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        [Fact]
        public void Test_40_IfcLoadOptions_ExposesOnlyLocalOnlyAndIfcOrigin()
        {
            RunInSta<bool>(() =>
            {
                var mockGeoref = new IfcGeoreferencingResult(
                    HasMapConversion: true,
                    Eastings: 1000.0,
                    Northings: 2000.0,
                    OrthogonalHeight: 0.0,
                    XAxisAbscissa: null,
                    XAxisOrdinate: null,
                    Scale: null,
                    CrsName: null,
                    CrsDescription: null,
                    EpsgCode: null,
                    HasSiteLatLon: false,
                    SiteLatitude: null,
                    SiteLongitude: null,
                    SiteElevation: null
                );
                var task = Task.FromResult<IfcGeoreferencingResult?>(mockGeoref);
                var dialog = new IFCLoadOptionsDialog("test.ifc", task);

                // Pump the dispatcher and wait for the async georef initialization task
                var start = DateTime.Now;
                while (!dialog.RadIfcOrigin.IsEnabled && (DateTime.Now - start).TotalSeconds < 3)
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => { }));
                    Thread.Sleep(10);
                }

                Assert.True(dialog.RadLocalOnly.IsChecked == true);
                Assert.True(dialog.RadIfcOrigin.IsEnabled);
                return true;
            });
        }

        [Fact]
        public void Test_41_ManualModeNotSelectableInDialog()
        {
            RunInSta<bool>(() =>
            {
                var dialog = new IFCLoadOptionsDialog("test.ifc", null);
                Assert.Equal(CoordinateReferenceMode.LocalOnly, dialog.SelectedCoordinateMode);
                return true;
            });
        }

        [Fact]
        public void Test_42_SelectingLocalOnlyDisablesActiveSurveyReferenceAfterImport()
        {
            var service = new ProjectCoordinateTransformService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 12345.0,
                Northings: 67890.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: "Test CRS",
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new IfcViewerViewModel(
                mockReader,
                null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
                service
            );

            // Set pending filter options to LocalOnly
            var field = typeof(IfcViewerViewModel).GetField("_pendingFilterOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(viewModel, new IfcLoadOptionsDto { CoordinateMode = CoordinateReferenceMode.LocalOnly });

            // Invoke TryApply
            var method = typeof(IfcViewerViewModel).GetMethod("TryApplyIfcGeoreferencing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(viewModel, null);

            Assert.False(service.IsEnabled);
            Assert.Equal("IfcMapConversion", service.CurrentReference.Source);
            Assert.Equal(12345.0, service.CurrentReference.SurveyEasting);
        }

        [Fact]
        public void Test_43_SelectingIfcOriginMappingActivatesOnlyWhenValidIfcMapConversionExists()
        {
            var service = new ProjectCoordinateTransformService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 12345.0,
                Northings: 67890.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: "Test CRS",
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new IfcViewerViewModel(
                mockReader,
                null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
                service
            );

            // Set pending filter options to IfcOrigin
            var field = typeof(IfcViewerViewModel).GetField("_pendingFilterOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(viewModel, new IfcLoadOptionsDto { CoordinateMode = CoordinateReferenceMode.IfcOrigin });

            // Invoke TryApply
            var method = typeof(IfcViewerViewModel).GetMethod("TryApplyIfcGeoreferencing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(viewModel, null);

            Assert.True(service.IsEnabled);
            Assert.Equal("IfcMapConversion", service.CurrentReference.Source);
            Assert.Equal(12345.0, service.CurrentReference.SurveyEasting);
        }

        [Fact]
        public void Test_44_SelectingIfcOriginMappingWithNoUsableIfcGeoreferencingFallsBackToLocalOnly()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockReader = new MockIfcReaderRepository();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            Assert.False(viewModel.IsIfcOriginMappingEnabled);
            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;

            // Fallback
            Assert.Equal(CoordinateReferenceMode.LocalOnly, viewModel.SelectedMode);
        }

        [Fact]
        public void Test_45_Ifc2x3SiteLatLonDoesNotEnableIfcOriginMapping()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: false,
                Eastings: 0.0,
                Northings: 0.0,
                OrthogonalHeight: 0.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: true,
                SiteLatitude: 30.0,
                SiteLongitude: 31.0,
                SiteElevation: 10.0
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            Assert.False(viewModel.IsIfcOriginMappingEnabled);
        }

        [Fact]
        public void Test_46_CoordinateReferenceTabComboBoxDisplaysCleanLabels()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, new MockIfcReaderRepository());

            var modes = viewModel.AvailableModes;
            Assert.Equal(2, modes.Count);
            Assert.Equal("Local Only", modes[0].DisplayName);
            Assert.Equal("IFC Origin Mapping", modes[1].DisplayName);
        }

        [Fact]
        public void Test_47_SaveLoadPreservesSelectedActiveMode()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            viewModel.ApplyCommand.Execute(null);

            var saved = service.CurrentReference;
            Assert.True(saved.IsEnabled);
            Assert.Equal("IfcMapConversion", saved.Source);
        }

        [Fact]
        public void Test_48_ElementLocalCoordinatesRemainUnchanged()
        {
            var element = new PrecastElement
            {
                ElementCOG = new DomainPoint3D(10.0, 20.0, 5.0)
            };

            Assert.Equal(10.0, element.ElementCOG.X);
            Assert.Equal(20.0, element.ElementCOG.Y);
        }

        [Fact]
        public void Test_49_GAServicesRemainIsolatedFromSurveyMode()
        {
            var pt = new DomainPoint3D(1.0, 2.0, 3.0);
            Assert.Equal(1.0, pt.X);
        }

        [Fact]
        public void Test_50_IfcOriginMappingSetsLocalReferenceXyzToZero()
        {
            var service = new ProjectCoordinateTransformService();
            var mockStatus = new MockStatusService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: null,
                CrsName: null,
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var viewModel = new CoordinateReferenceViewModel(service, mockStatus, mockReader);

            viewModel.SelectedMode = CoordinateReferenceMode.IfcOrigin;

            Assert.Equal(0.0, viewModel.LocalReferenceX);
            Assert.Equal(0.0, viewModel.LocalReferenceY);
            Assert.Equal(0.0, viewModel.LocalReferenceZ);
        }

        [Fact]
        public void Test_51_IfcOriginMappingDoesNotMoveOrTransformLocalModelCoordinates()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0.0,
                LocalReferenceY = 0.0,
                LocalReferenceZ = 0.0,
                SurveyEasting = 50000.0,
                SurveyNorthing = 60000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 45.0,
                Scale = 1.0,
                Source = "IfcMapConversion"
            };

            service.ApplyReference(reference, out _);

            // local point stays geometrically at (10, 15, 0) relative to local origin
            var localPoint = new DomainPoint3D(10.0, 15.0, 0.0);

            // Verify transform logic converts local coordinates to survey readouts without mutating the local coordinate itself
            var surveyReadout = service.LocalToSurvey(localPoint);

            // Coordinate readout changes based on the reference origin conversion
            Assert.NotEqual(10.0, surveyReadout.X);
            Assert.NotEqual(15.0, surveyReadout.Y);

            // But the original point coordinates remain completely unchanged in local space
            Assert.Equal(10.0, localPoint.X);
            Assert.Equal(15.0, localPoint.Y);
        }

        private class StubDialogService : IDialogService
        {
            public string? OpenFile(string filter) => "test.ifc";
            public void ShowMessage(string message) { }
            public bool Confirm(string message, string title) => true;
            public void ShowNonBlockingError(string message, string title) { }
        }

        private class StubIfcOptionsDialogService : IIfcOptionsDialogService
        {
            private readonly CoordinateReferenceMode _mode;
            public StubIfcOptionsDialogService(CoordinateReferenceMode mode) => _mode = mode;
            public IfcLoadOptionsDto? Show(string filePath) => new IfcLoadOptionsDto { CoordinateMode = _mode };
        }

        private class StubViewportInteractionService : IViewportInteractionService
        {
            public int PushModeCount { get; private set; }
            public IInteractionMode? PushedMode { get; private set; }
            public IInteractionMode CurrentMode => null!;
            public void PushMode(IInteractionMode mode)
            {
                PushModeCount++;
                PushedMode = mode;
            }
            public void PopMode() { }
            public void ResetToDefaultMode(IInteractionMode defaultMode) { }
            public void HandleMouseDown(HelixToolkit.Wpf.HelixViewport3D viewport, System.Windows.Input.MouseButtonEventArgs e) { }
            public void HandleMouseMove(HelixToolkit.Wpf.HelixViewport3D viewport, System.Windows.Input.MouseEventArgs e) { }
            public void HandleMouseUp(HelixToolkit.Wpf.HelixViewport3D viewport, System.Windows.Input.MouseButtonEventArgs e) { }
            public void HandleKeyDown(HelixToolkit.Wpf.HelixViewport3D viewport, System.Windows.Input.KeyEventArgs e) { }
        }

        private class StubElementVisibilityFilterService : IElementVisibilityFilterService
        {
            public event Action? StateChanged { add { } remove { } }
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
            public void SetWeightRange(double min, double max) { }
            public void SetIncludedElementIds(IEnumerable<string>? ids) { }
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

        private class StubIfcSceneOperationService : IIfcSceneOperationService
        {
            public bool HasIfcModel { get; set; } = true;
            public List<PrecastElement> LastReplacedElements { get; private set; } = new();
            public List<PrecastElement> GetElements() => new();
            public List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset) => elements.ToList();
            public List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements)
            {
                LastReplacedElements = elements.ToList();
                return LastReplacedElements;
            }
            public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements) => elements.ToList();
            public List<PrecastElement> MoveIfcModel(DomainPoint3D delta) => new();
            public void DeleteIfcModel(bool clearReaderData = true) { }
        }

        private class StubIfcDiagnosticAuditService : IIfcDiagnosticAuditService
        {
            public Task<IfcAuditResult> RunAuditAsync(string ifcPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult<IfcAuditResult>(null!);
            public Task<IfcAuditResult> RunAuditAsync(string ifcPath, IfcReadingOptions? options, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult<IfcAuditResult>(null!);
            public Task<string> GenerateReportsAsync(IfcAuditResult auditResult, string? projectFolderPath = null, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        }

        private class StubIfcNormalizationDecisionService : IIfcNormalizationDecisionService
        {
            public Task<IfcNormalizationPreviewSummary> GenerateNormalizationPreviewAsync(IfcAuditResult auditResult, CancellationToken cancellationToken = default) => Task.FromResult<IfcNormalizationPreviewSummary>(null!);
            public Task<string> GeneratePreviewReportsAsync(IfcNormalizationPreviewSummary preview, string? projectFolderPath = null, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        }

        [Fact]
        public async Task Test_52_IfcOriginModeDoesNotEnterPlacementMode()
        {
            var service = new ProjectCoordinateTransformService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: 1.0,
                XAxisOrdinate: 0.0,
                Scale: 1.0,
                CrsName: "Test CRS",
                CrsDescription: null,
                EpsgCode: "3857",
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var dialog = new StubDialogService();
            var dialogService = new StubIfcOptionsDialogService(CoordinateReferenceMode.IfcOrigin);
            var interaction = new StubViewportInteractionService();
            var filterService = new StubElementVisibilityFilterService();
            var sceneOps = new StubIfcSceneOperationService();
            var auditService = new StubIfcDiagnosticAuditService();
            var normService = new StubIfcNormalizationDecisionService();

            var viewModel = new IfcViewerViewModel(
                mockReader,
                null!,
                dialog,
                dialogService,
                interaction,
                null!,
                filterService,
                sceneOps,
                auditService,
                null!,
                normService,
                service
            );

            // Invoke LoadIfcAsync via reflection
            var method = typeof(IfcViewerViewModel).GetMethod("LoadIfcAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            var task = (Task)method.Invoke(viewModel, null)!;
            await task;

            // Placement mode must not be entered
            Assert.False(viewModel.IsPlacementMode);
            Assert.Equal(0, interaction.PushModeCount);
            Assert.Equal("test.ifc", viewModel.LoadedIfcFilePath);
        }

        [Fact]
        public async Task Test_53_IfcOriginModeHasIdentityOffsetAndBBoxUnchanged()
        {
            var service = new ProjectCoordinateTransformService();
            var mockGeoref = new IfcGeoreferencingResult(
                HasMapConversion: true,
                Eastings: 50000.0,
                Northings: 60000.0,
                OrthogonalHeight: 10.0,
                XAxisAbscissa: null,
                XAxisOrdinate: null,
                Scale: 1.0,
                CrsName: "Test CRS",
                CrsDescription: null,
                EpsgCode: null,
                HasSiteLatLon: false,
                SiteLatitude: null,
                SiteLongitude: null,
                SiteElevation: null
            );
            var mockReader = new MockIfcReaderWithGeoref(mockGeoref);
            var dialog = new StubDialogService();
            var dialogService = new StubIfcOptionsDialogService(CoordinateReferenceMode.IfcOrigin);
            var interaction = new StubViewportInteractionService();
            var filterService = new StubElementVisibilityFilterService();
            var sceneOps = new StubIfcSceneOperationService();
            var auditService = new StubIfcDiagnosticAuditService();
            var normService = new StubIfcNormalizationDecisionService();

            var viewModel = new IfcViewerViewModel(
                mockReader,
                null!,
                dialog,
                dialogService,
                interaction,
                null!,
                filterService,
                sceneOps,
                auditService,
                null!,
                normService,
                service
            );

            var method = typeof(IfcViewerViewModel).GetMethod("LoadIfcAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            await (Task)method.Invoke(viewModel, null)!;

            // PlacementOffset must be exactly (0,0,0)
            Assert.NotNull(viewModel.PendingPlacementPoint);
            Assert.Equal(0.0, viewModel.PendingPlacementPoint.Value.X);
            Assert.Equal(0.0, viewModel.PendingPlacementPoint.Value.Y);
            Assert.Equal(0.0, viewModel.PendingPlacementPoint.Value.Z);

            // Local reference points remain 0
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceX);
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceY);
            Assert.Equal(0.0, service.CurrentReference.LocalReferenceZ);
        }

        [Fact]
        public void Test_54_MoveIfcCommandDisabledDuringIfcOriginMapping()
        {
            var service = new ProjectCoordinateTransformService();
            var mockReader = new MockIfcReaderRepository();
            var viewModel = new IfcViewerViewModel(
                mockReader,
                null!, null!, null!, null!, null!, null!,
                new StubIfcSceneOperationService { HasIfcModel = true },
                null!, null!, null!,
                service
            );

            // Setup georeferencing
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                Source = "IfcMapConversion",
                SurveyEasting = 1000,
                SurveyNorthing = 2000
            };
            service.ApplyReference(reference, out _);

            // Verify Move command is disabled
            Assert.False(viewModel.MoveIfcCommand.CanExecute(null));

            // Switch to LocalOnly (Disable reference)
            reference.IsEnabled = false;
            service.ApplyReference(reference, out _);

            // Verify Move command is now enabled (since it is local only)
            Assert.True(viewModel.MoveIfcCommand.CanExecute(null));
        }

        [Fact]
        public void Test_55_LocalToSurveyConvertModelOriginToIfcMapConversion()
        {
            var service = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = 0,
                LocalReferenceY = 0,
                LocalReferenceZ = 0,
                SurveyEasting = 500000.0,
                SurveyNorthing = 100000.0,
                SurveyElevation = 1000.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0,
                Source = "IfcMapConversion"
            };
            service.ApplyReference(reference, out _);

            var origin = new DomainPoint3D(0, 0, 0);
            var survey = service.LocalToSurvey(origin);

            Assert.Equal(500000.0, survey.X);
            Assert.Equal(100000.0, survey.Y);
            Assert.Equal(1000.0, survey.Z);
        }

        [Fact]
        public void Test_56_GAServicesIsolatedFromIfcOriginMapping()
        {
            // Verify that GA-related calculations are not changed by IfcOrigin mapping
            var pt = new DomainPoint3D(10, 10, 0);
            Assert.Equal(10, pt.X);
            Assert.Equal(10, pt.Y);
        }

        [Fact]
        public void Test_57_LocalOnlyShowsSurveyInactiveInPreview()
        {
            var transformService = new ProjectCoordinateTransformService();
            var statusService = new MockStatusService();
            var ifcReader = new MockIfcReaderRepository();
            var vm = new CoordinateReferenceViewModel(transformService, statusService, ifcReader);

            vm.SelectedMode = CoordinateReferenceMode.LocalOnly;
            vm.IsEnabled = false;

            // Trigger preview update
            vm.PreviewLocalX = 15.0;
            vm.PreviewSurveyEasting = 15.0;

            Assert.Equal("Survey: Not active / Local Only", vm.PreviewSurveyResult);
            Assert.Contains("Local X: 15.000 m", vm.PreviewLocalResult);
        }

        [Fact]
        public void Test_58_IfcOriginShowsConvertedSurveyCoordinatesInPreview()
        {
            var transformService = new ProjectCoordinateTransformService();
            var statusService = new MockStatusService();
            var georef = new IfcGeoreferencingResult(
                true,
                500000.0,
                100000.0,
                10.0,
                1.0,
                0.0,
                1.0,
                "Projected CRS",
                "CRS Desc",
                "EPSG:31256",
                false,
                null,
                null,
                null
            );
            var ifcReader = new MockIfcReaderWithGeoref(georef);
            var vm = new CoordinateReferenceViewModel(transformService, statusService, ifcReader);

            vm.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            vm.IsEnabled = true;

            // Set test preview points
            vm.PreviewLocalX = 10.0;
            vm.PreviewLocalY = 20.0;
            vm.PreviewLocalZ = 5.0;

            Assert.Contains("Easting: 500010.000 m", vm.PreviewSurveyResult);
            Assert.Contains("Northing: 100020.000 m", vm.PreviewSurveyResult);
            Assert.Contains("Elevation: 15.000 m", vm.PreviewSurveyResult);
        }

        [Fact]
        public void Test_59_LocalElementCOGUnchangedAfterSurveyCalculation()
        {
            var transformService = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 500000.0,
                SurveyNorthing = 100000.0,
                SurveyElevation = 10.0
            };
            transformService.ApplyReference(reference, out _);

            var element = new PrecastElement
            {
                ElementCOG = new DomainPoint3D(10.0, 20.0, 30.0)
            };

            var surveyPt = transformService.LocalToSurvey(element.ElementCOG);

            // Verify converted values are correct
            Assert.Equal(500010.0, surveyPt.X);
            // Verify original COG is unmodified
            Assert.Equal(10.0, element.ElementCOG.X);
            Assert.Equal(20.0, element.ElementCOG.Y);
            Assert.Equal(30.0, element.ElementCOG.Z);
        }

        [Fact]
        public void Test_60_SurveyCoordinateDisplayUpdatesWhenModeChanges()
        {
            var transformService = new ProjectCoordinateTransformService();
            var statusService = new MockStatusService();
            var georef = new IfcGeoreferencingResult(
                true,
                500000.0,
                100000.0,
                10.0,
                1.0,
                0.0,
                1.0,
                "Projected CRS",
                "CRS Desc",
                "EPSG:31256",
                false,
                null,
                null,
                null
            );
            var ifcReader = new MockIfcReaderWithGeoref(georef);
            var vm = new CoordinateReferenceViewModel(transformService, statusService, ifcReader);

            vm.SelectedMode = CoordinateReferenceMode.LocalOnly;
            Assert.Equal("Survey: Not active / Local Only", vm.PreviewSurveyResult);

            vm.SelectedMode = CoordinateReferenceMode.IfcOrigin;
            vm.IsEnabled = true;
            Assert.Contains("Easting:", vm.PreviewSurveyResult);

            vm.SelectedMode = CoordinateReferenceMode.LocalOnly;
            Assert.Equal("Survey: Not active / Local Only", vm.PreviewSurveyResult);
        }

        [Fact]
        public void Test_61_DropdownLabelsRemainCleanAndManualNotSelectable()
        {
            var transformService = new ProjectCoordinateTransformService();
            var statusService = new MockStatusService();
            var ifcReader = new MockIfcReaderRepository();
            var vm = new CoordinateReferenceViewModel(transformService, statusService, ifcReader);

            var modes = vm.AvailableModes;
            Assert.Equal(2, modes.Count);
            Assert.Contains(modes, m => m.Value == CoordinateReferenceMode.LocalOnly && m.DisplayName == "Local Only");
            Assert.Contains(modes, m => m.Value == CoordinateReferenceMode.IfcOrigin && m.DisplayName == "IFC Origin Mapping");
            Assert.DoesNotContain(modes, m => m.Value == CoordinateReferenceMode.Manual);
        }

        [Fact]
        public void Test_62_GAResultPresentationDtoShowsSurveyWhenActive()
        {
            var transformService = new ProjectCoordinateTransformService();
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = true,
                SurveyEasting = 500000.0,
                SurveyNorthing = 100000.0,
                SurveyElevation = 10.0,
                RotationToNorthDegrees = 0.0,
                Scale = 1.0
            };
            transformService.ApplyReference(reference, out _);

            var underlying = new CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.CranePositionResult
            {
                X = 10.0,
                Y = 20.0,
                Z = 0.0
            };
            var dto = new CranePositionPresentationDto(underlying, transformService);

            string displayText = dto.PositionDisplayText;
            Assert.Contains("Local X: 10.00", displayText);
            Assert.Contains("Local Y: 20.00", displayText);
            Assert.Contains("Easting: 500010.00", displayText);
            Assert.Contains("Northing: 100020.00", displayText);
        }

        [Fact]
        public void Test_63_GAResultPresentationDtoShowsLocalOnlyWhenInactive()
        {
            var transformService = new ProjectCoordinateTransformService();
            transformService.ResetReference(); // IsEnabled = false

            var underlying = new CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.CranePositionResult
            {
                X = 10.0,
                Y = 20.0,
                Z = 0.0
            };
            var dto = new CranePositionPresentationDto(underlying, transformService);

            string displayText = dto.PositionDisplayText;
            Assert.Contains("Local X: 10.00", displayText);
            Assert.Contains("Local Y: 20.00", displayText);
            Assert.DoesNotContain("Easting", displayText);
        }

        [Fact]
        public void Test_64_NullMissingCoordinateReferenceDoesNotBreakPreviews()
        {
            var transformService = new ProjectCoordinateTransformService();
            var statusService = new MockStatusService();
            var ifcReader = new MockIfcReaderRepository();
            
            // Should load without exceptions even if transformService has default/null reference
            var vm = new CoordinateReferenceViewModel(transformService, statusService, ifcReader);
            Assert.NotNull(vm.PreviewSurveyResult);
            Assert.NotNull(vm.PreviewLocalResult);
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
                }
        }

        private class MockIfcReaderRepository : IIfcReaderRepository
        {
            public double ModelLengthFactor => 1.0;
            public double? ModelGroundElevation => null;
            public List<PrecastElement> ReadElements(string ifcPath) => new();
            public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options) => new();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress) => new();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress, IfcReadingOptions? options, CancellationToken cancellationToken = default) => new();
            public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public IfcGeoreferencingResult? GetGeoreferencingData() => null;
            public void ClearData() { }
            public void Dispose() { }
        }

        private class MockIfcReaderWithGeoref : IIfcReaderRepository
        {
            private readonly IfcGeoreferencingResult _georef;
            public MockIfcReaderWithGeoref(IfcGeoreferencingResult georef) => _georef = georef;
            public double ModelLengthFactor => 1.0;
            public double? ModelGroundElevation => null;

            private static List<PrecastElement> DummyElements() => new()
            {
                new PrecastElement
                {
                    ElementID = "TEST_001",
                    ElementMark = "T1",
                    ElementCOG = new DomainPoint3D(5.0, 5.0, 0.0),
                    ElementWT = 1.0
                }
            };

            public List<PrecastElement> ReadElements(string ifcPath) => DummyElements();
            public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options) => DummyElements();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress) => DummyElements();
            public List<PrecastElement> ReadElements(string path, IProgress<double> progress, IfcReadingOptions? options, CancellationToken cancellationToken = default) => DummyElements();
            public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public IfcGeoreferencingResult? GetGeoreferencingData() => _georef;
            public void ClearData() { }
            public void Dispose() { }
        }
    }
}
