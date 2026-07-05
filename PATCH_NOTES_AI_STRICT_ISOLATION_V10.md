# AI Strict Isolation Compliance V10

## Summary

This patch tightens the AI isolation boundary without removing the AI Companion features delivered in the previous passes.

The feature set remains intact:

- deterministic BIM count answers for slabs, beams, totals, thresholds, and category summaries;
- false stale-context reduction for scene/readiness/table/reference-geometry count scopes;
- send-time context capture and active-screen consistency;
- composer focus and chat scroll restoration;
- pinned mission status instead of chronological readiness spam;
- BIM, Mission, GA, Report, and Studio AI sections;
- right-rail active section highlight.

## Senior-rule compliance

The senior rule was applied as a strict design boundary:

> Most AI work lives in `CraneOptimizer.AI`. Other projects are touched only for narrow host integration, source-of-truth facts, or tests.

V10 strengthens that boundary by moving the host-facing UI interaction contract into the AI host contract area:

- added/kept: `CraneOptimizer.AI/Host/IUiInteractionContextService.cs`
- removed from service implementation area: `CraneOptimizer.AI/Services/IUiInteractionContextService.cs`

This makes the separation clearer:

- `CraneOptimizer.AI/Host/*` contains contracts that the desktop host may consume.
- `CraneOptimizer.AI/Services/*` contains AI implementation services.
- `CraneOptimizer.UI/*` consumes host contracts and contributes raw source-of-truth facts.

## Non-AI files intentionally touched

Only narrow UI host-adapter files are changed outside the AI project:

| File | Reason |
|---|---|
| `CraneOptimizer.UI/AIIntegration/AiHostContextBridge.cs` | Publishes model revision/fingerprint and count-scope facts through AI-owned keys. |
| `CraneOptimizer.UI/AIIntegration/Contributors/IfcAiContextContributor.cs` | Supplies raw IFC/readiness/category/count facts from the existing application services. |
| `CraneOptimizer.UI/AIIntegration/Contributors/GaAiContextContributor.cs` | Supplies raw GA/readiness blocker facts from the existing application services. |
| `CraneOptimizer.UI/Shared/Layouts/TopNavigation.xaml.cs` | Publishes the current active ribbon tab to the AI host contract so send-time context is accurate. |

No `CraneOptimizer.Application`, `CraneOptimizer.Domain`, or `CraneOptimizer.Infrastructure` source files are changed by this strict-isolation pass.

## Architecture preservation

The layered/MVVM architecture is preserved:

- **View/XAML** owns visual presentation, including the active right-rail highlight.
- **ViewModel** owns selected AI section state and composer behavior.
- **AI services** own deterministic routing, answer wording, stale/freshness interpretation, and mission guidance.
- **UI host adapters** only collect live app facts and pass them through AI-owned contracts.
- **Application/Domain/Infrastructure** remain responsible for existing business, domain, and data-access behavior.

The AI Companion consumes snapshots/contracts instead of directly owning repositories, IFC parsing, optimizer behavior, or domain entities.

## Regression coverage added/updated

`CraneOptimizer.Application.Tests/AI/AiProjectIsolationBoundaryTests.cs` now checks that:

- the AI project has no project references to host/domain layers;
- AI source does not import UI/Application/Infrastructure/Domain namespaces;
- AI fact keys live in the AI project;
- UI host adapters reference `CraneOptimizer.AI.Host` contracts instead of AI service implementation namespaces;
- `IUiInteractionContextService` lives in `CraneOptimizer.AI/Host`.

