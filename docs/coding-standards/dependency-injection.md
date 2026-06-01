# Coding Standards: Dependency Injection

The application uses the standard **Microsoft.Extensions.DependencyInjection** library to implement inversion of control.

All registrations occur in the `ConfigureServices` method of [App.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs).

---

## 1. Service Lifetimes

Choose the correct lifetime based on the component's state and purpose:

### Singletons (`AddSingleton`)
Used for stateless services, caching repositories, or application-wide coordinators:
* **Contexts & Repositories:** `AppDbContext`, `CraneRepository`, `SiteRepository`
* **Application Services:** `CraneService`, `SiteService`, `ProjectService`, `DiagnosticService`
* **Infrastructure Services:** `IfcModelService`
* **UI Infrastructure:** `NavigationService`, `MainWindow`, `MainViewModel`

### Transients (`AddTransient`)
Used for UI pages (ViewModels). This ensures that every time a user navigates away and returns to a screen, the ViewModel is instantiated fresh, preventing memory leaks or stale state accumulation:
* `DashboardViewModel`, `ProjectSetupViewModel`, `SiteManagementViewModel`, `CraneManagementViewModel`, `OptimizationViewModel`, `ResultsViewModel`, `SettingsViewModel`

---

## 2. Factory Navigation Pattern

Because ViewModels are registered as Transients, we cannot inject them directly into the singleton `NavigationService` constructor. Instead, we register a factory delegate in `App.xaml.cs`:

```csharp
services.AddSingleton<Func<Type, ViewModelBase>>(sp => 
    viewModelType => (ViewModelBase)sp.GetRequiredService(viewModelType));
```

This factory is injected into the [NavigationService](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core/NavigationService.cs). When `NavigateTo<TViewModel>()` is called, the navigation service uses this factory to resolve the ViewModel dynamically from the service provider.

---

## 3. Database Injection (Technical Debt Note)

Currently, the `AppDbContext` is registered as a Singleton:
```csharp
services.AddSingleton(new AppDbContext(dbPath));
```
While this works for simple desktop applications where only one user accesses a local SQLite database, it is not thread-safe. Multiple simultaneous queries from background workers (like optimization threads) may cause SQLite locks or entity state corruption.

**Best Practice:**
If queries begin running concurrently, register a factory (`IDbContextFactory<AppDbContext>`) or use transient DbContext resolution to spin up fresh connections per operation.
