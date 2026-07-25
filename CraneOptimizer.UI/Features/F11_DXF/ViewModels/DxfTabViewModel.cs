using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Interaction;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfTabViewModel : ViewModelBase
    {
        private readonly IDxfZoneImportService _zoneImportService;
        private readonly IDxfLayerZoneAssignmentService _layerAssignmentService;
        private readonly ISceneService _scene;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportSelectionService _selection;
        private readonly IProjectHistoryService _history;
        private HelixViewport3D? _viewport;
        private DxfSourceSession? _activeDxf;
        private DxfAlignmentDialog? _openAlignmentDialog;
        private DxfSourceSession? _openAlignmentSource;
        private DxfSelectionMode? _selectionMode;
        private DxfZoneImportDialogViewModel? _openZoneDialogViewModel;
        private DxfSourceSession? _openZoneDialogSource;
        private string _statusMessage = "No DXF loaded";
        private bool _useDxfColors;
        private bool _isSelectionPanelOpen;
        private bool _isDxfSelectionMode;

        public DxfTabViewModel(
            IDxfZoneImportService zoneImportService,
            ISceneService scene,
            IViewportInteractionService interaction,
            IViewportSelectionService selection,
            IProjectHistoryService history,
            IDxfLayerZoneAssignmentService layerAssignmentService)
        {
            _zoneImportService = zoneImportService;
            _layerAssignmentService = layerAssignmentService;
            _scene = scene;
            _interaction = interaction;
            _selection = selection;
            _history = history;

            UploadDxfCommand = new AsyncRelayCommand(ExecuteUploadDxfAsync);
            AddAnotherDxfCommand = new AsyncRelayCommand(ExecuteUploadDxfAsync);
            AlignCommand = new RelayCommand(ExecuteAlign, () => HasAnyDxf);
            DrawZonesByLayerCommand = new AsyncRelayCommand(ExecuteDrawZonesByLayerAsync, () => HasAnyDxf);
            SelectCommand = new RelayCommand(ToggleSelectionMode, () => HasAnyDxf);
            SelectAllCommand = new RelayCommand(SelectAll, () => HasAnyDxf);
            DeleteCommand = new RelayCommand(DeleteSelected, () => SelectedDxfCount > 0);
            ToggleSourceSelectionCommand = new RelayCommand<DxfSourceSession>(ToggleSourceSelection);
            ActivateSourceCommand = new RelayCommand<DxfSourceSession>(SetActiveDxf);

            _scene.DxfEntitiesSelectionChanged += OnDxfSelectionChanged;
        }

        public ObservableCollection<DxfSourceSession> LoadedDxfs { get; } = new();
        public IReadOnlyList<DxfSourceSession> SelectedDxfs => LoadedDxfs.Where(source => source.IsSelected).ToList();
        public int LoadedDxfCount => LoadedDxfs.Count;
        public int SelectedDxfCount => LoadedDxfs.Count(source => source.IsSelected);
        public bool HasAnyDxf => LoadedDxfCount > 0;
        public bool HasMultipleDxfs => LoadedDxfCount > 1;
        public bool HasDxfLoaded => HasAnyDxf;
        public string SelectionSummary => $"{LoadedDxfCount} loaded \u00b7 {SelectedDxfCount} selected";
        public string LoadedFileName => ActiveDxf?.DisplayName ?? string.Empty;
        public string? LoadedFilePath => ActiveDxf?.FilePath;
        public double[] SavedTransformArray => ActiveDxf?.SavedTransformArray ?? NormalizeTransformArray(null);
        public DxfUploader? CurrentUploader => ActiveDxf?.Uploader;
        public Matrix3D? PendingTransform => ActiveDxf == null
            ? null
            : MatrixFromTransformArray(ActiveDxf.SavedTransformArray);

        public DxfSourceSession? ActiveDxf
        {
            get => _activeDxf;
            private set
            {
                if (_activeDxf == value) return;
                if (_activeDxf != null) _activeDxf.IsActive = false;
                _activeDxf = value;
                if (_activeDxf != null) _activeDxf.IsActive = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoadedFileName));
                OnPropertyChanged(nameof(LoadedFilePath));
                OnPropertyChanged(nameof(SavedTransformArray));
                OnPropertyChanged(nameof(CurrentUploader));
                OnPropertyChanged(nameof(PendingTransform));
                UpdateStatusSummary();
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

        public bool UseDxfColors
        {
            get => _useDxfColors;
            set
            {
                if (_useDxfColors == value) return;
                _useDxfColors = value;
                foreach (var source in LoadedDxfs)
                    source.UseDxfColors = value;
                OnPropertyChanged();
                RaiseStateChanged();
            }
        }

        public bool IsSelectionPanelOpen
        {
            get => _isSelectionPanelOpen;
            set
            {
                if (_isSelectionPanelOpen == value) return;
                _isSelectionPanelOpen = value;
                OnPropertyChanged();
            }
        }

        public bool IsDxfSelectionMode
        {
            get => _isDxfSelectionMode;
            private set
            {
                if (_isDxfSelectionMode == value) return;
                _isDxfSelectionMode = value;
                OnPropertyChanged();
            }
        }

        public ICommand UploadDxfCommand { get; }
        public ICommand AddAnotherDxfCommand { get; }
        public ICommand AlignCommand { get; }
        public ICommand DrawZonesByLayerCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ToggleSourceSelectionCommand { get; }
        public ICommand ActivateSourceCommand { get; }

        public event Action<DxfUploader>? DxfLoaded;
        public event Action<DxfUploader>? DxfRemoved;
        public event Action? DxfStateChanged;

        public async Task InitializeAsync(HelixViewport3D viewport)
        {
            _viewport = viewport;
            foreach (var source in LoadedDxfs.Where(source => source.Uploader == null).ToList())
                await AttachUploaderAsync(source, CancellationToken.None);
        }

        public IReadOnlyList<DxfSourceState> GetSourceStates() =>
            LoadedDxfs.OrderBy(source => source.LoadedOrder).Select(source => source.ToState()).ToList();

        public void LoadDxfFromProject(string filePath, double[]? transformArray)
        {
            var state = new DxfSourceState(
                Guid.NewGuid(),
                filePath,
                NormalizeTransformArray(transformArray),
                LoadedDxfs.Count,
                true,
                false,
                transformArray?.Length >= 4,
                UseDxfColors);
            RestoreSource(new DxfSourceSession(state), LoadedDxfs.Count, showMissingWarning: true);
            if (_viewport != null)
                AttachUploaderSafely(LoadedDxfs[^1], showMissingWarning: true);
        }

        public void LoadDxfsFromProject(IEnumerable<DxfSourceState> states)
        {
            ClearAllSources();
            var ordered = states
                .Select(state => state with { TransformValues = NormalizeTransformArray(state.TransformValues) })
                .OrderBy(state => state.LoadedOrder)
                .ToList();
            var activeId = ordered.FirstOrDefault(state => state.IsActive)?.Id;
            var selectedIds = ordered.Where(state => state.IsSelected).Select(state => state.Id).ToHashSet();
            foreach (var state in ordered)
                RestoreSource(new DxfSourceSession(state), LoadedDxfs.Count, showMissingWarning: true);

            ActiveDxf = LoadedDxfs.FirstOrDefault(source => source.Id == activeId) ?? LoadedDxfs.FirstOrDefault();
            _scene.SetSelectedDxfEntities(LoadedDxfs
                .Where(source => selectedIds.Contains(source.Id))
                .Select(source => source.SelectionTarget)
                .ToList());
            if (_viewport != null)
            {
                foreach (DxfSourceSession source in LoadedDxfs)
                    AttachUploaderSafely(source, showMissingWarning: true);
            }
        }

        private async Task ExecuteUploadDxfAsync()
        {
            var picker = new OpenFileDialog
            {
                Title = HasAnyDxf ? "Add DXF File" : "Select DXF File",
                Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*"
            };

            if (picker.ShowDialog() != true)
                return;

            if (LoadedDxfs.Any(source =>
                string.Equals(source.FilePath, picker.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                var answer = MessageBox.Show(
                    $"{Path.GetFileName(picker.FileName)} is already loaded.\n\n" +
                    "Load another independent instance?",
                    "Duplicate DXF",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                    return;
            }

            var state = new DxfSourceState(
                Guid.NewGuid(),
                picker.FileName,
                NormalizeTransformArray(null),
                LoadedDxfs.Count,
                true,
                false,
                false,
                UseDxfColors);
            var source = new DxfSourceSession(state);
            var insertIndex = LoadedDxfs.Count;
            RestoreSource(source, insertIndex, showMissingWarning: false);

            if (_viewport != null &&
                !await AttachUploaderAsync(source, CancellationToken.None))
            {
                RemoveSource(source);
                return;
            }

            _history.RecordExecuted(new DelegateProjectHistoryAction(
                $"add DXF {source.DisplayName}",
                () => RestoreSource(source, insertIndex, showMissingWarning: true),
                () => RemoveSource(source)));

            OpenAlignment(source);
        }

        private void ExecuteAlign()
        {
            var source = ResolveSingleSource("Select DXF to Align", "Align", preferSingleSelection: false);
            if (source != null)
                OpenAlignment(source);
        }

        private void OpenAlignment(DxfSourceSession source)
        {
            if (_openAlignmentDialog != null)
            {
                if (ReferenceEquals(_openAlignmentSource, source))
                    _openAlignmentDialog.Activate();
                return;
            }

            if (source.Uploader?.IsLoaded != true)
            {
                StatusMessage = $"DXF is unavailable: {source.DisplayName}";
                return;
            }

            SetActiveDxf(source);
            try
            {
                var dialog = new DxfAlignmentDialog(source.Uploader)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                if (source.HasSavedTransform)
                    dialog.RestoreTransform(source.SavedTransformArray);

                dialog.Closed += (_, _) =>
                {
                    source.SavedTransformArray = dialog.GetTransformArray();
                    source.HasSavedTransform = true;
                    if (source.Uploader != null)
                        source.Uploader.AlignmentTransform = dialog.GetResultTransform();
                    _openAlignmentDialog = null;
                    _openAlignmentSource = null;
                    StatusMessage = $"Aligned: {source.DisplayName}";
                    RaiseStateChanged();
                };

                _openAlignmentSource = source;
                _openAlignmentDialog = dialog;
                dialog.Show();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Alignment error: {ex.Message}";
            }
        }

        private async Task ExecuteDrawZonesByLayerAsync()
        {
            var source = ResolveSingleSource("Select DXF for Layer Import", "Continue", preferSingleSelection: true);
            if (source?.Uploader?.LoadedDocument == null)
            {
                if (source != null)
                    StatusMessage = $"DXF is unavailable: {source.DisplayName}";
                return;
            }

            SetActiveDxf(source);
            var transform = MatrixFromTransformArray(source.SavedTransformArray);
            netDxf.DxfDocument document = source.Uploader.LoadedDocument;
            IReadOnlyList<DxfImportLayerSummary> layers;
            IReadOnlyDictionary<string, DxfLayerZoneState> initialStates;
            try
            {
                layers = await Task.Run(() =>
                {
                    lock (document)
                        return _zoneImportService.GetLayerSummaries(
                            document,
                            transform,
                            CancellationToken.None);
                });
                initialStates = await _layerAssignmentService.GetAssignmentStatesAsync(
                    CreateLayerZoneSource(source),
                    layers.Select(layer => layer.Name),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not prepare DXF layers for {source.DisplayName}: {ex.Message}";
                return;
            }

            if (layers.Count == 0)
            {
                StatusMessage = $"No DXF layers found in {source.DisplayName}.";
                return;
            }

            var viewModel = new DxfZoneImportDialogViewModel(
                layers,
                initialStates,
                (layerName, current, target, cancellationToken) =>
                    ApplyLayerAssignmentAsync(
                        source,
                        document,
                        transform,
                        layerName,
                        current,
                        target,
                        cancellationToken),
                (layerNames, cancellationToken) =>
                    ClearAllLayerAssignmentsAsync(
                        source,
                        layerNames,
                        cancellationToken));

            var dialog = new DxfZoneImportDialog
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = viewModel,
                Title = $"DXF Layers - {source.DisplayName}"
            };
            _openZoneDialogViewModel = viewModel;
            _openZoneDialogSource = source;
            dialog.Closed += (_, _) =>
            {
                viewModel.Dispose();
                _openZoneDialogViewModel = null;
                _openZoneDialogSource = null;
            };
            dialog.Show();
        }

        private DxfSourceSession? ResolveSingleSource(
            string title,
            string primaryButtonText,
            bool preferSingleSelection)
        {
            if (LoadedDxfs.Count == 0)
                return null;
            if (LoadedDxfs.Count == 1)
                return LoadedDxfs[0];
            if (preferSingleSelection && SelectedDxfCount == 1)
                return SelectedDxfs[0];

            var viewModel = new DxfSourcePickerDialogViewModel(
                title,
                primaryButtonText,
                LoadedDxfs.ToList(),
                ActiveDxf);
            var dialog = new DxfSourcePickerDialog
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = viewModel
            };
            return dialog.ShowDialog() == true ? viewModel.SelectedDxf : null;
        }

        internal async Task<DxfLayerZoneOperationResult> ApplyLayerAssignmentAsync(
            DxfSourceSession source,
            netDxf.DxfDocument document,
            Matrix3D transform,
            string layerName,
            DxfLayerZoneAssignment currentAssignment,
            DxfLayerZoneAssignment targetAssignment,
            CancellationToken cancellationToken)
        {
            DxfLayerZoneSource sourceIdentity = CreateLayerZoneSource(source);
            DxfLayerZoneTransaction transaction;
            if (targetAssignment == DxfLayerZoneAssignment.None)
            {
                transaction = await _layerAssignmentService.ClearAsync(
                    sourceIdentity,
                    layerName,
                    cancellationToken);
            }
            else
            {
                ZoneType zoneType = targetAssignment == DxfLayerZoneAssignment.Allowed
                    ? ZoneType.Allowed
                    : ZoneType.Forbidden;
                IReadOnlyList<DxfZoneCandidate> CandidateProvider(CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    lock (document)
                        return _zoneImportService.GetCandidates(document, layerName, transform, token);
                }

                transaction = currentAssignment == DxfLayerZoneAssignment.None
                    ? await _layerAssignmentService.AssignAsync(
                        sourceIdentity,
                        layerName,
                        zoneType,
                        CandidateProvider,
                        cancellationToken)
                    : await _layerAssignmentService.ReplaceAsync(
                        sourceIdentity,
                        layerName,
                        zoneType,
                        CandidateProvider,
                        cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (targetAssignment != DxfLayerZoneAssignment.None && transaction.NextZones.Count == 0)
            {
                return new DxfLayerZoneOperationResult(
                    transaction.PreviousAssignment,
                    transaction.PreviousZones.Count,
                    0,
                    0,
                    $"Layer {layerName} has no valid closed shapes to draw.");
            }

            if (!transaction.HasChanges)
            {
                SynchronizeOpenDialog(
                    source,
                    layerName,
                    new DxfLayerZoneState(
                        DxfLayerZoneAssignment.None,
                        0,
                        Array.Empty<Guid>()));
                return new DxfLayerZoneOperationResult(
                    DxfLayerZoneAssignment.None,
                    0,
                    0,
                    0,
                    $"No generated zones were found for layer {layerName}.");
            }

            _history.Execute(new DelegateProjectHistoryAction(
                transaction.Description,
                () => CommitLayerZoneTransaction(source, transaction, forward: true),
                () => CommitLayerZoneTransaction(source, transaction, forward: false)));

            string message = targetAssignment == DxfLayerZoneAssignment.None
                ? $"Removed {transaction.PreviousZones.Count} zones from layer {layerName}."
                : transaction.PreviousZones.Count == 0
                    ? $"Created {transaction.NextZones.Count} {targetAssignment.ToString().ToLowerInvariant()} zones from layer {layerName}."
                    : $"Replaced {transaction.PreviousZones.Count} zones with {transaction.NextZones.Count} {targetAssignment.ToString().ToLowerInvariant()} zones on layer {layerName}.";
            return new DxfLayerZoneOperationResult(
                transaction.NewAssignment,
                transaction.NextZones.Count,
                transaction.NextZones.Count,
                transaction.PreviousZones.Count,
                message);
        }

        internal async Task<DxfLayerZoneOperationResult> ClearAllLayerAssignmentsAsync(
            DxfSourceSession source,
            IReadOnlyList<string> layerNames,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<DxfLayerZoneTransaction> prepared =
                await _layerAssignmentService.ClearAllAsync(
                    CreateLayerZoneSource(source),
                    layerNames,
                    cancellationToken);
            DxfLayerZoneTransaction[] transactions = prepared
                .Where(transaction => transaction.HasChanges)
                .ToArray();
            cancellationToken.ThrowIfCancellationRequested();

            if (transactions.Length == 0)
            {
                foreach (string layerName in layerNames)
                {
                    SynchronizeOpenDialog(
                        source,
                        layerName,
                        new DxfLayerZoneState(
                            DxfLayerZoneAssignment.None,
                            0,
                            Array.Empty<Guid>()));
                }

                return new DxfLayerZoneOperationResult(
                    DxfLayerZoneAssignment.None,
                    0,
                    0,
                    0,
                    "No generated DXF layer zones were found.");
            }

            int removedCount = transactions.Sum(transaction => transaction.PreviousZones.Count);
            _history.Execute(new DelegateProjectHistoryAction(
                $"remove {removedCount} DXF zones from {transactions.Length} layers",
                () => CommitLayerZoneTransactions(source, transactions, forward: true),
                () => CommitLayerZoneTransactions(source, transactions, forward: false)));

            return new DxfLayerZoneOperationResult(
                DxfLayerZoneAssignment.None,
                0,
                0,
                removedCount,
                $"Removed {removedCount} zones from {transactions.Length} DXF layers.");
        }

        private void CommitLayerZoneTransaction(
            DxfSourceSession source,
            DxfLayerZoneTransaction transaction,
            bool forward) =>
            CommitLayerZoneTransactions(source, new[] { transaction }, forward);

        private void CommitLayerZoneTransactions(
            DxfSourceSession source,
            IReadOnlyList<DxfLayerZoneTransaction> transactions,
            bool forward)
        {
            GreenAreaZone[] remove = transactions
                .SelectMany(transaction => forward
                    ? transaction.PreviousZones
                    : transaction.NextZones)
                .ToArray();
            GreenAreaZone[] add = transactions
                .SelectMany(transaction => forward
                    ? transaction.NextZones
                    : transaction.PreviousZones)
                .ToArray();
            try
            {
                _scene.ApplyZoneChanges(
                    remove.Select(zone => zone.ZoneId).ToArray(),
                    add);
            }
            catch
            {
                _scene.ApplyZoneChanges(
                    add.Select(zone => zone.ZoneId).ToArray(),
                    remove);
                throw;
            }

            foreach (DxfLayerZoneTransaction transaction in transactions)
            {
                IReadOnlyList<GreenAreaZone> layerZones = forward
                    ? transaction.NextZones
                    : transaction.PreviousZones;
                DxfLayerZoneAssignment assignment = forward
                    ? transaction.NewAssignment
                    : transaction.PreviousAssignment;
                SynchronizeOpenDialog(
                    source,
                    transaction.Key.LayerName,
                    new DxfLayerZoneState(
                        assignment,
                        layerZones.Count,
                        layerZones.Select(zone => zone.ZoneId).ToArray()));
            }
            RaiseStateChanged();
        }

        private DxfLayerZoneSource CreateLayerZoneSource(DxfSourceSession source)
        {
            bool hasUniqueDisplayName = LoadedDxfs.Count(item =>
                string.Equals(item.DisplayName, source.DisplayName, StringComparison.OrdinalIgnoreCase)) == 1;
            return new DxfLayerZoneSource(
                source.Id,
                source.FilePath,
                source.DisplayName,
                hasUniqueDisplayName);
        }

        private void SynchronizeOpenDialog(
            DxfSourceSession source,
            string layerName,
            DxfLayerZoneState state)
        {
            if (_openZoneDialogViewModel == null || !ReferenceEquals(_openZoneDialogSource, source))
                return;
            _openZoneDialogViewModel.SynchronizeLayerState(layerName, state);
        }

        private void ToggleSelectionMode()
        {
            if (IsDxfSelectionMode)
            {
                IsSelectionPanelOpen = false;
                ExitSelectionMode();
                return;
            }

            IsSelectionPanelOpen = true;
            _selectionMode = new DxfSelectionMode(_selection, ExitSelectionMode);
            _interaction.PushMode(_selectionMode);
            IsDxfSelectionMode = true;
            StatusMessage = "Select DXF overlays in the viewport. Press Esc to finish.";
        }

        private void ExitSelectionMode()
        {
            if (!IsDxfSelectionMode)
                return;

            if (ReferenceEquals(_interaction.CurrentMode, _selectionMode))
                _interaction.PopMode();
            _selectionMode = null;
            IsDxfSelectionMode = false;
            IsSelectionPanelOpen = false;
            UpdateStatusSummary();
        }

        private void ToggleSourceSelection(DxfSourceSession source)
        {
            if (source == null) return;
            _selection.SelectDxf(source.SelectionTarget, true);
            SetActiveDxf(source);
        }

        private void SelectAll()
        {
            if (LoadedDxfs.Count == 0)
                return;

            _selection.SetDxfs(LoadedDxfs.Select(source => source.SelectionTarget));
        }

        private void DeleteSelected()
        {
            var selected = SelectedDxfs.OrderBy(source => source.LoadedOrder).ToList();
            if (selected.Count == 0)
                return;

            var names = string.Join("\n", selected.Take(6).Select(source => $"\u2022 {source.DisplayName}"));
            if (selected.Count > 6)
                names += $"\n\u2022 and {selected.Count - 6} more";
            var prompt = selected.Count == 1
                ? $"Delete {selected[0].DisplayName}?\n\nZones previously generated from this DXF will remain in the project."
                : $"Delete {selected.Count} selected DXFs?\n\n{names}\n\nZones previously generated from these DXFs will remain in the project.";
            if (MessageBox.Show(prompt, "Delete DXF", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            DeleteSources(selected);
        }

        internal void DeleteSources(IReadOnlyList<DxfSourceSession> selected)
        {
            if (selected.Count == 0)
                return;

            var snapshots = selected.Select(source => (
                Source: source,
                Index: LoadedDxfs.IndexOf(source),
                WasSelected: source.IsSelected)).ToList();
            var previousActive = ActiveDxf;
            _history.Execute(new DelegateProjectHistoryAction(
                selected.Count == 1 ? $"delete DXF {selected[0].DisplayName}" : $"delete {selected.Count} DXFs",
                () =>
                {
                    foreach (var snapshot in snapshots)
                        RemoveSource(snapshot.Source);
                },
                () =>
                {
                    foreach (var snapshot in snapshots.OrderBy(snapshot => snapshot.Index))
                    {
                        RestoreSource(snapshot.Source, snapshot.Index, showMissingWarning: true);
                        AttachUploaderSafely(snapshot.Source, showMissingWarning: true);
                    }
                    ActiveDxf = previousActive != null && LoadedDxfs.Contains(previousActive)
                        ? previousActive
                        : snapshots[0].Source;
                    _scene.SetSelectedDxfEntities(
                        snapshots.Where(snapshot => snapshot.WasSelected)
                            .Select(snapshot => snapshot.Source.SelectionTarget)
                            .ToList());
                }));
        }

        private void RestoreSource(DxfSourceSession source, int index, bool showMissingWarning)
        {
            if (LoadedDxfs.Contains(source))
                return;

            index = Math.Clamp(index, 0, LoadedDxfs.Count);
            LoadedDxfs.Insert(index, source);
            source.IsAttached = true;
            _scene.AddDxfEntity(source.SelectionTarget);
            SetActiveDxf(source);

            RefreshCollectionState();
            RaiseStateChanged();
        }

        private async Task<bool> AttachUploaderAsync(
            DxfSourceSession source,
            CancellationToken cancellationToken)
        {
            if (_viewport == null)
                return true;
            if (source.Uploader?.IsLoaded == true)
                return true;
            if (!File.Exists(source.FilePath))
            {
                source.IsAttached = false;
                StatusMessage = $"Missing DXF: {source.DisplayName}";
                return false;
            }

            var uploader = new DxfUploader(_viewport, source.SelectionTarget)
            {
                AlignmentTransform = MatrixFromTransformArray(source.SavedTransformArray),
                UseDxfColors = source.UseDxfColors
            };
            source.LoadCancellation?.Cancel();
            source.LoadCancellation?.Dispose();
            source.LoadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken loadToken = source.LoadCancellation.Token;
            try
            {
                bool success = await uploader.LoadDxfAsync(
                    source.FilePath,
                    GetShowText(source.SavedTransformArray),
                    loadToken);
                if (!success)
                {
                    StatusMessage = $"Could not load {source.DisplayName}: {uploader.LastLoadDiagnostic}";
                    uploader.Clear();
                    source.IsAttached = false;
                    return false;
                }
                if (loadToken.IsCancellationRequested || !LoadedDxfs.Contains(source))
                {
                    uploader.Clear();
                    return false;
                }

                uploader.ApplyDisplayState(
                    MatrixFromTransformArray(source.SavedTransformArray),
                    uploader.Opacity,
                    GetShowText(source.SavedTransformArray),
                    source.UseDxfColors);
                source.Uploader = uploader;
                source.IsAttached = true;
                uploader.SetSelectionHighlight(source.IsSelected);
                DxfLoaded?.Invoke(uploader);
                return true;
            }
            catch (OperationCanceledException)
            {
                uploader.Clear();
                source.IsAttached = false;
                return false;
            }
            catch (Exception ex)
            {
                uploader.Clear();
                source.IsAttached = false;
                StatusMessage = $"DXF load failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (source.LoadCancellation?.Token == loadToken)
                {
                    source.LoadCancellation.Dispose();
                    source.LoadCancellation = null;
                }
            }
        }

        private async void AttachUploaderSafely(DxfSourceSession source, bool showMissingWarning)
        {
            try
            {
                bool attached = await AttachUploaderAsync(source, CancellationToken.None);
                if (!attached && showMissingWarning && source.IsAttached)
                    return;
                if (!attached && showMissingWarning && LoadedDxfs.Contains(source))
                {
                    MessageBox.Show(
                        $"Could not restore DXF source:\n{source.FilePath}\n\nOther DXF sources will continue loading.",
                        "DXF Source Missing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"DXF load failed: {ex.Message}";
            }
        }

        private void RemoveSource(DxfSourceSession source)
        {
            if (!LoadedDxfs.Contains(source))
                return;

            source.LoadCancellation?.Cancel();
            source.LoadCancellation?.Dispose();
            source.LoadCancellation = null;
            var uploader = source.Uploader;
            if (uploader != null)
            {
                DxfRemoved?.Invoke(uploader);
                uploader.Clear();
                source.Uploader = null;
            }

            _scene.RemoveDxfEntity(source.SelectionTarget);
            LoadedDxfs.Remove(source);
            source.IsAttached = false;
            if (ReferenceEquals(ActiveDxf, source))
                ActiveDxf = LoadedDxfs.OrderBy(item => item.LoadedOrder).FirstOrDefault();
            RefreshCollectionState();
            RaiseStateChanged();
        }

        private void ClearAllSources()
        {
            ExitSelectionMode();
            foreach (var source in LoadedDxfs.ToList())
                RemoveSource(source);
            _scene.ClearDxfSelection();
        }

        private void SetActiveDxf(DxfSourceSession? source)
        {
            if (source != null && !LoadedDxfs.Contains(source))
                return;
            ActiveDxf = source;
            RaiseStateChanged();
        }

        private void OnDxfSelectionChanged(List<CraneOptimizer.UI.Core.Models.SceneDxf> selected)
        {
            var ids = selected.Select(entity => entity.Id).ToHashSet();
            foreach (var source in LoadedDxfs)
                source.IsSelected = ids.Contains(source.Id);
            if (selected.Count > 0)
                ActiveDxf = LoadedDxfs.FirstOrDefault(source => source.Id == selected[^1].Id) ?? ActiveDxf;
            OnPropertyChanged(nameof(SelectedDxfs));
            OnPropertyChanged(nameof(SelectedDxfCount));
            OnPropertyChanged(nameof(SelectionSummary));
            CommandManager.InvalidateRequerySuggested();
            RaiseStateChanged();
        }

        private void RefreshCollectionState()
        {
            for (var index = 0; index < LoadedDxfs.Count; index++)
                LoadedDxfs[index].LoadedOrder = index;
            OnPropertyChanged(nameof(LoadedDxfCount));
            OnPropertyChanged(nameof(SelectedDxfCount));
            OnPropertyChanged(nameof(SelectedDxfs));
            OnPropertyChanged(nameof(HasAnyDxf));
            OnPropertyChanged(nameof(HasMultipleDxfs));
            OnPropertyChanged(nameof(HasDxfLoaded));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(CurrentUploader));
            OnPropertyChanged(nameof(LoadedFileName));
            CommandManager.InvalidateRequerySuggested();
            UpdateStatusSummary();
        }

        private void UpdateStatusSummary()
        {
            StatusMessage = LoadedDxfs.Count switch
            {
                0 => "No DXF loaded",
                1 => $"Loaded: {LoadedDxfs[0].DisplayName}",
                _ => $"{LoadedDxfs.Count} DXFs loaded \u00b7 Active: {ActiveDxf?.DisplayName ?? "None"}"
            };
        }

        private void RaiseStateChanged() => DxfStateChanged?.Invoke();

        private static double[] NormalizeTransformArray(double[]? transformArray)
        {
            var normalized = new[] { 0d, 0d, 1d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, (double)DxfTransformModel.DefaultUnitIndex, 0d, 1d };
            if (transformArray == null)
                return normalized;
            for (var index = 0; index < Math.Min(normalized.Length, transformArray.Length); index++)
                normalized[index] = transformArray[index];
            return normalized;
        }

        private static bool GetShowText(double[] transformArray) =>
            transformArray.Length < 13 || transformArray[12] >= 0.5;

        private static Matrix3D MatrixFromTransformArray(double[] transformArray)
        {
            var model = DxfTransformModel.FromArray(transformArray);
            var scale = model.EffectiveScale;
            var radians = -model.RotationRadians;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            return new Matrix3D(
                scale * cos, -scale * sin, 0, 0,
                scale * sin, scale * cos, 0, 0,
                0, 0, 1, 0,
                model.OffsetX, model.OffsetY, 0, 1);
        }
    }
}
