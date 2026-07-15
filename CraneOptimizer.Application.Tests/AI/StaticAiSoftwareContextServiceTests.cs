using CraneOptimizer.AI.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class StaticAiSoftwareContextServiceTests
    {
        [Fact]
        public void GetSoftwareContext_IncludesVisibleAiAndOptimizationNavigation()
        {
            var service = new StaticAiSoftwareContextService();

            var context = service.GetSoftwareContext();

            Assert.Contains(context.Tabs, tab => tab.TabName == "AI COPILOT");
            Assert.Contains(context.Tabs, tab => tab.TabName == "ANALYZE");
            Assert.Contains(context.Tabs.Single(tab => tab.TabName == "ANALYZE").Actions, action => action.Label == "Run Optimizer");
            Assert.Contains(context.Tabs.Single(tab => tab.TabName == "AI COPILOT").Actions, action => action.Label == "Check Ollama");
        }

        [Fact]
        public void GetSoftwareContext_MarksHiddenOrComingSoonFeaturesExplicitly()
        {
            var service = new StaticAiSoftwareContextService();

            var context = service.GetSoftwareContext();

            Assert.Contains(context.Tabs, tab => tab.TabName == "SETTINGS" && tab.Purpose.Contains("hidden", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(context.Tabs.Single(tab => tab.TabName == "DXF").Actions, action => action.Availability.Contains("coming soon", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(context.SafetyGuardrails, guardrail => guardrail.Contains("Do not invent hidden buttons", StringComparison.OrdinalIgnoreCase));
        }
    }
}
