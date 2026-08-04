using System.Text.RegularExpressions;
using System.Xml.Linq;
using CraneOptimizer.AI.Host;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiProjectIsolationBoundaryTests
    {
        [Fact]
        public void AiProjectHasNoProjectReferencesToHostOrDomainLayers()
        {
            var root = FindRepositoryRoot();
            var aiProjectPath = Path.Combine(root, "CraneOptimizer.AI", "CraneOptimizer.AI.csproj");

            var project = XDocument.Load(aiProjectPath);
            var projectReferences = project
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            Assert.Empty(projectReferences);
        }

        [Fact]
        public void AiProjectDoesNotImportHostLayerNamespacesInSource()
        {
            var root = FindRepositoryRoot();
            var aiRoot = Path.Combine(root, "CraneOptimizer.AI");
            var forbiddenUsing = new Regex(
                @"^\s*using\s+CraneOptimizer\.(UI|Application|Infrastructure|Domain|GAExperiment)\b",
                RegexOptions.Compiled | RegexOptions.Multiline);

            var violations = Directory
                .EnumerateFiles(aiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                               && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .Select(path => new { Path = path, Match = forbiddenUsing.Match(File.ReadAllText(path)) })
                .Where(item => item.Match.Success)
                .Select(item => Path.GetRelativePath(root, item.Path) + ": " + item.Match.Value.Trim())
                .ToArray();

            Assert.Empty(violations);
        }

        [Fact]
        public void HostFactContractLivesInAiProjectWithStableNames()
        {
            Assert.Equal("HostModelRevision", AiWorkflowFactKeys.HostModelRevision);
            Assert.Equal("HostModelFingerprint", AiWorkflowFactKeys.HostModelFingerprint);
            Assert.Equal("SceneBimObjectCount", AiWorkflowFactKeys.SceneBimObjectCount);
            Assert.Equal("ReadinessElementCount", AiWorkflowFactKeys.ReadinessElementCount);
            Assert.Equal("ReferenceGeometryExcludedCount", AiWorkflowFactKeys.ReferenceGeometryExcludedCount);
            Assert.Equal("ElementCountScopeNote", AiWorkflowFactKeys.ElementCountScopeNote);
            Assert.Equal("PlanningCategoryCounts", AiWorkflowFactKeys.PlanningCategoryCounts);
            Assert.Equal("FilterableCategoryCounts", AiWorkflowFactKeys.FilterableCategoryCounts);
            Assert.Equal("FilterableElementCount", AiWorkflowFactKeys.FilterableElementCount);
            Assert.Equal("VisibleFilterableElementCount", AiWorkflowFactKeys.VisibleFilterableElementCount);
            Assert.Equal("CategoryCountScopeNote", AiWorkflowFactKeys.CategoryCountScopeNote);
        }


        [Fact]
        public void UiHostAdaptersReferenceOnlyAiHostContractsNotAiServiceImplementations()
        {
            var root = FindRepositoryRoot();
            var files = new[]
            {
                Path.Combine(root, "CraneOptimizer.UI", "AIIntegration", "AiHostContextBridge.cs"),
                Path.Combine(root, "CraneOptimizer.UI", "AIIntegration", "Contributors", "IfcAiContextContributor.cs"),
                Path.Combine(root, "CraneOptimizer.UI", "AIIntegration", "Contributors", "GaAiContextContributor.cs"),
                Path.Combine(root, "CraneOptimizer.UI", "Shared", "Layouts", "TopNavigation.xaml.cs")
            };

            var serviceNamespaceReferences = files
                .Where(File.Exists)
                .Select(path => new { Path = path, Text = File.ReadAllText(path) })
                .Where(item => item.Text.Contains("using CraneOptimizer.AI.Services;", StringComparison.Ordinal))
                .Select(item => Path.GetRelativePath(root, item.Path))
                .ToArray();

            Assert.Empty(serviceNamespaceReferences);

            foreach (var file in files.Where(File.Exists))
            {
                var text = File.ReadAllText(file);
                Assert.Contains("using CraneOptimizer.AI.Host;", text);
            }
        }

        [Fact]
        public void UiInteractionContextContractLivesInAiHostNamespace()
        {
            var root = FindRepositoryRoot();
            var hostContractPath = Path.Combine(root, "CraneOptimizer.AI", "Host", "IUiInteractionContextService.cs");
            var oldServiceContractPath = Path.Combine(root, "CraneOptimizer.AI", "Services", "IUiInteractionContextService.cs");

            Assert.True(File.Exists(hostContractPath), "The host-facing UI interaction contract must live in CraneOptimizer.AI/Host.");
            Assert.False(File.Exists(oldServiceContractPath), "The host-facing UI interaction contract should not live beside AI service implementations.");

            var text = File.ReadAllText(hostContractPath);
            Assert.Contains("namespace CraneOptimizer.AI.Host", text);
            Assert.Contains("interface IUiInteractionContextService", text);
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
