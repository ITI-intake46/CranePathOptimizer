using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels;

public sealed class DxfZoneImportLayerOption : ViewModelBase
{
    private DxfLayerZoneAssignment _assignment;
    private int _importedZoneCount;
    private bool _isBusy;

    public DxfZoneImportLayerOption(DxfImportLayerSummary summary, DxfLayerZoneState? state)
    {
        Summary = summary;
        var brush = (Brush)new BrushConverter().ConvertFromString(ColorHex)!;
        brush.Freeze();
        ColorBrush = brush;
        ApplyState(state ?? new DxfLayerZoneState(
            DxfLayerZoneAssignment.None,
            0,
            Array.Empty<Guid>()));
    }

    public DxfImportLayerSummary Summary { get; }
    public string Name => Summary.Name;
    public string ColorHex => Summary.ColorHex;
    public Brush ColorBrush { get; }
    public int TotalEntityCount => Summary.TotalEntityCount;
    public int ValidClosedCount => Summary.ValidClosedCount;
    public int InvalidCount => Summary.InvalidCount;
    public bool CanDraw => ValidClosedCount > 0;
    public string CountsText =>
        $"{ValidClosedCount} valid / {InvalidCount} invalid / {TotalEntityCount} entities";

    public DxfLayerZoneAssignment Assignment
    {
        get => _assignment;
        private set
        {
            if (_assignment == value)
                return;
            _assignment = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssignmentText));
            OnPropertyChanged(nameof(IsInconsistent));
        }
    }

    public int ImportedZoneCount
    {
        get => _importedZoneCount;
        private set
        {
            if (_importedZoneCount == value)
                return;
            _importedZoneCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImportedZones));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
                return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BusyText));
        }
    }

    public bool HasImportedZones => ImportedZoneCount > 0;
    public bool IsInconsistent => Assignment == DxfLayerZoneAssignment.Inconsistent;
    public string BusyText => IsBusy ? "Working..." : AssignmentText;
    public string AssignmentText => Assignment switch
    {
        DxfLayerZoneAssignment.Allowed => $"{ImportedZoneCount} allowed zones",
        DxfLayerZoneAssignment.Forbidden => $"{ImportedZoneCount} forbidden zones",
        DxfLayerZoneAssignment.Inconsistent => $"{ImportedZoneCount} mixed zones",
        _ => "Unassigned"
    };

    public void ApplyState(DxfLayerZoneState state)
    {
        Assignment = state.Assignment;
        ImportedZoneCount = state.ZoneCount;
        OnPropertyChanged(nameof(BusyText));
    }
}

public sealed class DxfZoneImportDialogViewModel : ViewModelBase, IDisposable
{
    private readonly Func<
        string,
        DxfLayerZoneAssignment,
        DxfLayerZoneAssignment,
        CancellationToken,
        Task<DxfLayerZoneOperationResult>> _operation;
    private readonly Func<
        IReadOnlyList<string>,
        CancellationToken,
        Task<DxfLayerZoneOperationResult>> _clearAllOperation;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private DxfZoneImportLayerOption? _selectedLayer;
    private string _statusMessage = "Choose one zone type for each DXF layer.";
    private bool _isBulkOperationBusy;

