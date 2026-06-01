# Workflow: Code Review

This document outlines the code review criteria and standards required before merging any Pull Request (PR) into the `dev` or `main` branches.

---

## 1. Approval Requirements

All Pull Requests targeting `dev` must satisfy the following conditions:
1. **At least 1 passing review** from a peer developer.
2. **Passing CI Build:** The GitHub Actions workflow `.github/workflows/dotnet.yml` must compile successfully and pass all unit tests without errors.
3. **No unresolved conversations:** All code review questions, criticisms, and suggestions must be resolved or explicitly deferred.

---

## 2. Reviewer Checklist

Reviewers must inspect the incoming code changes against these key guidelines:

### Architectural Compliance
* Did the developer add any NuGet packages or outer dependencies to `CraneOptimizer.Domain`? (Should be 0).
* Does `CraneOptimizer.Application` contain UI classes or reference views?
* Are repositories injected via interfaces rather than instantiated directly?

### MVVM and UI Standards
* Are there any UI control references in the ViewModels?
* Is the View code-behind free of calculations, database CRUD, and configuration files?
* Did the developer use `AsyncRelayCommand` for asynchronous actions?

### Code Quality and Thread Safety
* Are there any blocking asynchronous calls (`.Result`, `.Wait()`)?
* Are CPU-heavy calculations (e.g. GA operations, IFC parses) executed on background threads using `Task.Run`?
* Are public properties documented, and private fields correctly prefixed with `_`?

### Persistence and Schema
* If entity models changed, did the developer generate and include the corresponding EF Core migration files?
* Are cascade deletes configured correctly for any new relationships?
* Are ID resets handled correctly for project serialization saves?
