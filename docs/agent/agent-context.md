# Agent Context & System Reference

This document serves as the primary onboarding and contextual reference for AI coding agents working on the **Mobile Crane Path Optimizer** project.

---

## 1. Project Purpose

The system is a WPF-based engineering desktop application designed to optimize mobile crane positions for erecting precast concrete buildings. It processes building elements from IFC files, maps site obstacles, green setup areas, and site boundaries, and runs a Genetic Algorithm solver to calculate optimal coordinate placements.

---

## 2. Technology Stack

* **Language & Framework:** C# 12, .NET 8.0, Windows Presentation Foundation (WPF).
* **BIM / IFC Processing:** `xBim.Essentials` and `xBim.Geometry` (for coordinate, volume, and shape parsing).
* **3D Viewport:** `HelixToolkit.Wpf.SharpDX` (DirectX-accelerated 3D rendering).
* **ORM & Database:** Entity Framework Core 9.0.3 with SQLite provider.
* **DI Container:** `Microsoft.Extensions.DependencyInjection`.
* **Testing:** xUnit and Moq.

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

> [!WARNING]
> **Dependency Constraint:** The Domain project must have ZERO dependencies on outer projects or external packages. The Application project should only reference Domain (resolving the direct reference to Infrastructure is listed as technical debt).

---

## 4. Engineering & Optimization Constraints

* **Placement Limits:** Cranes must only be placed within specified `GreenArea` polygons. Setup coordinates outside green zones receive a $-50.0$ fitness penalty.
* **Collision Zones:** Crane placement within any `Obstacle` boundary is prohibited and penalized with a $-100.0$ penalty.
* **Boundary Checks:** Placing a crane outside the `SiteBoundary` results in a $-200.0$ penalty.
* **Lifting Range:** Lifts must be within $D_e \in [\text{MinRadius}, \text{MaxRadius}]$.
* **Capacity Constraint:** The weight of the precast element must not exceed the crane's capacity at that operating radius:
  $$\text{Capacity}(D_e) \ge \text{Weight}_e$$
  Capacity is determined via linear interpolation of manufacturer load chart entries.
* **Precast Concrete Weight:** Hardcoded at $2400 \text{ kg/m}^3$ multiplied by volume.

---

## 5. Performance Goals

* **Smooth UI:** Viewport actions and page navigation must run at 60 FPS. All calculations (solving, parsing) must be offloaded to background tasks.
* **GA Execution Time:** The GA optimization loop should converge and return results within 2-5 seconds for a standard population of 100 chromosomes over 500 generations.

---

## 6. Coding Standards Summary

1. **Naming:** `PascalCase` for public members and types, `_camelCase` for private fields, `I` prefix for interfaces.
2. **Asynchrony:** Use `AsyncRelayCommand` in ViewModels. Never use blocking async calls (`.Result`, `.Wait()`).
3. **DI:** Register all dependencies in `App.xaml.cs`. Use singleton lifetimes for services and transient lifetimes for viewmodels.
4. **Data Isolation:** Reset PK properties to 0 during project file saves to allow EF Core to auto-generate correct IDs in the target file.
