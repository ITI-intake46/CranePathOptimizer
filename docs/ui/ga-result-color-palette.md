# Genetic Algorithm (GA) Result Color Palette Reference

This document outlines the visual representation of precast elements and crane positions based on GA optimization results in both the **3D Viewport** and the **GA Results DataGrid**.

---

## 1. Visual Mappings & Color Legend

| Status / State | Meaning / User Criteria | Viewport Color | Viewport Hex | DataGrid Color / Tint | DataGrid Hex | Frequency / Type |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`Liftable`** | Element can be successfully lifted by the selected setup point. | Calm Green | `#009E73` | Dark Green Tint | `#122A1E` | User-Facing (Common) |
| **`Overweight`** | Element exceeds the crane's derated capacity at that radius. | Vermillion Red | `#D55E00` | Dark Red Tint | `#2D1818` | User-Facing (Critical Fail) |
| **`OutOfRadius`** | Element is beyond the maximum outreach of the crane configuration. | Calm Orange | `#E69F00` | Dark Orange Tint | `#2C1D12` | User-Facing (Reach Fail) |
| **`HeightFail`** | Element fails hook clearance, boom clearance, or clearance height limits. | Muted Purple | `#8E6AB8` | Dark Purple Tint | `#24172B` | User-Facing (Height Fail) |
| **`SpecialCase`** | Element fails general geometry checks or fails non-standard fallback checks. | Neutral Gray | `#8B8B8B` | Dark Gray Tint | `#222224` | User-Facing (Rare Fallback) |
| **`Chart/Data Issue`** | Load chart parameters are unavailable, zero, or the element is outside the chart. | Muted Gray-Blue | `#64748B` | Dark Slate Tint | `#1E2530` | Internal/Rare (Grouped) |
| **Default / Neutral** | Cleared results or element has not been optimized. | Concrete Gray | `#B8B8B8` | Default Dark Table Row | `#1E1E1E` | Neutral State |
| **Selected Element** | Element currently selected by the user in the UI. | Soft Blue | `#4EA1F3` | Grid Highlight (System Blue) | Varies | Selection Highlight |
| **Selected Crane Point**| Marker indicating the selected setup point location. | Theme Blue | `#3B82F6` | N/A | N/A | Marker Highlight |
| **Assigned to Other Point**| Assigned to a setup point other than the currently selected one in `FinalAssignment` mode. | Dimmed Gray | `#9CA3AF` | Hidden / Filtered | N/A | User-Facing (Assignment Mode) |

---

## 2. Grouping of Chart & Data Statuses

To keep the viewport clean and user-friendly, all rare load-chart and parameter availability errors are grouped under a single neutral/dimmed visual treatment called **`Chart/Data Issue`**:
* `NotCharted` (generic load chart failure)
* `OutsideChartEnvelope` (element falls outside the manufacturer's load chart range)
* `InvalidBlankCell` (resolved load chart cell is empty)
* `InvalidZeroCell` (resolved load chart cell has 0 capacity)

These statuses do not compete with the main safety failure colors (red, orange, and purple), preventing visual clutter. However, the **GA Results DataGrid** still displays the exact status string (e.g., "Outside Chart Envelope") in the "Status" column text so that details remain fully visible to the user.

---

## 3. Important Design Notes

* **Accessibility / Color Independence**: Color is never the sole indicator of status. The DataGrid maintains highly legible text labels in the "Status" column (e.g. "Overweight") for screen readers and color-blind accessibility.
* **Tints for Dark Theme**: The DataGrid uses dark semantic tints (`#122A1E`, `#2D1818`, `#2C1D12`, `#24172B`, etc.) to maintain a comfortable reading experience in the dark theme while still matching the semantic meanings of the viewport colors.
* **Separation of Modes**:
  - In **`FinalAssignment`** mode, green indicates that the element is assigned *only* to the selected crane setup point. Elements assigned to other points are dimmed to `#9CA3AF`.
  - In **`SelectedPointFeasibility`** mode, green represents all elements that the selected point is *capable* of lifting, regardless of final assignments.

---

## 4. Implementation Files

The following files control the GA result color palette and material rendering:
1. **Visualization Enum**: `CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualizationStatus.cs`
2. **Strategy Contract**: `CraneOptimizer.UI/Features/F2_IfcViewer/IElementColorStrategy.cs`
3. **Liftability Strategy**: `CraneOptimizer.UI/Features/F2_IfcViewer/LiftabilityColorStrategy.cs`
4. **Centralized Palette & Materials**: `CraneOptimizer.UI/Features/F2_IfcViewer/ElementMaterialProvider.cs`
5. **Dynamic Materials & Viewport Updates**: `CraneOptimizer.UI/Core/Services/SceneRenderingManager.cs`
6. **Point Marker Rendering**: `CraneOptimizer.UI/Features/F8_GAOptimizer/Rendering/BestPointVisualBuilder.cs`
7. **DataGrid Styles & Row Colors**: `CraneOptimizer.UI/Features/F8_GAOptimizer/Views/GAResultsWindow.xaml`
8. **Brand Theme Accents**: `CraneOptimizer.UI/Resources/Colors.xaml` and `Brushes.xaml`
