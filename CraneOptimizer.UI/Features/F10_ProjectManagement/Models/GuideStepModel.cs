using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Models
{
    /// <summary>
    /// Represents one step in the Application Guide stepper.
    /// UI-only — no database or business logic.
    /// </summary>
    public sealed class GuideStepModel : ViewModelBase
    {
        private bool _isSelected;

        public int    StepNumber   { get; init; }
        public string ShortTitle   { get; init; } = string.Empty;
        public string DetailTitle  { get; init; } = string.Empty;
        public string Description  { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}
