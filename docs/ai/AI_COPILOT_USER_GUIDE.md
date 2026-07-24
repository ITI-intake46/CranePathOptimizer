# AI Assist and AI Studio User Guide

## Purpose

AI Assist is the optional intelligence layer for the Crane Optimizer WPF application. It combines deterministic workflow checks with an explicitly selected generative provider. The larger AI Optimization Studio provides provider configuration, model selection, grounding, history, audit tools, and context inspection.

AI remains advisory. It does **not** replace the Genetic Algorithm, load-chart validation, crane engineering judgment, or qualified safety review.

## Entry Points

- **Bottom status-bar AI strip**: provides non-intrusive workflow hints and an **Assist** button.
- **Right dock → AI ASSIST**: opens the contextual assistant in the same slim dock used by Crane Properties.
- **AI ASSIST → STUDIO**: exposes advanced AI actions and opens the full Studio window.
- **AI ASSIST close button**: returns the right dock to **CRANE PROPERTIES** without disabling the bottom AI strip.

The main ribbon intentionally ends at **REPORTS**. AI is a cross-workflow assistance layer rather than a separate crane-planning phase.

## Docked AI Assist Layout

The docked surface uses a 30-pixel rotated section rail for **Chat**, **Mission**, **BIM**, **GA**, **Report**, and **Studio**. One authoritative section state drives both visible content and the persistent orange selected highlight. The panel shares the same 330–380 pixel right dock as **CRANE PROPERTIES**, preserving the BIM viewport.

New answers open at their beginning rather than forcing the conversation to the final line. Manual scroll position is preserved, **Latest** returns to the response tail on demand, and long agent reports show a concise summary before optional details.

## Provider Choices

Open **AI ASSIST → STUDIO → Open Full Studio** and choose a provider:

- **Local Ollama**: local inference; project context remains on the workstation.
- **OpenRouter**: BYOK cloud access to provider/model slugs through an OpenAI-compatible API.
- **OpenAI**: BYOK access to OpenAI chat-completion models.
- **Azure OpenAI**: BYOK access through an Azure `/openai/v1/` endpoint and an `api-key` header.
- **Custom OpenAI-compatible**: configurable base URL, chat path, models path, authentication mode, and non-secret headers.

The application never silently falls back from one provider to another. The provider shown in Studio is the provider used for the request.

## Secure BYOK Configuration

1. Choose the provider in the full Studio window.
2. Click **Configure**.
3. Verify the base URL, chat-completions path, and optional models path.
4. Enter an exact default model or deployment ID.
5. Enter your personal API key and click **Save and test**.
6. Click **Check** later to refresh connection status and discover model IDs where supported.

API keys are stored in **Windows Credential Manager**. The settings JSON stores only a credential target reference. Keys are never written to project databases, source control, copied context, response history, or operational telemetry.

Leaving the key field blank preserves the stored key. **Remove key** deletes it and clears prior cloud-consent state for that provider.

## OpenRouter Example

The built-in OpenRouter profile is preconfigured with:

```text
Base URL:    https://openrouter.ai/api/v1/
Chat path:   chat/completions
Models path: models
Auth:        Bearer
```

Click **Check** to retrieve available model IDs, then select or type a provider/model slug. The optional `HTTP-Referer` and `X-OpenRouter-Title` fields are available in the provider dialog. No specific free model is hard-coded because model availability can change.

## Custom Compatible Providers

The custom profile supports:

- HTTP or HTTPS base URL; HTTPS is required whenever an API key is configured;
- configurable chat-completions and model-list paths;
- Bearer, custom API-key-header, or no authentication;
- manual model entry when discovery is unavailable;
- streaming on/off;
- optional temperature omission for models that reject that parameter;
- optional non-secret headers entered as `Header=Value`.

`Authorization`, `api-key`, and `Content-Type` cannot be entered as additional headers because the application manages those safely.

## Cloud Privacy Boundary

Before the first grounded request to each cloud provider, AI Studio asks for explicit confirmation. The request may include selected readiness, BIM, crane, zone, GA, report, screen, retrieved-document, and conversation context. The IFC file and project database are not uploaded as files by this workflow.

The deterministic advisor, readiness agent, BIM inspection tools, and rule-based guidance remain available without Ollama or a cloud API.

## Studio Controls

- **Provider**: selects Local Ollama or a configured BYOK cloud profile.
- **Model**: selects a discovered model or accepts an exact manually entered ID.
- **Check**: validates the selected provider and discovers models where supported. Connection tests contain no project context.
- **Configure**: edits endpoint, authentication, key, optional headers, streaming, and model behavior.
- **Pull**: downloads a model only when Local Ollama is selected.
- **Benchmark**: sends a minimal context-free availability prompt to selected models.
- **Send / Stop**: submits or cancels the current request.
- **Refresh**: refreshes deterministic result and workflow context.
- **Copy Context**: copies the governed structured context used by AI.
- **Export MD / Report Appendix**: exports the current advisory answer with its context and disclaimer.

## Failure Messages

AI Studio distinguishes missing credentials, rejected credentials, unknown models/endpoints, insufficient quota, rate limiting, provider outages, timeouts, malformed responses, and interrupted streams. Provider errors are redacted by the governed gateway before they are surfaced.

## Safety Boundaries

AI Assist and AI Studio:

- do not silently edit project data;
- do not run optimization automatically;
- do not certify lift plans;
- do not override deterministic calculations;
- require explicit cloud-provider selection and consent;
- require review against manufacturer load charts, project requirements, site rules, and qualified engineering judgment.
