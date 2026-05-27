using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    public interface IElementVisibilityFilterService
    {
        event Action? StateChanged;

        IReadOnlyList<PrecastElement> AllElements { get; }
        IReadOnlyList<PrecastElement> VisibleElements { get; }
        IReadOnlyList<string> ElementTypes { get; }
        IReadOnlyList<string> Materials { get; }
        IReadOnlyList<string> Storeys { get; }

        int ShowingCount { get; }
        int TotalCount { get; }
        double AvailableMinWeightKg { get; }
        double AvailableMaxWeightKg { get; }
        double MinWeightKg { get; }
        double MaxWeightKg { get; }
        string SearchText { get; }

        void SetElements(IEnumerable<PrecastElement> elements);
        void SetSearchText(string text);
        void SetTypeEnabled(string type, bool enabled);
        void SetMaterialEnabled(string material, bool enabled);
        void SetStoreyEnabled(string storey, bool enabled);
        void SetWeightRange(double minWeightKg, double maxWeightKg);
        void SetIncludedElementIds(IEnumerable<string>? elementIds);
        void SetExcludedElementIds(IEnumerable<string>? elementIds);
        void ResetFilters();
        void ShowAll();
        void HideElements(IEnumerable<PrecastElement> elements);
        void IsolateElements(IEnumerable<PrecastElement> elements);
        void ApplyLoadOptions(IfcLoadOptionsDto options);
        string GetStorey(PrecastElement element);
        bool IsTypeEnabled(string type);
        bool IsMaterialEnabled(string material);
        bool IsStoreyEnabled(string storey);
        bool IsElementIncluded(PrecastElement element);
    }
}
