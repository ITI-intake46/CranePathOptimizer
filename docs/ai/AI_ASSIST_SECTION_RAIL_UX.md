# AI Assist Section Rail UX

This patch refines the AI Assist dock from an expandable-section list into a minimal professional section-rail layout.

## Design intent

The previous sleek navigator reduced button clutter, but still used stacked expandable sections plus a normal scrollbar. The new layout keeps the same AI capabilities and deterministic safety logic while replacing the visual scrollbar area with a persistent text-based navigation rail.

The rail behaves like a compact vertical tab strip:

- Chat
- Mission
- BIM
- GA
- Report
- Studio

The active rail item uses the same orange accent language as the main application ribbon tabs. This makes AI tools feel integrated into the engineering application rather than bolted on as a chatbot panel.

## UX structure

The dock now uses this hierarchy:

1. Fixed AI Assist header with workflow state, readiness, model revision, and BIM/table counts.
2. Fixed primary recommendation card with the one most important next action.
3. Focused section content area.
4. Full-height right-side section rail replacing the visual scrollbar/navigation list.
5. Fixed ask-current-model composer.
6. Fixed footer utilities: refresh, full Copilot, copy context, copy latest answer.

Only one section is shown at a time. Mouse-wheel scrolling still works inside long focused sections, but the rail is the primary navigation affordance.

## Preserved capabilities

All prior capabilities remain available:

- Deterministic BIM count Q&A.
- IFC metadata/building usage guidance.
- Mission Control diagnosis.
- Optimizer blocker explanation.
- What-if advisor.
- Confirmable navigation/copy proposals.
- Report narrative and QA evidence generation.
- AI Studio showcase, capability cards, demo prompts, and decision trace.
- Model freshness, revision, and stale-chat labeling safeguards.

The difference is presentation: actions are no longer shown as a top-level button cloud or accordion list. They are curated inside the focused rail section where they belong.

## Why this is more professional

A narrow engineering side panel should not force users to scan every AI feature at once. The section rail gives constant orientation, quick access, and a clean visual spine. It also makes the AI surface resemble professional CAD/BIM tools that use docked panels and navigation rails rather than consumer-style chat widgets.


## Slim section rail refinement

The later slim-rail patch reduces the section rail to a scrollbar-sized column with rotated labels and mouse-wheel section switching while preserving the focused-section behavior introduced here. See `AI_ASSIST_SLIM_SECTION_RAIL.md`.
