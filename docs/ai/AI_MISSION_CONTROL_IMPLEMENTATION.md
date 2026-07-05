# AI Mission Control implementation

This patch turns the existing local AI companion from a passive chat surface into a visible AI-powered crane-planning assistant.

## Implemented surfaces

- **AI Mission Control diagnosis**: visible root cause, evidence, recommended next action, confidence, and risk at the top of the docked AI drawer.
- **AI intelligence cards**: IFC Intelligence, Weight Trust Intelligence, GA Feasibility Intelligence, Crane Capacity Intelligence, and Report Readiness Intelligence.
- **AI recovery plan**: deterministic checklist for blocker classification, evidence gathering, owner assignment, verification, and safety boundary.
- **AI what-if advisor**: ranks the next investigation lever before users waste time changing crane, zone, or load-chart settings.
- **AI report language**: copies reviewer-required narrative text for blocked, partial, or successful optimization states.
- **QA evidence pack**: copies readiness checklist, blocker summary, recovery plan, and what-if advisor together.
- **BIM/exporter request**: copies the explicit mass/weight, assembly mark, source ID, and COG evidence request needed to unblock GA candidates.

## Safety boundary

Mission Control is deterministic and advisory. It never changes optimizer gates, project data, engineering quantities, crane parameters, zones, reports, or load charts automatically. It is designed to make the AI value obvious while preserving the existing strict blocker policy:

- do not bypass GA eligibility;
- do not use estimated/fallback weights for engineering optimization;
- do not certify crane capacity, rigging, site safety, or compliance;
- require reviewer approval before report language is issued.

## Main files

- `CraneOptimizer.AI/Workflow/AiMissionControlService.cs`
- `CraneOptimizer.AI/Workflow/AiMissionControlSnapshot.cs`
- `CraneOptimizer.AI/Workflow/AiMissionControlInsight.cs`
- `CraneOptimizer.AI/Services/AiWorkflowEvidenceTextBuilder.cs`
- `CraneOptimizer.AI/Companion/ViewModels/AiCompanionViewModel.cs`
- `CraneOptimizer.AI/Companion/Views/AiCompanionPanel.xaml`
- `CraneOptimizer.AI/Companion/Views/AiCompanionStatusStrip.xaml`
- `CraneOptimizer.Application.Tests/AI/AiMissionControlFeatureTests.cs`

## Accuracy hardening patch

The screenshot-review hardening pass adds a shared deterministic workflow-state classifier so every AI surface uses the same state order before recommending actions:

1. no project;
2. no IFC/model elements loaded;
3. IFC loaded but trusted lift-unit/GA candidate readiness blocked;
4. missing zones;
5. missing crane setup;
6. missing load-chart evidence;
7. ready for controlled optimization;
8. result review/report candidate.

Important behavior changes:

- Mission Control no longer says "IFC is loaded" when the live element count is zero.
- Zero-candidate blocker guidance is only strict for workflow/GA/weight/next-step questions.
- IFC metadata questions, such as building type or building usage, are answered directly and only receive the GA blocker as a separate caveat.
- The full AI Copilot result panel no longer tells users to run GA when the shared state classifier says a prerequisite is blocking the run.
- Generated recovery, what-if, report, and QA text avoid contradictory "Run Optimizer" advice while zero trusted candidates or other blockers are present.
- IFC metadata intelligence now exposes building-name, top IFC-class, top resolved-category, and explicit unknown building usage/type status where available from context.
- User-facing prompts suppress implementation/meta terms such as feature flags, XAML inventories, prompt templates, and Mission Control implementation details unless the user explicitly asks for implementation information.
