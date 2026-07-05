# AI Copilot Documentation

This folder contains the maintained documentation for the local-first AI Copilot feature.

## Maintained documents

| Document | Purpose |
|---|---|
| [AI Copilot Branch Engineering Dossier](./AI_COPILOT_BRANCH_ENGINEERING_DOSSIER.md) | Canonical engineering record for the branch. Describes purpose, architecture, implementation scope, subsystem impact, validation, safety model, reproducibility, and maintenance guidance. |
| [AI Copilot User Guide](./AI_COPILOT_USER_GUIDE.md) | User-facing guide for running and using the local Ollama-powered AI Copilot and docked AI Assist panel. |

## Documentation policy

The engineering dossier is the source of truth for this branch.

Incremental implementation notes, polish-pass notes, and temporary change reports are intentionally not kept in this folder. Their useful content has been consolidated into the canonical dossier so future contributors have a compact and accurate documentation set.

## Feature summary

The AI Copilot feature adds local-first advisory assistance to the crane planning workflow.

The implementation includes:

- Local Ollama integration.
- Deterministic workflow and optimization advisory logic.
- Docked AI Assist panel.
- Full AI Copilot window.
- Markdown response preview.
- Streaming and cancellation support.
- Prompt templates and local documentation context.
- Workflow readiness checks.
- Safe, confirmable action proposals.
- Engineering-grade branch documentation.

The AI feature is advisory only. It does not automatically mutate IFC data, crane data, zones, load charts, optimization results, reports, or project state without explicit user action.

| [AI Command Center UX](./AI_COMMAND_CENTER_UX.md) | Describes the tabbed, pinned-control redesign of the docked AI Assist panel. |

| [AI Assist Sleek Section Navigator](./AI_ASSIST_SLEEK_SECTION_NAVIGATOR.md) | Describes the minimal vertical section navigator that replaces the button-heavy command center layout while preserving all AI features. |
| [AI Assist Section Rail UX](./AI_ASSIST_SECTION_RAIL_UX.md) | Describes the full-height text-based rail that replaces a visible scrollbar/accordion with focused professional AI sections. |
