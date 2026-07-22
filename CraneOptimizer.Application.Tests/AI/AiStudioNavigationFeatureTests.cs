using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiStudioNavigationFeatureTests
    {
        [Fact]
        public void TopRibbon_EndsAtReportsAndDoesNotHostAiCopilotWorkspace()
        {
            var root = FindRepositoryRoot();
            var topNavigationXaml = File.ReadAllText(Path.Combine(root, "CraneOptimizer.UI", "Shared", "Layouts", "TopNavigation.xaml"));
            var topNavigationCode = File.ReadAllText(Path.Combine(root, "CraneOptimizer.UI", "Shared", "Layouts", "TopNavigation.xaml.cs"));

            Assert.Contains("Content=\"REPORTS\"", topNavigationXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Content=\"AI COPILOT\"", topNavigationXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("TabAiCopilot", topNavigationXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("PanelAiCopilot", topNavigationXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("AiAdvisorTabView", topNavigationXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("PanelAiCopilot", topNavigationCode, StringComparison.Ordinal);
            Assert.Contains("IsLegacyAiWorkspaceTarget", topNavigationCode, StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyAiWorkspaceNavigation_RedirectsToVisibleAiAssistStudio()
        {
            var root = FindRepositoryRoot();
            var mainWindowXaml = File.ReadAllText(Path.Combine(root, "CraneOptimizer.UI", "Shell", "Views", "MainWindow.xaml"));
            var mainWindowCode = File.ReadAllText(Path.Combine(root, "CraneOptimizer.UI", "Shell", "Views", "MainWindow.xaml.cs"));
            var companionCode = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml.cs"));

            Assert.Contains("x:Name=\"AiAssistPanel\"", mainWindowXaml, StringComparison.Ordinal);
            Assert.Contains("IUiInteractionContextService uiContext", mainWindowCode, StringComparison.Ordinal);
            Assert.Contains("_uiContext.ActionRequested += OnAiActionRequested", mainWindowCode, StringComparison.Ordinal);
            Assert.Contains("IsLegacyAiWorkspaceTarget", mainWindowCode, StringComparison.Ordinal);
            Assert.Contains("OpenAiAssistStudio", mainWindowCode, StringComparison.Ordinal);
            Assert.Contains("AiAssistPanel.OpenSection(\"AI Studio\")", mainWindowCode, StringComparison.Ordinal);
            Assert.Contains("public void OpenSection(string sectionName)", companionCode, StringComparison.Ordinal);
            Assert.Contains("_pendingSectionName", companionCode, StringComparison.Ordinal);
        }

        [Fact]
        public void StudioSection_OwnsFormerCopilotCommandsAndFullWindowUsesStudioLanguage()
        {
            var root = FindRepositoryRoot();
            var companionXaml = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var companionViewModel = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Companion", "ViewModels", "AiCompanionViewModel.cs"));
            var fullWindowXaml = File.ReadAllText(Path.Combine(root, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));

            Assert.Contains("Text=\"STUDIO ACTIONS\"", companionXaml, StringComparison.Ordinal);
            Assert.Contains("Content=\"Open Full Studio\"", companionXaml, StringComparison.Ordinal);
            Assert.Contains("Text=\"Analyze Current Result\"", companionXaml, StringComparison.Ordinal);
            Assert.Contains("Text=\"Ask Local Ollama\"", companionXaml, StringComparison.Ordinal);
            Assert.Contains("OpenFullStudioCommand", companionViewModel, StringComparison.Ordinal);
            Assert.Contains("PrepareLocalOllamaQuestionCommand", companionViewModel, StringComparison.Ordinal);
            Assert.Contains("SelectedCommandCenterTab = \"Chat\"", companionViewModel, StringComparison.Ordinal);
            Assert.Contains("Title=\"AI Optimization Studio\"", fullWindowXaml, StringComparison.Ordinal);
            Assert.Contains("Text=\"ASK AI STUDIO\"", fullWindowXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("AI Optimization Copilot", fullWindowXaml, StringComparison.Ordinal);
        }


        [Fact]
        public void MaintainedDocumentation_DescribesAiAssistStudioAsTheSupportedEntryPoint()
        {
            var root = FindRepositoryRoot();
            var userGuide = File.ReadAllText(Path.Combine(root, "docs", "ai", "AI_COPILOT_USER_GUIDE.md"));
            var documentationIndex = File.ReadAllText(Path.Combine(root, "docs", "ai", "README.md"));

            Assert.Contains("The main ribbon intentionally ends at **REPORTS**", userGuide, StringComparison.Ordinal);
            Assert.Contains("AI ASSIST → STUDIO", userGuide, StringComparison.Ordinal);
            Assert.Contains("Open Full Studio", userGuide, StringComparison.Ordinal);
            Assert.Contains("Ask Local Ollama", userGuide, StringComparison.Ordinal);
            Assert.DoesNotContain("**AI COPILOT ribbon tab**", userGuide, StringComparison.Ordinal);
            Assert.Contains("not a top-level AI Copilot ribbon tab", documentationIndex, StringComparison.Ordinal);
        }

        private static string FindRepositoryRoot()
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

            throw new InvalidOperationException("Could not locate CraneOptimizer.sln.");
        }
    }
}
