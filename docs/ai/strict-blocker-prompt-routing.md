# Strict blocker prompt routing

The AI companion now treats `NoGaCandidates`, `GaCannotStartNoCandidates`, `GaPreRunNoCandidates`, and estimated-weight GA blockers as high-priority deterministic workflow states.

When the user asks a next-step, explanation, troubleshooting, or BIM-exporter question during this state, the companion answers from deterministic evidence first. The intended answer is operational, not documentary:

1. Do not run the optimizer again yet.
2. Confirm that the IFC, zone, and crane may already be present.
3. Explain that GA has zero trusted grouped assembly lift units.
4. Direct the user to `IFC MODEL → Export Element List / weight-source review`.
5. Ask for explicit BIM mass/weight quantities, stable assembly/lift marks, and COG or auditable geometry.
6. Warn against bypassing GA eligibility or using estimated weights for engineering optimization.

For safety-critical blocker questions, the companion can return this deterministic answer without calling Ollama. For other questions in the same blocker state, the hidden Ollama context begins with `CURRENT CRITICAL BLOCKER` and suppresses generic documentation, XAML/software inventory, workflow inventory, and prompt-template summaries.

This is an AI-layer routing feature only. It does not relax optimizer gates or mutate engineering data.
