# Reports Ribbon Tab — Documentation

---

## 1. How the Ribbon System Works

The ribbon sits at the top of the app. It has two parts stacked vertically:

- **Tab strip (top)** — a row of clickable tab names: HOME, IFC MODEL, DXF, CRANES, ZONES, ANALYZE, **REPORTS**, SETTINGS, COORDINATE REFERENCE
- **Content area (bottom)** — one panel per tab, all sitting on top of each other; only the selected tab's panel is visible, all others are hidden

When you click a tab, all panels hide, then only the one matching your click becomes visible. Every tab panel is its own self-contained UserControl file.

---

## 2. ⭐ The Shared-Backend Pattern

> **The same button can live in multiple ribbon tabs and call the exact same logic — with zero code duplication.**

### Why it works

Every tab panel that needs access to the optimizer's data is given the same **DataContext** — a pointer to the single `CranePlacementViewModel` instance that lives for the entire app session. This means any button in any tab that binds to a command on that ViewModel calls the same underlying method.

Think of it like a TV remote — you can have two remotes in two different rooms, but pressing "Volume Up" on either one controls the same TV.

### Live examples in this app

| Button Label | Analyze Tab | Reports Tab | What it does |
|---|---|---|---|
| **GA Diagnostics** | ✅ present | ✅ present | Opens the GA convergence graph window |
| **View Results** | ✅ present | ✅ present | Opens the optimization results window |

Both buttons in both tabs are wired to the same command. Clicking either one does the exact same thing. If the optimizer hasn't been run yet, **both buttons are automatically disabled** — the ViewModel controls that, not the individual tabs.

### How to add a new shared button

1. Define the command and its logic once in [CranePlacementViewModel.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CranePlacementViewModel.cs)
2. Add a button to [AnalyzeTabView.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Views/AnalyzeTabView.xaml) bound to that command
3. Add the same button to [ReportsTabView.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F12_Reports/Views/ReportsTabView.xaml) bound to the same command
4. Done — both tabs now trigger the same logic, no code repeated anywhere

---

## 3. Files Changed

| File | What happened |
|---|---|
| [ReportsTabView.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F12_Reports/Views/ReportsTabView.xaml) | **Created** — the new Reports ribbon panel with 6 groups |
| [ReportsTabView.xaml.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F12_Reports/Views/ReportsTabView.xaml.cs) | **Created** — placeholder click handlers, all marked TODO |
| [TopNavigation.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Shared/Layouts/TopNavigation.xaml) | Reports tab button added to the strip; Reports panel added to the content area |
| [TopNavigation.xaml.cs](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Shared/Layouts/TopNavigation.xaml.cs) | Reports panel included in the hide/show switching logic |
| [AnalyzeTabView.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Views/AnalyzeTabView.xaml) | Restored the original 3-column layout (OPTIMIZE · REPORTS · RESULTS) |
| [GAResultsWindow.xaml](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Views/GAResultsWindow.xaml) | Two bug fixes applied (see section 6) |

---

## 4. Reports Tab — All Buttons

### ELEMENTS
- **All Elements List** — Full table of every IFC element: name, type, material, weight, dimensions, volume, storey, and whether it can be lifted

### LIFTING
- **Lifting Summary Report** — One row per element showing operating radius, hoist height, hook load, available crane capacity at that radius, and lift status (Liftable / Overweight / Out of range). This single report replaces four older buttons that showed the same data in smaller pieces.

### CRANE CATALOG
- **All Cranes in Database** — Catalog of every crane in the database: brand, model, max capacity, radius range, boom range, outrigger configuration

### COMPARE
- **Compare 2 Cranes** — Pick any two cranes from the catalog and see them side by side: full spec comparison plus which elements each crane can and cannot lift for the current building

### PROJECT
- **Project Overview** — Project name, ID, unit system, creation date, plus a table of all imported IFC files with their element counts and file sizes
- **Site Zones Report** — All site zones defined on the project: name, type (Exclusion / Green / Working), and boundary coordinates

### ANALYSIS *(shared with Analyze tab)*
- **GA Diagnostics** — Opens the optimizer convergence graph (shared backend with Analyze tab)
- **View Results** — Opens the optimization results window (shared backend with Analyze tab)

---

## 5. Analyze Tab — Final Layout

The Analyze tab was temporarily simplified during development and then restored to its original 3-group layout:

| OPTIMIZE | REPORTS | RESULTS |
|---|---|---|
| Run Optimizer button | Export Analysis · Export Zones (JSON) · GA Diagnostics | View Results |

---

## 6. Bugs Fixed Along the Way

### Missing color resource in the Results window
When clicking **View Results** from any tab, the window was crashing immediately on load because it referenced a color called `InfoBrush` that was never defined in that window's own resources. It was defined in the Diagnostics window only. Added the missing definition to the Results window — both windows now use the same sky-blue color.

### Incorrect binding direction on display-only fields
All the coordinate display fields in the Results window (Candidate Point, Crane Base, Slew Center, Boom Hinge, Load Point) were crashing at runtime because WPF was trying to write back to read-only properties. Marked all those display fields as one-directional (read-only data flow) while leaving the interactive controls (ComboBox for view mode, ListBox for selecting a position) untouched with their original two-way bindings.

---

## 7. Build Status

✅ **0 errors** — full solution builds clean after all changes.
