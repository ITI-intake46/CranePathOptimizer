using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CraneOptimizer.UI.Features.F17_Selection.ViewModels
{
    /// <summary>
    /// Lightweight status VM for the current IFC element selection.
    /// Values are recomputed only when the selection changes.
    /// </summary>
    public sealed class SelectionViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IViewportSelectionService _selection;
        private int _selectedCount;
        private double _totalWeightKg;

        public SelectionViewModel(IViewportSelectionService selection)
        {
            _selection = selection;
            _selection.SelectionChanged += OnSelectionChanged;
            ElementWeightCalculator.ConcreteDensityChanged += OnConcreteDensityChanged;
            Refresh();
        }

        private void OnConcreteDensityChanged(object? sender, EventArgs e) => Refresh();

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

        private void OnSelectionChanged(IReadOnlyList<SelectionTarget> targets) => Refresh();

        private void Refresh()
        {
            var elements = _selection.GetSelectedTargets()
                .Where(t => t.Kind == SelectionKind.IfcElement)
                .Select(t => t.UnderlyingObject)
                .OfType<PrecastElement>()
                .ToList();

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
            ElementWeightCalculator.ConcreteDensityChanged -= OnConcreteDensityChanged;
        }
    }
}
