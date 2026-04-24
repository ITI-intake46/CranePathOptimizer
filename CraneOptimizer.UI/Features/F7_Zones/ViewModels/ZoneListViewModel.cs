using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public class ZoneListViewModel : ViewModelBase
    {
        public ObservableCollection<ZoneItemViewModel> Zones { get; } = new();

        private ZoneItemViewModel? _selectedZone;
        public ZoneItemViewModel? SelectedZone
        {
            get => _selectedZone;
            set { _selectedZone = value; OnPropertyChanged(); }
        }

        public ICommand DeleteZoneCommand  { get; }
        public ICommand ExportZonesCommand { get; }

        public ZoneListViewModel()
        {
            DeleteZoneCommand  = new RelayCommand(DeleteSelected, () => SelectedZone != null);
            ExportZonesCommand = new RelayCommand(Export);
        }

        private void DeleteSelected()
        {
            if (SelectedZone != null)
                Zones.Remove(SelectedZone);
        }

        private void Export()
        {
            // TODO: export zones to JSON
        }
    }

    public class ZoneItemViewModel : ViewModelBase
    {
        private bool _isVisible = true;

        public string Name     { get; set; } = "Zone";
        public string ZoneType { get; set; } = "Allowed";   // Allowed | Forbidden | SiteBoundary

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }
    }
}
