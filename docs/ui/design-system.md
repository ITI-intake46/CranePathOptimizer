# UI Design System

The visual appearance of the **Mobile Crane Path Optimizer** is designed for modern, high-contrast engineering dashboards.

---

## 1. Aesthetic Identity

The application uses a **Curated Dark Theme** (swapped via `ThemeService.cs`) to reduce eye strain for engineers looking at dense 3D meshes and coordinates.
*   **Layout Structure:** Split layout with a fixed sidebar (width: `260px`) on the left and a responsive content area on the right.
*   **Window State:** Standard maximized view is set by default (`WindowState="Maximized"`) to maximize space for the 3D viewport.

---

## 2. Typography

*   **Primary Typeface:** **Exo 2** (weights: Regular, SemiBold, Bold).
*   **Font Loading:** Typefaces are bundled directly as resources in the application under [Resources/Fonts/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Fonts), allowing consistent rendering on any machine without needing local installations.
*   **Standard Typography Styles:**
    *   `TextHeadline`: Bold Exo 2, size `FontSizeLG` (13). Used for section and dialog headers.
    *   `TextTitle`: SemiBold Exo 2, size `FontSizeMD` (11). Used for card/panel headers.
    *   `TextBody`: Regular Exo 2, size `FontSizeBase` (11). Default reading text.
    *   `TextCaption`: Regular Exo 2, size `FontSizeSM` (10). Small helper / metadata text.
    *   `TextValue`: Bold Exo 2, size `FontSizeLG` (13) in brand terracotta, used for numerical readouts.
    *   `TextNavLabel`: Bold Exo 2, size `FontSizeBase` (11). Used for navigation labels.

### Implementation Reference

File:
[Typography.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Typography.xaml)

---

## 3. UI Controls and Custom Styling

All custom UI control styles are defined in [Styles.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Styles.xaml):
*   **Card Panels:** Styling defined under control dictionaries to organize forms, tables, and details cleanly.
*   **DataGrids:** Customized header heights, sienna selection highlights, and dark-themed cell structures to fit seamlessly into the dark interface.
*   **Form Inputs (`TextBox`, `ComboBox`):** Styled with clean borders and Exo 2 font metrics.
*   **ScrollBars:** Customized scroll tracks to replace thick gray default bars with modern, thin thumbs.
