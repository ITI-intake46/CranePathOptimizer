# GA Parameter Transparency & Trust

This document describes the design decisions, parameter behaviors, and UI constraints implemented under **Phase 7 — GA Parameter Transparency & Trust Fix** to ensure complete consistency, predictability, and user clarity when configured via the Crane Optimizer dialog.

## Overview

Historically, certain Genetic Algorithm (GA) parameters either had divergent behaviors between the UI presentation and the underlying solver execution, or lacked clear user documentation (tooltips). To restore trust and eliminate hidden optimization overrides, the UI and Application layers have been aligned.

---

## 1. Parameters with Adaptive or Divergent Solver Behavior

### Max Crane Positions (`MaxCranePositions`)
* **UI Input**: Specifies an integer count (e.g., 2, 3, or 4).
* **Actual Solver Behavior**: In multi-stage discovery mode, `MaxCranePositions` acts as a **strict upper limit**, not a mandatory target count. The solver will sequentially evaluate stages starting from 1 position up to the user-defined limit.
* **Early Stopping**: If a lower position count (e.g., 2 positions) satisfies the coverage target, the solver halts escalation immediately. The final solution may therefore return fewer positions than the limit specified by the user.

### Relocation Weight (`RelocationWeight`)
* **UI Input**: A base penalty value for relocations (e.g., 5.0).
* **Actual Solver Behavior**: In multi-stage mode, the relocation weight is **adapted per stage** and not applied directly as a constant:
  - **Stage 1 (1 setup)**: Relocation penalty is hardcoded to `0` since a single position has no relocation cost.
  - **Stage 2+**: The penalty scales adaptively based on the coverage achieved in the previous stage. The UI value acts as the **scaling base** for this calculation rather than a direct linear penalty.

---

## 2. Hard Clamping & Validation Policies

### Derating Ratio (`DeratingRatio`) & Safety Factor (`SafetyFactor`)
To prevent mathematical instability (e.g., division by zero or negative capacities) and adhere to structural engineering sanity limits:
* Any user input outside the range `[0.0, 1.0]` is **automatically clamped** at the workflow boundary before being passed to the solver:
  - Values `< 0.0` clamp to `0.0`
  - Values `> 1.0` clamp to `1.0`
* Tooltips explicitly notify the user of this clamp-on-parse policy.

---

## 3. UI Policy Locking

### Enable Multi-Stage Discovery (`EnableMultiStageDiscovery`)
* **WPF UI Policy**: To guarantee high-quality layout optimization that searches for the most compact relocation count, **Multi-Stage Discovery is locked to `true`** for all normal UI optimization runs.
* **UI Element**: The "Enable Multi-Stage Discovery" checkbox remains visible in the dialog so users can see the active policy, but is disabled (`IsEnabled="False"`) to prevent accidental disabling.
* **Workflow Override**: `CraneOptimizerDialogWorkflow.BuildSettings()` ignores the checkbox's underlying string/text parameter and hardcodes `EnableMultiStageDiscovery = true`.

---

## 4. Developer & Experiment Path Overrides

While the UI dialog enforces these user-facing restrictions and alignment rules, the core `GASettings` class remains fully flexible:
* Developers, integration tests, or experiment runners (such as `GAExperiment`) executing optimizations via non-UI paths can override `GASettings.EnableMultiStageDiscovery` or apply customized parameter sets directly.
* No internal GA fitness formulas or assignment logic constraints have been changed; this is purely a UI and workflow transparency adjustment.
