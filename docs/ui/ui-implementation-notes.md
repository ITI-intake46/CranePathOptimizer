# UI Implementation Notes — GA Parameter Help System & Run History Logger

This document records the architectural decisions, design patterns, and future requirements for the Genetic Algorithm (GA) Parameter Help System and the Optimization Run History Logger.

## GA Parameter Help System

### Design Decisions
1. **Centralized code-only static registry (`GAParameterHelpRegistry`)**: 
   - Keeps XAML files clean and prevents duplication.
   - Simplifies translation, localization, and updates to descriptions without modifying UI bindings.
   - Avoids database overhead for static metadata.
2. **Unified Custom UserControl (`ParameterHelpIcon`)**:
   - Represents the standard visual cue `ⓘ`.
   - Utilizes themed brushes (`TextSecondaryBrush`, `SurfaceBrush`, `BorderBrush`) and typography keys from the design system (`FontPrimary`, `FontSizeSM`, `FontSizeXS`).
   - Dynamically resolves parameter details from `GAParameterHelpRegistry` on load or parameter key changes.

### Future UI Requirements / Deferred Improvements
- **Localization**: Connect the metadata fields in `GAParameterHelpRegistry` to resource files (`.resx`) to support multi-language environments.
- **Rich Interactive UI**: Add interactive links in help tooltips referencing pages in the developer/user documentation for deep-dive tutorials on complex variables (e.g. `EnableAdaptiveSigma` or `DeratingRatio`).
- **Dynamic Help Side-Panel**: Implement a toggleable help panel next to the advanced settings scrollable panel, displaying full details of the currently focused property, rather than relying solely on tooltips.

---

## Optimization Run History Logger

### Design Decisions
1. **Consolidated Logging Integration Point (`CranePlacementService.cs`)**:
   - The UI-layer service `CranePlacementService` was chosen as the integration point (instead of `CranePlacementViewModel.cs`) to ensure every run initiated within the active dialog session is persistently logged.
   - Decoupled from the ViewModel to maintain single-responsibility separation of concerns.
2. **Robust Thread-Safe File Access (`CsvOptimizationHistoryLogger.cs`)**:
   - Implements `IOptimizationHistoryLogger` utilizing `SemaphoreSlim(1, 1)` to serialize async file writes, preventing lock errors from parallel tasks.
   - Applies strict culture-invariant decimal parsing (`CultureInfo.InvariantCulture`) to ensure CSV files are parseable on machines using German, French, or other non-English OS locale settings.
   - Automatically handles escaping for special characters (quotes, commas, newlines) inside text fields like `ProjectName`, `IfcFileName`, or `CraneModel`.
3. **Failsafe Catch-Block Protection**:
   - Contains a try-catch block that swallows logging exceptions, logging them to `Debug.WriteLine`. This prevents disk write errors or file locking failures from ever crashing the main optimization process or UI thread.

### Upcoming Changes / Future Roadmap
- **GA Modes Integration**: When standard presets (e.g., *Fast Run*, *Standard Run*, *Deep Search*) are implemented, the mode classification should be recorded in the CSV logger.
- **Visual Analytics Dashboard**: A future analytics tab will read `OptimizationHistory.csv` and plot parameter convergence trends, execution times, and solution quality across consecutive runs.
- **Database Storage Transition**: Consider migrating run entries from CSV to the SQLite embedded database (`crane.db`) once query/filtering capabilities are requested by end users.
