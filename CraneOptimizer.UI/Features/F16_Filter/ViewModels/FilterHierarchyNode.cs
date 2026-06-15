using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CraneOptimizer.UI.Features.F16_Filter.ViewModels
{
    public sealed class FilterHierarchyNode : ViewModelBase
    {
        private bool _isChecked = true;
        private bool _isExpanded;

        public string DisplayName { get; private init; } = string.Empty;
        public string WeightText { get; private init; } = string.Empty;
        public double? WeightValue { get; private init; }
        public string MaterialLabel { get; private init; } = string.Empty;
        public string WeightLabel { get; private init; } = string.Empty;
        public bool IsHidden { get; private init; }
        public PrecastElement? Element { get; private init; }
        public FilterHierarchyNode? Parent { get; private init; }
        public ObservableCollection<FilterHierarchyNode> Children { get; } = new();

        public int Count => Children.Count;
        public Visibility CountVisibility => Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility WeightVisibility => Element != null && !string.IsNullOrWhiteSpace(WeightText) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MaterialVisibility => Element != null && !string.IsNullOrWhiteSpace(MaterialLabel) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility WeightLabelVisibility => Element != null && !string.IsNullOrWhiteSpace(WeightLabel) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SubtitleRowVisibility => MaterialVisibility == Visibility.Visible || WeightLabelVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VisibilityStatusVisibility => IsHidden ? Visibility.Visible : Visibility.Collapsed;
        public string VisibilityStatus => IsHidden ? "Hidden" : string.Empty;
        public double DisplayOpacity => IsHidden ? 0.45d : 1d;
        public FontWeight FontWeight => Children.Count > 0 ? FontWeights.Bold : FontWeights.SemiBold;
        public Brush Foreground => Children.Count > 0 ? Brushes.DarkOrange : Brushes.LightSteelBlue;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public static FilterHierarchyNode CreateGroup(string name) =>
            new() { DisplayName = name };

        public static FilterHierarchyNode CreateElement(
            PrecastElement element,
            string name,
            string weight,
            double weightValue,
            string material,
            bool isChecked,
            bool isHidden,
            FilterHierarchyNode parent) =>
            new()
            {
                Element = element,
                DisplayName = name,
                WeightText = weight,
                WeightValue = weightValue,
                MaterialLabel = string.IsNullOrWhiteSpace(material) ? "N/A" : material.Trim(),
                WeightLabel = weight,
                IsChecked = isChecked,
                IsHidden = isHidden,
                Parent = parent
            };

        public void SetChildrenChecked(bool isChecked)
        {
            foreach (var child in Children)
                child.IsChecked = isChecked;

            RefreshFromChildren();
        }

        public void RefreshFromChildren()
        {
            if (Children.Count == 0)
                return;

            IsChecked = Children.All(child => child.IsChecked);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CountVisibility));
        }

        public void SetExpandedRecursive(bool isExpanded)
        {
            IsExpanded = isExpanded;
            foreach (var child in Children)
                child.SetExpandedRecursive(isExpanded);
        }
    }
}
