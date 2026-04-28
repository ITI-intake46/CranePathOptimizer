using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class SplashScreenViewModel : ViewModelBase
    {
        private string _status = "Starting...";

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged();
            }
        }
    }
}
