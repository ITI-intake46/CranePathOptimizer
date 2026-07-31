using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfSourcePickerDialogViewModel : ViewModelBase
    {
        private DxfSourceSession? _selectedDxf;

        public DxfSourcePickerDialogViewModel(
            string title,
            string primaryButtonText,
            IReadOnlyList<DxfSourceSession> sources,
            DxfSourceSession? preselected)
        {
            Title = title;
            PrimaryButtonText = primaryButtonText;
            Sources = sources;
            _selectedDxf = preselected ?? sources.FirstOrDefault();
        }

        public string Title { get; }
        public string PrimaryButtonText { get; }
        public IReadOnlyList<DxfSourceSession> Sources { get; }

        public DxfSourceSession? SelectedDxf
        {
            get => _selectedDxf;
            set
            {
                if (_selectedDxf == value) return;
                _selectedDxf = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirm));
            }
        }

        public bool CanConfirm => SelectedDxf != null;
    }
}
