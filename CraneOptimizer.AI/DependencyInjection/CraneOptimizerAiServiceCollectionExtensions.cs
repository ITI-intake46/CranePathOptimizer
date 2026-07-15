using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.AI.Knowledge;
using CraneOptimizer.AI.Providers.Ollama;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.ViewModels;
using CraneOptimizer.AI.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.DependencyInjection
{
    public static class CraneOptimizerAiServiceCollectionExtensions
    {
        public static IServiceCollection AddCraneOptimizerAi(this IServiceCollection services)
        {
            services.AddSingleton<IAiOptimizationAdvisorService, AiOptimizationAdvisorService>();
            services.AddSingleton<IAiSoftwareContextService, StaticAiSoftwareContextService>();

            services.AddSingleton<OllamaChatService>();
            services.AddSingleton<IAiChatService>(sp => sp.GetRequiredService<OllamaChatService>());
            services.AddSingleton<IAiStreamingChatService>(sp => sp.GetRequiredService<OllamaChatService>());
            services.AddSingleton<IAiModelManagementService, OllamaModelManagementService>();

            services.AddSingleton<IAiCopilotSettingsService, JsonAiCopilotSettingsService>();
            services.AddSingleton<IAiLocalDocumentSearchService, AiLocalDocumentSearchService>();
            services.AddSingleton<IAiSoftwareInventoryService, AiSoftwareInventoryService>();
            services.AddSingleton<IAiPromptTemplateService, AiPromptTemplateService>();
            services.AddSingleton<IAiReportAppendixService, AiReportAppendixService>();
            services.AddSingleton<IWorkflowReadinessService, WorkflowReadinessService>();
            services.AddSingleton<IUiInteractionContextService, UiInteractionContextService>();

            services.AddSingleton<AiAdvisorViewModel>();
            services.AddTransient<AiCompanionViewModel>();
            services.AddSingleton<IAiAdvisorWindowService, AiAdvisorWindowService>();

            return services;
        }
    }
}
