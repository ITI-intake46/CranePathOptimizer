using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F7_Zones.DTOs;
using CraneOptimizer.UI.Features.F7_Zones.Mappers;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public class ZoneListViewModel : ViewModelBase
    {
        private readonly ISceneService _scene;
        private readonly IUnitSettingsService? _unitSettings;
        private readonly IDeleteConfirmationService _deleteConfirmation;
        private readonly Dictionary<Guid, string> _fallbackNames = new();
        private readonly Dictionary<string, int> _fallbackCounters = new(
            StringComparer.Ordinal);
        private bool _syncingSelection;
        public ObservableCollection<ZoneItemViewModel> Zones { get; } = new();
        public int ZoneCount => Zones.Count;

        private ZoneItemViewModel? _selectedZone;
        public ZoneItemViewModel? SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (ReferenceEquals(_selectedZone, value))
                    return;

                _selectedZone = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
                if (_syncingSelection)
                    return;

                GreenAreaZone? sceneZone = value?.ZoneData == null
                    ? null
                    : _scene.GetZones().FirstOrDefault(
                        zone => zone.ZoneId == value.ZoneData.ZoneId);
                _scene.SetSelectedZone(sceneZone);
            }
        }

        public ICommand DeleteZoneCommand { get; }
        public ICommand ExportZonesCommand { get; }
        public ICommand DeleteAllZonesCommand { get; }
        public ICommand RenameZoneCommand { get; }

        public ZoneListViewModel(
            ISceneService scene,
            IUnitSettingsService? unitSettings = null,
            IDeleteConfirmationService? deleteConfirmation = null)
        {
            _scene = scene;
            _unitSettings = unitSettings;
            _deleteConfirmation = deleteConfirmation ?? new DeleteConfirmationService();

            DeleteZoneCommand = new RelayCommand(DeleteSelected, () => SelectedZone != null);
            ExportZonesCommand = new RelayCommand(Export, () => Zones.Count > 0);
            DeleteAllZonesCommand = new RelayCommand(DeleteAll, () => Zones.Count > 0);
            RenameZoneCommand = new RelayCommand<ZoneItemViewModel>(
                RenameZone,
                item => item?.ZoneData != null);

            Zones.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ZoneCount));
                CommandManager.InvalidateRequerySuggested();
            };

            _scene.ZoneAdded += OnZoneAdded;
            _scene.ZoneRemoved += OnZoneRemoved;
            _scene.ZoneUpdated += OnZoneUpdated;
            _scene.ZonesCleared += OnZonesCleared;
            _scene.ZoneSelected += OnZoneSelected;

            SyncFromScene();
        }

        private void DeleteSelected()
        {
            if (SelectedZone?.ZoneData == null)
                return;

            var zone = _scene.GetZones().FirstOrDefault(item => item.ZoneId == SelectedZone.ZoneData.ZoneId);
            if (zone == null)
                return;

            string message = string.IsNullOrWhiteSpace(SelectedZone.Name)
                ? "Are you sure you want to delete the selected zone?"
                : $"Are you sure you want to delete \"{SelectedZone.Name}\"?";
            if (!_deleteConfirmation.ConfirmDelete("Delete Zone", message, "Delete"))
                return;

            _scene.RemoveZone(zone);
        }

        private void DeleteAll()
        {
            if (Zones.Count == 0 || !_deleteConfirmation.ConfirmDelete(
                    "Delete All Zones",
                    "Are you sure you want to delete all zones? This action cannot be undone.",
                    "Delete"))
            {
                return;
            }

            _scene.ClearZones();
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
                .Where(zone => zone.ZoneData != null)
                .Select(zone => zone.ZoneData!.ToExportDto())
                .ToList();
            string json = JsonSerializer.Serialize(
                exportModel,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
        }

        public void AddOrUpdateFromZone(
            GreenAreaZone zone,
            double? minHeight = null,
            double? maxHeight = null)
        {
            string displayName = GetDisplayName(zone);
            ZoneDto dto = zone.ToZoneDto(
                minHeight ?? zone.MinimumHeight ?? 0,
                maxHeight ?? zone.MaximumHeight ?? 999);
            var existing = Zones.FirstOrDefault(z => z.ZoneData?.ZoneId == zone.ZoneId);
            if (existing == null)
            {
                Zones.Add(ZoneItemViewModel.FromZone(
                    zone,
                    dto,
                    GetSource(zone),
                    displayName,
                    FormatArea(zone, _unitSettings?.Current ?? UnitSettings.Default)));
                return;
            }

            existing.ApplyZone(
                zone,
                dto,
                GetSource(zone),
                displayName,
                FormatArea(zone, _unitSettings?.Current ?? UnitSettings.Default));
        }

        public void RefreshAreaDisplay(UnitSettings settings)
        {
            foreach (ZoneItemViewModel item in Zones)
            {
                if (item.ZoneReference != null)
                    item.SetAreaDisplay(FormatArea(item.ZoneReference, settings));
            }
        }

        private void SyncFromScene()
        {
            Zones.Clear();
            foreach (var zone in _scene.GetZones())
            {
                AddOrUpdateFromZone(zone);
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

        private void OnZoneUpdated(GreenAreaZone zone) =>
            AddOrUpdateFromZone(zone);

        private void OnZoneSelected(GreenAreaZone? zone)
        {
            _syncingSelection = true;
            try
            {
                SelectedZone = zone == null
                    ? null
                    : Zones.FirstOrDefault(
                        item => item.ZoneData?.ZoneId == zone.ZoneId);
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private void OnZonesCleared()
        {
            Zones.Clear();
            _fallbackNames.Clear();
            _fallbackCounters.Clear();
            SelectedZone = null;
        }

        private void RenameZone(ZoneItemViewModel item)
        {
            string newName = item.EditName.Trim();
            GreenAreaZone? zone = item.ZoneData == null
                ? null
                : _scene.GetZones().FirstOrDefault(
                    candidate => candidate.ZoneId == item.ZoneData.ZoneId);
            if (zone == null)
                return;

            if (string.IsNullOrWhiteSpace(newName))
            {
                item.EditName = item.Name;
                return;
            }

            if (string.Equals(zone.Name, newName, StringComparison.Ordinal))
                return;

            zone.Name = newName;
            _scene.UpdateZone(zone);
        }

        private string GetDisplayName(GreenAreaZone zone)
        {
            if (!string.IsNullOrWhiteSpace(zone.Name))
                return zone.Name.Trim();

            if (_fallbackNames.TryGetValue(zone.ZoneId, out string? fallback))
                return fallback;

            string typeName = zone.Type.ToString();
            int next = _fallbackCounters.TryGetValue(typeName, out int current)
                ? current + 1
                : 1;
            _fallbackCounters[typeName] = next;
            fallback = $"{typeName} Zone {next}";
            _fallbackNames[zone.ZoneId] = fallback;
            return fallback;
        }

        private static string GetSource(GreenAreaZone zone) =>
            zone.SourceDxfId.HasValue ||
            !string.IsNullOrWhiteSpace(zone.SourceDxfFilePath) ||
            !string.IsNullOrWhiteSpace(zone.SourceLayerName) ||
            string.Equals(zone.ImportMethod, "DXF", StringComparison.OrdinalIgnoreCase)
                ? "DXF"
                : "Manual";

        private static string FormatArea(
            GreenAreaZone zone,
            UnitSettings settings) =>
            UnitSystem.FormatArea(
                zone.Area,
                settings,
                CultureInfo.CurrentCulture);
    }

    public class ZoneItemViewModel : ViewModelBase
    {
        private bool _isVisible = true;
        private string _name = "Zone";
        private string _editName = "Zone";
        private string _zoneType = "Allowed";
        private string _source = "Manual";
        private string _areaDisplay = string.Empty;
        private string _nameBeforeEdit = "Zone";
        private bool _isEditingName;
        private double _minHeight;
        private double _maxHeight = 999;

        public ZoneDto? ZoneData { get; private set; }
        public GreenAreaZone? ZoneReference { get; private set; }

        public string Name
        {
            get => _name;
            private set { _name = value; OnPropertyChanged(); }
        }

        public string EditName
        {
            get => _editName;
            set { _editName = value; OnPropertyChanged(); }
        }

        public string ZoneType
        {
            get => _zoneType;
            set { _zoneType = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            private set { _source = value; OnPropertyChanged(); }
        }

        public string AreaDisplay
        {
            get => _areaDisplay;
            private set { _areaDisplay = value; OnPropertyChanged(); }
        }

        public bool IsEditingName
        {
            get => _isEditingName;
            private set { _isEditingName = value; OnPropertyChanged(); }
        }

        public bool HasNameEditChanged =>
            !string.Equals(
                EditName,
                _nameBeforeEdit,
                StringComparison.Ordinal);

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

        public static ZoneItemViewModel FromZone(
            GreenAreaZone zone,
            ZoneDto dto,
            string source,
            string displayName,
            string areaDisplay)
        {
            var vm = new ZoneItemViewModel();
            vm.ApplyZone(zone, dto, source, displayName, areaDisplay);
            return vm;
        }

        public void ApplyZone(
            GreenAreaZone zone,
            ZoneDto dto,
            string source,
            string displayName,
            string areaDisplay)
        {
            ZoneReference = zone;
            ZoneData = dto;
            Name = displayName;
            EditName = displayName;
            ZoneType = dto.ZoneType;
            Source = source;
            AreaDisplay = areaDisplay;
            MinHeight = dto.MinHeight;
            MaxHeight = dto.MaxHeight;
        }

        public void BeginNameEdit()
        {
            _nameBeforeEdit = Name;
            EditName = Name;
            IsEditingName = true;
        }

        public void CompleteNameEdit()
        {
            IsEditingName = false;
        }

        public void CancelNameEdit()
        {
            EditName = _nameBeforeEdit;
            IsEditingName = false;
        }

        public void SetAreaDisplay(string areaDisplay)
        {
            AreaDisplay = areaDisplay;
        }
    }
}
