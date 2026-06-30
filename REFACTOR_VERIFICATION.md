# AI Isolation Refactor Verification

Suggested branch name: `refactor/ai-isolated-project`

## What changed

- Added `CraneOptimizer.AI` as a WPF-capable class-library project.
- Moved AI DTOs, interfaces, services, Ollama provider, settings, local documentation/search, prompt/report support, AI views, AI view models, companion panel, rendering, and workflow-readiness logic into `CraneOptimizer.AI`.
- Removed previous AI source folders from:
  - `CraneOptimizer.Application/AI`
  - `CraneOptimizer.Infrastructure/AI`
  - `CraneOptimizer.UI/Features/F13_AiAdvisor`
- Added `CraneOptimizer.UI/AIIntegration/AiHostContextBridge.cs` as the thin host adapter that maps live UI/GA state into AI DTOs.
- Updated `App.xaml.cs`, `MainWindow.xaml`, `TopNavigation.xaml`, solution entries, project references, and AI-related tests.

## Intended dependency direction

```text
CraneOptimizer.Domain
CraneOptimizer.Application   -> CraneOptimizer.Domain
CraneOptimizer.Infrastructure -> CraneOptimizer.Application, CraneOptimizer.Domain
CraneOptimizer.AI            -> no project references to UI/Application/Infrastructure/Domain
CraneOptimizer.UI            -> CraneOptimizer.AI, CraneOptimizer.Application, CraneOptimizer.Infrastructure
```

## Static checks performed in this packaging environment

Passed:

- Solution contains 7 projects, including `CraneOptimizer.AI`.
- `.csproj` files parse as valid XML.
- `CraneOptimizer.AI` XAML `x:Class` values match their code-behind namespaces/classes.
- No active source references remain to the old AI namespaces or folders:
  - `CraneOptimizer.Application.AI`
  - `CraneOptimizer.Infrastructure.AI`
  - `CraneOptimizer.UI.Features.F13_AiAdvisor`
- `CraneOptimizer.AI` has no `using CraneOptimizer.UI...` references.
- `CraneOptimizer.Application`, `CraneOptimizer.Infrastructure`, `CraneOptimizer.Domain`, and `CraneOptimizer.GAExperiment` do not reference `CraneOptimizer.AI`.

## Build/test note

The packaging environment did not include the .NET SDK, so `dotnet restore`, `dotnet build`, and `dotnet test` could not be executed here. Please run the following on a Windows development machine with the .NET SDK installed:

```bash
dotnet restore CraneOptimizer.sln
dotnet build CraneOptimizer.sln
dotnet test CraneOptimizer.sln
```
