using CraneOptimizer.AI.Host;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.AIIntegration.Contributors;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class PlanningFilterAiFactBuilderTests
    {
        [Fact]
        public void BuildsAuthoritativeFilterFactsFromTheSameScopeAndTypeLabelsAsTheDialog()
        {
            var scene = new List<PrecastElement>();
            scene.AddRange(CreateElements("Wall", 214, IfcElementRole.LiftAssembly, includeInGa: true));
            scene.AddRange(CreateElements("Slab", 50, IfcElementRole.LiftAssembly, includeInGa: true));
            scene.AddRange(CreateElements("Beam", 40, IfcElementRole.LiftAssembly, includeInGa: true));
            scene.AddRange(CreateElements("Column", 36, IfcElementRole.LiftAssembly, includeInGa: true));
            scene.AddRange(CreateElements("Roof", 17, IfcElementRole.LiftAssembly, includeInGa: true));

            // Complete-scene child/component objects must not enter the planning/filter scope.
            scene.AddRange(CreateElements("Slab", 66, IfcElementRole.InternalAuxiliary, includeInGa: false));
            scene.AddRange(CreateElements("Beam", 40, IfcElementRole.LiftableElement, includeInGa: true));
            scene.Add(CreateElement("", IfcElementRole.LiftAssembly, includeInGa: true, id: "unknown-filterable"));

            var visible = scene
                .Where(ElementFilterScopeRules.IsPlanningFilterable)
                .Where(element => element.ElementID != "Beam-0")
                .ToList();

            var facts = PlanningFilterAiFactBuilder.Build(scene, visible);

            Assert.Equal("358", facts[AiWorkflowFactKeys.FilterableElementCount]);
            Assert.Equal("357", facts[AiWorkflowFactKeys.VisibleFilterableElementCount]);
            Assert.Equal(
                "Wall=214; Slab=50; Beam=40; Column=36; Roof=17; Unknown type=1",
                facts[AiWorkflowFactKeys.PlanningCategoryCounts]);
            Assert.Equal(
                facts[AiWorkflowFactKeys.PlanningCategoryCounts],
                facts[AiWorkflowFactKeys.FilterableCategoryCounts]);
            Assert.Contains("HOME → Filter", facts[AiWorkflowFactKeys.CategoryCountScopeNote]);
            Assert.Contains("LiftAssembly", facts[AiWorkflowFactKeys.CategoryCountScopeNote]);
            Assert.Contains("IncludeInGa=true", facts[AiWorkflowFactKeys.CategoryCountScopeNote]);
        }

        [Theory]
        [InlineData(IfcElementRole.LiftAssembly, true, true)]
        [InlineData(IfcElementRole.LiftAssembly, false, false)]
        [InlineData(IfcElementRole.LiftableElement, true, false)]
        [InlineData(IfcElementRole.InternalAuxiliary, true, false)]
        public void PlanningFilterRuleMatchesTheDialogContract(
            IfcElementRole role,
            bool includeInGa,
            bool expected)
        {
            var element = CreateElement("Slab", role, includeInGa, "scope-test");

            Assert.Equal(expected, ElementFilterScopeRules.IsPlanningFilterable(element));
        }

        private static IEnumerable<PrecastElement> CreateElements(
            string type,
            int count,
            IfcElementRole role,
            bool includeInGa)
        {
            for (var index = 0; index < count; index++)
                yield return CreateElement(type, role, includeInGa, $"{type}-{index}");
        }

        private static PrecastElement CreateElement(
            string type,
            IfcElementRole role,
            bool includeInGa,
            string id)
        {
            return new PrecastElement
            {
                ElementID = id,
                ElementType = type,
                ElementMaterial = "CONCRETE",
                Role = role,
                IncludeInGa = includeInGa
            };
        }
    }
}
