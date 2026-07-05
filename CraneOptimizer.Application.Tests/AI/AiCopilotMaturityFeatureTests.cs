using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Knowledge;
using CraneOptimizer.AI.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiCopilotMaturityFeatureTests
    {
        [Fact]
        public void PromptTemplateService_SelectsNoResultTemplate_WhenNoOptimizationResultExists()
        {
            var service = new AiPromptTemplateService();
            var template = service.SelectTemplate(
                new AiOptimizationContextDto { HasOptimizationResult = false },
                new AiAdvisorResponseDto { HealthLabel = "No run available" });

            Assert.Equal("no-result", template.Key);
            Assert.Contains("Status", template.Prompt);
        }

        [Fact]
        public void PromptTemplateService_SelectsOutOfRadiusTemplate_WhenOutOfRadiusDominates()
        {
            var service = new AiPromptTemplateService();
            var template = service.SelectTemplate(
                new AiOptimizationContextDto
                {
                    HasOptimizationResult = true,
                    TotalElementCount = 100,
                    LiftedElementCount = 50,
                    CoverageRatio = 0.5,
                    FailureCounts = new FailureCountsDto { OutOfRadius = 40, Overweight = 3, NotCharted = 1 }
                },
                new AiAdvisorResponseDto { HealthLabel = "Critical" });

            Assert.Equal("out-of-radius", template.Key);
        }

        [Fact]
        public void UiInteractionContextService_RaisesConfirmableNavigationAction()
        {
            var service = new UiInteractionContextService();
            AiUiActionRequestedEventArgs? observed = null;
            service.ActionRequested += (_, args) => observed = args;

            service.RequestTabNavigation("ANALYZE", "Test action proposal");

            Assert.NotNull(observed);
            Assert.Equal("NavigateTab", observed!.ActionType);
            Assert.Equal("ANALYZE", observed.Target);
        }

        [Fact]
        public void UiContracts_ExposeDockedAssistantAndFullCopilotActions()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);

            var mainWindowXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.UI", "Shell", "Views", "MainWindow.xaml"));
            var assistantXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var fullWindowXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));

            Assert.Contains("AI ASSIST", mainWindowXaml);
            Assert.Contains("Confirmable action proposals", assistantXaml);
            Assert.Contains("Report Appendix", fullWindowXaml);
            Assert.Contains("Context Cards", fullWindowXaml);
        }

        private static string? FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            return null;
        }
    }
}
