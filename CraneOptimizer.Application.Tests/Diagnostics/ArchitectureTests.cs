using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using System;
using System.Linq;
using System.Reflection;

namespace CraneOptimizer.Application.Tests
{
    public class ArchitectureTests
    {
        [Fact]
        public void TestGeneticAlgorithmsNamespaceHasNoUiOrWpfOrHelixOrViewModelOrCoordinateServiceCoupling()
        {
            // Load the CraneOptimizer.Application assembly
            var appAssembly = typeof(ConstraintEngine).Assembly;
            
            // Get all referenced assemblies
            var referencedAssemblies = appAssembly.GetReferencedAssemblies();
            
            foreach (var reference in referencedAssemblies)
            {
                string name = reference.Name ?? string.Empty;

                if (name.Contains("WindowsBase") 
                    || name.Contains("PresentationFramework") 
                    || name.Contains("PresentationCore") 
                    || name.Contains("HelixToolkit") 
                    || name.Equals("CraneOptimizer.UI", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"Architecture Violation! Production Application assembly is coupled to UI library: '{name}'");
                }
            }

            // UI, WPF, Helix, ViewModel, and CoordinateService tokens to forbid
            string[] forbiddenTokens = new[]
            {
                "PresentationFramework",
                "PresentationCore",
                "WindowsBase",
                "Helix",
                "System.Xaml",
                "ViewModel",
                "CoordinateService"
            };

            // Verify types within CraneOptimizer.Application.Optimization.GeneticAlgorithms
            var gaTypes = appAssembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("CraneOptimizer.Application.Optimization.GeneticAlgorithms", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(gaTypes);

            foreach (var type in gaTypes)
            {
                // Scan all members (fields, properties, methods, constructors, etc.)
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var member in members)
                {
                    string memberString = member.ToString() ?? string.Empty;
                    foreach (var token in forbiddenTokens)
                    {
                        if (memberString.Contains(token, StringComparison.OrdinalIgnoreCase))
                        {
                            Assert.Fail($"Architecture Violation! Type '{type.FullName}' in GeneticAlgorithms exposes UI/WPF/Helix/ViewModel/CoordinateService coupling in member: '{memberString}'");
                        }
                    }
                }
            }
        }
    }
}
