# AI Assist Sleek Section Navigator

This patch converts the docked AI surface from a button-heavy command center into a minimal vertical section navigator.

## UX intent

The AI layer remains fully visible, but it is no longer presented as a dense cloud of equivalent buttons. The panel now behaves like a professional engineering assistant:

1. A compact state header shows workflow status, readiness progress, model revision, and live BIM/table counts.
2. A single primary recommendation is always visible and can open the next workflow tab when a safe navigation action exists.
3. A vertical section navigator exposes focused AI areas instead of horizontal tabs and pinned button rows.
4. Only the selected section expands; long evidence remains behind deliberate expanders.
5. The prompt composer remains fixed at the bottom so users can ask from any section without losing context.

## Sections

- **Chat** — conversation history with stale-model labels when the model context changes.
- **Mission Control** — readiness diagnosis, root cause, next action, blocker explanation, and readiness checklist.
- **BIM Intelligence** — deterministic BIM counts, category summaries, IFC metadata questions, model revision evidence, and BIM evidence cards.
- **Optimization Readiness** — GA blocker guidance, trusted lift-unit warnings, what-if ordering, and confirmable action proposals.
- **Report & QA** — report-safe language, QA evidence packs, and recovery-plan exports.
- **AI Studio** — capability showcase, local RAG/ModelOps story, and decision trace.

## Preserved behavior

The redesign is visual and navigational. It does not relax any AI grounding, freshness, deterministic-readiness, trusted-weight, GA, or report-safety guardrail.

The same commands remain available, but they now live where users expect them:

- count and metadata actions live in BIM Intelligence;
- blocker and next-step actions live in Mission Control / Optimization Readiness;
- report-copy actions live in Report & QA;
- showcase-copy actions live in AI Studio;
- refresh, full view, context copy, and answer copy remain in the compact footer.

## Why this is more professional

A narrow engineering side panel should not expose every action as a first-level button. The section navigator makes the AI feel curated, reduces scanning friction, and lets evaluators quickly understand the product as a mature AI assistant rather than a generic chatbot with many controls.


## Section rail refinement

The follow-up section-rail UX keeps this minimal philosophy but replaces the visible accordion-style section list with a full-height text rail at the right edge of the AI dock. The rail functions as a professional navigation spine while the selected section content occupies the main panel area.
