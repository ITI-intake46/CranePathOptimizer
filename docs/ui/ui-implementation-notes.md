# UI Implementation Notes - F8_GA Run View

This document records the UI architectural decisions, layout structures, dynamic visibility strategies, and future extension plans for the **F8_GA Run View** within the Crane Path Optimizer.

---

## 1. Architectural UI Decisions

The user interface for adjusting solver settings utilizes a code-behind manual retrieval and validation model rather than a dynamic bindings-only Model-View-ViewModel (MVVM) approach for performance, simpler data coercion, and instant UI state-validation feedback. 

*   **Validation Context**: Input conversion (e.g., parsing strings in `TextBox.Text` to `double` or `int` parameters) occurs during settings construction/extraction.
*   **Controls Design**: To maintain styling consistency, the UI uses the existing color palette (including gradient borders, dark grey backgrounds, and modern rounded input elements).
*   **Layout Container**: A single, scrollable vertical layout (`ScrollViewer` nested with collapsible sections or styled group headers) is used inside the `PanelAdvanced` SideWindow. This ensures all settings remain easily accessible in one location.

---

## 2. Future GA Parameters Expected to Become User-Facing

The following optimization parameters are currently hardcoded in the solver implementation but are slated for future exposure through `GASettings` and the WPF UI:

*   **`TournamentSize`** (integer selection pool size for tournament selection)
*   **`HeuristicSeedingRatio`** (ratio of initial population generated via greedy heuristic vs random placement)
*   **`Mutation Distribution Weights (80/10/10)`** (relative proportions of mutation operators applied: Position Shift / Crane Selection / Set Mutation)
*   **`ElitismPreservationRate`** (fraction of top chromosomes preserved directly into the next generation)
*   **`SpecialCasePenaltyWeight`** (penalty applied to layouts violating non-overlapping setups or restricted crane placements)
*   **`GreedyMaxSetups`** (upper setup limit constraint used inside Greedy Set-Covering heuristic)
*   **`GreedyTargetCoverage`** (percentage of elements required to be covered in Greedy Phase)
*   **`GreedyMinImprovement`** (minimum percentage gain required to add another crane position in Greedy Phase)

### Extensibility Plan
To add these settings without causing layout bloat:
1.  **Group Alignment**:
    *   `TournamentSize`, `HeuristicSeedingRatio`, and `ElitismPreservationRate` will be added to the **Advanced GA** section.
    *   `SpecialCasePenaltyWeight` will be placed under the **Fitness Strategy** section.
    *   `GreedyMaxSetups`, `GreedyTargetCoverage`, and `GreedyMinImprovement` will be added to the **Layout Constraints** or **Advanced GA** section depending on user optimization flow.
2.  **UI Layout Grid**: Each new setting will use the standard `<DockPanel>` or grid-aligned label/textbox pair.

---

## 3. Dynamic Visibility Preparation

Although the current UI exposes all settings unconditionally, the layout architecture is structured to allow conditional toggling based on parent values using WPF `BooleanToVisibilityConverter` or manual visibility assignment in code-behind.

### Visibility Rules Planned:
1.  **If `EnableAdaptiveSigma` = `false`**:
    *   **Hide**: `SigmaCoolingExponent`
    *   **Hide**: `MinimumMutationSigma`
    *   *Rationale*: These cooling schedule parameters have no effect when adaptive sigma is disabled.
2.  **If `EnableConcurrentOptimization` = `false`** (i.e. running greedy setup covers):
    *   **Show**: `GreedyMaxSetups`, `GreedyTargetCoverage`, `GreedyMinImprovement`
    *   *Rationale*: These settings govern the set cover process and are irrelevant in concurrent multi-setup GA runs.

### Implementation Checklist for Future Visibility Hooks:
*   Wrap related controls inside nested `<StackPanel x:Name="AdaptiveSigmaSettingsPanel" Visibility="Visible">` to allow group toggling.
*   Bind the `Visibility` property of the nested panel to the toggle control (e.g., `ChkEnableAdaptiveSigma` checkbox) using a converter.

---

## 4. Future GA Modes (Upcoming Roadmap)

The upcoming roadmap outlines four GA Modes that will partition optimization runs by functional goals:

*   **GA Mode 1: Static Single Crane** (Optimizes coordinate position and boom length for a single fixed crane setup).
*   **GA Mode 2: Multi-Crane Cover** (Solves minimum-set relocation coverage using a single mobile crane moving between multiple setup positions).
*   **GA Mode 3: Dynamic Relocation Routing** (Solves sequencing, path planning, and movement logistics between sequential setups).
*   **GA Mode 4: Collaborative Swarm** (Coordinates multiple active cranes operating simultaneously with collision avoidance).

### Design Assumptions:
*   **Common Settings**: Fitness strategy weights (Coverage, Relocation, Radius Penalty) apply to all four modes.
*   **Mode-Specific Settings**:
    *   *Mode 1*: Setup counts (`MaxCranePositions`) is locked to 1.
    *   *Mode 2*: Relocation-specific penalties (`SimilarPositionPenaltyWeight`, `RelocationDistancePenaltyWeight`) are heavily exposed and tuned.
    *   *Mode 3*: Dynamic route calculations will require exposing path-finding step limits.
    *   *Mode 4*: Collision clearance offsets and safety margins will join the Diagnostics/Engineering sections.
