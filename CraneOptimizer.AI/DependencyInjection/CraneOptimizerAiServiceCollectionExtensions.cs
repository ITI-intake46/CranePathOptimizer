using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Agents.Actions;
using CraneOptimizer.AI.Agents.Readiness;
using CraneOptimizer.AI.Agents.Runtime;
using CraneOptimizer.AI.Agents.Tools;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.AI.Knowledge;
using CraneOptimizer.AI.Knowledge.Rag;
using CraneOptimizer.AI.Providers.Ollama;
using CraneOptimizer.AI.Runtime;
using CraneOptimizer.AI.Security;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.StructuredOutput;
using CraneOptimizer.AI.ViewModels;
using CraneOptimizer.AI.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.DependencyInjection
{
    public static class CraneOptimizerAiServiceCollectionExtensions
    {
        public static IServiceCollection AddCraneOptimizerAi(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IAiCopilotSettingsService, JsonAiCopilotSettingsService>();

            services.AddSingleton<OllamaChatService>();
            services.AddSingleton<IAiChatProvider>(sp => sp.GetRequiredService<OllamaChatService>());
            services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
            services.AddSingleton<IAiRequestPolicy, DefaultAiRequestPolicy>();
            services.AddSingleton<InMemoryAiTelemetrySink>();
            services.AddSingleton<IAiTelemetrySink>(sp => sp.GetRequiredService<InMemoryAiTelemetrySink>());
            services.AddSingleton<IAiTelemetryStore>(sp => sp.GetRequiredService<InMemoryAiTelemetrySink>());
            services.AddSingleton<AiChatGateway>();
            services.AddSingleton<IAiChatService>(sp => sp.GetRequiredService<AiChatGateway>());
            services.AddSingleton<IAiStreamingChatService>(sp => sp.GetRequiredService<AiChatGateway>());

            services.AddSingleton<IAiSensitiveDataRedactor, AiSensitiveDataRedactor>();
            services.AddSingleton<AiStructuredOutputParser>();
            services.AddSingleton<IAiModelManagementService, OllamaModelManagementService>();

            services.AddSingleton<IAiOptimizationAdvisorService, AiOptimizationAdvisorService>();
            services.AddSingleton<IAiSoftwareContextService, StaticAiSoftwareContextService>();
            services.AddSingleton<IAiLocalDocumentSearchService, AiLocalDocumentSearchService>();
            services.AddSingleton<IAiTextEmbeddingService, AiDeterministicHashEmbeddingService>();
            services.AddSingleton<IAiWorkflowKnowledgeProjector, AiWorkflowKnowledgeProjector>();
            services.AddSingleton<IAiHybridKnowledgeRetriever, AiHybridKnowledgeRetriever>();
            services.AddSingleton<IAiProjectGroundingService, AiProjectGroundingService>();
            services.AddSingleton<IAiSoftwareInventoryService, AiSoftwareInventoryService>();
            services.AddSingleton<IAiPromptTemplateService, AiPromptTemplateService>();
            services.AddSingleton<IAiReportAppendixService, AiReportAppendixService>();
            services.AddSingleton<IWorkflowReadinessService, WorkflowReadinessService>();
            services.AddSingleton<IUiInteractionContextService, UiInteractionContextService>();

            services.AddSingleton<IAiProjectTool, GetProjectOverviewTool>();
            services.AddSingleton<IAiProjectTool, GetBimReadinessTool>();
            services.AddSingleton<IAiProjectTool, GetZoneSummaryTool>();
            services.AddSingleton<IAiProjectTool, GetCraneSetupTool>();
            services.AddSingleton<IAiProjectTool, GetGaReadinessTool>();
            services.AddSingleton<IAiProjectTool, GetCriticalIssuesTool>();
            services.AddSingleton<IAiProjectTool, SearchProjectEvidenceTool>();
            services.AddSingleton<IAiToolRegistry, AiToolRegistry>();
            services.AddSingleton<IAiAgentApprovalPolicy, DefaultAiAgentApprovalPolicy>();
            services.AddSingleton<IAiAgentAuditStore, InMemoryAiAgentAuditStore>();
            services.AddSingleton<IProjectReadinessAgentService, ProjectReadinessAgentService>();

            services.AddSingleton<IAiAgentActionTool, OpenCraneSetupActionTool>();
            services.AddSingleton<IAiAgentActionTool, OpenLoadChartImportActionTool>();
            services.AddSingleton<IAiAgentActionTool, OpenElementExportActionTool>();
            services.AddSingleton<IAiAgentActionTool, OpenElementManagementActionTool>();
            services.AddSingleton<IAiAgentActionTool, OpenZoneManagerActionTool>();
            services.AddSingleton<IAiAgentActionTool, OpenGaReadinessActionTool>();
            services.AddSingleton<IAiAgentActionToolRegistry, AiAgentActionToolRegistry>();
            services.AddSingleton<IAiAgentActionAuditStore, InMemoryAiAgentActionAuditStore>();
            services.AddSingleton<IAiAgentActionService, AiAgentActionService>();

            services.AddSingleton<AiAdvisorViewModel>();
            services.AddTransient<AiCompanionViewModel>();
            services.AddSingleton<IAiAdvisorWindowService, AiAdvisorWindowService>();

            return services;
        }
    }
}
