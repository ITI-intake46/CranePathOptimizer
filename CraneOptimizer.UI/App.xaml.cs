using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Reports.Builders;
using CraneOptimizer.Application.Reports.Inputs;
using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Application.Services;
using CraneOptimizer.AI.DependencyInjection;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.UI.AIIntegration;
using CraneOptimizer.UI.AIIntegration.Contributors;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Persistence;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Reports.Csv;
using CraneOptimizer.Infrastructure.Reports.Pdf;
using CraneOptimizer.Infrastructure.Reports.UserFacing;
using CraneOptimizer.Infrastructure.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Infrastructure.GAAnalysis;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F3_CraneSetup.Services;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.F10_ProjectManagement.Views;
using CraneOptimizer.UI.Shell.Services;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.UI.Shell.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F9_Settings.Views;
using CraneOptimizer.UI.Features.F17_Selection.Interaction;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using CraneOptimizer.UI.Features.F17_Selection.ViewModels;
using CraneOptimizer.UI.Shared.Behaviors;

namespace CraneOptimizer.UI
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;
        private AcceptanceUiOptions? _acceptanceUiOptions;

        protected override void OnStartup(StartupEventArgs e)
        {
            _acceptanceUiOptions = AcceptanceUiOptions.TryParse(e.Args);
            SmoothScrollBehavior.Initialize();
            CraneOptimizer.UI.Resources.NativeTitleBarTheme.Initialize();

            // Initialize transitional static holder
            ZoneGeometryServiceHolder.Instance = new CraneOptimizer.Domain.Services.ZoneGeometryService();

            // Global exception handlers
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Apply default theme — Dark. Can be changed later via ThemeService.Instance.ApplyTheme(AppTheme.Light)
            CraneOptimizer.UI.Resources.ThemeService.Instance.ApplyTheme(
                CraneOptimizer.UI.Resources.AppTheme.Dark);

            var services = new ServiceCollection();

            // =========================
            // Catalog persistence: immutable shipped baseline + writable per-user copy.
            // Never migrate a database under Program Files / the application directory.
            // =========================
            var catalogBootstrapper = new CatalogDatabaseBootstrapper();
            catalogBootstrapper.EnsureWritableCatalogExists();
            var dbPath = catalogBootstrapper.WritableCatalogPath;
            services.AddSingleton(catalogBootstrapper);

            // =========================
            // DbContext
            // =========================
            services.AddDbContext<CraneDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddDbContextFactory<CraneDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // =========================
            // Infrastructure
            // =========================
            services.AddScoped<ICraneRepository, CraneRepository>();
            services.AddSingleton<IIfcModelSessionService, IfcModelSessionService>();
            services.AddSingleton<IIfcGeoreferencingExtractor, IfcGeoreferencingExtractor>();
            services.AddSingleton<IIfcReaderRepository, IfcReaderRepository>();
            services.AddSingleton<IProjectRepository, ProjectRepository>();
            services.AddSingleton<IProjectMaterialSettingsService, ProjectMaterialSettingsService>();
            services.AddSingleton<IUnitSettingsService, UnitSettingsService>();
            services.AddSingleton<IIfcDiagnosticAuditService, IfcDiagnosticAuditService>();
            services.AddSingleton<IIfcExporterProfileResolver, IfcExporterProfileResolver>();
            services.AddSingleton<ICentralIfcPropertyAliasResolver, CentralIfcPropertyAliasResolver>();
            services.AddSingleton<IIfcNormalizationDecisionService, IfcNormalizationDecisionService>();

            // =========================
            // Core Services
            // =========================
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IDeleteConfirmationService, DeleteConfirmationService>();
            services.AddSingleton<IStatusService, StatusService>();
            services.AddSingleton<ISceneGridSizingService, SceneGridSizingService>();
            services.AddSingleton<IElementVisibilityFilterService, ElementVisibilityFilterService>();
            services.AddSingleton<IIfcSceneOperationService, IfcSceneOperationService>();
            services.AddSingleton<IIfcVisibilityOptionsService, IfcVisibilityOptionsService>();
            services.AddSingleton<IImportReadinessService, ImportReadinessService>();

            // =========================
            // F10 Project Management
            // =========================
            services.AddSingleton<IProjectWorkspaceService, ProjectWorkspaceService>();
            services.AddSingleton<IProjectStateService, ProjectStateService>();
            services.AddTransient<IProjectService, ProjectService>();
            services.AddSingleton<IProjectShellService, ProjectShellService>();
            services.AddSingleton<IProjectNavigationService, ProjectNavigationService>();

            // =========================
            // Project Output Path Services (Phase 5E)
            // =========================
            services.AddSingleton<IActiveProjectPathProvider, UiActiveProjectPathProvider>();
            services.AddSingleton<IProjectOutputPathService, ProjectOutputPathService>();
            services.AddSingleton<IGaResultSnapshotMapper, GaResultSnapshotMapper>();
            services.AddSingleton<IGaResultSnapshotValidator, GaResultSnapshotValidator>();
            services.AddSingleton<IGaSnapshotClassificationService, GaSnapshotClassificationService>();
            services.AddSingleton<IGaModelIdentityService, GaModelIdentityService>();
            services.AddSingleton<IGaCurrentResultStore, GaCurrentResultStore>();
            services.AddSingleton<IGaSnapshotReportAdapter, GaSnapshotReportAdapter>();
            services.AddSingleton<IGaResultSnapshotPathResolver, GaResultSnapshotPathResolver>();
            services.AddSingleton<IGaResultSnapshotStore, JsonGaResultSnapshotStore>();
            services.AddSingleton<IGaLegacyCsvSnapshotAdapter, LegacyCsvGaSnapshotAdapter>();
            services.AddSingleton<IGaResultSnapshotProvider, GaResultSnapshotProvider>();
            services.AddGaLiftAnalysisFeature();

            services.AddTransient<SplashScreenViewModel>();
            services.AddTransient<ProjectManagementViewModel>();

            services.AddTransient<SplashScreenWindow>();
            services.AddTransient<ProjectManagementWindow>();

            // =========================
            // Scene
            // =========================
            services.AddSingleton<IZoneGeometryService, CraneOptimizer.Domain.Services.ZoneGeometryService>();
            services.AddSingleton<SceneService>();
            services.AddSingleton<DrawingService>();
            services.AddSingleton<CoordinateService>();
            services.AddSingleton<ICoordinateTransformService, CoordinateTransformService>();
            services.AddSingleton<IProjectCoordinateTransformService, ProjectCoordinateTransformService>();
            services.AddSingleton<ICoordinateReadoutFormatter, CoordinateReadoutFormatter>();
            services.AddSingleton<ICraneValidationService, CraneValidationService>();
            services.AddSingleton<IElementLiftStateService, ElementLiftStateService>();
            services.AddSingleton<IDxfZoneImportService, DxfZoneImportService>();
            services.AddSingleton<IDxfLayerZoneAssignmentService, DxfLayerZoneAssignmentService>();
            services.AddSingleton<IProjectHistoryService, ProjectHistoryService>();

            // =========================
            // GA
            // =========================
            RegisterGAServices(services);

            services.AddScoped<IOptimizationInputBuilder, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope.OptimizationInputBuilder>();
            services.AddScoped<ICranePlacementEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration.CranePlacementEngine>();
            services.AddScoped<IOptimizationScopeEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope.OptimizationScopeEngine>();
            services.AddScoped<ICranePlacementService, CranePlacementService>();
            services.AddSingleton<ICraneOptimizerDialogService, CraneOptimizerDialogService>();
            services.AddSingleton<IOptimizationViewportHighlightService, OptimizationViewportHighlightService>();
            services.AddSingleton<IGAResultsWindowService, GAResultsWindowService>();
            services.AddSingleton<IGaSnapshotToUiResultMapper, GaSnapshotToUiResultMapper>();
            services.AddSingleton<CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces.IGADiagnosticsReaderService, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GADiagnosticsReaderService>();
            services.AddSingleton<CraneOptimizer.UI.Features.F8_GAOptimizer.Services.IGADiagnosticsWindowService, CraneOptimizer.UI.Features.F8_GAOptimizer.Services.GADiagnosticsWindowService>();

            // =========================
            // AI Assist / Local Ollama Studio
            // =========================
            services.AddCraneOptimizerAi();
            services.AddSingleton<IAiScreenContextContributor, IfcAiContextContributor>();
            services.AddSingleton<IAiScreenContextContributor, GaAiContextContributor>();
            services.AddSingleton<IAiScreenContextContributor, CraneLoadChartAiContextContributor>();
            services.AddSingleton<IAiScreenContextContributor, ElementManagementAiContextContributor>();
            services.AddSingleton<IAiScreenContextContributor, ReportAiContextContributor>();
            services.AddSingleton<AiHostContextBridge>();
            services.AddSingleton<IAiOptimizationContextProvider>(sp => sp.GetRequiredService<AiHostContextBridge>());
            services.AddSingleton<IAiWorkflowContextProvider>(sp => sp.GetRequiredService<AiHostContextBridge>());

            // =========================
            // User-facing Reports
            // =========================
            services.AddSingleton<IReportValidator, ReportValidator>();
            services.AddSingleton<IReportValueFormatter, ReportValueFormatter>();
            services.AddSingleton<IReportDefinitionProvider, ReportDefinitionProvider>();
#pragma warning disable CS0618
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IReportExporter, CsvReportExporter>();
            services.AddSingleton<IReportExporter, PdfReportExporter>();
            services.AddSingleton<IReportBuilder<OptimizationReportInput>, OptimizationSummaryReportBuilder>();
#pragma warning restore CS0618
            services.AddSingleton<CraneOptimizer.Application.Reports.Services.IReportBuilder, UserFacingReportBuilder>();
            services.AddSingleton<IReportableElementResolver, ReportableElementResolver>();
            services.AddSingleton<IElementReportDataProvider, ElementReportDataProvider>();
            services.AddSingleton<ILiftingResultReportDataProvider, LiftingResultReportDataProvider>();
            services.AddSingleton<IReportPdfExporter, UserFacingReportPdfExporter>();
            services.AddSingleton<IReportExcelExporter, UserFacingReportExcelExporter>();
            services.AddSingleton<IReportTemplateStore, JsonReportTemplateStore>();
            services.AddSingleton<IReportFileDialogService, ReportFileDialogService>();
            services.AddSingleton<IReportPreviewDialogService, ReportPreviewDialogService>();
            services.AddSingleton<IReportBuilderDialogService, ReportBuilderDialogService>();

            // =========================
            // Rendering
            // =========================
            services.AddSingleton<IRenderDiagnosticsStore, RenderDiagnosticsStore>();
            services.AddSingleton<ElementVisualBuilder>();
            services.AddSingleton<CraneVisualBuilder>();       // shared singleton — carries CraneParametersReady event
            services.AddSingleton<IRenderingService, HelixRenderingService>();
            services.AddSingleton<ISceneRenderingManager, SceneRenderingManager>();
            services.AddSingleton<ICameraService, CameraService>();
            services.AddSingleton<IViewportHitTestService, ViewportHitTestService>();
            services.AddSingleton<IViewportInteractionService, ViewportInteractionService>();
            services.AddSingleton<IOriginService, OriginService>();
            services.AddSingleton<ICraneSceneInteractionService, CraneSceneInteractionService>();
            services.AddSingleton<ISceneService>(provider => provider.GetRequiredService<SceneService>());
            services.AddSingleton<IViewportSelectionService, ViewportSelectionService>();
            services.AddSingleton<IIfcOptionsDialogService, IfcOptionsDialogService>();

            // =========================
            // ViewModels
            // =========================
            services.AddTransient<MainViewModel>();
            services.AddSingleton<CraneManagerViewModel>();    // right-panel properties VM
            services.AddSingleton<DrawingViewModel>();
            services.AddSingleton<CranePlacementViewModel>();
            services.AddSingleton<IfcViewerViewModel>();
            services.AddSingleton<DxfTabViewModel>();
            services.AddSingleton<CraneOptimizer.UI.Features.F18_CoordinateReference.ViewModels.CoordinateReferenceViewModel>();
            services.AddScoped<ElementManagementViewModel>();
            services.AddTransient<ReportBuilderViewModel>();
            services.AddSingleton<SelectionHandler>();
            services.AddSingleton<HoverHitCache>();
            services.AddTransient<SelectionViewModel>();

            // =========================
            // F15 Object Snap
            // =========================
            services.AddSingleton<OsnapSettingsService>();
            services.AddSingleton<OsnapSettings>(sp =>
                sp.GetRequiredService<OsnapSettingsService>().Load());
            services.AddSingleton<IOsnapService, OsnapService>();
            services.AddSingleton<OsnapPanelViewModel>();
            services.AddSingleton<OsnapGeometryBridge>();

            // =========================
            // Views
            // =========================
            services.AddTransient<MainWindow>();
            services.AddTransient<UnitsSettingsDialog>();

            ServiceProvider = services.BuildServiceProvider();
            AiServiceLocator.ServiceProvider = ServiceProvider;

            base.OnStartup(e);
            CraneOptimizer.Domain.Diagnostics.DebugOutputGate.EnableProjectPerformanceCapture =
                CraneOptimizer.UI.Core.Configuration.AppFeatureFlags.EnableProjectPerformanceCapture;

            // Start pipeline safely on UI thread
            _ = Dispatcher.InvokeAsync(async () => await StartupPipelineAsync());
        }

        private async Task StartupPipelineAsync()
        {
            SplashScreenWindow? splash = null;
            SplashScreenViewModel? vm = null;

            try
            {
                // =========================
                // Splash (MainWindow)
                // =========================
                splash = ServiceProvider.GetRequiredService<SplashScreenWindow>();
                vm = ServiceProvider.GetRequiredService<SplashScreenViewModel>();
                splash.DataContext = vm;
                MainWindow = splash;
                splash.Show();

                try
                {
                    vm.Status = "Initializing...";

                    vm.Status = "Updating database...";
                    await ApplyMigrationsAsync();

                    vm.Status = "Starting application...";

                    if (_acceptanceUiOptions != null)
                    {
                        vm.Status = "Opening SOFTWARE ACCEPTANCE FIXTURE...";
                        await RunAcceptanceUiModeAsync(_acceptanceUiOptions, splash);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Startup error: {ex}");
                    MessageBox.Show(ex.ToString(), "Startup Error");
                    vm.Status = "Startup failed.";
                    return;
                }

                // =========================
                // Navigate to Project Management
                // =========================
                ProjectManagementWindow projectWindow;

                try
                {
                    projectWindow = ServiceProvider.GetRequiredService<ProjectManagementWindow>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Window creation failed: {ex}");
                    MessageBox.Show(ex.ToString(), "Window Creation Failed");
                    vm.Status = "Startup failed. Project window could not be created.";
                    return;
                }

                MainWindow = projectWindow;
                projectWindow.Show();

                splash.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup error: {ex}");
                MessageBox.Show(ex.ToString(), "Startup Error");

                if (splash != null)
                {
                    MainWindow = splash;
                    if (!splash.IsVisible)
                        splash.Show();
                }

                if (vm != null)
                    vm.Status = "Startup failed.";
            }
        }

        private async Task RunAcceptanceUiModeAsync(AcceptanceUiOptions options, SplashScreenWindow splash)
        {
            string evidencePath = Path.GetFullPath(options.EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath) ?? AppContext.BaseDirectory);

            try
            {
                var workspace = ServiceProvider.GetRequiredService<IProjectWorkspaceService>();
                var navigation = ServiceProvider.GetRequiredService<IProjectNavigationService>();
                var sessionService = ServiceProvider.GetRequiredService<IIfcModelSessionService>();

                var session = await workspace.OpenProjectFolderAsync(
                    Path.GetFullPath(options.ProjectFolder),
                    CancellationToken.None);
                await navigation.OpenMainWindowAsync(session);

                splash.Close();

                if (Current.MainWindow is not MainWindow mainWindow ||
                    mainWindow.DataContext is not MainViewModel mainViewModel)
                {
                    throw new InvalidOperationException("Acceptance mode could not resolve the main application window/view-model.");
                }

                var deadline = DateTime.UtcNow.AddSeconds(options.TimeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    var nativeSession = sessionService.Current;
                    if (!mainViewModel.IfcViewer.IsLoading &&
                        mainViewModel.IfcViewer.HasIfcModel &&
                        !string.IsNullOrWhiteSpace(mainViewModel.IfcViewer.LoadedIfcFilePath) &&
                        mainViewModel.Scene.GetElements().Count > 0 &&
                        nativeSession != null &&
                        nativeSession.ShapeInstanceCount > 0)
                    {
                        break;
                    }

                    await Task.Delay(250);
                }

                var activeNativeSession = sessionService.Current;
                var elements = mainViewModel.Scene.GetElements();
                var loadedIfc = mainViewModel.IfcViewer.LoadedIfcFilePath;
                bool visibleGeometryReady = mainWindow.IsVisible &&
                    mainViewModel.IfcViewer.HasIfcModel &&
                    elements.Count > 0 &&
                    activeNativeSession != null &&
                    activeNativeSession.ShapeInstanceCount > 0;

                // Acceptance must prove that the supported project is not merely renderable: it
                // must also reopen with production GA-ready lift units. This catches project-cache
                // drift where a portable project silently falls back to a fresh IFC extraction and
                // loses persisted role/trust/GA metadata.
                var readinessService = ServiceProvider.GetRequiredService<IImportReadinessService>();
                var readiness = readinessService.BuildSummary(elements);
                var targetElementTypes = elements
                    .Where(element => element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.LiftAssembly
                                      && element.IncludeInGa
                                      && !string.IsNullOrWhiteSpace(element.ElementType))
                    .Select(element => element.ElementType.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var liftUnitCounts = GaLiftUnitInputPreparation.CountLiftUnits(
                    elements,
                    elements,
                    targetElementTypes);
                bool gaReady = readiness.GaCandidateElements > 0 && liftUnitCounts.FinalCandidateCount > 0;

                bool selectionRoundTrip = false;
                string? selectedElement = null;
                if (elements.Count > 0)
                {
                    mainViewModel.Scene.SetSelectedElements(new List<CraneOptimizer.Domain.Schemas.PrecastElement> { elements[0] });
                    var selected = mainViewModel.Scene.GetSelectedElements();
                    selectionRoundTrip = selected.Count == 1 &&
                        string.Equals(selected[0].ElementID, elements[0].ElementID, StringComparison.Ordinal);
                    selectedElement = selected.FirstOrDefault()?.ElementID;
                }

                int initialShapeCount = activeNativeSession?.ShapeInstanceCount ?? 0;
                int reopenedShapeCount = 0;
                bool closeReopen = false;
                if (!string.IsNullOrWhiteSpace(loadedIfc) && File.Exists(loadedIfc))
                {
                    sessionService.Clear();
                    var reopened = sessionService.OpenSession(loadedIfc, cancellationToken: CancellationToken.None);
                    reopenedShapeCount = reopened.ShapeInstanceCount;
                    closeReopen = reopenedShapeCount > 0;
                }

                bool passed = visibleGeometryReady && selectionRoundTrip && closeReopen && gaReady;
                var evidence = new
                {
                    schemaVersion = 1,
                    release = "REV05",
                    fixtureType = "SOFTWARE ACCEPTANCE FIXTURE",
                    passed,
                    projectFolder = Path.GetFullPath(options.ProjectFolder),
                    mainApplicationExecutable = Environment.ProcessPath,
                    mainWindowVisible = mainWindow.IsVisible,
                    loadedIfcFilePath = loadedIfc,
                    sceneElementCount = elements.Count,
                    nativeShapeInstanceCount = initialShapeCount,
                    ifcViewerHasModel = mainViewModel.IfcViewer.HasIfcModel,
                    gaReadinessCandidateElements = readiness.GaCandidateElements,
                    groupedAssemblyLiftUnits = liftUnitCounts.GroupedAssemblyCount,
                    standaloneLiftUnits = liftUnitCounts.StandaloneCount,
                    finalGaLiftUnits = liftUnitCounts.FinalCandidateCount,
                    gaReady,
                    gaBlockingMessages = readiness.Messages
                        .Where(message => string.Equals(message.Severity, "Error", StringComparison.OrdinalIgnoreCase))
                        .Select(message => new { message.Code, message.Message, message.RecommendedAction })
                        .ToArray(),
                    selectionRoundTrip,
                    selectedElement,
                    closeReopenNativeSession = closeReopen,
                    reopenedShapeInstanceCount = reopenedShapeCount,
                    ifcStatus = mainViewModel.IfcViewer.Status,
                    generatedAtUtc = DateTime.UtcNow
                };

                File.WriteAllText(
                    evidencePath,
                    JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

                if (options.HoldSeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(options.HoldSeconds));

                Shutdown(passed ? 0 : 2);
            }
            catch (Exception ex)
            {
                var failure = new
                {
                    schemaVersion = 1,
                    release = "REV05",
                    fixtureType = "SOFTWARE ACCEPTANCE FIXTURE",
                    passed = false,
                    error = ex.ToString(),
                    generatedAtUtc = DateTime.UtcNow
                };
                File.WriteAllText(
                    evidencePath,
                    JsonSerializer.Serialize(failure, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
                Shutdown(2);
            }
        }

        private sealed record AcceptanceUiOptions(
            string ProjectFolder,
            string EvidencePath,
            int HoldSeconds,
            int TimeoutSeconds)
        {
            public static AcceptanceUiOptions? TryParse(string[] args)
            {
                if (args == null || args.Length == 0)
                    return null;

                string? project = null;
                string? evidence = null;
                int holdSeconds = 3;
                int timeoutSeconds = 90;

                for (int i = 0; i < args.Length; i++)
                {
                    string current = args[i];
                    if (string.Equals(current, "--acceptance-project", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        project = args[++i];
                    else if (string.Equals(current, "--acceptance-evidence", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        evidence = args[++i];
                    else if (string.Equals(current, "--acceptance-hold-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[++i], out int parsedHold))
                        holdSeconds = Math.Max(0, Math.Min(parsedHold, 60));
                    else if (string.Equals(current, "--acceptance-timeout-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[++i], out int parsedTimeout))
                        timeoutSeconds = Math.Max(15, Math.Min(parsedTimeout, 300));
                }

                if (string.IsNullOrWhiteSpace(project) && string.IsNullOrWhiteSpace(evidence))
                    return null;
                if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(evidence))
                    throw new ArgumentException("Acceptance UI mode requires both --acceptance-project and --acceptance-evidence.");

                return new AcceptanceUiOptions(project, evidence, holdSeconds, timeoutSeconds);
            }
        }

        private async Task ApplyMigrationsAsync()
        {
            try
            {
                await using var scope = ServiceProvider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<CraneDbContext>();

                // A verified backup is created before *any* compatibility shim or EF migration
                // can mutate an existing database. Failure aborts startup and migration.
                var bootstrapper = scope.ServiceProvider.GetRequiredService<CatalogDatabaseBootstrapper>();
                bootstrapper.CreateVerifiedPreMigrationBackup();

                await SqliteMigrationCompatibilityGuard.EnsureCatalogOperatingConditionSchemaAsync(context);
                await SqliteMigrationCompatibilityGuard.EnsureCatalogCompanyNameSchemaAsync(context);

                await context.Database.MigrateAsync();

                await SqliteMigrationCompatibilityGuard.EnsureCatalogOperatingConditionSchemaAsync(context);
                await SqliteMigrationCompatibilityGuard.EnsureCatalogCompanyNameSchemaAsync(context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Migration failed: {ex}");
                MessageBox.Show(ex.ToString(), "Database Error");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                ServiceProvider?.GetService<IGaLiftAnalysisJobCoordinator>()?.CancelAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lift analysis shutdown cancellation error: {ex}");
            }

            Dispatcher.InvokeShutdown();

            try
            {
                ServiceProvider?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dispose error: {ex}");
            }

            base.OnExit(e);
        }

        #region Exception Handlers

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception);
            MessageBox.Show(e.Exception.ToString(), "UI Error");
            e.Handled = false;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine(e.ExceptionObject);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception);
            e.SetObserved();
        }

        #endregion

        private static void RegisterGAServices(IServiceCollection services)
        {
            services.AddScoped<IGeneticAlgorithmEngine, GAEngine>();

            services.AddScoped<ICraneConfigurationEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration.CraneConfigurationEngine>();
            services.AddScoped<ILiftingCalculationEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation.LiftingCalculationEngine>();
            services.AddScoped<CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces.ILoadChartCapacityResolver, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.LoadChartCapacityResolver>();
            services.AddScoped<IConstraintEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints.ConstraintEngine>();
            services.AddScoped<ICoverageEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage.CoverageEngine>();

            services.AddScoped<IGAInitializer, GAInitializer>();
            services.AddScoped<IReachabilityMapService, ReachabilityMapService>();
            services.AddScoped<ElementAssignmentService>();
            services.AddScoped<HungarianAssignmentService>();
            services.AddScoped<IElementAssignmentService, RoutingAssignmentService>();
            services.AddScoped<IMultiStageGAOrchestrator, MultiStageGAOrchestrator>();
            services.AddScoped<IFitnessEvaluator, FitnessEvaluator>();
            services.AddScoped<IGACsvDiagnosticsService, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GACsvDiagnosticsService>();
            services.AddSingleton<CraneSetupViewModel>();
            services.AddScoped<ISelectionStrategy, TournamentSelection>();
            services.AddScoped<ICrossoverOperator, CrossoverOperator>();
            services.AddScoped<IMutationOperator, MutationOperator>();
            services.AddScoped<IReplacementStrategy, ReplacementStrategy>();

            services.AddSingleton<IOptimizationHistoryLogger, CsvOptimizationHistoryLogger>();
            services.AddSingleton<GASettings>();
        }
    }
}


