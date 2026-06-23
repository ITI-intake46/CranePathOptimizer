using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ReportableElementResolver : IReportableElementResolver
{
    public IReadOnlyList<PrecastElement> ResolveReportableElements(IEnumerable<PrecastElement> sourceElements)
    {
        ArgumentNullException.ThrowIfNull(sourceElements);

        var elements = sourceElements.Where(element => element != null).ToList();
        var byId = elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var resolved = new List<PrecastElement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in elements)
        {
            var reportable = ResolveParentOrSelf(element, byId);
            var key = StableKey(reportable);
            if (seen.Add(key))
                resolved.Add(reportable);
        }

        return resolved;
    }

    private static PrecastElement ResolveParentOrSelf(
        PrecastElement element,
        IReadOnlyDictionary<string, PrecastElement> byId)
    {
        if (!string.IsNullOrWhiteSpace(element.ParentGlobalId)
            && byId.TryGetValue(element.ParentGlobalId, out var parent))
        {
            return parent;
        }

        return element;
    }

    private static string StableKey(PrecastElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.ElementID))
            return element.ElementID;

        if (!string.IsNullOrWhiteSpace(element.ElementRevitId))
            return element.ElementRevitId;

        return $"{element.ElementMark}|{element.ElementType}|{element.ElementCOG.X:0.###}|{element.ElementCOG.Y:0.###}|{element.ElementCOG.Z:0.###}";
    }
}
