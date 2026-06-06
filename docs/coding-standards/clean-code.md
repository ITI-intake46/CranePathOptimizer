# Coding Standards: Clean Code & Architecture

This document defines the general clean coding guidelines for the **Mobile Crane Path Optimizer** codebase.

---

## 1. Separation of Concerns & Clean Architecture Boundaries

To preserve Onion Architecture boundaries, follow these guidelines:
* **The Domain is Sacred:** `CraneOptimizer.Domain` must not reference any outer libraries, WPF namespaces, or ORMs (Entity Framework). Use simple C# types and interfaces.
* **No Database Details in Business logic:** Application services must communicate with databases via interfaces like `ICraneRepository` or `ISiteRepository`. Never expose EF Core `DbSet` or `IQueryable` structures directly to the presentation layer.
* **Dependency Injection over Instantiation:** Never use the `new` keyword to instantiate services or database contexts in business logic or ViewModels. Use Constructor Injection.

---

## 2. Asynchronous Programming Guidelines

Since this is a desktop application, blocking the UI thread will cause the app to freeze.

* **Return Tasks:** Database accesses, file writing, and optimization workflows should be asynchronous and return `Task` or `Task<T>`. IFC parsing repository methods may expose synchronous APIs, but UI command handlers must invoke them through an asynchronous workflow such as `Task.Run()`.
* **No Blocking Calls:** Do not use `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()`. Use the `await` keyword.
* **Background Processing for Heavy Calculations:** Use `Task.Run()` to offload CPU-bound calculations (like running the Genetic Algorithm optimization loop) to background threads, keeping the WPF thread responsive.
* **Use Async Commands:** ViewModels executing asynchronous actions must use the custom `AsyncRelayCommand` class rather than the blocking `RelayCommand`.

---

## 3. Error Handling

* **Defensive Coding:** Always perform null checks, file existence validation, and value boundary assertions at service boundaries.
* **Try-Catch Block Usage:** Wrap external system operations (e.g. SQLite database updates, file saving/loading, and xBIM IFC model reads) in `try-catch` structures.
* **Graceful Degradation:** When an error occurs during background tasks, catch it, log it via the `IDiagnosticService`, and display a user-friendly error message on screen using bound properties. Do not let the app crash.
