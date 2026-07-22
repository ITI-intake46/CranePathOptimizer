# Project RAG: Live Evidence Grounding

## Purpose

This milestone turns the live `AiWorkflowContextSnapshot` into a bounded, citation-addressable knowledge corpus. It is the first end-to-end RAG capability in Crane Optimizer and remains fully isolated inside `CraneOptimizer.AI`.

The feature answers project questions with evidence from the active BIM/crane/zone/GA/report workflow rather than relying on generic model knowledge.

## Pipeline

```text
AI-owned workflow snapshot
  → deterministic knowledge projection
  → bounded paragraph-aware chunking
  → hybrid lexical + hashed-semantic retrieval
  → authority and metadata boosting
  → ranked evidence IDs [E1]...[En]
  → grounded prompt packet
  → deterministic or Ollama answer
  → visible evidence cards and citation trace
```

## Safety and isolation

- The RAG corpus is built only from AI-owned DTOs in `CraneOptimizer.AI.Host`.
- No Domain, Application, Infrastructure, or UI assembly is referenced by the AI project.
- Retrieval never changes project state.
- Workflow issues and action hints remain deterministic host evidence.
- The LLM is instructed not to bypass weight, load-chart, crane, or GA-readiness gates.
- Answers append a visible evidence trace when project evidence is retrieved.
- Model revision and fingerprint are captured so stale answers can be recognized.

## Retrieval model

The first implementation is dependency-free and offline-safe:

- weighted title/source/body lexical matching;
- query expansion for crane-planning concepts;
- deterministic 384-dimensional hashed semantic vectors;
- cosine similarity;
- source-authority boosts;
- one best chunk per source document;
- bounded top-k evidence.

This baseline remains available even when Ollama is stopped. A later implementation can add Ollama/cloud embedding providers behind a provider-neutral embedding interface and compare them against this deterministic evaluation baseline.

## Visible surfaces

- AI Assist CHAT displays an expanded grounding-evidence list after a question.
- Full Copilot includes a **RAG Grounding** audit tab.
- Context Cards show corpus size, retrieval coverage, and the top citations.
- Deterministic and model-generated project answers include a Markdown grounding trace.

## Initial acceptance scenario

Question:

> Why is optimization blocked, which evidence proves it, and what should I do next?

Expected behavior:

1. Retrieve the highest-ranked workflow issue and relevant crane/weight/readiness evidence.
2. Preserve the deterministic blocker recommendation.
3. Append citations such as `[E1]` with source locator, relevance, confidence, and model revision.
4. Never recommend forcing GA, bypassing eligibility, or using estimated weights as trusted engineering evidence.
