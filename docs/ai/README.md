# AI Assist and AI Studio Documentation

This folder contains the maintained documentation for the local-first AI assistance layer.

## Maintained documents

| Document | Purpose |
|---|---|
| [AI Branch Engineering Dossier](./AI_COPILOT_BRANCH_ENGINEERING_DOSSIER.md) | Canonical engineering record for the branch. Describes purpose, architecture, implementation scope, subsystem impact, validation, safety model, reproducibility, and maintenance guidance. |
| [AI Assist and AI Studio User Guide](./AI_COPILOT_USER_GUIDE.md) | User-facing guide for the docked AI Assist panel, Studio actions, and the local Ollama-powered AI Optimization Studio. |

The filenames remain stable for existing links, but the current product architecture is **AI Assist → Studio**, not a top-level AI Copilot ribbon tab.

## Documentation policy

The engineering dossier is the source of truth for this branch.

Incremental implementation notes, polish-pass notes, and temporary change reports are intentionally not kept in this folder. Their useful content has been consolidated into the canonical dossier so future contributors have a compact and accurate documentation set.

## Feature summary

The AI layer adds local-first advisory assistance to the crane-planning workflow.

The implementation includes:

- local Ollama integration;
- deterministic workflow and optimization advisory logic;
- docked AI Assist panel with Chat, Mission, BIM, GA, Report, and Studio sections;
- full AI Optimization Studio window;
- Markdown response preview;
- streaming and cancellation support;
- prompt templates and local documentation context;
- workflow-readiness checks;
- safe, confirmable action proposals;
- legacy navigation redirection from the removed AI Copilot tab to AI Assist → Studio;
- engineering-grade branch documentation.

The AI feature is advisory only. It does not automatically mutate IFC data, crane data, zones, load charts, optimization results, reports, or project state without explicit user action.
