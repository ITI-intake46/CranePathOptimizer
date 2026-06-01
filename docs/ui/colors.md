# UI Color Palette & Visual Coding

Branding colors and 3D modeling colors are coordinated to ensure maximum legibility and professional look.

---

## 1. Palette Reference

 Brushes are defined in [Colors.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Colors.xaml):

| Brush Key | Hex Color | Role |
|---|---|---|
| `BackgroundDark` | `#1A1A2E` | Page backdrop color |
| `SidebarBackground` | `#16213E` | Navigation sidebar background |
| `CardBackground` | `#0F3460` | Forms, tables, and settings card wrappers |
| `AccentColor` | `#E94560` | Main CTA buttons, active states |
| `AccentHover` | `#FF6B81` | Interactive elements highlight color |
| `TextPrimary` | `#FFFFFF` | Primary readable labels and headings |
| `TextSecondary` | `#A0AEC0` | Helper text, fields placeholders |
| `BorderColor` | `#2D3748` | Card dividing lines |
| `SuccessColor` | `#48BB78` | Save success, correct GA metrics |
| `WarningColor` | `#ECC94B` | Warnings, intermediate GA boundaries |
| `ErrorColor` | `#FC8181` | Invalid placement flags, deletion alerts |
| `InputBackground` | `#1E2D4A` | Text box background color |
| `InputBorder` | `#4A5568` | Text box borders |

---

## 2. 3D Model Visual Coding (HelixViewport)

To make it easy for structural planners to understand imported BIM designs, elements are colored systematically in the 3D Helix Toolkit viewport:

* **Structural Elements:**
  * **Columns:** Royal Blue (representing structural vertical supports).
  * **Beams:** Bright Green (representing structural spanning elements).
  * **Slabs:** Flat Gray (representing floors/roofs).
  * **Other precast pieces:** Off-white/concrete cream.
* **Constraints and Overlays:**
  * **Green Areas (Crane setup zones):** Translucent Green (visualized as flat polygon sheets).
  * **Obstacles:** Translucent Red (extruded 3D bounding boxes).
  * **Site Boundary:** Solid Yellow lines.
  * **Crane Radius Boundary:** Translucent Cyan circle boundary lines centered at the optimal crane coordinate.
