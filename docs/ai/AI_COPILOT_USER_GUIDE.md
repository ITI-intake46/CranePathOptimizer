# AI Copilot User Guide

## Purpose

The AI Copilot is a local, optional assistant for the Crane Optimizer WPF application. It helps users understand the active screen, workflow readiness, optimization results, deterministic advisor findings, and report preparation steps. It does **not** replace the Genetic Algorithm, load-chart validation, crane engineering judgment, or qualified safety review.

## Entry Points

- **AI COPILOT ribbon tab**: opens the full AI Optimization Copilot window.
- **Bottom status-bar AI Copilot strip**: provides non-intrusive workflow hints and an **Assist** button.
- **Right dock → AI ASSIST**: shows the contextual companion beside the existing Properties dock.
- **AI ASSIST close button**: returns the right dock to **PROPERTIES** without disabling the bottom AI strip.
- **Status-bar hide button**: hides the bottom AI strip and shows a small restore button.

## Local Model Setup

1. Install and start Ollama.
2. Confirm a model is available, for example `llama3.2:latest`.
3. Open **AI COPILOT → Open Copilot**.
4. Click **Check Ollama**.
5. Select an installed model.
6. Click **Ask Ollama**.

The deterministic advisor works even when Ollama is unavailable.

## Workflow Readiness

The AI Assist panel evaluates the current project state using deterministic checks:

- project workspace availability
- IFC element availability
- planning zone availability
- crane setup availability
- load-chart/capacity evidence
- optimization result availability
- diagnostics availability
- report readiness

The readiness checklist is used as evidence for AI guidance and is visible in the **Readiness checklist and action cards** expander.

## Response Controls

The full Copilot window includes:

- **Ask Ollama**: sends the current question and application context to the local model.
- **Stop**: cancels a long-running local model request.
- **Copy Answer**: copies the current Markdown response.
- **Export Answer**: writes the answer to a Markdown file.
- **Context Inspector**: exposes raw Markdown, JSON context, software map, local documentation snippets, and model benchmarks.
- **History**: stores recent responses for the current app session.
- **Benchmark**: tests installed models and records response time/availability.

## Safety Boundaries

The AI Copilot is advisory only:

- It does not silently edit project data.
- It does not run optimization automatically.
- It does not certify lift plans.
- It does not override deterministic calculations.
- It should be used with qualified engineering review and manufacturer load-chart verification.
