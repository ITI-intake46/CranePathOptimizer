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
            Assert.Contains(aiAssist.Actions, action => action.Label == "Ask Local Ollama");
            Assert.Contains(aiAssist.Actions, action => action.Label == "Check Ollama");
        }

        [Fact]
        public void GetSoftwareContext_MarksHiddenFeaturesAndCompletedDxfCommandsAccurately()
        {
            var service = new StaticAiSoftwareContextService();

            var context = service.GetSoftwareContext();

            Assert.Contains(context.Tabs, tab => tab.TabName == "SETTINGS" && tab.Purpose.Contains("hidden", StringComparison.OrdinalIgnoreCase));
            var dxfActions = context.Tabs.Single(tab => tab.TabName == "DXF").Actions;
            Assert.DoesNotContain(dxfActions, action => action.CommandOrHandler.Contains("Coming soon", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dxfActions, action => action.CommandOrHandler.Contains("AddAnotherDxfCommand", StringComparison.Ordinal));
            Assert.Contains(dxfActions, action => action.CommandOrHandler.Contains("SelectAllCommand", StringComparison.Ordinal));
            Assert.Contains(context.SafetyGuardrails, guardrail => guardrail.Contains("Do not invent hidden buttons", StringComparison.OrdinalIgnoreCase));
        }
    }
}
