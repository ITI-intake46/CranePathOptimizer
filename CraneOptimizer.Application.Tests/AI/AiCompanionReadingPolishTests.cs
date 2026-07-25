using CraneOptimizer.AI.Companion.ViewModels;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiCompanionReadingPolishTests
    {
        [Fact]
        public void LongAgentReport_ShowsConciseReadingPreviewBeforeFullDetails()
        {
            var report = """
                Project Readiness Agent

                Goal: What is next step?
                Run status: Completed
                Snapshot: model revision 7

                Conclusion
                Optimization is blocked because no trusted GA-ready grouped lift units are available.
                Select a mobile crane and verify trusted weight evidence before running optimization.

                Plan and tool execution
                T1 · get_project_overview · Succeeded
                T2 · get_bim_readiness · Succeeded
                T3 · get_zone_summary · Succeeded
                T4 · get_crane_setup · Succeeded
                T5 · get_ga_readiness · Succeeded

                Deterministic findings
                The model contains 362 BIM objects, five zones, and no selected crane.
                Estimated fallback weight blocks 327 elements from GA.

                Recommended actions
                1. CRANES → MOBILE CRANE — Add or select the crane model.
                2. CRANES → Import Load Chart — Verify manufacturer capacity evidence.
                3. IFC MODEL → Export Element List — Review trusted lift weights.

                Governance
                All tools were typed, read-only project inspections.
                Engineering review remains required.
                """;

            var message = AiCompanionChatMessageViewModel.Assistant(report);

            Assert.True(message.HasHiddenDetails);
            Assert.Contains("Conclusion", message.DisplayBody);
            Assert.Contains("Next actions", message.DisplayBody);
            Assert.DoesNotContain("Plan and tool execution", message.DisplayBody);
            Assert.Equal("View details", message.DetailsToggleLabel);

            message.ToggleDetailsCommand.Execute(null);

            Assert.True(message.IsDetailsExpanded);
            Assert.Contains("Plan and tool execution", message.DisplayBody);
            Assert.Contains("Governance", message.DisplayBody);
            Assert.Equal("Show less", message.DetailsToggleLabel);
        }

        [Fact]
        public void ShortAnswer_RemainsFullyVisibleWithoutDetailsToggle()
        {
            const string answer = "Answer\nThere are 60 elements categorized as Slab in the captured model context.";

            var message = AiCompanionChatMessageViewModel.Assistant(answer);

            Assert.False(message.HasHiddenDetails);
            Assert.Equal(message.FullBodyPreview, message.DisplayBody);
            Assert.Equal(System.Windows.Visibility.Collapsed, message.DetailsToggleVisibility);
        }
    }
}
