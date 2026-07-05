# AI Makeover Showcase

This patch turns the existing AI layer from a small assistant into a visible product feature. The design goal is to make AI obvious to evaluators before they type a question.

## What changed

The docked AI Companion now includes an **AI Studio Showcase** surface:

- live AI hero headline based on workflow state;
- capability stack summary;
- visible AI capability cards;
- one-click demo prompt chips;
- explainable AI decision trace;
- copyable AI makeover presentation brief.

The implementation is deterministic and safe. It reads the same workflow evidence used by Mission Control and never changes crane data, optimizer eligibility, load-chart state, or reports.

## New AI capabilities surfaced

The AI Studio Showcase presents these product-facing capabilities:

1. Readiness Sentinel
2. BIM / IFC Intelligence
3. Lift-Unit Trust Agent
4. Optimization Failure Explainer, when a GA result exists
5. What-If Advisor
6. Report Narrative Generator
7. Local RAG + Software Navigator
8. ModelOps Benchmarking

## New files

- `CraneOptimizer.AI/Workflow/AiShowcaseService.cs`
- `CraneOptimizer.AI/Workflow/AiShowcaseSnapshot.cs`
- `CraneOptimizer.AI/Workflow/AiShowcaseCapability.cs`
- `CraneOptimizer.AI/Workflow/AiShowcasePrompt.cs`

## Modified surfaces

- `AiCompanionPanel.xaml` now shows AI Studio Showcase above the existing intelligence cards.
- `AiCompanionViewModel.cs` exposes showcase bindings and copy action.
- `AiAdvisorTabView.xaml` now frames the tab as AI Studio / Mission Control / RAG / ModelOps.
- `StaticAiSoftwareContextService.cs` documents the new visible AI surface for RAG/software-map answers.
- `AiMissionControlFeatureTests.cs` verifies showcase artifacts and UI contract strings.

## Safety boundary

The showcase is intentionally advisory. AI can explain, rank, draft, copy evidence, generate prompt-ready text, and propose confirmed navigation. Deterministic services remain authoritative for IFC import, trusted-weight eligibility, GA execution, capacity checks, load-chart evidence, and report validity.


## Grounding hardening update

The AI Studio showcase now includes deterministic BIM Q&A routing. Direct live-model questions such as `How many slabs are there?` are answered from imported model/category facts before Ollama or local documentation retrieval is used. The side dock also surfaces fast action chips for `Count slabs` and `BIM counts`, and GA candidate evidence is labeled as trusted lift-unit readiness rather than ambiguous raw candidate counts.
