# UI Design System

The visual appearance of the **Mobile Crane Path Optimizer** is designed for modern, high-contrast engineering dashboards.

---

## 1. Aesthetic Identity

The application uses a **Curated Dark Theme** to reduce eye strain for engineers looking at dense 3D meshes and coordinates.
* **Layout Structure:** Split layout with a fixed sidebar (width: `260px`) on the left and a responsive content area on the right.
* **Window State:** Standard maximized view is set by default (`WindowState="Maximized"`) to maximize space for the 3D viewport.

---

## 2. Typography

* **Branding Font:** **Poppins** (Poppins-Regular, Poppins-SemiBold, Poppins-Bold).
* **Font Loading:** Typefaces are bundled directly as resources in the application under [Resources/Fonts/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Fonts), allowing consistent rendering on any machine without needing local installations.
* **Text Styles:**
  * `AppTitle`: Bold Poppins, size 22. Used for sidebar branding.
  * `PageTitle`: SemiBold Poppins, size 24. Used for main screen headers.
  * `SectionTitle`: SemiBold Poppins, size 18. Used for card headers.
  * `BodyText`: Regular Poppins, size 14. Used for descriptive copy and fields.

---

## 3. UI Controls and Custom Styling

All custom UI control styles are defined in [Styles.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources/Styles.xaml):
* **Cards (`CardPanel`):** Border panels with rounded corners (`CornerRadius="8"`) and deep blue backgrounds, acting as containers for forms, tables, and settings.
* **DataGrids:** Transformed from default white grids to dark-themed grids with custom row heights, accent-colored selection lines, and dark column headers.
* **Form Inputs (`TextBox`, `ComboBox`):** Styled with deep blue inputs, light borders, and Poppins font metrics.
* **ScrollBars:** Customized scroll tracks to replace thick gray default bars with modern, thin dark thumbs.
