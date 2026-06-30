# AI Isolation Refactor

This branch isolates the local AI/Ollama copilot into a dedicated WPF-capable class-library project:

- `CraneOptimizer.AI`

The previous AI implementation areas were removed from:

- `CraneOptimizer.Application/AI`
- `CraneOptimizer.Infrastructure/AI`
- `CraneOptimizer.UI/Features/F13_AiAdvisor`

## Current dependency direction

```text
CraneOptimizer.Domain
CraneOptimizer.Application  -> CraneOptimizer.Domain
CraneOptimizer.Infrastructure -> CraneOptimizer.Application, CraneOptimizer.Domain
CraneOptimizer.AI           -> no CraneOptimizer.UI dependency
CraneOptimizer.UI           -> CraneOptimizer.AI, CraneOptimizer.Application, CraneOptimizer.Infrastructure
```

## Host integration

`CraneOptimizer.UI/AIIntegration/AiHostContextBridge.cs` is the thin adapter that translates live UI/shell/GA state into AI DTOs. The isolated AI project reads host state only through:

- `IAiOptimizationContextProvider`
- `IAiWorkflowContextProvider`

This prevents the AI project from depending on `MainViewModel`, `CranePlacementViewModel`, scene services, or UI feature folders.

## Service registration

AI services are registered through:

```csharp
services.AddCraneOptimizerAi();
```

The UI host then registers the adapter bridge and assigns the WPF service locator used by AI controls created from XAML:

```csharp
services.AddSingleton<AiHostContextBridge>();
services.AddSingleton<IAiOptimizationContextProvider>(sp => sp.GetRequiredService<AiHostContextBridge>());
services.AddSingleton<IAiWorkflowContextProvider>(sp => sp.GetRequiredService<AiHostContextBridge>());
AiServiceLocator.ServiceProvider = ServiceProvider;
```

## Verification target

Run on a Windows machine with the .NET SDK installed:

```bash
dotnet restore CraneOptimizer.sln
dotnet build CraneOptimizer.sln
dotnet test CraneOptimizer.sln
```
