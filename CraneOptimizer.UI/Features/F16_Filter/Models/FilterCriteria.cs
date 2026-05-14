using System.Collections.Generic;

namespace CraneOptimizer.UI.Features.F16_Filter.Models
{
    public class FilterCriteria
    {
        public double MinWeight { get; set; } = 0;
        public double MaxWeight { get; set; } = 50000;
        public List<string> SelectedTypes { get; set; } = new();
        public List<string> SelectedMaterials { get; set; } = new();
        public List<string> SelectedStoreys { get; set; } = new();
        public List<string>? IncludedElementIds { get; set; }
        public string SortBy { get; set; } = "Name";
        public bool SortDescending { get; set; }
    }
}
