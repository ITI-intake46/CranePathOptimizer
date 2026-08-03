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
using System.Windows;
using System.Windows.Threading;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F9_Settings.Views;
using CraneOptimizer.UI.F90_CraneSeedData.Services;
using CraneOptimizer.UI.Features.F17_Selection.Interaction;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using CraneOptimizer.UI.Features.F17_Selection.ViewModels;
using CraneOptimizer.UI.Shared.Behaviors;

namespace CraneOptimizer.UI
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
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
            // Unified runtime DB path (always under bin/.../Data/crane.db)
            // =========================
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "crane.db");

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
            services.AddScoped<CraneSeedService>();

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

        private async Task ApplyMigrationsAsync()
        {
            try
            {
                await using var scope = ServiceProvider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<CraneDbContext>();

                await SqliteMigrationCompatibilityGuard.EnsureCatalogOperatingConditionSchemaAsync(context);
                await SqliteMigrationCompatibilityGuard.EnsureCatalogCompanyNameSchemaAsync(context);

                await context.Database.MigrateAsync();

                await SqliteMigrationCompatibilityGuard.EnsureCatalogOperatingConditionSchemaAsync(context);
                await SqliteMigrationCompatibilityGuard.EnsureCatalogCompanyNameSchemaAsync(context);
                var seeder = scope.ServiceProvider.GetRequiredService<CraneSeedService>();
                await seeder.SeedAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Migration failed: {ex}");
                MessageBox.Show(ex.ToString(), "Database Error");
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


