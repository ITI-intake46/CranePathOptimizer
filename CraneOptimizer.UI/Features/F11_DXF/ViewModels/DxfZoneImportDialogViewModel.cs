using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfZoneImportLayerOption : ViewModelBase
    {
        private string _assignment = "";

        public DxfZoneImportLayerOption(DxfImportLayerSummary summary)
        {
            Summary = summary;
            var brush = (Brush)new BrushConverter().ConvertFromString(ColorHex)!;
            brush.Freeze();
            ColorBrush = brush;
        }

        public DxfImportLayerSummary Summary { get; }
        public string Name => Summary.Name;
        public string ColorHex => Summary.ColorHex;
        public Brush ColorBrush { get; }
        public int TotalEntityCount => Summary.TotalEntityCount;
        public int ValidClosedCount => Summary.ValidClosedCount;
        public int InvalidCount => Summary.InvalidCount;
        public bool CanDraw => ValidClosedCount > 0;
        public string CountsText => $"{ValidClosedCount} valid / {InvalidCount} invalid / {TotalEntityCount} entities";

        public string Assignment
        {
            get => _assignment;
            set
            {
                if (_assignment == value) return;
                _assignment = value;
                OnPropertyChanged();
            }
        }
    }

    public sealed class DxfZoneImportDialogViewModel : ViewModelBase
    {
        private readonly Func<string, IReadOnlyList<DxfZoneCandidate>> _candidateProvider;
        private readonly Func<IReadOnlyList<DxfZoneCandidate>, string, IReadOnlyList<Guid>> _importCallback;
        private readonly Action<IReadOnlyList<Guid>> _removeCallback;
        private readonly Dictionary<string, IReadOnlyList<Guid>> _importedZoneIdsByLayer = new(StringComparer.OrdinalIgnoreCase);
        private DxfZoneImportLayerOption? _selectedLayer;
        private string _statusMessage = "Choose one zone type for each DXF layer.";

        public DxfZoneImportDialogViewModel(
            IReadOnlyList<DxfImportLayerSummary> layers,
            Func<string, IReadOnlyList<DxfZoneCandidate>> candidateProvider,
            Func<IReadOnlyList<DxfZoneCandidate>, string, IReadOnlyList<Guid>> importCallback,
            Action<IReadOnlyList<Guid>> removeCallback)
        {
            _candidateProvider = candidateProvider;
            _importCallback = importCallback;
            _removeCallback = removeCallback;

            foreach (var layer in layers)
                Layers.Add(new DxfZoneImportLayerOption(layer));

            DrawAllowedCommand = new RelayCommand<DxfZoneImportLayerOption>(layer => ImportLayer(layer, "Allowed"), CanImportLayer);
            DrawForbiddenCommand = new RelayCommand<DxfZoneImportLayerOption>(layer => ImportLayer(layer, "Forbidden"), CanImportLayer);
            RemoveImportedZonesCommand = new RelayCommand(RemoveSelectedLayerZones, () => SelectedLayer != null && _importedZoneIdsByLayer.ContainsKey(SelectedLayer.Name));

            SelectedLayer = Layers.FirstOrDefault(layer => layer.ValidClosedCount > 0) ?? Layers.FirstOrDefault();
        }

        public ObservableCollection<DxfZoneImportLayerOption> Layers { get; } = new();
        public ICommand DrawAllowedCommand { get; }
        public ICommand DrawForbiddenCommand { get; }
        public ICommand RemoveImportedZonesCommand { get; }

        public DxfZoneImportLayerOption? SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (_selectedLayer == value) return;
                _selectedLayer = value;
                OnPropertyChanged();
                UpdateLayerStatus();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private void UpdateLayerStatus()
        {
            CommandManager.InvalidateRequerySuggested();

            if (SelectedLayer == null)
            {
                StatusMessage = "No layer selected.";
                return;
            }

            StatusMessage = SelectedLayer.ValidClosedCount == 0
                ? "No closed polyline or line-loop candidates found on this layer."
                : $"Layer {SelectedLayer.Name}: choose Allowed or Forbidden. Only one type can be assigned.";
        }

        private static bool CanImportLayer(DxfZoneImportLayerOption layer) =>
            layer?.CanDraw == true;

        private void ImportLayer(DxfZoneImportLayerOption layer, string zoneType)
        {
            if (layer == null || !layer.CanDraw)
                return;

            if (_importedZoneIdsByLayer.TryGetValue(layer.Name, out var previousIds))
                _removeCallback(previousIds);

            var validCandidates = _candidateProvider(layer.Name)
                .Where(candidate => candidate.IsValid)
                .ToList();

            if (validCandidates.Count == 0)
            {
                StatusMessage = $"Layer {layer.Name} has no valid closed shapes to draw.";
                return;
            }

            var imported = _importCallback(validCandidates, zoneType);
            _importedZoneIdsByLayer[layer.Name] = imported;
            layer.Assignment = zoneType;
            SelectedLayer = layer;

            StatusMessage = $"Layer {layer.Name} is now {zoneType.ToLowerInvariant()} ({imported.Count} zones drawn).";
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveSelectedLayerZones()
        {
            if (SelectedLayer == null || !_importedZoneIdsByLayer.TryGetValue(SelectedLayer.Name, out var zoneIds))
                return;

            _removeCallback(zoneIds);
            _importedZoneIdsByLayer.Remove(SelectedLayer.Name);
            SelectedLayer.Assignment = "";
            StatusMessage = $"Removed zones drawn from layer {SelectedLayer.Name}.";
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
