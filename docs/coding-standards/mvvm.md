# Coding Standards: MVVM Implementation

The user interface of the **Mobile Crane Path Optimizer** follows a strict **Model-View-ViewModel (MVVM)** pattern.

---

## 1. MVVM separation rules

* **Zero UI References in ViewModels:** A ViewModel must never reference UI control classes, Windows, UserControls, or anything in the `System.Windows` namespace (except basic structures like `Visibility` if absolutely required, though converters are preferred).
* **Minimal Code-Behind:** View code-behind (e.g. `DashboardView.xaml.cs`) should contain only constructor components (`InitializeComponent();`). No business logic, calculation routines, or database connections are allowed in code-behind files.
* **No Direct View-ViewModel coupling:** View controls must obtain data solely through **WPF Data Binding** to their corresponding ViewModel properties.

---

## 2. Base Classes

All MVVM components must use the core infrastructure found in [CraneOptimizer.UI.Core](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core):

### ViewModelBase
All viewmodel classes must inherit from `ViewModelBase` and use `SetProperty` to notify changes to the UI:

```csharp
private double _craneX;
public double CraneX
{
    get => _craneX;
    set => SetProperty(ref _craneX, value); // Auto-fires PropertyChanged if value changes
}
```

### RelayCommand & AsyncRelayCommand
* For synchronous actions (like changing pages), use `RelayCommand`.
* For long-running asynchronous actions (like database saves, file loads, and running optimization solvers), use `AsyncRelayCommand`.

---

## 3. Navigation System

Navigation between screens is structured as **ViewModel-First**:

1. **INavigationService:** Configured as a singleton. Tracks the `CurrentView` property (which is a `ViewModelBase`).
2. **Implicit DataTemplates:** Defined in [ViewDataTemplates.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Data/ViewDataTemplates.xaml), which maps ViewModels to Views:
   ```xml
   <DataTemplate DataType="{x:Type vm:DashboardViewModel}">
       <views:DashboardView/>
   </DataTemplate>
   ```
3. **MainWindow Content Control:** Binds its content to the current viewmodel:
   ```xml
   <ContentControl Content="{Binding CurrentView}"/>
   ```
4. **Triggering Page Changes:** Simply call `NavigateTo<TViewModel>()` on the navigation service:
   ```csharp
   _navigationService.NavigateTo<OptimizationViewModel>();
   ```
   The DI container will automatically resolve the transient ViewModel, update the navigation state, and WPF will render the corresponding View using the DataTemplate.
