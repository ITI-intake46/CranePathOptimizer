using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using System.IO;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class CreateProjectViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private string _projectName = string.Empty;
        private string _ifcFilePath = string.Empty;

        public CreateProjectViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            BrowseIfcCommand = new RelayCommand(BrowseIfc);
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

        public string IfcFilePath
        {
            get => _ifcFilePath;
            set
            {
                if (_ifcFilePath == value)
                    return;

                _ifcFilePath = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand BrowseIfcCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }

        private void BrowseIfc()
        {
            var path = _dialogService.OpenFile("IFC files (*.ifc)|*.ifc|All files (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(path))
                return;

            IfcFilePath = path;

            if (string.IsNullOrWhiteSpace(ProjectName))
                ProjectName = Path.GetFileNameWithoutExtension(path);
        }

        private bool CanCreate()
        {
            return !string.IsNullOrWhiteSpace(ProjectName)
                && !string.IsNullOrWhiteSpace(IfcFilePath);
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
