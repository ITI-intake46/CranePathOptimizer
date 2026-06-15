using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F16_Filter.Services;

namespace CraneOptimizer.Application.Tests.IFC.Filtering;

public sealed class ElementVisibilityFilterServiceTests
{
    [Fact]
    public void HideAndShowElements_UseCaseSensitiveElementIds()
    {
        var upper = CreateElement("A");
        var lower = CreateElement("a");
        var service = CreateService(upper, lower);

        service.HideElements([upper]);

        Assert.True(service.IsElementHidden(upper));
        Assert.False(service.IsElementHidden(lower));

        service.ShowElements([upper]);

        Assert.False(service.IsElementHidden(upper));
        Assert.False(service.IsElementHidden(lower));
    }

    [Fact]
    public void IsolateElements_ShowsTargetsAndKeepsSelectionTargetsIndependent()
    {
        var first = CreateElement("FIRST");
        var second = CreateElement("SECOND");
        var third = CreateElement("THIRD");
        var service = CreateService(first, second, third);

        service.HideElements([first]);
        service.IsolateElements([first, second]);

        Assert.False(service.IsElementHidden(first));
        Assert.False(service.IsElementHidden(second));
        Assert.True(service.IsElementHidden(third));
    }

    [Fact]
    public void ShowElements_AddsCheckedElementToActiveIsolation()
    {
        var first = CreateElement("FIRST");
        var second = CreateElement("SECOND");
        var service = CreateService(first, second);

        service.IsolateElements([first]);
        service.ShowElements([second]);

        Assert.False(service.IsElementHidden(first));
        Assert.False(service.IsElementHidden(second));
    }

    [Fact]
    public void ShowAll_RestoresEveryElement()
    {
        var first = CreateElement("FIRST");
        var second = CreateElement("SECOND");
        var service = CreateService(first, second);

        service.HideElements([first]);
        service.IsolateElements([second]);
        service.ShowAll();

        Assert.False(service.IsElementHidden(first));
        Assert.False(service.IsElementHidden(second));
        Assert.Equal(2, service.VisibleElements.Count);
    }

    private static ElementVisibilityFilterService CreateService(params PrecastElement[] elements)
    {
        var service = new ElementVisibilityFilterService(new StubIfcReaderRepository());
        service.SetElements(elements);
        return service;
    }

    private static PrecastElement CreateElement(string id) =>
        new()
        {
            ElementID = id,
            ElementType = "IfcColumn",
            ElementMaterial = "Concrete"
        };

    private sealed class StubIfcReaderRepository : IIfcReaderRepository
    {
        public double ModelLengthFactor => 1d;
        public double? ModelGroundElevation => null;

        public List<PrecastElement> ReadElements(string ifcPath) => [];
        public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options) => [];
        public List<PrecastElement> ReadElements(string path, IProgress<double> progress) => [];

        public List<PrecastElement> ReadElements(
            string path,
            IProgress<double> progress,
            IfcReadingOptions? options,
            CancellationToken cancellationToken = default) => [];

        public Task InitializeSessionForRenderingAsync(
            string ifcPath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ClearData()
        {
        }

        public void Dispose()
        {
        }
    }
}
