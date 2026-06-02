# Agent Rules: "Do Not Do" Checklist

To prevent regressions, architectural drift, or instability, AI agents and developers must **NEVER** perform the following actions:

---

## 1. Architectural Violations

*   **Do NOT reference outer namespaces in Domain:** Never add NuGet packages, Entity Framework libraries, or references to Application/Infrastructure/UI projects inside the `CraneOptimizer.Domain` project.
*   **Do NOT reference View components in ViewModels:** Never import `System.Windows.Controls`, reference windows, user controls, page elements, or update visual elements directly from a ViewModel class.
*   **Do NOT bypass repositories:** Never write EF Core LINQ queries (such as `_context.Set<CraneSpecification>().Where(...)`) directly inside Application services or ViewModels. All database operations must go through the declared repository interfaces (`ICraneRepository`, `IProjectRepository`).

---

## 2. Technical and Runtime Errors

*   **Do NOT block the UI Thread:** Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on asynchronous operations inside the UI thread. This will cause WPF layout thread deadlocks.
*   **Do NOT use hardcoded database paths:** Do not assume a hardcoded folder like `C:/db/` exists. Always resolve local database directories using `Environment.SpecialFolder.ApplicationData` or relative data directory paths.
*   **Do NOT instantiate DbContext inside ViewModels:** Do not use `new CraneDbContext(...)` inside page controllers. Use the injected repository services.
*   **Do NOT run heavy calculations on the Main UI Thread:** Do not invoke `GAEngine.Run()` or `IfcReaderRepository.ReadElements()` directly inside synchronous command handlers. Always wrap them in a `Task.Run` background call or an asynchronous event workflow.
*   **Do NOT commit SQLite database files (`.db`, `.sqlite`) to Git:** These files are excluded by `.gitignore` for a reason (except test fixtures if required, though dynamic generation is preferred).

---

## 3. Styling and Design Mistakes

*   **Do NOT hardcode colors in page XAML:** Never use raw hex strings (e.g. `#FF0000`) inside a View's background or foreground properties. Always reference the shared color palette brushes (e.g. `Background="{StaticResource PrimaryBrush}"`).
*   **Do NOT mix typography styles:** Do not use default Windows fonts like Segoe UI in new views. Always apply the **Exo 2** resource keys (e.g., `FontFamily="{StaticResource FontPrimary}"`).
