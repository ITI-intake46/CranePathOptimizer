using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F9_Settings.DTOs;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F9_Settings.ViewModels
{
    public class UserSettingsViewModel : ViewModelBase
    {
        public string UserId      { get; set; } = "—";
        public string UserName    { get; set; } = "—";
        public string OfflineUntil{ get; set; } = "—";
        public string LicenceName { get; set; } = "CraneOptimizer Free";

        public ICommand LogoutCommand { get; }

        public UserSettingsViewModel()
        {
            LogoutCommand = new RelayCommand(Logout);
        }

        public void ApplySettings(UserSettingsDto settings)
        {
            UserId = settings.UserId;
            UserName = settings.UserName;
            OfflineUntil = settings.OfflineUntil;
            LicenceName = settings.LicenceName;
        }

        private void Logout()
        {
            // TODO: clear session and navigate to login/welcome
        }
    }
}
