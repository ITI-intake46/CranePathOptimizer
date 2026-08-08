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

    // The 3D viewport renders from VisibleElements and repaints on StateChanged.
    // IsElementHidden only drives dialog badges, so these two assertions are what
    // actually prove "Hide Checked" reaches the viewer.
    [Fact]
    public void HideElements_DropsElementFromVisibleElements()
    {
        var first = CreateElement("FIRST");
        var second = CreateElement("SECOND");
        var service = CreateService(first, second);

        service.HideElements([first]);

        Assert.DoesNotContain(first, service.VisibleElements);
        Assert.Contains(second, service.VisibleElements);
        Assert.Equal(1, service.ShowingCount);
    }

    [Fact]
    public void HideElements_RaisesStateChangedSoTheViewportRepaints()
    {
        var element = CreateElement("FIRST");
        var service = CreateService(element);

        var raised = 0;
        service.StateChanged += () => raised++;

        service.HideElements([element]);

        Assert.Equal(1, raised);
    }

    // Regression: the Hide/Show toggle used IsElementHidden, which is true for any
    // element that is merely filtered out. With a search term active that made
    // "Hide Checked" run ShowElements instead of hiding.
    [Fact]
    public void IsElementExplicitlyHidden_IsFalseForElementsOnlyFilteredOut()
    {
        var element = CreateElement("FIRST");
        var service = CreateService(element);

        service.SetSearchText("no-such-element");

        Assert.True(service.IsElementHidden(element));
        Assert.False(service.IsElementExplicitlyHidden(element));

        service.HideElements([element]);

        Assert.True(service.IsElementExplicitlyHidden(element));
    }

    // The filter lists assemblies, but the viewport draws their child parts.
    // Hiding an assembly must take its parts with it or nothing appears to change.
    [Fact]
    public void HideElements_AlsoHidesTheAssemblyChildPartsThatCarryTheGeometry()
    {
        var assembly = CreateElement("ASSEMBLY");
        var partOne = CreateElement("PART-1");
        var partTwo = CreateElement("PART-2");
        partOne.ParentGlobalId = "ASSEMBLY";
        partTwo.ParentGlobalId = "ASSEMBLY";
        var unrelated = CreateElement("OTHER");
        var service = CreateService(assembly, partOne, partTwo, unrelated);

        service.HideElements([assembly]);

        Assert.True(service.IsElementExplicitlyHidden(partOne));
        Assert.True(service.IsElementExplicitlyHidden(partTwo));
        Assert.False(service.IsElementExplicitlyHidden(unrelated));
        Assert.Equal(1, service.ShowingCount);
    }

    [Fact]
    public void ShowElements_RestoresAssemblyChildPartsToo()
    {
        var assembly = CreateElement("ASSEMBLY");
        var part = CreateElement("PART-1");
        part.ParentGlobalId = "ASSEMBLY";
        var service = CreateService(assembly, part);

        service.HideElements([assembly]);
        service.ShowElements([assembly]);

        Assert.False(service.IsElementHidden(part));
        Assert.Equal(2, service.ShowingCount);
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

        public CraneOptimizer.Domain.CreatedProject.IfcGeoreferencingResult? GetGeoreferencingData() => null;

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
