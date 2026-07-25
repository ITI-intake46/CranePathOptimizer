# AI Assist and AI Studio Documentation

This folder contains maintained documentation for the deterministic, local-first, and BYOK cloud AI assistance layer.

## Maintained documents

| Document | Purpose |
|---|---|
| [AI Branch Engineering Dossier](./AI_COPILOT_BRANCH_ENGINEERING_DOSSIER.md) | Historical engineering record for the earlier local-only branch baseline. |
| [AI Assist and AI Studio User Guide](./AI_COPILOT_USER_GUIDE.md) | Current user guide for the docked assistant, provider configuration, secure BYOK credentials, privacy, and Studio controls. |

The current product architecture is **AI Assist → Studio**, not a top-level AI Copilot ribbon tab.

## Current feature summary

The AI layer includes:

- deterministic workflow and optimization advice that works without a model;
- local Ollama chat and model management;
- OpenRouter, OpenAI, Azure OpenAI, and custom OpenAI-compatible BYOK profiles;
- Windows Credential Manager secret storage;
- explicit cloud selection and per-provider project-context consent;
- no automatic local/cloud fallback;
- streaming, cancellation, model discovery, manual model IDs, and connection testing;
- docked Chat, Mission, BIM, GA, Report, and Studio sections;
- project RAG, prompt templates, history, context inspection, and governed actions.

The AI feature is advisory only. It does not automatically mutate IFC data, crane data, zones, load charts, optimization results, reports, or project state.
