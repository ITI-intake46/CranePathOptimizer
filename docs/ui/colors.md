# UI Color Palette & Visual Coding

Branding colors and 3D modeling colors are coordinated to ensure maximum legibility and a professional look.

---

## 1. Palette Reference

Color tokens are defined in [Colors.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Colors.xaml) and resolved as brushes in [Brushes.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Brushes.xaml):

| Color Key | Hex Color | Role |
|---|---|---|
| `Color.Primary` | `#D68A56` | Main brand color (warm terracotta/sienna) |
| `Color.PrimaryHover` | `#E49B6A` | Hover states |
| `Color.PrimaryPressed` | `#BD7743` | Pressed states |
| `Color.Secondary` | `#757575` | Muted elements / gray |
| `Color.Tertiary` | `#CFD8DC` | Light blue-gray |
| `Color.Destructive` | `#EF5350` | Deletion, delete alerts, CTAs |
| `Color.Error` | `#EF5350` | Failure indicators |
| `Color.Success` | `#66BB6A` | Verified checks / success |
| `Color.Warning` | `#D68A56` | Warning flags |
| `Color.OnPrimary` | `#1A1A1A` | Dark text on primary surfaces |
| `Color.BuildingDefault`| `#D68A56` | Default brand tint for building elements |

---

## 2. 3D Model Visual Coding (HelixViewport)

To make it easy for structural planners to understand imported BIM designs, elements are colored systematically in the 3D Helix Toolkit viewport:

*   **Structural Elements:**
    *   **Columns:** Mapped based on role.
    *   **Beams:** Mapped based on role.
    *   **Slabs:** Mapped based on role.
    *   **Building Default Tint:** Mapped using `Color.BuildingDefault` (`#D68A56`) to maintain unified brand aesthetics.
*   **Constraints and Overlays:**
    *   **Green Areas (Crane setup zones):** Translucent Green (visualized as flat polygon sheets).
    *   **Obstacles:** Translucent Red (extruded 3D bounding boxes).
    *   **Site Boundary:** Solid Yellow lines.
    *   **Crane Radius Boundary:** Translucent Cyan circle boundary lines centered at the active crane coordinates.
