# AI Context Freshness and Session Integrity Hardening

This patch prevents the AI companion from silently answering BIM questions with facts from a previous IFC/model snapshot.

## Delivered safeguards

- Every workflow snapshot now carries a session-local model revision, model fingerprint, model display name, model loaded timestamp, total BIM object count, element-table item count, and currently visible table item count. The fingerprint represents model identity/scope, not transient visible-table filtering.
- The UI bridge increments the model revision when IFC/model elements are loaded, merged, moved, deleted, or when the active project/model identity changes.
- Deterministic BIM Q&A now checks explicit host revision, model fingerprint, and scene-object count facts before answering. It no longer treats readiness/planning element counts as stale when they legitimately differ from full scene BIM object counts because IFC reference geometry is excluded from GA readiness.
- AI answers now include explicit model evidence: active screen, model context, model revision, model fingerprint, total BIM objects, element-table scope, and visible table scope.
- The ribbon publishes the initially selected tab after load, so first AI answers use the actual visible screen instead of falling back to HOME.
- Previous chat messages are marked as stale captured context after the active model fingerprint changes; current messages display captured-at-send context.
- The companion prompt box clears after sending, the Chat tab remains selected, and the composer regains keyboard focus after staged prompts, send, deterministic replies, and streamed reply completion.
- The side-dock now exposes fast BIM count chips for slab count and category breakdown.
- Building type/usage answers now lead with the current-model result before explaining where IFC metadata may be stored.
- Element-count terminology is separated into scene BIM/model objects, readiness/planning elements, IFC reference geometry excluded from readiness, element-table items, and currently visible table items.

## Accuracy contract

For direct live-BIM questions such as `How many slabs are there?`, the assistant must answer from deterministic project facts before invoking Ollama/RAG. Documentation snippets must not override live model data.

If the live model changes after a previous answer, older chat messages are labeled as stale captured context. If explicit host revision, fingerprint, or scene-object count facts disagree with the captured answer snapshot, the assistant refuses the count and asks the user to refresh or ask again after the model operation completes. Readiness/planning counts may differ from scene BIM object counts and are reported as scope evidence rather than treated as stale by themselves.
