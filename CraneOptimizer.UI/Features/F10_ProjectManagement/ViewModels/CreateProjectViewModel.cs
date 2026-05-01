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
