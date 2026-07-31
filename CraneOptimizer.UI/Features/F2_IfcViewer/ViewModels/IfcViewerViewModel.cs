using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.CreatedProject;
using Microsoft.Extensions.DependencyInjection;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel : ViewModelBase
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IIfcOptionsDialogService _dialogService;
        private readonly IDialogService _dialog;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly IElementVisibilityFilterService _filterService;
        private readonly IIfcSceneOperationService _ifcSceneOperations;
        private readonly IIfcVisibilityOptionsService _visibilityOptionsService;
        private readonly IIfcDiagnosticAuditService _auditService;
        private readonly IProjectWorkspaceService? _workspaceService;
        private readonly IIfcNormalizationDecisionService _normalizationService;
        private readonly IProjectMaterialSettingsService? _materialSettingsService;
        private readonly IProjectCoordinateTransformService? _transformService;
        private List<PrecastElement>? _pendingIfcElements;
        private IfcLoadOptionsDto? _pendingFilterOptions;
        private bool _isMovingExistingIfc;

        public ICommand LoadIfcCommand { get; }
        public ICommand MergeIfcCommand { get; }
        public ICommand CancelPlacementCommand { get; }
        public ICommand MoveIfcCommand { get; }
        public ICommand DeleteIfcCommand { get; }
        public ICommand ShowIfcDisplayOptionsCommand { get; }

        public event Action? IfcModelChanged;

        public string? LoadedIfcFilePath { get; private set; }
        public bool HasIfcModel => _ifcSceneOperations.HasIfcModel;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _status = "Idle";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private bool _isPlacementMode;
        public bool IsPlacementMode
        {
            get => _isPlacementMode;
            private set
            {
                _isPlacementMode = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PlacementPrompt => _isMovingExistingIfc
            ? "Click the new IFC origin location. Press Esc to cancel."
            : "Click to place IFC origin. Press Esc to cancel.";

        private Point3D? _pendingPlacementPoint;
        public Point3D? PendingPlacementPoint
        {
            get => _pendingPlacementPoint;
            private set
            {
                _pendingPlacementPoint = value;
                OnPropertyChanged();
            }
        }

        public IfcViewerViewModel(
            IIfcReaderRepository ifcReader,
            ISceneService scene,
            IDialogService dialog,
            IIfcOptionsDialogService dialogService,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            IElementVisibilityFilterService filterService,
            IIfcSceneOperationService ifcSceneOperations,
            IIfcDiagnosticAuditService auditService,
            IProjectWorkspaceService? workspaceService,
            IIfcNormalizationDecisionService normalizationService,
            IProjectCoordinateTransformService? transformService = null,
            IProjectMaterialSettingsService? materialSettingsService = null,
            IIfcVisibilityOptionsService? visibilityOptionsService = null)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;
            _interaction = interaction;
            _hitTest = hitTest;
            _filterService = filterService;
            _ifcSceneOperations = ifcSceneOperations;
            _visibilityOptionsService = visibilityOptionsService ?? new IfcVisibilityOptionsService(filterService);
            _auditService = auditService;
            _workspaceService = workspaceService;
            _normalizationService = normalizationService;
            _transformService = transformService ?? App.ServiceProvider?.GetService<IProjectCoordinateTransformService>();
            _materialSettingsService = materialSettingsService;

            LoadIfcCommand = new RelayCommand(async () => await LoadIfcAsync(), () => !IsLoading && !IsPlacementMode);
            MergeIfcCommand = new RelayCommand(ShowMergeIfcPlaceholder, () => !IsLoading && !IsPlacementMode);
            CancelPlacementCommand = new RelayCommand(CancelPlacement, () => IsPlacementMode);
            MoveIfcCommand = new RelayCommand(StartMoveIfc, CanMoveIfc);
            DeleteIfcCommand = new RelayCommand(DeleteIfc, CanEditPlacedIfc);
            ShowIfcDisplayOptionsCommand = new RelayCommand(ShowIfcDisplayOptions, CanEditPlacedIfc);
            if (_scene != null)
            {
                _scene.ElementsChanged += OnSceneElementsChanged;
            }
        }

        private async Task LoadIfcAsync()
        {
            string lastStatus = "Selecting file...";
            try
            {
                var osnap = App.ServiceProvider?.GetService<IOsnapService>();
                osnap?.SetMasterEnabled(false);

                IsLoading = true;
                Progress = 0;
                Status = "Selecting file...";

                var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
                if (string.IsNullOrEmpty(path)) return;

                var filter = _dialogService.Show(path);
                if (filter == null) return;

                Status = "Reading IFC...";

                string? firstStatus = null;
                lastStatus = "Opening IFC...";

                var progress = new Progress<IfcLoadingProgressUpdate>(update =>
                {
                    Progress = update.Progress;
                    if (!string.IsNullOrEmpty(update.Status))
                    {
                        Status = $"{update.Status} {update.Progress:F0}%";
                        if (firstStatus == null)
                        {
                            firstStatus = update.Status;
                            Debug.WriteLine($"[IFC-PERF] First UI status: {firstStatus}");
                        }
                        lastStatus = update.Status;
                    }
                });

                var readingOptions = await GetCurrentIfcReadingOptionsAsync();
                var all = await Task.Run(() =>
                    _ifcReader.ReadElements(path, progress, readingOptions));

                Debug.WriteLine("IFC Loaded: " + (all?.Count ?? 0));

                if (all == null || !all.Any())
                {
                    _dialog.ShowMessage("No elements found.");
                    return;
                }

                _pendingIfcElements = all;
                _pendingFilterOptions = filter;
                LoadedIfcFilePath = path;
                TryApplyIfcGeoreferencing();

                // Safe non-destructive background diagnostic audit trigger
                var auditPath = path;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var auditOptions = await GetCurrentIfcReadingOptionsAsync();
                        var auditResult = await _auditService.RunAuditAsync(auditPath, auditOptions);
                        var projectFolder = _workspaceService?.CurrentSession?.ProjectFolderPath;
                        await _auditService.GenerateReportsAsync(auditResult, projectFolder);
                        Debug.WriteLine("[IfcViewerViewModel] Background diagnostic audit completed successfully.");

                        // Run normalization decision preview
                        var previewResult = await _normalizationService.GenerateNormalizationPreviewAsync(auditResult);
                        await _normalizationService.GeneratePreviewReportsAsync(previewResult, projectFolder);
                        Debug.WriteLine("[IfcViewerViewModel] Background normalization preview reports completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[IfcViewerViewModel] Background diagnostic audit or normalization failed: {ex.Message}");
                    }
                });
                if (filter.CoordinateMode == CoordinateReferenceMode.IfcOrigin)
                {
                    // For IFC Origin Mapping, we do not translate or offset the elements at all.
                    // The model is placed exactly at its native local coordinates (offset = 0,0,0).
                    var placedElements = all;
                    PendingPlacementPoint = new Point3D(0, 0, 0);
                    DateTime viewportStartedUtc = DateTime.UtcNow;
                    var viewportTimer = Stopwatch.StartNew();
                    _visibilityOptionsService.ApplyOptions(filter);
                    _ifcSceneOperations.ReplaceIfcModel(placedElements);
                    viewportTimer.Stop();
                    IfcImportProfiler.Record(
                        "PreparingViewport",
                        "",
                        "Completed",
                        viewportStartedUtc,
                        DateTime.UtcNow,
                        viewportTimer.ElapsedMilliseconds,
                        "ElementsCount",
                        placedElements.Count,
                        true);
                    _pendingIfcElements = null;
                    _pendingFilterOptions = null;
                    _isMovingExistingIfc = false;
                    IsPlacementMode = false;
                    OnPropertyChanged(nameof(PlacementPrompt));
                    Status = $"Placed {_filterService.ShowingCount} of {placedElements.Count} elements using IFC Origin Mapping.";
                    IfcModelChanged?.Invoke();
                }
                else
                {
                    _isMovingExistingIfc = false;
                    PendingPlacementPoint = null;
                    IsPlacementMode = true;
                    OnPropertyChanged(nameof(PlacementPrompt));

                    _interaction.PushMode(new Interaction.IfcPlacementMode(
                        this,
                        _hitTest,
                        _interaction));

                    Progress = 100;
                    Status = $"Loaded {all.Count} elements. Click to place IFC origin.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IFC-PERF] IFC load failed: {ex}");
                _dialog.ShowMessage(ex.Message);
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine($"[IFC-PERF] Last UI status completed: {lastStatus}");
            }
        }

        public bool PlacePendingIfc(Point3D insertionPoint)
        {
            if (!IsPlacementMode || _pendingIfcElements == null || _pendingIfcElements.Count == 0)
                return false;

            var originalBounds = CalculateBounds(_pendingIfcElements);
            var placementOffset = CalculatePlacementOffset(_pendingIfcElements, insertionPoint);

            PendingPlacementPoint = placementOffset;

            var placedElements = _ifcSceneOperations.TranslateElements(
                _pendingIfcElements,
                new DomainPoint3D(placementOffset.X, placementOffset.Y, placementOffset.Z));

            var finalBounds = CalculateBounds(placedElements);

            // Log [IfcImport Z-Preservation Diagnosis] as strictly requested in safeguards
            Debug.WriteLine($"[IfcImport Z-Preservation Diagnosis] Original Z-Range: [{originalBounds.MinZ:F3}, {originalBounds.MaxZ:F3}]");
            Debug.WriteLine($"[IfcImport Z-Preservation Diagnosis] Insertion Offset Z: {placementOffset.Z:F3}");
            Debug.WriteLine($"[IfcImport Z-Preservation Diagnosis] Final Z-Range: [{finalBounds.MinZ:F3}, {finalBounds.MaxZ:F3}]");
            Debug.WriteLine($"[IfcImport Z-Preservation Diagnosis] Vertical Normalization Occurred: False (Preserved original coordinates)");

            Debug.WriteLine($"IFC Origin Placed At: {insertionPoint.X:F3}, {insertionPoint.Y:F3}, {insertionPoint.Z:F3}");
            if (_pendingFilterOptions != null)
                _visibilityOptionsService.ApplyOptions(_pendingFilterOptions);
            _ifcSceneOperations.ReplaceIfcModel(placedElements);

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Placed {_filterService.ShowingCount} of {placedElements.Count} elements";
            IfcModelChanged?.Invoke();
            return true;
        }

        public async Task<bool> LoadIfcFromProjectAsync(
            string filePath,
            Point3D insertionOffset,
            List<PrecastElement>? cachedElements = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                Debug.WriteLine("[IfcRestore] Fallback: IFC file path is null or file does not exist on disk.");
                return false;
            }

            string lastStatus = "Restoring IFC...";
            try
            {
                var osnap = App.ServiceProvider?.GetService<IOsnapService>();
                osnap?.SetMasterEnabled(false);

                IsLoading = true;
                Progress = 0;
                Status = "Restoring IFC...";

                List<PrecastElement>? elements = null;
                bool usedCache = false;

                if (cachedElements != null && cachedElements.Count > 0)
                {
                    elements = cachedElements;
                    usedCache = true;
                    Debug.WriteLine("[IfcRestore] Diagnostic: Cached restore used DB elements. Full extraction skipped.");
                    Status = $"Restored {elements.Count} elements from database cache. Initializing background render session...";
                    lastStatus = "Restoring from database cache";
                    
                    // Start only the xBIM model session/context in the background for detailed mesh rendering
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Debug.WriteLine("[IfcRestore] Background session: Initializing render-only xBIM session.");
                            await _ifcReader.InitializeSessionForRenderingAsync(filePath);
                            Debug.WriteLine("[IfcRestore] Background session: Render-only xBIM session initialized. Real mesh re-render triggered.");
                            
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                TryApplyIfcGeoreferencing();
                                IfcModelChanged?.Invoke();
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[IfcRestore] Background session: Failed to initialize xBIM session for rendering: {ex.Message}");
                        }
                    });
                }
                else
                {
                    string fallbackReason = cachedElements == null
                        ? "Cached element metadata is missing."
                        : "Cached elements list is empty.";

                    Debug.WriteLine($"[IfcRestore] Diagnostic: Restore used full IFC extraction. Fallback reason: {fallbackReason}.");

                    string? firstStatus = null;
                    lastStatus = "Opening IFC...";

                    var progress = new Progress<IfcLoadingProgressUpdate>(update =>
                    {
                        Progress = update.Progress;
                        if (!string.IsNullOrEmpty(update.Status))
                        {
                            Status = $"{update.Status} {update.Progress:F0}%";
                            if (firstStatus == null)
                            {
                                firstStatus = update.Status;
                                Debug.WriteLine($"[IFC-PERF] First UI status: {firstStatus}");
                            }
                            lastStatus = update.Status;
                        }
                    });

                    var readingOptions = await GetCurrentIfcReadingOptionsAsync();
                    elements = await Task.Run(() => _ifcReader.ReadElements(filePath, progress, readingOptions));

                    if (elements != null && elements.Count > 0)
                    {
                        // Safe non-destructive background diagnostic audit trigger
                        var auditPath = filePath;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var auditOptions = await GetCurrentIfcReadingOptionsAsync();
                                var auditResult = await _auditService.RunAuditAsync(auditPath, auditOptions);
                                var projectFolder = _workspaceService?.CurrentSession?.ProjectFolderPath;
                                await _auditService.GenerateReportsAsync(auditResult, projectFolder);
                                Debug.WriteLine("[IfcViewerViewModel] Background diagnostic audit completed successfully.");

                                // Run normalization decision preview
                                var previewResult = await _normalizationService.GenerateNormalizationPreviewAsync(auditResult);
                                await _normalizationService.GeneratePreviewReportsAsync(previewResult, projectFolder);
                                Debug.WriteLine("[IfcViewerViewModel] Background normalization preview reports completed successfully.");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[IfcViewerViewModel] Background diagnostic audit or normalization failed: {ex.Message}");
                            }
                        });
                    }
                    TryApplyIfcGeoreferencing();
                }

                if (elements == null || elements.Count == 0)
                {
                    Status = "Saved IFC contains no elements.";
                    return false;
                }

                var beforeFirstCOG = elements.FirstOrDefault()?.ElementCOG;

                // DIAGNOSTIC DETECTION FOR DOUBLE-TRANSLATION CORRUPTION IN LEGACY PROJECTS
                var firstEl = elements.FirstOrDefault();
                if (firstEl != null && usedCache)
                {
                    double dx = insertionOffset.X;
                    double dy = insertionOffset.Y;
                    double dz = insertionOffset.Z;
                    double offsetLen = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (offsetLen > 10.0)
                    {
                        var cog = firstEl.ElementCOG;
                        double distToSingle = Math.Sqrt(
                            Math.Pow(cog.X - dx, 2) +
                            Math.Pow(cog.Y - dy, 2) +
                            Math.Pow(cog.Z - dz, 2));
                        double distToDouble = Math.Sqrt(
                            Math.Pow(cog.X - 2 * dx, 2) +
                            Math.Pow(cog.Y - 2 * dy, 2) +
                            Math.Pow(cog.Z - 2 * dz, 2));

                        if (distToDouble < distToSingle && distToDouble < offsetLen * 0.5)
                        {
                            Debug.WriteLine("[IfcRestore Diagnosis] WARNING: Legacy double-translated coordinates detected in saved project state! The stored coordinates in the project file/DB appear to have been corrupted by double-translation. To avoid silently corrupting further, they are restored without translation, but visual alignment might require repositioning the IFC model.");
                        }
                    }
                }

                List<PrecastElement> placedElements;
                if (usedCache)
                {
                    // Cached elements already have the insertion offset baked in from user placement.
                    // Do NOT translate them again!
                    placedElements = elements;
                }
                else
                {
                    // Full extraction requires applying the insertion offset once.
                    placedElements = _ifcSceneOperations.TranslateElements(
                        elements,
                        new DomainPoint3D(insertionOffset.X, insertionOffset.Y, insertionOffset.Z));
                }

                var afterFirstCOG = placedElements.FirstOrDefault()?.ElementCOG;

                DateTime viewportStartedUtc = DateTime.UtcNow;
                var viewportTimer = Stopwatch.StartNew();
                _visibilityOptionsService.ApplyOptions(_visibilityOptionsService.GetCurrentOptions());
                _ifcSceneOperations.ReplaceIfcModel(placedElements);
                viewportTimer.Stop();
                IfcImportProfiler.Record(
                    "PreparingViewport",
                    "",
                    "Completed",
                    viewportStartedUtc,
                    DateTime.UtcNow,
                    viewportTimer.ElapsedMilliseconds,
                    "ElementsCount",
                    placedElements.Count,
                    true);
                LoadedIfcFilePath = filePath;
                PendingPlacementPoint = insertionOffset;

                var originalBounds = CalculateBounds(elements);
                var finalBounds = CalculateBounds(placedElements);

                // Log [IfcRestore Z-Preservation Diagnosis] as strictly requested in safeguards
                Debug.WriteLine($"[IfcRestore Z-Preservation Diagnosis] Original Z-Range: [{originalBounds.MinZ:F3}, {originalBounds.MaxZ:F3}]");
                Debug.WriteLine($"[IfcRestore Z-Preservation Diagnosis] Insertion Offset Z: {insertionOffset.Z:F3}");
                Debug.WriteLine($"[IfcRestore Z-Preservation Diagnosis] Final Z-Range: [{finalBounds.MinZ:F3}, {finalBounds.MaxZ:F3}]");
                Debug.WriteLine($"[IfcRestore Z-Preservation Diagnosis] Vertical Normalization Occurred: False (Preserved original coordinates)");

                // Log [IfcRestore Diagnosis] as strictly requested in safeguards
                Debug.WriteLine($"[IfcRestore Diagnosis] CacheUsed: {usedCache}");
                Debug.WriteLine($"[IfcRestore Diagnosis] Saved InsertionOffset: ({insertionOffset.X:F3}, {insertionOffset.Y:F3}, {insertionOffset.Z:F3})");
                Debug.WriteLine($"[IfcRestore Diagnosis] First Element COG Before: {(beforeFirstCOG != null ? $"({beforeFirstCOG.X:F3}, {beforeFirstCOG.Y:F3}, {beforeFirstCOG.Z:F3})" : "null")}");
                Debug.WriteLine($"[IfcRestore Diagnosis] First Element COG After: {(afterFirstCOG != null ? $"({afterFirstCOG.X:F3}, {afterFirstCOG.Y:F3}, {afterFirstCOG.Z:F3})" : "null")}");
                Debug.WriteLine($"[IfcRestore Diagnosis] PendingPlacementPoint Set To: {(PendingPlacementPoint != null ? $"({PendingPlacementPoint.Value.X:F3}, {PendingPlacementPoint.Value.Y:F3}, {PendingPlacementPoint.Value.Z:F3})" : "null")}");

                Progress = 100;

                if (usedCache)
                {
                    Status = $"Restored {placedElements.Count} elements (UI ready, meshes rendering in background)";
                }
                else
                {
                    Status = $"Restored {placedElements.Count} IFC elements via full extraction";
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IFC-PERF] IFC restore failed: {ex}");
                Status = $"IFC restore failed: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine($"[IFC-PERF] Last UI status completed: {lastStatus}");
            }
        }

        public bool MovePlacedIfc(Point3D insertionPoint)
        {
            if (!IsPlacementMode || !_isMovingExistingIfc)
                return false;

            var elements = _ifcSceneOperations.GetElements();
            if (elements.Count == 0)
                return false;

            var firstEl = elements.FirstOrDefault();
            var beforeCOG = firstEl?.ElementCOG;

            var originalBounds = CalculateBounds(elements);
            var currentOffset = PendingPlacementPoint ?? new Point3D(0, 0, 0);
            var delta = new Point3D(
                insertionPoint.X - currentOffset.X,
                insertionPoint.Y - currentOffset.Y,
                insertionPoint.Z - currentOffset.Z);

            var newOffset = new Point3D(
                currentOffset.X + delta.X,
                currentOffset.Y + delta.Y,
                currentOffset.Z + delta.Z);

            // CRITICAL rendering sync fix: Set PendingPlacementPoint BEFORE calling MoveIfcModel,
            // so that ReRenderElements triggered synchronously inside MoveIfcModel reads the new offset correctly!
            PendingPlacementPoint = newOffset;

            _ifcSceneOperations.MoveIfcModel(new DomainPoint3D(delta.X, delta.Y, delta.Z));

            var movedElements = _ifcSceneOperations.GetElements();
            var afterCOG = movedElements.FirstOrDefault()?.ElementCOG;
            var finalBounds = CalculateBounds(movedElements);

            // Log [IfcMove Z-Preservation Diagnosis] as strictly requested in safeguards
            Debug.WriteLine($"[IfcMove Z-Preservation Diagnosis] Original Z-Range: [{originalBounds.MinZ:F3}, {originalBounds.MaxZ:F3}]");
            Debug.WriteLine($"[IfcMove Z-Preservation Diagnosis] Insertion Offset Z: {newOffset.Z:F3}");
            Debug.WriteLine($"[IfcMove Z-Preservation Diagnosis] Final Z-Range: [{finalBounds.MinZ:F3}, {finalBounds.MaxZ:F3}]");
            Debug.WriteLine($"[IfcMove Z-Preservation Diagnosis] Vertical Normalization Occurred: False (Preserved original coordinates)");

            // Log [IfcMove Diagnosis] as strictly requested in safeguards
            Debug.WriteLine($"[IfcMove Diagnosis] Old Offset: ({currentOffset.X:F3}, {currentOffset.Y:F3}, {currentOffset.Z:F3})");
            Debug.WriteLine($"[IfcMove Diagnosis] New Offset: ({newOffset.X:F3}, {newOffset.Y:F3}, {newOffset.Z:F3})");
            Debug.WriteLine($"[IfcMove Diagnosis] Move Delta: ({delta.X:F3}, {delta.Y:F3}, {delta.Z:F3})");
            Debug.WriteLine($"[IfcMove Diagnosis] First Element COG Before: {(beforeCOG != null ? $"({beforeCOG.X:F3}, {beforeCOG.Y:F3}, {beforeCOG.Z:F3})" : "null")}");
            Debug.WriteLine($"[IfcMove Diagnosis] First Element COG After: {(afterCOG != null ? $"({afterCOG.X:F3}, {afterCOG.Y:F3}, {afterCOG.Z:F3})" : "null")}");

            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Moved IFC origin to {insertionPoint.X:F2}, {insertionPoint.Y:F2}, {insertionPoint.Z:F2}";
            IfcModelChanged?.Invoke();
            return true;
        }

        private void CancelPlacement()
        {
            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            if (!_isMovingExistingIfc)
            {
                PendingPlacementPoint = null;
                LoadedIfcFilePath = null;
            }
            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Progress = 0;
            Status = "IFC placement cancelled";
        }

        private void StartMoveIfc()
        {
            if (!CanEditPlacedIfc())
                return;

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = true;
            IsPlacementMode = true;
            OnPropertyChanged(nameof(PlacementPrompt));

            _interaction.PushMode(new Interaction.IfcMoveMode(
                this,
                _hitTest,
                _interaction));

            Status = "Click a new origin location for the IFC model.";
        }

        private void DeleteIfc()
        {
            if (!CanEditPlacedIfc())
                return;

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = false;
            PendingPlacementPoint = null;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));

            _ifcSceneOperations.DeleteIfcModel();
            LoadedIfcFilePath = null;
            Progress = 0;
            Status = "IFC model deleted";
            IfcModelChanged?.Invoke();
        }

        private void ShowIfcDisplayOptions()
        {
            if (!CanEditPlacedIfc())
                return;

            var appliedOptions = _dialogService.ShowDisplayOptions(LoadedIfcFilePath);
            if (appliedOptions == null)
                return;

            _visibilityOptionsService.ApplyOptions(appliedOptions);
            Status = $"Showing {_filterService.ShowingCount} of {_filterService.TotalCount} elements after IFC display options.";
        }

        private void ShowMergeIfcPlaceholder()
        {
            Status = "Merge IFC is available in the ribbon for multi-building workflow, but shared multi-model geometry loading is not implemented yet.";
        }

        private async Task<IfcReadingOptions> GetCurrentIfcReadingOptionsAsync()
        {
            var session = _workspaceService?.CurrentSession;
            if (session == null || _materialSettingsService == null)
                return new IfcReadingOptions(ProjectMaterialSettingsService.DefaultConcreteDensityKgPerM3);

            var density = await _materialSettingsService
                .GetConcreteDensityKgPerM3Async(session.ProjectId)
                .ConfigureAwait(false);

            return new IfcReadingOptions(density);
        }

        private bool CanEditPlacedIfc() => !IsLoading && !IsPlacementMode && _ifcSceneOperations.HasIfcModel;

        private bool CanMoveIfc()
        {
            if (!CanEditPlacedIfc())
                return false;

            if (_transformService != null && _transformService.IsEnabled && _transformService.CurrentReference?.Source == "IfcMapConversion")
            {
                return false;
            }

            return true;
        }

        private void OnSceneElementsChanged(List<PrecastElement> elements)
        {
            OnPropertyChanged(nameof(HasIfcModel));
            CommandManager.InvalidateRequerySuggested();
        }

        private Point3D CalculatePlacementOffset(
            IReadOnlyList<PrecastElement> sourceElements,
            Point3D insertionPoint)
        {
            var anchor = CalculatePlacementAnchor(sourceElements);
            return new Point3D(
                insertionPoint.X - anchor.X,
                insertionPoint.Y - anchor.Y,
                insertionPoint.Z - anchor.Z);
        }

        private DomainPoint3D CalculatePlacementAnchor(IReadOnlyList<PrecastElement> sourceElements)
        {
            var bounds = CalculateBounds(sourceElements);
            var centerX = (bounds.MinX + bounds.MaxX) * 0.5;
            var centerY = (bounds.MinY + bounds.MaxY) * 0.5;
            var extent = Math.Max(bounds.MaxX - bounds.MinX, bounds.MaxY - bounds.MinY);
            var distanceFromOrigin = Math.Sqrt(centerX * centerX + centerY * centerY);
            var horizontalAnchor = distanceFromOrigin > Math.Max(1000d, extent * 10d)
                ? new DomainPoint3D(centerX, centerY, 0d)
                : new DomainPoint3D(0d, 0d, 0d);

            var groundZ = 0d;
            return new DomainPoint3D(horizontalAnchor.X, horizontalAnchor.Y, groundZ);
        }

        private static double CalculateModelBaseElevation(IReadOnlyList<PrecastElement> elements)
            => CalculateBounds(elements).MinZ;

        private static (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
            CalculateBounds(IReadOnlyList<PrecastElement> elements)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var element in elements)
            {
                var cog = element.ElementCOG;
                var halfLength = (element.BBoxLength ?? element.ElementLength ?? 0d) / 2d;
                var halfWidth = (element.BBoxWidth ?? element.ElementWidth ?? 0d) / 2d;
                var halfHeight = (element.BBoxHeight ?? element.ElementHeight ?? 0d) / 2d;

                minX = Math.Min(minX, cog.X - halfLength);
                maxX = Math.Max(maxX, cog.X + halfLength);
                minY = Math.Min(minY, cog.Y - halfWidth);
                maxY = Math.Max(maxY, cog.Y + halfWidth);
                minZ = Math.Min(minZ, cog.Z - halfHeight);
                maxZ = Math.Max(maxZ, cog.Z + halfHeight);
            }

            return minX == double.MaxValue
                ? (0d, 0d, 0d, 0d, 0d, 0d)
                 : (minX, minY, minZ, maxX, maxY, maxZ);
        }

        private void TryApplyIfcGeoreferencing()
        {
            if (_transformService == null)
            {
                return;
            }

            var currentRef = _transformService.CurrentReference;
            if (currentRef != null && currentRef.IsEnabled)
            {
                if (currentRef.Source != "IfcMapConversion" && currentRef.Source != "IfcSiteLatLon")
                {
                    Debug.WriteLine("[IfcGeoreferencing] Manual reference detected. Overwrite protected.");
                    return;
                }
            }

            var result = _ifcReader.GetGeoreferencingData();
            if (result == null)
            {
                Debug.WriteLine("[IfcGeoreferencing] No georeferencing data found in IFC.");
                return;
            }

            var chosenMode = _pendingFilterOptions?.CoordinateMode ?? 
                             ((currentRef != null && currentRef.IsEnabled) ? CoordinateReferenceMode.IfcOrigin : CoordinateReferenceMode.LocalOnly);

            ProjectCoordinateReference reference;
            if (result.HasMapConversion)
            {
                double rotationToNorthDegrees = 0.0;
                if (result.XAxisAbscissa.HasValue && result.XAxisOrdinate.HasValue)
                {
                    double abscissa = result.XAxisAbscissa.Value;
                    double ordinate = result.XAxisOrdinate.Value;
                    if (abscissa != 0.0 || ordinate != 0.0)
                    {
                        double angleRad = Math.Atan2(ordinate, abscissa);
                        rotationToNorthDegrees = angleRad * 180.0 / Math.PI;
                    }
                }

                bool isEnabled = chosenMode == CoordinateReferenceMode.IfcOrigin;

                reference = new ProjectCoordinateReference
                {
                    IsEnabled = isEnabled,
                    Name = string.IsNullOrWhiteSpace(result.CrsName) ? "IFC Georeferenced" : result.CrsName,
                    CoordinateSystemName = string.IsNullOrWhiteSpace(result.CrsName) ? "Projected CRS" : result.CrsName,
                    EPSGCode = result.EpsgCode,
                    LocalReferenceX = 0.0,
                    LocalReferenceY = 0.0,
                    LocalReferenceZ = 0.0,
                    SurveyEasting = result.Eastings,
                    SurveyNorthing = result.Northings,
                    SurveyElevation = result.OrthogonalHeight,
                    RotationToNorthDegrees = rotationToNorthDegrees,
                    Scale = result.Scale ?? 1.0,
                    Source = "IfcMapConversion",
                    Latitude = result.SiteLatitude,
                    Longitude = result.SiteLongitude,
                    Description = string.IsNullOrWhiteSpace(result.CrsDescription) ? null : result.CrsDescription
                };

                Debug.WriteLine($"[IfcGeoreferencing] Applied IfcMapConversion: Enabled={isEnabled}, E={reference.SurveyEasting}, N={reference.SurveyNorthing}, Rotation={reference.RotationToNorthDegrees}°");
            }
            else if (result.HasSiteLatLon)
            {
                reference = new ProjectCoordinateReference
                {
                    IsEnabled = false,
                    Name = "IFC Site Location",
                    CoordinateSystemName = "WGS84",
                    LocalReferenceX = 0.0,
                    LocalReferenceY = 0.0,
                    LocalReferenceZ = 0.0,
                    SurveyElevation = result.SiteElevation ?? 0.0,
                    Source = "IfcSiteLatLon",
                    Latitude = result.SiteLatitude,
                    Longitude = result.SiteLongitude,
                    Description = $"WGS84 Latitude={result.SiteLatitude:F6}°, Longitude={result.SiteLongitude:F6}°"
                };

                Debug.WriteLine($"[IfcGeoreferencing] Site location detected (informational only): Lat={reference.Latitude}, Lon={reference.Longitude}");
            }
            else
            {
                return;
            }

            _transformService.ApplyReference(reference, out _);
        }
    }
}
