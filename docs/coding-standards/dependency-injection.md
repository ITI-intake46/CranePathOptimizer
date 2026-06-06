# Coding Standards: Dependency Injection

The application uses the standard **Microsoft.Extensions.DependencyInjection** library to implement inversion of control.

All registrations occur in the `OnStartup` method of [App.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs).

---

## 1. Service Lifetimes

Choose the correct lifetime based on the component's state and purpose:

### Singletons (`AddSingleton`)
Used for stateless services, caching repositories, or application-wide coordinators:
*   **IFC Processing & Auditing:** `IIfcModelSessionService` ([IfcModelSessionService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcModelSessionService.cs)), `IIfcReaderRepository` ([IfcReaderRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs)), `IIfcDiagnosticAuditService` ([IfcDiagnosticAuditService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs)), `IIfcExporterProfileResolver` ([IfcExporterProfileResolver.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcExporterProfileResolver.cs)), `IIfcNormalizationDecisionService` ([IfcNormalizationDecisionService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/IfcNormalizationDecisionService.cs)).
*   **Scene & Rendering:** `ElementVisualBuilder`, `CraneVisualBuilder`, `IRenderingService`, `ISceneRenderingManager`, `IViewportHitTestService`, `IViewportInteractionService`.
*   **Project State:** `IProjectWorkspaceService`, `IProjectStateService`, `IProjectShellService`, `IProjectNavigationService`.
*   **Mixed ViewModels:** `IfcViewerViewModel`, `CraneManagerViewModel`, `DxfTabViewModel`, `CoordinateReferenceViewModel`, `SelectionHandler`, `HoverHitCache`.

### Scoped (`AddScoped`)
Used for components that carry state specific to an optimization run or db operation:
*   **DbContext & Repositories:** `CraneDbContext`, `ICraneRepository` ([CraneRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/CraneRepository.cs)), `IProjectRepository` ([ProjectRepository.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Repositories/ProjectRepository.cs)).
*   **Optimization Engines:** `IGeneticAlgorithmEngine`, `ICraneConfigurationEngine`, `ILiftingCalculationEngine`, `ILoadChartCapacityResolver`, `IConstraintEngine`, `ICoverageEngine`, `IGAInitializer`, `IElementAssignmentService`, `IFitnessEvaluator`, `IGACsvDiagnosticsService`.
*   **Mixed ViewModels:** `ElementManagementViewModel` (tied to selected element session).

### Transients (`AddTransient`)
Used for lightweight views and transient viewmodels resolved dynamically:
*   **Application Services:** `IProjectService` ([ProjectService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectService.cs)).
*   **Views & Window VMs:** `MainViewModel` (resolved fresh per session), `SelectionViewModel`, `MainWindow`, `ProjectManagementWindow`, `ProjectManagementViewModel`.

---

## 2. Database Context Injection

To ensure thread safety across concurrent background solvers (such as GA threads querying specifications):
*   **Scoped DbContext:** `CraneDbContext` is registered via `AddDbContext<CraneDbContext>`.
*   **DbContext Factory:** Registered via `AddDbContextFactory<CraneDbContext>()`. This allows background worker tasks to safely spin up isolated connections via `dbFactory.CreateDbContextAsync()` to prevent SQLite threading lockouts.

### Implementation Reference

File:
[App.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs)

Method:
`OnStartup()`