    public DxfZoneImportDialogViewModel(
        IReadOnlyList<DxfImportLayerSummary> layers,
        IReadOnlyDictionary<string, DxfLayerZoneState> initialStates,
        Func<
            string,
            DxfLayerZoneAssignment,
            DxfLayerZoneAssignment,
            CancellationToken,
            Task<DxfLayerZoneOperationResult>> operation,
        Func<
            IReadOnlyList<string>,
            CancellationToken,
            Task<DxfLayerZoneOperationResult>> clearAllOperation)
    {
        _operation = operation;
        _clearAllOperation = clearAllOperation;
        foreach (DxfImportLayerSummary layer in layers)
        {
            initialStates.TryGetValue(layer.Name, out DxfLayerZoneState? state);
            Layers.Add(new DxfZoneImportLayerOption(layer, state));
        }

        DrawAllowedCommand = new AsyncRelayCommand<DxfZoneImportLayerOption>(
            layer => ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed),
            CanAssignLayer);
        DrawForbiddenCommand = new AsyncRelayCommand<DxfZoneImportLayerOption>(
            layer => ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Forbidden),
            CanAssignLayer);
        RemoveImportedZonesCommand = new AsyncRelayCommand(
            RemoveAllLayerZonesAsync,
            CanRemoveAllLayerZones);

        SelectedLayer = Layers.FirstOrDefault(layer => layer.CanDraw || layer.HasImportedZones)
            ?? Layers.FirstOrDefault();
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
            if (_selectedLayer == value)
                return;
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
            if (_statusMessage == value)
                return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private bool CanAssignLayer(DxfZoneImportLayerOption? layer) =>
        layer != null &&
        !_isBulkOperationBusy &&
        !layer.IsBusy &&
        !layer.IsInconsistent &&
        (layer.CanDraw || layer.HasImportedZones);

    private bool CanRemoveAllLayerZones() =>
        !_isBulkOperationBusy &&
        Layers.All(layer => !layer.IsBusy) &&
        Layers.Any(layer => layer.HasImportedZones);

    internal async Task ToggleAssignmentAsync(
        DxfZoneImportLayerOption? layer,
        DxfLayerZoneAssignment requestedAssignment)
    {
        if (layer == null || !CanAssignLayer(layer))
            return;

        DxfLayerZoneAssignment target = layer.Assignment == requestedAssignment
            ? DxfLayerZoneAssignment.None
            : requestedAssignment;
        await ExecuteOperationAsync(layer, target);
    }

    internal async Task RemoveAllLayerZonesAsync()
    {
        DxfZoneImportLayerOption[] importedLayers = Layers
            .Where(layer => layer.HasImportedZones)
            .ToArray();
        if (importedLayers.Length == 0 || !CanRemoveAllLayerZones())
            return;

        _isBulkOperationBusy = true;
        foreach (DxfZoneImportLayerOption layer in importedLayers)
            layer.IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        StatusMessage = $"Removing generated zones from {importedLayers.Length} DXF layers...";

        try
        {
            DxfLayerZoneOperationResult result = await _clearAllOperation(
                importedLayers.Select(layer => layer.Name).ToArray(),
                _lifetimeCancellation.Token);
            foreach (DxfZoneImportLayerOption layer in importedLayers)
            {
                layer.ApplyState(new DxfLayerZoneState(
                    DxfLayerZoneAssignment.None,
                    0,
                    Array.Empty<Guid>()));
            }
            StatusMessage = result.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Remove layer zones operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not remove layer zones: {ex.Message}";
        }
        finally
        {
            foreach (DxfZoneImportLayerOption layer in importedLayers)
                layer.IsBusy = false;
            _isBulkOperationBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ExecuteOperationAsync(
        DxfZoneImportLayerOption layer,
        DxfLayerZoneAssignment target)
    {
        layer.IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        StatusMessage = target == DxfLayerZoneAssignment.None
            ? $"Removing generated zones from layer {layer.Name}..."
            : $"Preparing {target.ToString().ToLowerInvariant()} zones for layer {layer.Name}...";

        try
        {
            DxfLayerZoneOperationResult result = await _operation(
                layer.Name,
                layer.Assignment,
                target,
                _lifetimeCancellation.Token);
            layer.ApplyState(new DxfLayerZoneState(
                result.Assignment,
                result.ZoneCount,
                Array.Empty<Guid>()));
            SelectedLayer = layer;
            StatusMessage = result.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Layer operation cancelled for {layer.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update layer {layer.Name}: {ex.Message}";
        }
        finally
        {
            layer.IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void SynchronizeLayerState(string layerName, DxfLayerZoneState state)
    {
        DxfZoneImportLayerOption? layer = Layers.FirstOrDefault(option =>
            string.Equals(option.Name, layerName, StringComparison.OrdinalIgnoreCase));
        if (layer == null)
            return;

        layer.ApplyState(state);
        if (ReferenceEquals(layer, SelectedLayer))
            UpdateLayerStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateLayerStatus()
    {
        CommandManager.InvalidateRequerySuggested();
        if (SelectedLayer == null)
        {
            StatusMessage = "No layer selected.";
            return;
        }

        StatusMessage = SelectedLayer.Assignment switch
        {
            DxfLayerZoneAssignment.Inconsistent =>
                $"Layer {SelectedLayer.Name} contains mixed generated zone types. Use Remove Layer Zones to recover.",
            DxfLayerZoneAssignment.Allowed =>
                $"Layer {SelectedLayer.Name} is allowed. Click Allowed again to return it to Default.",
            DxfLayerZoneAssignment.Forbidden =>
                $"Layer {SelectedLayer.Name} is forbidden. Click Forbidden again to return it to Default.",
            _ when SelectedLayer.ValidClosedCount == 0 =>
                "No closed polyline or line-loop candidates found on this layer.",
            _ => $"Layer {SelectedLayer.Name} is unassigned."
        };
    }

    public void Dispose()
    {
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}
