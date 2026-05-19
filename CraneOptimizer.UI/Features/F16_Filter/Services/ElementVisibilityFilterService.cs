using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Converters;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.UI.Features.F16_Filter.Services
{
    public sealed class ElementVisibilityFilterService : IElementVisibilityFilterService
    {
        private readonly List<PrecastElement> _allElements = new();
        private readonly List<PrecastElement> _visibleElements = new();
        private readonly HashSet<string> _enabledTypes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _enabledMaterials = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _enabledStoreys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _hiddenElementIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _isolatedElementIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _storeyByElementId = new(StringComparer.OrdinalIgnoreCase);
        private readonly IIfcReaderRepository _ifcReader;
        private HashSet<string>? _includedElementIds;

        public ElementVisibilityFilterService(IIfcReaderRepository ifcReader)
        {
            _ifcReader = ifcReader;
        }

        public event Action? StateChanged;

        public IReadOnlyList<PrecastElement> AllElements => _allElements;
        public IReadOnlyList<PrecastElement> VisibleElements => _visibleElements;
        public IReadOnlyList<string> ElementTypes { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> Materials { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> Storeys { get; private set; } = Array.Empty<string>();
        public int ShowingCount => _visibleElements.Count;
        public int TotalCount => _allElements.Count;
        public double AvailableMinWeightKg { get; private set; }
        public double AvailableMaxWeightKg { get; private set; }
        public double MinWeightKg { get; private set; }
        public double MaxWeightKg { get; private set; }
        public string SearchText { get; private set; } = string.Empty;

        public void SetElements(IEnumerable<PrecastElement> elements)
        {
            _allElements.Clear();
            _allElements.AddRange(elements);
            _hiddenElementIds.Clear();
            _isolatedElementIds.Clear();
            _includedElementIds = null;
            SearchText = string.Empty;

            BuildStoreyMap();
            ElementTypes = BuildOptions(_allElements.Select(e => Normalize(e.ElementType, "Unknown type")));
            Materials = BuildOptions(_allElements.Select(e => Normalize(e.ElementMaterial, "Unspecified")));
            Storeys = BuildOptions(_allElements.Select(GetStorey));

            ResetEnabledSets();

            var weights = _allElements.Select(ElementWeightCalculator.GetWeightKg).ToList();
            AvailableMinWeightKg = weights.Count == 0 ? 0 : Math.Floor(weights.Min());
            AvailableMaxWeightKg = weights.Count == 0 ? 0 : Math.Ceiling(weights.Max());
            MinWeightKg = AvailableMinWeightKg;
            MaxWeightKg = AvailableMaxWeightKg;

            Recalculate();
        }

        public void SetSearchText(string text)
        {
            SearchText = text?.Trim() ?? string.Empty;
            Recalculate();
        }

        public void SetTypeEnabled(string type, bool enabled) => SetEnabled(_enabledTypes, Normalize(type, "Unknown type"), enabled);
        public void SetMaterialEnabled(string material, bool enabled) => SetEnabled(_enabledMaterials, Normalize(material, "Unspecified"), enabled);
        public void SetStoreyEnabled(string storey, bool enabled) => SetEnabled(_enabledStoreys, Normalize(storey, "Unassigned"), enabled);

        public void SetWeightRange(double minWeightKg, double maxWeightKg)
        {
            if (maxWeightKg < minWeightKg)
                (minWeightKg, maxWeightKg) = (maxWeightKg, minWeightKg);

            MinWeightKg = Math.Max(AvailableMinWeightKg, minWeightKg);
            MaxWeightKg = Math.Min(AvailableMaxWeightKg, maxWeightKg);
            Recalculate();
        }

        public void SetIncludedElementIds(IEnumerable<string>? elementIds)
        {
            _includedElementIds = elementIds == null
                ? null
                : elementIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Recalculate();
        }

        public void ResetFilters()
        {
            SearchText = string.Empty;
            ResetEnabledSets();
            MinWeightKg = AvailableMinWeightKg;
            MaxWeightKg = AvailableMaxWeightKg;
            _hiddenElementIds.Clear();
            _isolatedElementIds.Clear();
            _includedElementIds = null;
            Recalculate();
        }

        public void ShowAll()
        {
            ResetFilters();
        }

        public void HideElements(IEnumerable<PrecastElement> elements)
        {
            foreach (var element in elements)
            {
                if (!string.IsNullOrWhiteSpace(element.ElementID))
                    _hiddenElementIds.Add(element.ElementID);
            }

            Recalculate();
        }

        public void IsolateElements(IEnumerable<PrecastElement> elements)
        {
            _isolatedElementIds.Clear();
            foreach (var element in elements)
            {
                if (!string.IsNullOrWhiteSpace(element.ElementID))
                    _isolatedElementIds.Add(element.ElementID);
            }

            Recalculate();
        }

        public void ApplyLoadOptions(IfcLoadOptionsDto options)
        {
            ResetFilters();

            foreach (var type in ElementTypes)
            {
                var enabled = true;
                if (options.HideGrids && type.Contains("IfcGrid", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideLevels && type.Contains("IfcBuildingStorey", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideAnnotations && type.Contains("IfcAnnotation", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideSite && type.Contains("IfcSite", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideSpaces && type.Contains("IfcSpace", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideOpenings && type.Contains("IfcOpeningElement", StringComparison.OrdinalIgnoreCase)) enabled = false;
                if (options.HideVirtualElements && type.Contains("IfcVirtualElement", StringComparison.OrdinalIgnoreCase)) enabled = false;

                if (!enabled)
                    _enabledTypes.Remove(type);
            }

            Recalculate();
        }

        public string GetStorey(PrecastElement element)
        {
            if (!string.IsNullOrWhiteSpace(element.ElementID) && _storeyByElementId.TryGetValue(element.ElementID, out var storey))
                return storey;

            return "Unassigned";
        }

        public bool IsTypeEnabled(string type) => _enabledTypes.Contains(Normalize(type, "Unknown type"));
        public bool IsMaterialEnabled(string material) => _enabledMaterials.Contains(Normalize(material, "Unspecified"));
        public bool IsStoreyEnabled(string storey) => _enabledStoreys.Contains(Normalize(storey, "Unassigned"));
        public bool IsElementIncluded(PrecastElement element) =>
            _includedElementIds == null
            || (!string.IsNullOrWhiteSpace(element.ElementID) && _includedElementIds.Contains(element.ElementID));

        private void SetEnabled(HashSet<string> set, string value, bool enabled)
        {
            bool changed;
            if (enabled)
                changed = set.Add(value);
            else
                changed = set.Remove(value);

            if (changed)
                Recalculate();
        }

        private void Recalculate()
        {
            _visibleElements.Clear();
            _visibleElements.AddRange(_allElements.Where(IsVisible));
            StateChanged?.Invoke();
        }

        private bool IsVisible(PrecastElement element)
        {
            var id = element.ElementID ?? string.Empty;

            if (_hiddenElementIds.Contains(id))
                return false;

            if (_includedElementIds != null && !_includedElementIds.Contains(id))
                return false;

            if (_isolatedElementIds.Count > 0 && !_isolatedElementIds.Contains(id))
                return false;

            if (!_enabledTypes.Contains(Normalize(element.ElementType, "Unknown type")))
                return false;

            if (!_enabledMaterials.Contains(Normalize(element.ElementMaterial, "Unspecified")))
                return false;

            if (!_enabledStoreys.Contains(GetStorey(element)))
                return false;

            var weight = ElementWeightCalculator.GetWeightKg(element);
            if (weight < MinWeightKg || weight > MaxWeightKg)
                return false;

            return string.IsNullOrWhiteSpace(SearchText) || MatchesSearch(element, SearchText);
        }

        private static bool MatchesSearch(PrecastElement element, string search)
        {
            var name = ElementDisplayNameConverter.GetDisplayName(element);

            return Matches(name, search)
                || Matches(element.ElementType, search)
                || Matches(element.ElementMark, search)
                || Matches(element.ElementMaterial, search)
                || Matches(element.ElementID, search)
                || Matches(element.ElementRevitId, search);
        }

        private static bool Matches(string? value, string search) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);

        private void ResetEnabledSets()
        {
            _enabledTypes.Clear();
            _enabledMaterials.Clear();
            _enabledStoreys.Clear();

            foreach (var type in ElementTypes) _enabledTypes.Add(type);
            foreach (var material in Materials) _enabledMaterials.Add(material);
            foreach (var storey in Storeys) _enabledStoreys.Add(storey);
        }

        private void BuildStoreyMap()
        {
            _storeyByElementId.Clear();

            if (TryBuildIfcStoreyMap())
                return;

            var elevations = _allElements
                .Select(GetBaseElevationMeters)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            var labels = elevations
                .Select((z, index) => new
                {
                    Elevation = z,
                    Label = $"Level {index + 1} ({z:0.##} m)"
                })
                .ToDictionary(x => x.Elevation, x => x.Label);

            foreach (var element in _allElements)
            {
                if (!string.IsNullOrWhiteSpace(element.ElementID))
                    _storeyByElementId[element.ElementID] = labels[GetBaseElevationMeters(element)];
            }
        }

        private bool TryBuildIfcStoreyMap()
        {
            if (_ifcReader is not IfcReaderRepository repository || repository.CurrentModel == null)
                return false;

            var model = repository.CurrentModel;
            var idAliasesByProduct = _allElements
                .SelectMany(element => BuildElementAliases(element).Select(alias => new { Alias = alias, Element = element }))
                .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Element, StringComparer.OrdinalIgnoreCase);

            var assigned = 0;

            foreach (var storey in model.Instances.OfType<IIfcBuildingStorey>())
            {
                var storeyName = storey.Name?.ToString();
                if (string.IsNullOrWhiteSpace(storeyName))
                    storeyName = $"Level {storey.Elevation.GetValueOrDefault():0.##}";

                var containments = model.Instances
                    .OfType<IIfcRelContainedInSpatialStructure>()
                    .Where(rel => rel.RelatingStructure == storey);

                foreach (var relation in containments)
                {
                    foreach (var product in relation.RelatedElements.OfType<IIfcProduct>())
                    {
                        var aliases = BuildProductAliases(product);
                        foreach (var alias in aliases)
                        {
                            if (!idAliasesByProduct.TryGetValue(alias, out var element) || string.IsNullOrWhiteSpace(element.ElementID))
                                continue;

                            _storeyByElementId[element.ElementID] = storeyName;
                            assigned++;
                            break;
                        }
                    }
                }
            }

            return assigned > 0;
        }

        private static IEnumerable<string> BuildElementAliases(PrecastElement element)
        {
            if (!string.IsNullOrWhiteSpace(element.ElementID)) yield return element.ElementID;
            if (!string.IsNullOrWhiteSpace(element.ElementRevitId)) yield return element.ElementRevitId;
            if (!string.IsNullOrWhiteSpace(element.ElementMark)) yield return element.ElementMark;
        }

        private static IEnumerable<string> BuildProductAliases(IIfcProduct product)
        {
            var globalId = product.GlobalId.ToString();
            if (!string.IsNullOrWhiteSpace(globalId)) yield return globalId;

            var tag = (product as IIfcElement)?.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(tag))
                yield return tag;

            var name = product.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }

        private static double GetBaseElevationMeters(PrecastElement element)
        {
            var z = element.ElementCOG?.Z ?? 0d;
            var halfHeight = (element.BBoxHeight ?? element.ElementHeight ?? 0d) / 2d;
            var baseZ = z - halfHeight;

            return Math.Round(baseZ, 0);
        }

        private static IReadOnlyList<string> BuildOptions(IEnumerable<string> values) =>
            values.Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string Normalize(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    }
}
