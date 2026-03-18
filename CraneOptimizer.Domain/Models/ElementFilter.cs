using System.Collections.Generic;

namespace WPF_IFC_test1.Models
{
    /// <summary>
    /// Holds all active filter criteria for the Building Elements panel.
    /// </summary>
    public class ElementFilter
    {
        public string SearchText { get; set; } = "";

        /// <summary>Null = all types allowed. Non-null = only listed types shown.</summary>
        public HashSet<string> AllowedTypes { get; set; } = null;

        /// <summary>Null = all materials allowed.</summary>
        public HashSet<string> AllowedMaterials { get; set; } = null;

        public double? MinWeightKg { get; set; } = null;
        public double? MaxWeightKg { get; set; } = null;

        public double? MinHeightM { get; set; } = null;
        public double? MaxHeightM { get; set; } = null;

        public bool OnlySelected { get; set; } = false;

        public ElementSortField SortField { get; set; } = ElementSortField.Name;
        public bool SortDescending { get; set; } = false;

        public ElementGroupBy GroupBy { get; set; } = ElementGroupBy.None;

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(SearchText) &&
            AllowedTypes == null &&
            AllowedMaterials == null &&
            MinWeightKg == null &&
            MaxWeightKg == null &&
            MinHeightM == null &&
            MaxHeightM == null &&
            !OnlySelected &&
            SortField == ElementSortField.Name &&
            !SortDescending &&
            GroupBy == ElementGroupBy.None;

        public ElementFilter Clone()
        {
            return new ElementFilter
            {
                SearchText = SearchText,
                AllowedTypes = AllowedTypes == null ? null : new HashSet<string>(AllowedTypes),
                AllowedMaterials = AllowedMaterials == null ? null : new HashSet<string>(AllowedMaterials),
                MinWeightKg = MinWeightKg,
                MaxWeightKg = MaxWeightKg,
                MinHeightM = MinHeightM,
                MaxHeightM = MaxHeightM,
                OnlySelected = OnlySelected,
                SortField = SortField,
                SortDescending = SortDescending,
                GroupBy = GroupBy
            };
        }
    }

    public enum ElementSortField
    {
        Name,
        Type,
        WeightAsc,
        WeightDesc,
        HeightAsc,
        HeightDesc,
        Mark
    }

    public enum ElementGroupBy
    {
        None,
        Type,
        Material,
        WeightCategory
    }
}