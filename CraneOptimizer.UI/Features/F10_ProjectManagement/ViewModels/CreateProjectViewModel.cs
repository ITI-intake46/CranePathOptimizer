using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class CreateProjectViewModel : ViewModelBase
    {
        private string _projectName = string.Empty;

        public CreateProjectViewModel()
        {
            CreateCommand = new RelayCommand(Create, CanCreate);
            CancelCommand = new RelayCommand(Cancel);
        }

        public event EventHandler? CreateRequested;
        public event EventHandler? CancelRequested;

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (_projectName == value)
                    return;

                _projectName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ── Unit selection ─────────────────────────────────────────────
        // Indices map to ComboBox items defined in the XAML.

        private int _selectedLengthIndex = 1;   // 0=mm, 1=m
        public int SelectedLengthIndex
        {
            get => _selectedLengthIndex;
            set { _selectedLengthIndex = value; OnPropertyChanged(); }
        }

        private int _selectedAreaIndex;         // 0=m², 1=ft²
        public int SelectedAreaIndex
        {
            get => _selectedAreaIndex;
            set { _selectedAreaIndex = value; OnPropertyChanged(); }
        }

        private int _selectedMassIndex = 1;     // 0=kg, 1=t
        public int SelectedMassIndex
        {
            get => _selectedMassIndex;
            set { _selectedMassIndex = value; OnPropertyChanged(); }
        }

        private int _selectedPrecisionIndex = 2; // 0..3 decimal places
        public int SelectedPrecisionIndex
        {
            get => _selectedPrecisionIndex;
            set { _selectedPrecisionIndex = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Builds a <see cref="UnitSettings"/> from the current selection.
        /// </summary>
        public UnitSettings BuildUnitSettings() => new()
        {
            DisplayLengthUnit = SelectedLengthIndex == 0 ? LengthUnit.Millimeter : LengthUnit.Meter,
            DisplayAreaUnit   = (AreaUnit)Math.Max(0, SelectedAreaIndex),
            DisplayMassUnit   = (MassUnit)Math.Max(0, SelectedMassIndex),
            DecimalPlaces     = Math.Clamp(SelectedPrecisionIndex, 0, 6)
        };

        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanCreate()
        {
            return !string.IsNullOrWhiteSpace(ProjectName);
        }

        private void Create()
        {
            CreateRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
