using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F16_Filter.ViewModels
{
    public sealed class FilterDialogOption : ViewModelBase
    {
        private bool _isChecked;

        public FilterDialogOption(string displayName, int count, bool isChecked)
        {
            DisplayName = displayName;
            Count = count;
            _isChecked = isChecked;
        }

        public string DisplayName { get; }
        public int Count { get; }

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
    }
}
