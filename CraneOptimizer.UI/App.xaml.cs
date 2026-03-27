using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.IFC;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.UI.Shell.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CraneOptimizer.UI
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
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
            services.AddScoped<IIfcReader, IfcReader>();

            // =========================
            // UI Services
            // =========================
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IRenderingService, HelixRenderingService>();

            // =========================
            // Scene
            // =========================
            services.AddSingleton<SceneViewModel>();
            services.AddSingleton<DrawingViewModel>();

            // =========================
            // GA
            // =========================
            RegisterGAServices(services);

            // =========================
            // ViewModels
            // =========================
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SceneViewModel>();
            services.AddSingleton<DrawingViewModel>();
            services.AddSingleton<DrawingService>();

            services.AddSingleton<IRenderingService, HelixRenderingService>();

            // =========================
            // Views
            // =========================
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();

            mainWindow.Show();

            base.OnStartup(e);
        }

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
        }
    }
}