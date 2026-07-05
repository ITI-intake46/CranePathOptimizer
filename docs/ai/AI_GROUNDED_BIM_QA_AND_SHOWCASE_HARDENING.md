# AI Grounded BIM Q&A + Showcase Hardening

This patch turns the AI makeover from a visible showcase into a more accurate, live-project-aware copilot. The goal is simple: when the user asks a factual BIM question, AI must answer from deterministic project evidence before Ollama, local docs, or RAG can produce generic software prose.

## Delivered behavior

### 1. Deterministic BIM quantity answers

The side-dock and full Copilot now route live BIM count questions through `AiBimProjectQuestionService` before using the LLM.

Handled examples:

- `How many slabs are there?`
- `How many beams are there?`
- `How many columns are there?`
- `How many walls are there?`
- `How many unknown elements are there?`
- `Show the BIM category breakdown.`
- `How many trusted lift units are there?`
- `How many elements are over 3000 kg?`

Expected answer shape:

```text
## Answer
There are 97 elements categorized as Slab in the current imported model context.

## Evidence
- Active screen: IFC MODEL
- Total imported elements: 2,483
- Source: resolved BIM category summary.

## Caveat
This is the app's resolved BIM category, not necessarily the raw IFC entity class.
```

This directly fixes the failure where `How many slabs are there?` was answered with generic documentation despite the live context already exposing `Slab (97)`.

### 2. RAG suppression for live model facts

Local documentation snippets and XAML/software inventory are intentionally suppressed for live BIM quantity questions. The priority order is now:

1. deterministic project facts,
2. IFC metadata/import-readiness context,
3. workflow blocker evidence,
4. Ollama wording only when deterministic routing does not apply,
5. local RAG/software docs only for documentation/software-navigation questions.

This prevents documentation content from overpowering the actual model.

### 3. Candidate label hardening

The UI and AI evidence now distinguish:

- `raw/import-screen candidate-like elements`,
- `trusted GA-ready lift units`,
- `deterministically blocked elements`,
- `estimated/fallback-weight elements blocked from GA`,
- `elements missing trusted weight evidence`.

This avoids the confusing visual combination of `GA candidates=232` and `zero trusted grouped lift-unit candidates`. Raw candidate-like counts are not presented as permission to run GA.

### 4. Readiness hierarchy and downstream blocker separation

When IFC is loaded but zones/cranes/load charts are still missing, AI now separates the immediate workflow step from downstream optimizer data blockers.

Example:

```text
Immediate workflow step: ZONES → Allowed Zone.
Known downstream optimizer data blocker: IFC MODEL → Export Element List / trusted weight-source review.
```

This prevents the readiness card and Mission Control from appearing to disagree.

### 5. Visible AI UX improvements

The AI side panel is widened and now surfaces compact fast actions near the top:

- `Next`
- `Count slabs`
- `BIM counts`
- `Blocker`
- `What-if`

Long capability, prompt, and intelligence sections are collapsed by default so the demo begins with a compact live state card instead of a wall of text.

### 6. Run Optimizer blocker feedback

The Cranes ribbon `Run Optimizer` command now uses a real `CanExecute` gate and tooltip. It is visibly blocked until the project has:

- IFC elements,
- at least one allowed zone,
- a placed crane,
- load-chart evidence on the placed crane,
- trusted GA-ready lift-unit readiness.

Attempting to run through the command path while blocked sets a status message with the blocker reason.

## Safety boundary

This patch does not relax optimization, import-readiness, load-chart, trusted-weight, COG, grouping, or report rules. AI can explain, count, summarize, and stage prompts; it cannot bypass deterministic engineering gates.
