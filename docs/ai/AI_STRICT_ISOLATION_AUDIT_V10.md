# AI Strict Isolation Audit V10

## Verdict

V10 is the strict-isolation version of the AI Companion patch.

The AI behavior remains centralized in `CraneOptimizer.AI`. Non-AI changes are limited to thin UI host adapters and AI regression tests/documentation.

## Source boundary

### AI-owned behavior

The following responsibilities live in `CraneOptimizer.AI`:

- AI Companion ViewModel state;
- AI Companion views and active rail styling;
- composer focus/scroll coordination;
- deterministic BIM question routing;
- false stale-context interpretation;
- mission status and recommendation wording;
- AI host fact-key contract;
- UI interaction host contract.

### Narrow UI host integration

The following responsibilities remain in `CraneOptimizer.UI` because the AI project must not directly depend on WPF shell view models or project scene state:

- publish active ribbon tab;
- provide current scene/model counts;
- provide table-visible/table-total counts;
- provide IFC readiness facts;
- provide GA blocker facts;
- pass all such facts through AI-owned host keys.

### No source changes in lower layers

This strict pass does not require source changes in:

- `CraneOptimizer.Application`
- `CraneOptimizer.Domain`
- `CraneOptimizer.Infrastructure`

Those layers continue to own their existing business/domain/data-access responsibilities.

## Allowed non-AI files

| File | Classification | Justification |
|---|---|---|
| `CraneOptimizer.UI/AIIntegration/AiHostContextBridge.cs` | Host adapter | Captures source-of-truth shell/model snapshot values for AI. |
| `CraneOptimizer.UI/AIIntegration/Contributors/IfcAiContextContributor.cs` | Host adapter | Exposes existing IFC/readiness facts without moving readiness logic into AI. |
| `CraneOptimizer.UI/AIIntegration/Contributors/GaAiContextContributor.cs` | Host adapter | Exposes existing GA/readiness facts without moving optimizer logic into AI. |
| `CraneOptimizer.UI/Shared/Layouts/TopNavigation.xaml.cs` | Host adapter | Publishes active-screen state to the AI contract. |
| `CraneOptimizer.Application.Tests/AI/*` | Tests | Validates AI behavior and architecture boundaries. |
| `docs/ai/*`, `PATCH_NOTES_*` | Documentation | Describes implementation and review scope. |

## Why this follows the senior architecture

The senior architecture separates PL/View, ViewModel/API boundary, Services/BLL, Repository/Interfaces, Entities/contexts, and DAL.

V10 follows that separation:

1. **PL/View** renders AI panel and rail state.
2. **ViewModel/API boundary** controls selected AI section and prompt flow.
3. **AI services** interpret AI facts and produce deterministic guidance.
4. **UI host adapters** translate live UI/application state into AI snapshots.
5. **Business/domain/infrastructure layers** are not repurposed for AI presentation logic.

The result is a strict, reviewable boundary: AI owns AI behavior; the host supplies facts.
