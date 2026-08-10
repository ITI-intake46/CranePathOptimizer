# Strict AI Isolation: Readiness Authority Boundary

## Scope invariant

Production implementation for this readiness correction lives entirely in `CraneOptimizer.AI`.
The AI project does not reference `CraneOptimizer.UI`, `CraneOptimizer.Application`,
`CraneOptimizer.Infrastructure`, `CraneOptimizer.Domain`, or `CraneOptimizer.GAExperiment`.

The genetic-algorithm engine and optimizer UI are not modified, wrapped, moved, or reimplemented.

## Authority model

The `div` host contract exposes project facts, screen facts/issues, and completed-result facts.
It does **not** expose the optimizer dialog's authoritative current-run preflight decision.
Therefore:

- project / IFC / zone / crane / current load-chart presence may be classified from host facts;
- import/readiness candidate counts and weight warnings are AI-side troubleshooting evidence;
- those candidate/weight signals must not be promoted to an authoritative optimizer `Ready` or `Blocked` state;
- a completed optimization result proves only what was evaluated in that result, not current rerun eligibility;
- the definitive current-run decision remains `ANALYSIS -> Run Optimizer -> Current Run Readiness`;
- if the optimizer reports a blocker, AI may explain the existing host evidence but must not suggest bypassing the blocker.

## Historical-result boundary

Historical result `MaxCapacity`, `MaxRadius`, evaluated-lift count, or coverage do not prove the
current crane/load-chart setup. When the dedicated current crane context is present, AI requires
that current context to expose an active specification, radius, or capacity value before treating
load-chart evidence as current.

## Question semantics

The same project snapshot can support different, non-contradictory answers:

- **"What should I do next?"**: follow the visible project prerequisite or, after a low-coverage run,
  review `ANALYSIS -> Lift Summary`.
- **"Can I run the optimizer now?"**: AI states that it cannot certify the current run from its
  snapshot and directs the user to the optimizer's built-in readiness indicator.

This keeps workflow guidance inside AI while preserving engineering ownership of optimizer behavior.
