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
