# AI Assist and AI Studio User Guide

## Purpose

AI Assist is the local, optional intelligence layer for the Crane Optimizer WPF application. It helps users understand the active screen, workflow readiness, optimization results, deterministic advisor findings, and report preparation steps. The larger AI Optimization Studio provides model management, local Ollama chat, grounding, history, and audit tools for deeper work.

AI remains advisory. It does **not** replace the Genetic Algorithm, load-chart validation, crane engineering judgment, or qualified safety review.

## Entry Points

- **Bottom status-bar AI strip**: provides non-intrusive workflow hints and an **Assist** button.
- **Right dock → AI ASSIST**: opens the contextual assistant beside the existing Crane Properties dock.
- **AI ASSIST → STUDIO**: contains the advanced AI actions formerly exposed in a separate ribbon tab.
- **AI ASSIST close button**: returns the right dock to **CRANE PROPERTIES** without disabling the bottom AI strip.
- **Status-bar hide button**: hides the bottom AI strip and shows a small restore button.

The main ribbon intentionally ends at **REPORTS**. AI is a cross-workflow assistance layer rather than a separate crane-planning phase.

## Studio Actions

Open **AI ASSIST → STUDIO** to access:

- **Open Full Studio**: opens the complete AI Optimization Studio window.
- **Analyze Current Result**: runs deterministic advisor rules against the current optimization result. This works without Ollama.
- **Ask Local Ollama**: opens Chat and focuses the private prompt composer for the selected local model.

Legacy internal navigation requests that target the former AI Copilot workspace are redirected safely to **AI ASSIST → STUDIO**.

## Local Model Setup

1. Install and start Ollama.
2. Confirm a model is available, for example `llama3.2:latest`.
3. Open **AI ASSIST → STUDIO → Open Full Studio**.
4. Click **Check Ollama**.
5. Select an installed model.
6. Enter a question and click **Send**.

The deterministic advisor works even when Ollama is unavailable.

## Workflow Readiness

AI Assist evaluates the current project state using deterministic checks:

- project workspace availability;
- IFC element availability;
- planning-zone availability;
- crane setup availability;
- load-chart/capacity evidence;
- optimization-result availability;
- diagnostics availability;
- report readiness.

Mission, BIM, GA, Report, and Studio sections expose the relevant evidence without forcing users to leave the current engineering screen.

## AI Optimization Studio Controls

The full Studio window includes:

- **Send / Stop**: submits or cancels the current local-model request.
- **Refresh**: refreshes deterministic result and workflow context.
- **Copy Context**: copies the exact structured context used by AI.
- **Export MD**: writes the current answer to a Markdown file.
- **Report Appendix**: exports the current answer with safety disclaimer and context snapshot.
- **Context Inspector**: exposes raw Markdown, JSON context, software map, local documentation snippets, and model benchmarks.
- **History**: stores recent responses for the current application session.
- **Benchmark**: tests installed models and records response time and availability.

## Safety Boundaries

AI Assist and AI Studio are advisory only:

- They do not silently edit project data.
- They do not run optimization automatically.
- They do not certify lift plans.
- They do not override deterministic calculations.
- Their output must be reviewed against manufacturer load charts, project requirements, site rules, and qualified engineering judgment.
