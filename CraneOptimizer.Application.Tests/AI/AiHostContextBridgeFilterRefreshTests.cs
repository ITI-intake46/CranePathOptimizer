using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.AIIntegration;
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

        private sealed class FakeFilterService : IElementVisibilityFilterService
        {
            public event Action? StateChanged;

            public IReadOnlyList<PrecastElement> AllElements { get; } = Array.Empty<PrecastElement>();
            public IReadOnlyList<PrecastElement> VisibleElements { get; } = Array.Empty<PrecastElement>();
            public IReadOnlyList<string> ElementTypes { get; } = Array.Empty<string>();
            public IReadOnlyList<string> Materials { get; } = Array.Empty<string>();
            public IReadOnlyList<string> Storeys { get; } = Array.Empty<string>();
            public int ShowingCount => 0;
            public int TotalCount => 0;
            public double AvailableMinWeightKg => 0;
            public double AvailableMaxWeightKg => 0;
            public double MinWeightKg => 0;
            public double MaxWeightKg => 0;
            public string SearchText => string.Empty;

            public void RaiseStateChanged() => StateChanged?.Invoke();
            public void SetElements(IEnumerable<PrecastElement> elements) { }
            public void SetSearchText(string text) { }
            public void SetTypeEnabled(string type, bool enabled) { }
            public void SetMaterialEnabled(string material, bool enabled) { }
            public void SetStoreyEnabled(string storey, bool enabled) { }
            public void SetWeightRange(double minWeightKg, double maxWeightKg) { }
            public void SetIncludedElementIds(IEnumerable<string>? elementIds) { }
            public void SetExcludedElementIds(IEnumerable<string>? elementIds) { }
            public void ResetFilters() { }
            public void ShowAll() { }
            public void ShowElements(IEnumerable<PrecastElement> elements) { }
            public void HideElements(IEnumerable<PrecastElement> elements) { }
            public void IsolateElements(IEnumerable<PrecastElement> elements) { }
            public void ApplyLoadOptions(IfcLoadOptionsDto options) { }
            public string GetStorey(PrecastElement element) => "Unassigned";
            public bool IsTypeEnabled(string type) => true;
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
            public bool IsElementIncluded(PrecastElement element) => true;
            public bool IsElementHidden(PrecastElement element) => false;
        }
    }
}
