using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F3_CraneSetup.Services;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
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

namespace CraneOptimizer.UI
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
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
            services.AddSingleton<IIfcReaderRepository, IfcReaderRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddSingleton<IUnitSettingsService, UnitSettingsService>();
            services.AddSingleton<IIfcDiagnosticAuditService, IfcDiagnosticAuditService>();
            services.AddSingleton<IIfcExporterProfileResolver, IfcExporterProfileResolver>();
            services.AddSingleton<IIfcNormalizationDecisionService, IfcNormalizationDecisionService>();

            // =========================
            // Core Services
            // =========================
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IStatusService, StatusService>();
            services.AddSingleton<ISceneGridSizingService, SceneGridSizingService>();
            services.AddSingleton<IElementVisibilityFilterService, ElementVisibilityFilterService>();
            services.AddSingleton<IIfcSceneOperationService, IfcSceneOperationService>();

            // =========================
            // F10 Project Management
            // =========================
            services.AddSingleton<IProjectWorkspaceService, ProjectWorkspaceService>();
            services.AddSingleton<IProjectStateService, ProjectStateService>();
            services.AddTransient<IProjectService, ProjectService>();
            services.AddSingleton<IProjectShellService, ProjectShellService>();
            services.AddSingleton<IProjectNavigationService, ProjectNavigationService>();

            services.AddTransient<SplashScreenViewModel>();
            services.AddTransient<ProjectManagementViewModel>();

            services.AddTransient<SplashScreenWindow>();
            services.AddTransient<ProjectManagementWindow>();

            // =========================
            // Scene
            // =========================
            services.AddSingleton<SceneService>();
            services.AddSingleton<DrawingService>();
            services.AddSingleton<CoordinateService>();
            services.AddSingleton<ICoordinateTransformService, CoordinateTransformService>();
            services.AddSingleton<ICraneValidationService, CraneValidationService>();
            services.AddSingleton<IElementLiftStateService, ElementLiftStateService>();

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
            services.AddSingleton<CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces.IGADiagnosticsReaderService, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GADiagnosticsReaderService>();
            services.AddSingleton<CraneOptimizer.UI.Features.F8_GAOptimizer.Services.IGADiagnosticsWindowService, CraneOptimizer.UI.Features.F8_GAOptimizer.Services.GADiagnosticsWindowService>();


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
            services.AddScoped<ElementManagementViewModel>();
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

            base.OnStartup(e);

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

                await context.Database.MigrateAsync();

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
            services.AddScoped<IElementAssignmentService, ElementAssignmentService>();
            services.AddScoped<IFitnessEvaluator, FitnessEvaluator>();
            services.AddScoped<IGACsvDiagnosticsService, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GACsvDiagnosticsService>();
            services.AddSingleton<CraneSetupViewModel>();
            services.AddScoped<ISelectionStrategy, TournamentSelection>();
            services.AddScoped<ICrossoverOperator, CrossoverOperator>();
            services.AddScoped<IMutationOperator, MutationOperator>();
            services.AddScoped<IReplacementStrategy, ReplacementStrategy>();

            services.AddSingleton<GASettings>();
        }
    }
}


