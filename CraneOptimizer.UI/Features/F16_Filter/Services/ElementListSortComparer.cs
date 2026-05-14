using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Converters;
using System;
using System.Collections;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    internal sealed class ElementListSortComparer : IComparer
    {
        private readonly string _sortBy;
        private readonly bool _descending;
        private readonly IElementVisibilityFilterService _visibilityFilter;

        public ElementListSortComparer(string sortBy, bool descending, IElementVisibilityFilterService visibilityFilter)
        {
            _sortBy = sortBy;
            _descending = descending;
            _visibilityFilter = visibilityFilter;
        }

        public int Compare(object? x, object? y)
        {
            var left = x as PrecastElement;
            var right = y as PrecastElement;
            if (left == null || right == null)
                return 0;

            var result = CompareValues(GetValue(left), GetValue(right));
            if (result == 0)
                result = string.Compare(ElementDisplayNameConverter.GetDisplayName(left), ElementDisplayNameConverter.GetDisplayName(right), StringComparison.CurrentCultureIgnoreCase);

            return _descending ? -result : result;
        }

        private object GetValue(PrecastElement element) =>
            _sortBy switch
            {
                "Type" => element.ElementType ?? string.Empty,
                "Weight" => ElementWeightCalculator.GetWeightKg(element),
                "Height" => element.ElementHeight ?? element.BBoxHeight ?? 0d,
                "Material" => element.ElementMaterial ?? string.Empty,
                "Storey / Level" => _visibilityFilter.GetStorey(element),
                _ => ElementDisplayNameConverter.GetDisplayName(element)
            };

        private static int CompareValues(object left, object right)
        {
            if (left is double leftNumber && right is double rightNumber)
                return leftNumber.CompareTo(rightNumber);

            return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
