# Definition of Done (DoD)

A task or feature branch is considered "Done" and ready to merge into `dev` only when it satisfies all criteria on this checklist.

---

## 1. Code Standards and Design Compliance

* `[ ]` **Clean Architecture:** No dependencies point outward. Core Domain has zero external references.
* `[ ]` **MVVM Compliance:** ViewModels are completely decoupled from WPF views. The View code-behind is clean of business logic.
* `[ ]` **DTO Usage:** Domain database records (`*Record`) are mapped to schema DTOs (`*Schema`, `*Data`) before crossing boundaries for serialization or external parsing.
* `[ ]` **Branding Consistency:** UI changes utilize the Poppins typography keys and shared palette brushes (`Colors.xaml`).

---

## 2. Dependency Registration and Database

* `[ ]` **DI Container Registration:** Any new service or viewmodel is registered with the correct lifetime (`AddSingleton` for services, `AddTransient` for page viewmodels) in `App.xaml.cs`.
* `[ ]` **Database Schema:** If entity structures were modified, a new EF Core migration was generated and applied to the database schema.
* `[ ]` **Cascade Deletes:** Foreign keys and cascading deletes are configured via EF Fluent API for child lists.

---

## 3. Testing and Coverage

* `[ ]` **Unit Tests:** New business logic (e.g. fitness scoring adjustments, conversion functions) is covered by unit tests in `CraneOptimizer.Application.Tests`.
* `[ ]` **Regression Tests:** Bug fixes include a regression unit test demonstrating the fix.
* `[ ]` **Local Run:** All tests in the solution pass successfully.

---

## 4. Documentation and Cleanup

* `[ ]` **Documentation Updates:** Associated architecture, domain, and API documents are updated to reflect code additions.
* `[ ]` **No Compiler Warnings:** The code compiles cleanly under .NET 8.0 without warnings or errors.
* `[ ]` **Git Tracking:** Added documentation or configuration files are tracked in git (not excluded by `.gitignore`).
