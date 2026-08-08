using CraneOptimizer.AI.Host;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.AIIntegration;
using CraneOptimizer.UI.AIIntegration.Contributors;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiHostContextBridgeFilterRefreshTests
    {
        [Fact]
        public void FilterStateChangesRaiseAiContextChangedAndDisposeUnsubscribes()
        {
            var filter = new FakeFilterService();
            using var bridge = new AiHostContextBridge(Array.Empty<IAiScreenContextContributor>(), filter);
            var changeCount = 0;
            bridge.ContextChanged += (_, _) => changeCount++;

            filter.RaiseStateChanged();

            Assert.Equal(1, changeCount);

            bridge.Dispose();
            filter.RaiseStateChanged();

            Assert.Equal(1, changeCount);
        }

        [Fact]
        public void HideCategoryAndShowAllPublishCurrentVisiblePlanningCounts()
        {
            var scene = CreateElements("Wall", 214)
                .Concat(CreateElements("Slab", 50))
                .Concat(CreateElements("Beam", 40))
                .Concat(CreateElements("Column", 36))
                .Concat(CreateElements("Roof", 17))
                .ToList();
            var filter = new FakeFilterService(scene);
            using var bridge = new AiHostContextBridge(Array.Empty<IAiScreenContextContributor>(), filter);
            var observedVisibleCounts = new List<string>();

            bridge.ContextChanged += (_, _) =>
            {
                var facts = PlanningFilterAiFactBuilder.Build(scene, filter.VisibleElements);
                observedVisibleCounts.Add(facts[AiWorkflowFactKeys.VisibleFilterableElementCount]);
            };

            filter.HideElements(scene.Where(element => element.ElementType == "Slab"));
            filter.ShowAll();

            Assert.Equal(new[] { "307", "357" }, observedVisibleCounts);
        }

        private static IEnumerable<PrecastElement> CreateElements(string type, int count)
        {
            for (var index = 0; index < count; index++)
            {
                yield return new PrecastElement
                {
                    ElementID = $"{type}-{index}",
                    ElementType = type,
                    ElementMaterial = "CONCRETE",
                    Role = IfcElementRole.LiftAssembly,
                    IncludeInGa = true
                };
            }
        }

        private sealed class FakeFilterService : IElementVisibilityFilterService
        {
            private readonly List<PrecastElement> _allElements = new();
            private readonly List<PrecastElement> _visibleElements = new();

            public FakeFilterService(IEnumerable<PrecastElement>? elements = null)
            {
                if (elements != null)
                    SetElements(elements);
            }

            public event Action? StateChanged;

            public IReadOnlyList<PrecastElement> AllElements => _allElements;
            public IReadOnlyList<PrecastElement> VisibleElements => _visibleElements;
            public IReadOnlyList<string> ElementTypes { get; } = Array.Empty<string>();
            public IReadOnlyList<string> Materials { get; } = Array.Empty<string>();
            public IReadOnlyList<string> Storeys { get; } = Array.Empty<string>();
            public int ShowingCount => _visibleElements.Count;
            public int TotalCount => _allElements.Count;
            public double AvailableMinWeightKg => 0;
            public double AvailableMaxWeightKg => 0;
            public double MinWeightKg => 0;
            public double MaxWeightKg => 0;
            public string SearchText => string.Empty;

            public void RaiseStateChanged() => StateChanged?.Invoke();

            public void SetElements(IEnumerable<PrecastElement> elements)
            {
                _allElements.Clear();
                _allElements.AddRange(elements);
                _visibleElements.Clear();
                _visibleElements.AddRange(_allElements);
            }

            public void SetSearchText(string text) { }
            public void SetTypeEnabled(string type, bool enabled) { }
            public void SetMaterialEnabled(string material, bool enabled) { }
            public void SetStoreyEnabled(string storey, bool enabled) { }
            public void SetWeightRange(double minWeightKg, double maxWeightKg) { }
            public void SetIncludedElementIds(IEnumerable<string>? elementIds) { }
            public void SetExcludedElementIds(IEnumerable<string>? elementIds) { }
            public void ResetFilters() => ShowAll();

            public void ShowAll()
            {
                _visibleElements.Clear();
                _visibleElements.AddRange(_allElements);
                RaiseStateChanged();
            }

            public void ShowElements(IEnumerable<PrecastElement> elements)
            {
                var visibleIds = _visibleElements
                    .Select(element => element.ElementID)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var element in elements)
                {
                    if (visibleIds.Add(element.ElementID))
                        _visibleElements.Add(element);
                }

                RaiseStateChanged();
            }

            public void HideElements(IEnumerable<PrecastElement> elements)
            {
                var ids = elements
                    .Select(element => element.ElementID)
                    .ToHashSet(StringComparer.Ordinal);
                _visibleElements.RemoveAll(element => ids.Contains(element.ElementID));
                RaiseStateChanged();
            }

            public void IsolateElements(IEnumerable<PrecastElement> elements)
            {
                _visibleElements.Clear();
                _visibleElements.AddRange(elements);
                RaiseStateChanged();
            }

            public void ApplyLoadOptions(IfcLoadOptionsDto options) { }
            public string GetStorey(PrecastElement element) => "Unassigned";
            public bool IsTypeEnabled(string type) => true;
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
            public bool IsElementIncluded(PrecastElement element) => _visibleElements.Contains(element);
            public bool IsElementHidden(PrecastElement element) => !_visibleElements.Contains(element);
            public bool IsElementExplicitlyHidden(PrecastElement element) => !_visibleElements.Contains(element);
        }
    }
}
