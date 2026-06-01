# ADR-003: Use MVVM Pattern for WPF UI Layer

## Status
Accepted

---

## Context
WPF applications can easily degenerate into poorly structured, tightly-coupled codebases if business logic is written inside View code-behind files. To keep the UI responsive, testable, and maintainable, we must choose a clean architectural pattern for presenting data.

---

## Decision
We chose the **Model-View-ViewModel (MVVM)** design pattern with a **ViewModel-First navigation system** resolved via a singleton factory container.

---

## Consequences

### Positive
* **Decoupled Business Logic:** ViewModels contain zero references to visual controls, allowing them to be unit tested in isolation without spinning up WPF windows.
* **Declarative Data Binding:** Views bind to properties in ViewModels via XAML data-binding syntax.
* **Separation of Concerns:** Design teams can customize XAML views (Colors, ButtonStyles, Fonts) without modifying optimization calculations or backend database controllers.
* **Flexible Page Swapping:** Changing screens is as simple as updating the `CurrentView` property in the navigation service.

### Negative
* **Boilerplate Code:** Requires creating matching View and ViewModel classes for every screen, along with custom commands (`RelayCommand`, `AsyncRelayCommand`) and value converters.
* **State Synchronization:** Handling UI feedback during background thread executions requires carefully invoking property updates on the main thread via `SetProperty`.
