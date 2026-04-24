using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels
{
    public class CraneManagerViewModel : ViewModelBase
    {
        // ── Properties ────────────────────────────────────────────────────
        public ObservableCollection<CraneItemViewModel> Cranes { get; } = new();

        private CraneItemViewModel? _selectedCrane;
        public CraneItemViewModel? SelectedCrane
        {
            get => _selectedCrane;
            set { _selectedCrane = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand AddTowerCraneCommand  { get; }
        public ICommand AddMobileCraneCommand { get; }
        public ICommand RemoveCraneCommand    { get; }
        public ICommand RemoveAllCranesCommand{ get; }

        public CraneManagerViewModel()
        {
            AddTowerCraneCommand   = new RelayCommand(AddTowerCrane);
            AddMobileCraneCommand  = new RelayCommand(AddMobileCrane);
            RemoveCraneCommand     = new RelayCommand<CraneItemViewModel>(RemoveCrane);
            RemoveAllCranesCommand = new RelayCommand(RemoveAll);
        }

        private void AddTowerCrane()
        {
            var crane = new CraneItemViewModel
            {
                Name = $"Tower Crane {Cranes.Count + 1}",
                Type = "TowerCrane"
            };
            Cranes.Add(crane);
            SelectedCrane = crane;
        }

        private void AddMobileCrane()
        {
            var crane = new CraneItemViewModel
            {
                Name = $"Mobile Crane {Cranes.Count + 1}",
                Type = "MobileCrane"
            };
            Cranes.Add(crane);
            SelectedCrane = crane;
        }

        private void RemoveCrane(CraneItemViewModel? crane)
        {
            if (crane != null) Cranes.Remove(crane);
        }

        private void RemoveAll() => Cranes.Clear();
    }

    public class CraneItemViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string _type = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }
    }
}
