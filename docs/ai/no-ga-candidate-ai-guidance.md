# AI guidance for `NoGaCandidates` / estimated-weight GA blockers

This patch keeps the engineering optimizer untouched. The AI layer may explain and package evidence, but it must not relax GA eligibility, use estimated weights as trusted lift weights, or mutate load-chart/import/domain rules.

## Recognized deterministic blockers

The AI context now treats these codes as high-priority workflow blockers:

- `GaCannotStartNoCandidates`
- `GaPreRunNoCandidates`
- `NoGaCandidates`
- `EstimatedWeightBlockedFromGA`
- `MissingTrustedWeight`

When these appear, the AI should explain that the IFC can be loaded and visible while still having zero valid GA lift units. Displayed weights are not necessarily trusted engineering weights. The safe next step is import/element evidence review, not bypassing the optimizer.

## AI-only actions

The docked AI Assist panel can now propose text-only helper actions:

- Copy BIM exporter checklist.
- Copy optimizer issue summary.
- Copy readiness evidence.
- Stage the focused prompt: “Why is the optimizer blocked, and what should I ask the BIM exporter to fix?”

All actions are confirmable and clipboard/prompt/navigation only. They do not change project data, GA settings, load-chart data, report validity, or element eligibility.

## Expected response posture

For this scenario, AI responses should use this shape:

- **Status:** optimizer cannot start because there are zero valid grouped assembly lift units.
- **Evidence:** IFC/elements/zones/cranes may exist, but deterministic readiness reports no GA candidates and estimated/trust gaps.
- **Recommendation:** review/export explicit BIM mass or weight quantities, stable assembly marks, COG, volume/material, and parent-child assembly structure.
- **Next Click:** `IFC MODEL → Export Element List / weight-source review`.
- **Risk:** using estimated weights for engineering GA would undermine capacity/readiness validity.
- **Confidence:** high when based on deterministic readiness codes.
