# Coding Standards: MVVM Implementation

The user interface of the **Mobile Crane Path Optimizer** follows a strict **Model-View-ViewModel (MVVM)** pattern.

---

## 1. MVVM separation rules

*   **Zero UI References in ViewModels:** A ViewModel must never reference UI control classes, Windows, UserControls, or anything in the `System.Windows` namespace (except basic structures like `Visibility` if absolutely required, though converters are preferred).
*   **Minimal Code-Behind:** View code-behind (e.g. `IfcViewerControl.xaml.cs`) should contain only constructor components (`InitializeComponent();`) and UI-exclusive mouse event forwarding. No business logic, calculation routines, or database connections are allowed in code-behind files.
*   **No Direct View-ViewModel coupling:** View controls must obtain data solely through **WPF Data Binding** to their corresponding ViewModel properties.

---

## 2. Base Classes

All MVVM components must use the core infrastructure found in [CraneOptimizer.UI/Core](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core):

### ViewModelBase
All viewmodel classes must inherit from `ViewModelBase` and call `OnPropertyChanged` to notify changes to the UI:

```csharp
private double _craneX;
public double CraneX
{
    get => _craneX;
    set
    {
        if (Math.Abs(_craneX - value) > 1e-9)
        {
            _craneX = value;
            OnPropertyChanged(); // Auto-fires PropertyChanged via [CallerMemberName]
        }
    }
}
```

### RelayCommand & AsyncRelayCommand
*   For synchronous actions (like changing selections), use `RelayCommand`.
*   For long-running asynchronous actions (like database saves, file loads, and running optimization solvers), use `AsyncRelayCommand`.

### Implementation Reference

File:
[ViewModelBase.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core/Base/ViewModelBase.cs)

Class:
`ViewModelBase`

---

## 3. Navigation System

Navigation between main screens is structured as **ViewModel-First**:

1.  **IProjectNavigationService:** Configured as a singleton. Tracks navigation state.
2.  **Implicit DataTemplates:** Defined in [ViewDataTemplates.xaml](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Data/ViewDataTemplates.xaml), which maps ViewModels to Views.
3.  **Content Control:** Binds its content to the current active viewmodel.
