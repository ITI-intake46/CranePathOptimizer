using CraneOptimizer.Application.Services.Identity;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;
using Xunit;

namespace CraneOptimizer.Application.Tests.Identity
{
    /// <summary>
    /// Unit tests for <see cref="CanonicalMarkResolver"/>.
    /// Validates the Phase 5 canonical display identity priority chain.
    /// </summary>
    public class CanonicalMarkResolverTests
    {
        // ───────────────────────────────────────────────────────────────
        // Priority chain: AssemblyMark > ResolvedDisplayMark > ElementMark
        //                 > ParentAssemblyName > ElementID
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_WhenExplicitAssemblyMark_ReturnsAssemblyMark_UsedFallbackFalse()
        {
            var element = MakeElement("EL-001", assemblyMark: "B-12", isExplicit: true,
                confidence: AssemblyMarkConfidence.High, elementMark: "B12-raw");

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("B-12", result.DisplayLabel);
            Assert.Equal("B-12", result.AssemblyMark);
            Assert.False(result.UsedFallback);
            Assert.True(result.IsExplicitMark);
            Assert.Equal("High", result.MarkConfidence);
        }

        [Fact]
        public void Resolve_WhenNonExplicitAssemblyMark_ReturnsAssemblyMark_UsedFallbackFalse()
        {
            var element = MakeElement("EL-002", assemblyMark: "W-05", isExplicit: false,
                confidence: AssemblyMarkConfidence.Medium, elementMark: "W05-raw");

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("W-05", result.DisplayLabel);
            Assert.False(result.UsedFallback);
            Assert.False(result.IsExplicitMark);
            Assert.Equal("Medium", result.MarkConfidence);
        }

        [Fact]
        public void Resolve_NoAssemblyMark_UsesResolvedDisplayMark_UsedFallbackTrue()
        {
            var element = MakeElement("EL-003", assemblyMark: null,
                resolvedDisplayMark: "Column-A", elementMark: "col-raw");

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Column-A", result.DisplayLabel);
            Assert.True(result.UsedFallback);
            Assert.Equal("Column-A", result.FallbackLabel);
        }

        [Fact]
        public void Resolve_NoAssemblyMarkNoResolvedDisplayMark_UsesElementMark()
        {
            var element = MakeElement("EL-004", assemblyMark: null,
                resolvedDisplayMark: null, elementMark: "S-7");

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("S-7", result.DisplayLabel);
            Assert.True(result.UsedFallback);
        }

        [Fact]
        public void Resolve_NoAssemblyMarkNoElementMark_UsesParentAssemblyName()
        {
            var element = MakeElement("EL-005", assemblyMark: null,
                resolvedDisplayMark: null, elementMark: null);
            element.ParentAssemblyName = "AssemblyGroup-3";

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("AssemblyGroup-3", result.DisplayLabel);
            Assert.True(result.UsedFallback);
        }

        [Fact]
        public void Resolve_AllNullFallbackToElementId()
        {
            var element = MakeElement("EL-GLOBAL-0001", assemblyMark: null,
                resolvedDisplayMark: null, elementMark: null);
            element.ParentAssemblyName = null;

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("EL-GLOBAL-0001", result.DisplayLabel);
            Assert.True(result.UsedFallback);
            Assert.Equal("EL-GLOBAL-0001", result.FallbackLabel);
        }

        [Fact]
        public void Resolve_PopulatesMarkSource_FromAssemblyMarkSource()
        {
            var element = MakeElement("EL-006", assemblyMark: "T-1");
            element.AssemblyMarkSource = "Pset_Revit_Identity Data.Assembly Mark";

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Pset_Revit_Identity Data.Assembly Mark", result.MarkSource);
        }

        [Fact]
        public void Resolve_PopulatesValidationStatus_FromElement()
        {
            var element = MakeElement("EL-007", assemblyMark: "T-2");
            element.ValidationStatus = ValidationStatus.Warning;

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Warning", result.ValidationStatus);
        }

        [Fact]
        public void Resolve_PopulatesSourceSystem_FromElement()
        {
            var element = MakeElement("EL-008", assemblyMark: "T-3");
            element.SourceSystem = SourceSystem.Tekla;

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Tekla", result.SourceSystem);
        }

        [Fact]
        public void Resolve_PopulatesExporterProfile_FromElement()
        {
            var element = MakeElement("EL-009", assemblyMark: "T-4");
            element.ExporterProfile = IfcExporterProfile.Revit;

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Revit", result.ExporterProfile);
        }

        [Fact]
        public void ResolveDisplayMark_ReturnsDisplayLabelOnly()
        {
            var element = MakeElement("EL-010", assemblyMark: "S-2A");

            var mark = CanonicalMarkResolver.ResolveDisplayMark(element);

            Assert.Equal("S-2A", mark);
        }

        [Fact]
        public void Resolve_ConfidenceNone_WhenNoAssemblyMark()
        {
            var element = MakeElement("EL-011", assemblyMark: null,
                elementMark: "raw-mark",
                confidence: AssemblyMarkConfidence.None);

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("None", result.MarkConfidence);
        }

        [Fact]
        public void Resolve_EmptyAssemblyMark_TreatedAsNoAssemblyMark()
        {
            // Whitespace-only AssemblyMark should fall through to ResolvedDisplayMark
            var element = MakeElement("EL-012", assemblyMark: "   ",
                resolvedDisplayMark: "Fallback-Display", elementMark: "raw");

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("Fallback-Display", result.DisplayLabel);
            Assert.True(result.UsedFallback);
        }

        [Fact]
        public void Resolve_ExplicitAssemblyMark_KeepsElementIdAndElementNameSeparate()
        {
            var element = MakeElement(
                "01jhsjVXzBy81J1pRn_FyV",
                assemblyMark: "W5102",
                isExplicit: true,
                confidence: AssemblyMarkConfidence.High,
                elementMark: "W5102");
            element.ElementName = "W5102 - Wall";

            var result = CanonicalMarkResolver.Resolve(element);

            Assert.Equal("01jhsjVXzBy81J1pRn_FyV", result.ElementId);
            Assert.Equal("W5102", result.DisplayLabel);
            Assert.Equal("W5102 - Wall", element.ElementName);
        }
        // ─── helpers ────────────────────────────────────────────────────

        private static PrecastElement MakeElement(
            string elementId,
            string? assemblyMark = null,
            bool isExplicit = false,
            AssemblyMarkConfidence confidence = AssemblyMarkConfidence.None,
            string? elementMark = null,
            string? resolvedDisplayMark = null)
        {
            return new PrecastElement
            {
                ElementID = elementId,
                AssemblyMark = assemblyMark,
                AssemblyMarkIsExplicit = isExplicit,
                AssemblyMarkConfidence = confidence,
                ElementMark = elementMark,
                ResolvedDisplayMark = resolvedDisplayMark,
            };
        }
    }
}
