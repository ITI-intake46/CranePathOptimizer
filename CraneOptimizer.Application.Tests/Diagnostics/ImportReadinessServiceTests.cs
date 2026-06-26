using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.Diagnostics
{
    public class ImportReadinessServiceTests
    {
        private readonly IImportReadinessService _readinessService;

        public ImportReadinessServiceTests()
        {
            _readinessService = new ImportReadinessService();
        }

        [Fact]
        public void TestBuildSummary_CountsScopedElementsOnly()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", IncludeInGa = true, IncludeInRender = true, WeightTons = 5.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(1, 2, 3) },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", IncludeInGa = true, IncludeInRender = true, WeightTons = 10.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(4, 5, 6) }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(2, summary.TotalElements);
            Assert.Equal(2, summary.RenderableElements);
            Assert.Equal(2, summary.GaCandidateElements);
            Assert.Equal(0, summary.GaBlockedElements);
        }

        [Fact]
        public void TestBuildSummary_ExcludesNonScopedHiddenElements()
        {
            // Arrange
            // We pass a subset of elements representing only the scoped list.
            // This test verifies that we only process what is passed.
            var allElements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", IncludeInGa = true, WeightTons = 5.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(1, 2, 3) },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", IncludeInGa = true, WeightTons = 10.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(4, 5, 6) },
                new PrecastElement { ElementID = "EL-3", ElementType = "Slab", IncludeInGa = true, WeightTons = 12.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(7, 8, 9) } // Hidden/Not in scoped run
            };

            var scopedElements = allElements.Take(2).ToList();

            // Act
            var summary = _readinessService.BuildSummary(scopedElements);

            // Assert
            Assert.Equal(2, summary.TotalElements);
            Assert.Equal(2, summary.GaCandidateElements);
        }

        [Fact]
        public void TestBuildSummary_SelectedElementScopedRun()
        {
            // Arrange
            var allElements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", IncludeInGa = true, WeightTons = 5.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(1, 2, 3) },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", IncludeInGa = true, WeightTons = 10.0, WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, ElementCOG = new DomainPoint3D(4, 5, 6) }
            };

            // Act: Optimize selected element only (EL-2)
            var selectedElements = allElements.Where(e => e.ElementID == "EL-2").ToList();
            var summary = _readinessService.BuildSummary(selectedElements);

            // Assert
            Assert.Equal(1, summary.TotalElements);
            Assert.Equal("EL-2", selectedElements[0].ElementID);
            Assert.Equal(1, summary.GaCandidateElements);
        }

        [Fact]
        public void TestBuildSummary_ElementCheckerOff_ReturnsNotCheckedState()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", AssemblyMarkIsExplicit = false, WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements, isCheckerEnabled: false);

            // Assert
            Assert.Equal(ValidationStatus.Valid, summary.OverallStatus); // Neutral Valid state
            Assert.Single(summary.Messages);
            Assert.Equal("ElementCheckerDisabled", summary.Messages[0].Code);
            Assert.Equal("Info", summary.Messages[0].Severity);
            
            // Warnings must not be evaluated/counted
            Assert.Equal(0, summary.MissingAssemblyMarkCount);
            Assert.Equal(0, summary.EstimatedWeightBlockedCount);
        }

        [Fact]
        public void TestBuildSummary_GroupsMissingAssemblyMarkWarnings()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", AssemblyMarkIsExplicit = false },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", AssemblyMarkIsExplicit = false }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(2, summary.MissingAssemblyMarkCount);
            var msg = summary.Messages.FirstOrDefault(m => m.Code == "MissingAssemblyMark");
            Assert.NotNull(msg);
            Assert.Equal(2, msg.AffectedCount);
            Assert.Equal("Warning", msg.Severity);
            Assert.Contains("EL-1", msg.AffectedElementIds);
            Assert.Contains("EL-2", msg.AffectedElementIds);
        }

        [Fact]
        public void TestBuildSummary_LowConfidenceMappingCount()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", AssemblyMarkConfidence = AssemblyMarkConfidence.Low },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", AssemblyMarkConfidence = AssemblyMarkConfidence.None },
                new PrecastElement { ElementID = "EL-3", ElementType = "Slab", AssemblyMarkConfidence = AssemblyMarkConfidence.High }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(2, summary.LowConfidenceMappingCount);
            var msg = summary.Messages.FirstOrDefault(m => m.Code == "LowConfidenceMapping");
            Assert.NotNull(msg);
            Assert.Equal(2, msg.AffectedCount);
            Assert.Contains("EL-1", msg.AffectedElementIds);
            Assert.Contains("EL-2", msg.AffectedElementIds);
        }

        [Fact]
        public void TestBuildSummary_EstimatedWeightBlockedFromGA()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA },
                new PrecastElement { ElementID = "EL-2", ElementType = "Wall", WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity, WeightTons = 5.0, IncludeInGa = true, ElementCOG = new DomainPoint3D(1, 2, 3) }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(1, summary.EstimatedWeightBlockedCount);
            var msg = summary.Messages.FirstOrDefault(m => m.Code == "EstimatedWeightBlockedFromGA");
            Assert.NotNull(msg);
            Assert.Equal("Warning", msg.Severity); // Because there is still 1 valid GA candidate left (EL-2)
            Assert.Contains("EL-1", msg.AffectedElementIds);
        }

        [Fact]
        public void TestBuildSummary_NoGaCandidates_ProducesErrorStatus()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(0, summary.GaCandidateElements);
            Assert.Equal(ValidationStatus.Error, summary.OverallStatus);
            
            var msgNoGa = summary.Messages.FirstOrDefault(m => m.Code == "NoGaCandidates");
            Assert.NotNull(msgNoGa);
            Assert.Equal("Error", msgNoGa.Severity);

            var msgWeight = summary.Messages.FirstOrDefault(m => m.Code == "EstimatedWeightBlockedFromGA");
            Assert.NotNull(msgWeight);
            Assert.Equal("Error", msgWeight.Severity); // Becomes Error since GaCandidateElements is 0
        }

        [Fact]
        public void TestBuildSummary_SurfacesParentChildDoubleCountRisk()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "Parent", ElementType = "Assembly", Role = IfcElementRole.LiftAssembly, IncludeInGa = true },
                new PrecastElement { ElementID = "Child", ElementType = "Beam", ParentGlobalId = "Parent", IncludeInGa = true }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(1, summary.ParentChildDoubleCountRiskCount);
            var msg = summary.Messages.FirstOrDefault(m => m.Code == "ParentChildDoubleCountRisk");
            Assert.NotNull(msg);
            Assert.Equal("Warning", msg.Severity);
            Assert.Contains("Child", msg.AffectedElementIds);
        }

        [Fact]
        public void TestBuildSummary_ElementDiagnostics_SurfaceMissingCogBlocker()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "Beam",
                    IncludeInGa = true,
                    WeightTons = 5.0,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    ElementCOG = new DomainPoint3D()
                }
            };

            var summary = _readinessService.BuildSummary(elements);

            var diagnostic = Assert.Single(summary.ElementDiagnostics);
            Assert.False(diagnostic.IsReadyForGa);
            Assert.Equal("Blocked", diagnostic.ReadinessStatus);
            Assert.Contains(diagnostic.BlockingReasons, reason => reason.Contains("COG", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TestBuildSummary_ElementDiagnostics_SurfaceWarningsForReadyElements()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementType = "Beam",
                    IncludeInGa = true,
                    WeightTons = 5.0,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    ElementCOG = new DomainPoint3D(1, 2, 3),
                    AssemblyMarkConfidence = AssemblyMarkConfidence.Low,
                    AssemblyMarkIsExplicit = false
                }
            };

            var summary = _readinessService.BuildSummary(elements);

            var diagnostic = Assert.Single(summary.ElementDiagnostics);
            Assert.True(diagnostic.IsReadyForGa);
            Assert.Equal("Warning", diagnostic.ReadinessStatus);
            Assert.NotEmpty(diagnostic.WarningMessages);
        }
        [Fact]
        public void TestBuildSummary_DoesNotMutateInputs()
        {
            // Arrange
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementType = "Beam", AssemblyMarkIsExplicit = false, ValidationStatus = ValidationStatus.Valid }
            };

            // Act
            var summary = _readinessService.BuildSummary(elements);

            // Assert
            Assert.Equal(ValidationStatus.Valid, elements[0].ValidationStatus); // Should remain unchanged on the input element
        }
    }
}
