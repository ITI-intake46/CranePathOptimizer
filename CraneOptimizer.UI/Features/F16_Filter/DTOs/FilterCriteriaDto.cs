namespace CraneOptimizer.UI.Features.F16_Filter.DTOs
{
    /// <summary>
    /// Simple filter data chosen in the element filter dialog.
    /// </summary>
    public sealed record FilterCriteriaDto
    {
        public double MinWeight { get; init; }
        public double MaxWeight { get; init; } = 50000;
        public List<string> SelectedTypes { get; init; } = new();
        public List<string> SelectedMaterials { get; init; } = new();
        public List<string> SelectedStoreys { get; init; } = new();
        public List<string>? IncludedElementIds { get; init; }
        public string SortBy { get; init; } = "Name";
        public bool SortDescending { get; init; }
    }
}
