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

            Assert.DoesNotContain(context.Tabs, tab => tab.TabName == "AI COPILOT");
            Assert.Contains(context.Tabs, tab => tab.TabName == "AI ASSIST");
            Assert.Contains(context.Tabs, tab => tab.TabName == "ANALYZE");
            Assert.Contains(context.Tabs.Single(tab => tab.TabName == "ANALYZE").Actions, action => action.Label == "Run Optimizer");

            var aiAssist = context.Tabs.Single(tab => tab.TabName == "AI ASSIST");
            Assert.Contains("not a top-level ribbon tab", aiAssist.Purpose, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(aiAssist.Actions, action => action.Label == "Open Full Studio");
            Assert.Contains(aiAssist.Actions, action => action.Label == "Analyze Current Result");
            Assert.Contains(aiAssist.Actions, action => action.Label == "Ask AI Model");
            Assert.Contains(aiAssist.Actions, action => action.Label == "Check Provider");
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
