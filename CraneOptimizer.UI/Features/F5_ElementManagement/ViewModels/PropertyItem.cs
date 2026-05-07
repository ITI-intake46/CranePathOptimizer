using System.Collections.ObjectModel;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels
{
    public class PropertyItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = true;
        public ObservableCollection<PropertyItem> Children { get; } = new();
    }
}
