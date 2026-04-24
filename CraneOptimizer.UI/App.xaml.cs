using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.UI.Shell.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace CraneOptimizer.UI
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("Data/appsettings.json", optional: false)
                .Build();

            var services = new ServiceCollection();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // =========================
            // DbContext
            // =========================
            services.AddDbContext<CraneDbContext>(options =>
                options.UseSqlite(connectionString));

            // =========================
            // Infrastructure
            // =========================
            services.AddScoped<ICraneRepository, CraneRepository>();
            services.AddScoped<IIfcReaderRepository, IfcReaderRepository>();

            // =========================
            // UI Services
            // =========================
            services.AddSingleton<IDialogService, DialogService>();

            // =========================
            // Scene
            // =========================
            services.AddSingleton<SceneService>();
            services.AddSingleton<DrawingService>();
            services.AddSingleton<CoordinateService>();

            // =========================
            // GA
            // =========================
            RegisterGAServices(services);
            services.AddScoped<GAInputBuilder>();
            services.AddScoped<ICranePlacementService, CranePlacementService>();

            // =========================
            // Rendering and Geometry Builders
            // =========================
            services.AddSingleton<ElementVisualBuilder>();
            services.AddSingleton<IRenderingService, HelixRenderingService>();
            services.AddSingleton<ISceneService, SceneService>();
            services.AddSingleton<IIfcOptionsDialogService, IfcOptionsDialogService>();

            // =========================
            // ViewModels
            // =========================
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<DrawingViewModel>();
            services.AddSingleton<CranePlacementViewModel>();
            services.AddSingleton<IfcViewerViewModel>();
            //services.AddSingleton<DxfTabViewModel>();

            // =========================
            // Views
            // =========================
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            // DataContext is already assigned in MainWindow's constructor via DI.
            // Assigning it a second time here would toggle bindings unnecessarily.

            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Shut down the Dispatcher FIRST so no pending render callbacks
            // can fire against ViewModels that are about to be disposed.
            Dispatcher.InvokeShutdown();

            try
            {
                ServiceProvider?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing ServiceProvider: {ex}");
            }

            base.OnExit(e);
        }

        #region Global exception handlers

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log and show — capture stack trace
            Debug.WriteLine($"DispatcherUnhandledException: {e.Exception}");
            MessageBox.Show($"Unhandled UI exception:\n{e.Exception}\n\nSee Output window for details.", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            // If you want the app to continue, set e.Handled = true; otherwise the app will shut down
            e.Handled = false;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"CurrentDomain UnhandledException: {e.ExceptionObject}");
            // Do not attempt UI actions here if not on UI thread
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine($"UnobservedTaskException: {e.Exception}");
            e.SetObserved();
        }

        #endregion

        private static void RegisterGAServices(IServiceCollection services)
        {
            services.AddScoped<IGeneticAlgorithmEngine, GAEngine>();

            services.AddScoped<IGAInitializer, GAInitializer>();
            services.AddScoped<IElementAssignmentService, ElementAssignmentService>();
            services.AddScoped<IFitnessEvaluator, FitnessEvaluator>();

            services.AddScoped<ISelectionStrategy, TournamentSelection>();
            services.AddScoped<ICrossoverOperator, CrossoverOperator>();
            services.AddScoped<IMutationOperator, MutationOperator>();
            services.AddScoped<IReplacementStrategy, ReplacementStrategy>();
            services.AddSingleton<GASettings>();
        }
    }
}
