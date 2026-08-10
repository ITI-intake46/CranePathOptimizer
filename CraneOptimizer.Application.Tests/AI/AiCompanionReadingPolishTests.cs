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
        public void DeterministicAssistantMessage_ExposesApplicationFactsSourceBadge()
        {
            var message = AiCompanionChatMessageViewModel.Assistant(
                "There are 50 filterable planning slabs.",
                responseSourceLabel: "Application facts · deterministic");

            Assert.Equal("Application facts · deterministic", message.ResponseSourceLabel);
            Assert.Equal(System.Windows.Visibility.Visible, message.ResponseSourceVisibility);
        }

        [Fact]
        public void UserMessage_DoesNotExposeAResponseSourceBadge()
        {
            var message = AiCompanionChatMessageViewModel.User("How many slabs are there?");

            Assert.Equal(string.Empty, message.ResponseSourceLabel);
            Assert.Equal(System.Windows.Visibility.Collapsed, message.ResponseSourceVisibility);
        }

        [Fact]
        public void BimQuickPromptsAreUserFacingAndResponseSourceIsVisibleInTheChatTemplate()
        {
            var root = FindRepositoryRoot();
            var xaml = File.ReadAllText(Path.Combine(
                root,
                "CraneOptimizer.AI",
                "Companion",
                "Views",
                "AiCompanionPanel.xaml"));

            Assert.Contains("How many slabs are there, and how does the planning/filter count compare", xaml);
            Assert.Contains("ResponseSourceLabel", xaml);
            Assert.Contains("ResponseSourceVisibility", xaml);
            Assert.DoesNotContain("do not use a model provider", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Use host-confirmed values", xaml, StringComparison.OrdinalIgnoreCase);
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

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CraneOptimizer.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate CraneOptimizer.sln from the test output directory.");
        }
    }
}
