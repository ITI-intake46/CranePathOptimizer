using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CraneOptimizer.UI.Features.F17_Selection.ViewModels
{
    /// <summary>
    /// Lightweight status VM for the current IFC element selection.
    /// Values are recomputed only when the selection changes.
    /// </summary>
    public sealed class SelectionViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISelectionService _selection;
        private int _selectedCount;
        private double _totalWeightKg;

        public SelectionViewModel(ISelectionService selection)
        {
            _selection = selection;
            _selection.SelectionChanged += OnSelectionChanged;
            Refresh();
        }

        public int SelectedCount
        {
            get => _selectedCount;
            private set => SetField(ref _selectedCount, value);
        }

        public double TotalWeightKg
        {
            get => _totalWeightKg;
            private set => SetField(ref _totalWeightKg, value);
        }

        public string StatusText =>
            SelectedCount == 0
                ? string.Empty
                : $"{SelectedCount} selected - {TotalWeightKg / 1000d:0.##} t";

        public bool HasSelection => SelectedCount > 0;

        private void OnSelectionChanged() => Refresh();

        private void Refresh()
        {
            var elements = _selection.SelectedElements;
            var weightKg = 0d;

            foreach (var element in elements)
                weightKg += ElementWeightCalculator.GetWeightKg(element);

            SelectedCount = elements.Count;
            TotalWeightKg = weightKg;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasSelection));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        public void Dispose()
        {
            _selection.SelectionChanged -= OnSelectionChanged;
        }
    }
}
