using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Infrastructure.GAAnalysis;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public static class GaLiftAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddGaLiftAnalysisFeature(this IServiceCollection services)
    {
        services.AddScoped<IGaLiftAnalysisService, GaLiftAnalysisService>();
        services.AddScoped<IGaAlternativeScreeningService, GaAlternativeScreeningService>();

        services.AddSingleton<IGaLiftAnalysisPathResolver, GaLiftAnalysisPathResolver>();
        services.AddSingleton<IGaLiftAnalysisSerializer, SystemTextJsonGaLiftAnalysisSerializer>();
        services.AddSingleton<IGaLiftAnalysisStore, JsonGaLiftAnalysisStore>();
        services.AddSingleton<IGaCraneCatalogProvider, EfGaCraneCatalogProvider>();
        services.AddSingleton<IGaTechnicalEvidenceProvider, GaTechnicalEvidenceProvider>();
        services.AddSingleton<IGaAnalysisCurrencyService, GaAnalysisCurrencyService>();
        services.AddSingleton<IGaAnalysisCurrentContextProvider, GaAnalysisCurrentContextProvider>();
        services.AddSingleton<IGaLiftAnalysisJobCoordinator, GaLiftAnalysisJobCoordinator>();
        services.AddSingleton<IGaLiftAnalysisWindowFactory, GaLiftAnalysisWindowFactory>();
        services.AddSingleton<IGaLiftAnalysisWindowService, GaLiftAnalysisWindowService>();
        services.AddSingleton<IGaLiftAnalysisPresentationActions, GaLiftAnalysisPresentationActions>();
        services.AddSingleton<IGaLiftSummaryPresentationMapper, GaLiftSummaryPresentationMapper>();

        services.AddTransient<GaLiftAnalysisViewModel>();
        services.AddTransient<GaLiftAnalysisWindow>();
        return services;
    }
}
