# Agent Context & System Reference

This document serves as the primary onboarding and contextual reference for AI coding agents working on the **Mobile Crane Path Optimizer** project.

---

## 1. Project Purpose

The system is a WPF-based engineering desktop application designed to optimize mobile crane positions for erecting precast concrete buildings. It processes building elements from IFC files, maps site obstacles, green setup areas, and site boundaries, and runs a Genetic Algorithm solver to calculate optimal coordinate placements.

---

## 2. Technology Stack

*   **Language & Framework:** C# 12, .NET 8.0, Windows Presentation Foundation (WPF).
*   **BIM / IFC Processing:** `xBim.Essentials` and `xBim.Geometry` (for coordinate, volume, and shape parsing).
*   **3D Viewport:** `HelixToolkit.Wpf.SharpDX` (DirectX-accelerated 3D rendering).
*   **ORM & Database:** Entity Framework Core 9.0.3 with SQLite provider.
*   **DI Container:** `Microsoft.Extensions.DependencyInjection`.
*   **Testing:** xUnit and Moq.

---

## 3. Architecture Style

The solution follows a strict **Clean Architecture (Onion)** pattern:

```
[UI Layer (WPF, HelixViewport)]
      ↓
[Infrastructure Layer (SQLite, xBIM)] ──→ [Domain Layer (Core Entities, Schemas)]
      ↓                                            ↑
[Application Layer (GA Engine, Services)] ─────────┘
```

> [!NOTE]
> **Decoupled Application Layer:** The Domain project has ZERO dependencies on outer projects or external packages. The Application project has **no project reference** to Infrastructure, communicating solely via domain repository interfaces (`ICraneRepository`, `IProjectRepository`), strictly preserving architectural separation.

---

## 4. Engineering & Optimization Constraints

*   **Setup Location Limits:** Cranes must only be placed in coordinates where they are physically compliant. The `ConstraintEngine` validates each setup coordinate by ensuring the crane footprint is fully within allowed zones and does not overlap obstacles. Coordinates that fail these hard checks are excluded entirely.
*   **Lifting Range:** Lifts must be within $D_e \in [\text{MinRadius}, \text{MaxRadius}]$.
*   **Capacity Constraint:** The weight of the precast element must not exceed the crane's capacity at that operating radius:
    $$\text{AvailableCapacity} \ge \text{LiftingWeight}$$
    Capacity is determined via a **ConservativeStep** lookup of manufacturer load charts (stepping up to the nearest larger radius and selecting the shortest compliant configuration). No linear interpolation is performed.
*   **Precast Concrete Weight:** Fallback concrete density is hardcoded at $2500 \text{ kg/m}^3$ multiplied by volume. Name-based overrides are active for Steel ($7850$), Aluminum ($2700$), Glass ($2500$), and Timber ($600$).

---

## 5. Performance Goals

*   **Smooth UI:** Viewport actions and page navigation must run at 60 FPS. All calculations (solving, parsing) must be offloaded to background tasks.
*   **GA Execution Time:** The GA optimization loop should converge and return results within 2-5 seconds for a standard population of 100 chromosomes over 500 generations.

---

## 6. Coding Standards Summary

1.  **Naming:** `PascalCase` for public members and types, `_camelCase` for private fields, `I` prefix for interfaces. Entities use standard brand names (e.g., `Crane`, `CraneSpecification`). Project database tables use the `Entity` suffix (e.g., `SiteZoneEntity`).
2.  **Asynchrony:** Use `AsyncRelayCommand` in ViewModels. Never use blocking async calls (`.Result`, `.Wait()`).
3.  **DI:** Register all dependencies in `App.xaml.cs`. ViewModels use mixed lifetimes: `MainViewModel` is Transient, `IfcViewerViewModel` and `CraneManagerViewModel` are Singletons, and `ElementManagementViewModel` is Scoped.
4.  **Database Concurrency:** Use `IDbContextFactory<CraneDbContext>` to spin up isolated DbContext connections inside background tasks to prevent thread lockups.
5.  **Typography & Styles:** The UI typeface is **Exo 2**. Brushes are mapped via terracotta `#D68A56` and gray `#757575` tokens defined in `Colors.xaml` and `Brushes.xaml`.
