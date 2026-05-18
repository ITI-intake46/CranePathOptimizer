using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones.DTOs;
using CraneOptimizer.UI.Features.F7_Zones.Mappers;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public class ZoneListViewModel : ViewModelBase
    {
        private readonly ISceneService _scene;
        public ObservableCollection<ZoneItemViewModel> Zones { get; } = new();
        public int ZoneCount => Zones.Count;

        private ZoneItemViewModel? _selectedZone;
        public ZoneItemViewModel? SelectedZone
        {
            get => _selectedZone;
            set
            {
                _selectedZone = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand DeleteZoneCommand { get; }
        public ICommand ExportZonesCommand { get; }
        public ICommand DeleteAllZonesCommand { get; }

        public ZoneListViewModel(ISceneService scene)
        {
            _scene = scene;

            DeleteZoneCommand = new RelayCommand(DeleteSelected, () => SelectedZone != null);
            ExportZonesCommand = new RelayCommand(Export, () => Zones.Count > 0);
            DeleteAllZonesCommand = new RelayCommand(DeleteAll, () => Zones.Count > 0);

            Zones.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ZoneCount));
                CommandManager.InvalidateRequerySuggested();
            };

            _scene.ZoneAdded += OnZoneAdded;
            _scene.ZoneRemoved += OnZoneRemoved;
            _scene.ZonesCleared += OnZonesCleared;

            SyncFromScene();
        }

        private void DeleteSelected()
        {
            if (SelectedZone?.ZoneData == null)
                return;

            var zone = _scene.GetZones().FirstOrDefault(item => item.ZoneId == SelectedZone.ZoneData.ZoneId);
            if (zone != null)
                _scene.RemoveZone(zone);
        }

        private void Export()
        {
            if (Zones.Count == 0)
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Export Zones JSON",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "zones-export.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            var exportModel = Zones
                .Where(z => z.ZoneData != null)
                .Select(z => z.ZoneData!.ToExportDto())
                .ToList();

            var json = JsonSerializer.Serialize(exportModel, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(dialog.FileName, json);
        }

        private void DeleteAll()
        {
            _scene.ClearZones();
        }

        public void AddOrUpdateFromZone(
            GreenAreaZone zone,
            double? minHeight = null,
            double? maxHeight = null)
        {
            var dto = zone.ToZoneDto(minHeight ?? 0, maxHeight ?? 999);
            var existing = Zones.FirstOrDefault(z => z.ZoneData?.ZoneId == zone.ZoneId);
            if (existing == null)
            {
                Zones.Add(ZoneItemViewModel.FromDto(dto));
                return;
            }

            existing.ApplyDto(dto);
        }

        private void SyncFromScene()
        {
            Zones.Clear();
            foreach (var zone in _scene.GetZones())
            {
                Zones.Add(ZoneItemViewModel.FromDto(zone.ToZoneDto()));
            }
        }

        private void OnZoneAdded(GreenAreaZone zone) => AddOrUpdateFromZone(zone);

        private void OnZoneRemoved(GreenAreaZone zone)
        {
            var existing = Zones.FirstOrDefault(z => z.ZoneData?.ZoneId == zone.ZoneId);
            if (existing != null)
            {
                Zones.Remove(existing);
            }
        }

        private void OnZonesCleared()
        {
            Zones.Clear();
            SelectedZone = null;
        }
    }

    public class ZoneItemViewModel : ViewModelBase
    {
        private bool _isVisible = true;
        private string _name = "Zone";
        private string _zoneType = "Allowed";
        private double _minHeight;
        private double _maxHeight = 999;

        public ZoneDto? ZoneData { get; private set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string ZoneType
        {
            get => _zoneType;
            set { _zoneType = value; OnPropertyChanged(); }
        }

        public double MinHeight
        {
            get => _minHeight;
            set { _minHeight = value; OnPropertyChanged(); }
        }

        public double MaxHeight
        {
            get => _maxHeight;
            set { _maxHeight = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public static ZoneItemViewModel FromDto(ZoneDto dto)
        {
            var vm = new ZoneItemViewModel();
            vm.ApplyDto(dto);
            return vm;
        }

        public void ApplyDto(ZoneDto dto)
        {
            ZoneData = dto;
            Name = dto.Name;
            ZoneType = dto.ZoneType;
            MinHeight = dto.MinHeight;
            MaxHeight = dto.MaxHeight;
        }
    }
}
