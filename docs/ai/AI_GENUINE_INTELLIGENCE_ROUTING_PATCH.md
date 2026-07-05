# AI Assist genuine-intelligence routing patch

This patch removes deterministic prose answers for open-ended user questions. The assistant still uses deterministic services for facts, counts, metrics, readiness, and safety-critical blockers, but it no longer answers domain, delivery, quality, product-value, or local-model questions by matching a phrase and returning a fixed paragraph.

## Implemented behavior

- Open-ended questions now route to the local Ollama model with an intent/evidence contract.
- The intent router still classifies the question family so the model can choose the correct evidence world: domain knowledge, live project facts, or software/navigation context.
- Deterministic answers remain only where they are appropriate: BIM/category counts, zone/crane/load-chart counts, project area/height metrics, workflow status, IFC metadata exposure, and safety-critical optimizer blockers.
- Local documentation/RAG remains selective. It is suppressed for domain explanations, live project facts, quality/advisory questions, delivery-readiness questions, ModelOps questions, workflow status, and unknown mixed questions unless the user explicitly asks for documentation.
- The prompt contract explicitly forbids memorized Q&A prose and requires a fresh response synthesized from the visible user question plus supplied evidence.
- ModelOps questions receive factual model evidence only: selected model, benchmark summary, and capability summary. The local model must make the recommendation and state what remains unknown.

## Regression intent

The tests now verify that questions such as `What is a crane?`, `What is critical about crane logistics?`, `Why is this program important?`, `What is needed to be delivered?`, `How to ensure accuracy of results?`, and `Which local model is best?` are **not** handled by deterministic canned prose. They are classified and routed to the LLM.

Counts and metrics, such as green-area count and building area, remain deterministic because those require exact live project facts rather than generative reasoning.
