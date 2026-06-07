# UI Design System & Resource Mapping

This consolidated guide covers typography, colors palette, and Resource Dictionaries for the WPF application.

## Consolidated from: UI Color Palette & Visual Coding (ui/design-system.md)


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


---

## Consolidated from: UI Resource Dictionaries (ui/design-system.md)


WPF styling is organized into separate XAML resource dictionaries to modularize style definitions and support dynamic themes.

---

## 1. Directory Layout

All UI styling resources are located in [CraneOptimizer.UI/Resources/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources):

```
Resources/
├── Colors.xaml                # Brand color tokens (shared by both themes)
├── Brushes.xaml               # Primary brand brush mappings
├── Styles.xaml                # Shared base WPF control overrides
├── Typography.xaml            # Exo 2 font definitions and text styles
├── Theme/                     # Brushes for Light and Dark theme modes
├── DesignSystem/              # Spacing, Elevation, and subfolders for Control styles
└── Shell/                     # Main chrome and window window structures
```

---

## 2. Merged Dictionaries Configuration

These resource dictionaries are loaded during startup in the `App.xaml` root dictionary:

```xml
<ResourceDictionary.MergedDictionaries>
    <!-- [0] Color tokens -->
    <ResourceDictionary Source="Resources/Colors.xaml"/>

    <!-- [1] Brush tokens -->
    <ResourceDictionary Source="Resources/Brushes.xaml"/>

    <!-- [1b] Theme surface brushes -->
    <ResourceDictionary Source="Resources/Theme/Dark.Brushes.xaml"/>

    <!-- [2] Typography -->
    <ResourceDictionary Source="Resources/Typography.xaml"/>

    <!-- [3] Spacing -->
    <ResourceDictionary Source="Resources/DesignSystem/Spacing.xaml"/>

    <!-- [3b] Elevation -->
    <ResourceDictionary Source="Resources/DesignSystem/Elevation.xaml"/>

    <!-- [4] Control styles -->
    <ResourceDictionary Source="Resources/DesignSystem/Controls/DataGridStyles.xaml"/>
    <ResourceDictionary Source="Resources/DesignSystem/Controls/RibbonStyles.xaml"/>
    <ResourceDictionary Source="Resources/DesignSystem/Controls/NavigationStyles.xaml"/>
    <ResourceDictionary Source="Resources/DesignSystem/Controls/ControlStyles.xaml"/>

    <!-- [5] Shell chrome -->
    <ResourceDictionary Source="Resources/Shell/ShellStyles.xaml"/>

    <!-- [6] Window chrome -->
    <ResourceDictionary Source="Resources/Shell/WindowStyles.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

> [!NOTE]
> The order of dictionaries inside `MergedDictionaries` matters. Dictionaries that depend on other dictionaries (like `Brushes.xaml` depending on color tokens in `Colors.xaml`) must be loaded *after* their dependencies.

---

## 3. Resolving Resources

*   **Colors & Brushes:** Fixed branding colors are referenced using `StaticResource` (e.g., `Background="{StaticResource PrimaryBrush}"`).
*   **Theme Brushes:** Theme-dependent colors (like background and borders) are swapped dynamically by changing the dictionary loaded at index `2` at runtime via `ThemeService.cs`.
*   **Typography:** Applied using font family keys (e.g., `FontFamily="{StaticResource FontPrimary}"`).


---

